using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Dev sandbox (Run.Sandbox, from the team menu in devMode): Pokémon set around the arena keep terrain alive,
/// so terrains and their affinities can be tried at leisure. Read from terrain_sandbox.json. The emitters are
/// scenery: no collider, nothing can hit them, and the terrain they paint has no owner (it acts on everyone).
/// An emitter casts its zone, then only keeps alive the tiles that still hold it (every "every" seconds): a tile
/// neutralised by another terrain stays so until the next cast ("recast" seconds later). Strikes on another patch
/// (a "target") are casts each time.
/// Stairs lead to a second room, the obstacle room (Obstacles.Generate from obstacle_room.json: 2 or 3 obstacles
/// drawn at random, R or Select to draw again), and back. Other stairs lead to the mega gallery: every mega evolution
/// of megas.json standing still in a grid, scenery like the emitters, unnamed; the one the Pokémon stands next to faces it and
/// plays its animations one after the other.
/// The obstacle room also holds training dummies ("dummies"): enemies that neither move nor attack, at random free
/// tiles, which come back a few seconds after being knocked out, to try moves, Z moves and obstacles on.
/// Another room shows the Z moves (dark orange stairs): sturdy dummies in a ring, the controlled Pokémon carries the Z crystal picked with
/// ↑/↓ (1/2) and borrows the strongest move of its type when it knows none (Ultimates.DemoMoves).
/// Two more rooms are real fights, with the opponent back a few seconds after a knock-out: an elite beyond a river
/// (with a bridge or not, R / Select: it freezes the river to come, burns the bridge under a close fighter, uses
/// its ultimate), and a legendary boss whose ultimate (Terastal phase or Z move) depends on the fight. The blue stairs
/// lead to the rescue and heal demos (RescueDemos). Rooms 6 to 9 (ZDemoRoom...RescueRoom) follow those of the island work.
public class TerrainSandbox : MonoBehaviour
{
    const float AttackAnimTime = 0.4f;

    [Serializable]
    class Emitter
    {
        public int pokemon;
        public float[] offset = new float[2];
        public string terrain;
        public float[] target = new float[2];
        public float radius = 2f;
        public float every = 2f;
        public float duration = 3f;
        public float delay;
        public float recast = 15f; // seconds between two full casts of the zone
    }

    [Serializable] class SandboxFile { public bool enemyWaves = true; public Emitter[] emitters; public int galleryColumns = 9; public float gallerySpacing = 3f;
        public int[] dummies = new int[0]; public int dummyCount = 6, dummyLevel = 5; public float dummyRespawn = 4f, dummyHealth = 3f, dummyMinDistance = 4f;
        public int zDemoCount = 8; public float zDemoRadius = 4.5f, zDemoHealth = 30f, zDemoRespawn = 2f;
        public Fighter eliteRoom = new(), bossRoom = new(); public Focus abilityFocus = new(); }

    /// The cases the ability room opens on (AbilityDemos), when not empty.
    [Serializable] class Focus { public int[] abilities = new int[0], megas = new int[0]; }

    /// Ability ids and megas.json ids the ability room opens on (both empty: its usual first list).
    public static (int[] abilities, int[] megas) AbilityFocus()
    {
        var f = Settings().abilityFocus ?? new Focus();
        return (f.abilities ?? new int[0], f.megas ?? new int[0]);
    }

    /// The opponent of the elite or boss room: a real fight (its AI, its moves, its ultimate).
    [Serializable]
    class Fighter
    {
        public int pokemon;
        public int[] moves = new int[0];
        public int level = 8;
        public float health = 3f, respawn = 6f;
        public float[] offset = new float[2];
    }

    [Serializable] class Mega { public int id; public string name; }
    [Serializable] class MegaFile { public Mega[] megas; }

    class Shown
    {
        public PmdAnimator animator;
        public List<string> anims;
        public int anim = -1; // the animation shown while inspected, -1 when idle
        public float nextAnim;
    }

    readonly List<Shown> gallery = new();
    const float InspectReach = 1.5f, AnimShowTime = 1.2f;

    class Live
    {
        public Emitter def;
        public Vector2 position, target;
        public PmdAnimator animator;
        public float next, idleAt, nextCast;
    }

    readonly List<Live> emitters = new();

    const float FadeTime = 0.25f, StairsReach = 0.6f;
    static readonly Vector2 BaseStairs = new(12f, 12f), ObstacleStairs = new(-12f, -12f); // from the arena centre, in tiles
    static readonly Vector2 GalleryStairs = new(12f, -12f), GalleryBackStairs = new(-12f, 12f);
    static readonly Vector2 RoomsStairs = new(-12f, -12f); // to the room gallery (rooms.json)
    static readonly Vector2 IslandStairs = new(0f, -12f); // to a test island (IslandPlan, IslandRun)
    static readonly Vector2 QuarterStairs = new(0f, 12f); // to the test quarter of island (quarter_test.json)

