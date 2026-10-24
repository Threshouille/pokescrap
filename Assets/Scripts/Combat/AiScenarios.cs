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
        yield return Scenario("Refait le pont pour atteindre un ennemi", RepairTowardFoe());
        yield return Scenario("Refroidit la lave pour passer", Repair("lava", 3, "any"));
        yield return Scenario("Brûle le pont sous un ennemi au corps à corps", CutBridge());
        yield return Scenario("Marche sur le métal chargé sans s'en soucier", IgnoreMetal());
        yield return Scenario("Objets : la capacité Z frappe et paralyse tous les ennemis visibles", ZMoveHits());
        yield return Scenario("Objets : la capacité Z épargne les alliés (chaque cristal)", ZSparesAllies());
        yield return Scenario("Élite : gèle une rivière pour rejoindre sa cible", EliteFreezesRiver());
        yield return Scenario("Élite : brûle le pont sous un adversaire au corps à corps", EliteBurnsBridge());
        yield return Scenario("Ultime ennemi : se téracristallise dans un type qui résiste", EnemyTera());
        yield return Scenario("Ultime ennemi : capacité Z quand l'équipe est groupée", EnemyZ());
        yield return Scenario("Ultime ennemi : méga-évolue quand un adversaire approche", EnemyMega());
        yield return Scenario("Ultime ennemi : Téra Spectre pour traverser un mur de rochers", EnemyTeraCrosses());
        yield return Scenario("Objets : le Téracristal change le type", TeraChangesType());
        yield return Scenario("Objets : une méga-gemme obtenue ne retombe plus", StonesStayUnique());
        yield return Scenario("Objets : les types de l'équipe tombent plus souvent", LootFavoursTeam());

        // Rescue: a third member joins (last, so that the scenarios above keep a team of two).
        if (team.Count < 3) third = team.AddMember(Salameche, team.Controlled.transform.position + Vector3.left);
        yield return Scenario("Secours : un allié réanime le Pokémon K.O.", AllyRevives());
        yield return Scenario("Secours : le plus apte éloigne l'ennemi du Pokémon K.O.", GuardDrawsAway());
        yield return Scenario("Soins : soigne un coéquipier presque K.O.", HealsLowMate());
        yield return Scenario("Soins : blessé, ne va pas soigner au milieu des ennemis", NoRiskyHeal());
        yield return Scenario("Rencontres : le budget de chaque vague est respecté", EncounterBudget());
        yield return Scenario("Rencontres : une meute compte 4 à 6 fois le même Pokémon", PackIsKin());
        yield return Scenario("Rencontres : un gang de 3, rares ou épiques, chacun avec un objet", GangCarriesItems());
        yield return Scenario("Rencontres : zones peuplées typiques par étape", TypicalZones());
        yield return Scenario("Rencontres : une meute vient parfois avec un solitaire rare ou épique", PackWithLeader());
        yield return Scenario("Groupe : pas d'à-coups dans une meute de 6 et son chef", CrowdStaysSteady());
        yield return Scenario("Élite : un champion très fort, plus grand, avec objet et butin garanti", ChampionStands());
        yield return Scenario("Élite : une escouade de 3, seul le chef porte l'objet, butin quand tous tombent", SquadLoot());
        yield return Scenario("Élite : au plus un par étape, plus souvent en avançant", EliteOdds());
        yield return Scenario("Eau : poussé dedans, un Pokémon qui ne nage pas revient, un nageur y reste", PushedIntoWater());
        yield return Scenario("Ultime ennemi : Téra pour se déplacer sur la lave qui l'entoure", EnemyTeraForWater());
        yield return Scenario("Rencontres : une équipe a ses rôles et des types qui se combinent", TeamHasRoles());
        yield return Scenario("Rencontres : les rangs montent avec l'étape", RanksRise());
        yield return Scenario("Rencontres : les Pokémon vont avec le décor", FitsDecor());
        yield return Scenario("Rencontres : apparition avec rang, anneau et objet", EncounterSpawns());

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

    // The controlled Pokémon carries an Electric Z crystal and knows an Electric move; its Z move must strike both
    // opponents on screen, near and behind it, far harder than one hit of the move, and paralyse them.
    IEnumerator ZMoveHits()
    {
        var (leader, _) = Place(Vector2.zero, new Vector2(-4f, -3f), settle: false);
        var move = FindMove(5, m => m.family == "projectile" && m.path != "arc");
        if (move == null) { Report(false, "pas d'attaque Électrik"); yield break; }
        var kept = (MoveDef[])leader.Moves.Clone();
        var keptItem = leader.Special;
        leader.LearnMove(1, move);
        leader.Special = new SpecialItems.Equipped { family = SpecialItems.Family.Z, type = 5, level = 1 };
        var near = Enemy(BatchTeam[1], new Vector2(3f, 0f));
        var far = Enemy(BatchTeam[1], new Vector2(-4f, 2.5f));
        foreach (var foe in new[] { near, far }) foe.maxHp = foe.hp = 100000f;
        yield return new WaitForFixedUpdate();
        float one = leader.EstimateHit(move, near);
        bool triggered = team.GetComponent<Ultimates>().Trigger(leader);
        yield return new WaitForSeconds(SpecialItems.ZPose + 0.5f);
        float a = near.maxHp - near.hp, b = far.maxHp - far.hp;
        bool stuck = near.HasStatus("paralysis") && far.HasStatus("paralysis");
        for (int i = 0; i < Combatant.MoveSlots; i++) leader.LearnMove(i, kept[i]);
        leader.Special = keptItem;
        Report(triggered && stuck && one > 0f && a >= one * 2.5f && b >= one * 2.5f,
            $"{(triggered ? "lancée" : "refusée")} : {a:0} et {b:0} dégâts (un coup de {move.name} : ≈ {one:0}), paralysie {(stuck ? "sur les deux" : "manquante")}");
    }

    // An ally stands right by the opponent while each Z crystal in turn strikes it: the ally must lose no health, get
    // no status, penalty or stun, stand on no terrain that acts on it, and still follow the controlled Pokémon.
    IEnumerator ZSparesAllies()
    {
        var (leader, ally) = Place(Vector2.zero, new Vector2(2.4f, 0.6f), settle: false);
        var keptItem = leader.Special;
        bool keptDemo = Ultimates.DemoMoves;
        Ultimates.DemoMoves = true; // Pikachu borrows a move of each type
        var foe = Enemy(BatchTeam[1], new Vector2(3f, 0f));
        foe.maxHp = foe.hp = 1000000f;
        var ultimates = team.GetComponent<Ultimates>();
        var faults = new List<string>();
        foreach (var crystal in SpecialItems.Crystals)
        {
            ally.hp = ally.maxHp;
            leader.Special = new SpecialItems.Equipped { family = SpecialItems.Family.Z, type = crystal.type, level = 3 };
            yield return new WaitForFixedUpdate();
            if (!ultimates.Trigger(leader)) { faults.Add($"{crystal.name} refusé"); continue; }
            yield return new WaitForSeconds(SpecialItems.ZPose + 0.6f);
            var wrong = new List<string>();
            if (ally.hp < ally.maxHp) wrong.Add("PV");
            if (ally.Modifier("attack") < 0.999f || ally.Modifier("defense") < 0.999f || ally.Modifier("speed") < 0.999f) wrong.Add("malus");
            foreach (var key in new[] { "poison", "paralysis", "burn", "sleep", "freeze", "confusion", "trap", "yawn", "leech-seed" })
                if (ally.HasStatus(key)) wrong.Add(key);
            if (ally.IsHurt) wrong.Add("étourdi");
            if (Terrain.Affecting(ally) != null) wrong.Add($"terrain {Terrain.Affecting(ally).key}");
            if (wrong.Count > 0) faults.Add($"{crystal.name} : {string.Join(", ", wrong)}");
        }
        // Then the ally still follows: the controlled Pokémon walks away.
        leader.transform.position = Centre + new Vector2(-6f, 0f);
        yield return new WaitForSeconds(4f);
        float distance = Distance(ally, leader);
        if (distance > 4f) faults.Add($"l'allié ne suit plus ({distance:0.0} cases)");
        leader.Special = keptItem;
        Ultimates.DemoMoves = keptDemo;
        Report(faults.Count == 0, faults.Count == 0 ? $"{SpecialItems.Crystals.Count} cristaux, allié intact et toujours à {distance:0.0} case(s)" : string.Join(" ; ", faults));
    }

    // The controlled Pokémon (Pikachu, Électrik) carries a Water Tera shard: once Terastallized it is Water, and an
    // Électrik move hits it twice as hard instead of half.
    IEnumerator TeraChangesType()
    {
        var (leader, _) = Place(Vector2.zero, new Vector2(-4f, -3f), settle: false);
        var keptItem = leader.Special;
        leader.Special = new SpecialItems.Equipped { family = SpecialItems.Family.Tera, type = 3, level = 1 };
        float before = TypeChart.Multiplier(5, leader.Stats.types, out _);
        yield return new WaitForFixedUpdate();
        bool triggered = team.GetComponent<Ultimates>().Trigger(leader);
        yield return new WaitForSeconds(0.8f);
        float after = TypeChart.Multiplier(5, leader.Stats.types, out _);
        int tera = leader.TeraType;
        leader.RevertTera();
        leader.Special = keptItem;
        Report(triggered && tera == 3 && after > before, $"type Téra {tera}, Électrik contre lui : ×{before} → ×{after}");
    }

    // With every stone owned but one, a boss's roll gives that one; with all owned, none.
    IEnumerator StonesStayUnique()
    {
        var rng = new System.Random(3);
        string left = SpecialItems.Stones[5].key;
        bool always = true;
        for (int n = 0; n < 50; n++)
            always &= Loot.RollStone(rng, new[] { 1 }, key => key != left)?.stone == left;
        bool none = Loot.RollStone(rng, new[] { 1 }, _ => true) == null;
        Report(always && none, $"dernière gemme toujours tirée : {always}, aucune quand tout est possédé : {none}");
        yield break;
    }

    // Chests favour the team's types (teamTypeWeight).
    IEnumerator LootFavoursTeam()
    {
        var rng = new System.Random(4);
        var types = new HashSet<int> { 4, 8 };
        int mine = 0, rolls = 2000;
        for (int n = 0; n < rolls; n++)
            if (types.Contains(Loot.RollTyped(rng, types).type)) mine++;
        float share = mine / (float)rolls, fair = 2f / SpecialItems.Crystals.Count;
        Report(share > fair * 1.8f, $"{share:P0} des tirages pour les 2 types de l'équipe (au hasard : {fair:P0})");
        yield break;
    }

    // ---------------------------------------------------------------- elites and enemy ultimates

    const int Lokhlass = 131, OndeBoreale = 42, LanceFlamme = 53;

    // A fighting elite (EnemyAI, ultimate) with these moves, too sturdy to fall during a scenario.
    Combatant Elite(Vector2 offset, params int[] moves)
    {
        var spawner = FindAnyObjectByType<EnemySpawner>();
        var c = spawner != null ? spawner.SpawnFighter(Lokhlass, moves, Centre + offset, false, true, 5, 1000f) : null;
        if (c != null) placed.Add(c);
        return c;
    }

    // The team cannot fall during a scenario where an elite fights back.
    static float[] Sturdy(params Combatant[] members)
    {
        var kept = new float[members.Length];
        for (int i = 0; i < members.Length; i++)
        {
            kept[i] = members[i].maxHp;
            members[i].maxHp = members[i].hp = 100000f;
        }
        return kept;
    }

    static void Restore(float[] kept, params Combatant[] members)
    {
        for (int i = 0; i < members.Length; i++) members[i].maxHp = members[i].hp = kept[i];
    }

    void River(int from, int to, bool bridge)
    {
        var grid = DungeonGenerator.Current;
        var centre = grid.CellOf(Centre);
        var inside = grid.Interior;
        for (int x = centre.x + from; x <= centre.x + to; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                Obstacles.Place(x, y, bridge && y == centre.y ? "bridge" : "water");
    }

    // A river from wall to wall between the elite and the controlled Pokémon, too far to be shot at from the bank;
    // the elite only has Onde Boréale: it freezes the river to come across.
    IEnumerator EliteFreezesRiver()
    {
        var (leader, ally) = Place(new Vector2(-11f, 0f), new Vector2(-11f, 2f), settle: false);
        var kept = Sturdy(leader, ally);
        River(1, 3, false);
        var elite = Elite(new Vector2(7f, 0f), OndeBoreale);
        if (elite == null) { Restore(kept, leader, ally); Report(false, "pas d'élite (sprites de Lokhlass ?)"); yield break; }
        float riverX = DungeonGenerator.Current.CentreOf(DungeonGenerator.Current.CellOf(Centre).x + 1, 0).x - 0.5f;
        float started = Time.time;
        while (Time.time - started < 25f && elite != null && elite.transform.position.x > riverX) yield return new WaitForFixedUpdate();
        bool crossed = elite != null && elite.transform.position.x <= riverX;
        Restore(kept, leader, ally);
        Report(crossed, crossed ? $"de l'autre côté en {Time.time - started:0.0} s"
            : $"toujours de son côté de la rivière (à {(elite != null ? elite.transform.position.x - riverX : 0f):0.0} case(s) de la rive)");
    }

    // The controlled Pokémon fights at close range and stands on a bridge; the elite only has Lance-Flamme: it burns
    // the bridge under it.
    IEnumerator EliteBurnsBridge()
    {
        var (leader, ally) = Place(new Vector2(2f, 0f), new Vector2(-10f, 8f), settle: false);
        var kept = Sturdy(leader, ally);
        var keptMoves = (MoveDef[])leader.Moves.Clone();
        var hit = FindMove(1, m => m.family == "melee");
        for (int i = 0; i < Combatant.MoveSlots; i++) leader.LearnMove(i, i == 0 ? hit : null); // fights at close range
        River(1, 3, true);
        var grid = DungeonGenerator.Current;
        var cell = grid.CellOf(Centre + new Vector2(2f, 0f));
        var elite = Elite(new Vector2(6.5f, 0f), LanceFlamme);
        float started = Time.time;
        bool burnt = false;
        while (Time.time - started < 10f && !burnt)
        {
            yield return new WaitForFixedUpdate();
            burnt = Obstacles.At(cell.x, cell.y)?.key != "bridge";
        }
        for (int i = 0; i < Combatant.MoveSlots; i++) leader.LearnMove(i, keptMoves[i]);
        Restore(kept, leader, ally);
        Report(elite != null && burnt, burnt ? $"pont en feu après {Time.time - started:0.0} s" : "le pont tient toujours");
    }

    // Pikachu (Électrik) and Bulbizarre face a hurt Lokhlass (Eau / Glace, weak to both): it Terastallizes into a type
    // that takes their moves better.
    IEnumerator EnemyTera()
    {
        var (leader, ally) = Place(new Vector2(-4f, 0f), new Vector2(-4f, 2f), settle: false);
        var kept = Sturdy(leader, ally);
        var elite = Elite(new Vector2(4f, 0f), LanceFlamme);
        if (elite == null) { Restore(kept, leader, ally); Report(false, "pas d'élite"); yield break; }
        elite.hp = elite.maxHp * 0.4f;
        var ultimate = elite.GetComponent<EnemyUltimate>();
        float before = IncomingMultiplier(elite);
        bool used = ultimate.Decide();
        elite.hp = elite.maxHp * 0.1f;
        bool again = ultimate.Decide(); // one item, one ultimate
        yield return new WaitForSeconds(0.8f);
        float after = IncomingMultiplier(elite);
        int tera = elite.TeraType;
        Restore(kept, leader, ally);
        Report(used && !again && ultimate.LastUsed == "tera" && after < before - 0.15f,
            $"{ultimate.LastUsed} : type {(tera >= 0 ? GameData.TypeName(tera) : "aucun")}, attaques de l'équipe ×{before:0.00} → ×{after:0.00}");
    }

    // An enemy whose line has a mega (sprites included) carries its stone: once the team is near, it mega evolves,
    // takes the mega's form and sprites.
    IEnumerator EnemyMega()
    {
        var (leader, ally) = Place(new Vector2(-3f, 0f), new Vector2(-3f, 2f), settle: false);
        var kept = Sturdy(leader, ally);
        MegaDef mega = null;
        foreach (var st in SpecialItems.Stones)
        {
            var m = GameData.Mega(st.mega);
            if (m != null && PokemonRoster.Has(m.id) && PokemonRoster.Has(m.@base)) { mega = m; break; }
        }
        var spawner = FindAnyObjectByType<EnemySpawner>();
        var foe = mega != null && spawner != null ? spawner.SpawnFighter(mega.@base, null, Centre + new Vector2(2f, 0f), false, false, 5, 1000f) : null;
        if (foe == null) { Restore(kept, leader, ally); Report(false, "pas de Pokémon à méga"); yield break; }
        placed.Add(foe);
        var ultimate = foe.gameObject.AddComponent<EnemyUltimate>();
        ultimate.Carry(SpecialItems.Family.Mega, -1, mega);
        bool used = ultimate.Decide();
        yield return new WaitForSeconds(0.5f);
        bool form = foe.Mega == mega && foe.Stats.name == mega.name;
        Restore(kept, leader, ally);
        Report(used && ultimate.LastUsed == "mega" && form, $"{(used ? ultimate.LastUsed : "rien")} : {foe.Stats.name}");
    }

    // A wall of rocks from wall to wall between an elite and the team: only a Spectre goes through. It picks the
    // Spectre shard, Terastallizes as soon as the wall stops it, and walks through.
    IEnumerator EnemyTeraCrosses()
    {
        var (leader, ally) = Place(new Vector2(-6f, 0f), new Vector2(-6f, 2f), settle: false);
        var kept = Sturdy(leader, ally);
        var grid = DungeonGenerator.Current;
        var centre = grid.CellOf(Centre);
        var inside = grid.Interior;
        for (int y = inside.yMin; y < inside.yMax; y++) Obstacles.Place(centre.x + 1, y, "boulder");
        var elite = Elite(new Vector2(5f, 0f), LanceFlamme);
        if (elite == null) { Restore(kept, leader, ally); Report(false, "pas d'élite"); yield break; }
        var ultimate = elite.GetComponent<EnemyUltimate>();
        ultimate.ChooseItem();
        string picked = ultimate.Item == SpecialItems.Family.Tera ? GameData.TypeName(ultimate.ItemType) : ultimate.Item.ToString();
        bool used = ultimate.Decide();
        float wall = grid.CentreOf(centre.x + 1, centre.y).x - 0.5f;
        float started = Time.time;
        while (Time.time - started < 12f && elite != null && elite.transform.position.x > wall) yield return new WaitForFixedUpdate();
        bool through = elite != null && elite.transform.position.x <= wall;
        Restore(kept, leader, ally);
        Report(used && elite.TeraType == 14 && through,
            $"objet : {picked}, {(used ? "téracristallisé" : "pas utilisé")}, {(through ? "a traversé le mur" : "bloqué derrière le mur")}");
    }

    // Lava all around an elite (Lokhlass swims, but lava stops it), the team on dry land in clear sight: it picks a
    // Tera shard that gets about the lava (Feu, Vol or Spectre) and uses it once the team is near.
    IEnumerator EnemyTeraForWater()
    {
        var (leader, ally) = Place(new Vector2(-2.5f, 0f), new Vector2(-2.5f, 2f), settle: false);
        var kept = Sturdy(leader, ally);
        var grid = DungeonGenerator.Current;
        var c = grid.CellOf(Centre + new Vector2(4f, 0f));
        for (int x = c.x - 6; x <= c.x + 6; x++)
            for (int y = c.y - 6; y <= c.y + 6; y++)
            {
                int d = Mathf.Max(Mathf.Abs(x - c.x), Mathf.Abs(y - c.y));
                if (d >= 2 && d <= 5 && !(y == c.y && x < c.x)) Obstacles.Place(x, y, "lava"); // a way out to the team
            }
        var elite = Elite(new Vector2(4f, 0f), LanceFlamme);
        if (elite == null) { Restore(kept, leader, ally); Report(false, "pas d'élite"); yield break; }
        var ultimate = elite.GetComponent<EnemyUltimate>();
        ultimate.ChooseItem();
        int type = ultimate.Item == SpecialItems.Family.Tera ? ultimate.ItemType : -1;
        bool walksOnWater = type == 2 || type == 10 || type == 14; // over the lava
        bool used = ultimate.Decide();
        yield return new WaitForSeconds(0.8f);
        Restore(kept, leader, ally);
        Report(walksOnWater && ultimate.TeraForTerrain && used && elite.TeraType == type,
            $"objet : {(type >= 0 ? "Téra " + GameData.TypeName(type) : ultimate.Item.ToString())}, {(ultimate.TeraForTerrain ? "pour le terrain" : "pour la défense")}, {(used ? "utilisé" : "pas utilisé")}");
    }

    static float IncomingMultiplier(Combatant target)
    {
        float sum = 0f;
        int n = 0;
        foreach (var c in Combatant.All)
            if (c != null && c.IsAlive && c.team != target.team)
                foreach (var m in c.Moves)
                    if (m != null && m.power > 0) { sum += TypeChart.Multiplier(m.type, target.Stats.types, out _); n++; }
        return n > 0 ? sum / n : 1f;
    }

    // The two members stand close to a healthy elite carrying a Fire Z crystal: its Z move strikes them both, once,
    // and takes at most zMaxShare of their health.
    IEnumerator EnemyZ()
    {
        var (leader, ally) = Place(new Vector2(-2.5f, 0f), new Vector2(-2.5f, 1.5f), settle: false);
        var elite = Elite(new Vector2(2f, 0f), LanceFlamme);
        if (elite == null) { Report(false, "pas d'élite"); yield break; }
        var ultimate = elite.GetComponent<EnemyUltimate>();
        ultimate.Carry(SpecialItems.Family.Z, 2); // left alone, it would pick a Tera shard against this team
        bool used = ultimate.Decide();
        bool again = ultimate.Decide(); // one item, one ultimate
        yield return new WaitForSeconds(SpecialItems.ZPose + 0.6f);
        bool both = leader.hp < leader.maxHp && ally.hp < ally.maxHp;
        bool capped = leader.hp >= leader.maxHp * (0.99f - SpecialItems.Enemy.zMaxShare) && ally.hp >= ally.maxHp * (0.99f - SpecialItems.Enemy.zMaxShare);
        Report(used && !again && ultimate.LastUsed == "z" && both && capped,
            $"{(used ? ultimate.LastUsed : "rien")}{(again ? " deux fois" : "")} : Pikachu {leader.hp:0}/{leader.maxHp:0}, allié {ally.hp:0}/{ally.maxHp:0}");
    }

    // ---------------------------------------------------------------- rescue and heals

    const int Salameche = 4, Rattata = 19, Soin = 117;
    Combatant third; // third team member, added for the rescue scenarios

    static void KnockOut(Combatant c) => c.TakeFixedDamage(c.hp + 1f);

    // The third member is knocked out far from the controlled Pokémon, nobody around: the ally walks to it and stands
    // on it until it is back.
    IEnumerator AllyRevives()
    {
        var (leader, ally) = Place(new Vector2(-8f, 0f), new Vector2(-3f, 2f), settle: false);
        Teleport(third, Centre + new Vector2(4f, -1f));
        yield return new WaitForFixedUpdate();
        KnockOut(third);
        float started = Time.time;
        while (Time.time - started < 12f && !third.IsAlive) yield return new WaitForFixedUpdate();
        Report(third.IsAlive, third.IsAlive ? $"réanimé en {Time.time - started:0.0} s" : $"toujours K.O. (allié à {Distance(ally, third):0.0} case(s))");
    }

    // The controlled Pokémon stands on the knocked-out member to revive it, a fighting enemy right there: the ally
    // (the guard) draws it away while the revive goes on.
    IEnumerator GuardDrawsAway()
    {
        var (leader, ally) = Place(Vector2.zero, new Vector2(-3f, -2f), settle: false);
        var kept = Sturdy(leader, ally);
        Teleport(third, Centre);
        yield return new WaitForFixedUpdate();
        KnockOut(third);
        var spawner = FindAnyObjectByType<EnemySpawner>();
        var foe = spawner != null ? spawner.SpawnFighter(Rattata, null, Centre + new Vector2(2.5f, 1f), false, false, 3, 1000f) : null;
        if (foe == null) { Restore(kept, leader, ally); Report(false, "pas d'ennemi (Rattata ?)"); yield break; }
        placed.Add(foe);
        float started = Time.time, sum = 0f;
        int samples = 0;
        bool onGuard = false;
        while (Time.time - started < 6f)
        {
            yield return new WaitForFixedUpdate();
            if (Time.time - started < 2f) continue; // the time for the guard to step in
            sum += Vector2.Distance(foe.transform.position, Centre);
            samples++;
            onGuard |= TargetChoice.Current(foe) == ally;
        }
        float average = samples > 0 ? sum / samples : 0f;
        bool back = third.IsAlive;
        Restore(kept, leader, ally);
        Report(average >= 2.5f && back && onGuard,
            $"ennemi en moyenne à {average:0.0} case(s) du K.O., {(onGuard ? "attiré par l'allié" : "jamais attiré par l'allié")}, {(back ? "réanimé" : "toujours K.O.")}");
    }

    // The controlled Pokémon is low on health, out of reach of the ally's heal (Soin), nobody around: the ally walks
    // over and heals it.
    IEnumerator HealsLowMate()
    {
        var (leader, ally) = Place(new Vector2(6f, 0f), new Vector2(-4f, 0f), settle: false);
        var keptMoves = (MoveDef[])ally.Moves.Clone();
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, i == 0 ? GameData.Move(Soin) : null);
        leader.hp = leader.maxHp * 0.3f;
        float before = leader.hp;
        float started = Time.time;
        while (Time.time - started < 8f && leader.hp <= before) yield return new WaitForFixedUpdate();
        bool healed = leader.hp > before;
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, keptMoves[i]);
        leader.hp = leader.maxHp;
        Report(healed, healed ? $"soigné après {Time.time - started:0.0} s" : $"pas soigné (allié à {Distance(ally, leader):0.0} case(s))");
    }

    // Same, but the controlled Pokémon stands among two opponents and the ally is hurt itself: it keeps its distance.
    IEnumerator NoRiskyHeal()
    {
        var (leader, ally) = Place(new Vector2(5f, 0f), new Vector2(-3f, 0f), settle: false);
        var keptMoves = (MoveDef[])ally.Moves.Clone();
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, i == 0 ? GameData.Move(Soin) : null);
        Enemy(BatchTeam[1], new Vector2(3f, 1f));
        Enemy(BatchTeam[1], new Vector2(3f, -1f));
        leader.hp = leader.maxHp * 0.3f;
        ally.hp = ally.maxHp * 0.4f;
        float closest = float.MaxValue, started = Time.time;
        while (Time.time - started < 5f)
        {
            yield return new WaitForFixedUpdate();
            foreach (var c in placed)
                if (c != null) closest = Mathf.Min(closest, Distance(ally, c));
        }
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, keptMoves[i]);
        leader.hp = leader.maxHp;
        ally.hp = ally.maxHp;
        float gap = SaveData.Settings.allyHealSafeGap;
        Report(closest >= gap, $"au plus près à {closest:0.0} case(s) des ennemis (minimum {gap:0.0})");
    }

    // The team on one side of a river (a burnt bridge in the middle), an opponent on the other: the ally, with only a
    // Grass move that hits at close range (a shot would just fly across the river), makes the vine bridge to go and
    // fight it.
    IEnumerator RepairTowardFoe()
    {
        var (leader, ally) = Place(new Vector2(-3f, 2f), new Vector2(-3f, 0f));
        var grid = DungeonGenerator.Current;
        var centre = grid.CellOf(Centre);
        var inside = grid.Interior;
        for (int x = centre.x + 1; x <= centre.x + 3; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                Obstacles.Place(x, y, Mathf.Abs(y - centre.y) <= 1 ? "burnt_bridge" : "water");
        var move = FindMove(4, m => m.family == "melee" && m.lunge <= 0f);
        if (move == null) { Report(false, "pas d'attaque Plante au corps à corps"); yield break; }
        var kept = (MoveDef[])ally.Moves.Clone();
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, i == 0 ? move : null);
        var foe = Enemy(BatchTeam[1], new Vector2(5.5f, 0f));
        foe.maxHp = foe.hp = 100000f;
        float bank = grid.CentreOf(centre.x + 3, centre.y).x + 0.5f;
        float started = Time.time;
        while (Time.time - started < 15f && ally.transform.position.x < bank) yield return new WaitForFixedUpdate();
        bool crossed = ally.transform.position.x >= bank;
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, kept[i]);
        Report(crossed, crossed ? $"{move.name} : de l'autre côté en {Time.time - started:0.0} s" : $"{move.name} : resté de son côté");
    }

    // ---------------------------------------------------------------- encounters

    const int Volcano = 12; // décor of the third stage: Feu, Roche, Sol, Poison in encounters.json

    // Many waves at each stage: one encounter, or several whose costs fit in the budget, never more than maxGroups.
    IEnumerator EncounterBudget()
    {
        var rng = new System.Random(1);
        int waves = 0, over = 0, most = 0;
        for (int stage = 0; stage < 4; stage++)
            for (int wave = 0; wave < 3; wave++)
                for (int k = 0; k < 40; k++)
                {
                    var list = Encounters.Wave(rng, stage, wave, Volcano);
                    waves++;
                    most = Mathf.Max(most, list.Count);
                    float cost = 0f;
                    foreach (var e in list) cost += e.cost;
                    if (list.Count == 0 || (list.Count > 1 && cost > Encounters.BudgetOf(stage, wave) + 0.01f)) over++;
                }
        Report(over == 0 && most <= 3, $"{waves} vagues, {over} hors budget ou vides, jusqu'à {most} rencontres par vague");
        yield break;
    }

    // Packs: four to six of one Pokémon, at a low level, never épique.
    IEnumerator PackIsKin()
    {
        var rng = new System.Random(2);
        var pack = Encounters.CategoryOf("pack");
        int packs = 0, wrong = 0;
        for (int k = 0; k < 100 && pack != null; k++)
        {
            var e = Encounters.Make(rng, pack, 3, Volcano);
            if (e == null) continue;
            packs++;
            bool species = e.members.TrueForAll(m => m.pokemonId == e.members[0].pokemonId);
            bool size = e.members.Count >= 4 && e.members.Count <= 6;
            bool low = e.members.TrueForAll(m => m.levelBonus < 2 && m.rarity.key != "epic");
            if (!species || !size || !low) wrong++;
        }
        Report(packs > 0 && wrong == 0, $"{packs} meutes de 4 à 6 d'une seule espèce, niveau bas, jamais épiques ; {wrong} hors règle");
        yield break;
    }

    // Gangs: three strong Pokémon, rare or épique, each carrying an item (three ultimates).
    IEnumerator GangCarriesItems()
    {
        var rng = new System.Random(7);
        var gang = Encounters.CategoryOf("gang");
        int gangs = 0, wrong = 0;
        for (int k = 0; k < 100 && gang != null; k++)
        {
            var e = Encounters.Make(rng, gang, 2, Volcano);
            if (e == null) continue;
            gangs++;
            bool rule = e.members.Count == 3 && e.members.TrueForAll(m => m.item && (m.rarity.key == "rare" || m.rarity.key == "epic"));
            if (!rule) wrong++;
        }
        Report(gangs > 0 && wrong == 0, $"{gangs} gangs de 3, rares ou épiques, chacun avec un objet ; {wrong} hors règle");
        yield break;
    }

    // Combos: about the chance of encounters.json, a pack comes with a solitaire, always rare or épique, in its group.
    IEnumerator PackWithLeader()
    {
        var rng = new System.Random(9);
        var pack = Encounters.CategoryOf("pack");
        Encounters.Combo combo = null;
        foreach (var c in Encounters.Combos)
            if (c.lot == "pack" && c.with == "solo") combo = c;
        if (pack == null || combo == null) { Report(false, "pas de combinaison meute + solitaire"); yield break; }
        int packs = 0, led = 0, wrong = 0;
        for (int k = 0; k < 400; k++)
        {
            var e = Encounters.Make(rng, pack, 2, Volcano);
            if (e == null) continue;
            packs++;
            int before = e.members.Count;
            Encounters.Accompany(rng, e, 2, Volcano);
            if (e.with == null) continue;
            if (e.with.key == "solo")
            {
                led++;
                var leader = e.members[before];
                if (System.Array.IndexOf(combo.rarities, leader.rarity.key) < 0) wrong++;
            }
        }
        float share = packs > 0 ? (float)led / packs : 0f;
        Report(packs > 0 && wrong == 0 && Mathf.Abs(share - combo.chance) < 0.1f,
            $"{led} meutes menées sur {packs} ({share:P0}, attendu {combo.chance:P0}), {wrong} chef hors rang");
        yield break;
    }

    // A pack of six and its leader, all on top of each other near the team: none of them should flip its facing or
    // turn back several times a second (it looked like spinning on the spot).
    IEnumerator CrowdStaysSteady()
    {
        var (leader, ally) = Place(new Vector2(-3f, 0f), new Vector2(-3f, 2f), settle: false);
        var kept = Sturdy(leader, ally);
        var spawner = FindAnyObjectByType<EnemySpawner>();
        var pack = Encounters.CategoryOf("pack");
        var e = pack != null ? Encounters.Make(new System.Random(10), pack, 1, -1) : null;
        Encounters.Combo combo = null;
        foreach (var c in Encounters.Combos)
            if (c.lot == "pack" && c.with == "solo") combo = c;
        if (spawner == null || e == null) { Restore(kept, leader, ally); Report(false, "pas de meute"); yield break; }
        while (e.members.Count < 6) e.members.Add(e.members[0]);
        if (combo != null) Encounters.Join(new System.Random(11), e, combo, 1, -1);
        var crowd = spawner.SpawnEncounter(e, Centre + new Vector2(3f, 0f), 3);
        placed.AddRange(crowd);
        foreach (var c in crowd)
        {
            c.maxHp = c.hp = 100000f;
            Teleport(c, Centre + new Vector2(3f, 0f)); // all on the same spot
        }
        var turns = new int[crowd.Count];
        var reversals = new int[crowd.Count];
        var lastDir = new int[crowd.Count];
        var lastVel = new Vector2[crowd.Count];
        for (int i = 0; i < crowd.Count; i++) lastDir[i] = crowd[i].GetComponent<PmdAnimator>().direction;
        float started = Time.time, length = 5f;
        while (Time.time - started < length)
        {
            yield return new WaitForFixedUpdate();
            for (int i = 0; i < crowd.Count; i++)
            {
                var c = crowd[i];
                if (c == null || !c.IsAlive) continue;
                int dir = c.GetComponent<PmdAnimator>().direction;
                if (dir != lastDir[i]) turns[i]++;
                lastDir[i] = dir;
                var v = c.GetComponent<Rigidbody2D>().linearVelocity;
                if (v.magnitude > 0.5f && lastVel[i].magnitude > 0.5f && Vector2.Dot(v.normalized, lastVel[i].normalized) < -0.3f) reversals[i]++;
                lastVel[i] = v;
            }
        }
        Restore(kept, leader, ally);
        float worstTurns = 0f, worstBack = 0f;
        for (int i = 0; i < crowd.Count; i++)
        {
            worstTurns = Mathf.Max(worstTurns, turns[i] / length);
            worstBack = Mathf.Max(worstBack, reversals[i] / length);
        }
        Report(worstTurns <= 3f && worstBack <= 1.5f,
            $"{crowd.Count} Pokémon : au pire {worstTurns:0.0} changements d'orientation et {worstBack:0.0} demi-tours par seconde");
    }

    // A lake; Bulbizarre (walks) and Tentacool (swims) are pushed into it: the first falls and is back on the bank,
    // the second may stay in the water.
    IEnumerator PushedIntoWater()
    {
        Place(new Vector2(-8f, 0f), new Vector2(-8f, 2f), settle: false);
        var grid = DungeonGenerator.Current;
        var c = grid.CellOf(Centre);
        for (int x = c.x + 1; x <= c.x + 6; x++)
            for (int y = c.y - 4; y <= c.y + 4; y++)
                Obstacles.Place(x, y, "water");
        var walker = Enemy(1, new Vector2(0f, 1f));
        var swimmer = Enemy(72, new Vector2(0f, -1f)); // Tentacool
        yield return new WaitForFixedUpdate();
        foreach (var e in new[] { walker, swimmer })
        {
            e.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic; // the bench's enemies stand still: let the push move them
            e.Knockback(Vector2.right * 3f / 0.4f, 0.4f);
        }
        yield return new WaitForSeconds(1.5f);
        bool walkerOut = !Obstacles.BlocksAt(walker.transform.position, walker) && Obstacles.At(walker.transform.position) == null;
        bool swimmerIn = Obstacles.At(swimmer.transform.position)?.key == "water";
        Report(walkerOut && swimmerIn, $"Bulbizarre {(walkerOut ? "revenu sur la berge" : "resté dans l'eau")}, Tentacool {(swimmerIn ? "dans l'eau" : "sur la berge")}");
    }

    // The champion: one Pokémon, golden ring, bigger, much tougher than the same Pokémon as a solitaire, with an item;
    // its knock-out leaves an item on the ground.
    IEnumerator ChampionStands()
    {
        var (leader, ally) = Place(new Vector2(-8f, 0f), new Vector2(-8f, 2f), settle: false);
        var spawner = FindAnyObjectByType<EnemySpawner>();
        var champion = Encounters.CategoryOf("champion");
        var e = champion != null ? Encounters.Make(new System.Random(12), champion, 1, -1) : null;
        if (spawner == null || e == null) { Report(false, "pas de champion"); yield break; }
        var solo = Encounters.CategoryOf("solo");
        var spawned = spawner.SpawnEncounter(e, Centre + new Vector2(4f, 0f), 5);
        placed.AddRange(spawned);
        var c = spawned.Count == 1 ? spawned[0] : null;
        yield return new WaitForFixedUpdate();
        bool marked = c != null && c.GetComponent<EliteMark>() is { sureLoot: true } && c.transform.Find("Aura d'élite") != null;
        bool item = c != null && c.GetComponent<EnemyUltimate>() != null;
        bool big = c != null && c.transform.localScale.x > 1.1f;
        float ratio = solo != null ? e.members[0].health / solo.health : 0f;
        string name = c != null ? c.Stats.name : null; // the champion is gone once knocked out
        Loot.DropsInSandbox = true;
        if (c != null) c.TakeFixedDamage(c.hp + 1f);
        yield return new WaitForFixedUpdate();
        Loot.DropsInSandbox = false;
        bool dropped = System.Array.Exists(FindObjectsByType<Transform>(), t => t.name.StartsWith("Objet :"));
        ClearDrops();
        Report(name != null && marked && item && big && ratio >= 2f && dropped,
            $"{name ?? "rien"} : {(marked ? "anneau doré" : "sans marque")}, {(item ? "objet" : "sans objet")}, "
            + $"{(big ? "plus grand" : "taille normale")}, PV ×{ratio:0.0} d'un solitaire, {(dropped ? "objet au sol après le K.O." : "rien au sol")}");
    }

    // The squad: three, rare or épique, elites; only its leader carries an item; nothing drops until the last one falls.
    IEnumerator SquadLoot()
    {
        Place(new Vector2(-8f, 0f), new Vector2(-8f, 2f), settle: false);
        var spawner = FindAnyObjectByType<EnemySpawner>();
        var squad = Encounters.CategoryOf("squad");
        var e = squad != null ? Encounters.Make(new System.Random(13), squad, 2, -1) : null;
        if (spawner == null || e == null) { Report(false, "pas d'escouade"); yield break; }
        var spawned = spawner.SpawnEncounter(e, Centre + new Vector2(5f, 0f), 5);
        placed.AddRange(spawned);
        yield return new WaitForFixedUpdate();
        int items = spawned.FindAll(c => c.GetComponent<EnemyUltimate>() != null).Count;
        bool ranks = e.members.TrueForAll(m => m.rarity.key == "rare" || m.rarity.key == "epic");
        bool elites = spawned.TrueForAll(c => c.GetComponent<EliteMark>() is { sureLoot: true, squad: not null });
        bool Dropped() => System.Array.Exists(FindObjectsByType<Transform>(), t => t.name.StartsWith("Objet :"));
        bool early = false;
        Loot.DropsInSandbox = true;
        for (int i = 0; i < spawned.Count; i++)
        {
            spawned[i].TakeFixedDamage(spawned[i].hp + 1f);
            yield return new WaitForFixedUpdate();
            if (i < spawned.Count - 1 && Dropped()) early = true;
        }
        Loot.DropsInSandbox = false;
        bool last = Dropped();
        ClearDrops();
        Report(spawned.Count == 3 && items == 1 && ranks && elites && !early && last,
            $"{spawned.Count} membres, {items} objet(s), {(ranks ? "rares ou épiques" : "rangs faux")}, {(elites ? "tous élites" : "marques manquantes")}, "
            + $"{(early ? "butin trop tôt" : "rien avant la fin")}, {(last ? "butin au dernier" : "pas de butin")}");
    }

    // The items left on the ground by a scenario (they would be picked up later and fill the save).
    static void ClearDrops()
    {
        foreach (var t in FindObjectsByType<Transform>())
            if (t != null && t.name.StartsWith("Objet :")) Destroy(t.gameObject);
    }

    // Elite odds: the chance of a stage with an elite rises with the stage, and an elite lot is a champion or a squad.
    IEnumerator EliteOdds()
    {
        float first = Encounters.EliteChance(0), fourth = Encounters.EliteChance(3);
        var rng = new System.Random(14);
        int champions = 0, squads = 0;
        for (int k = 0; k < 200; k++)
        {
            var e = Encounters.Elite(rng, 2, -1);
            if (e == null) continue;
            if (e.category.key == "champion") champions++;
            if (e.category.key == "squad") squads++;
        }
        Report(fourth > first && champions > 40 && squads > 40,
            $"élite par étape : {first:P0} à l'étape 1, {fourth:P0} à l'étape 4 ; {champions} champions et {squads} escouades sur 200");
        yield break;
    }

    // Typical populated zones: whole waves at each stage, every lot following its rules; the log shows one zone per
    // stage and how often each lot appears.
    IEnumerator TypicalZones()
    {
        var rng = new System.Random(8);
        int wrong = 0;
        var lines = new List<string>();
        for (int stage = 0; stage < 4; stage++)
        {
            var count = new Dictionary<string, int>();
            int pokemon = 0, items = 0, zones = 50;
            for (int k = 0; k < zones; k++)
            {
                var wave = Encounters.Wave(rng, stage, 2, Volcano);
                if (k == 0) Debug.Log($"AIZONE étape {stage + 1} : " + string.Join("  |  ", wave.ConvertAll(e => e.Describe())));
                foreach (var e in wave)
                {
                    count[e.category.name] = count.TryGetValue(e.category.name, out int n) ? n + 1 : 1;
                    pokemon += e.members.Count;
                    items += e.members.FindAll(m => m.item).Count;
                    if (e.category.key == "gang" && stage < e.category.minStage) wrong++;
                    // Each member against its own lot (a pack's rare or épique leader is a solitaire).
                    if (!e.members.TrueForAll(m => m.lot.rarities.Length == 0 || System.Array.IndexOf(m.lot.rarities, m.rarity.key) >= 0)) wrong++;
                }
            }
            lines.Add($"étape {stage + 1} : {(float)pokemon / zones:0.0} Pokémon, {(float)items / zones:0.0} objets, "
                      + string.Join(" ", System.Linq.Enumerable.Select(count, kv => $"{kv.Key} {kv.Value}")));
        }
        Report(wrong == 0, string.Join(" ; ", lines) + (wrong > 0 ? $" ; {wrong} lots hors règle" : ""));
        yield break;
    }

    // Teams: a support most of the time, and a second member whose type combines with the first one's more often
    // than chance would give.
    IEnumerator TeamHasRoles()
    {
        var rng = new System.Random(3);
        var teamCategory = Encounters.CategoryOf("team");
        int teams = 0, support = 0, combine = 0, randomCombine = 0;
        var pool = Encounters.Pool(1, -1);
        for (int k = 0; k < 100 && teamCategory != null; k++)
        {
            var e = Encounters.Make(rng, teamCategory, 1, -1);
            if (e == null || e.members.Count < 2) continue;
            teams++;
            if (e.members.Exists(m => m.role == "support")) support++;
            if (Encounters.Combines(e.members[0].pokemonId, e.members[1].pokemonId)) combine++;
            if (Encounters.Combines(e.members[0].pokemonId, pool[rng.Next(pool.Count)])) randomCombine++;
        }
        Report(teams > 0 && support >= teams * 0.6f && combine > randomCombine,
            $"{teams} équipes : {support} avec un soutien, {combine} paires qui se combinent (au hasard : {randomCombine})");
        yield break;
    }

    // Ranks: rare and épique members are more frequent at the fourth stage than at the first.
    IEnumerator RanksRise()
    {
        var rng = new System.Random(4);
        float Share(int stage)
        {
            int high = 0, n = 2000;
            for (int k = 0; k < n; k++)
            {
                var r = Encounters.PickRarity(rng, stage);
                if (r.key == "rare" || r.key == "epic") high++;
            }
            return (float)high / n;
        }
        float first = Share(0), fourth = Share(3);
        Report(fourth > first * 2f, $"rare ou épique : {first:P0} à l'étape 1, {fourth:P0} à l'étape 4");
        yield break;
    }

    // On the volcano, every Pokémon drawn has a Feu, Roche, Sol or Poison type.
    IEnumerator FitsDecor()
    {
        var rng = new System.Random(5);
        int members = 0, off = 0;
        var allowed = new[] { 2, 13, 9, 8 };
        for (int k = 0; k < 60; k++)
            foreach (var e in Encounters.Wave(rng, 1, 1, Volcano))
                foreach (var m in e.members)
                {
                    members++;
                    if (!System.Array.Exists(GameData.Pokemon(m.pokemonId).types, t => System.Array.IndexOf(allowed, t) >= 0)) off++;
                }
        Report(members > 0 && off == 0, $"{members} Pokémon tirés sur le volcan, {off} d'un autre type");
        yield break;
    }

    // An épique team appears: three fighters in one group, each with its rank mark, its ring, and an ultimate.
    IEnumerator EncounterSpawns()
    {
        Place(new Vector2(-8f, 0f), new Vector2(-8f, 2f), settle: false);
        var spawner = FindAnyObjectByType<EnemySpawner>();
        var teamCategory = Encounters.CategoryOf("team");
        Encounters.Rarity epic = null;
        foreach (var r in Encounters.Rarities)
            if (r.key == "epic") epic = r;
        var e = teamCategory != null ? Encounters.Make(new System.Random(6), teamCategory, 3, -1) : null;
        if (spawner == null || e == null || epic == null) { Report(false, "pas de rencontre ou de rang épique"); yield break; }
        foreach (var m in e.members)
        {
            m.rarity = epic;
            m.item = true;
        }
        var spawned = spawner.SpawnEncounter(e, Centre + new Vector2(5f, 0f), 5);
        placed.AddRange(spawned);
        foreach (var c in spawned) c.maxHp = c.hp = 100000f;
        yield return new WaitForFixedUpdate();
        int marked = spawned.FindAll(c => c.GetComponent<RarityMark>() != null && c.transform.Find("Rang") != null).Count;
        int ultimates = spawned.FindAll(c => c.GetComponent<EnemyUltimate>() != null).Count;
        var group = spawned.Count > 0 ? spawned[0].GetComponent<EnemyAI>().group : null;
        bool together = group != null && spawned.TrueForAll(c => c.GetComponent<EnemyAI>().group == group);
        Report(spawned.Count == e.members.Count && marked == spawned.Count && ultimates == spawned.Count && together,
            $"{spawned.Count} apparus sur {e.members.Count}, {marked} avec leur anneau, {ultimates} avec un ultime, {(together ? "un seul groupe" : "groupes séparés")}");
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
