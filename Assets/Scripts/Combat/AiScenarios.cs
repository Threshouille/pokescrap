using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// AI test bench, in the test arena (Run.Sandbox): B runs every scenario in turn. Each one sets a situation up
/// (the controlled Pokémon in danger, a strip of danger to walk around, opponents to choose from, the leader falling
/// back...), lets the AIs act for a few seconds, then shows passed or failed with what was measured. Enemies placed by
/// the bench stand still and have no moves, so that only the behaviour under test shows. The emitters of the arena
/// are removed first.
/// Batch (Unity closed): python3 Tools/setup.py --ai-scenarios (Assets/Editor/AiScenarioBatch.cs) starts a play
/// session, runs the bench and quits with the number of failures as exit code; results are logged with the
/// AISCENARIO prefix in Logs/ai_scenarios.log.
public class AiScenarios : MonoBehaviour
{
    /// Set by the batch runner: start the test arena from the menu, run the bench, then report.
    public static bool Requested;
    public static bool Finished { get; private set; }
    public static int Failures { get; private set; }

    static readonly int[] BatchTeam = { 25, 1 }; // Pikachu (controlled), Bulbizarre (ally)
    const string Hazard = "embers"; // harmful terrain painted for the scenarios (no owner: it acts on everyone)
    const float Settle = 1.5f; // seconds given to the AIs after a set-up, before measuring
    const float Choose = 0.4f; // seconds before reading the target an AI chose (before it knocks anything out)

    class Result
    {
        public string name, detail;
        public bool passed;
    }

    readonly List<Result> results = new();
    readonly List<Combatant> placed = new(); // enemies placed by the bench
    PlayerController team;
    bool running;
    string current;
    GUIStyle style;

