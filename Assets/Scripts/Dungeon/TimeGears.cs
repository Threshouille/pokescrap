using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static HudLayout;

/// The Time Gears of the island (Resources/Data/time_gears.json; GDD « L'île flottante »): the passage to the next
/// quarter opens with the quarter's gear, the centre with all four. When the island is made, each quarter gets the
/// way it gives its gear: a boss on its islet (a portal on the coast, opened by the key found at the end of the
/// quarter's key dungeon: KeyDungeon, BossPortal; a big Pokémon that mega evolves at its second phase, or rarely the
/// theme's legendary, which Terastallizes), three fragments in three sub-biomes (guarded by an elite asleep beside
/// them, or up in the sub-biome's highest nook; a hint on the mini-map), or a quest (a boss until the quests exist).
/// The last quarter of the order always ends with a boss. Only the zone being played has its guards and pickups.
/// Added by IslandRun.
public class TimeGears : MonoBehaviour
{
    [Serializable] class Modes { public int boss = 2, fragments = 1, quest = 1; }
    [Serializable] class Weighted { public string key; public float weight = 1f; }

    [Serializable]
    class BossSettings
    {
        public string roomType = "boss", keyIcon = "pmd_Key_White", chestIcon;
        public float dungeonShare = 0.45f, portalShare = 0.5f, legendaryChance = 0.03f, optionWeight = 3f, scale = 1.6f;
        public float health = 8f, stoneChance = 0.3f;
        public int level = 3;
        public string[] dungeonRooms = { "ambush", "guardian" };
    }

    /// One Pokémon of a boss: ids (the first with sprites), and its second phase: its mega, or a Tera shard.
    [Serializable] public class Member { public int[] pokemon = new int[0]; public bool mega, tera; }
    [Serializable] public class Option { public float weight = 1f; public Member[] members = new Member[0]; }
    [Serializable] class ThemeBosses { public string theme; public int[] legendary = new int[0]; public Option[] options = new Option[0]; }

    [Serializable]
    class FragmentSettings
    {
        public int count = 3;
        public float hint = 4f, wake = 5f, leash = 10f;
        public string eliteLot = "champion";
        public Weighted[] guards = new Weighted[0];
    }

    [Serializable]
    class File
    {
        public int baseLevel = 2;
        public Modes modes = new();
        public string questAs = "boss", testMode = "boss";
        public BossSettings boss = new();
        public ThemeBosses[] bosses = new ThemeBosses[0];
        public FragmentSettings fragments = new();
    }

    static File data;
    static File Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/time_gears");
            data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return data;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => data = null;

    public const string Boss = "boss", Fragments = "fragments", Quest = "quest";
    const float PickReach = 0.9f, RefuseEvery = 2f;
    static readonly Color FragmentHint = new(0.98f, 0.82f, 0.3f);

    public class Fragment
    {
        public Vector2Int tile, hint;
        public string habitat, guard;
        public bool taken, guardBeaten;
        public readonly List<Combatant> spawned = new();
    }

    /// How a quarter gives its gear, and how far the team got.
    public class Quarter
    {
        public string mode;
        public bool gear, bossBeaten, hasKey;
        public Vector2Int dungeonTile = new(-1, -1), portalTile = new(-1, -1); // the key dungeon's entrance, the islet's portal
        public Option option; // the boss
        public bool legendary;
        public int rank;
        public float distance; // the portal: 0 at the arrival, 1 at the farthest tile
        public readonly List<Fragment> fragments = new();
        public readonly RoomContent[] rooms = new RoomContent[4]; // the key dungeon's rooms, then the islet's
    }

    /// The quarters' (null for those the plan lacks).
    public readonly Quarter[] quarters = new Quarter[4];

    /// Gears found, and how many there are to find (one per quarter of the plan).
    public int Found => quarters.Count(q => q != null && q.gear);
    public int Total => quarters.Count(q => q != null);

    /// The quarter being played (tests, the stairs), and the boss on its islet (tests).
    public Quarter Here => island != null && island.Zone >= 0 && island.Zone < 4 ? quarters[island.Zone] : null;
    public IReadOnlyList<Combatant> BossInPlay => boss;

    IslandRun island;
    IslandPlan madeFor;
    EnemySpawner spawner;
    PlayerController team;
    Progression progression;
    readonly System.Random rng = new();
    readonly List<Combatant> boss = new();
    readonly List<(GameObject go, Fragment fragment)> pickups = new();
    float refusedAt = -10f;
    bool testPlan;

