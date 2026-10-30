using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        var teamBag = team.GetComponent<Bag>();
        if (teamBag != null) teamBag.Clear(); // the test arena's starting bag would change the other scenarios (a Reviver Seed...)
        var ground = FindAnyObjectByType<Loot>();
        if (ground != null) ground.ClearGround(); // nor should the held items of the showcase be picked up on the way
        // No experience during the bench: a level-up opens the upgrade screen, which pauses the game and the bench with it.
        var progression = team.GetComponent<Progression>();
        if (progression != null) progression.enabled = false;
        if (team.Count < 2) team.AddMember(BatchTeam[1], team.Controlled.transform.position + Vector3.right);
        // Abilities drawn at random would change the scenarios: none, unless a scenario gives one.
        Abilities.Suspended = true;
        foreach (var member in team.Members) member.SetAbility(0);

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
        yield return Scenario("Grande carte : refait le pont même sans chemin complet", LargeMap(Repair("burnt_bridge", 4, "vine")));
        yield return Scenario("Refroidit la lave pour passer", Repair("lava", 3, "any"));
        yield return Scenario("Brûle le pont sous un ennemi au corps à corps", CutBridge());
        yield return Scenario("Marche sur le métal chargé sans s'en soucier", IgnoreMetal());
        // Interactive obstacles inside rooms of the catalogue (rooms.json).
        yield return Scenario("Salle : les obstacles sont dessinés", InRoom("lava", "lake", RoomDrawn()));
        yield return Scenario("Salle : l'eau arrête la marche", InRoom("water", "river", WaterStopsWalking()));
        yield return Scenario("Salle : poussé dans la lave, tombe et revient", InRoom("lava", "lake", PushedIntoLava()));
        yield return Scenario("Salle : le feu brûle un pont", InRoom("water", "river", Turns("bridge", 2, "burning_bridge")));
        yield return Scenario("Salle : la glace gèle l'eau", InRoom("water", "lake", Turns("water", 6, "frozen")));
        yield return Scenario("Salle : l'eau refroidit la lave", InRoom("lava", "lake", Turns("lava", 3, "rock")));
        yield return Scenario("Salle : une attaque Feu tirée brûle le pont", InRoom("water", "river", FireShotBurnsBridge()));
        yield return Scenario("Salle : les pylônes lancent leur arc", InRoom("electric", "storm", PylonsFire()));
        yield return Scenario("Niveau : pas de gel sur les obstacles traversables", LevelHitches());
        yield return Scenario("Île : arrivée praticable, pas de gel en se promenant", IslandWalk());
        yield return Scenario("Hauteur : tire par-dessus la falaise d'en haut, pas d'en bas", HighGround());
        RestoreArena();
        yield return Scenario("Objets : la capacité Z frappe et paralyse tous les ennemis visibles", ZMoveHits());
        yield return Scenario("Objets : la capacité Z épargne les alliés (chaque cristal)", ZSparesAllies());
        yield return Scenario("Élite : gèle une rivière pour rejoindre sa cible", EliteFreezesRiver());
        yield return Scenario("Élite : brûle le pont sous un adversaire au corps à corps", EliteBurnsBridge());
        yield return Scenario("Ultime ennemi : se téracristallise dans un type qui résiste", EnemyTera());
        yield return Scenario("Ultime ennemi : capacité Z quand l'équipe est groupée", EnemyZ());
        yield return Scenario("Ultime ennemi : méga-évolue à mi-PV, pas avant", EnemyMega());
        yield return Scenario("Seconde phase : seuil infranchissable d'un coup, soin, recharges plus courtes", SecondPhase());
        yield return Scenario("Ultime ennemi : Téra Spectre pour traverser un mur de rochers", EnemyTeraCrosses());
        yield return Scenario("Objets : le Téracristal change le type", TeraChangesType());
        yield return Scenario("Objets : une méga-gemme obtenue ne retombe plus", StonesStayUnique());
        yield return Scenario("Objets : les types de l'équipe tombent plus souvent", LootFavoursTeam());

        // Rescue: a third member joins (last, so that the scenarios above keep a team of two).
        if (team.Count < 3) team.AddMember(Salameche, team.Controlled.transform.position + Vector3.left);
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
        yield return Scenario("Sac : Baie Oran, Graine Explo, Orbe Gel", BagEffects());
        yield return Scenario("Sac : un allié blessé mange une baie du sac", AllyEatsBerry());
        yield return Scenario("Sac : 4 emplacements, piles de 9, plein ensuite", BagCapacity());
        yield return Scenario("Sac : un élite lâche un objet, ramassé en marchant dessus", EliteDropsConsumable());
        yield return Scenario("Objets tenus : Bandeau Choix, Restes, Ballon", HeldEffects());
        yield return Scenario("Talents : l'allié ne tire pas de l'Eau sur un Absorbe-Eau", AbilityTarget());
        yield return Scenario("Talents IA : pas de statut sur Miroir Magik ni Synchro, peu de tirs sur Ténéfix", AbilityAiLotOne());
        yield return Scenario("Talents : absorption, Fermeté, Attention", AbilityRules());
        yield return Scenario("Talents : Synchro, Garde Magik, Marque Ombre", AbilityRulesTwo());
        yield return Scenario("Talents : Absorbe-Eau et Herbivore sur leur terrain", AbilityTerrain());
        yield return Scenario("Talents : Garde Mystik, Brise Moule, Pickpocket", AbilityRulesThree());
        yield return Scenario("Talents : une méga prend son talent et son effet, puis les rend", MegaAbility());
        yield return Scenario("Talents : la démo de chaque talent se joue sans erreur", AbilityDemosRun());
        yield return Scenario("Objets tenus : Ceinture Force et Casque Brut", HeldLastStand());
        yield return Scenario("Objets tenus : pris en marchant dessus, l'ancien tombe à ses pieds", HeldSwap());
        yield return Scenario("Objets tenus : un ennemi qui en tient un le lâche", EnemyDropsHeld());
        yield return Scenario("Objets tenus : tiers I à III et fusion au ramassage", HeldTiers());
        yield return Scenario("Objets : Explo et Casque Brut plafonnés contre un boss, Orbe Vie une fois par attaque", ItemLimits());
        yield return Scenario("Interface : chaque objet a une description", ItemsDescribed());
        yield return Scenario("Interface : bandeaux regroupés, trois au plus", BannersGrouped());
        yield return Scenario("Eau : poussé dedans, un Pokémon qui ne nage pas revient, un nageur y reste", PushedIntoWater());
        yield return Scenario("Ultime ennemi : Téra pour se déplacer sur la lave qui l'entoure", EnemyTeraForWater());
        yield return Scenario("Rencontres : une équipe a ses rôles et des types qui se combinent", TeamHasRoles());
        yield return Scenario("Rencontres : les rangs montent avec l'étape", RanksRise());
        yield return Scenario("Rencontres : les Pokémon vont avec le décor", FitsDecor());
        yield return Scenario("Rencontres : apparition avec rang, anneau et objet", EncounterSpawns());
        yield return Scenario("Peuplement : chaque sous-biome a sa population, plus forte en avançant", HabitatsPopulated());
        yield return Scenario("Peuplement : camps et rondes au repos, réveil, laisse, retour d'un autre lot", QuarterPopulated());
        yield return Scenario("Salles de l'île : trésor, embuscade à sortie fermée, gardien sur son coffre", IslandRoomContents());
        yield return Scenario("Engrenages : passages fermés jusqu'à l'engrenage, centre avec les quatre", GearsLockPassages());
        yield return Scenario("Engrenages : donjon de la clé, portail de l'îlot, boss grand et prêt à sa seconde phase ; fragments et indices", GearsFromBossAndFragments());

        Clear();
        if (progression != null) progression.enabled = true;
        Abilities.Suspended = false;
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

    // ---------------------------------------------------------------- rooms

    DungeonTileset arenaDecor; // the test arena's own décor, put back after the room scenarios

    // Builds a room of the catalogue (that theme and variant), runs the scenario in it.
    IEnumerator InRoom(string theme, string variant, IEnumerator body)
    {
        var arena = DungeonGenerator.Current;
        if (arenaDecor == null) arenaDecor = arena.tileset;
        var room = RoomCatalog.Pick("combat", new System.Random(1234), theme, variant);
        if (room == null)
        {
            Report(false, $"pas de salle {theme}/{variant} dans rooms.json");
            yield break;
        }
        Clear();
        arena.room = room;
        arena.Generate();
        team.PrepareForNextArena(team.transform.position, 0f);
        yield return new WaitForFixedUpdate();
        yield return body;
        if (results.Count > 0) results[^1].detail = $"[{room.variant.name}, décor {room.tileset}] " + results[^1].detail;
    }

    void RestoreArena()
    {
        var arena = DungeonGenerator.Current;
        if (arena == null || arena.room == null) return;
        arena.room = null;
        if (arenaDecor != null) arena.tileset = arenaDecor;
        Clear();
        arena.Generate();
        team.PrepareForNextArena(team.transform.position, 0f);
    }

    IEnumerator RoomDrawn()
    {
        yield return new WaitForSeconds(0.2f);
        int count = Obstacles.Current != null ? Obstacles.Current.Count : 0, drawn = Obstacles.Current != null ? Obstacles.Current.DrawnTiles : 0;
        Report(count > 0 && drawn >= count, $"{count} cases d'obstacle, {drawn} tuiles dessinées");
    }

    // Walks the ally (its AI off) straight at the water from the bank for a second: it must stop at the edge.
    IEnumerator WaterStopsWalking()
    {
        var (_, ally) = Place(Vector2.zero, Vector2.zero, settle: false);
        if (!Shore("water", out var bank, out var into)) { Report(false, "pas de rive trouvée"); yield break; }
        var ai = ally.GetComponent<AllyAI>();
        ai.enabled = false;
        Teleport(ally, bank);
        float hp = ally.hp;
        int onWater = 0;
        float started = Time.time;
        while (Time.time - started < 1.2f)
        {
            yield return new WaitForFixedUpdate();
            ally.Drive((into - bank).normalized * ally.CurrentSpeed);
            if (Obstacles.At(ally.transform.position)?.key == "water") onWater++;
        }
        ai.enabled = true;
        Report(onWater == 0 && ally.hp >= hp, $"{onWater} images sur l'eau, PV {hp:0} → {ally.hp:0}");
    }

    // Knocks the ally from the bank into the lava: it falls, loses health and comes back to firm ground.
    IEnumerator PushedIntoLava()
    {
        var (_, ally) = Place(Vector2.zero, Vector2.zero, settle: false);
        if (!Shore("lava", out var bank, out var into)) { Report(false, "pas de rive trouvée"); yield break; }
        var ai = ally.GetComponent<AllyAI>();
        ai.enabled = false;
        Teleport(ally, bank);
        float hp = ally.hp;
        ally.Knockback((into - bank).normalized * 10f, 0.4f);
        yield return new WaitForSeconds(2f);
        ai.enabled = true;
        bool onLava = Obstacles.At(ally.transform.position)?.key == "lava";
        Report(ally.hp < hp && !onLava, $"PV {hp:0} → {ally.hp:0}, {(onLava ? "toujours dans la lave" : "revenu sur la terre ferme")}");
    }

    // A hit of that type on a tile of "from": it turns into "to".
    IEnumerator Turns(string from, int type, string to)
    {
        var cell = FindCell(from);
        var move = FindMove(type, m => true);
        if (cell == null || move == null) { Report(false, cell == null ? $"pas de case {from}" : "pas d'attaque"); yield break; }
        var grid = DungeonGenerator.Current;
        Terrain.OnImpact(move, grid.CentreOf(cell.Value.x, cell.Value.y), null);
        yield return new WaitForSeconds(0.6f);
        string now = Obstacles.At(cell.Value.x, cell.Value.y)?.key ?? "sol";
        Report(now == to || (to == "burning_bridge" && now == "burnt_bridge"), $"{move.name} : {from} → {now}");
    }

    // The ally (AI off) fires a straight Fire shot that ends its flight on a bridge tile: the bridge burns. The river
    // and its bridges are laid out anew each time: every bridge tile and eight directions are tried for a free tile
    // at the shot's range with a clear line to it.
    IEnumerator FireShotBurnsBridge()
    {
        var (_, ally) = Place(Vector2.zero, Vector2.zero, settle: false);
        var fire = FindMove(2, m => m.family == "projectile" && m.path != "arc");
        var bridges = FindCells("bridge");
        if (bridges.Count == 0 || fire == null) { Report(false, "pas de pont ou pas d'attaque Feu"); yield break; }
        var grid = DungeonGenerator.Current;
        Vector2Int? cell = null;
        Vector2 target = Vector2.zero, stand = Vector2.zero;
        foreach (var b in bridges)
        {
            target = grid.CentreOf(b.x, b.y);
            for (int k = 0; k < 8 && cell == null; k++)
            {
                var d = (Vector2)(Quaternion.Euler(0f, 0f, 45f * k) * Vector2.right);
                var at = grid.CellOf(target - d * fire.range);
                if (!grid.Walkable(at.x, at.y) || Obstacles.At(at.x, at.y) != null) continue;
                var from = grid.CentreOf(at.x, at.y);
                // The tile's centre must leave the shot ending on the bridge itself.
                if (grid.CellOf(from + (target - from).normalized * fire.range) != b || !grid.LineClear(from, target, 0.2f)) continue;
                cell = b;
                stand = from;
            }
            if (cell != null) break;
        }
        if (cell == null) { Report(false, $"pas d'endroit à {fire.range:0} cases d'un des {bridges.Count} ponts"); yield break; }
        var ai = ally.GetComponent<AllyAI>();
        ai.enabled = false;
        Teleport(ally, stand);
        var kept = (MoveDef[])ally.Moves.Clone();
        ally.LearnMove(0, fire);
        yield return new WaitForFixedUpdate();
        bool used = ally.TryUseMove(0, target - stand, Vector2.Distance(target, stand));
        yield return new WaitForSeconds(2f);
        for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, kept[i]);
        ai.enabled = true;
        string now = Obstacles.At(cell.Value.x, cell.Value.y)?.key ?? "sol";
        Report(used && now != "bridge", $"{fire.name} {(used ? "lancée" : "refusée")} : pont → {now}");
    }

    IEnumerator PylonsFire()
    {
        int pairs = Pylons.PairCount;
        float started = Time.time;
        bool live = false;
        while (!live && Time.time - started < 8f)
        {
            yield return new WaitForFixedUpdate();
            live = Pylons.AnyArcLive();
        }
        Report(pairs > 0 && live, $"{pairs} paire(s), arc {(live ? "lancé" : "jamais lancé")}");
    }

    // A whole level: the controlled Pokémon (and the allies following it) stand on each kind of obstacle one can walk
    // on, then a flying Pokémon (Ptéra) controlled in its place hovers over water, lava and void, where the allies cannot
    // follow; the longest frame must stay short.
    IEnumerator LevelHitches()
    {
        var arena = DungeonGenerator.Current;
        if (arenaDecor == null) arenaDecor = arena.tileset;
        Clear();
        var rng = new System.Random(21);
        LevelPlan plan = null;
        // A level with walkable obstacles in it (bridges, ice, metal...).
        for (int n = 0; n < 20; n++)
        {
            plan = LevelPlan.Generate(rng);
            arena.level = plan;
            arena.Generate();
            if (WalkableKinds().Count >= 2 && FlyOverCells().Exists(c => c.key == "water")) break;
        }
        team.PrepareForNextArena(arena.SpawnPoint, 0f);
        yield return new WaitForSeconds(0.5f);
        var leader = team.Controlled;
        float worst = 0f;
        string worstWhere = "-";
        var kinds = WalkableKinds();
        foreach (var (key, cell) in kinds)
        {
            Teleport(leader, arena.CentreOf(cell.x, cell.y));
            float started = Time.realtimeSinceStartup, last = started;
            while (Time.realtimeSinceStartup - started < 0.6f)
            {
                yield return null;
                float now = Time.realtimeSinceStartup, frame = now - last;
                last = now;
                if (now - started > 0.05f && frame > worst) { worst = frame; worstWhere = key; }
            }
        }
        // A flyer over what the others cannot cross.
        var over = FlyOverCells();
        var setControlled = typeof(PlayerController).GetMethod("SetControlled", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var flyer = team.Count < team.teamSize ? team.AddMember(Flyer, leader.transform.position) : null;
        if (flyer != null && setControlled != null)
        {
            setControlled.Invoke(team, new object[] { team.Count - 1 });
            foreach (var (key, cell) in over)
            {
                float started = Time.realtimeSinceStartup, last = started;
                while (Time.realtimeSinceStartup - started < 2f)
                {
                    Teleport(flyer, arena.CentreOf(cell.x, cell.y));
                    yield return null;
                    float now = Time.realtimeSinceStartup, frame = now - last;
                    last = now;
                    if (now - started > 0.05f && frame > worst) { worst = frame; worstWhere = $"{key} (survol)"; }
                }
            }
            setControlled.Invoke(team, new object[] { 0 });
            flyer.hp = flyer.maxHp;
        }
        arena.level = null;
        arena.room = new RoomInstance(); // makes RestoreArena rebuild the test arena
        string flown = flyer != null ? $", survol de {string.Join(", ", over.ConvertAll(k => k.key))}" : ", pas de survol (équipe pleine)";
        Report(worst < 0.1f, $"{kinds.Count} sortes d'obstacles ({string.Join(", ", kinds.ConvertAll(k => k.key))}){flown}, pire image {worst * 1000f:0} ms sur {worstWhere}, carte {plan.size.x}×{plan.size.y}");
    }

    const int Flyer = 142; // Ptéra: Rock / Flying, crosses water, lava and void

    // One tile well inside each of water, lava and void (its 8 neighbours of the same kind).
    static List<(string key, Vector2Int cell)> FlyOverCells()
    {
        var found = new List<(string, Vector2Int)>();
        var grid = DungeonGenerator.Current;
        int size = grid.Size;
        for (int x = 1; x < size - 1; x++)
            for (int y = 1; y < size - 1; y++)
            {
                var o = Obstacles.At(x, y);
                if (o == null || (o.key != "water" && o.key != "lava" && o.key != "void") || found.Exists(f => f.Item1 == o.key)) continue;
                bool deep = true;
                for (int dx = -1; dx <= 1 && deep; dx++)
                    for (int dy = -1; dy <= 1 && deep; dy++)
                        deep = Obstacles.At(x + dx, y + dy) == o;
                if (deep) found.Add((o.key, new Vector2Int(x, y)));
            }
        return found;
    }

    // One tile of each kind of obstacle a walker can stand on.
    static List<(string key, Vector2Int cell)> WalkableKinds()
    {
        var found = new List<(string, Vector2Int)>();
        var grid = DungeonGenerator.Current;
        int size = grid.Size;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                var o = Obstacles.At(x, y);
                if (o == null || Obstacles.Blocks(x, y, null) || found.Exists(f => f.Item1 == o.key)) continue;
                found.Add((o.key, new Vector2Int(x, y)));
            }
        return found;
    }

    // The whole island: built, the team lands on a walkable tile; the controlled Pokémon then runs inland (8 tiles a
    // second, twice the walk) for three seconds; the longest frame must stay short.
    IEnumerator IslandWalk()
    {
        var arena = DungeonGenerator.Current;
        if (arenaDecor == null) arenaDecor = arena.tileset;
        Clear();
        var plan = IslandPlan.Generate(new System.Random(55));
        float built = Time.realtimeSinceStartup;
        arena.island = plan;
        arena.Generate();
        built = Time.realtimeSinceStartup - built;
        team.PrepareForNextArena(arena.SpawnPoint, 0f);
        var startCell = arena.CellOf(arena.SpawnPoint);
        bool landed = arena.Walkable(startCell.x, startCell.y) && !Obstacles.Blocks(startCell.x, startCell.y, null);
        yield return new WaitForSeconds(0.5f);
        var leader = team.Controlled;
        Vector2 from = arena.SpawnPoint, to = arena.CentreOf(arena.Size / 2, arena.Size / 2);
        var heading = (to - from).normalized;
        var phases = new List<string>();
        float worst = 0f;
        for (int phase = 0; phase < 3; phase++)
        {
            // 0: still; 1: running, allies' AI off; 2: running with the allies.
            foreach (var m in team.Members)
                if (m != leader && m.GetComponent<AllyAI>() is { } ai) ai.enabled = phase != 1;
            Teleport(leader, from);
            yield return new WaitForSeconds(0.3f);
            float phaseWorst = 0f, started = Time.realtimeSinceStartup, last = started;
            int slow = 0;
            while (Time.realtimeSinceStartup - started < 3f)
            {
                if (phase > 0) Teleport(leader, from + heading * 8f * (Time.realtimeSinceStartup - started));
                yield return null;
                float now = Time.realtimeSinceStartup, frame = now - last;
                last = now;
                if (now - started < 0.05f) continue;
                phaseWorst = Mathf.Max(phaseWorst, frame);
                if (frame > 0.05f) slow++;
            }
            phases.Add($"{(phase == 0 ? "immobile" : phase == 1 ? "course sans IA" : "course avec IA")} {phaseWorst * 1000f:0} ms ({slow} > 50 ms)");
            worst = Mathf.Max(worst, phaseWorst);
        }
        Debug.Log("AISCENARIO île : " + string.Join(" ; ", phases));
        arena.island = null;
        arena.room = new RoomInstance(); // makes RestoreArena rebuild the test arena
        Report(landed && worst < 0.1f, $"construite en {built * 1000f:0} ms, arrivée {(landed ? "praticable" : "BLOQUÉE")}, pire image {worst * 1000f:0} ms");
    }

    // A cliff of level 1 across the arena, the ally with a single straight shot and an opponent on the other side:
    // from above it shoots over the cliff and hits; from below its shots would break on it, so it does not shoot.
    IEnumerator HighGround()
    {
        var arena = DungeonGenerator.Current;
        MoveDef shot = null;
        for (int type = 1; type < 18 && shot == null; type++) // a typed move (type 0 is "Aucun")
            shot = FindMove(type, m => m.family == "projectile" && (string.IsNullOrEmpty(m.path) || m.path == "straight") && m.count <= 1 && m.burst <= 1
                && m.castTime <= 0f && string.IsNullOrEmpty(m.status));
        if (shot == null) { Report(false, "pas d'attaque à distance simple"); yield break; }
        int row = arena.CellOf(Centre).y;
        var hits = new bool[2];
        for (int phase = 0; phase < 2; phase++)
        {
            // 0: the ally above (level 1), the opponent below; 1: the other way round.
            float side = phase == 0 ? 1f : -1f;
            arena.TestCliff(row);
            // 5 tiles apart, well within the shot's reach: the ally stays put and only the cliff decides.
            var (_, ally) = Place(new Vector2(-6f, 2.5f * side), new Vector2(0f, 2.5f * side));
            var kept = (MoveDef[])ally.Moves.Clone();
            for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, i == 0 ? shot : null);
            var foe = Enemy(BatchTeam[1], new Vector2(0f, -2.5f * side));
            foe.maxHp = foe.hp = 100000f;
            yield return new WaitForSeconds(6f);
            hits[phase] = foe.hp < foe.maxHp;
            for (int i = 0; i < Combatant.MoveSlots; i++) ally.LearnMove(i, kept[i]);
            Clear();
            arena.Generate();
            team.PrepareForNextArena(team.transform.position, 0f);
        }
        Report(hits[0] && !hits[1], $"{shot.name} : d'en haut {(hits[0] ? "touche" : "ne touche pas")}, d'en bas {(hits[1] ? "touche (à travers la falaise)" : "ne touche pas")}");
    }

    // First tile of that obstacle, scanning the room.
    static List<Vector2Int> FindCells(string key)
    {
        var cells = new List<Vector2Int>();
        var inside = DungeonGenerator.Current.Interior;
        for (int x = inside.xMin; x < inside.xMax; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                if (Obstacles.At(x, y)?.key == key) cells.Add(new Vector2Int(x, y));
        return cells;
    }

    static Vector2Int? FindCell(string key)
    {
        var inside = DungeonGenerator.Current.Interior;
        for (int x = inside.xMin; x < inside.xMax; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                if (Obstacles.At(x, y)?.key == key) return new Vector2Int(x, y);
        return null;
    }

    // A bare floor tile next to that obstacle (bank) and the obstacle tile beside it.
    static bool Shore(string key, out Vector2 bank, out Vector2 into)
    {
        var grid = DungeonGenerator.Current;
        var inside = grid.Interior;
        bank = into = Vector2.zero;
        for (int x = inside.xMin; x < inside.xMax; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
            {
                if (Obstacles.At(x, y)?.key != key) continue;
                foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                {
                    int bx = x + d.x, by = y + d.y;
                    if (!grid.Walkable(bx, by) || Obstacles.At(bx, by) != null) continue;
                    bank = grid.CentreOf(bx, by);
                    into = grid.CentreOf(x, y);
                    return true;
                }
            }
        return false;
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

    // An enemy whose line has a mega (sprites included) carries its stone: not at full health, but once down to half
    // its HP it mega evolves, takes the mega's form and sprites.
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
        bool early = ultimate.Decide();
        foe.hp = foe.maxHp * 0.45f;
        bool used = ultimate.Decide();
        yield return new WaitForSeconds(0.5f);
        bool form = foe.Mega == mega && foe.Stats.name == mega.name;
        Restore(kept, leader, ally);
        Report(!early && used && ultimate.LastUsed == "mega" && form,
            $"PV pleins : {(early ? "méga trop tôt" : "rien")}, à mi-PV : {(used ? ultimate.LastUsed : "rien")} : {foe.Stats.name}");
    }

    // An elite with a mega stone at 80 % takes a blow of 60 %: it stops at half its HP, mega evolves on its own, heals
    // and recharges faster.
    IEnumerator SecondPhase()
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
        foe.gameObject.AddComponent<EliteMark>();
        var ultimate = foe.gameObject.AddComponent<EnemyUltimate>();
        ultimate.Carry(SpecialItems.Family.Mega, -1, mega);
        float cooldown = foe.cooldownMul;
        foe.hp = foe.maxHp * 0.8f;
        foe.TakeFixedDamage(foe.maxHp * 0.6f);
        float floored = foe.hp / foe.maxHp;
        yield return new WaitForSeconds(1f);
        float after = foe.hp / foe.maxHp;
        bool faster = foe.cooldownMul < cooldown * 0.9f;
        Restore(kept, leader, ally);
        Report(Mathf.Abs(floored - 0.5f) < 0.02f && ultimate.LastUsed == "mega" && after > 0.6f && faster && foe.phaseFloorShare == 0f,
            $"après le coup : {floored:P0} des PV, {(ultimate.LastUsed == "mega" ? "méga" : "pas de méga")}, puis {after:P0}, recharges ×{foe.cooldownMul / cooldown:0.00}");
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

    static void KnockOut(Combatant c)
    {
        c.phaseFloorShare = 0f; // no second phase in the way
        c.TakeFixedDamage(c.hp + 1f);
    }

    // The team member that is neither the controlled one nor "ally" (the team has three for these scenarios).
    Combatant Third(Combatant leader, Combatant ally)
    {
        foreach (var m in team.Members)
            if (m != null && m != leader && m != ally) return m;
        return null;
    }

    // The third member is knocked out far from the controlled Pokémon, nobody around: the ally walks to it and stands
    // on it until it is back.
    IEnumerator AllyRevives()
    {
        var (leader, ally) = Place(new Vector2(-8f, 0f), new Vector2(-3f, 2f), settle: false);
        third = Third(leader, ally);
        if (third == null) { Report(false, "pas de troisième membre"); yield break; }
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
        third = Third(leader, ally);
        if (third == null) { Report(false, "pas de troisième membre"); yield break; }
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
                // A push from a hit or a dodge is not a jolt of its own walking: only its walking counts.
                if (c.IsHurt || c.IsDodging) { lastVel[i] = Vector2.zero; continue; }
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
        if (c != null) c.Hold(null); // a Focus Sash would keep it standing
        if (c != null) KnockOut(c);
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
            spawned[i].Hold(null); // a Focus Sash would keep it standing
            KnockOut(spawned[i]);
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
            if (t != null && (t.name.StartsWith("Objet :") || t.name.StartsWith("Consommable :"))) Destroy(t.gameObject);
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

    // The bag's effects: an Oran Berry heals the controlled Pokémon, a Blast Seed hurts the opponent it is thrown at,
    // a Freeze Orb freezes the room's water.
    IEnumerator BagEffects()
    {
        var (leader, _) = Place(new Vector2(-3f, 0f), new Vector2(-3f, 3f), settle: false);
        var bag = team.GetComponent<Bag>();
        bag.Clear();
        bag.Add("oran");
        bag.Add("blast");
        bag.Add("freeze");
        var grid = DungeonGenerator.Current;
        var c = grid.CellOf(Centre + new Vector2(0f, -5f));
        for (int x = c.x - 3; x <= c.x + 3; x++) Obstacles.Place(x, c.y, "water");
        var foe = Enemy(BatchTeam[1], new Vector2(0f, 0f));
        foe.maxHp = foe.hp = 1000f;
        leader.hp = leader.maxHp * 0.5f;
        float hp = leader.hp;
        bool healed = bag.Use(bag.Find(i => i.key == "oran"), leader) && leader.hp > hp;
        bool hurt = bag.Use(bag.Find(i => i.key == "blast"), leader) && foe.hp < foe.maxHp;
        bool frozen = bag.Use(bag.Find(i => i.key == "freeze"), leader) && Obstacles.At(c.x, c.y)?.key == "frozen";
        bool spent = bag.Count("oran") + bag.Count("blast") + bag.Count("freeze") == 0;
        leader.hp = leader.maxHp;
        Report(healed && hurt && frozen && spent, $"soin {(healed ? "oui" : "non")}, explosion {(hurt ? "oui" : "non")}, eau gelée {(frozen ? "oui" : "non")}, {(spent ? "objets dépensés" : "objets restants")}");
        yield break;
    }

    // An ally under the healing threshold eats an Oran Berry from the shared bag on its own.
    IEnumerator AllyEatsBerry()
    {
        var (leader, ally) = Place(new Vector2(-3f, 0f), new Vector2(-1f, 2f), settle: false);
        var bag = team.GetComponent<Bag>();
        bag.Clear();
        bag.Add("oran", 2);
        ally.hp = ally.maxHp * 0.2f;
        float hp = ally.hp;
        yield return new WaitForSeconds(1.5f);
        bool ate = ally.hp > hp && bag.Count("oran") == 1;
        ally.hp = ally.maxHp;
        Report(ate, $"{(ate ? "baie mangée" : "pas de baie")} : PV {hp:0} → {ally.hp:0}, il reste {bag.Count("oran")} baie(s)");
    }

    // Capacity: one consumable stacks up to 9 in its slot; four kinds fill the four slots; a fifth kind does not fit.
    IEnumerator BagCapacity()
    {
        var bag = team.GetComponent<Bag>();
        bag.Clear();
        bag.Add("oran", 12);
        bool stack = bag.Count("oran") == 9;
        bool fourth = bag.Add("blast") && bag.Add("sleep") && bag.Add("quick");
        bool fifth = bag.Add("petrify");
        bool more = bag.Add("oran"); // its stack is full and no slot is free
        bag.Clear();
        Report(stack && fourth && !fifth && !more, $"pile de {(stack ? 9 : 0)}, 4 sortes {(fourth ? "rangées" : "refusées")}, 5e sorte {(fifth ? "acceptée" : "refusée")}");
        yield break;
    }

    // An elite always drops a consumable; the controlled Pokémon walks onto it and it goes into the bag.
    IEnumerator EliteDropsConsumable()
    {
        var (leader, _) = Place(new Vector2(-3f, 0f), new Vector2(-6f, 3f), settle: false);
        var bag = team.GetComponent<Bag>();
        bag.Clear();
        var foe = Enemy(BatchTeam[1], new Vector2(0f, 0f));
        Loot.Mark(foe);
        KnockOut(foe);
        yield return new WaitForFixedUpdate();
        Transform drop = null;
        foreach (var t in FindObjectsByType<Transform>())
            if (t != null && t.name.StartsWith("Consommable :")) drop = t;
        bool dropped = drop != null;
        string what = dropped ? drop.name : "rien au sol"; // read now: picked up, it is gone
        if (dropped) Teleport(leader, drop.position);
        yield return new WaitForSeconds(0.3f);
        int held = 0;
        foreach (var s in bag.Contents)
            if (s != null) held += s.count;
        ClearDrops();
        bag.Clear();
        Report(dropped && held == 1, $"{what}, {held} objet(s) dans le sac");
    }

    // Held items: a Choice Band raises the attack and slows the recharges, and leaves them as they were once let go;
    // Leftovers heal a little every second; a Balloon floats over water until the first blow bursts it.
    IEnumerator HeldEffects()
    {
        var (leader, _) = Place(new Vector2(-3f, 0f), new Vector2(-6f, 3f), settle: false);
        float atk = leader.attackMul, cd = leader.cooldownMul;
        leader.Hold(HeldItems.Get("choice_band"));
        bool band = Mathf.Approximately(leader.attackMul, atk * 1.5f) && leader.cooldownMul > cd;
        leader.Hold(null);
        bool back = Mathf.Abs(leader.attackMul - atk) < 0.001f && Mathf.Abs(leader.cooldownMul - cd) < 0.001f;
        leader.Hold(HeldItems.Get("leftovers"));
        leader.hp = leader.maxHp * 0.5f;
        float hp = leader.hp;
        yield return new WaitForSeconds(2.2f);
        bool regen = leader.hp > hp;
        leader.Hold(HeldItems.Get("air_balloon"));
        var grid = DungeonGenerator.Current;
        var c = grid.CellOf(Centre + new Vector2(2f, -4f));
        Obstacles.Place(c.x, c.y, "water");
        Vector2 water = grid.CentreOf(c.x, c.y);
        bool floats = !Obstacles.BlocksAt(water, leader);
        leader.TakeHit(5f, GameData.Move(1) ?? FindMove(1, m => true), out _);
        bool burst = leader.Held == null && Obstacles.BlocksAt(water, leader);
        leader.Hold(null);
        leader.hp = leader.maxHp;
        Report(band && back && regen && floats && burst,
            $"Bandeau {(band ? "oui" : "non")} (rendu {(back ? "oui" : "non")}), Restes {(regen ? "oui" : "non")}, Ballon : {(floats ? "flotte" : "tombe")} puis {(burst ? "éclate" : "reste")}");
    }

    // Only Water moves, two identical opponents, the nearer one with Water Absorb: the ally picks the other one.
    IEnumerator AbilityTarget()
    {
        var (_, ally) = Place(new Vector2(-3f, -3f), Vector2.zero);
        var water = FindMove(3, m => m.family == "projectile");
        int neutral = FindSpecies(t => Mathf.Approximately(TypeChart.Multiplier(3, t, out _), 1f));
        if (water == null || neutral < 0)
        {
            Report(false, "pas d'attaque Eau ou pas d'espèce neutre");
            yield break;
        }
        var kept = (MoveDef[])ally.Moves.Clone();
        for (int i = 0; i < ally.Moves.Length; i++) ally.Moves[i] = water;
        var near = Enemy(neutral, new Vector2(3f, 0f));
        var far = Enemy(neutral, new Vector2(0f, 5f));
        near.SetAbility(36); // Absorbe-Eau
        near.maxHp = near.hp = far.maxHp = far.hp = 100000f;
        yield return new WaitForSeconds(Choose);
        var chosen = TargetChoice.Current(ally);
        for (int i = 0; i < kept.Length; i++) ally.Moves[i] = kept[i];
        Report(chosen == far, $"{water.name} ; près {Name(near)} (Absorbe-Eau), loin {Name(far)} ; choisit {Name(chosen)}");
    }

    // AIs and abilities, lot 1 (AbilityAi): an AI that knows them sends no status move to a Magic Bounce nor a
    // Synchronize and shoots a Sableye only now and then; one that knows nothing makes all these mistakes.
    IEnumerator AbilityAiLotOne()
    {
        var (_, ally) = Place(new Vector2(-3f, -3f), Vector2.zero);
        MoveDef status = null, shot = null;
        foreach (var m in GameData.AllMoves)
        {
            if (status == null && m.family == "projectile" && m.power <= 0 && m.range >= 5f && m.status is "paralysis" or "burn" or "poison") status = m;
            if (shot == null && m.family == "projectile" && m.power > 0 && string.IsNullOrEmpty(m.status) && m.path != "arc" && m.range >= 5f) shot = m;
        }
        var sableye = GameData.AllMegas.Find(m => Abilities.ForMega(m)?.reflectShotChance > 0f);
        int neutral = FindSpecies(t => true);
        if (status == null || shot == null || sableye == null)
        {
            Report(false, "pas d'attaque de statut, de tir ou de Méga-Ténéfix");
            yield break;
        }
        if (ally.GetComponent<AllyAI>() is { } ai) ai.enabled = false; // only its decisions are read
        var kept = (MoveDef[])ally.Moves.Clone();
        var foe = Enemy(neutral, new Vector2(4.5f, 0f)); // in reach, beyond the ranged retreat
        foe.maxHp = foe.hp = 100000f;
        yield return new WaitForFixedUpdate();
        var navigator = new Navigator();
        int Pick(float knowledge, params MoveDef[] moves)
        {
            AbilityAi.ForcedKnowledge = knowledge;
            for (int i = 0; i < ally.Moves.Length; i++) ally.LearnMove(i, i < moves.Length ? moves[i] : null);
            return RoleTactics.Decide(ally, navigator, 20f, foe).slot;
        }
        foe.SetAbility(1156); // Miroir Magik
        int bounceKnown = Pick(1f, status, shot), bounceBlind = Pick(0f, status, shot);
        foe.SetAbility(25); // Synchro
        int syncKnown = Pick(1f, status, shot), syncBlind = Pick(0f, status, shot);
        foe.SetAbility(sableye.ability, Abilities.ForMega(sableye));
        int first = Pick(1f, shot), second = Pick(1f, shot), blind = Pick(0f, shot);
        AbilityAi.ForcedKnowledge = -1f;
        for (int i = 0; i < kept.Length; i++) ally.LearnMove(i, kept[i]);
        bool ok = bounceKnown == 1 && bounceBlind == 0 && syncKnown == 1 && syncBlind == 0 && first == 0 && second < 0 && blind == 0;
        var grid = DungeonGenerator.Current;
        string why = ok ? "" : $" [rôle {ally.Role}, distance {Vector2.Distance(ally.transform.position, foe.transform.position):0.0}, portées {status.Reach:0.0} / {shot.Reach:0.0}, "
            + $"ligne {(grid == null || grid.ShotClear(ally.transform.position, foe.transform.position, 0.2f))}, vivant {foe.IsAlive} {ally.IsAlive}, équipes {ally.team} {foe.team}]";
        Report(ok, $"{status.name} / {shot.name}{why} : Miroir Magik {bounceKnown} (sans connaissance {bounceBlind}), Synchro {syncKnown} ({syncBlind}), "
            + $"Ténéfix tir {first} puis {second} ({blind})");
    }

    // Water Absorb heals instead of taking a Water hit; Sturdy leaves 1 HP from full health; Inner Focus is never stunned.
    IEnumerator AbilityRules()
    {
        Place(new Vector2(-3f, -3f), new Vector2(-6f, -3f), settle: false);
        var water = FindMove(3, m => true);
        var other = FindMove(1, m => true) ?? FindMove(7, m => true);
        int neutral = FindSpecies(t => Mathf.Approximately(TypeChart.Multiplier(3, t, out _), 1f));
        var foe = Enemy(neutral, new Vector2(4f, 0f));
        yield return null;
        foe.SetAbility(36);
        foe.hp = foe.maxHp * 0.5f;
        float before = foe.hp;
        bool landed = foe.TakeHit(50f, water, out _);
        bool absorbed = !landed && foe.hp > before;
        foe.SetAbility(13);
        foe.hp = foe.maxHp;
        foe.TakeHit(foe.maxHp * 50f, other, out _);
        bool sturdy = foe.IsAlive && foe.hp <= 1.01f;
        foe.SetAbility(31);
        foe.Stun(1f);
        bool focus = !foe.IsHurt;
        Report(absorbed && sturdy && focus,
            $"absorption {(absorbed ? "oui" : "non")}, Fermeté {(sturdy ? "oui" : "non")}, Attention {(focus ? "oui" : "non")}");
    }

    // Water Absorb on a puddle heals and is not slowed; Sap Sipper on brambles is neither slowed nor hurt.
    IEnumerator AbilityTerrain()
    {
        Place(new Vector2(-3f, -3f), new Vector2(-6f, -3f), settle: false);
        var foe = Enemy(BatchTeam[1], new Vector2(3f, 2f));
        yield return new WaitForFixedUpdate();
        Vector2 at = foe.transform.position;
        foe.SetAbility(36); // Absorbe-Eau
        foe.hp = foe.maxHp * 0.5f;
        Terrain.PaintArea("water", at, 1f, 10f);
        float before = foe.hp;
        yield return new WaitForSeconds(2.2f);
        bool heals = foe.hp > before;
        bool unslowedWater = Mathf.Approximately(Terrain.SpeedMul(foe), 1f);
        Terrain.ClearArea("water", at, 1.5f);
        foe.SetAbility(1157); // Herbivore
        foe.hp = foe.maxHp * 0.5f;
        Terrain.PaintArea("brambles", at, 1f, 10f);
        before = foe.hp;
        yield return new WaitForSeconds(2.2f);
        bool unhurt = foe.hp >= before - 0.01f;
        bool unslowedBrambles = Mathf.Approximately(Terrain.SpeedMul(foe), 1f);
        bool boosted = foe.HasModifier("attack", true);
        Terrain.ClearArea("brambles", at, 1.5f);
        Report(heals && unslowedWater && unhurt && unslowedBrambles && boosted,
            $"Flaque : {(heals ? "soigne" : "ne soigne pas")}, {(unslowedWater ? "pas ralenti" : "ralenti")} ; Ronces : {(unhurt ? "pas de dégâts" : "blessé")}, {(unslowedBrambles ? "pas ralenti" : "ralenti")}, attaque {(boosted ? "+" : "inchangée")}");
    }

    // Wonder Guard shrugs off a neutral hit, which Mold Breaker gets through; Pickpocket takes the item of whoever
    // strikes it at close range.
    IEnumerator AbilityRulesThree()
    {
        var (leader, _) = Place(Vector2.zero, new Vector2(-6f, -3f), settle: false);
        var foe = Enemy(BatchTeam[1], new Vector2(2f, 0f));
        yield return new WaitForFixedUpdate();
        var move = FindMove(1, m => true) ?? FindMove(7, m => true);
        foe.SetAbility(AbilityId("Garde Mystik"));
        float before = foe.hp;
        Combatant.HitSource = leader;
        bool landed = foe.TakeHit(20f, move, out _);
        bool guarded = !landed && Mathf.Approximately(foe.hp, before);
        leader.SetAbility(AbilityId("Brise Moule"));
        bool broken = foe.TakeHit(20f, move, out _) && foe.hp < before;
        Combatant.HitSource = null;
        leader.SetAbility(0);
        foe.SetAbility(AbilityId("Pickpocket"));
        var item = HeldItems.Get("leftovers");
        var kept = leader.Hold(item);
        Combatant.AbilityContact(leader, foe);
        bool stolen = foe.Held == item && leader.Held == null;
        leader.Hold(kept);
        Report(guarded && broken && stolen,
            $"Garde Mystik {(guarded ? "bloque" : "laisse passer")}, Brise Moule {(broken ? "passe" : "bloqué")}, Pickpocket {(stolen ? "vole" : "ne vole pas")}");
    }

    // A walkable spot about "distance" tiles from a point, nothing in the way (a wall, a tree, water).
    static Vector2 FreeNear(Vector2 point, float distance)
    {
        var arena = DungeonGenerator.Current;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.PI / 8f;
            var spot = point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            if (arena == null || (arena.WalkableAt(spot, 0.3f) && !Obstacles.BlocksAt(spot, null))) return spot;
        }
        return point;
    }

    // Méga-Kangourex: during the mega, Parental Bond and the stun of its own effect; back to its ability after.
    IEnumerator MegaAbility()
    {
        var (leader, _) = Place(Vector2.zero, new Vector2(-6f, -3f), settle: false);
        var mega = GameData.AllMegas.Find(m => m.id == 21151);
        if (mega == null) { Report(false, "pas de Méga-Kangourex dans megas.json"); yield break; }
        leader.SetAbility(AbilityId("Fermeté"));
        leader.MegaEvolve(mega);
        yield return null;
        bool during = leader.AbilityId == mega.ability && leader.Ability != null && leader.Ability.echoShare > 0f && leader.Ability.echoStun > 0f;
        // Its second strike lands.
        var foe = Enemy(BatchTeam[1], new Vector2(1.2f, 0f));
        foe.maxHp = foe.hp = 100000f; // the first strike must not knock it out
        yield return new WaitForFixedUpdate();
        var punch = FindMove(1, m => m.family == "melee") ?? FindMove(7, m => true);
        float before = foe.hp;
        Combatant.HitSource = leader;
        foe.TakeHit(leader.DamageOf(punch), punch, out float first);
        Combatant.HitSource = null;
        yield return new WaitForSeconds(0.4f);
        bool echoed = before - foe.hp > first + 0.5f;
        leader.RevertMega();
        bool after = leader.AbilityId == AbilityId("Fermeté") && leader.Ability != null && leader.Ability.echoShare <= 0f;
        leader.SetAbility(0);
        Report(during && echoed && after, $"en méga {(during ? "Amour Filial + étourdissement" : "talent manquant")}, second coup {(echoed ? "oui" : "non")}, retour {(after ? "Fermeté" : "raté")}");
    }

    static int AbilityId(string name)
    {
        foreach (var a in AbilityLibrary.All)
            if (a.name == name) return a.id;
        return 0;
    }

    // The ability demo room (AbilityDemos) plays every case for a moment: no error logged, every ability has a demo.
    IEnumerator AbilityDemosRun()
    {
        var sandbox = FindAnyObjectByType<TerrainSandbox>();
        if (sandbox == null) { Report(false, "pas de terrain de test"); yield break; }
        int errors = 0;
        string first = null;
        void Count(string message, string trace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            errors++;
            first ??= message;
        }
        Application.logMessageReceived += Count;
        var demos = sandbox.gameObject.AddComponent<AbilityDemos>();
        demos.caseTime = 100f; // the scenario moves on itself
        yield return null;
        yield return null;
        demos.ShowVisuals(); // the abilities with a look of their own (the room may open on a shorter list)
        int visuals = demos.Count;
        for (int i = 0; i < visuals; i++)
        {
            demos.Play(i);
            yield return new WaitForSeconds(0.5f);
        }
        demos.ShowAll();
        int cases = demos.Count;
        for (int i = 0; i < cases; i++)
        {
            demos.Play(i);
            yield return new WaitForSeconds(0.5f);
        }
        Application.logMessageReceived -= Count;
        Destroy(demos);
        yield return null;
        int megas = GameData.AllMegas.FindAll(m => Abilities.ForMega(m) != null && PokemonRoster.Has(m.@base)).Count;
        Report(errors == 0 && cases == Abilities.All.Count + megas && visuals > 0,
            $"{cases} démos pour {Abilities.All.Count} talents et {megas} mégas, {visuals} visuels, {errors} erreur(s){(first != null ? " : " + first : "")}");
    }

    // Synchronize sends a burn back to who gave it; Magic Guard takes no poison damage; Shadow Tag keeps the
    // controlled Pokémon from dodging while near, and no longer once the ability is gone.
    IEnumerator AbilityRulesTwo()
    {
        var (leader, _) = Place(Vector2.zero, new Vector2(-6f, -3f), settle: false);
        var foe = Enemy(BatchTeam[1], new Vector2(2f, 0f));
        yield return new WaitForFixedUpdate();
        foe.SetAbility(25); // Synchro
        foe.AddStatus("burn", 3f, leader);
        bool synced = leader.HasStatus("burn");
        leader.Cleanse();
        foe.Cleanse();
        foe.SetAbility(115); // Garde Magik
        foe.AddStatus("poison", 3f, leader);
        float before = foe.hp;
        yield return new WaitForSeconds(1.5f);
        bool guarded = Mathf.Approximately(foe.hp, before);
        foe.Cleanse();
        foe.SetAbility(10); // Marque Ombre
        bool trapped = !leader.TryDodge(Vector2.left);
        foe.SetAbility(0);
        bool free = leader.TryDodge(Vector2.left);
        Report(synced && guarded && trapped && free,
            $"Synchro {(synced ? "oui" : "non")}, Garde Magik {(guarded ? "oui" : "non")}, Marque Ombre {(trapped ? "bloque" : "laisse esquiver")} puis {(free ? "libre" : "bloqué")}");
    }

    // A Focus Sash leaves 1 HP after a knock-out blow, once, and breaks; a Rocky Helmet hurts who strikes it at close range.
    IEnumerator HeldLastStand()
    {
        var (leader, _) = Place(new Vector2(-3f, 0f), new Vector2(-6f, 3f), settle: false);
        leader.Hold(HeldItems.Get("focus_sash"));
        leader.hp = leader.maxHp;
        leader.TakeFixedDamage(leader.maxHp * 3f);
        bool survived = leader.IsAlive && leader.hp <= 1.01f && leader.Held == null;
        leader.hp = leader.maxHp;
        leader.Hold(HeldItems.Get("rocky_helmet"));
        var foe = Enemy(BatchTeam[1], new Vector2(0f, 0f));
        float before = foe.hp;
        HeldItems.OnContact(foe, leader);
        bool hurt = foe.hp < before;
        leader.Hold(null);
        Report(survived && hurt, $"Ceinture Force : {(survived ? "1 PV, brisée" : "rien")}, Casque Brut : {(hurt ? "l'attaquant perd des PV" : "rien")}");
        yield break;
    }

    // Walking onto a held item: the controlled Pokémon takes it; onto another one: it takes that one and the first
    // drops at its feet, without being picked up again at once.
    IEnumerator HeldSwap()
    {
        var (leader, _) = Place(new Vector2(-6f, 0f), new Vector2(-6f, 3f), settle: false);
        leader.Hold(null);
        var loot = FindAnyObjectByType<Loot>();
        loot.PlaceHeld("leftovers", Centre + new Vector2(-2f, 0f));
        loot.PlaceHeld("choice_scarf", Centre + new Vector2(2f, 0f));
        Teleport(leader, Centre + new Vector2(-2f, 0f));
        yield return new WaitForSeconds(0.3f);
        bool first = leader.Held?.key == "leftovers";
        Teleport(leader, Centre + new Vector2(2f, 0f));
        yield return new WaitForSeconds(0.5f);
        bool second = leader.Held?.key == "choice_scarf";
        bool dropped = System.Array.Exists(FindObjectsByType<Transform>(), t => t != null && t.name.StartsWith("Objet tenu : Restes"));
        loot.ClearGround();
        leader.Hold(null);
        Report(first && second && dropped, $"{(first ? "Restes pris" : "Restes pas pris")}, {(second ? "Mouchoir pris" : "Mouchoir pas pris")}, {(dropped ? "Restes au sol" : "Restes disparus")}");
    }

    // An enemy holding an item drops it when knocked out.
    IEnumerator EnemyDropsHeld()
    {
        Place(new Vector2(-6f, 0f), new Vector2(-6f, 3f), settle: false);
        var foe = Enemy(BatchTeam[1], new Vector2(3f, 0f));
        foe.Hold(HeldItems.Get("life_orb"));
        KnockOut(foe);
        yield return new WaitForFixedUpdate();
        bool dropped = System.Array.Exists(FindObjectsByType<Transform>(), t => t != null && t.name == "Objet tenu : Orbe Vie");
        FindAnyObjectByType<Loot>().ClearGround();
        Report(dropped, dropped ? "Orbe Vie au sol" : "rien au sol");
    }

    // Tiers: a Choice Band I < II < III; picking the same item of the same tier raises it one tier, a higher one is kept,
    // another item replaces it and the old one drops.
    IEnumerator HeldTiers()
    {
        var band = HeldItems.Get("choice_band");
        float a1 = HeldItems.AtTier(band, 1).attackMul, a2 = HeldItems.AtTier(band, 2).attackMul, a3 = HeldItems.AtTier(band, 3).attackMul;
        bool rising = a1 < a2 && a2 < a3 && Mathf.Abs(HeldItems.AtTier(band, 2).cooldownMul - band.cooldownMul) < 0.001f;
        var up = HeldItems.Merge(HeldItems.AtTier(band, 1), HeldItems.AtTier(band, 1), out var d1);
        var keep = HeldItems.Merge(HeldItems.AtTier(band, 3), HeldItems.AtTier(band, 2), out var d2);
        var swap = HeldItems.Merge(HeldItems.AtTier(band, 2), HeldItems.Get("air_balloon"), out var d3);
        bool merge = up.tier == 2 && d1 == null && keep.tier == 3 && d2 == null && swap.key == "air_balloon" && d3 != null && d3.tier == 2;
        Report(rising && merge, $"Bandeau ×{a1:0.00} / ×{a2:0.00} / ×{a3:0.00}, I+I → {up.FullName}, III+II → {keep.FullName}, autre objet : {d3?.FullName} tombe");
        yield break;
    }

    // Limits: a Blast Seed takes 5 % of a boss's health (25 % of another's), a Rocky Helmet 2 % of a boss's; a Life Orb
    // costs its toll once for an attack that hits several times.
    IEnumerator ItemLimits()
    {
        var (leader, _) = Place(new Vector2(-3f, 0f), new Vector2(-6f, 3f), settle: false);
        var bag = team.GetComponent<Bag>();
        bag.Clear();
        bag.Add("blast");
        var boss = Enemy(BatchTeam[1], new Vector2(0f, 0f));
        boss.isBoss = true;
        boss.maxHp = boss.hp = 1000f;
        bag.Use(bag.Find(i => i.key == "blast"), leader);
        bool blast = Mathf.Abs(boss.maxHp - boss.hp - 50f) < 1f;
        leader.Hold(HeldItems.Get("rocky_helmet"));
        float before = boss.hp;
        HeldItems.OnContact(boss, leader);
        bool helmet = Mathf.Abs(before - boss.hp - 20f) < 1f;
        leader.Hold(HeldItems.Get("life_orb"));
        float keptHp = leader.maxHp;
        leader.maxHp = leader.hp = 1000f;
        var move = GameData.Move(1) ?? FindMove(1, m => true);
        leader.TryUseMove(0, Vector2.right); // one attack...
        leader.HitLanded(move, 10f);
        leader.HitLanded(move, 10f);
        leader.HitLanded(move, 10f); // ...that hits three times
        bool orb = Mathf.Abs(leader.maxHp - leader.hp - 30f) < 1f;
        leader.Hold(null);
        leader.maxHp = leader.hp = keptHp;
        Report(blast && helmet && orb, $"Explo sur un boss : {(blast ? "5 %" : "autre")}, Casque Brut : {(helmet ? "2 %" : "autre")}, Orbe Vie : {(orb ? "une fois" : "plusieurs fois")}");
        yield break;
    }

    // Every consumable and held item says what it does (the HUD shows it), tiers included.
    IEnumerator ItemsDescribed()
    {
        var missing = new List<string>();
        foreach (var i in BagItems.All)
            if (string.IsNullOrEmpty(BagItems.Describe(i))) missing.Add(i.name);
        foreach (var i in HeldItems.All)
            if (string.IsNullOrEmpty(HeldItems.Describe(i))) missing.Add(i.name);
        string band = HeldItems.Describe(HeldItems.AtTier(HeldItems.Get("choice_band"), 3));
        Report(missing.Count == 0 && band.Contains("70"), missing.Count == 0 ? $"toutes décrites, ex. Bandeau Choix III : {band}" : $"sans description : {string.Join(", ", missing)}");
        yield break;
    }

    // The same banner twice shows "×2" on one line; five different ones leave the last three.
    IEnumerator BannersGrouped()
    {
        var progression = team.GetComponent<Progression>();
        progression.Announce("Test : baie", Notice.Loot);
        progression.Announce("Test : baie", Notice.Loot);
        int twice = progression.TimesShown("Test : baie");
        for (int k = 0; k < 5; k++) progression.Announce($"Test {k}");
        int count = progression.BannerCount;
        Report(twice == 2 && count == 3, $"même texte : ×{twice}, après cinq autres : {count} bandeau(x)");
        yield break;
    }

    // Every sub-biome of populations.json has enough Pokémon of its types; lots drawn there are of those types (or
    // forced in); the night brings its own types; stage and levels rise with the quarter and the distance.
    IEnumerator HabitatsPopulated()
    {
        var unknown = Populations.UnknownTypes().ToList();
        var rng = new System.Random(21);
        var problems = new List<string>(unknown.Select(n => $"type inconnu {n}"));
        var solo = Encounters.CategoryOf("solo");
        int habitats = 0, members = 0;
        foreach (var key in Populations.Habitats)
        {
            habitats++;
            var where = new Populations.Where(key);
            var pool = Encounters.Pool(1, -1, where);
            if (pool.Count == 0 || !pool.All(id => Populations.Lives(key, id, false))) problems.Add($"{key} : trop peu de Pokémon (réservoir général)");
            for (int k = 0; k < 6 && solo != null; k++)
            {
                var e = Encounters.Make(rng, solo, 1, -1, where: where);
                foreach (var m in e?.members ?? new List<Encounters.Member>())
                {
                    members++;
                    if (!Populations.Lives(key, m.pokemonId, false)) problems.Add($"{key} : {GameData.Pokemon(m.pokemonId)?.name} hors population");
                }
            }
        }
        bool rises = Populations.Stage(0, 0f) < Populations.Stage(3, 1f) && Populations.LevelBonus(0, 0f) < Populations.LevelBonus(1, 0f)
                     && Populations.LevelBonus(0, 0f) < Populations.LevelBonus(0, 1f);
        var meadow = Encounters.Pool(1, -1, new Populations.Where("meadow")).Distinct().Count();
        var meadowNight = Encounters.Pool(1, -1, new Populations.Where("meadow", true)).Distinct().Count();
        Report(problems.Count == 0 && rises && meadowNight > meadow,
            problems.Count > 0 ? string.Join(", ", problems.Take(5))
                : $"{habitats} sous-biomes, {members} Pokémon tirés dans leur population ; prairie : {meadow} espèces le jour, {meadowNight} la nuit ; étape {Populations.Stage(0, 0f)} → {Populations.Stage(3, 1f)}");
        yield break;
    }

    // The test quarter with its population: camps, patrols and spawn points placed on open land; the camps' lots live
    // in their sub-biome. A camp rests while the team is far, wakes when it comes close, goes back to rest once the team
    // has run off; a patrol walks its round; a beaten camp gets another lot later, out of the team's sight.
    IEnumerator QuarterPopulated()
    {
        var arena = DungeonGenerator.Current;
        if (arenaDecor == null) arenaDecor = arena.tileset;
        Clear();
        var run = gameObject.AddComponent<IslandRun>();
        run.Begin(IslandPlan.QuarterTestPlan(17));
        var population = GetComponent<IslandPopulation>();
        yield return new WaitForSeconds(0.5f);
        var places = population != null ? population.places : new List<IslandPopulation.Place>();
        var camps = places.Where(p => p.sort == IslandPopulation.Sort.Camp).ToList();
        var patrols = places.Where(p => p.sort == IslandPopulation.Sort.Patrol).ToList();
        int spawns = places.Count(p => p.sort == IslandPopulation.Sort.Spawn);
        // Day or night population: the clock starts at random (DayNight), so a camp may be a night one.
        bool fitting = camps.All(c => c.lot != null && c.lot.members.All(m => Populations.Lives(c.habitat, m.pokemonId, true) || m.lot != c.lot.category));
        string result = $"{camps.Count} camps, {patrols.Count} rondes, {spawns} points d'apparition";
        bool ok = camps.Count > 0 && patrols.Count > 0 && fitting;
        var members = team.Members.ToArray();
        var kept = Sturdy(members);
        var camp = camps.FirstOrDefault(c => c.spawned.Count > 0);
        if (ok && camp != null)
        {
            var group = camp.spawned[0].GetComponent<EnemyAI>().group;
            Vector2 home = run.Cell(camp.tile);
            // The spot farthest from the camp: far, and out of sight.
            Vector2 far = places.Select(p => run.Cell(p.tile)).OrderByDescending(t => Vector2.Distance(t, home)).First();
            foreach (var m in members) Teleport(m, far);
            yield return new WaitForSeconds(1f);
            bool restsFar = !group.Engaged;
            // Right by a member of the camp (its home spot may lie in a wall or a tree): it must wake within 2 s.
            Vector2 sleeper = camp.spawned[0].transform.position;
            foreach (var m in members) Teleport(m, FreeNear(sleeper, 2f));
            bool wakes = false;
            for (float t = 0f; t < 2f && !wakes; t += Time.fixedDeltaTime)
            {
                yield return new WaitForFixedUpdate();
                wakes = group.Engaged;
            }
            foreach (var m in members) Teleport(m, far);
            yield return new WaitForSeconds(group.roam.giveUp + 1.5f);
            bool backToRest = !group.Engaged;
            // A patrol walks its round while the team is away.
            bool walks = false;
            var patrol = patrols.FirstOrDefault(p => p.spawned.Count > 0);
            if (patrol != null)
            {
                Vector2 before = patrol.spawned[0].transform.position;
                yield return new WaitForSeconds(3f);
                walks = patrol.spawned[0] != null && Vector2.Distance(before, patrol.spawned[0].transform.position) > 1f;
            }
            // Beaten, then free again: another lot comes while the team is far.
            foreach (var c in camp.spawned.ToList()) if (c != null) KnockOut(c);
            yield return new WaitForSeconds(0.5f);
            bool beaten = camp.Beaten && camp.lot == null;
            camp.freeAt = 0f;
            yield return new WaitForSeconds(0.5f);
            bool back = camp.lot != null && camp.spawned.Count > 0;
            ok = restsFar && wakes && backToRest && (patrol == null || walks) && beaten && back;
            result += $" ; camp : au repos de loin {restsFar}, réveillé {wakes}, revenu au repos {backToRest} ; ronde {(patrol == null ? "aucune en jeu" : walks ? "en marche" : "immobile")} ; battu {beaten}, autre lot {back}";
        }
        else if (camp == null) { ok = false; result += " ; aucun camp en jeu"; }
        Restore(kept, members);
        run.End();
        foreach (var c in new MonoBehaviour[] { GetComponent<KeyDungeon>(), GetComponent<BossPortal>(), GetComponent<TimeGears>(), GetComponent<IslandPopulation>(), GetComponent<SecretStairs>(), GetComponent<CaveSeal>(), GetComponent<CaveStairs>(), run })
            if (c != null) Destroy(c);
        arena.room = new RoomInstance(); // makes RestoreArena rebuild the test arena
        Report(ok, result);
    }

    // The test quarter's secret stairs (in a clearing) are taken three times, with each content forced: a treasure (a
    // chest, the sub-biome's look), an ambush (the way out shut until its waves are beaten, then a chest), a guardian
    // (an elite easier to recruit, asleep by a chest).
    IEnumerator IslandRoomContents()
    {
        var arena = DungeonGenerator.Current;
        if (arenaDecor == null) arenaDecor = arena.tileset;
        Clear();
        IslandPlan plan = null;
        for (int seed = 1; seed <= 40 && plan == null; seed++)
        {
            var p = IslandPlan.QuarterTestPlan(seed);
            if (p.secret.x >= 0 && p.secretSpot == "clearing") plan = p;
        }
        if (plan == null) { Report(false, "pas de quart de test avec un escalier secret en clairière"); yield break; }
        var members = team.Members.ToArray();
        var kept = Sturdy(members);
        var run = gameObject.AddComponent<IslandRun>();
        var loot = FindAnyObjectByType<Loot>();
        var lines = new List<string>();
        bool ok = true;
        foreach (var key in new[] { RoomContent.Treasures, RoomContent.Ambushes, RoomContent.Guardians })
        {
            run.End();
            run.Begin(plan);
            var stairs = GetComponent<SecretStairs>();
            stairs.forceContent = key;
            yield return new WaitForSecondsRealtime(0.5f);
            foreach (var m in members) Teleport(m, run.Cell(plan.secret));
            float until = Time.realtimeSinceStartup + 6f;
            while ((stairs.Content == null || LoadingScreen.Busy) && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(0.5f);
            var content = stairs.Content;
            if (content == null) { ok = false; lines.Add($"{key} : salle jamais atteinte"); continue; }
            bool chest = loot != null && loot.ChestAt != null;
            string line = $"{key} : salle {arena.room?.theme?.key}/{arena.tileset?.romId}";
            if (key == RoomContent.Ambushes)
            {
                bool shut = content.Locked;
                int beaten = 0;
                float end = Time.time + 25f;
                while (!content.Done && Time.time < end)
                {
                    foreach (var c in Combatant.All.ToList())
                        if (c != null && c.IsAlive && c.team == Team.Enemy) { KnockOut(c); beaten++; }
                    yield return new WaitForSeconds(0.5f);
                }
                yield return new WaitForSeconds(0.3f);
                chest = loot != null && loot.ChestAt != null;
                ok &= shut && content.Done && !content.Locked && beaten > 0 && chest;
                line += $", sortie fermée {shut}, {beaten} ennemis battus, rouverte {!content.Locked}, coffre {chest}";
            }
            else if (key == RoomContent.Guardians)
            {
                var guard = Combatant.All.FirstOrDefault(c => c != null && c.IsAlive && c.team == Team.Enemy && c.GetComponent<RecruitBonus>() != null);
                bool resting = guard != null && guard.GetComponent<EnemyAI>().group is { Engaged: false };
                ok &= guard != null && guard.IsElite && chest && resting;
                line += $", gardien {(guard != null ? guard.Stats.name : "absent")} (élite {guard != null && guard.IsElite}, endormi {resting}), coffre {chest}";
            }
            else
            {
                ok &= chest && !content.Locked;
                line += $", coffre {chest}";
            }
            lines.Add(line);
        }
        Restore(kept, members);
        run.End();
        if (loot != null) loot.RemoveChest();
        foreach (var c in new MonoBehaviour[] { GetComponent<KeyDungeon>(), GetComponent<BossPortal>(), GetComponent<TimeGears>(), GetComponent<IslandPopulation>(), GetComponent<SecretStairs>(), GetComponent<CaveSeal>(), GetComponent<CaveStairs>(), run })
            if (c != null) Destroy(c);
        arena.room = new RoomInstance(); // makes RestoreArena rebuild the test arena
        Report(ok, string.Join(" ; ", lines));
    }

    // A whole island: each quarter has its way of giving its gear (the last one a boss); onward passages and the centre
    // wait for the gears, the way back never does.
    IEnumerator GearsLockPassages()
    {
        var arena = DungeonGenerator.Current;
        if (arenaDecor == null) arenaDecor = arena.tileset;
        Clear();
        var run = gameObject.AddComponent<IslandRun>();
        var plan = IslandPlan.Generate(new System.Random(77));
        run.Begin(plan);
        yield return new WaitForSeconds(0.3f);
        var gears = GetComponent<TimeGears>();
        var o = plan.order;
        bool lastBoss = gears.quarters[o[3]] != null && gears.quarters[o[3]].mode == TimeGears.Boss;
        bool shut = !gears.CanPass(o[0], o[1]) && !gears.CanPass(o[3], IslandPlan.Centre);
        bool back = gears.CanPass(o[1], o[0]);
        gears.quarters[o[0]].gear = true;
        bool opens = gears.CanPass(o[0], o[1]) && !gears.CanPass(o[1], o[2]);
        foreach (var q in gears.quarters) q.gear = true;
        bool centre = gears.CanPass(o[3], IslandPlan.Centre);
        string modes = string.Join(", ", o.Select(q => gears.quarters[q].mode));
        EndIsland(run);
        Report(lastBoss && shut && back && opens && centre,
            $"modes {modes} ; fermés au départ {shut}, retour libre {back}, ouvert avec l'engrenage {opens}, centre avec les quatre {centre}");
    }

    // The test quarter with a boss: its islet's portal stays shut; the key dungeon's two rooms (an ambush, then a
    // guardian by a chest) give the key; the islet's boss is big, ready to mega evolve (or Terastallize), and its fall
    // gives the gear. The legendary comes about 3 % of the time. Then with fragments: three, each with a hint.
    IEnumerator GearsFromBossAndFragments()
    {
        var arena = DungeonGenerator.Current;
        if (arenaDecor == null) arenaDecor = arena.tileset;
        Clear();
        var members = team.Members.ToArray();
        var kept = Sturdy(members);
        var run = gameObject.AddComponent<IslandRun>();
        var plan = IslandPlan.QuarterTestPlan(23);
        run.Begin(plan);
        var gears = GetComponent<TimeGears>();
        gears.ForceTestMode(TimeGears.Boss);
        yield return new WaitForSeconds(0.5f);
        var q = gears.Here;
        var q0 = q;
        var dungeon = GetComponent<KeyDungeon>();
        var portal = GetComponent<BossPortal>();
        var loot = FindAnyObjectByType<Loot>();
        int legendaries = 0;
        for (int k = 0; k < 600; k++) { gears.PickBoss("test", out bool legend); if (legend) legendaries++; }
        // The portal, without the key: nothing happens.
        IEnumerator Walk(Vector2? to, Vector2? from = null)
        {
            if (to == null) yield break;
            foreach (var m in members) Teleport(m, from ?? to.Value + Vector2.down * 3f);
            yield return new WaitForSeconds(0.3f);
            foreach (var m in members) Teleport(m, to.Value);
            float until = Time.realtimeSinceStartup + 6f;
            yield return new WaitForSecondsRealtime(0.3f);
            while (LoadingScreen.Busy && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(0.5f);
        }
        IEnumerator Clean(IslandStairs stairs)
        {
            float end = Time.time + 25f;
            while (stairs.Content != null && !stairs.Content.Done && Time.time < end)
            {
                foreach (var c in Combatant.All.ToList())
                    if (c != null && c.IsAlive && c.team == Team.Enemy) KnockOut(c);
                if (loot != null && loot.ChestAt is { } chest) foreach (var m in members) Teleport(m, chest);
                yield return new WaitForSeconds(0.5f);
            }
        }
        // The islet's bridge, from the coast at its foot (the islet itself is only seen).
        Vector2 coast = run.Cell(q.portalTile);
        bool bridged = q.HasBridge;
        bool landmark = plan.caves.Contains(q.dungeonTile) || (plan.height != null && plan.height[plan.Index(q.dungeonTile.x, q.dungeonTile.y)] > 0)
                        || plan.clearings.Any(c => Vector2.Distance(c, q.dungeonTile) < 2f);
        yield return Walk(portal.StairsAt, coast);
        bool shut = !portal.InRoom;
        // The key dungeon: room after room.
        yield return Walk(dungeon.StairsAt);
        bool inDungeon = dungeon.InRoom;
        yield return Clean(dungeon);
        string dungeonTheme = arena.room?.theme?.key;
        yield return Walk(dungeon.NextAt);
        bool second = dungeon.InRoom && dungeon.Floor == 1;
        // The guardian: evolved, ready to Terastallize, with two minions holding Z crystals.
        var guards = Combatant.All.Where(c => c != null && c.IsAlive && c.team == Team.Enemy).ToList();
        var guardian = guards.FirstOrDefault(c => c.IsElite);
        bool guardianReady = guardian != null && Encounters.Depth(guardian.Stats) >= 1 && guardian.GetComponent<EnemyUltimate>()?.Item == SpecialItems.Family.Tera
                             && guards.Count(c => c.GetComponent<EnemyUltimate>()?.Item == SpecialItems.Family.Z) == 2;
        yield return Clean(dungeon);
        yield return new WaitForSeconds(0.3f);
        bool key = q.hasKey;
        yield return Walk(dungeon.BackAt);
        // The islet: out on the bridge.
        yield return Walk(portal.StairsAt, coast);
        var bosses = gears.BossInPlay.Where(c => c != null).ToList();
        bool big = bosses.Count > 0 && bosses.All(c => c.transform.localScale.x >= 1.5f);
        bool phase = bosses.Count > 0 && bosses.All(c => c.GetComponent<EnemyUltimate>() is { } u && (u.Item == SpecialItems.Family.Mega || u.Item == SpecialItems.Family.Tera));
        string who = string.Join(" + ", bosses.Select(c => c.Stats.name));
        // Its four moves are its fixed set (the bubbles 1 to 4 can be learnt).
        bool fixedSet = bosses.Count > 0 && TimeGears.BossMoves(q.boss) is { } set
                        && bosses[0].Moves.Where(m => m != null).Select(m => m.id).SequenceEqual(set.Take(Combatant.MoveSlots));
        bool locked = portal.Content != null && portal.Content.Locked;
        yield return Clean(portal);
        yield return new WaitForSeconds(0.3f);
        bool gear = q.gear && gears.Found == 1;
        yield return Walk(portal.BackAt);
        loot?.RemoveChest();
        // Fragments (the team back at the arrival).
        foreach (var m in members) Teleport(m, arena.SpawnPoint);
        yield return new WaitForSeconds(0.2f);
        gears.ForceTestMode(TimeGears.Fragments);
        yield return new WaitForSeconds(0.3f);
        q = gears.Here;
        int fragments = q.fragments.Count, hints = run.hints.Count;
        foreach (var f in q.fragments)
        {
            foreach (var m in members) Teleport(m, run.Cell(f.tile));
            yield return new WaitForSeconds(0.3f);
        }
        bool together = q.gear && q.fragments.All(f => f.taken) && run.hints.Count == 0;
        Restore(kept, members);
        EndIsland(run);
        float rate = legendaries / 6f;
        bool themed = dungeonTheme == TimeGears.DungeonTheme(q0);
        Report(bridged && landmark && shut && inDungeon && themed && second && guardianReady && key && big && phase && fixedSet && locked && gear && rate > 0.5f && rate < 7f && fragments == 3 && hints == 3 && together,
            $"pont {bridged}, donjon sur un point d'intérêt {landmark}, portail fermé sans clé {shut} ; donjon {inDungeon} (thème {dungeonTheme}, attendu {TimeGears.DungeonTheme(q0)}), 2e salle {second}, gardien {guardian?.Stats.name} prêt {guardianReady}, clé {key} ; îlot : {who} (grand {big}, seconde phase prête {phase}, jeu fixe {fixedSet}, sortie fermée {locked}), engrenage {gear} ; légendaire {rate:0.0} % ; {fragments} fragments, {hints} indices, engrenage reconstitué {together}");
    }

    void EndIsland(IslandRun run)
    {
        run.End();
        foreach (var c in new MonoBehaviour[] { GetComponent<KeyDungeon>(), GetComponent<BossPortal>(), GetComponent<TimeGears>(), GetComponent<IslandPopulation>(), GetComponent<SecretStairs>(), GetComponent<CaveSeal>(), GetComponent<CaveStairs>(), run })
            if (c != null) Destroy(c);
        DungeonGenerator.Current.room = new RoomInstance(); // makes RestoreArena rebuild the test arena
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

    // The same scenario as on a large map (the island): the path search stops after a few tiles and gives a partial
    // path, so the far bank never looks out of reach; the ally must still notice it is stuck and repair.
    IEnumerator LargeMap(IEnumerator body)
    {
        Navigator.Limit = 60;
        try
        {
            while (body.MoveNext()) yield return body.Current;
        }
        finally
        {
            Navigator.ResetLimit();
        }
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

    /// Batch runs: only the scenarios whose name holds one of these words ("a|b"; empty or null: all of them).
    public static string Filter;

    IEnumerator Scenario(string name, IEnumerator body)
    {
        if (!string.IsNullOrEmpty(Filter) && !Filter.Split('|').Any(w => w.Length > 0 && name.Contains(w, System.StringComparison.OrdinalIgnoreCase)))
            yield break;
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