    // Statics outlive a play session in the editor ("Requested" is set by the batch runner before it starts one).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Finished = false;
        Failures = 0;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Requested && scene.name == Run.MenuScene)
        {
            // Straight into the test arena, without touching the save (no SaveData.SetLastTeam).
            Run.Team = BatchTeam;
            Run.Sandbox = true;
            SceneManager.LoadScene(Run.ArenaScene);
            return;
        }
        if (scene.name == Run.ArenaScene && Run.Sandbox) new GameObject("Banc d'essai IA").AddComponent<AiScenarios>();
    }

    IEnumerator Start()
    {
        yield return null; // the team and the arena are made in their own Start
        team = FindAnyObjectByType<PlayerController>();
        if (Requested) StartCoroutine(RunAll());
    }

    void Update()
    {
        var kb = Keyboard.current;
        if (!running && kb != null && kb.bKey.wasPressedThisFrame) StartCoroutine(RunAll());
    }

    IEnumerator RunAll()
    {
        running = true;
        results.Clear();
        var sandbox = FindAnyObjectByType<TerrainSandbox>();
        if (sandbox != null) sandbox.Quiet();
        if (team.Count < 2) team.AddMember(BatchTeam[1], team.Controlled.transform.position + Vector3.right);

        yield return Scenario("Ne suit pas le Pokémon joué dans le danger", NoFollowIntoDanger());
        yield return Scenario("Contourne un danger pour le rejoindre", WalkAround());
        yield return Scenario("Sans chemin sûr, attend au bord", WaitAtEdge());
        yield return Scenario("Sort d'un terrain nocif", LeaveDanger());
        yield return Scenario("Cible : préfère l'adversaire faible à ses attaques", TargetByType());
        yield return Scenario("Cible : va finir un adversaire presque K.O.", TargetToFinish());
        yield return Scenario("Suit le Pokémon joué qui recule", FollowRetreat());
        yield return Scenario("Blessé, reste près du Pokémon joué", StayCloseWhenHurt());
        yield return Scenario("Gèle une rivière pour rejoindre le Pokémon joué", Repair("water", 6, "path"));
        yield return Scenario("Fait pousser des nénuphars pour traverser", Repair("water", 4, "lily"));
        yield return Scenario("Refait un pont brûlé avec des lianes", Repair("burnt_bridge", 4, "vine"));
        yield return Scenario("Refroidit la lave pour passer", Repair("lava", 3, "any"));
        yield return Scenario("Brûle le pont sous un ennemi au corps à corps", CutBridge());
        yield return Scenario("Marche sur le métal chargé sans s'en soucier", IgnoreMetal());

        Clear();
        Failures = results.FindAll(r => !r.passed).Count;
        Debug.Log($"AISCENARIO DONE {results.Count - Failures}/{results.Count}");
        current = null;
        running = false;
        Finished = true;
    }

    // ---------------------------------------------------------------- scenarios

    // The controlled Pokémon stands in a patch of embers; the ally comes closer but never steps in.
    IEnumerator NoFollowIntoDanger()
    {
        var (leader, ally) = Place(Vector2.zero, new Vector2(7f, 0f));
        Terrain.PaintArea(Hazard, Centre, 2.5f, 60f);
        var watch = new Watch(ally);
        yield return watch.For(6f);
        float distance = Distance(ally, leader);
        Report(watch.InDanger <= 2 && distance <= 5.5f, $"{watch.InDanger} images dans le danger, finit à {distance:0.0} case(s)");
    }

    // A strip of embers between the ally and the controlled Pokémon, open at one end: the ally walks round.
    IEnumerator WalkAround()
    {
        var (leader, ally) = Place(new Vector2(6f, 0f), new Vector2(-1f, 0f));
        for (float y = -7f; y <= 4f; y += 0.8f) Terrain.PaintArea(Hazard, Centre + new Vector2(3f, y), 0.7f, 60f);
        var watch = new Watch(ally);
        yield return watch.For(12f, () => Distance(ally, leader) <= 3f);
        float distance = Distance(ally, leader);
        Report(watch.InDanger <= 2 && distance <= 3f, $"{watch.InDanger} images dans le danger, finit à {distance:0.0} case(s) en {watch.Elapsed:0.0} s");
    }

    // The controlled Pokémon is ringed by embers: no safe way in, the ally waits outside.
    IEnumerator WaitAtEdge()
    {
        var (leader, ally) = Place(Vector2.zero, new Vector2(8f, 0f));
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI * 2f / 24f;
            Terrain.PaintArea(Hazard, Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.5f, 0.9f, 60f);
        }
        var watch = new Watch(ally);
        yield return watch.For(6f);
        float distance = Distance(ally, leader);
        Report(watch.InDanger <= 2 && distance <= 6.5f, $"{watch.InDanger} images dans le danger, attend à {distance:0.0} case(s)");
    }

    // The ally starts on embers: it gets out quickly.
    IEnumerator LeaveDanger()
    {
        var (_, ally) = Place(new Vector2(-6f, 0f), new Vector2(2f, 0f), settle: false);
        Terrain.PaintArea(Hazard, Centre + new Vector2(2f, 0f), 2f, 60f);
        float started = Time.time, outAt = -1f;
        while (Time.time - started < 4f)
        {
            yield return new WaitForFixedUpdate();
            if (!Terrain.Hazard(ally.transform.position, ally)) { outAt = Time.time - started; break; }
        }
        Report(outAt >= 0f && outAt <= 2f, outAt >= 0f ? $"dehors en {outAt:0.0} s" : "toujours dans le danger après 4 s");
    }

    // Two opponents: one neutral and nearer, one weak to the ally's moves and farther: the ally picks the weak one.
    IEnumerator TargetByType()
    {
        var (_, ally) = Place(new Vector2(-3f, -3f), Vector2.zero);
        int type = BestMoveType(ally);
        int weak = FindSpecies(t => TypeChart.Multiplier(type, t, out _) >= 2f);
        int neutral = FindSpecies(t => Mathf.Approximately(TypeChart.Multiplier(type, t, out _), 1f));
        if (type < 0 || weak < 0 || neutral < 0)
        {
            Report(false, "pas d'attaque offensive ou pas d'espèce adaptée");
            yield break;
        }
        var near = Enemy(neutral, new Vector2(3f, 0f));
        var far = Enemy(weak, new Vector2(0f, 5f));
        near.maxHp = near.hp = far.maxHp = far.hp = 100000f; // health aside: only the types tell them apart
        yield return new WaitForSeconds(Choose);
        var chosen = TargetChoice.Current(ally);
        Report(chosen == far, $"attaque {GameData.TypeName(type)} ; près {Name(near)}, loin {Name(far)} ; choisit {Name(chosen)}");
    }

    // Two identical opponents, the farther one almost knocked out: the ally goes for it.
    IEnumerator TargetToFinish()
    {
        var (_, ally) = Place(new Vector2(-3f, -3f), Vector2.zero);
        int type = BestMoveType(ally);
        int species = FindSpecies(t => Mathf.Approximately(TypeChart.Multiplier(type, t, out _), 1f));
        var near = Enemy(species, new Vector2(3f, 0f));
        var far = Enemy(species, new Vector2(0f, 6f));
        far.hp = far.maxHp * 0.1f;
        yield return new WaitForSeconds(Choose);
        var chosen = TargetChoice.Current(ally);
        bool finished = !far.IsAlive && near.hp >= near.maxHp; // already knocked out, and not the other one hit
        Report(chosen == far || finished, $"choisit {(chosen == far || finished ? "celui presque K.O." : chosen == near ? "le plus proche" : "personne")}");
    }

    // The ally fights an opponent next to the controlled Pokémon, which then walks away: the ally follows.
    IEnumerator FollowRetreat()
    {
        var (leader, ally) = Place(Vector2.zero, new Vector2(1.5f, 0f));
        var foe = Enemy(BatchTeam[1], new Vector2(3f, 0f));
        foe.maxHp = foe.hp = 100000f; // stays up for the whole scenario
        yield return new WaitForSeconds(Settle);
        float started = Time.time;
        var body = leader.GetComponent<Rigidbody2D>();
        while (Time.time - started < 3f)
        {
            yield return new WaitForFixedUpdate();
            Vector2 to = Centre + new Vector2(-3f * (Time.time - started), 0f);
            body.position = to;
            leader.transform.position = to;
        }
        yield return new WaitForSeconds(2f);
        float distance = Distance(ally, leader);
        Report(distance <= 4f, $"à {distance:0.0} case(s) du Pokémon joué, {Vector2.Distance(ally.transform.position, foe.transform.position):0.0} de l'adversaire");
    }

    // A hurt ally does not walk out to an opponent six tiles off: it keeps close to the controlled Pokémon.
    IEnumerator StayCloseWhenHurt()
    {
        var (leader, ally) = Place(Vector2.zero, new Vector2(1f, 1f));
        ally.hp = ally.maxHp * 0.2f;
        Enemy(BatchTeam[1], new Vector2(6.5f, 0f));
        var watch = new Watch(ally, leader);
        yield return watch.For(4f);
        ally.hp = ally.maxHp;
        Report(watch.FarthestFromLeader <= 4f, $"au plus loin à {watch.FarthestFromLeader:0.0} case(s) du Pokémon joué");
    }

    // A river (or lava, or a burnt bridge over water) three tiles wide from wall to wall between the ally and the
    // controlled Pokémon; the ally gets a move of "type" only: it changes the obstacle to get across.
    IEnumerator Repair(string obstacle, int type, string kind)
    {
        var (leader, ally) = Place(new Vector2(5f, 0f), new Vector2(-4f, 0f));
        var grid = DungeonGenerator.Current;
        var centre = grid.CellOf(Centre);
        var inside = grid.Interior;
        for (int x = centre.x - 1; x <= centre.x + 1; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                Obstacles.Place(x, y, obstacle == "burnt_bridge" && Mathf.Abs(y - centre.y) > 1 ? "water" : obstacle);
        var move = FindMove(type, m => kind == "path" ? m.family == "projectile" && m.path != "arc"
            : m.family == "projectile" || m.family == "zone" || m.family == "melee");
        if (move == null)
        {
            Report(false, $"pas d'attaque de type {GameData.TypeName(type)}");
            yield break;
        }
        var kept = (MoveDef[])ally.Moves.Clone();
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, i == 0 ? move : null);
        var watch = new Watch(ally);
        yield return watch.For(20f, () => Distance(ally, leader) <= 3f);
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, kept[i]);
        float distance = Distance(ally, leader);
        Report(distance <= 3f, $"{move.name} : {(distance <= 3f ? $"de l'autre côté en {watch.Elapsed:0.0} s" : $"bloqué à {distance:0.0} case(s)")}");
    }

    // An opponent that fights at close range stands on a bridge over water; the ally has a Fire move: it burns
    // the bridge under it.
    IEnumerator CutBridge()
    {
        var (_, ally) = Place(new Vector2(-6f, -4f), new Vector2(-3f, 0f));
        var grid = DungeonGenerator.Current;
        var centre = grid.CellOf(Centre);
        var inside = grid.Interior;
        for (int x = centre.x - 1; x <= centre.x + 1; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                Obstacles.Place(x, y, Mathf.Abs(y - centre.y) <= 0 ? "bridge" : "water");
        var fire = FindMove(2, m => m.family == "projectile" && m.path != "arc");
        var hit = FindMove(1, m => m.family == "melee");
        var foe = Enemy(BatchTeam[1], Vector2.zero);
        foe.maxHp = foe.hp = 100000f;
        if (hit != null) foe.LearnMove(0, hit); // fights at close range
        var kept = (MoveDef[])ally.Moves.Clone();
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, i == 0 ? fire : null);
        float started = Time.time;
        bool burnt = false;
        while (Time.time - started < 6f && !burnt)
        {
            yield return new WaitForFixedUpdate();
            burnt = Obstacles.At(centre.x, centre.y)?.key != "bridge";
        }
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, kept[i]);
        Report(burnt, burnt ? $"{fire.name} : pont en feu après {Time.time - started:0.0} s" : $"le pont tient toujours ({fire?.name})");
    }

    // A strip of charged metal between the ally and the controlled Pokémon, with a way round: the ally does not
    // bother and walks across.
    IEnumerator IgnoreMetal()
    {
        var (leader, ally) = Place(new Vector2(5f, 0f), new Vector2(-4f, 0f));
        var grid = DungeonGenerator.Current;
        var centre = grid.CellOf(Centre);
        for (int x = centre.x - 1; x <= centre.x + 1; x++)
            for (int y = centre.y - 4; y <= centre.y + 4; y++)
                Obstacles.Place(x, y, "metal");
        Terrain.PaintArea("charged_metal", Centre, 3f, 60f);
        int onMetal = 0;
        float started = Time.time;
        while (Time.time - started < 8f && Distance(ally, leader) > 3f)
        {
            yield return new WaitForFixedUpdate();
            if (Obstacles.At(ally.transform.position)?.key == "metal") onMetal++;
        }
        Report(onMetal > 0 && Distance(ally, leader) <= 3f, $"{onMetal} images sur le métal, arrivé en {Time.time - started:0.0} s");
    }

    // ---------------------------------------------------------------- helpers

    // A move of that type with power that passes the test, or null.
    static MoveDef FindMove(int type, Func<MoveDef, bool> fits)
    {
        for (int id = 1; id < 1000; id++)
        {
            var m = GameData.Move(id);
            if (m != null && m.type == type && m.power > 0 && !m.basic && fits(m)) return m;
        }
        return null;
    }

    IEnumerator Scenario(string name, IEnumerator body)
    {
        current = name;
        Clear();
        yield return new WaitForFixedUpdate();
        int before = results.Count;
        yield return body;
        if (results.Count == before) Report(false, "pas de résultat");
        results[^1].name = name;
        var r = results[^1];
        Debug.Log($"AISCENARIO {(r.passed ? "PASS" : "FAIL")} {name} : {r.detail}");
    }

    void Report(bool passed, string detail) => results.Add(new Result { passed = passed, detail = detail });

    static Vector2 Centre
    {
        get
        {
            var arena = DungeonGenerator.Current;
            return arena.CentreOf(arena.Size / 2, arena.Size / 2);
        }
    }

    // Puts the controlled Pokémon and the first ally at these offsets from the centre, full health, at rest.
    (Combatant leader, Combatant ally) Place(Vector2 leaderAt, Vector2 allyAt, bool settle = true)
    {
        var leader = team.Controlled;
        Combatant ally = null;
        foreach (var m in team.Members)
            if (m != leader && m != null) { ally = m; break; }
        foreach (var c in team.Members)
        {
            if (c == null) continue;
            c.hp = c.maxHp;
            Teleport(c, Centre + (c == leader ? leaderAt : c == ally ? allyAt : new Vector2(-10f, 10f)));
        }
        team.transform.position = leader.transform.position;
        ally.GetComponent<AllyAI>().ForgetLeaderMotion();
        return (leader, ally);
    }

    static void Teleport(Combatant c, Vector2 at)
    {
        var body = c.GetComponent<Rigidbody2D>();
        body.position = at;
        body.linearVelocity = Vector2.zero;
        c.transform.position = at;
    }

    // An enemy that stands still and has no moves.
    Combatant Enemy(int pokemonId, Vector2 offset)
    {
        var go = new GameObject($"Banc : {GameData.Pokemon(pokemonId)?.name}");
        go.transform.position = Centre + offset;
        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        go.AddComponent<PmdAnimator>().SetAnimSet(PokemonRoster.Get(pokemonId));
        var c = go.AddComponent<Combatant>();
        c.Init(pokemonId, Team.Enemy, new int[0]);
        go.AddComponent<HealthBar>();
        placed.Add(c);
        return c;
    }

    void Clear()
    {
        foreach (var c in placed)
            if (c != null) Destroy(c.gameObject);
        placed.Clear();
        foreach (var zone in FindObjectsByType<Zone>()) Destroy(zone.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        if (DungeonGenerator.Current != null)
        {
            Terrain.Rebuild(DungeonGenerator.Current);
            Obstacles.Rebuild(DungeonGenerator.Current);
        }
    }

    static float Distance(Combatant a, Combatant b) => Vector2.Distance(a.transform.position, b.transform.position);

    static string Name(Combatant c) => c != null ? c.Stats.name : "personne";

    // Type of the ally's strongest damaging move, or -1.
    static int BestMoveType(Combatant c)
    {
        MoveDef best = null;
        foreach (var m in c.Moves)
            if (m != null && m.power > 0 && (best == null || m.power > best.power)) best = m;
        return best != null ? best.type : -1;
    }

    // First species with sprites whose types pass the test.
    static int FindSpecies(Func<int[], bool> fits)
    {
        for (int id = 1; id <= 493; id++)
        {
            var p = GameData.Pokemon(id);
            if (p != null && p.types != null && p.types.Length > 0 && PokemonRoster.Has(id) && fits(p.types)) return id;
        }
        return -1;
    }

    // Follows an ally while a scenario runs: frames spent on a hazard, farthest distance from the leader.
    class Watch
    {
        readonly Combatant ally, leader;
        public int InDanger;
        public float FarthestFromLeader, Elapsed;

        public Watch(Combatant ally, Combatant leader = null)
        {
            this.ally = ally;
            this.leader = leader;
        }

        public IEnumerator For(float seconds, Func<bool> done = null)
        {
            float started = Time.time;
            while (Time.time - started < seconds)
            {
                yield return new WaitForFixedUpdate();
                Elapsed = Time.time - started;
                Vector2 at = ally.transform.position;
                if (Terrain.Hazard(at, ally)) InDanger++;
                if (leader != null) FarthestFromLeader = Mathf.Max(FarthestFromLeader, Vector2.Distance(at, leader.transform.position));
                if (done != null && done()) yield break;
            }
        }
    }

    void OnGUI()
    {
        if (!running && results.Count == 0)
        {
            Line(0, "B : banc d'essai des IA", Color.white);
            return;
        }
        int row = 0;
        foreach (var r in results)
            Line(row++, $"{(r.passed ? "OK " : "ÉCHEC")}  {r.name} — {r.detail}", r.passed ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.5f, 0.4f));
        if (running && current != null) Line(row, $"…  {current}", Color.white);
        else if (!running) Line(row, $"{results.Count - Failures}/{results.Count} réussis   (B : relancer)", Color.white);
    }

    void Line(int row, string text, Color colour)
    {
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        style ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(7f * s) };
        var rect = new Rect(8f * s, 40f * s + row * 10f * s, Screen.width, 10f * s);
        var previous = GUI.color;
        GUI.color = Color.black;
        GUI.Label(new Rect(rect.x + s, rect.y + s, rect.width, rect.height), text, style);
        GUI.color = colour;
        GUI.Label(rect, text, style);
        GUI.color = previous;
    }
}