    void Awake()
    {
        island = GetComponent<IslandRun>();
        island.Built += OnBuilt;
    }

    void OnDestroy()
    {
        if (island != null) island.Built -= OnBuilt;
        Clear();
    }

    /// Forgets the island (it is left).
    public void Clear()
    {
        Despawn();
        for (int q = 0; q < 4; q++) quarters[q] = null;
        madeFor = null;
    }

    void OnBuilt()
    {
        spawner = FindAnyObjectByType<EnemySpawner>();
        team = FindAnyObjectByType<PlayerController>();
        progression = team != null ? team.GetComponent<Progression>() : null;
        if (island.Plan != madeFor) Make(island.Plan);
        Despawn();
        SpawnZone();
    }

    /// Whether the team may take a passage from zone "from" to zone "to": onward with the quarter's gear, to the
    /// centre with all of them; back always.
    public bool CanPass(int from, int to)
    {
        if (madeFor == null) return true;
        if (to == IslandPlan.Centre) return Found >= Total;
        int at = Array.IndexOf(madeFor.order, from), next = Array.IndexOf(madeFor.order, to);
        if (at < 0 || next != at + 1) return true;
        return quarters[from] == null || quarters[from].gear;
    }

    /// The team stepped on a locked passage end: a word (not every frame).
    public void Refuse(int to)
    {
        if (Time.time - refusedAt < RefuseEvery) return;
        refusedAt = Time.time;
        progression?.Announce(to == IslandPlan.Centre ? $"Il faut les {Total} Engrenages du Temps pour le centre"
            : "Il faut l'Engrenage du Temps de ce quartier", Notice.Alert);
    }

    // ---- Making ----

    void Make(IslandPlan plan)
    {
        madeFor = plan;
        for (int q = 0; q < 4; q++) quarters[q] = null;
        if (plan == null) return;
        testPlan = plan.themes[0] != null && plan.themes[0].key == "test";
        var population = GetComponent<IslandPopulation>();
        var modes = new List<string>();
        for (int k = 0; k < Data.modes.boss; k++) modes.Add(Boss);
        for (int k = 0; k < Data.modes.fragments; k++) modes.Add(Fragments);
        for (int k = 0; k < Data.modes.quest; k++) modes.Add(Quest);
        // The last quarter of the order always ends with a boss; the others draw what is left.
        modes.Remove(Boss);
        for (int i = modes.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (modes[i], modes[j]) = (modes[j], modes[i]);
        }
        int quarterCount = testPlan ? 1 : 4;
        for (int k = 0; k < quarterCount; k++)
        {
            int q = plan.order[k];
            string mode = testPlan ? Data.testMode : k == quarterCount - 1 ? Boss : k < modes.Count ? modes[k] : Boss;
            quarters[q] = MakeQuarter(plan, q, k, mode, population);
        }
    }