    // Rooms of the items and AI work (dev): Z demo, elite, boss, rescue demos.
    static readonly Vector2 ZDemoStairs = new(-12f, 12f), ZDemoBackStairs = new(12f, 12f);
    static readonly Vector2 EliteStairs = new(-12f, 0f), EliteBackStairs = new(-12f, 12f);
    static readonly Vector2 BossStairs = new(6f, 12f), BossBackStairs = new(0f, 12f);
    static readonly Vector2 RescueStairs = new(12f, 0f), RescueBackStairs = new(-12f, 0f);
    static readonly Vector2 EncounterStairs = new(-6f, -12f); // to the encounter lab (its way back: ObstacleStairs)
    public const int EncounterRoom = 20; // far from the other rooms' numbers, which other branches add to
    public const int AbilityRoom = 21; // the demo of every ability (AbilityDemos)
    static readonly Vector2 AbilityStairs = new(6f, -12f); // its way back: RescueBackStairs
    const int RiverFrom = 3, RiverTo = 5; // elite room: columns of the river, from the arrival point
    static readonly Vector2 GalleryCentre = new(0f, 5f); // above the arrival point, which stays clear

    // 0: terrain sandbox, 1: obstacle room, 2: mega gallery, 3: room gallery, 4: test island, 5: test quarter,
    // 6: Z demo, 7: elite room, 8: boss room, 9: rescue demos
    int room;
    const int ZDemoRoom = 6, EliteRoom = 7, BossRoom = 8, RescueRoom = 9;
    Combatant fighter; // the elite or the boss of their rooms
    float fighterBack; // when it comes back once knocked out
    bool bridge; // elite room: the river has a bridge (R / Select switches)
    class Dummy
    {
        public Combatant combatant;
        public int pokemon;
        public Vector2? at; // where it stands again (Z demo room), else a random free tile
        public float health, back; // health ×; when a knocked-out dummy comes back
    }

    readonly List<Dummy> dummies = new();
    readonly Dictionary<Combatant, SpecialItems.Equipped> keptItems = new(); // items before the Z demo room
    int zDemoIndex;
    int quarterSeed, quarterRock; // test quarter: kept to draw the same one again with another rock (C)
    IslandRun island; // test island
    RoomInstance shownRoom; // room gallery: the room on show
    int roomType; // room gallery: 0 any type, else RoomCatalog.Types[roomType - 1]
    DungeonTileset sandboxDecor; // the arena's own décor, put back when leaving the room gallery
    int pendingRoom = -1;
    float fadeOutAt, switchAt, fadeInUntil;
    bool stairsArmed; // the stairs only work once the Pokémon has stepped off them
    readonly List<(GameObject stairs, int room)> stairs = new();
    Transform emitterRoot; // the emitters' parent (this component sits on the enemy spawner, whose children are enemies)
    readonly System.Random rng = new();
    List<string> drawn = new();
    GUIStyle label;

    /// Whether enemies keep coming in the sandbox (EnemySpawner asks).
    public static bool EnemyWaves()
    {
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        return asset == null || JsonUtility.FromJson<SandboxFile>(asset.text).enemyWaves;
    }

    void Start()
    {
        SpawnEmitters();
        PlaceStairs();
        if (DungeonGenerator.Current != null) PlaceShowcase(DungeonGenerator.Current);
    }

    // Every held item in a row below the arrival point, to try them, and a row of special items below (room 0).
    void PlaceShowcase(DungeonGenerator arena)
    {
        var loot = GetComponent<Loot>();
        if (loot == null) return;
        for (int k = 0; k < HeldItems.All.Count; k++)
            loot.PlaceHeld(HeldItems.All[k].key, Centre(arena) + new Vector2((k - (HeldItems.All.Count - 1) / 2f) * 1.4f, -7f), HeldItems.ShowcaseTier);
        // A second row: a few special items (mega stones, Z crystals, Tera shards), to see them.
        var specials = new List<(SpecialItems.Family family, string stone, int type)>();
        for (int k = 0; k < SpecialItems.Stones.Count && k < 3; k++) specials.Add((SpecialItems.Family.Mega, SpecialItems.Stones[k].key, -1));
        foreach (int type in new[] { 2, 3, 5, 14 }) specials.Add((SpecialItems.Family.Z, null, type));
        foreach (int type in new[] { 2, 3, 5, 14 }) specials.Add((SpecialItems.Family.Tera, null, type));
        for (int k = 0; k < specials.Count; k++)
            loot.PlaceSpecial(specials[k].family, specials[k].stone, specials[k].type,
                Centre(arena) + new Vector2((k - (specials.Count - 1) / 2f) * 1.4f, -9f));
    }

    static Vector2 Centre(DungeonGenerator arena) => arena.CentreOf(arena.Size / 2, arena.Size / 2);

