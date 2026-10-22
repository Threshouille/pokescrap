using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// A kind of obstacle (deep water, lava, void, bridge, ice wall...), from obstacles.json.
[Serializable]
public class ObstacleDef
{
    public string key;
    public string name;
    public bool solid; // blocks everyone and every shot, like a wall
    public bool fall; // a Pokémon that gets in falls, unless its movement type is in passTypes
    public int[] passTypes = new int[0]; // ROM movement types: 2 float, 3 through walls, 4 lava, 5 swim
    public int[] passPokemonTypes = new int[0]; // Pokémon types that cross it too (Eau through water, Spectre through rocks)
    public Toll[] toll = new Toll[0]; // what crossing costs a type (a Vol over lava)
    public int[] easyTypes = new int[0]; // types it does not slow down nor make slide (Glace on ice)
    public int[] easyMovementTypes = new int[0]; // ROM movement types it leaves alone too (floating over quicksand)
    public float sinkTime; // seconds standing in it before sinking (quicksand): sinkStatus for sinkDuration seconds
    public string sinkStatus;
    public float sinkDuration;
    public float navCost; // extra path cost for the AIs it would hinder (tiles of detour)
    public float fallDamage; // share of max HP lost by a fall
    public string fallStatus; // status given by a fall (burn for lava)
    public float speedMul = 1f;
    public float slideAccel; // tiles/s² to change speed (slippery ground), 0 = instant
    public bool holdsTerrain; // terrain may lie on it (electrified water)
    public int tileset = -1; // ROM tileset whose secondary layer draws it (Resources/ObstacleTiles/<id>)
    public bool overlay; // lies over what was there (bridge over the void, plants on water), drawn on top of it
    public string decaysTo; // what it becomes by itself after decayTime seconds (a burning bridge burns out)
    public float decayTime;
    public RockSource[] romRocks = new RockSource[0]; // ROM rocks drawing it, one picked per tile (variants)
    public string spriteTint; // colour laid over its sprite (a cracked boulder is darker)
    public float bumpStun; // solid: seconds a Pokémon thrown against it (push, slide) is stunned
    public float bumpDamage; // ... and the share of its max HP it loses
    public int[] anchorTypes = new int[0]; // these types cannot be pushed while on it and take less damage (metal: Acier)
    public float anchorDefense = 1f;
    public float magnetRadius; // magnet: reach of its pull, in tiles
    public float magnetPull; // tiles/s it drags the Pokémon of attractTypes toward it
    public float captureRadius; // shots of attractTypes passing this close are caught by it
    public int[] attractTypes = new int[0];
    public bool planks; // provisional look: planks (a bridge) rather than a plain square
    public float flatAlpha = 1f; // opacity of the provisional look
    public string color = "#ffffff"; // fallback look, when neither the ROM tiles nor a hand-made sheet exist
    [NonSerialized] public Color32 tint;
    [NonSerialized] public EffectLibrary.Sheet sheet; // hand-made look: obstacle_<key> in Resources/Effects
    [NonSerialized] public Color spriteColor = Color.white;
    public bool Bridge => key != null && key.Contains("bridge");
}

/// The secondary layer (water, lava or void) of a ROM dungeon tileset, with PMD's banks: the chunk of a tile
/// depends on which of its 8 neighbours are of the same kind, like the arena's walls and floor.
public class RomObstacleTiles
{
    [Serializable] class Layout { public int chunkSize = 24, columns = 16, padding; public int[] water; }

    readonly Texture2D texture;
    readonly Layout layout;
    readonly Dictionary<int, Sprite> sprites = new();

    RomObstacleTiles(Texture2D texture, Layout layout)
    {
        this.texture = texture;
        this.layout = layout;
    }

    static readonly Dictionary<int, RomObstacleTiles> loaded = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        loaded.Clear();
        decors.Clear();
    }

    static readonly Dictionary<DungeonTileset, RomObstacleTiles> decors = new();

    /// The secondary layer of the arena's own décor (its water), or null.
    public static RomObstacleTiles FromDecor(DungeonTileset decor)
    {
        if (decor == null || decor.texture == null || decor.water == null || decor.water.Length < 256 * 3) return null;
        if (decors.TryGetValue(decor, out var found) && found != null) return found;
        found = new RomObstacleTiles(decor.texture, new Layout { chunkSize = decor.chunkSize, columns = decor.columns, padding = decor.padding, water = decor.water });
        decors[decor] = found;
        return found;
    }

    /// Null when the tileset was not extracted (Tools/setup.py).
    public static RomObstacleTiles Get(int id)
    {
        if (id < 0) return null;
        if (loaded.TryGetValue(id, out var found)) return found;
        var texture = Resources.Load<Texture2D>($"ObstacleTiles/{id:000}/chunks");
        var json = Resources.Load<TextAsset>($"ObstacleTiles/{id:000}/tileset");
        found = texture != null && json != null ? new RomObstacleTiles(texture, JsonUtility.FromJson<Layout>(json.text)) : null;
        if (found != null && (found.layout.water == null || found.layout.water.Length < 256 * 3)) found = null;
        loaded[id] = found;
        return found;
    }

    /// Mask bits (neighbour of the same kind): S=1 SE=2 E=4 NE=8 N=16 NW=32 W=64 SW=128; variation 0 to 2.
    public Sprite Tile(int mask, int variation)
    {
        int chunk = layout.water[mask * 3 + variation];
        if (sprites.TryGetValue(chunk, out var sprite) && sprite != null) return sprite;
        int size = layout.chunkSize, cell = size + 2 * layout.padding;
        var rect = new Rect(chunk % layout.columns * cell + layout.padding,
            texture.height - (chunk / layout.columns + 1) * cell + layout.padding, size, size);
        sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        sprites[chunk] = sprite;
        return sprite;
    }
}

/// What a type pays while over an obstacle it crosses: damageShare of max HP per second, and a status with a
/// chance per second. Only when that type is its only way across.
[Serializable]
public class Toll
{
    public int type;
    public float damageShare;
    public string status;
    public float statusChance;
}

/// A rock taken from a ROM tileset: its lone wall chunk, cut out of its floor by Tools/pmd/extract_rock.py
/// (Resources/ObstacleTiles/rock_<tileset>); "cut": floor (default) or core (also removes a shadow on grass).
[Serializable]
public class RockSource
{
    public int tileset;
    public string cut = "floor";
}

/// A move of type "byType" hitting obstacle "on", or terrain "byTerrain" laid on it or next to it: it turns into
/// "becomes" ("" removes it) for "duration" seconds (0: for good), or paints terrain "paintTerrain" over the whole
/// connected obstacle; "leaves": terrain painted where it was removed; "path": also along a projectile's flight;
/// "spread": the change then runs along the connected tiles of "on" (and of "spreadOver"), spreadDelay s a tile.
[Serializable]
public class ObstacleInteraction
{
    public string on;
    public int byType = -1;
    public string byTerrain;
    public bool spread;
    public float spreadDelay = 0.2f;
    public string[] spreadOver = new string[0];
    public string becomes;
    public float duration;
    public bool path;
    public float radius = 0.8f;
    public string paintTerrain;
    public string floodKind; // paintTerrain over the obstacle of this kind next to the hit (a charged magnet: the metal around)
    public string action; // a behaviour of its own instead (pylons: "overcharge", "ground")
    public string leaves;
}

/// Lasting features of the arena's floor, one per tile, under the terrain layer: water, lava and void that only
/// some Pokémon cross (ROM movement types), bridges, ice walls. Walking never takes anyone in (Combatant.Drive
/// stops at the edge), but a push, a slide or a dash may: the Pokémon falls, comes back to its last safe tile and
/// loses some health. Moves change obstacles by their type (an ice move freezes water, fire burns a bridge...).
/// The dev sandbox's obstacle room fills it (Generate); everywhere else it stays empty.
public class Obstacles : MonoBehaviour
{
    const float FallCooldown = 0.6f; // seconds after a fall before another one counts
    const float FallProtection = 1f; // seconds without taking hits after coming back from a fall
    const int SafeSearch = 15; // tiles around a fall searched for solid ground
    const float BumpSpeed = 2.5f; // tiles/s a thrown or sliding Pokémon must have to be hurt by hitting a solid obstacle
    const float BumpReach = 0.45f, BumpCooldown = 0.6f;
    const float EdgeLook = 0.4f; // tiles ahead a walking Pokémon looks for an edge
    const int FloodLimit = 400; // tiles a terrain painted over a whole obstacle reaches at most

    [Serializable] class ObstacleFile { public ObstacleDef[] obstacles; public ObstacleInteraction[] interactions; }

    static Dictionary<string, ObstacleDef> defs;
    static List<ObstacleInteraction> interactions;

