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
        public float dungeonShare = 0.45f, portalShare = 0.5f, legendaryChance = 0.03f, scale = 1.6f;
        public float health = 30f, stoneChance = 0.3f, damage = 4f, speed = 0.9f, recover = 0.6f;
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
        public Vector2Int dungeonTile = new(-1, -1), portalTile = new(-1, -1); // the key dungeon's entrance, the islet's portal
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

    // The quarter's level, or the team's when it is higher (a boss must not fall to a team that outgrew the island).
    int Level(Quarter q, int bonus) => Mathf.Max(Data.baseLevel + Populations.LevelBonus(q.rank, q.distance), progression != null ? progression.Level : 1) + bonus;

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
        if (c.GetComponent<BossAI>() is { } ai)
        {
            ai.seeksReach = true;
            ai.speedFactor = Data.boss.speed;
            ai.recoverTime = Data.boss.recover;
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
        progression?.Announce($"Le gardien du quartier : {c.Stats.name} !", Notice.Elite);
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

    GUIStyle label, barLabel;
    static readonly Color FirstPhase = new(0.85f, 0.2f, 0.2f), SecondPhase = new(0.75f, 0.3f, 0.95f);

    // The islet's boss, top centre: its name, a mark at the threshold of its second phase, another colour once in it.
    void BossBar(float s)
    {
        var b = boss.FirstOrDefault(c => c != null && c.IsAlive);
        if (b == null) return;
        var ultimate = b.GetComponent<EnemyUltimate>();
        bool second = ultimate != null && !string.IsNullOrEmpty(ultimate.LastUsed);
        var bar = new Rect(Screen.width / 2f - 110f * s, (Top + 2f) * s, 220f * s, 9f * s);
        Fill(new Rect(bar.x - s, bar.y - s, bar.width + 2f * s, bar.height + 2f * s), new Color(0f, 0f, 0f, 0.8f));
        Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(b.hp / b.maxHp), bar.height), second ? SecondPhase : FirstPhase);
        if (!second) Fill(new Rect(bar.x + bar.width * SpecialItems.Enemy.phaseHealth - s, bar.y - 2f * s, 2f * s, bar.height + 4f * s), Color.white);
        barLabel ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        barLabel.fontSize = Mathf.RoundToInt(7f * s);
        GUI.Label(bar, $"{b.Stats.name} · phase {(second ? 2 : 1)} / 2", barLabel);
    }

    void OnGUI()
    {
        if (madeFor == null || Total == 0) return;
        float s = Scale;
        BossBar(s);
        if (island.Away) return;
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