    void SpawnEmitters()
    {
        var arena = DungeonGenerator.Current;
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        if (arena == null || asset == null) return;
        Vector2 centre = Centre(arena);
        foreach (var e in JsonUtility.FromJson<SandboxFile>(asset.text).emitters ?? new Emitter[0])
        {
            var set = PokemonRoster.Get(e.pokemon);
            Vector2 at = centre + Vec(e.offset);
            var go = new GameObject($"Émetteur {GameData.Pokemon(e.pokemon)?.name} ({e.terrain})");
            if (emitterRoot == null) emitterRoot = new GameObject("Émetteurs").transform;
            go.transform.SetParent(emitterRoot, false);
            go.transform.position = at;
            go.AddComponent<SpriteRenderer>().sortingOrder = 1;
            PmdAnimator animator = null;
            if (set != null)
            {
                animator = go.AddComponent<PmdAnimator>();
                animator.SetAnimSet(set);
                animator.direction = PmdAnimator.DirectionIndex(centre - at); // facing the middle
            }
            emitters.Add(new Live { def = e, position = at, target = at + Vec(e.target), animator = animator, next = Time.time + e.delay, nextCast = Time.time + e.delay });
        }
    }

    static Vector2 Vec(float[] v) => v != null && v.Length >= 2 ? new Vector2(v[0], v[1]) : Vector2.zero;

    // Every mega evolution with sprites, in rows facing down around the arena centre.
    void SpawnGallery()
    {
        var arena = DungeonGenerator.Current;
        var asset = Resources.Load<TextAsset>("Data/megas");
        var settings = Resources.Load<TextAsset>("Data/terrain_sandbox");
        if (arena == null || asset == null) return;
        var file = settings != null ? JsonUtility.FromJson<SandboxFile>(settings.text) : new SandboxFile();
        var megas = new List<Mega>();
        foreach (var m in JsonUtility.FromJson<MegaFile>(asset.text).megas ?? new Mega[0])
            if (PokemonRoster.Has(m.id)) megas.Add(m);
        int columns = Mathf.Max(1, file.galleryColumns);
        int rows = (megas.Count + columns - 1) / columns;
        if (emitterRoot == null) emitterRoot = new GameObject("Galerie des mégas").transform;
        for (int i = 0; i < megas.Count; i++)
        {
            int row = i / columns, column = i % columns;
            int inRow = Mathf.Min(columns, megas.Count - row * columns);
            var at = Centre(arena) + GalleryCentre + new Vector2((column - (inRow - 1) / 2f) * file.gallerySpacing, ((rows - 1) / 2f - row) * file.gallerySpacing);
            var go = new GameObject(megas[i].name);
            go.transform.SetParent(emitterRoot, false);
            go.transform.position = at;
            go.AddComponent<SpriteRenderer>().sortingOrder = 1;
            var set = PokemonRoster.Get(megas[i].id);
            var animator = go.AddComponent<PmdAnimator>();
            animator.SetAnimSet(set);
            animator.direction = PmdAnimator.DirectionIndex(Vector2.down);
            var anims = new List<string>();
            foreach (var a in set.anims) anims.Add(a.name);
            gallery.Add(new Shown { animator = animator, anims = anims });
        }
    }

    /// Removes the emitters and dummies (the AI test bench needs a quiet arena).
    public void Quiet()
    {
        ClearEmitters();
        ClearDummies();
    }

    // ---- Training dummies (obstacle room) ----

    static SandboxFile Settings()
    {
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        return asset != null ? JsonUtility.FromJson<SandboxFile>(asset.text) : new SandboxFile();
    }

    void SpawnDummies()
    {
        var file = Settings();
        for (int i = 0; i < file.dummyCount && file.dummies.Length > 0; i++)
            AddDummy(file, file.dummies[i % file.dummies.Length], null, file.dummyHealth);
    }

    // Z demo room: dummies in a ring around the arrival point, sturdy enough to show every effect, back where they stood.
    void SpawnZDemo()
    {
        var file = Settings();
        var arena = DungeonGenerator.Current;
        if (arena == null || file.dummies.Length == 0) return;
        for (int i = 0; i < file.zDemoCount; i++)
        {
            float angle = (90f + 360f * i / file.zDemoCount) * Mathf.Deg2Rad;
            AddDummy(file, file.dummies[i % file.dummies.Length], Centre(arena) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * file.zDemoRadius, file.zDemoHealth);
        }
    }

    void AddDummy(SandboxFile file, int pokemonId, Vector2? at, float health)
    {
        var d = new Dummy { pokemon = pokemonId, at = at, health = health };
        d.combatant = SpawnDummy(file, d);
        dummies.Add(d);
    }

    // An enemy without AI: it stands still and never attacks, but takes hits, statuses and pushes.
    Combatant SpawnDummy(SandboxFile file, Dummy d)
    {
        var arena = DungeonGenerator.Current;
        if (arena == null || !PokemonRoster.Has(d.pokemon)) return null;
        var go = new GameObject($"Cible : {GameData.Pokemon(d.pokemon)?.name}");
        go.transform.SetParent(transform, false);
        go.transform.position = d.at ?? (Vector2)arena.RandomOpenPosition(rng, file.dummyMinDistance);
        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.mass = 5f; // hard to shove by walking into it
        body.linearDamping = 8f; // stops where a push leaves it
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var animator = go.AddComponent<PmdAnimator>();
        animator.SetAnimSet(PokemonRoster.Get(d.pokemon));
        animator.direction = PmdAnimator.DirectionIndex(Vector2.down);
        var c = go.AddComponent<Combatant>();
        c.Init(d.pokemon, Team.Enemy, new int[0]);
        for (int level = 2; level <= file.dummyLevel; level++) c.ApplyGrowth(LevelData.Growth(d.pokemon, level), level);
        c.maxHp = c.hp = c.maxHp * d.health;
        go.AddComponent<HealthBar>();
        return c;
    }

