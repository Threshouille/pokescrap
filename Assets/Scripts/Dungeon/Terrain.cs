using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// A kind of ground effect (ice, embers...), from terrains.json.
[Serializable]
public class TerrainDef
{
    public string key;
    public string name;
    public string color = "#ffffff";
    public float alpha = 0.5f;
    public int[] immuneTypes = new int[0];
    public bool grounded = true; // Pokémon that float or go through walls (movementType 2, 3) are not affected
    public float speedMul = 1f;
    public int[] boostTypes = new int[0]; // these types get boostMul instead of speedMul
    public float boostMul = 1f;
    public float slideAccel; // tiles/s² to change speed: the Pokémon slides (0: instant)
    public float knockbackMul = 1f;
    public string status; // given to whoever stands there, statusChance per second
    public float statusChance;
    public float damageShare; // share of max HP lost per second
    public string cures; // status removed from whoever stands there
    public float navCost; // extra path cost for the AIs, in tiles of detour
    [NonSerialized] public Color tint;
}

/// Ground effect a move leaves, from terrains.json.
[Serializable]
public class MoveTerrain
{
    public int move;
    public string terrain;
    public string shape; // trail, impact, around, dash, strike
    public float radius = 0.5f;
    public float duration = 3f;
    public float chance = 1f;
}

/// A terrain painted (by) over another (on) gives "becomes" instead; empty clears the cell.
[Serializable]
public class TerrainReaction
{
    public string on, by, becomes;
}

/// Ground effects left by moves, one state per tile of the arena: everyone standing on a tile gets its effect
/// (both teams), except the Pokémon immune to it. Drawn as a texture with one pixel per tile over the floor.
/// Moves paint it through OnTrail / OnImpact / OnAround / OnDash / OnStrike, each painting only when the move's
/// terrain has that shape.
public class Terrain : MonoBehaviour
{
    const float DamageTick = 1f;
    const float FadeTime = 1f; // a tile fades out over its last second
    const float HazardCost = 5f; // navCost from which a tile is worth stepping out of

    [Serializable] class TerrainFile { public TerrainDef[] terrains; public MoveTerrain[] moves; public TerrainReaction[] reactions; }

    static Dictionary<string, TerrainDef> defs;
    static Dictionary<int, MoveTerrain> byMove;
    static Dictionary<(string, string), string> reactions;

    public static Terrain Current { get; private set; }