    Quarter MakeQuarter(IslandPlan plan, int q, int rank, string mode, IslandPopulation population)
    {
        var quarter = new Quarter { mode = mode, rank = rank };
        var land = IslandPopulation.Land(plan, q, out var arrival);
        if (land.Count == 0) { quarter.gear = true; return quarter; }
        float far = land.Max(t => Vector2.Distance(t, arrival));
        var camps = population != null ? population.places.Where(p => p.quarter == q).Select(p => p.tile).ToList() : new List<Vector2Int>();
        bool ClearOfCamps(Vector2Int t, float gap) => camps.All(c => Vector2.Distance(c, t) >= gap);
        int Height(Vector2Int t) => plan.height != null ? plan.height[plan.Index(t.x, t.y)] : 0;
        if (mode != Fragments)
        {
            // The key dungeon's entrance: far from the arrival, clear of the camps; the portal: on the coast (open
            // land by the sky), farther still, away from the dungeon.
            var deep = land.Where(t => Vector2.Distance(t, arrival) >= Data.boss.dungeonShare * far && ClearOfCamps(t, 6f)).ToList();
            quarter.dungeonTile = (deep.Count > 0 ? deep : land)[rng.Next((deep.Count > 0 ? deep : land).Count)];
            var coast = land.Where(t => ByTheSky(plan, t) && Vector2.Distance(t, arrival) >= Data.boss.portalShare * far
                                        && Vector2.Distance(t, quarter.dungeonTile) >= 15f && ClearOfCamps(t, 6f)).ToList();
            if (coast.Count == 0) coast = land.Where(t => Vector2.Distance(t, quarter.dungeonTile) >= 15f).ToList();
            if (coast.Count == 0) coast = land;
            quarter.portalTile = coast.OrderByDescending(t => Vector2.Distance(t, arrival)).Take(Mathf.Max(1, coast.Count / 5)).OrderBy(_ => rng.Next()).First();
            quarter.distance = far > 0f ? Vector2.Distance(quarter.portalTile, arrival) / far : 1f;
            quarter.option = PickBoss(plan, q, out quarter.legendary);
            if (quarter.option == null) quarter.gear = true; // no boss at all: the way stays open
            return quarter;
        }
        // Fragments: in different sub-biomes when there are enough.
        var byHabitat = land.GroupBy(t => plan.biome[plan.Index(t.x, t.y)].key).OrderBy(_ => rng.Next()).ToList();
        for (int k = 0; k < Data.fragments.count; k++)
        {
            var group = byHabitat[k % byHabitat.Count].Where(t => quarter.fragments.All(f => Vector2.Distance(f.tile, t) >= 10f) && ClearOfCamps(t, 5f)).ToList();
            if (group.Count == 0) group = byHabitat[k % byHabitat.Count].ToList();
            string guard = PickGuard();
            var tile = guard == "nook"
                ? group.OrderByDescending(Height).ThenByDescending(t => Vector2.Distance(t, arrival)).First()
                : group.Where(t => Vector2.Distance(t, arrival) >= 0.4f * far).DefaultIfEmpty(group[rng.Next(group.Count)]).OrderBy(_ => rng.Next()).First();
            float spread = Data.fragments.hint;
            var hint = tile + new Vector2Int(rng.Next(-(int)spread, (int)spread + 1), rng.Next(-(int)spread, (int)spread + 1));
            quarter.fragments.Add(new Fragment { tile = tile, hint = hint, habitat = byHabitat[k % byHabitat.Count].Key, guard = guard });
        }
        quarter.distance = 0.5f;
        return quarter;
    }

    // An open tile with the sky (the island's edge) within two tiles: the coast.
    static bool ByTheSky(IslandPlan plan, Vector2Int t)
    {
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
            {
                int x = t.x + dx, y = t.y + dy;
                if (x < 0 || y < 0 || x >= plan.size || y >= plan.size || plan.region[plan.Index(x, y)] == IslandPlan.Void) return true;
            }
        return false;
    }

    /// The boss of a quarter: rarely the theme's legendary (it Terastallizes at its second phase), otherwise a Pokémon
    /// whose mega has sprites and that lives in one of the quarter's sub-biomes (any of them when none does), or one
    /// of the written options; it mega evolves at its second phase.
    public Option PickBoss(IslandPlan plan, int q, out bool legendary)
    {
        var theme = Array.Find(Data.bosses, b => b.theme == plan.themes[q]?.key);
        legendary = false;
        int legend = theme != null ? theme.legendary.FirstOrDefault(PokemonRoster.Has) : 0;
        if (legend > 0 && rng.NextDouble() < Data.boss.legendaryChance)
        {
            legendary = true;
            return new Option { members = new[] { new Member { pokemon = new[] { legend }, tera = true } } };
        }
        var habitats = new HashSet<string>();
        for (int i = 0; i < plan.region.Length; i++)
            if (plan.region[i] == q && plan.biome[i] != null) habitats.Add(plan.biome[i].key);
        var megas = GameData.AllMegas.Where(m => PokemonRoster.Has(m.@base)).ToList();
        var fitting = megas.Where(m => habitats.Any(h => Populations.Lives(h, m.@base, false))).ToList();
        var pool = (fitting.Count > 0 ? fitting : megas)
            .Select(m => (option: new Option { members = new[] { new Member { pokemon = new[] { m.@base }, mega = true } } }, weight: 1f)).ToList();
        foreach (var o in theme?.options ?? new Option[0])
            if (o.members.All(m => Resolve(m) > 0)) pool.Add((o, o.weight * Data.boss.optionWeight));
        if (pool.Count == 0) return null;
        double roll = rng.NextDouble() * pool.Sum(p => p.weight);
        foreach (var (option, weight) in pool)
            if ((roll -= weight) < 0) return option;
        return pool[^1].option;
    }

    string PickGuard()
    {
        var guards = Data.fragments.guards.Where(g => g.weight > 0f).ToList();
        if (guards.Count == 0) return "nook";
        double roll = rng.NextDouble() * guards.Sum(g => g.weight);
        foreach (var g in guards)
            if ((roll -= g.weight) < 0) return g.key;
        return guards[^1].key;
    }