    void UpdateDummies()
    {
        if (dummies.Count == 0) return;
        SandboxFile file = null;
        foreach (var d in dummies)
        {
            if (d.combatant != null && d.combatant.IsAlive) continue;
            file ??= Settings();
            if (d.back <= 0f)
            {
                d.back = Time.time + (room == ZDemoRoom ? file.zDemoRespawn : file.dummyRespawn);
                continue;
            }
            if (Time.time < d.back) continue;
            if (d.combatant != null) Destroy(d.combatant.gameObject);
            if (d.at == null) d.pokemon = file.dummies[rng.Next(file.dummies.Length)];
            d.combatant = SpawnDummy(file, d);
            d.back = 0f;
        }
    }

    // ---- Elite and boss rooms: a real fight ----

    // A river from wall to wall between the arrival point and the elite, with a bridge in the middle or not.
    void BuildRiver(DungeonGenerator arena)
    {
        if (Obstacles.Current == null) return;
        var centre = arena.CellOf(Centre(arena));
        var inside = arena.Interior;
        for (int x = centre.x + RiverFrom; x <= centre.x + RiverTo; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                Obstacles.Place(x, y, bridge && Mathf.Abs(y - centre.y) <= 0 ? "bridge" : "water");
    }

    // The boss room: the bosses of the first zone's roster (time_gears.json, "test"), as on their islet.
    int bossIndex;
    readonly List<Combatant> bossParty = new();
    List<(int id, bool legendary)> BossRoster => TimeGears.Roster("test");
    string arenaName => DungeonGenerator.Current != null && DungeonGenerator.Current.room != null && DungeonGenerator.Current.room.variant != null
        ? DungeonGenerator.Current.room.variant.name : "—";

    void SpawnFighter()
    {
        var spawner = GetComponent<EnemySpawner>();
        var arena = DungeonGenerator.Current;
        if (room == BossRoom)
        {
            var roster = BossRoster;
            if (spawner == null || arena == null || roster.Count == 0) return;
            var (id, legendary) = roster[bossIndex % roster.Count];
            var gears = GetComponent<TimeGears>();
            if (gears == null) gears = gameObject.AddComponent<TimeGears>();
            foreach (var c in bossParty) if (c != null) Destroy(c.gameObject);
            bossParty.Clear();
            bossParty.AddRange(gears.SandboxBoss(id, legendary, spawner, arena.RandomOpenPosition(rng, 8f, arena.SpawnPoint)));
            fighter = bossParty.Count > 0 ? bossParty[0] : null;
            fighterBack = 0f;
            return;
        }
        var f = room == EliteRoom ? Settings().eliteRoom : Settings().bossRoom;
        if (spawner == null || arena == null || f.pokemon <= 0) return;
        fighter = spawner.SpawnFighter(f.pokemon, f.moves.Length > 0 ? f.moves : null, Centre(arena) + Vec(f.offset),
            isBoss: room == BossRoom, elite: room == EliteRoom, f.level, f.health);
        if (fighter != null && room == BossRoom) fighter.gameObject.AddComponent<HealthBar>(); // no boss bar in the test arena
        fighterBack = 0f;
    }

    void UpdateFighter()
    {
        if ((room != EliteRoom && room != BossRoom) || pendingRoom >= 0 || (fighter != null && fighter.IsAlive)) return;
        var f = room == EliteRoom ? Settings().eliteRoom : Settings().bossRoom;
        if (fighterBack <= 0f)
        {
            fighterBack = Time.time + f.respawn;
            return;
        }
        if (Time.time < fighterBack) return;
        if (fighter != null) Destroy(fighter.gameObject);
        if (room == BossRoom) { Switch(BossRoom); return; } // the arena again, the minions too
        SpawnFighter();
    }

    // ---- Z demo room: the controlled Pokémon carries the chosen Z crystal ----

    void UpdateZDemo()
    {
        var team = FindAnyObjectByType<PlayerController>();
        var member = team != null ? team.Controlled : null;
        var crystals = SpecialItems.Crystals;
        if (member == null || crystals.Count == 0) return;
        if (!keptItems.ContainsKey(member)) keptItems[member] = member.Special;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        int step = 0;
        if ((pad != null && pad.dpad.up.wasPressedThisFrame) || (kb != null && kb.digit1Key.wasPressedThisFrame)) step = -1;
        if ((pad != null && pad.dpad.down.wasPressedThisFrame) || (kb != null && kb.digit2Key.wasPressedThisFrame)) step = 1;
        zDemoIndex = (zDemoIndex + step + crystals.Count) % crystals.Count;
        int type = crystals[zDemoIndex].type;
        if (member.Special == null || member.Special.family != SpecialItems.Family.Z || member.Special.type != type)
            member.Special = new SpecialItems.Equipped { family = SpecialItems.Family.Z, type = type, level = 1 };
    }

    void RestoreItems()
    {
        foreach (var (member, item) in keptItems)
            if (member != null) member.Special = item;
        keptItems.Clear();
        Ultimates.DemoMoves = false;
    }

    void ClearDummies()
    {
        foreach (var d in dummies)
            if (d.combatant != null) Destroy(d.combatant.gameObject);
        dummies.Clear();
    }

    void ClearEmitters()
    {
        emitters.Clear();
        gallery.Clear();
        if (emitterRoot != null) Destroy(emitterRoot.gameObject);
        emitterRoot = null;
    }

    // Provisional stairs: a dark square with a light one inside, to walk onto.
    void PlaceStairs()
    {
        var arena = DungeonGenerator.Current;
        if (arena == null) return;
        foreach (var (old, _) in stairs) Destroy(old);
        stairs.Clear();
        if (room == 0)
        {
            AddStairs(arena, BaseStairs, 1, "Escalier vers la salle des obstacles", new Color(0.95f, 0.8f, 0.3f));
            AddStairs(arena, GalleryStairs, 2, "Escalier vers la galerie des mégas", new Color(0.85f, 0.5f, 1f));
            AddStairs(arena, RoomsStairs, 3, "Escalier vers la galerie des salles", new Color(0.5f, 0.9f, 0.5f));
            AddStairs(arena, IslandStairs, 4, "Escalier vers une île de test", new Color(1f, 0.6f, 0.2f));
            AddStairs(arena, QuarterStairs, 5, "Escalier vers le quart d'île de test", new Color(0.3f, 0.85f, 0.75f));
            AddStairs(arena, ZDemoStairs, ZDemoRoom, "Escalier vers la démo des capacités Z", new Color(1f, 0.4f, 0.1f));
            AddStairs(arena, EliteStairs, EliteRoom, "Escalier vers la salle de l'élite", new Color(1f, 0.85f, 0.25f));
            AddStairs(arena, BossStairs, BossRoom, "Escalier vers la salle du boss", new Color(0.9f, 0.2f, 0.2f));
            AddStairs(arena, RescueStairs, RescueRoom, "Escalier vers les démos de secours", new Color(0.3f, 0.6f, 1f));
            AddStairs(arena, EncounterStairs, EncounterRoom, "Escalier vers l'observation des rencontres", new Color(0.9f, 0.9f, 0.9f));
            AddStairs(arena, AbilityStairs, AbilityRoom, "Escalier vers la démo des talents", new Color(1f, 0.55f, 0.8f));
        }
        else if (room == 4 || room == 5)
            AddStairs(arena, FreeNear(arena, arena.CellOf(arena.SpawnPoint)) - Centre(arena), 0, "Escalier vers le terrain de test", new Color(0.6f, 0.85f, 1f));
        else if (room == 3) AddStairs(arena, BackStairsNearSpawn(arena), 0, "Escalier vers le terrain de test", new Color(0.6f, 0.85f, 1f));
        else AddStairs(arena, room switch { 1 or EncounterRoom => ObstacleStairs, ZDemoRoom => ZDemoBackStairs, EliteRoom => EliteBackStairs, BossRoom => BossBackStairs, RescueRoom or AbilityRoom => RescueBackStairs, _ => GalleryBackStairs }, 0,
            "Escalier vers le terrain de test", new Color(0.6f, 0.85f, 1f));
        stairsArmed = false;
    }

    void AddStairs(DungeonGenerator arena, Vector2 offset, int target, string name, Color colour)
    {
        var cell = arena.CellOf(Centre(arena) + offset);
        var go = new GameObject(name);
        go.transform.position = arena.CentreOf(cell.x, cell.y);
        var outer = go.AddComponent<SpriteRenderer>();
        outer.sprite = Shapes.Square;
        outer.color = new Color(0.1f, 0.1f, 0.12f);
        outer.sortingOrder = -1; // under the light square inside (a tie would hide it)
        var inner = new GameObject("Marches").AddComponent<SpriteRenderer>();
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = Vector3.one * 0.6f;
        inner.sprite = Shapes.Square;
        inner.color = colour;
        inner.sortingOrder = 0;
        stairs.Add((go, target));
    }

    // The first free tile (no wall, no water) about 3 tiles from a cell, for the way back on an island.
    static Vector2 FreeNear(DungeonGenerator arena, Vector2Int cell)
    {
        for (int r = 3; r <= 10; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = cell.x + dx, y = cell.y + dy;
                    if (arena.Walkable(x, y) && Obstacles.At(x, y) == null) return arena.CentreOf(x, y);
                }
        return arena.CentreOf(cell.x, cell.y);
    }

    // A room of the gallery may be small or full of walls: the way back is laid on the first free tile found a few
    // tiles from the spawn point (as an offset from the centre).
    static Vector2 BackStairsNearSpawn(DungeonGenerator arena)
    {
        var centre = arena.CellOf(Centre(arena));
        for (int r = 3; r <= 8; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = centre.x + dx, y = centre.y + dy;
                    if (arena.Walkable(x, y) && Obstacles.At(x, y) == null) return arena.CentreOf(x, y) - Centre(arena);
                }
        return Vector2.zero;
    }

    List<Vector2Int> KeptClear(DungeonGenerator arena)
    {
        var c = Centre(arena);
        return new List<Vector2Int> { arena.CellOf(c), arena.CellOf(c + ObstacleStairs), arena.CellOf(c + BaseStairs) };
    }

    // Fades out, rebuilds the arena as the other room (or draws the obstacle room again), fades back in.
    /// Draws the current room again (the encounter lab, between two rounds).
    public void Redraw() => Switch(room);

    /// Raised once a room has been built.
    public event Action Rebuilt;

    void Switch(int target)
    {
        if (pendingRoom >= 0) return;
        pendingRoom = target;
        fadeOutAt = Time.unscaledTime;
        switchAt = Time.unscaledTime + FadeTime;
    }

    void Rebuild()
    {
        var arena = DungeonGenerator.Current;
        var team = FindAnyObjectByType<PlayerController>();
        if (arena == null) return;
        room = pendingRoom;
        foreach (var zone in FindObjectsByType<Zone>()) Destroy(zone.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        ClearEmitters();
        ClearDummies();
        if (fighter != null) Destroy(fighter.gameObject);
        fighter = null;
        fighterBack = 0f;
        foreach (var c in bossParty) if (c != null) Destroy(c.gameObject); // the islet boss's minions
        bossParty.Clear();
        RestoreItems();
        var demos = GetComponent<RescueDemos>();
        if (room != RescueRoom && demos != null) Destroy(demos);
        var abilityDemos = GetComponent<AbilityDemos>();
        if (room != AbilityRoom && abilityDemos != null) Destroy(abilityDemos);
        // The room gallery builds a room of the catalogue; the other rooms are the sandbox's own open arena.
        if (sandboxDecor == null) sandboxDecor = arena.tileset;
        // Before the island rooms too: the showcase of room 0 lay under the secret rooms (same world positions).
        var loot = GetComponent<Loot>();
        if (loot != null) loot.ClearGround();
        if (island != null) island.End();
        // What lies on the ground (the showcase of room 0) stays behind, the test island included.
        if (GetComponent<Loot>() is { } ground) ground.ClearGround();
        if (room == 4 || room == 5)
        {
            // A test island: four quarters of biomes around the sealed central zone; or the test quarter.
            if (island == null)
            {
                island = gameObject.AddComponent<IslandRun>();
                island.Built += PlaceStairs; // the way back, near where the team arrives in each zone
                island.revealAll = true; // test island: the whole mini-map at once
            }
            if (room == 5)
            {
                island.Begin(IslandPlan.QuarterTestPlan(quarterSeed, quarterRock));
                quarterSeed = 0; // R draws a new one
            }
            else island.Begin(rng);
            PlaceStairs();
            return;
        }
        if (room == BossRoom && BossRoster.Count > 0)
        {
            // The arena made for this boss's types (islands and bridges for a Vol, lava for a Feu...).
            var (id, legendary) = BossRoster[bossIndex % BossRoster.Count];
            arena.room = TimeGears.ArenaFor(id, legendary, rng);
        }
        else if (room == 3)
        {
            var types = RoomCatalog.Types;
            shownRoom = RoomCatalog.Pick(roomType > 0 && roomType <= types.Count ? types[roomType - 1].key : null, rng);
            arena.room = shownRoom;
        }
        else
        {
            arena.room = null;
            arena.tileset = sandboxDecor;
        }
        arena.Generate(); // also clears terrain and obstacles, and moves the team object to the centre
        drawn = new List<string>();
        if ((room == 1 || room == EncounterRoom) && Obstacles.Current != null)
        {
            var asset = Resources.Load<TextAsset>("Data/obstacle_room");
            if (asset != null) drawn = Obstacles.Current.Generate(JsonUtility.FromJson<Obstacles.Room>(asset.text), rng, KeptClear(arena));
        }
        if (room == 0) SpawnEmitters();
        if (room == 0) PlaceShowcase(arena);
        if (room == 1) SpawnDummies();
        if (room == ZDemoRoom)
        {
            SpawnZDemo();
            Ultimates.DemoMoves = true;
        }
        if (room == EliteRoom) BuildRiver(arena);
        if (room == EliteRoom || room == BossRoom) SpawnFighter();
        if (room == RescueRoom && demos == null) gameObject.AddComponent<RescueDemos>();
        if (room == AbilityRoom && abilityDemos == null) gameObject.AddComponent<AbilityDemos>();
        var lab = GetComponent<EncounterLab>();
        if (room == EncounterRoom && lab == null) gameObject.AddComponent<EncounterLab>();
        if (room != EncounterRoom && lab != null) Destroy(lab);
        if (room == 2) SpawnGallery();
        PlaceStairs();
        if (team != null) team.PrepareForNextArena(team.transform.position, 0f);
        Rebuilt?.Invoke();
    }

    void Update()
    {
        if (pendingRoom >= 0 && Time.unscaledTime >= switchAt)
        {
            Rebuild();
            pendingRoom = -1;
            fadeInUntil = Time.unscaledTime + FadeTime;
        }
        if (room == ZDemoRoom && pendingRoom < 0) UpdateZDemo(); // input: read each frame
        if ((room != 1 && room != 3 && room != 4 && room != 5 && room != EliteRoom && room != BossRoom && room != EncounterRoom) || pendingRoom >= 0) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
        {
            if (room == EliteRoom) bridge = !bridge;
            Switch(room);
        }
        // Test quarter: C or d-pad down draws the same quarter with the other rock for its cliffs.
        if (room == 5 && ((kb != null && kb.cKey.wasPressedThisFrame) || (pad != null && pad.dpad.down.wasPressedThisFrame)))
        {
            quarterSeed = IslandPlan.LastQuarterSeed;
            quarterRock++;
            Switch(5);
        }
        // Boss room: N or d-pad up for the next boss of the roster (its own arena).
        if (room == BossRoom && ((kb != null && kb.nKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame)))
        {
            bossIndex++;
            Switch(BossRoom);
        }
        // Room gallery: N or d-pad up picks the type of room shown (any, then each type in turn).
        if (room == 3 && ((kb != null && kb.nKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame)))
        {
            roomType = (roomType + 1) % (RoomCatalog.Types.Count + 1);
            Switch(3);
        }
    }

    string ZDemoLine()
    {
        var crystals = SpecialItems.Crystals;
        if (crystals.Count == 0) return "Pas de cristal Z dans items.json";
        var c = crystals[zDemoIndex % crystals.Count];
        return $"Démo Z ({zDemoIndex % crystals.Count + 1}/{crystals.Count}) : {c.name} → {c.move} : {SpecialItems.ZDescription(c.type)}"
               + "     ↑/↓ ou 1/2 : cristal     F ou L3 + R3 : lancer     Escalier (en haut à droite) : retour";
    }

    GUIStyle roomLine;

    void OnDestroy() => HudLayout.SandboxLine = 0f;

    void OnGUI()
    {
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        label.fontSize = Mathf.RoundToInt(8f * s);
        string line = room == 0 ? "Escaliers : jaune (en haut à droite) obstacles, violet (en bas à droite) mégas, vert (en bas à gauche) salles, orange (en bas) île, turquoise (en haut) quart d'île, orange foncé (en haut à gauche) capacités Z, doré (à gauche) élite, rouge (en haut, à droite du turquoise) boss, bleu (à droite) secours, blanc (en bas, à gauche de l'orange) rencontres, rose (en bas, à droite de l'orange) talents"
            : room == ZDemoRoom ? ZDemoLine()
            : room == RescueRoom ? (GetComponent<RescueDemos>() is { } rescue ? rescue.Line() : "Démos de secours")
            : room == AbilityRoom ? (GetComponent<AbilityDemos>() is { } talents ? talents.Line() : "Démo des talents")
            : room == EncounterRoom ? (GetComponent<EncounterLab>() is { } lab ? lab.Line(drawn) : "Observation des rencontres")
            : room == EliteRoom ? $"Salle de l'élite : rivière {(bridge ? "avec un pont (en mêlée sur le pont, l'élite le brûle)" : "sans pont (l'élite la gèle pour venir)")}     R ou Select : {(bridge ? "sans" : "avec")} pont     Escalier (en haut à gauche) : retour"
            : room == BossRoom ? $"Boss d'îlot ({bossIndex % Mathf.Max(1, BossRoster.Count) + 1}/{BossRoster.Count}) : {fighter?.Stats?.name ?? "boss"}{(fighter == null || !fighter.IsAlive ? " (revient bientôt)" : "")} · arène {arenaName}     N ou ↑ : boss suivant     R ou Select : relancer     Escalier (en haut) : retour"
            : room == 2 ? "Galerie des mégas : s'approcher d'un Pokémon montre ses animations     Escalier (en haut à gauche) : retour"
            : room == 4 ? $"Île de test : {(island?.Quarter >= 0 ? $"{island.Plan.themes[island.Quarter].name} (quartier {System.Array.IndexOf(island.Plan.order, island.Quarter) + 1}/4) — {island.Biome?.name}" : "hors des quartiers")}     R ou Select : nouvelle île     Escalier (à l'arrivée) : retour"
            : room == 5 ? $"Quart d'île de test : {island?.Biome?.name ?? "—"}     R ou Select : nouveau tirage     C ou croix bas : autre roche     Escalier (à l'arrivée) : retour"
            : room == 3 ? $"{shownRoom?.Describe() ?? "Aucune salle dans rooms.json"}     Type : {(roomType > 0 && roomType <= RoomCatalog.Types.Count ? RoomCatalog.Types[roomType - 1].name : "tous")} (N, croix haut)     R ou Select : autre salle     Escalier : retour"
            : $"Salle des obstacles : {(drawn.Count > 0 ? string.Join(", ", drawn) : "rien")}     Cibles immobiles (reviennent après un K.O.)     R ou Select : nouveau tirage     Escalier (en bas à gauche) : retour";
        // Top centre, between the stage box and the lots panel, wrapped; the banners go below it (HudLayout).
        roomLine ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        roomLine.fontSize = Mathf.RoundToInt(7f * s);
        float width = HudLayout.CenterWidth(s);
        var content = new GUIContent(line.Replace("     ", "  ·  "));
        width = Mathf.Min(width, roomLine.CalcSize(content).x + 16f * s);
        float height = roomLine.CalcHeight(content, width - 12f * s) + 6f * s;
        var rect = new Rect(Screen.width / 2f - width / 2f, HudLayout.Top * s, width, height);
        var previous = GUI.color;
        HudLayout.Panel(rect, s);
        GUI.Label(new Rect(rect.x + 6f * s, rect.y + 3f * s, rect.width - 12f * s, rect.height - 6f * s), content, roomLine);
        HudLayout.SandboxLine = height;
        GalleryLabels(s);
        // Fade between the rooms.
        float alpha = pendingRoom >= 0 ? Mathf.Clamp01((Time.unscaledTime - fadeOutAt) / FadeTime)
            : Time.unscaledTime < fadeInUntil ? (fadeInUntil - Time.unscaledTime) / FadeTime : 0f;
        if (alpha <= 0f) return;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // The animation the inspected gallery Pokémon plays, under it.
    void GalleryLabels(float s)
    {
        var cam = Camera.main;
        if (gallery.Count == 0 || cam == null) return;
        int size = label.fontSize;
        label.fontSize = Mathf.RoundToInt(6f * s);
        foreach (var g in gallery)
        {
            if (g.animator == null || g.anim < 0) continue;
            Vector3 p = cam.WorldToScreenPoint(g.animator.transform.position + Vector3.down * 0.9f);
            if (p.z < 0f) continue;
            string text = g.anims[g.anim];
            var rect = new Rect(p.x - 60f * s, Screen.height - p.y, 120f * s, 16f * s);
            var previous = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + s, rect.y + s, rect.width, rect.height), text, label);
            GUI.color = new Color(1f, 0.9f, 0.5f);
            GUI.Label(rect, text, label);
            GUI.color = previous;
        }
        label.fontSize = size;
    }

    // The gallery Pokémon next to the controlled one faces it and plays its animations in turn; the others idle.
    void UpdateGallery(Combatant walker)
    {
        foreach (var g in gallery)
        {
            if (g.animator == null || g.anims.Count == 0) continue;
            Vector2 at = g.animator.transform.position;
            bool near = walker != null && Vector2.Distance(walker.transform.position, at) <= InspectReach;
            if (!near)
            {
                if (g.anim >= 0)
                {
                    g.anim = -1;
                    g.animator.direction = PmdAnimator.DirectionIndex(Vector2.down);
                    g.animator.Play("Idle");
                }
                continue;
            }
            g.animator.direction = PmdAnimator.DirectionIndex((Vector2)walker.transform.position - at);
            if (g.anim >= 0 && Time.time < g.nextAnim) continue;
            g.anim = (g.anim + 1) % g.anims.Count;
            g.nextAnim = Time.time + AnimShowTime;
            g.animator.Play(g.anims[g.anim]);
        }
    }

    void FixedUpdate()
    {
        var team = FindAnyObjectByType<PlayerController>();
        var walker = team != null ? team.Controlled : null;
        // In a secret room of the test island the way back to the sandbox is put away (SecretStairs has its own).
        bool away = island != null && island.Away;
        foreach (var (go, _) in stairs)
            if (go != null && go.activeSelf == away) go.SetActive(!away);
        if (walker != null && pendingRoom < 0 && !away)
        {
            bool onAny = false;
            foreach (var (go, target) in stairs)
            {
                if (go == null || Vector2.Distance(walker.transform.position, go.transform.position) > StairsReach) continue;
                onAny = true;
                if (stairsArmed) Switch(target);
            }
            if (!onAny) stairsArmed = true;
        }
        UpdateGallery(walker);
        UpdateDummies();
        UpdateFighter();
        foreach (var e in emitters)
        {
            if (e.animator != null && e.idleAt > 0f && Time.time >= e.idleAt)
            {
                e.animator.Play("Idle");
                e.idleAt = 0f;
            }
            if (Time.time < e.next) continue;
            e.next = Time.time + Mathf.Max(0.2f, e.def.every);
            bool strike = Vec(e.def.target) != Vector2.zero;
            if (!strike && Time.time < e.nextCast)
            {
                Terrain.RefreshArea(e.def.terrain, e.target, e.def.radius, e.def.duration);
                continue;
            }
            e.nextCast = Time.time + Mathf.Max(e.def.every, e.def.recast);
            Terrain.PaintArea(e.def.terrain, e.target, e.def.radius, e.def.duration);
            if (e.animator != null)
            {
                // A cast shows an attack, toward the patch it strikes.
                if (strike) e.animator.direction = PmdAnimator.DirectionIndex(e.target - e.position);
                e.animator.Play(e.animator.Has("Shoot") ? "Shoot" : "Attack");
                e.idleAt = Time.time + AttackAnimTime;
            }
        }
    }
}