    DungeonGenerator grid;
    int size;
    TerrainDef[] kind;
    float[] until, length;
    int active;
    Texture2D texture;
    Color32[] pixels;
    readonly Dictionary<Combatant, float> nextDamage = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        defs = null;
        Current = null;
    }

    static void Load()
    {
        if (defs != null) return;
        defs = new Dictionary<string, TerrainDef>();
        byMove = new Dictionary<int, MoveTerrain>();
        reactions = new Dictionary<(string, string), string>();
        var asset = Resources.Load<TextAsset>("Data/terrains");
        if (asset == null) return;
        var file = JsonUtility.FromJson<TerrainFile>(asset.text);
        foreach (var d in file.terrains ?? new TerrainDef[0])
        {
            d.tint = ColorUtility.TryParseHtmlString(d.color, out var c) ? c : Color.white;
            d.tint.a = d.alpha;
            defs[d.key] = d;
        }
        foreach (var m in file.moves ?? new MoveTerrain[0]) byMove[m.move] = m;
        foreach (var r in file.reactions ?? new TerrainReaction[0]) reactions[(r.on, r.by)] = r.becomes ?? "";
    }

    public static TerrainDef Def(string key)
    {
        Load();
        return key != null && defs.TryGetValue(key, out var d) ? d : null;
    }

    /// Ground effect a move leaves, or null.
    public static MoveTerrain Of(MoveDef move)
    {
        Load();
        return move != null && byMove.TryGetValue(move.id, out var t) ? t : null;
    }

    /// Clears the terrain for a new arena (called by DungeonGenerator.Generate).
    public static void Rebuild(DungeonGenerator arena)
    {
        if (Current == null)
        {
            var go = new GameObject("Terrain");
            Current = go.AddComponent<Terrain>();
        }
        Current.Setup(arena);
    }

    void Setup(DungeonGenerator arena)
    {
        grid = arena;
        size = arena.Size;
        kind = new TerrainDef[size * size];
        until = new float[size * size];
        length = new float[size * size];
        active = 0;
        nextDamage.Clear();
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
                renderer.sortingOrder = floor.sortingOrder + 1; // over the floor, under zones and Pokémon
            }
        }
        // One texture pixel per tile, laid over the floor map.
        Vector2 origin = arena.CentreOf(0, 0), step = arena.CentreOf(1, 1) - origin;
        transform.position = origin - step / 2f;
        transform.localScale = new Vector3(step.x, step.y, 1f);
        Array.Clear(pixels, 0, pixels.Length);
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    // ---- Painting -------------------------------------------------------------------------------------------

    public static void OnTrail(MoveDef move, Vector2 from, Vector2 to) => PaintLine(Of(move), "trail", from, to);
    public static void OnDash(MoveDef move, Vector2 at) => PaintDisc(Of(move), "dash", at, -1f);
    public static void OnImpact(MoveDef move, Vector2 at) => PaintDisc(Of(move), "impact", at, -1f);
    public static void OnAround(MoveDef move, Vector2 at) => PaintDisc(Of(move), "around", at, -1f);

    /// A melee hit area: a line for a thrust, the area itself for the others.
    public static void OnStrike(MoveDef move, string shape, Vector2 at, Vector2 direction, float reach)
    {
        var t = Of(move);
        if (t == null || t.shape != "strike") return;
        switch (shape)
        {
            case "thrust": PaintLine(t, "strike", at, at + direction * reach); break;
            case "around": PaintDisc(t, "strike", at, reach); break;
            case "arc": PaintDisc(t, "strike", at + direction * reach / 2f, reach / 2f); break;
            default: PaintDisc(t, "strike", at + direction * move.range, move.radius); break;
        }
    }

    static void PaintDisc(MoveTerrain t, string shape, Vector2 centre, float radius)
    {
        if (Current == null || t == null || t.shape != shape) return;
        if (shape != "dash" && t.chance < 1f && UnityEngine.Random.value > t.chance) return;
        Current.Paint(t, centre, centre, radius > 0f ? radius : t.radius);
    }

    static void PaintLine(MoveTerrain t, string shape, Vector2 from, Vector2 to)
    {
        if (Current == null || t == null || t.shape != shape) return;
        Current.Paint(t, from, to, t.radius);
    }

    // Every tile whose centre lies within radius of the segment (at least the tiles under its ends).
    void Paint(MoveTerrain t, Vector2 from, Vector2 to, float radius)
    {
        var def = Def(t.terrain);
        if (def == null || grid == null) return;
        var a = grid.CellOf(from);
        var b = grid.CellOf(to);
        int r = Mathf.CeilToInt(radius) + 1;
        for (int x = Mathf.Min(a.x, b.x) - r; x <= Mathf.Max(a.x, b.x) + r; x++)
            for (int y = Mathf.Min(a.y, b.y) - r; y <= Mathf.Max(a.y, b.y) + r; y++)
            {
                if (!grid.Walkable(x, y)) continue;
                bool end = (x == a.x && y == a.y) || (x == b.x && y == b.y);
                if (end || DistanceToSegment(grid.CentreOf(x, y), from, to) <= radius) Set(x, y, def, t.duration);
            }
    }

    void Set(int x, int y, TerrainDef def, float duration)
    {
        int i = y * size + x;
        var old = Live(i);
        if (old != null && reactions.TryGetValue((old.key, def.key), out string becomes))
            def = Def(becomes); // null (empty or unknown key): the two cancel out
        if (old == null && def != null) active++;
        else if (old != null && def == null) active--;
        kind[i] = def;
        until[i] = Time.time + duration;
        length[i] = duration;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    // ---- Queries --------------------------------------------------------------------------------------------

    TerrainDef Live(int i) => kind[i] != null && Time.time < until[i] ? kind[i] : null;

    TerrainDef KindAt(int x, int y) => x >= 0 && y >= 0 && x < size && y < size ? Live(y * size + x) : null;

    /// Terrain on the tile under a point, whoever stands there.
    public static TerrainDef At(Vector2 world)
    {
        if (Current == null || Current.grid == null) return null;
        var cell = Current.grid.CellOf(world);
        return Current.KindAt(cell.x, cell.y);
    }

    public static bool Immune(Combatant c, TerrainDef def) => Immune(c.Stats, def);

    /// Not affected by that terrain: one of its types, or floating above the ground.
    public static bool Immune(PokemonStats stats, TerrainDef def)
    {
        if (stats == null) return false;
        if (def.grounded && (stats.movementType == 2 || stats.movementType == 3)) return true;
        foreach (int type in stats.types ?? new int[0])
            if (Array.IndexOf(def.immuneTypes, type) >= 0) return true;
        return false;
    }

    /// Terrain that acts on this Pokémon where it stands (null when none, or immune).
    public static TerrainDef Affecting(Combatant c)
    {
        var def = At(c.transform.position);
        return def != null && !Immune(c, def) ? def : null;
    }

    public static float SpeedMul(Combatant c)
    {
        var def = Affecting(c);
        if (def == null) return 1f;
        foreach (int type in c.Stats?.types ?? new int[0])
            if (Array.IndexOf(def.boostTypes, type) >= 0) return def.boostMul;
        return def.speedMul;
    }

    public static float SlideAccel(Combatant c) => Affecting(c)?.slideAccel ?? 0f;

    public static float KnockbackMul(Combatant c) => Affecting(c)?.knockbackMul ?? 1f;

    /// Extra path cost of a tile for an AI (0 when the agent is immune).
    public static float NavCost(int x, int y, Combatant agent)
    {
        var def = Current != null ? Current.KindAt(x, y) : null;
        return def != null && (agent == null || !Immune(agent, def)) ? def.navCost : 0f;
    }

    /// True when the tile under a point is worth avoiding for this agent.
    public static bool Hazard(Vector2 world, Combatant agent)
    {
        if (Current == null || Current.grid == null) return false;
        var cell = Current.grid.CellOf(world);
        return NavCost(cell.x, cell.y, agent) >= HazardCost;
    }

    /// Direction to the nearest safe tile when the agent stands on a hazard, zero otherwise.
    public static Vector2 EscapeDirection(Vector2 position, Combatant agent)
    {
        if (!Hazard(position, agent)) return Vector2.zero;
        var grid = Current.grid;
        var cell = grid.CellOf(position);
        for (int r = 1; r <= 4; r++)
        {
            Vector2 best = Vector2.zero;
            float bestDistance = float.MaxValue;
            for (int x = cell.x - r; x <= cell.x + r; x++)
                for (int y = cell.y - r; y <= cell.y + r; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x - cell.x), Mathf.Abs(y - cell.y)) != r || !grid.Walkable(x, y)) continue;
                    if (NavCost(x, y, agent) >= HazardCost) continue;
                    Vector2 to = grid.CentreOf(x, y) - position;
                    if (to.sqrMagnitude < bestDistance) { bestDistance = to.sqrMagnitude; best = to; }
                }
            if (best != Vector2.zero) return best.normalized;
        }
        return Vector2.zero;
    }

    // ---- Effects and drawing --------------------------------------------------------------------------------

    void FixedUpdate()
    {
        if (active <= 0) return;
        foreach (var c in Combatant.All)
        {
            if (c == null || !c.IsAlive) continue;
            var def = Affecting(c);
            if (def == null) continue;
            if (!string.IsNullOrEmpty(def.cures) && c.HasStatus(def.cures)) c.RemoveStatus(def.cures);
            if (!string.IsNullOrEmpty(def.status) && def.statusChance > 0f && !c.HasStatus(def.status)
                && UnityEngine.Random.value < 1f - Mathf.Pow(1f - def.statusChance, Time.fixedDeltaTime))
            {
                var status = StatusLibrary.Get(def.status);
                if (status != null) c.AddStatus(def.status, status.duration);
            }
            if (def.damageShare > 0f)
            {
                if (!nextDamage.TryGetValue(c, out float at)) nextDamage[c] = at = Time.time + DamageTick;
                if (Time.time >= at)
                {
                    c.TakeFixedDamage(c.maxHp * def.damageShare * DamageTick);
                    nextDamage[c] = Time.time + DamageTick;
                }
            }
            else nextDamage.Remove(c);
        }
    }

    void LateUpdate()
    {
        if (texture == null || active <= 0 && !dirty) return;
        int live = 0;
        for (int i = 0; i < kind.Length; i++)
        {
            var def = kind[i];
            if (def == null) { pixels[i] = default; continue; }
            float left = until[i] - Time.time;
            if (left <= 0f)
            {
                kind[i] = null;
                pixels[i] = default;
                continue;
            }
            live++;
            var c = def.tint;
            c.a *= Mathf.Clamp01(left / Mathf.Min(FadeTime, length[i]));
            pixels[i] = c;
        }
        dirty = live > 0 || active > 0; // one more frame to clear the last tiles
        active = live;
        texture.SetPixels32(pixels);
        texture.Apply();
    }

    bool dirty;
}