    // The first id of the member that has sprites (0: none).
    static int Resolve(Member m) => m.pokemon.FirstOrDefault(PokemonRoster.Has);

    // ---- Playing ----

    int Level(Quarter q, int bonus) => Data.baseLevel + Populations.LevelBonus(q.rank, q.distance) + bonus;

    void SpawnZone()
    {
        RefreshHints();
        var q = Here;
        if (q == null || spawner == null) return;
        foreach (var f in q.fragments)
        {
            if (f.taken) continue;
            Vector2 at = island.Cell(f.tile);
            var go = Pickup("Fragment d'Engrenage", at, 0.55f);
            pickups.Add((go, f));
            if (f.guard != "elite" || f.guardBeaten) continue;
            var lot = Encounters.CategoryOf(Data.fragments.eliteLot);
            var e = lot != null ? Encounters.Make(rng, lot, Populations.Stage(q.rank, 0.6f) + 1, -1, where: new Populations.Where(f.habitat, spawner.Night)) : null;
            if (e == null) { f.guardBeaten = true; continue; }
            f.spawned.AddRange(spawner.SpawnEncounter(e, at + Vector2.right * 2f, Level(q, 1)));
            if (f.spawned.Count > 0 && f.spawned[0].GetComponent<EnemyAI>()?.group is { } group)
                group.Rest(new EnemyGroup.Roam { home = at + Vector2.right * 2f, wake = Data.fragments.wake, leash = Data.fragments.leash });
        }
    }

    // ---- The key dungeon and the islet (KeyDungeon, BossPortal) ----

    /// The rooms of a quarter's key dungeon, in order (room_contents.json keys).
    public static string[] DungeonRooms => Data.boss.dungeonRooms;
    public static string BossRoomType => Data.boss.roomType;

    /// The last room of the key dungeon is over: the islet's key.
    public void GiveKey(Quarter q)
    {
        if (q == null || q.hasKey) return;
        q.hasKey = true;
        progression?.Announce("Clé de l'îlot obtenue : le portail de la côte s'ouvre", Notice.Elite);
        team?.GetComponent<TeamHud>()?.ShowCard("Clé de l'îlot", "Le portail au bord de la côte mène au gardien du quartier", SpecialItems.Icon(Data.boss.keyIcon));
    }

    /// The boss of the islet: big, and its second phase ready (its mega, or a Tera shard of the type that takes the
    /// team's moves best). Called by its room (RoomContent).
    public List<Combatant> SpawnBoss(Quarter q, EnemySpawner by, Vector2 at)
    {
        boss.Clear();
        if (q?.option == null || by == null) return new List<Combatant>(boss);
        for (int i = 0; i < q.option.members.Length; i++)
        {
            var m = q.option.members[i];
            int id = Resolve(m);
            var offset = q.option.members.Length > 1 ? new Vector2(i == 0 ? -1.5f : 1.5f, 0f) : Vector2.zero;
            // A boss without hand-made moves takes the strongest of its list.
            int[] moves = GameData.HasLoadout(id) ? null : GameData.RandomMoves(id, Combatant.MoveSlots, rng, null, strong: true);
            var c = by.SpawnFighter(id, moves, at + offset, true, false, Level(q, Data.boss.level), Data.boss.health);
            if (c == null) continue;
            c.transform.localScale = Vector3.one * Data.boss.scale;
            if (c.GetComponent<EnemyUltimate>() is { } ultimate)
            {
                if (m.mega && GameData.MegasOf(id).FirstOrDefault() is { } mega) ultimate.Carry(SpecialItems.Family.Mega, -1, mega);
                else if (m.tera && ultimate.BestTeraType(out _) is int type and > 0) ultimate.Carry(SpecialItems.Family.Tera, type);
            }
            if (c.GetComponent<HealthBar>() == null) c.gameObject.AddComponent<HealthBar>();
            boss.Add(c);
        }
        progression?.Announce($"Le gardien du quartier : {string.Join(" et ", boss.Select(c => c.Stats.name))} !", Notice.Elite);
        return new List<Combatant>(boss);
    }

    /// The islet's boss fell: the gear, and a chest.
    public void BossBeaten(Quarter q, Vector2 at)
    {
        if (q == null || q.bossBeaten) return;
        q.bossBeaten = true;
        boss.Clear();
        FindAnyObjectByType<Loot>()?.SpawnChest(at + Vector2.right * 1.5f, anywhere: true, Data.boss.stoneChance, Data.boss.chestIcon);
        Obtain(q);
    }