    public static Obstacles Current { get; private set; }

    DungeonGenerator grid;
    int size;
    ObstacleDef[] kind;
    ObstacleDef[] revertTo; // what a temporary change turns back into
    ObstacleDef[] under; // what a bridge (or another hand-drawn obstacle) spans: drawn beneath it
    SpriteRenderer[] baseTiles, topTiles; // ROM layer, then hand-made sheet, per tile (made when first needed)
    readonly List<int> animated = new(); // tiles whose hand-made sheet has several frames
    readonly Dictionary<int, EffectLibrary.Sheet> sheetOverrides = new(); // another look for a tile (a charged pylon)
    Transform tileRoot;
    int sortingLayer, sortingOrder;
    float[] revertAt;
    readonly Dictionary<int, GameObject> solids = new(); // colliders of the solid tiles
    readonly Dictionary<Combatant, Vector2> lastSafe = new();
    readonly Dictionary<Combatant, float> fellAt = new();
    readonly Dictionary<Combatant, float> bumpedAt = new();
    Texture2D texture;
    Color32[] pixels;
    bool dirty;
    int count;

    /// Tiles holding an obstacle.
    public int Count => count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        defs = null;
        Current = null;
    }

    static void Load()
    {
        if (defs != null) return;
        arenaFile = null;
        flats.Clear();
        rocks.Clear();
        defs = new Dictionary<string, ObstacleDef>();
        interactions = new List<ObstacleInteraction>();
        var asset = Resources.Load<TextAsset>("Data/obstacles");
        if (asset == null) return;
        var file = JsonUtility.FromJson<ObstacleFile>(asset.text);
        foreach (var d in file.obstacles ?? new ObstacleDef[0])
        {
            d.tint = ColorUtility.TryParseHtmlString(d.color, out var c) ? c : Color.white;
            d.sheet = EffectLibrary.Get("obstacle_" + d.key);
            if (ColorUtility.TryParseHtmlString(d.spriteTint, out var tint)) d.spriteColor = tint;
            defs[d.key] = d;
        }
        interactions.AddRange(file.interactions ?? new ObstacleInteraction[0]);
    }

    public static ObstacleDef Def(string key)
    {
        Load();
        return !string.IsNullOrEmpty(key) && defs.TryGetValue(key, out var d) ? d : null;
    }

    /// Clears the obstacles for a new arena (called by DungeonGenerator.Generate).
    public static void Rebuild(DungeonGenerator arena)
    {
        if (Current == null) Current = new GameObject("Obstacles").AddComponent<Obstacles>();
        Current.Setup(arena);
    }

    void Setup(DungeonGenerator arena)
    {
        Load();
        grid = arena;
        size = arena.Size;
        kind = new ObstacleDef[size * size];
        revertTo = new ObstacleDef[size * size];
        revertAt = new float[size * size];
        under = new ObstacleDef[size * size];
        if (tileRoot != null) Destroy(tileRoot.gameObject);
        tileRoot = null;
        baseTiles = new SpriteRenderer[size * size];
        topTiles = new SpriteRenderer[size * size];
        animated.Clear();
        sheetOverrides.Clear();
        decorTiles = null;
        reach = null;
        foreach (var solid in solids.Values)
            if (solid != null) Destroy(solid);
        solids.Clear();
        lastSafe.Clear();
        fellAt.Clear();
        bumpedAt.Clear();
        passers.Clear();
        seen.Clear();
        tollAt.Clear();
        sunk.Clear();
        pending.Clear();
        Pylons.Clear(this);
        count = 0;
        if (texture == null || texture.width != size)
        {
            if (texture != null) Destroy(texture);
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            pixels = new Color32[size * size];
            var renderer = GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.zero, 1f);
            var floor = arena.floorMap.GetComponent<TilemapRenderer>();
            if (floor != null)
            {
                renderer.sortingLayerID = floor.sortingLayerID;
                renderer.sortingOrder = floor.sortingOrder + 1; // over the floor, under the terrain
            }
            sortingLayer = renderer.sortingLayerID;
            sortingOrder = renderer.sortingOrder;
        }
        Vector2 origin = arena.CentreOf(0, 0), step = arena.CentreOf(1, 1) - origin;
        transform.position = origin - step / 2f;
        transform.localScale = new Vector3(step.x, step.y, 1f);
        dirty = true;
    }

    // ---- State ----------------------------------------------------------------------------------------------

    bool Inside(int x, int y) => x >= 0 && y >= 0 && x < size && y < size;

    ObstacleDef KindAt(int x, int y) => Inside(x, y) ? kind[y * size + x] : null;

    /// Obstacle on a tile, or null.
    public static ObstacleDef At(int x, int y) => Current != null && Current.kind != null ? Current.KindAt(x, y) : null;

    public static ObstacleDef At(Vector2 world)
    {
        if (Current == null || Current.grid == null) return null;
        var cell = Current.grid.CellOf(world);
        return Current.KindAt(cell.x, cell.y);
    }

    void Set(int x, int y, ObstacleDef def, float duration = 0f)
    {
        int i = y * size + x;
        var old = kind[i];
        if (old == def) return;
        if (duration > 0f)
        {
            if (revertAt[i] <= 0f) revertTo[i] = old; // a temporary change on top of another keeps the first origin
            revertAt[i] = Time.time + duration;
        }
        else
        {
            revertAt[i] = 0f;
            revertTo[i] = null;
        }
        // A hand-drawn obstacle laid over a ROM-drawn one (a bridge over the void) keeps it drawn underneath.
        if (def == null || !def.overlay) under[i] = null;
        else if (old != null && old.tileset >= 0) under[i] = old;
        if (def != null && def.decayTime > 0f && duration <= 0f)
        {
            revertAt[i] = Time.time + def.decayTime; // turns into the next stage by itself
            revertTo[i] = Def(def.decaysTo);
        }
        kind[i] = def;
        if (old == null && def != null) count++;
        else if (old != null && def == null) count--;
        UpdateSolid(x, y, def);
        dirty = true;
    }

    // A solid tile (ice wall) gets a collider: it stops Pokémon and shots like a wall.
    void UpdateSolid(int x, int y, ObstacleDef def)
    {
        int i = y * size + x;
        bool solid = def != null && def.solid;
        if (solids.TryGetValue(i, out var go))
        {
            if (solid) return;
            if (go != null) Destroy(go);
            solids.Remove(i);
            return;
        }
        if (!solid) return;
        go = new GameObject("Bloc " + def.name);
        go.transform.SetParent(transform, false);
        go.transform.position = grid.CentreOf(x, y);
        go.transform.localScale = Vector3.one;
        var box = go.AddComponent<BoxCollider2D>();
        box.size = grid.CentreOf(1, 1) - grid.CentreOf(0, 0);
        solids[i] = go;
        foreach (var c in passers) LetThrough(c, box, def);
    }

    // The Pokémon that walk through some solid obstacles (Spectre): their colliders ignore those obstacles' (shots
    // still hit them). New Pokémon are handled as they appear (FixedUpdate).
    readonly HashSet<Combatant> passers = new();
    readonly HashSet<Combatant> seen = new();

    void LetThrough(Combatant c, BoxCollider2D box, ObstacleDef def)
    {
        if (c == null || box == null || !CanPass(def, c.Stats)) return;
        foreach (var own in c.GetComponents<Collider2D>()) Physics2D.IgnoreCollision(own, box);
    }

    void Meet(Combatant c)
    {
        if (!seen.Add(c)) return;
        passers.Add(c); // checked against each solid's own rule
        foreach (var kv in solids)
            if (kv.Value != null) LetThrough(c, kv.Value.GetComponent<BoxCollider2D>(), kind[kv.Key]);
    }

    // ---- Who goes where -------------------------------------------------------------------------------------

    /// Crosses that obstacle: by its ROM movement type, or by one of its Pokémon types.
    static bool CanPass(ObstacleDef def, PokemonStats stats) =>
        stats != null && (Array.IndexOf(def.passTypes, stats.movementType) >= 0 || HasType(stats, def.passPokemonTypes));

    /// The toll this Pokémon pays over that obstacle: only when a tolled type is its only way across (a Feu/Vol
    /// crosses lava as a Feu, for free).
    static Toll TollFor(ObstacleDef def, PokemonStats stats)
    {
        if (stats?.types == null || def.toll.Length == 0 || Array.IndexOf(def.passTypes, stats.movementType) >= 0) return null;
        Toll found = null;
        foreach (int type in stats.types)
        {
            if (Array.IndexOf(def.passPokemonTypes, type) < 0) continue;
            var t = Array.Find(def.toll, x => x.type == type);
            if (t == null) return null; // crosses freely by another type
            found = t;
        }
        return found;
    }

    /// This Pokémon cannot be on that tile: a solid one, or one it would fall into.
    public static bool Blocks(int x, int y, Combatant c) => WouldBlock(At(x, y), c);

    /// That obstacle would stop this Pokémon (null: no obstacle, nothing stops it).
    public static bool WouldBlock(ObstacleDef def, Combatant c) =>
        def != null && (def.solid || def.fall) && (c == null || !CanPass(def, c.Stats));

    /// What a move of this type does to that obstacle on purpose (no paint, no special action), or null.
    public static ObstacleInteraction TurnedBy(int moveType, ObstacleDef on)
    {
        if (on == null || interactions == null) return null;
        foreach (var it in interactions)
            if (it.byType == moveType && it.on == on.key && string.IsNullOrEmpty(it.paintTerrain) && string.IsNullOrEmpty(it.action))
                return it;
        return null;
    }

    /// The obstacle left by that interaction (null: bare floor).
    public static ObstacleDef Result(ObstacleInteraction it) =>
        !string.IsNullOrEmpty(it.becomes) ? Def(it.becomes) : !string.IsNullOrEmpty(it.leaves) ? Def(it.leaves) : null;

    /// Lays an obstacle on a tile by hand (the AI test bench); key null or empty clears it.
    public static void Place(int x, int y, string key)
    {
        if (Current == null || x < 0 || y < 0 || x >= Current.size || y >= Current.size) return;
        Current.Set(x, y, string.IsNullOrEmpty(key) ? null : Def(key));
    }

    public static bool BlocksAt(Vector2 world, Combatant c)
    {
        if (Current == null || Current.grid == null) return false;
        var cell = Current.grid.CellOf(world);
        return Blocks(cell.x, cell.y, c);
    }

    /// Terrain may not lie there (deep water, lava, void, walls), except on obstacles that hold it.
    public static bool RejectsTerrain(int x, int y)
    {
        var def = At(x, y);
        return def != null && (def.solid || (def.fall && !def.holdsTerrain));
    }

    /// A walking velocity stopped at the edge of what this Pokémon cannot enter (each axis on its own, so it
    /// slides along the bank).
    public static Vector2 Clamp(Combatant c, Vector2 position, Vector2 velocity)
    {
        if (Current == null || Current.count <= 0 || velocity == Vector2.zero) return velocity;
        if (velocity.x != 0f && BlocksAt(position + new Vector2(Mathf.Sign(velocity.x) * EdgeLook, 0f), c)) velocity.x = 0f;
        if (velocity.y != 0f && BlocksAt(position + new Vector2(0f, Mathf.Sign(velocity.y) * EdgeLook), c)) velocity.y = 0f;
        return velocity;
    }

    /// That obstacle leaves this Pokémon alone (its type, or how it moves): no slowing, sliding or sinking.
    static bool Easy(ObstacleDef def, PokemonStats stats) =>
        stats != null && (HasType(stats, def.easyTypes) || Array.IndexOf(def.easyMovementTypes, stats.movementType) >= 0);

    public static float SpeedMul(Combatant c) => At(c.transform.position) is { } d && !Easy(d, c.Stats) ? d.speedMul : 1f;

    public static float SlideAccel(Combatant c) => At(c.transform.position) is { } d && !Easy(d, c.Stats) ? d.slideAccel : 0f;

    /// Extra path cost of a tile for an AI: what it would pay there, or how much it would hinder it.
    public static float PathCost(int x, int y, Combatant agent)
    {
        var def = At(x, y);
        if (def == null || agent == null) return 0f;
        float cost = TollFor(def, agent.Stats) is { } t ? 8f + 100f * t.damageShare : 0f;
        if (def.navCost > 0f && !Easy(def, agent.Stats)) cost += def.navCost;
        return cost;
    }

    readonly Dictionary<Combatant, float> sunk = new(); // seconds spent in quicksand

    // Standing too long in quicksand: stuck for a while (the trap status), then it starts over.
    void Sink(Combatant c, Vector2 position)
    {
        var def = At(position);
        if (def == null || def.sinkTime <= 0f || Easy(def, c.Stats) || string.IsNullOrEmpty(def.sinkStatus))
        {
            sunk.Remove(c);
            return;
        }
        if (c.HasStatus(def.sinkStatus)) return;
        sunk.TryGetValue(c, out float t);
        t += Time.fixedDeltaTime;
        if (t >= def.sinkTime)
        {
            if (StatusLibrary.Get(def.sinkStatus) != null) c.AddStatus(def.sinkStatus, def.sinkDuration);
            t = 0f;
        }
        sunk[c] = t;
    }

    // ---- Moves changing obstacles ---------------------------------------------------------------------------

    /// A move hits around a point (impact, burst, strike): radius < 0 uses each interaction's own.
    public static void OnHit(MoveDef move, Vector2 at, float radius, Combatant caster)
    {
        if (Current == null || Current.count <= 0 || move == null) return;
        Current.Touch(move, at, at, radius, false, caster);
    }

    /// A projectile flies from one point to another: only the interactions marked "path" apply.
    public static void OnPath(MoveDef move, Vector2 from, Vector2 to, Combatant caster)
    {
        if (Current == null || Current.count <= 0 || move == null) return;
        Current.Touch(move, from, to, -1f, true, caster);
    }

    // Every interaction sees the tiles as they were before the hit: one blow cracks a big boulder, it does not
    // crack it then break it at once.
    void Touch(MoveDef move, Vector2 from, Vector2 to, float radius, bool path, Combatant caster)
    {
        var found = new List<(ObstacleInteraction, ObstacleDef, List<(int, int)>)>();
        foreach (var it in interactions)
        {
            if (it.byType < 0 || it.byType != move.type || (path && !it.path)) continue;
            var on = Def(it.on);
            if (on == null) continue;
            float r = radius > 0f ? radius : it.radius;
            var hit = Cells(from, to, r, on);
            if (hit.Count > 0) found.Add((it, on, hit));
        }
        foreach (var (it, on, hit) in found) Apply(it, on, hit, caster);
    }

    /// Terrain was just laid on a tile (Terrain calls it): it acts on the obstacle of that tile and of the 4 next
    /// to it (lightning on the bank electrifies the water, embers set a bridge on fire).
    public static void OnTerrain(int x, int y, string terrain, Combatant caster)
    {
        // Terrain painted by an interaction (poison spreading over the water) does not trigger another one.
        if (Current == null || Current.count <= 0 || string.IsNullOrEmpty(terrain) || applying) return;
        applying = true;
        try
        {
            var found = new List<(ObstacleInteraction, ObstacleDef, List<(int, int)>)>();
            foreach (var it in interactions)
            {
                if (it.byTerrain != terrain) continue;
                var on = Def(it.on);
                if (on == null) continue;
                var hit = new List<(int, int)>();
                foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (Current.KindAt(x + dx, y + dy) == on) hit.Add((x + dx, y + dy));
                if (hit.Count > 0) found.Add((it, on, hit));
            }
            foreach (var (it, on, hit) in found) Current.Apply(it, on, hit, caster); // as the tiles were before
        }
        finally
        {
            applying = false;
        }
    }

    static bool applying;

    void Apply(ObstacleInteraction it, ObstacleDef on, List<(int, int)> hit, Combatant caster)
    {
        if (!string.IsNullOrEmpty(it.action))
        {
            foreach (var (x, y) in hit) Pylons.Act(x, y, it.action);
            return;
        }
        if (!string.IsNullOrEmpty(it.paintTerrain))
        {
            var target = string.IsNullOrEmpty(it.floodKind) ? Flood(hit, on) : FloodNext(hit, Def(it.floodKind));
            Terrain.PaintCells(target, it.paintTerrain, it.duration, caster);
            return;
        }
        var becomes = Def(it.becomes);
        foreach (var (x, y) in hit)
        {
            if (KindAt(x, y) != on) continue;
            Set(x, y, becomes, becomes != null ? it.duration : 0f);
            if (becomes == null && !string.IsNullOrEmpty(it.leaves)) Terrain.PaintArea(it.leaves, grid.CentreOf(x, y), 0.4f, 4f, caster);
        }
        if (it.spread) Spread(hit, on, it, becomes);
    }

    struct Pending { public float at; public int cell; public ObstacleDef from, to; public float duration; }
    readonly List<Pending> pending = new();

    // The change runs along the connected tiles of "on" (and of spreadOver), farther tiles later.
    void Spread(List<(int, int)> start, ObstacleDef on, ObstacleInteraction it, ObstacleDef becomes)
    {
        var kinds = new HashSet<ObstacleDef> { on };
        foreach (var k in it.spreadOver)
            if (Def(k) is { } d) kinds.Add(d);
        var seen = new HashSet<int>();
        var queue = new Queue<(int x, int y, int steps)>();
        foreach (var (x, y) in start)
            if (seen.Add(y * size + x)) queue.Enqueue((x, y, 0));
        while (queue.Count > 0 && seen.Count < FloodLimit)
        {
            var (x, y, steps) = queue.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                var there = KindAt(nx, ny);
                if (there == null || !kinds.Contains(there) || !seen.Add(ny * size + nx)) continue;
                pending.Add(new Pending { at = Time.time + (steps + 1) * it.spreadDelay, cell = ny * size + nx, from = there, to = becomes, duration = it.duration });
                queue.Enqueue((nx, ny, steps + 1));
            }
        }
    }

    // Tiles of obstacle "on" whose centre lies within r of the segment.
    List<(int, int)> Cells(Vector2 from, Vector2 to, float r, ObstacleDef on)
    {
        var result = new List<(int, int)>();
        var a = grid.CellOf(from);
        var b = grid.CellOf(to);
        int pad = Mathf.CeilToInt(r) + 1;
        for (int x = Mathf.Min(a.x, b.x) - pad; x <= Mathf.Max(a.x, b.x) + pad; x++)
            for (int y = Mathf.Min(a.y, b.y) - pad; y <= Mathf.Max(a.y, b.y) + pad; y++)
                if (KindAt(x, y) == on && DistanceToSegment(grid.CentreOf(x, y), from, to) <= r + 0.5f)
                    result.Add((x, y));
        return result;
    }

    // Every tile of the same obstacle connected to the given ones.
    List<(int, int)> Flood(List<(int, int)> start, ObstacleDef on)
    {
        var seen = new HashSet<int>();
        var queue = new Queue<(int, int)>();
        foreach (var c in start)
            if (seen.Add(c.Item2 * size + c.Item1)) queue.Enqueue(c);
        var result = new List<(int, int)>();
        while (queue.Count > 0 && result.Count < FloodLimit)
        {
            var (x, y) = queue.Dequeue();
            result.Add((x, y));
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = x + dx, ny = y + dy;
                if (KindAt(nx, ny) == on && seen.Add(ny * size + nx)) queue.Enqueue((nx, ny));
            }
        }
        return result;
    }

    // Every tile of "kind" connected to the ones next to the given tiles.
    List<(int, int)> FloodNext(List<(int, int)> from, ObstacleDef kind)
    {
        var start = new List<(int, int)>();
        if (kind == null) return start;
        foreach (var (x, y) in from)
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (KindAt(x + dx, y + dy) == kind) start.Add((x + dx, y + dy));
        return start.Count > 0 ? Flood(start, kind) : start;
    }

    /// Draws tile (x, y) with another sheet (null: back to its own), e.g. a pylon overcharged or grounded.
    public static void OverrideSheet(int x, int y, EffectLibrary.Sheet sheet)
    {
        if (Current == null || Current.kind == null || !Current.Inside(x, y)) return;
        int i = y * Current.size + x;
        bool had = Current.sheetOverrides.TryGetValue(i, out var old);
        if (sheet == null && !had || had && old == sheet) return;
        if (sheet == null) Current.sheetOverrides.Remove(i);
        else Current.sheetOverrides[i] = sheet;
        Current.dirty = true;
    }

    public static int FrameOf(EffectLibrary.Sheet sheet) => FrameAt(sheet);

    /// Every tile of the obstacle of tile (x, y) connected to it (an arc touching water or metal electrifies it all).
    public static List<(int, int)> FloodFrom(int x, int y)
    {
        var def = At(x, y);
        return def != null ? Current.Flood(new List<(int, int)> { (x, y) }, def) : new List<(int, int)>();
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    // ---- Falls, reverting, drawing --------------------------------------------------------------------------

    void FixedUpdate()
    {
        if (kind == null) return;
        for (int k = 0; k < pending.Count; k++)
        {
            var p = pending[k];
            if (Time.time < p.at) continue;
            pending.RemoveAt(k--);
            if (kind[p.cell] == p.from) Set(p.cell % size, p.cell / size, p.to, p.to != null ? p.duration : 0f);
        }
        for (int i = 0; i < kind.Length; i++)
            if (revertAt[i] > 0f && Time.time >= revertAt[i])
            {
                var back = revertTo[i];
                revertAt[i] = 0f;
                revertTo[i] = null;
                Set(i % size, i / size, back);
            }
        if (count <= 0) return;
        foreach (var c in Combatant.All)
        {
            if (c == null || !c.IsAlive) continue;
            Meet(c);
            Vector2 position = c.transform.position;
            Bump(c, position);
            PayToll(c, position);
            Sink(c, position);
            if (!BlocksAt(position, c))
            {
                lastSafe[c] = position; // where it comes back after a fall
                continue;
            }
            if (fellAt.TryGetValue(c, out float at) && Time.time < at + FallCooldown) continue;
            Fall(c, At(position));
        }
    }

    readonly Dictionary<Combatant, float> tollAt = new();

    // Crossing by a tolled type (a Vol over lava): damage every second, and a status now and then.
    void PayToll(Combatant c, Vector2 position)
    {
        var def = At(position);
        var toll = def != null ? TollFor(def, c.Stats) : null;
        if (toll == null) { tollAt.Remove(c); return; }
        if (!string.IsNullOrEmpty(toll.status) && toll.statusChance > 0f && !c.HasStatus(toll.status)
            && UnityEngine.Random.value < 1f - Mathf.Pow(1f - toll.statusChance, Time.fixedDeltaTime))
        {
            var status = StatusLibrary.Get(toll.status);
            if (status != null) c.AddStatus(toll.status, status.duration);
        }
        if (!tollAt.TryGetValue(c, out float at)) tollAt[c] = at = Time.time + 1f;
        if (Time.time < at) return;
        tollAt[c] = Time.time + 1f;
        if (toll.damageShare > 0f) c.TakeFixedDamage(c.maxHp * toll.damageShare);
    }

    // Thrown against a solid obstacle — pushed by a hit, or sliding on a slippery ground — : stunned and hurt.
    // Walking into it, dodging or dashing on purpose never does.
    void Bump(Combatant c, Vector2 position)
    {
        bool pushed = c.IsKnockedBack;
        bool sliding = !c.IsLunging && !c.IsDodging && (Terrain.SlideAccel(c) > 0f || SlideAccel(c) > 0f);
        if (!pushed && !sliding) return;
        var body = c.GetComponent<Rigidbody2D>();
        if (body == null || body.linearVelocity.magnitude < BumpSpeed) return;
        var hit = At(position + body.linearVelocity.normalized * BumpReach);
        if (hit == null || !hit.solid || CanPass(hit, c.Stats) || (hit.bumpStun <= 0f && hit.bumpDamage <= 0f)) return;
        if (bumpedAt.TryGetValue(c, out float at) && Time.time < at + BumpCooldown) return;
        bumpedAt[c] = Time.time;
        c.Stun(hit.bumpStun);
        if (hit.bumpDamage > 0f) c.TakeFixedDamage(c.maxHp * hit.bumpDamage);
        body.linearVelocity = Vector2.zero;
    }

    static bool HasType(PokemonStats stats, int[] types)
    {
        if (stats?.types == null || types == null) return false;
        foreach (int type in stats.types)
            if (Array.IndexOf(types, type) >= 0) return true;
        return false;
    }

    /// Standing where this Pokémon is anchored (an Acier on metal): it cannot be pushed.
    public static bool Anchored(Combatant c) => At(c.transform.position) is { } def && HasType(c.Stats, def.anchorTypes);

    /// Divides the damage this Pokémon takes (an Acier anchored on metal resists better).
    public static float DefenseMul(Combatant c) => At(c.transform.position) is { } def && HasType(c.Stats, def.anchorTypes) ? def.anchorDefense : 1f;

    /// Pull of the magnets on this Pokémon (tiles/s), when they attract its type.
    public static Vector2 Pull(Combatant c)
    {
        if (Current == null || Current.count <= 0 || c.Stats == null) return Vector2.zero;
        Vector2 position = c.transform.position, pull = Vector2.zero;
        foreach (var (centre, def) in Current.Magnets(position, 5))
            if (HasType(c.Stats, def.attractTypes) && Vector2.Distance(centre, position) <= def.magnetRadius)
                pull += (centre - position).normalized * def.magnetPull;
        return pull;
    }

    /// A magnet catching a shot of a type it attracts that passes within its capture radius: where it is.
    public static bool Capture(MoveDef move, Vector2 at, out Vector2 magnet)
    {
        magnet = default;
        if (Current == null || Current.count <= 0 || move == null) return false;
        foreach (var (centre, def) in Current.Magnets(at, 2))
            if (Array.IndexOf(def.attractTypes, move.type) >= 0 && Vector2.Distance(centre, at) <= def.captureRadius)
            {
                magnet = centre;
                return true;
            }
        return false;
    }

    IEnumerable<(Vector2, ObstacleDef)> Magnets(Vector2 around, int reach)
    {
        var cell = grid.CellOf(around);
        for (int x = cell.x - reach; x <= cell.x + reach; x++)
            for (int y = cell.y - reach; y <= cell.y + reach; y++)
                if (KindAt(x, y) is { magnetRadius: > 0f } def) yield return (grid.CentreOf(x, y), def);
    }

    // Back on solid ground with the obstacle's toll: knocked out there if the fall takes its last HP, otherwise
    // briefly protected so it is not finished off as it climbs out.
    void Fall(Combatant c, ObstacleDef def)
    {
        fellAt[c] = Time.time;
        Vector2 back = SafeSpot(c, lastSafe.TryGetValue(c, out var safe) ? safe : (Vector2)c.transform.position);
        var body = c.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = back;
            body.linearVelocity = Vector2.zero;
        }
        c.transform.position = back;
        lastSafe[c] = back;
        if (def == null) return;
        if (def.fallDamage > 0f) c.TakeFixedDamage(c.maxHp * def.fallDamage);
        if (!c.IsAlive) return;
        if (!string.IsNullOrEmpty(def.fallStatus))
        {
            var status = StatusLibrary.Get(def.fallStatus);
            if (status != null) c.AddStatus(def.fallStatus, status.duration);
        }
        c.Protect(FallProtection);
    }

    // The remembered spot if it is still solid ground for this Pokémon (the ice it stood on may have melted since),
    // otherwise the nearest tile that is.
    Vector2 SafeSpot(Combatant c, Vector2 wanted)
    {
        var start = grid.CellOf(wanted);
        if (grid.Walkable(start.x, start.y) && !Blocks(start.x, start.y, c)) return wanted;
        var seen = new HashSet<int> { start.y * size + start.x };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (grid.Walkable(p.x, p.y) && !Blocks(p.x, p.y, c)) return grid.CentreOf(p.x, p.y);
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = p.x + dx, ny = p.y + dy;
                if (!Inside(nx, ny) || Mathf.Abs(nx - start.x) > SafeSearch || Mathf.Abs(ny - start.y) > SafeSearch) continue;
                if (seen.Add(ny * size + nx)) queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
        return grid.CentreOf(size / 2, size / 2);
    }

    void LateUpdate()
    {
        if (texture == null) return;
        if (dirty)
        {
            dirty = false;
            animated.Clear();
            for (int i = 0; i < kind.Length; i++) pixels[i] = Draw(i) ? default : kind[i] != null ? kind[i].tint : default;
            texture.SetPixels32(pixels);
            texture.Apply();
        }
        foreach (int i in animated)
        {
            var sheet = sheetOverrides.TryGetValue(i, out var other) ? other : kind[i]?.sheet;
            if (topTiles[i] != null && sheet != null) topTiles[i].sprite = sheet.Frame(FrameAt(sheet));
        }
    }

    // Draws tile i with sprites (ROM layer underneath, hand-made sheet on top); false when it has neither and the
    // flat colour of the texture stands in.
    bool Draw(int i)
    {
        var def = kind[i];
        var floorKind = def == null ? null : def.tileset >= 0 ? def : under[i];
        var rom = floorKind == null ? null
            : decorTiles != null && floorKind.key == "water" ? decorTiles : RomObstacleTiles.Get(floorKind.tileset);
        var sheet = sheetOverrides.TryGetValue(i, out var other) && def != null ? other : def?.sheet;
        int x = i % size, y = i / size;
        // Without its own look yet, a flat stand-in drawn on top (planks for a bridge, so it shows over the void).
        Sprite rock = def != null && sheet == null && def.romRocks.Length > 0
            ? RockSprite(def.romRocks[(x * 7 + y * 13) % def.romRocks.Length].tileset) : null;
        Sprite flat = def != null && sheet == null && rock == null && def.tileset < 0 ? FlatSprite(def) : null;
        Show(baseTiles, i, rom != null ? rom.Tile(Mask(x, y, floorKind), (x * 7 + y * 13) % 8 == 0 ? 1 + (x + y) % 2 : 0) : null, 0, 0f);
        float angle = def != null && def.Bridge && !SpansNorthSouth(x, y) ? 90f : 0f;
        Show(topTiles, i, sheet != null ? sheet.Frame(FrameAt(sheet)) : rock != null ? rock : flat, 1, angle, def?.spriteColor ?? Color.white);
        if (sheet != null && sheet.frame_count > 1) animated.Add(i);
        return def != null && (rom != null || sheet != null || rock != null || flat != null);
    }

    static readonly Dictionary<int, Sprite> rocks = new();

    // The ROM rock (lone wall chunk) extracted by Tools/setup.py; null when missing.
    static Sprite RockSprite(int tileset)
    {
        if (rocks.TryGetValue(tileset, out var sprite) && sprite != null) return sprite;
        var texture = Resources.Load<Texture2D>($"ObstacleTiles/rock_{tileset}");
        sprite = texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width, 0, SpriteMeshType.FullRect) : null;
        rocks[tileset] = sprite;
        return sprite;
    }

    void Show(SpriteRenderer[] layer, int i, Sprite sprite, int above, float angle, Color? color = null)
    {
        var tile = layer[i];
        if (sprite == null)
        {
            if (tile != null) tile.enabled = false;
            return;
        }
        if (tile == null)
        {
            if (tileRoot == null) tileRoot = new GameObject("Tuiles d'obstacles").transform;
            var go = new GameObject("Tuile");
            go.transform.SetParent(tileRoot, false);
            go.transform.position = grid.CentreOf(i % size, i / size);
            Vector2 step = grid.CentreOf(1, 1) - grid.CentreOf(0, 0);
            go.transform.localScale = new Vector3(step.x, step.y, 1f);
            layer[i] = tile = go.AddComponent<SpriteRenderer>();
            tile.sortingLayerID = sortingLayer;
            tile.sortingOrder = sortingOrder + above;
        }
        tile.sprite = sprite;
        tile.color = color ?? Color.white;
        tile.transform.rotation = Quaternion.Euler(0f, 0f, angle);
        tile.enabled = true;
    }

    static readonly Vector2Int[] Neighbours =
    {
        new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1),
    };

    // PMD bank mask: neighbours drawn with the same ROM layer (a bridge counts as what it spans), and the walls
    // around the arena, so a band reaching them has no bank there.
    int Mask(int x, int y, ObstacleDef floorKind)
    {
        int mask = 0;
        for (int n = 0; n < Neighbours.Length; n++)
        {
            int nx = x + Neighbours[n].x, ny = y + Neighbours[n].y;
            bool same = !grid.Walkable(nx, ny) || (Inside(nx, ny) && (kind[ny * size + nx] == floorKind || under[ny * size + nx] == floorKind));
            if (same) mask |= 1 << n;
        }
        return mask;
    }

    // A bridge spans north-south when what it crosses lies east and west of it.
    bool SpansNorthSouth(int x, int y)
    {
        bool Gap(int cx, int cy) => Inside(cx, cy) && (kind[cy * size + cx] is { fall: true } || under[cy * size + cx] != null && kind[cy * size + cx] is { } k && !k.Bridge);
        return Gap(x + 1, y) || Gap(x - 1, y);
    }

    static readonly Dictionary<ObstacleDef, Sprite> flats = new();

    // Provisional tile in the obstacle's colour (at flatAlpha, so what lies beneath shows): planks for a bridge, a
    // plain square with a darker edge for the rest.
    static Sprite FlatSprite(ObstacleDef def)
    {
        if (flats.TryGetValue(def, out var sprite) && sprite != null) return sprite;
        const int n = 24;
        var pixels = new Color32[n * n];
        Color light = def.tint, dark = Color.Lerp(def.tint, Color.black, 0.45f);
        light.a = dark.a = def.flatAlpha;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Color c;
                if (def.planks) c = y % 4 == 3 || x == 0 || x == n - 1 ? dark : light;
                else c = x == 0 || y == 0 || x == n - 1 || y == n - 1 ? dark : light;
                pixels[y * n + x] = c;
            }
        var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n, 0, SpriteMeshType.FullRect);
        flats[def] = sprite;
        return sprite;
    }

    static int FrameAt(EffectLibrary.Sheet sheet)
    {
        var durations = sheet.frame_duration_ms;
        if (sheet.frame_count <= 1 || durations == null || durations.Length == 0) return 0;
        int total = 0;
        for (int f = 0; f < sheet.frame_count; f++) total += durations[Mathf.Min(f, durations.Length - 1)];
        if (total <= 0) return 0;
        int t = (int)(Time.time * 1000f) % total;
        for (int f = 0; f < sheet.frame_count; f++)
        {
            t -= durations[Mathf.Min(f, durations.Length - 1)];
            if (t < 0) return f;
        }
        return sheet.frame_count - 1;
    }

    // ---- Generation (dev sandbox's obstacle room) ------------------------------------------------------------

    [Serializable]
    public class Feature
    {
        public string type; // chasm, river, lake, island, ice_walls
        public int weight = 1;
        public string kind;
        public int width = 2;
        public int[] bridges = { 1, 2 };
        public string bridgeKind = "bridge";
        public int[] sizes = { 2, 3, 4 };
        public int[] count = { 2, 4 };
        public float[] coverage = { 1f, 1f }; // band: share of the arena's width it spans (1: wall to wall)
        public float bigChance = 0.35f; // boulders: share of big ones (2 x 2, two blows)
        public int[] distance = { 4, 7 }; // pylons: tiles between the two of a pair
        public int[] magnets = { 1, 1 }; // metal: magnets on the edge of the plates
        public string[] near = new string[0]; // ice_patch: obstacles it lies against (void, water, boulders)
    }

    /// Two obstacles that never touch: at least "gap" tiles of plain floor between them (water and lava).
    [Serializable]
    public class Separation
    {
        public string a, b;
        public int gap = 1;
    }

    [Serializable]
    public class Room
    {
        public int[] features = { 2, 3 };
        public int spawnClear = 4; // tiles kept clear around the spawn point
        public int stairsClear = 2; // ... and around the stairs
        public Separation[] separate;
        public Feature[] pool;
        // Arena profiles (arena_obstacles.json): the ROM tilesets (décors) it is for, how many of the generator's
        // rectangular wall blocks are kept (-1: all), and whether the water is drawn with the décor's own.
        public int[] tilesets = new int[0];
        public int walls = -1;
        public bool decorWater;
    }

    [Serializable] class ArenaFile { public Room[] profiles; public Room fallback; }

    static ArenaFile arenaFile;

    /// Obstacle profile of a real run's arena for that décor (null: none, or obstacles switched off).
    public static Room ArenaProfile(int romTileset)
    {
        if (!SaveData.Settings.obstaclesInRuns) return null;
        if (arenaFile == null)
        {
            var asset = Resources.Load<TextAsset>("Data/arena_obstacles");
            arenaFile = asset != null ? JsonUtility.FromJson<ArenaFile>(asset.text) : new ArenaFile();
        }
        foreach (var p in arenaFile.profiles ?? new Room[0])
            if (Array.IndexOf(p.tilesets, romTileset) >= 0) return p;
        return arenaFile.fallback;
    }

    /// Lays the obstacles of a real run's arena (DungeonGenerator.Generate): drawn from the décor's profile,
    /// the team's spawn point kept clear. Then works out which tiles a walker reaches from it (enemy spawns).
    public void GenerateArena(Room profile, System.Random rng, Vector2Int spawn)
    {
        decorTiles = profile != null && profile.decorWater ? RomObstacleTiles.FromDecor(grid.tileset) : null;
        if (profile?.pool != null && profile.pool.Length > 0) Generate(profile, rng, new List<Vector2Int> { spawn });
        ComputeReach(spawn);
    }

    RomObstacleTiles decorTiles; // the arena's own water (its décor's secondary layer), drawn instead of the default
    bool[] reach; // tiles a standard walker reaches from the spawn point (null: everywhere)

    void ComputeReach(Vector2Int from)
    {
        reach = null;
        if (count <= 0 || !Inside(from.x, from.y)) return;
        reach = new bool[size * size];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        reach[from.y * size + from.x] = true;
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = p.x + dx, ny = p.y + dy;
                if (!Inside(nx, ny) || reach[ny * size + nx] || !grid.Walkable(nx, ny) || Blocks(nx, ny, null)) continue;
                reach[ny * size + nx] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
    }

    /// A tile where any Pokémon can stand and that a walker reaches from the spawn point (enemy spawns).
    public static bool Standable(int x, int y) =>
        Current == null || Current.kind == null || (At(x, y) == null && (Current.reach == null || Current.reach[y * Current.size + x]));

    Room room;
    List<Vector2Int> keepCells;
    readonly HashSet<int> reserved = new(); // floor that later features must leave alone (an island's land, bridge ends)

    void Reserve(int x, int y, int margin = 0)
    {
        for (int dx = -margin; dx <= margin; dx++)
            for (int dy = -margin; dy <= margin; dy++)
                if (Inside(x + dx, y + dy)) reserved.Add((y + dy) * size + x + dx);
    }

    /// Fills the arena with 2 or 3 features drawn from the room's pool (bands across the arena first), keeping
    /// clear tiles around "keep" (spawn point first, then stairs) and drawing again when a walker could not reach
    /// them all, or most of the floor. Returns the names drawn.
    public List<string> Generate(Room room, System.Random rng, List<Vector2Int> keep)
    {
        this.room = room;
        keepCells = keep;
        var names = new List<string>();
        for (int attempt = 0; attempt < 30; attempt++)
        {
            Clear();
            Pylons.Clear(this); // pairs of a discarded attempt would leave arcs between empty tiles
            reserved.Clear();
            names.Clear();
            int wanted = rng.Next(room.features[0], room.features[Mathf.Min(1, room.features.Length - 1)] + 1);
            var pool = new List<Feature>(room.pool);
            var drawn = new List<Feature>();
            for (int n = 0; n < wanted && pool.Count > 0; n++)
            {
                var f = Draw(pool, rng);
                pool.RemoveAll(p => p.type == f.type && p.kind == f.kind); // never twice the same
                drawn.Add(f);
            }
            // Bands first (they must run unbroken), ice patches last (they lie against what the others placed).
            drawn.Sort((x, y) => Order(x).CompareTo(Order(y)));
            foreach (var f in drawn)
                if (Place(f, rng)) names.Add(Describe(f));
            if (Connected(keep)) return names;
        }
        Clear();
        Pylons.Clear(this);
        names.Clear();
        return names;
    }

    static bool Band(Feature f) => f.type == "chasm" || f.type == "river";

    static int Order(Feature f) => Band(f) ? 0 : f.type == "ice_patch" ? 2 : 1;

    void Clear()
    {
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                if (kind[y * size + x] != null) Set(x, y, null);
    }

    static Feature Draw(List<Feature> pool, System.Random rng)
    {
        int total = 0;
        foreach (var f in pool) total += Mathf.Max(1, f.weight);
        int pick = rng.Next(total);
        foreach (var f in pool)
        {
            pick -= Mathf.Max(1, f.weight);
            if (pick < 0) return f;
        }
        return pool[0];
    }

    static string Describe(Feature f) => f.type switch
    {
        "chasm" => f.coverage != null && f.coverage.Length > 0 && f.coverage[0] < 1f ? "Ravin" : "Vide avec ponts",
        "river" => f.kind == "ice_floor" ? "Rivière glacée" : "Rivière",
        "island" => f.kind == "void" ? "Île au-dessus du vide" : "Île au milieu de l'eau",
        "lake" => f.kind switch { "lava" => "Lac de lave", "ice_floor" => "Lac de glace", "void" => "Gouffre", "quicksand" => "Sable mouvant", _ => "Lac" },
        "boulders" => "Rochers",
        "pylons" => "Pylônes électriques",
        "metal" => "Plaques de métal et borne aimantée",
        "ice_patch" => "Glace au bord du danger",
        _ => "Murs de glace",
    };

    static int Pick(int[] range, System.Random rng, int fallback) =>
        range != null && range.Length > 0 ? rng.Next(range[0], range[Mathf.Min(1, range.Length - 1)] + 1) : fallback;

    // A tile the feature may take: on the floor, inside the arena (its outermost ring too for "edges"), clear of
    // the spawn point and the stairs, and far enough from the obstacles it must not touch.
    bool Free(int x, int y, ObstacleDef placing, bool edges = false)
    {
        var interior = grid.Interior;
        int m = edges ? 0 : 1;
        if (x < interior.xMin + m || y < interior.yMin + m || x >= interior.xMax - m || y >= interior.yMax - m) return false;
        if (!grid.Walkable(x, y) || KindAt(x, y) != null || reserved.Contains(y * size + x)) return false;
        for (int k = 0; k < keepCells.Count; k++)
        {
            int clear = k == 0 ? room.spawnClear : room.stairsClear;
            if (Mathf.Abs(keepCells[k].x - x) <= clear && Mathf.Abs(keepCells[k].y - y) <= clear) return false;
        }
        return Separated(x, y, placing);
    }

    bool Separated(int x, int y, ObstacleDef placing)
    {
        if (room.separate == null || placing == null) return true;
        foreach (var s in room.separate)
        {
            string other = s.a == placing.key ? s.b : s.b == placing.key ? s.a : null;
            if (other == null) continue;
            for (int dx = -s.gap; dx <= s.gap; dx++)
                for (int dy = -s.gap; dy <= s.gap; dy++)
                    if (KindAt(x + dx, y + dy) is { } near && near.key == other) return false;
        }
        return true;
    }

    bool Place(Feature f, System.Random rng)
    {
        return f.type switch
        {
            "chasm" or "river" => PlaceBand(f, rng),
            "lake" => PlaceLake(f, rng),
            "island" => PlaceIsland(f, rng),
            "boulders" => PlaceBoulders(f, rng),
            "pylons" => PlacePylons(f, rng),
            "metal" => PlaceMetal(f, rng),
            "ice_patch" => PlaceIcePatch(f, rng),
            _ => PlaceIceWalls(f, rng),
        };
    }

    // A band across the arena (wall to wall, or only part of the way: "coverage"), wandering by one tile now and
    // then but never near the spawn point or the stairs, with straight bridges where it runs straight for two tiles.
    bool PlaceBand(Feature f, System.Random rng)
    {
        var band = Def(f.kind);
        if (band == null) return false;
        var interior = grid.Interior;
        bool horizontal = rng.Next(2) == 0;
        int length = horizontal ? interior.width : interior.height;
        int start = horizontal ? interior.xMin : interior.yMin;
        int across0 = horizontal ? interior.yMin : interior.xMin, across1 = horizontal ? interior.yMax : interior.xMax;
        // Offsets (first row or column of the band) that stay clear of every kept tile.
        var allowed = new List<int>();
        for (int o = across0 + 3; o <= across1 - 3 - f.width; o++)
        {
            bool ok = true;
            for (int k = 0; k < keepCells.Count && ok; k++)
            {
                int clear = (k == 0 ? room.spawnClear : room.stairsClear) + 1;
                int at = horizontal ? keepCells[k].y : keepCells[k].x;
                ok = o + f.width - 1 < at - clear || o > at + clear;
            }
            if (ok) allowed.Add(o);
        }
        if (allowed.Count == 0) return false;
        int offset = allowed[rng.Next(allowed.Count)];
        // The stretch it covers: all of it, or a part touching one wall, or floating in the middle.
        float lo = f.coverage != null && f.coverage.Length > 0 ? f.coverage[0] : 1f;
        float hi = f.coverage != null && f.coverage.Length > 1 ? f.coverage[1] : lo;
        int covered = Mathf.Clamp(Mathf.RoundToInt(length * Mathf.Lerp(lo, hi, (float)rng.NextDouble())), 3, length);
        int from = covered >= length ? 0 : rng.Next(3) switch { 0 => 0, 1 => length - covered, _ => rng.Next(0, length - covered + 1) };
        int to = from + covered;
        var columns = new int[length];
        for (int s = 0; s < length; s++)
        {
            if (s > 0 && rng.Next(4) == 0)
            {
                int next = offset + (rng.Next(2) == 0 ? -1 : 1);
                if (allowed.Contains(next)) offset = next;
            }
            columns[s] = offset;
            if (s < from || s >= to) continue;
            for (int w = 0; w < f.width; w++)
            {
                int x = horizontal ? start + s : offset + w, y = horizontal ? offset + w : start + s;
                if (Free(x, y, band, true)) Set(x, y, band);
            }
        }
        // Bridges: on straight stretches, spread along the band and apart from each other.
        int bridges = Pick(f.bridges, rng, 0);
        var bridge = Def(f.bridgeKind);
        if (bridges <= 0 || bridge == null) return true;
        var spots = new List<int>();
        for (int s = Mathf.Max(2, from + 1); s < Mathf.Min(length - 3, to - 2); s++)
            if (columns[s] == columns[s + 1]) spots.Add(s);
        var chosen = new List<int>();
        for (int b = 0; b < bridges && spots.Count > 0; b++)
        {
            int target = (b + 1) * length / (bridges + 1);
            int best = -1;
            foreach (int s in spots)
                if (!chosen.Exists(c => Mathf.Abs(c - s) < 4) && (best < 0 || Mathf.Abs(s - target) < Mathf.Abs(best - target))) best = s;
            if (best < 0) break;
            chosen.Add(best);
        }
        foreach (int s in chosen)
            for (int t = s; t < s + 2; t++)
            {
                for (int w = 0; w < f.width; w++)
                {
                    int x = horizontal ? start + t : columns[t] + w, y = horizontal ? columns[t] + w : start + t;
                    if (KindAt(x, y) == band) Set(x, y, bridge);
                }
                // Both ends of the bridge stay plain floor, so nothing placed later blocks the way off it.
                for (int w = -2; w <= f.width + 1; w += f.width + 3)
                {
                    int x = horizontal ? start + t : columns[t] + w, y = horizontal ? columns[t] + w : start + t;
                    Reserve(x, y, 1);
                }
            }
        return true;
    }

    // An organic blob: a few overlapping discs, away from the kept tiles.
    bool PlaceLake(Feature f, System.Random rng)
    {
        var lake = Def(f.kind);
        if (lake == null) return false;
        var interior = grid.Interior;
        int r = f.sizes != null && f.sizes.Length > 0 ? f.sizes[rng.Next(f.sizes.Length)] : 3;
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int cx = rng.Next(interior.xMin + r + 2, interior.xMax - r - 2), cy = rng.Next(interior.yMin + r + 2, interior.yMax - r - 2);
            if (!FarFromKept(cx, cy, r)) continue;
            var discs = new List<(float, float, float)> { (cx, cy, r) };
            for (int d = 0; d < 3; d++) discs.Add((cx + rng.Next(-r, r + 1) * 0.6f, cy + rng.Next(-r, r + 1) * 0.6f, r * 0.6f + rng.Next(0, 2)));
            int placed = 0;
            for (int x = cx - 2 * r; x <= cx + 2 * r; x++)
                for (int y = cy - 2 * r; y <= cy + 2 * r; y++)
                {
                    bool inside = false;
                    foreach (var (dx, dy, dr) in discs)
                        if ((x - dx) * (x - dx) + (y - dy) * (y - dy) <= dr * dr) inside = true;
                    if (inside && Free(x, y, lake)) { Set(x, y, lake); placed++; }
                }
            if (placed > 0) return true;
        }
        return false;
    }

    bool FarFromKept(int cx, int cy, int reach)
    {
        for (int k = 0; k < keepCells.Count; k++)
            if (Vector2.Distance(keepCells[k], new Vector2(cx, cy)) < reach + (k == 0 ? room.spawnClear : room.stairsClear) + 2) return false;
        return true;
    }

    // Land of radius "size" ringed by "width" tiles of void or water, reached by straight bridges.
    bool PlaceIsland(Feature f, System.Random rng)
    {
        var ring = Def(f.kind);
        var bridge = Def(f.bridgeKind);
        if (ring == null) return false;
        var interior = grid.Interior;
        int r = f.sizes != null && f.sizes.Length > 0 ? f.sizes[rng.Next(f.sizes.Length)] : 2;
        int outer = r + f.width;
        for (int attempt = 0; attempt < 150; attempt++) // it needs a wide free area
        {
            int cx = rng.Next(interior.xMin + outer + 2, interior.xMax - outer - 2), cy = rng.Next(interior.yMin + outer + 2, interior.yMax - outer - 2);
            if (!FarFromKept(cx, cy, outer)) continue;
            bool fits = true; // the whole ring must fit, or there would be a dry way in
            for (int x = cx - outer; x <= cx + outer && fits; x++)
                for (int y = cy - outer; y <= cy + outer && fits; y++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > r + 0.5f && d <= outer + 0.5f && !Free(x, y, ring)) fits = false;
                    else if (d <= r + 0.5f && (KindAt(x, y) != null || !grid.Walkable(x, y))) fits = false; // land, not lava
                }
            if (!fits) continue;
            for (int x = cx - outer; x <= cx + outer; x++)
                for (int y = cy - outer; y <= cy + outer; y++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > r + 0.5f && d <= outer + 0.5f) Set(x, y, ring);
                    else if (d <= r + 0.5f) Reserve(x, y); // the island's land stays land
                }
            // Bridges along the axes, two tiles wide, from the island to the shore.
            var axes = new List<Vector2Int> { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            int bridges = Mathf.Clamp(Pick(f.bridges, rng, 2), 1, 4);
            for (int b = 0; b < bridges && bridge != null; b++)
            {
                var axis = axes[rng.Next(axes.Count)];
                axes.Remove(axis);
                var side = new Vector2Int(axis.y, axis.x); // perpendicular, for the second tile of width
                for (int step = 1; step <= outer + 1; step++)
                    for (int w = 0; w < 2; w++)
                    {
                        int x = cx + axis.x * step + side.x * w, y = cy + axis.y * step + side.y * w;
                        if (KindAt(x, y) == ring) Set(x, y, bridge);
                    }
                // The shore where the bridge lands stays floor too.
                Reserve(cx + axis.x * (outer + 2), cy + axis.y * (outer + 2), 1);
            }
            return true;
        }
        return false;
    }

    // Lone boulders (1 tile) and big ones (2 x 2), apart from each other.
    bool PlaceBoulders(Feature f, System.Random rng)
    {
        var small = Def("boulder");
        var big = Def("boulder_big");
        var interior = grid.Interior;
        int wanted = Pick(f.count, rng, 4), built = 0;
        for (int attempt = 0; attempt < 80 && built < wanted; attempt++)
        {
            bool large = big != null && rng.NextDouble() < f.bigChance;
            int s = large ? 2 : 1;
            int x0 = rng.Next(interior.xMin + 2, interior.xMax - 2 - s), y0 = rng.Next(interior.yMin + 2, interior.yMax - 2 - s);
            bool ok = true;
            for (int x = x0 - 1; x <= x0 + s && ok; x++) // a free ring around, so boulders never form walls
                for (int y = y0 - 1; y <= y0 + s && ok; y++)
                    ok = Free(x, y, small);
            if (!ok) continue;
            for (int x = x0; x < x0 + s; x++)
                for (int y = y0; y < y0 + s; y++)
                    Set(x, y, large ? big : small);
            built++;
        }
        return built > 0;
    }

    // Pairs of pylons, aligned, "distance" tiles apart; their arc must not cross the spawn point or the stairs.
    bool PlacePylons(Feature f, System.Random rng)
    {
        var pylon = Def("pylon");
        if (pylon == null) return false;
        var interior = grid.Interior;
        int wanted = Pick(f.count, rng, 1), built = 0;
        for (int attempt = 0; attempt < 80 && built < wanted; attempt++)
        {
            bool horizontal = rng.Next(2) == 0;
            int gap = Pick(f.distance, rng, 5);
            int x0 = rng.Next(interior.xMin + 2, interior.xMax - 2 - (horizontal ? gap : 0));
            int y0 = rng.Next(interior.yMin + 2, interior.yMax - 2 - (horizontal ? 0 : gap));
            var a = new Vector2Int(x0, y0);
            var b = horizontal ? new Vector2Int(x0 + gap, y0) : new Vector2Int(x0, y0 + gap);
            if (!Free(a.x, a.y, pylon) || !Free(b.x, b.y, pylon)) continue;
            bool clear = true;
            for (int t = 0; t <= gap && clear; t++)
            {
                var p = horizontal ? new Vector2Int(x0 + t, y0) : new Vector2Int(x0, y0 + t);
                for (int k = 0; k < keepCells.Count && clear; k++)
                    clear = Mathf.Abs(keepCells[k].x - p.x) > 2 || Mathf.Abs(keepCells[k].y - p.y) > 2;
            }
            if (!clear) continue;
            Set(a.x, a.y, pylon);
            Set(b.x, b.y, pylon);
            Pylons.AddPair(a, b);
            built++;
        }
        return built > 0;
    }

    // A patch of metal plates (an organic blob) with magnets on its edge.
    bool PlaceMetal(Feature f, System.Random rng)
    {
        var metal = Def("metal");
        var magnet = Def("magnet");
        if (metal == null) return false;
        var interior = grid.Interior;
        int r = f.sizes != null && f.sizes.Length > 0 ? f.sizes[rng.Next(f.sizes.Length)] : 2;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            int cx = rng.Next(interior.xMin + r + 3, interior.xMax - r - 3), cy = rng.Next(interior.yMin + r + 3, interior.yMax - r - 3);
            if (!FarFromKept(cx, cy, r)) continue;
            var plates = new List<Vector2Int>();
            for (int x = cx - r - 1; x <= cx + r + 1; x++)
                for (int y = cy - r - 1; y <= cy + r + 1; y++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) + (float)rng.NextDouble() * 0.8f;
                    if (d <= r + 0.5f && Free(x, y, metal)) plates.Add(new Vector2Int(x, y));
                }
            if (plates.Count < 4) continue;
            foreach (var p in plates) Set(p.x, p.y, metal);
            // Magnets: free tiles touching the plates.
            int magnets = Pick(f.magnets, rng, 1);
            for (int m = 0; m < magnets && magnet != null; m++)
                for (int tries = 0; tries < 30; tries++)
                {
                    var p = plates[rng.Next(plates.Count)];
                    var side = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }[rng.Next(4)];
                    var q = p + side;
                    if (!Free(q.x, q.y, magnet)) continue;
                    Set(q.x, q.y, magnet);
                    break;
                }
            return true;
        }
        return false;
    }

    // Patches of slippery ground against a danger (void, water, boulders): sliding, or being pushed, there sends
    // a Pokémon into it.
    bool PlaceIcePatch(Feature f, System.Random rng)
    {
        var ice = Def(f.kind ?? "ice_floor");
        if (ice == null || f.near.Length == 0) return false;
        var targets = new List<Vector2Int>();
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                if (KindAt(x, y) is { } k && Array.IndexOf(f.near, k.key) >= 0) targets.Add(new Vector2Int(x, y));
        if (targets.Count == 0) return false;
        int wanted = Pick(f.count, rng, 1), built = 0;
        for (int attempt = 0; attempt < 60 && built < wanted; attempt++)
        {
            var t = targets[rng.Next(targets.Count)];
            var centre = t + new Vector2Int(rng.Next(-2, 3), rng.Next(-2, 3));
            if (!Free(centre.x, centre.y, ice)) continue;
            int r = f.sizes != null && f.sizes.Length > 0 ? f.sizes[rng.Next(f.sizes.Length)] : 2;
            int placed = 0;
            for (int x = centre.x - r; x <= centre.x + r; x++)
                for (int y = centre.y - r; y <= centre.y + r; y++)
                {
                    float d = Mathf.Sqrt((x - centre.x) * (x - centre.x) + (y - centre.y) * (y - centre.y)) + (float)rng.NextDouble() * 0.7f;
                    if (d <= r + 0.3f && Free(x, y, ice)) { Set(x, y, ice); placed++; }
                }
            if (placed > 0) built++;
        }
        return built > 0;
    }

    // Short straight walls of ice, three tiles long.
    bool PlaceIceWalls(Feature f, System.Random rng)
    {
        var wall = Def("ice_wall");
        var interior = grid.Interior;
        int walls = Pick(f.count, rng, 3);
        int built = 0;
        for (int attempt = 0; attempt < 60 && built < walls; attempt++)
        {
            bool horizontal = rng.Next(2) == 0;
            int x0 = rng.Next(interior.xMin + 2, interior.xMax - 4), y0 = rng.Next(interior.yMin + 2, interior.yMax - 4);
            bool ok = true;
            for (int t = 0; t < 3 && ok; t++) ok = Free(horizontal ? x0 + t : x0, horizontal ? y0 : y0 + t, wall);
            if (!ok) continue;
            for (int t = 0; t < 3; t++) Set(horizontal ? x0 + t : x0, horizontal ? y0 : y0 + t, wall);
            built++;
        }
        return built > 0;
    }

    // A standard walker (movement type 0) can reach every kept tile, and most of the free floor, from the first.
    bool Connected(List<Vector2Int> keep)
    {
        if (keep.Count == 0) return true;
        var seen = new bool[size * size];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(keep[0]);
        seen[keep[0].y * size + keep[0].x] = true;
        int reached = 0;
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            reached++;
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = p.x + dx, ny = p.y + dy;
                if (!Inside(nx, ny) || seen[ny * size + nx] || !grid.Walkable(nx, ny)) continue;
                var def = KindAt(nx, ny);
                if (def != null && (def.solid || def.fall)) continue;
                seen[ny * size + nx] = true;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
        foreach (var k in keep)
            if (!seen[k.y * size + k.x]) return false;
        int open = 0;
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                var def = KindAt(x, y);
                if (grid.Walkable(x, y) && (def == null || (!def.solid && !def.fall))) open++;
            }
        return reached >= open * 0.85f;
    }
}
