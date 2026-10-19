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
    public float fallDamage; // share of max HP lost by a fall
    public string fallStatus; // status given by a fall (burn for lava)
    public float speedMul = 1f;
    public float slideAccel; // tiles/s² to change speed (slippery ground), 0 = instant
    public bool holdsTerrain; // terrain may lie on it (electrified water)
    public int tileset = -1; // ROM tileset whose secondary layer draws it (Resources/ObstacleTiles/<id>)
    public bool overlay; // lies over what was there (bridge over the void, plants on water), drawn on top of it
    public string color = "#ffffff"; // fallback look, when neither the ROM tiles nor a hand-made sheet exist
    [NonSerialized] public Color32 tint;
    [NonSerialized] public EffectLibrary.Sheet sheet; // hand-made look: obstacle_<key> in Resources/Effects
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
    static void Reset() => loaded.Clear();

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

/// A move of type "byType" hitting obstacle "on": it turns into "becomes" ("" removes it) for "duration" seconds
/// (0: for good), or paints terrain "paintTerrain" over the whole connected obstacle; "leaves": terrain painted
/// where it was removed; "path": also along a projectile's flight.
[Serializable]
public class ObstacleInteraction
{
    public string on;
    public int byType;
    public string becomes;
    public float duration;
    public bool path;
    public float radius = 0.8f;
    public string paintTerrain;
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
    Transform tileRoot;
    int sortingLayer, sortingOrder;
    float[] revertAt;
    readonly Dictionary<int, GameObject> solids = new(); // colliders of the solid tiles
    readonly Dictionary<Combatant, Vector2> lastSafe = new();
    readonly Dictionary<Combatant, float> fellAt = new();
    Texture2D texture;
    Color32[] pixels;
    bool dirty;
    int count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        defs = null;
        Current = null;
    }

    static void Load()
    {
        if (defs != null) return;
        defs = new Dictionary<string, ObstacleDef>();
        interactions = new List<ObstacleInteraction>();
        var asset = Resources.Load<TextAsset>("Data/obstacles");
        if (asset == null) return;
        var file = JsonUtility.FromJson<ObstacleFile>(asset.text);
        foreach (var d in file.obstacles ?? new ObstacleDef[0])
        {
            d.tint = ColorUtility.TryParseHtmlString(d.color, out var c) ? c : Color.white;
            d.sheet = EffectLibrary.Get("obstacle_" + d.key);
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
        foreach (var solid in solids.Values)
            if (solid != null) Destroy(solid);
        solids.Clear();
        lastSafe.Clear();
        fellAt.Clear();
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
    }

    // ---- Who goes where -------------------------------------------------------------------------------------

    static bool CanPass(ObstacleDef def, PokemonStats stats) => stats != null && Array.IndexOf(def.passTypes, stats.movementType) >= 0;

    /// This Pokémon cannot be on that tile: a solid one, or one it would fall into.
    public static bool Blocks(int x, int y, Combatant c)
    {
        var def = At(x, y);
        return def != null && (def.solid || (def.fall && (c == null || !CanPass(def, c.Stats))));
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

    public static float SpeedMul(Combatant c) => At(c.transform.position)?.speedMul ?? 1f;

    public static float SlideAccel(Combatant c) => At(c.transform.position)?.slideAccel ?? 0f;

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

    void Touch(MoveDef move, Vector2 from, Vector2 to, float radius, bool path, Combatant caster)
    {
        foreach (var it in interactions)
        {
            if (it.byType != move.type || (path && !it.path)) continue;
            var on = Def(it.on);
            if (on == null) continue;
            float r = radius > 0f ? radius : it.radius;
            var hit = Cells(from, to, r, on);
            if (hit.Count == 0) continue;
            if (!string.IsNullOrEmpty(it.paintTerrain))
            {
                Terrain.PaintCells(Flood(hit, on), it.paintTerrain, it.duration, caster);
                continue;
            }
            var becomes = Def(it.becomes);
            foreach (var (x, y) in hit)
            {
                Set(x, y, becomes, becomes != null ? it.duration : 0f);
                if (becomes == null && !string.IsNullOrEmpty(it.leaves)) Terrain.PaintArea(it.leaves, grid.CentreOf(x, y), 0.4f, 4f, caster);
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
            Vector2 position = c.transform.position;
            if (!BlocksAt(position, c))
            {
                lastSafe[c] = position; // where it comes back after a fall
                continue;
            }
            if (fellAt.TryGetValue(c, out float at) && Time.time < at + FallCooldown) continue;
            Fall(c, At(position));
        }
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
            if (topTiles[i] != null && kind[i]?.sheet != null) topTiles[i].sprite = kind[i].sheet.Frame(FrameAt(kind[i].sheet));
    }

    // Draws tile i with sprites (ROM layer underneath, hand-made sheet on top); false when it has neither and the
    // flat colour of the texture stands in.
    bool Draw(int i)
    {
        var def = kind[i];
        var floorKind = def == null ? null : def.tileset >= 0 ? def : under[i];
        var rom = floorKind != null ? RomObstacleTiles.Get(floorKind.tileset) : null;
        var sheet = def?.sheet;
        int x = i % size, y = i / size;
        Show(baseTiles, i, rom != null ? rom.Tile(Mask(x, y, floorKind), (x * 7 + y * 13) % 8 == 0 ? 1 + (x + y) % 2 : 0) : null, 0, 0f);
        float angle = def != null && def.Bridge && !SpansNorthSouth(x, y) ? 90f : 0f;
        Show(topTiles, i, sheet?.Frame(FrameAt(sheet)), 1, angle);
        if (sheet != null && sheet.frame_count > 1) animated.Add(i);
        return def != null && (rom != null || sheet != null);
    }

    void Show(SpriteRenderer[] layer, int i, Sprite sprite, int above, float angle)
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
        public string type; // chasm, river, lake, ice_walls
        public int weight = 1;
        public string kind;
        public int width = 2;
        public int[] bridges = { 1, 2 };
        public string bridgeKind = "bridge";
        public int[] sizes = { 2, 3, 4 };
        public int[] count = { 2, 4 };
    }

    [Serializable]
    public class Room
    {
        public int[] features = { 2, 3 };
        public int spawnClear = 4;
        public Feature[] pool;
    }

    /// Fills the arena with 2 or 3 features drawn from the room's pool, keeping clear tiles around "keep"
    /// (spawn point, stairs) and drawing again when a walker could not reach them all. Returns the names drawn.
    public List<string> Generate(Room room, System.Random rng, List<Vector2Int> keep)
    {
        var names = new List<string>();
        for (int attempt = 0; attempt < 20; attempt++)
        {
            Clear();
            names.Clear();
            int wanted = rng.Next(room.features[0], room.features[Mathf.Min(1, room.features.Length - 1)] + 1);
            var pool = new List<Feature>(room.pool);
            for (int n = 0; n < wanted && pool.Count > 0; n++)
            {
                var f = Draw(pool, rng);
                pool.RemoveAll(p => p.type == f.type && p.kind == f.kind); // never twice the same
                if (Place(f, rng, keep, room.spawnClear)) names.Add(Describe(f));
            }
            if (Connected(keep)) return names;
        }
        Clear();
        names.Clear();
        return names;
    }

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
        "chasm" => "Vide avec ponts",
        "river" => f.kind == "ice_floor" ? "Rivière glacée" : "Rivière",
        "lake" => f.kind == "lava" ? "Lac de lave" : f.kind == "ice_floor" ? "Lac de glace" : "Lac",
        _ => "Murs de glace",
    };

    bool Free(int x, int y, List<Vector2Int> keep, int clear)
    {
        var interior = grid.Interior;
        if (x < interior.xMin + 1 || y < interior.yMin + 1 || x >= interior.xMax - 1 || y >= interior.yMax - 1) return false;
        if (!grid.Walkable(x, y) || KindAt(x, y) != null) return false;
        foreach (var k in keep)
            if (Mathf.Abs(k.x - x) <= clear && Mathf.Abs(k.y - y) <= clear) return false;
        return true;
    }

    bool Place(Feature f, System.Random rng, List<Vector2Int> keep, int clear)
    {
        var interior = grid.Interior;
        switch (f.type)
        {
            case "chasm":
            case "river":
            {
                // A band across the arena, wandering by one tile now and then, with bridges across it.
                bool horizontal = rng.Next(2) == 0;
                int length = horizontal ? interior.width : interior.height;
                int lo = (horizontal ? interior.yMin : interior.xMin) + 3, hi = (horizontal ? interior.yMax : interior.xMax) - 3 - f.width;
                int offset = rng.Next(lo, hi);
                int placed = 0;
                var columns = new List<int>();
                int start = horizontal ? interior.xMin : interior.yMin;
                for (int s = 0; s < length; s++)
                {
                    if (s > 0 && rng.Next(4) == 0) offset = Mathf.Clamp(offset + (rng.Next(2) == 0 ? -1 : 1), lo, hi);
                    for (int w = 0; w < f.width; w++)
                    {
                        int x = horizontal ? start + s : offset + w, y = horizontal ? offset + w : start + s;
                        if (!Free(x, y, keep, clear)) continue;
                        Set(x, y, Def(f.kind));
                        placed++;
                    }
                    columns.Add(offset);
                }
                if (placed == 0) return false;
                int bridges = f.bridges != null && f.bridges.Length > 0 ? rng.Next(f.bridges[0], f.bridges[Mathf.Min(1, f.bridges.Length - 1)] + 1) : 0;
                var bridge = Def(f.bridgeKind);
                for (int b = 0; b < bridges && bridge != null; b++)
                {
                    // Spread along the band, two tiles wide, a little beyond the band on both sides.
                    int s = (b + 1) * length / (bridges + 1) + rng.Next(-2, 3);
                    s = Mathf.Clamp(s, 1, length - 3);
                    for (int t = s; t < s + 2; t++)
                        for (int w = 0; w < f.width; w++)
                        {
                            int x = horizontal ? start + t : columns[t] + w, y = horizontal ? columns[t] + w : start + t;
                            if (KindAt(x, y) == Def(f.kind)) Set(x, y, bridge);
                        }
                }
                return true;
            }
            case "lake":
            {
                int r = f.sizes != null && f.sizes.Length > 0 ? f.sizes[rng.Next(f.sizes.Length)] : 3;
                for (int attempt = 0; attempt < 40; attempt++)
                {
                    int cx = rng.Next(interior.xMin + r + 2, interior.xMax - r - 2), cy = rng.Next(interior.yMin + r + 2, interior.yMax - r - 2);
                    bool far = true;
                    foreach (var k in keep)
                        if (Vector2.Distance(k, new Vector2(cx, cy)) < r + clear + 1) far = false;
                    if (!far) continue;
                    // An organic blob: a few overlapping discs.
                    var discs = new List<(float, float, float)> { (cx, cy, r) };
                    for (int d = 0; d < 3; d++) discs.Add((cx + rng.Next(-r, r + 1) * 0.6f, cy + rng.Next(-r, r + 1) * 0.6f, r * 0.6f + rng.Next(0, 2)));
                    int placed = 0;
                    for (int x = cx - 2 * r; x <= cx + 2 * r; x++)
                        for (int y = cy - 2 * r; y <= cy + 2 * r; y++)
                        {
                            bool inside = false;
                            foreach (var (dx, dy, dr) in discs)
                                if ((x - dx) * (x - dx) + (y - dy) * (y - dy) <= dr * dr) inside = true;
                            if (inside && Free(x, y, keep, clear)) { Set(x, y, Def(f.kind)); placed++; }
                        }
                    if (placed > 0) return true;
                }
                return false;
            }
            default: // ice_walls: short straight walls
            {
                var wall = Def("ice_wall");
                int walls = f.count != null && f.count.Length > 0 ? rng.Next(f.count[0], f.count[Mathf.Min(1, f.count.Length - 1)] + 1) : 3;
                int built = 0;
                for (int attempt = 0; attempt < 60 && built < walls; attempt++)
                {
                    bool horizontal = rng.Next(2) == 0;
                    int x0 = rng.Next(interior.xMin + 2, interior.xMax - 4), y0 = rng.Next(interior.yMin + 2, interior.yMax - 4);
                    bool ok = true;
                    for (int t = 0; t < 3 && ok; t++) ok = Free(horizontal ? x0 + t : x0, horizontal ? y0 : y0 + t, keep, clear);
                    if (!ok) continue;
                    for (int t = 0; t < 3; t++) Set(horizontal ? x0 + t : x0, horizontal ? y0 : y0 + t, wall);
                    built++;
                }
                return built > 0;
            }
        }
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