    static GameObject Pickup(string name, Vector2 at, float size)
    {
        var go = new GameObject(name);
        go.transform.position = at;
        go.transform.localScale = Vector3.one * size;
        var r = go.AddComponent<SpriteRenderer>();
        r.sprite = Shapes.Gear;
        r.sortingOrder = 2;
        return go;
    }

    void Despawn()
    {
        foreach (var c in boss)
            if (c != null) Destroy(c.gameObject);
        boss.Clear();
        foreach (var (go, f) in pickups)
        {
            if (go != null) Destroy(go);
            foreach (var c in f.spawned)
                if (c != null) Destroy(c.gameObject);
            f.spawned.Clear();
        }
        pickups.Clear();
    }

    void FixedUpdate()
    {
        var q = Here;
        if (island.Away && pickups.Count > 0) Despawn(); // into a room: back when the zone is built again
        if (q == null || island.Away || LoadingScreen.Busy) return;
        var leader = team != null ? team.Controlled : null;
        foreach (var (go, f) in pickups)
            if (!f.guardBeaten && f.spawned.Count > 0 && f.spawned.All(c => c == null || !c.IsAlive)) f.guardBeaten = true;
        if (leader == null) return;
        Vector2 at = leader.transform.position;
        for (int i = pickups.Count - 1; i >= 0; i--)
        {
            var (go, f) = pickups[i];
            if (go == null || Vector2.Distance(at, go.transform.position) > PickReach) continue;
            Destroy(go);
            pickups.RemoveAt(i);
            f.taken = true;
            int taken = q.fragments.Count(x => x.taken);
            if (taken >= q.fragments.Count) Obtain(q);
            else progression?.Announce($"Fragment d'Engrenage {taken} / {q.fragments.Count}", Notice.Loot);
            RefreshHints();
        }
    }

    void Obtain(Quarter q)
    {
        q.gear = true;
        progression?.Announce($"Engrenage du Temps obtenu ({Found} / {Total}) : le passage suivant s'ouvre", Notice.Elite);
        team?.GetComponent<TeamHud>()?.ShowCard("Engrenage du Temps", Found >= Total ? "Le centre de l'île s'ouvre" : "Le passage vers le quartier suivant s'ouvre", Shapes.Gear);
    }

    void RefreshHints()
    {
        island.hints.Clear();
        var q = Here;
        if (q == null) return;
        foreach (var f in q.fragments)
            if (!f.taken) island.hints.Add((f.hint, FragmentHint));
    }

    // Dev: G in the test quarter switches its way of giving the gear (boss, fragments).
    void Update()
    {
        if (!testPlan || !SaveData.Settings.devMode || madeFor == null || UnityEngine.InputSystem.Keyboard.current == null) return;
        if (!UnityEngine.InputSystem.Keyboard.current.gKey.wasPressedThisFrame) return;
        ForceTestMode(Data.testMode == Boss ? Fragments : Boss);
        progression?.Announce($"Quart de test : engrenage par {(Data.testMode == Boss ? "un boss" : "des fragments")}");
    }

    /// The test quarter's way of giving its gear, laid out again (tests, G).
    public void ForceTestMode(string mode)
    {
        if (!testPlan || madeFor == null) return;
        Data.testMode = mode;
        Despawn();
        quarters[0] = MakeQuarter(madeFor, 0, 0, mode, GetComponent<IslandPopulation>());
        SpawnZone();
        GetComponent<KeyDungeon>()?.Refresh();
        GetComponent<BossPortal>()?.Refresh();
    }

    GUIStyle label;

    void OnGUI()
    {
        if (madeFor == null || island.Away || Total == 0) return;
        float s = Scale;
        var box = new Rect(Side * s, (Top + 31f) * s, StageWidth * s, 14f * s);
        Panel(box, s);
        var q = Here;
        string line = $"Engrenages du Temps : {Found} / {Total}";
        if (q != null && !q.gear)
            line += q.mode == Fragments ? $" · fragments {q.fragments.Count(f => f.taken)} / {q.fragments.Count}"
                : q.hasKey ? " · îlot du gardien" : " · clé dans le donjon";
        label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        label.fontSize = Mathf.RoundToInt(7f * s);
        GUI.Label(new Rect(box.x + 6f * s, box.y, box.width - 12f * s, box.height), line, label);
    }
}
