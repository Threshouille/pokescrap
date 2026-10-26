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
        public int bridgeLength = 9; // tiles of rope bridge from the coast toward the islet
        public float dungeonShare = 0.45f, portalShare = 0.5f, legendaryChance = 0.03f, scale = 1.6f;
        public float health = 22f, stoneChance = 0.3f, damage = 4f, speed = 0.9f, recover = 0.6f, hitCap = 0.35f, areaWindup = 0.3f;
        public int level = 3;
        public string[] dungeonRooms = { "ambush", "guardian" };
    }

    [Serializable] class ThemeBosses { public string theme; public int[] roster = new int[0], legendary = new int[0]; }
    [Serializable] class TypeTheme { public string[] types = new string[0]; public string theme; }
    [Serializable] class Minions { public string[] types = new string[0]; public int count; }
    [Serializable] class Arena { public string[] types = new string[0]; public string theme, variant; public Minions minions = new(); }
    [Serializable] class Moveset { public int pokemon; public string name; public int[] moves = new int[0]; }
    [Serializable] class GuardianSettings { public float health = 10f, damage = 1.5f; public int level = 4, minions = 2; }

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
        public TypeTheme[] dungeonThemes = new TypeTheme[0];
        public GuardianSettings guardian = new();
        public Arena[] arenas = new Arena[0];
        public Moveset[] movesets = new Moveset[0];
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
        public bool gateOpened; // its portal's opening was seen: open from then on
        public Vector2Int dungeonTile = new(-1, -1), portalTile = new(-1, -1); // the key dungeon's entrance, the islet's portal
        /// The key dungeon is the mountain's cave: walking under its roof leads in (no rift), back out at caveExit.
        public HashSet<int> cave;
        public Vector2Int caveExit = new(-1, -1);
        /// The rope bridge to the islet: its tiles (2 wide, from the coast), its direction, the gate (first row), where
        /// it takes the team to the arena (near its end), the islet seen beyond it. None: a portal on the coast.
        public readonly List<Vector2Int> bridge = new();
        public Vector2Int bridgeDir, gateTile = new(-1, -1), triggerTile = new(-1, -1);
        public Vector2 isletAt;
        public bool HasBridge => bridge.Count > 0;
        public int boss; // the boss's Pokémon id (0: none)
        public bool legendary; // it Terastallizes instead of mega evolving
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
        if (island != null) island.Built += OnBuilt; // none in the test arena's boss room (SandboxBoss)
    }

    /// Makes the gears' places of a plan before its first zone is built (its sky bridges must be in the plan).
    public void Prepare(IslandPlan plan)
    {
        if (plan != madeFor) Make(plan);
    }

    /// Follows that island (added before it, by the test arena's boss room).
    public void Bind(IslandRun run)
    {
        if (island != null || run == null) return;
        island = run;
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
            var walk = OnFoot(plan, q, arrival);
            // The key dungeon on the quarter's landmark: a cave's mouth (the mountain's), else the highest plateau,
            // else a forest's clearing, else somewhere far.
            var caves = plan.caves.Where(c => plan.region[plan.Index(c.x, c.y)] == q && c != plan.sealedCave).ToList();
            int top = land.Max(Height);
            var heights = top > 0 ? land.Where(t => Height(t) == top).ToList() : new List<Vector2Int>();
            var glades = plan.clearings.Select(c => Vector2Int.RoundToInt(c)).Where(c => plan.region[plan.Index(c.x, c.y)] == q).ToList();
            var deep = land.Where(t => Vector2.Distance(t, arrival) >= Data.boss.dungeonShare * far && ClearOfCamps(t, 6f)).ToList();
            // Each landmark on the free tile nearest to it, far from the other dungeons (secret stairs, sealed cave).
            var spots = caves.Select(c => FreeNear(plan, q, c)).Concat(heights.Count > 0 ? new[] { FreeNear(plan, q, Vector2Int.RoundToInt(Centroid(heights))) } : new Vector2Int[0])
                .Concat(glades.Select(c => FreeNear(plan, q, c))).Where(t => t.x >= 0 && walk[plan.Index(t.x, t.y)]).ToList();
            var fallback = (deep.Count > 0 ? deep : land).Where(t => Free(plan, q, t) && walk[plan.Index(t.x, t.y)]).ToList();
            quarter.dungeonTile = spots.Count > 0 ? spots[0] : fallback.Count > 0 ? fallback[rng.Next(fallback.Count)] : land[rng.Next(land.Count)];
            // The mountain's cave itself: the dungeon is wherever its roof is.
            if (caves.Count > 0) CaveEntrance(plan, quarter, caves[0], walk, arrival);
            // The islet's bridge: from the coast, far from the arrival and the dungeon, straight out over the sky.
            var coast = land.Where(t => ByTheSky(plan, t) && Vector2.Distance(t, arrival) >= Data.boss.portalShare * far
                                        && Vector2.Distance(t, quarter.dungeonTile) >= 15f).OrderByDescending(t => Vector2.Distance(t, arrival)).ToList();
            // Only on the round coast (far from the island's middle, not the quarter's straight sides), straight out;
            // upright first: the portal is an arch seen from the front.
            float reach = land.Max(t => Vector2.Distance(t, plan.hub));
            var round = coast.Where(t => Free(plan, q, t) && Vector2.Distance(t, plan.hub) >= RoundCoast * reach).ToList();
            // Upright and well out first, then upright a little aside, sideways only when nothing else fits.
            foreach (var (upright, outward) in new[] { (true, Outward), (true, 0.15f), (false, Outward) })
            {
                foreach (var t in round)
                    if (LayBridge(plan, q, t, quarter, upright, outward, walk)) break;
                if (quarter.HasBridge) break;
            }
            if (!quarter.HasBridge)
            {
                if (coast.Count == 0) coast = land.Where(t => Vector2.Distance(t, quarter.dungeonTile) >= 15f).ToList();
                if (coast.Count == 0) coast = land;
                quarter.portalTile = coast[rng.Next(Mathf.Max(1, coast.Count / 5))];
            }
            plan.reserved.Add(quarter.dungeonTile);
            plan.reserved.Add(quarter.portalTile);
            quarter.distance = far > 0f ? Vector2.Distance(quarter.portalTile, arrival) / far : 1f;
            quarter.boss = PickBoss(plan.themes[q]?.key, out quarter.legendary);
            if (quarter.boss <= 0) quarter.gear = true; // no boss at all: the way stays open
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

    const float DungeonGap = 6f; // tiles between the key dungeon and another dungeon's entrance

    // A tile to stand on: in the quarter, no wall, no obstacle (a blocking decor element is one), no decor element
    // drawn on it, and away from the other dungeons and the passages.
    static bool Free(IslandPlan plan, int q, Vector2Int t)
    {
        if (t.x < 0 || t.y < 0 || t.x >= plan.size || t.y >= plan.size) return false;
        int i = plan.Index(t.x, t.y);
        if (plan.region[i] != q || plan.wall[i] || plan.obstacle[i] != null) return false;
        if (plan.wallLook != null && plan.wallLook[i]) return false;
        Vector2 centre = t + new Vector2(0.5f, 0.5f);
        if (plan.decorProps.Any(p => Vector2.Distance(p.at, centre) < 1.2f)) return false;
        var others = new List<Vector2Int>(plan.markers);
        if (plan.secret.x >= 0) others.Add(plan.secret);
        if (plan.sealedCave.x >= 0) others.Add(plan.sealedCave);
        foreach (var p in plan.passages) { others.Add(p.a); others.Add(p.b); }
        return others.All(o => Vector2.Distance(o, t) >= DungeonGap);
    }

    // The free tile nearest to a landmark, on its own level when it can (-1, -1: none within reach).
    static Vector2Int FreeNear(IslandPlan plan, int q, Vector2Int at)
    {
        int level = plan.height != null && at.x >= 0 && at.y >= 0 && at.x < plan.size && at.y < plan.size ? plan.height[plan.Index(at.x, at.y)] : 0;
        Vector2Int best = new(-1, -1);
        for (int r = 0; r <= 6; r++)
        {
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    var t = at + new Vector2Int(dx, dy);
                    if (!Free(plan, q, t)) continue;
                    if (plan.height == null || plan.height[plan.Index(t.x, t.y)] == level) return t;
                    if (best.x < 0) best = t;
                }
            if (best.x >= 0 && r >= 2) return best;
        }
        return best;
    }

    static Vector2 Centroid(List<Vector2Int> tiles)
    {
        var sum = Vector2.zero;
        foreach (var t in tiles) sum += t;
        return tiles.Count > 0 ? sum / tiles.Count : Vector2.zero;
    }

    // A rope bridge from that coast tile straight out over the sky, two tiles wide, in the first of the four
    // directions where the whole length is sky: true when laid (its tiles join the plan's sky bridges).
    const int FootDepth = 4; // tiles of open ground before the bridge
    const float RoundCoast = 0.8f, Outward = 0.5f; // share of the farthest land; cosine between the bridge and the way out

    bool LayBridge(IslandPlan plan, int q, Vector2Int coast, Quarter quarter, bool upright, float least, bool[] walk)
    {
        int length = Data.boss.bridgeLength;
        var outward = ((Vector2)coast - plan.hub).normalized;
        var dirs = upright ? new[] { Vector2Int.up, Vector2Int.down } : new[] { Vector2Int.left, Vector2Int.right };
        foreach (var dir in dirs.Where(d => Vector2.Dot(d, outward) >= least).OrderBy(_ => rng.Next()))
        {
            var side = new Vector2Int(Mathf.Abs(dir.y), Mathf.Abs(dir.x)); // the second tile of width
            var cells = new List<Vector2Int>();
            bool sky = true;
            for (int k = 1; k <= length && sky; k++)
                for (int w = 0; w < 2 && sky; w++)
                {
                    var c = coast + dir * k + side * w;
                    if (c.x < 1 || c.y < 1 || c.x >= plan.size - 1 || c.y >= plan.size - 1 || plan.region[plan.Index(c.x, c.y)] != IslandPlan.Void) sky = false;
                    else cells.Add(c);
                }
            // Its first row must touch the coast on both tiles (no gap to step over).
            // Its foot: both columns, FootDepth tiles inland, open ground that anyone reaches on foot (no water at its end).
            var beside = coast + side;
            if (!sky || plan.region[plan.Index(beside.x, beside.y)] != q) continue;
            bool foot = true;
            for (int k = 0; k < FootDepth && foot; k++)
                for (int w = 0; w < 2 && foot; w++)
                {
                    var c = coast - dir * k + side * w;
                    foot = c.x >= 0 && c.y >= 0 && c.x < plan.size && c.y < plan.size && walk[plan.Index(c.x, c.y)];
                }
            if (!foot) continue;
            quarter.bridge.AddRange(cells);
            quarter.bridgeDir = dir;
            quarter.portalTile = coast;
            quarter.gateTile = coast + dir;
            quarter.triggerTile = coast + dir * (length - 1);
            quarter.isletAt = (Vector2)(coast + dir * (length + 3)) + (Vector2)side * 0.5f;
            foreach (var c in cells) plan.skyBridge[plan.Index(c.x, c.y)] = q;
            return true;
        }
        return false;
    }

    // The roofed tiles of the cave around "inside" (its whole hollow) and the open tile in front of its mouth nearest to
    // the arrival, reached on foot; nothing when the cave has no roof or no way in.
    static void CaveEntrance(IslandPlan plan, Quarter quarter, Vector2Int inside, bool[] walk, Vector2Int arrival)
    {
        if (plan.roof == null || !plan.roof[plan.Index(inside.x, inside.y)]) return;
        var cave = new HashSet<int> { plan.Index(inside.x, inside.y) };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(inside);
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        var exit = new Vector2Int(-1, -1);
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            foreach (var d in steps)
            {
                var n = t + d;
                if (n.x < 0 || n.y < 0 || n.x >= plan.size || n.y >= plan.size) continue;
                int i = plan.Index(n.x, n.y);
                if (plan.roof[i])
                {
                    if (cave.Add(i)) queue.Enqueue(n);
                }
                else if (walk[i] && (exit.x < 0 || Vector2.Distance(n, arrival) < Vector2.Distance(exit, arrival))) exit = n;
            }
        }
        if (exit.x < 0) return;
        quarter.cave = cave;
        quarter.caveExit = exit;
        quarter.dungeonTile = inside;
    }

    // The quarter's tiles any Pokémon reaches on foot from its arrival: no wall, no obstacle in the way of anyone (water,
    // lava, the void, quicksand, a ledge; a bridge over a river is fine).
    static bool[] OnFoot(IslandPlan plan, int q, Vector2Int arrival)
    {
        var seen = new bool[plan.size * plan.size];
        bool Open(int x, int y)
        {
            if (x < 0 || y < 0 || x >= plan.size || y >= plan.size) return false;
            int i = plan.Index(x, y);
            if (plan.region[i] != q || plan.wall[i] || (plan.wallLook != null && plan.wallLook[i])) return false;
            var key = plan.obstacle[i];
            if (key == null) return true;
            var def = Obstacles.Def(key);
            return def != null && !def.solid && !def.fall && def.sinkTime <= 0f && !def.OneWay;
        }
        var start = Open(arrival.x, arrival.y) ? arrival : FirstOpen();
        Vector2Int FirstOpen()
        {
            for (int r = 1; r < 12; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dy = -r; dy <= r; dy++)
                        if (Open(arrival.x + dx, arrival.y + dy)) return arrival + new Vector2Int(dx, dy);
            return arrival;
        }
        if (!Open(start.x, start.y)) return seen;
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        seen[plan.Index(start.x, start.y)] = true;
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0)
        {
            var t = queue.Dequeue();
            foreach (var d in steps)
            {
                var n = t + d;
                if (!Open(n.x, n.y) || seen[plan.Index(n.x, n.y)]) continue;
                seen[plan.Index(n.x, n.y)] = true;
                queue.Enqueue(n);
            }
        }
        return seen;
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

    /// The boss of a quarter of that theme: rarely its legendary (it Terastallizes at its second phase), otherwise one
    /// of its roster whose mega has sprites (it mega evolves). 0 when the theme has none.
    public int PickBoss(string theme, out bool legendary)
    {
        var bosses = Array.Find(Data.bosses, b => b.theme == theme) ?? Array.Find(Data.bosses, b => b.theme == "forest");
        legendary = false;
        if (bosses == null) return 0;
        int legend = bosses.legendary.FirstOrDefault(PokemonRoster.Has);
        var roster = bosses.roster.Where(id => PokemonRoster.Has(id) && GameData.MegasOf(id).Count > 0).ToList();
        if (legend > 0 && (roster.Count == 0 || rng.NextDouble() < Data.boss.legendaryChance))
        {
            legendary = true;
            return legend;
        }
        return roster.Count > 0 ? roster[rng.Next(roster.Count)] : 0;
    }

    /// The boss's types (ROM ids), in its own order.
    public static int[] BossTypes(Quarter q) => q != null && GameData.Pokemon(q.boss) is { } p ? p.types : new int[0];

    static int TypeId(string name)
    {
        for (int t = 0; t < 32; t++)
            if (GameData.TypeName(t) == name) return t;
        return -1;
    }

    // The first entry holding one of the boss's types, taken in the boss's own order (Altaria: Dragon, then Vol).
    static T ByType<T>(T[] entries, Func<T, string[]> types, Quarter q) where T : class
    {
        foreach (int t in BossTypes(q))
            foreach (var e in entries)
                if (types(e).Any(n => TypeId(n) == t)) return e;
        return entries.FirstOrDefault(e => types(e).Length == 0);
    }

    /// The key dungeon's room theme (rooms.json), from the boss's types.
    public static string DungeonTheme(Quarter q) => ByType(Data.dungeonThemes, d => d.types, q)?.theme;

    // The islet's arena, from the boss's types.
    static Arena ArenaOf(Quarter q) => ByType(Data.arenas, a => a.types, q);

    /// The islet's room theme and variant (rooms.json).
    public static (string theme, string variant) ArenaRoom(Quarter q) => ArenaOf(q) is { } a ? (a.theme, a.variant) : (null, null);

    string PickGuard()
    {
        var guards = Data.fragments.guards.Where(g => g.weight > 0f).ToList();
        if (guards.Count == 0) return "nook";
        double roll = rng.NextDouble() * guards.Sum(g => g.weight);
        foreach (var g in guards)
            if ((roll -= g.weight) < 0) return g.key;
        return guards[^1].key;
    }


    // ---- Playing ----

    // The team's level plus a bonus, never under the quarter's (a boss follows the team, neither crushes it nor falls
    // to a team that outgrew the island).
    int Level(Quarter q, int bonus) => Mathf.Max(Data.baseLevel + Populations.LevelBonus(q.rank, q.distance), (progression != null ? progression.Level : 1) + bonus);

    void SpawnZone()
    {
        RefreshHints();
        var q = Here;
        if (q == null || spawner == null) return;
        if (q.mode != Fragments && q.HasBridge) DrawBridge(q);
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

    // ---- The test arena's boss room (TerrainSandbox) ----

    /// The bosses of a zone's roster, then its legendary: the test arena shows them one after the other.
    public static List<(int id, bool legendary)> Roster(string theme)
    {
        var bosses = Array.Find(Data.bosses, b => b.theme == theme);
        var list = new List<(int, bool)>();
        if (bosses == null) return list;
        foreach (int id in bosses.roster)
            if (PokemonRoster.Has(id) && GameData.MegasOf(id).Count > 0) list.Add((id, false));
        int legend = bosses.legendary.FirstOrDefault(PokemonRoster.Has);
        if (legend > 0) list.Add((legend, true));
        return list;
    }

    // As in the first quarter, near its arrival: the boss follows the team's level only.
    static Quarter SandboxQuarter(int id, bool legendary) => new() { mode = Boss, boss = id, legendary = legendary, rank = 0, distance = 0f };

    /// The arena an islet would have for that boss.
    public static RoomInstance ArenaFor(int id, bool legendary, System.Random random)
    {
        var (theme, variant) = ArenaRoom(SandboxQuarter(id, legendary));
        return (theme != null ? RoomCatalog.Pick(Data.boss.roomType, random, theme, variant) : null) ?? RoomCatalog.Pick(Data.boss.roomType, random);
    }

    /// That boss as on its islet (with its minions), at the team's level: the test arena's boss room.
    public List<Combatant> SandboxBoss(int id, bool legendary, EnemySpawner by, Vector2 at)
    {
        if (progression == null) progression = FindAnyObjectByType<Progression>();
        return SpawnBoss(SandboxQuarter(id, legendary), by, at);
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
        SetGate(q);
        progression?.Announce("Clé de l'îlot obtenue : le portail du pont s'ouvre", Notice.Elite);
        team?.GetComponent<TeamHud>()?.ShowCard("Clé de l'îlot", "Le portail au bord de la côte mène au boss du quartier", SpecialItems.Icon(Data.boss.keyIcon));
    }

    // A fighter of the island's rooms, at the level of the quarter: the strongest moves of its list unless it has
    // hand-made ones.
    Combatant Fighter(EnemySpawner by, int id, Vector2 at, bool isBoss, int level, float health)
    {
        at = OpenNear(at); // never inside a wall, a pillar or the void
        int[] moves = isBoss ? BossMoves(id)
            : GameData.HasLoadout(id) ? null : GameData.RandomMoves(id, Combatant.MoveSlots, rng, null, strong: true);
        return by.SpawnFighter(id, moves, at, isBoss, false, level, health);
    }

    // The free tile nearest to a point of the room being played.
    static Vector2 OpenNear(Vector2 point)
    {
        var arena = DungeonGenerator.Current;
        if (arena == null) return point;
        var c = arena.CellOf(point);
        for (int r = 0; r < 10; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = c.x + dx, y = c.y + dy;
                    if (arena.Walkable(x, y) && Obstacles.Standable(x, y)) return arena.CentreOf(x, y);
                }
        return point;
    }

    /// A boss's four moves, always the same (its numbered announcements can be learnt): its moveset of the data, else
    /// its hand-made loadout (null: Combatant.Init takes it), else its basic move and the three strongest damaging
    /// moves of its list (no status move).
    public static int[] BossMoves(int id)
    {
        if (Array.Find(Data.movesets, m => m.pokemon == id) is { } set && set.moves.Length > 0) return set.moves;
        if (GameData.HasLoadout(id)) return null;
        var moves = new List<int>();
        var basic = GameData.BasicMoves(id, GameData.StatStyle(id)).FirstOrDefault();
        if (basic != null) moves.Add(basic.id);
        foreach (var m in GameData.MovePool(id).Where(m => !m.basic && m.power > 0).OrderByDescending(m => m.power).ThenBy(m => m.id))
        {
            if (moves.Count >= Combatant.MoveSlots) break;
            if (!moves.Contains(m.id)) moves.Add(m.id);
        }
        return moves.ToArray();
    }

    // Pokémon of those types (ROM ids) at that evolution stage at most, a few of each.
    static List<int> OfTypes(int[] types, int stage) =>
        types.Length == 0 ? new List<int>() : Encounters.Pool(stage, -1, new Populations.Where(types)).Distinct().ToList();

    /// The boss of the islet: big, and its second phase ready (its mega, or for the legendary a Tera shard of the type
    /// that takes the team's moves best); with the arena's minions (elites: they change the décor against the team).
    /// Called by its room (RoomContent).
    public List<Combatant> SpawnBoss(Quarter q, EnemySpawner by, Vector2 at)
    {
        boss.Clear();
        var all = new List<Combatant>();
        if (q == null || q.boss <= 0 || by == null) return all;
        var c = Fighter(by, q.boss, at, true, Level(q, Data.boss.level), Data.boss.health);
        if (c == null) return all;
        c.transform.localScale = Vector3.one * Data.boss.scale;
        c.damageFactor *= Data.boss.damage;
        c.hitCap = Data.boss.hitCap; // never a whole bar in one blow
        if (c.GetComponent<BossAI>() is { } ai)
        {
            ai.seeksReach = true;
            ai.speedFactor = Data.boss.speed;
            ai.recoverTime = Data.boss.recover;
            ai.areaWindup = Data.boss.areaWindup;
        }
        if (c.GetComponent<EnemyUltimate>() is { } ultimate)
        {
            if (!q.legendary && GameData.MegasOf(q.boss).FirstOrDefault() is { } mega) ultimate.Carry(SpecialItems.Family.Mega, -1, mega);
            else if (ultimate.BestTeraType(out _) is int type and > 0) ultimate.Carry(SpecialItems.Family.Tera, type);
        }
        boss.Add(c);
        all.Add(c);
        var arena = ArenaOf(q);
        var kinds = arena?.minions.types.Select(TypeId).Where(t => t > 0).ToArray() ?? new int[0];
        var pool = OfTypes(kinds, Populations.Stage(q.rank, q.distance) + 1);
        for (int k = 0; k < (arena?.minions.count ?? 0) && pool.Count > 0; k++)
        {
            var m = Fighter(by, pool[rng.Next(pool.Count)], at + new Vector2(k == 0 ? -3f : 3f, -2f), false, Level(q, 0), 1.5f);
            if (m == null) continue;
            m.gameObject.AddComponent<EliteMark>(); // burns the bridges, freezes the water... like the elites
            all.Add(m);
        }
        progression?.Announce($"Le boss du quartier : {c.Stats.name} !", Notice.Elite);
        return all;
    }

    /// The key dungeon's guardian: an evolved Pokémon of the boss's types (the most evolved there is), an elite that
    /// Terastallizes at its second phase, asleep by the chest with two minions, each with the Z crystal of its
    /// strongest move. Called by the dungeon's last room (RoomContent).
    public List<Combatant> SpawnGuardian(Quarter q, EnemySpawner by, Vector2 at)
    {
        var all = new List<Combatant>();
        if (q == null || by == null) return all;
        // Recruitable or not (most final forms are not, in the ROM), legendaries and the boss itself left out.
        var types = BossTypes(q);
        var pool = GameData.AllPokemon.Where(p => p.id != q.boss && p.types.Intersect(types).Any() && !GameData.IsDev(p.id) && !Encounters.Excluded(p.id)
                                                  && PokemonRoster.Has(p.id) && GameData.MovePool(p.id).Count >= 2).Select(p => p.id).ToList();
        if (pool.Count == 0) pool = OfTypes(types, 9);
        if (pool.Count == 0) return all;
        int deepest = pool.Max(id => Encounters.Depth(GameData.Pokemon(id)));
        var evolved = pool.Where(id => Encounters.Depth(GameData.Pokemon(id)) == deepest).ToList();
        var guardian = Fighter(by, evolved[rng.Next(evolved.Count)], at, false, Level(q, Data.guardian.level), Data.guardian.health);
        if (guardian == null) return all;
        Loot.Mark(guardian);
        guardian.damageFactor *= Data.guardian.damage;
        var tera = guardian.gameObject.AddComponent<EnemyUltimate>();
        tera.cutScene = true; // its Tera on screen too, shorter than a boss's
        guardian.Died += BossDefeat.PlayGuardian; // and its fall
        if (tera.BestTeraType(out _) is int type and > 0) tera.Carry(SpecialItems.Family.Tera, type);
        all.Add(guardian);
        var minions = OfTypes(BossTypes(q), Populations.Stage(q.rank, q.distance)).Where(id => !evolved.Contains(id)).ToList();
        if (minions.Count == 0) minions = pool;
        for (int k = 0; k < Data.guardian.minions; k++)
        {
            var m = Fighter(by, minions[rng.Next(minions.Count)], at + new Vector2(k == 0 ? -2f : 2f, -1.5f), false, Level(q, 0), 1.5f);
            if (m == null) continue;
            var z = m.gameObject.AddComponent<EnemyUltimate>();
            var strongest = m.Moves.Where(x => x != null && x.power > 0).OrderByDescending(x => x.power).FirstOrDefault();
            if (strongest != null) z.Carry(SpecialItems.Family.Z, strongest.type);
            all.Add(m);
        }
        // One group: they rest together by the chest and wake together.
        var group = new EnemyGroup();
        foreach (var f in all)
        {
            group.members.Add(f);
            if (f.GetComponent<EnemyAI>() is { } ai) ai.group = group;
        }
        group.Rest(new EnemyGroup.Roam { home = at, wake = RoomContent.GuardianWake, leash = RoomContent.GuardianLeash });
        return all;
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
        r.sprite = RomArt.Get("GroundObjects/time_gear") ?? Shapes.Gear; // the story's Time Gear
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
        if (bridgeRoot != null) Destroy(bridgeRoot.gameObject);
        bridgeRoot = null;
        gate = null;
    }

    // ---- The islet's bridge (story art: the beach pier's planks, a light portal, Temporal Tower's floating rocks) ----

    const int BridgeOrder = -4, IsletOrder = -5; // over the sky, under the Pokémon
    Transform bridgeRoot;
    GameObject gate;
    SpriteRenderer gateLight;
    float gateWarnedAt = -10f;
    // The portal's frames (story art, light_portal_frames): the arch flickering (shut), its opening, the open swirl.
    const int GateFrames = 12, GateLoop = 4;
    const float GateFrameTime = 4f / 60f, GateOpenReach = 5f; // tiles: the key's holder this close opens it
    static readonly Color GateShut = new(0.45f, 0.5f, 0.6f, 0.85f);
    Sprite[] gateFrames;
    int gatePhase; // 0 shut, 1 opening, 2 open
    float gatePhaseAt;

    static float Angle(Vector2Int dir) => dir == Vector2Int.up ? 0f : dir == Vector2Int.down ? 180f : dir == Vector2Int.left ? 90f : -90f;

    // The rope bridge over its tiles, the portal at its foot (shut without the key), the islet's rocks beyond.
    void DrawBridge(Quarter q)
    {
        bridgeRoot = new GameObject("Îlot du boss").transform;
        var side = new Vector2(Mathf.Abs(q.bridgeDir.y), Mathf.Abs(q.bridgeDir.x)) * 0.5f; // between its two tiles
        var mid = RomArt.Get("Bridges/rope_bridge_mid");
        var end = RomArt.Get("Bridges/rope_bridge_end");
        int length = q.bridge.Count / 2;
        for (int k = 1; k <= length; k++)
        {
            var piece = new GameObject($"Pont {k}").AddComponent<SpriteRenderer>();
            piece.transform.SetParent(bridgeRoot, false);
            piece.transform.position = island.Cell(q.portalTile + q.bridgeDir * k) + side;
            // The far end keeps its posts, facing the islet.
            piece.sprite = k == length && end != null ? end : mid;
            piece.transform.rotation = Quaternion.Euler(0f, 0f, Angle(q.bridgeDir) + (k == length ? 180f : 0f));
            piece.sortingOrder = BridgeOrder;
        }
        // The islet: a few floating rocks out in the sky, seen, not reached.
        var rocks = new[] { ("GroundObjects/floating_rock", Vector2.zero, 1.6f), ("GroundObjects/floating_rock_b", new Vector2(-2.6f, -1.2f), 0.9f),
                            ("GroundObjects/floating_rock_c", new Vector2(2.4f, 1.4f), 0.8f) };
        Vector2 islet = island.Cell(Vector2Int.RoundToInt(q.isletAt)) + (q.isletAt - (Vector2)Vector2Int.RoundToInt(q.isletAt));
        foreach (var (path, offset, size) in rocks)
        {
            var sprite = RomArt.Get(path);
            if (sprite == null) continue;
            var rock = new GameObject("Rocher flottant").AddComponent<SpriteRenderer>();
            rock.transform.SetParent(bridgeRoot, false);
            rock.transform.position = islet + offset;
            rock.transform.localScale = Vector3.one * size;
            rock.sprite = sprite;
            rock.sortingOrder = IsletOrder;
        }
        // The portal at its foot: grey and shut until the team has the key.
        gate = new GameObject("Portail de l'îlot");
        gate.transform.SetParent(bridgeRoot, false);
        gate.transform.position = island.Cell(q.gateTile) + side;
        gateLight = gate.AddComponent<SpriteRenderer>();
        gateFrames = RomArt.Frames("GroundObjects/light_portal_frames", GateFrames);
        gateLight.sprite = gateFrames != null ? gateFrames[0] : RomArt.Get("GroundObjects/light_portal", new Vector2(0.5f, 0.1f));
        gateLight.sortingOrder = 1; // stands like a Pokémon (sorted by its foot)
        gateLight.spriteSortPoint = SpriteSortPoint.Pivot;
        var wall = gate.AddComponent<BoxCollider2D>();
        wall.size = q.bridgeDir.x == 0 ? new Vector2(2f, 0.5f) : new Vector2(0.5f, 2f);
        SetGate(q);
    }

    void SetGate(Quarter q)
    {
        if (gate == null) return;
        gate.GetComponent<BoxCollider2D>().enabled = !q.hasKey;
        if (gateLight != null) gateLight.color = q.hasKey ? Color.white : GateShut;
        gatePhase = q.gateOpened ? 2 : 0;
        gatePhaseAt = Time.time;
    }

    // The portal plays its frames: the arch flickers, dull without the key; the first time the key's holder comes
    // near, it opens (a flash, a light shake), then its swirl turns.
    void AnimateGate()
    {
        var q = Here;
        if (gate == null || gateLight == null || gateFrames == null || q == null) return;
        var leader = team != null ? team.Controlled : null;
        if (gatePhase == 0 && q.hasKey && !island.Away && leader != null && Vector2.Distance(leader.transform.position, gate.transform.position) < GateOpenReach)
        {
            gatePhase = 1;
            gatePhaseAt = Time.time;
            CameraFollow.Shake(0.1f, 0.25f);
            EffectPlayer.Spawn("evolution_eclat", (Vector2)gate.transform.position + Vector2.up, Vector2.right);
        }
        int frame = (int)((Time.time - gatePhaseAt) / GateFrameTime);
        if (gatePhase == 1 && frame >= GateLoop)
        {
            gatePhase = 2;
            q.gateOpened = true;
            gatePhaseAt = Time.time;
            frame = 0;
        }
        int first = gatePhase * GateLoop;
        gateLight.sprite = gateFrames[first + (gatePhase == 1 ? frame : frame % GateLoop)];
    }

    void FixedUpdate()
    {
        var q = Here;
        if (island == null) return;
        if (island.Away && pickups.Count > 0) Despawn(); // into a room: back when the zone is built again
        if (q == null || island.Away || LoadingScreen.Busy) return;
        var leader = team != null ? team.Controlled : null;
        foreach (var (go, f) in pickups)
            if (!f.guardBeaten && f.spawned.Count > 0 && f.spawned.All(c => c == null || !c.IsAlive)) f.guardBeaten = true;
        if (leader == null) return;
        Vector2 at = leader.transform.position;
        if (gate != null && !q.hasKey && Vector2.Distance(at, gate.transform.position) < 1.6f && Time.time - gateWarnedAt > 2.5f)
        {
            gateWarnedAt = Time.time;
            progression?.Announce("Le portail est scellé : il faut la clé de l'îlot, au fond du donjon de la clé", Notice.Alert);
        }
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
        team?.GetComponent<TeamHud>()?.ShowCard("Engrenage du Temps", Found >= Total ? "Le centre de l'île s'ouvre" : "Le passage vers le quartier suivant s'ouvre", RomArt.Get("GroundObjects/time_gear") ?? Shapes.Gear);
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
        AnimateGate();
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
        foreach (var k in madeFor.skyBridge.Where(e => e.Value == 0).Select(e => e.Key).ToList()) madeFor.skyBridge.Remove(k);
        madeFor.reserved.Clear();
        quarters[0] = MakeQuarter(madeFor, 0, 0, mode, GetComponent<IslandPopulation>());
        if (island != null && island.Plan == madeFor)
        {
            island.Return(island.Zone, madeFor.Arrival(island.Zone)); // its bridge over the sky must be built
            return;
        }
        SpawnZone();
        GetComponent<KeyDungeon>()?.Refresh();
        GetComponent<BossPortal>()?.Refresh();
    }

    GUIStyle label, barLabel;
    float barShown = -1f;
    int barFrame;
    static readonly Color FirstPhase = new(0.85f, 0.2f, 0.2f), SecondPhase = new(0.75f, 0.3f, 0.95f);

    // The islet's boss, top centre: its name and health (another colour once it has changed).
    void BossBar(float s)
    {
        var b = boss.FirstOrDefault(c => c != null && c.IsAlive);
        if (b == null) { barShown = -1f; return; }
        var ultimate = b.GetComponent<EnemyUltimate>();
        bool second = ultimate != null && !string.IsNullOrEmpty(ultimate.LastUsed);
        var bar = new Rect(Screen.width / 2f - 110f * s, BossBarTop(s), 220f * s, 9f * s);
        ReserveBossBar(11f * s);
        Fill(new Rect(bar.x - s, bar.y - s, bar.width + 2f * s, bar.height + 2f * s), new Color(0f, 0f, 0f, 0.8f));
        float shown = Ease(ref barShown, ref barFrame, Mathf.Clamp01(b.hp / b.maxHp), 0.25f); // its heal fills the bar over ~1 s
        Fill(new Rect(bar.x, bar.y, bar.width * shown, bar.height), second ? SecondPhase : FirstPhase);
        barLabel ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        barLabel.fontSize = Mathf.RoundToInt(7f * s);
        GUI.Label(bar, b.Stats.name, barLabel); // no hint of a second phase: it comes as a surprise
    }

    void OnGUI()
    {
        float s = Scale;
        BossBar(s);
        if (madeFor == null || Total == 0) return;
        if (island.Away) return;
        var box = new Rect(Side * s, (Top + 31f) * s, StageWidth * s, 14f * s);
        Panel(box, s);
        var q = Here;
        string line = $"Engrenages du Temps : {Found} / {Total}";
        if (q != null && !q.gear)
            line += q.mode == Fragments ? $" · fragments {q.fragments.Count(f => f.taken)} / {q.fragments.Count}"
                : q.hasKey ? " · îlot du boss" : " · clé dans le donjon";
        label ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
        label.fontSize = Mathf.RoundToInt(7f * s);
        GUI.Label(new Rect(box.x + 6f * s, box.y, box.width - 12f * s, box.height), line, label);
        KeyItems(new Rect(box.x, box.yMax + 3f * s, box.width, 16f * s), q, s);
    }

    // The important items, under the counter: a Time Gear per quarter (lit once found), the quarter's key (grey until
    // found) or its fragments. Story art (GroundObjects, PMDCollab's key).
    void KeyItems(Rect row, Quarter q, float s)
    {
        Panel(row, s);
        float size = 12f * s, x = row.x + 4f * s, y = row.y + 2f * s;
        var gear = RomArt.Get("GroundObjects/time_gear");
        for (int k = 0; k < 4; k++)
        {
            var quarter = quarters[madeFor.order[k]];
            if (quarter == null) continue;
            Icon(new Rect(x, y, size, size), gear, quarter.gear ? 1f : 0.25f);
            x += size + 2f * s;
        }
        if (q == null || q.gear) return;
        x += 6f * s;
        if (q.mode == Fragments)
            foreach (var f in q.fragments)
            {
                Icon(new Rect(x, y + 2f * s, size * 0.7f, size * 0.7f), gear, f.taken ? 1f : 0.25f);
                x += size * 0.7f + 2f * s;
            }
        else Icon(new Rect(x, y, size, size), SpecialItems.Icon(Data.boss.keyIcon), q.hasKey ? 1f : 0.25f);
    }

    static void Icon(Rect rect, Sprite sprite, float alpha)
    {
        if (sprite == null) return;
        var t = sprite.texture;
        var r = sprite.textureRect;
        // Keep its proportions inside the square.
        float scale = Mathf.Min(rect.width / r.width, rect.height / r.height);
        var fit = new Rect(rect.center.x - r.width * scale / 2f, rect.center.y - r.height * scale / 2f, r.width * scale, r.height * scale);
        var previous = GUI.color;
        GUI.color = new Color(alpha < 1f ? 0.5f : 1f, alpha < 1f ? 0.5f : 1f, alpha < 1f ? 0.55f : 1f, Mathf.Max(alpha, 0.6f));
        GUI.DrawTextureWithTexCoords(fit, t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
        GUI.color = previous;
    }
}
