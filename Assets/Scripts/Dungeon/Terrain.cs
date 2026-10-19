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
    public string flickerColor; // the tile pulses between color and this one (electrified water), flickerRate times a second
    public float flickerRate = 4f;
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
    public int[] regenTypes = new int[0]; // these types heal regenShare of their max HP per second
    public float regenShare;
    public string cures; // status removed from whoever stands there
    public float navCost; // extra path cost for the AIs, in tiles of detour
    [NonSerialized] public Color tint, flicker;
    [NonSerialized] public bool flickers;
}

/// Ground effect a move leaves, from terrains.json. Radius and duration come from the move's power when 0.
[Serializable]
public class MoveTerrain
{
    public int move;
    public string terrain;
    public string shape; // trail, impact, around, dash, strike
    public float radius;
    public float duration;
    public float chance = 1f;
}

/// A terrain painted (by) over another (on) gives "becomes" instead: "" clears the cell, "=" keeps the old one.
/// spread: the whole connected area of "on" turns too, spreadDelay seconds per tile away from the hit.
/// Duration: "duration" when set, else the old remaining time (keepDuration) or the new one, times durationMul.
[Serializable]
public class TerrainReaction
{
    public string on, by, becomes;
    public float duration;
    public bool keepDuration;
    public float durationMul = 1f;
    public bool spread;
    public float spreadDelay;
}

/// How a move's power sets the size and duration of its trace.
[Serializable]
public class TerrainSizing
{
    public float durationBase = 2f, durationPerPower = 0.125f, durationMax = 7f;
    public float impactBase = 0.5f, impactPerPower = 0.033f, impactMax = 2f;
    public float trailBase = 0.4f, trailPerPower = 0.017f, trailMax = 0.9f;
    public float aroundBase = 1f, aroundPerPower = 0.05f, aroundMax = 3f;
    public float dashRadius = 0.4f;
}

/// Ground effects left by moves, one state per tile of the arena: everyone standing on a tile gets its effect
/// (both teams), except the Pokémon immune to it and the one whose move left it (its owner). Drawn as a texture with one pixel per tile over the floor.
/// Moves paint it through OnTrail / OnImpact / OnAround / OnDash / OnStrike, each painting only when the move's
/// terrain has that shape. A terrain painted over another may react (reactions of terrains.json), possibly
/// spreading over the whole connected area as a wave.
public class Terrain : MonoBehaviour
{
    const float Tick = 1f; // seconds between two damages or heals
    const float FadeTime = 1f; // a tile fades out over its last second
    const float HazardCost = 5f; // navCost from which a tile is worth stepping out of

    [Serializable] class TerrainFile { public TerrainSizing sizing; public TerrainDef[] terrains; public MoveTerrain[] moves; public TerrainReaction[] reactions; }

    static Dictionary<string, TerrainDef> defs;
    static Dictionary<int, MoveTerrain> byMove;
    static Dictionary<(string, string), TerrainReaction> reactions;
    static TerrainSizing sizing;

    public static Terrain Current { get; private set; }

    struct Pending { public float at; public int cell; public TerrainDef from, to; public float duration; public Combatant owner; }

    DungeonGenerator grid;
    int size;
    TerrainDef[] kind;
    float[] until, length;
    Combatant[] owner; // who left the terrain on each tile (spared by it); null for nobody
    int active;
    bool dirty;
    Texture2D texture;
    Color32[] pixels;
    readonly List<Pending> pending = new(); // tiles a spreading reaction reaches later
    readonly HashSet<int> queued = new();
    readonly Dictionary<Combatant, float> nextTick = new();

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
        reactions = new Dictionary<(string, string), TerrainReaction>();
        sizing = new TerrainSizing();
        var asset = Resources.Load<TextAsset>("Data/terrains");
        if (asset == null) return;
        var file = JsonUtility.FromJson<TerrainFile>(asset.text);
        if (file.sizing != null) sizing = file.sizing;
        foreach (var d in file.terrains ?? new TerrainDef[0])
        {
            d.tint = ColorUtility.TryParseHtmlString(d.color, out var c) ? c : Color.white;
            d.tint.a = d.alpha;
            d.flickers = ColorUtility.TryParseHtmlString(d.flickerColor, out var f);
            d.flicker = f;
            d.flicker.a = d.alpha;
            defs[d.key] = d;
        }
        foreach (var m in file.moves ?? new MoveTerrain[0]) byMove[m.move] = m;
        foreach (var r in file.reactions ?? new TerrainReaction[0]) reactions[(r.on, r.by)] = r;
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

    /// Seconds the trace of a move lasts: given, or from its power.
    public static float DurationOf(MoveTerrain t, MoveDef move)
    {
        if (t.duration > 0f) return t.duration;
        return Mathf.Min(sizing.durationMax, sizing.durationBase + sizing.durationPerPower * move.power);
    }

    /// Radius of the trace of a move, in tiles: given, or from its power (and its own area for "around").
    public static float RadiusOf(MoveTerrain t, MoveDef move)
    {
        if (t.radius > 0f) return t.radius;
        float p = move.power;
        return t.shape switch
        {
            "trail" => Mathf.Min(sizing.trailMax, sizing.trailBase + sizing.trailPerPower * p),
            "around" => Mathf.Min(move.radius > 0f ? move.radius : sizing.aroundMax, Mathf.Min(sizing.aroundMax, sizing.aroundBase + sizing.aroundPerPower * p)),
            "dash" or "strike" => sizing.dashRadius,
            _ => Mathf.Min(sizing.impactMax, sizing.impactBase + sizing.impactPerPower * p),
        };
    }

    /// Terrain left when "by" is painted over "old" (either may be null): null when the tile ends up empty.
    public static string Predict(string old, string by)
    {
        Load();
        if (old == null || by == null || !reactions.TryGetValue((old, by), out var r)) return by;
        return r.becomes == "=" ? old : string.IsNullOrEmpty(r.becomes) ? null : r.becomes;
    }

    /// True when painting "by" over "old" triggers a reaction; spreads: it turns the whole connected area.
    public static bool Reacts(string old, string by, out bool spreads)
    {
        Load();
        spreads = false;
        if (old == null || by == null || !reactions.TryGetValue((old, by), out var r)) return false;
        spreads = r.spread;
        return true;
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
        Load();
        grid = arena;
        size = arena.Size;
        kind = new TerrainDef[size * size];
        until = new float[size * size];
        length = new float[size * size];
        owner = new Combatant[size * size];
        active = 0;
        pending.Clear();
        queued.Clear();
        nextTick.Clear();
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

    // "caster": the Pokémon using the move, spared by the terrain it leaves.
    public static void OnTrail(MoveDef move, Vector2 from, Vector2 to, Combatant caster) => PaintLine(move, "trail", from, to, caster);
    public static void OnDash(MoveDef move, Vector2 at, Combatant caster) => PaintDisc(move, "dash", at, -1f, caster);
    public static void OnImpact(MoveDef move, Vector2 at, Combatant caster) => PaintDisc(move, "impact", at, -1f, caster);
    public static void OnAround(MoveDef move, Vector2 at, Combatant caster) => PaintDisc(move, "around", at, -1f, caster);

    /// A melee hit area: a line for a thrust, the area itself for the others.
    public static void OnStrike(MoveDef move, string shape, Vector2 at, Vector2 direction, float reach, Combatant caster)
    {
        var t = Of(move);
        if (t == null || t.shape != "strike") return;
        switch (shape)
        {
            case "thrust": PaintLine(move, "strike", at, at + direction * reach, caster); break;
            case "around": PaintDisc(move, "strike", at, reach, caster); break;
            case "arc": PaintDisc(move, "strike", at + direction * reach / 2f, reach / 2f, caster); break;
            default: PaintDisc(move, "strike", at + direction * move.range, Mathf.Max(move.radius, RadiusOf(t, move)), caster); break;
        }
    }

    /// Paints a terrain directly, without a move (the dev sandbox's emitters): a disc of that radius.
    public static void PaintArea(string key, Vector2 centre, float radius, float duration, Combatant caster = null)
    {
        var def = Def(key);
        if (Current == null || def == null) return;
        Current.Paint(def, duration, centre, centre, radius, caster);
    }

    /// Makes the tiles of a disc that still hold that terrain last "duration" more seconds from now; tiles where
    /// it was removed (burnt, washed away...) stay as they are.
    public static void RefreshArea(string key, Vector2 centre, float radius, float duration)
    {
        var def = Def(key);
        if (Current == null || def == null || Current.grid == null) return;
        var grid = Current.grid;
        var c = grid.CellOf(centre);
        int r = Mathf.CeilToInt(radius) + 1;
        for (int x = c.x - r; x <= c.x + r; x++)
            for (int y = c.y - r; y <= c.y + r; y++)
            {
                if (Current.KindAt(x, y) != def || Vector2.Distance(grid.CentreOf(x, y), centre) > radius) continue;
                int i = y * Current.size + x;
                Current.until[i] = Mathf.Max(Current.until[i], Time.time + duration);
                Current.length[i] = Mathf.Max(Current.length[i], duration);
            }
    }

    static void PaintDisc(MoveDef move, string shape, Vector2 centre, float radius, Combatant caster)
    {
        var t = Of(move);
        if (Current == null || t == null || t.shape != shape) return;
        if (shape != "dash" && t.chance < 1f && UnityEngine.Random.value > t.chance) return;
        Current.Paint(Def(t.terrain), DurationOf(t, move), centre, centre, radius > 0f ? radius : RadiusOf(t, move), caster);
    }

    static void PaintLine(MoveDef move, string shape, Vector2 from, Vector2 to, Combatant caster)
    {
        var t = Of(move);
        if (Current == null || t == null || t.shape != shape) return;
        Current.Paint(Def(t.terrain), DurationOf(t, move), from, to, RadiusOf(t, move), caster);
    }

    // Every tile whose centre lies within radius of the segment (at least the tiles under its ends).
    void Paint(TerrainDef def, float duration, Vector2 from, Vector2 to, float radius, Combatant caster)
    {
        if (def == null || grid == null) return;
        var a = grid.CellOf(from);
        var b = grid.CellOf(to);
        int r = Mathf.CeilToInt(radius) + 1;
        for (int x = Mathf.Min(a.x, b.x) - r; x <= Mathf.Max(a.x, b.x) + r; x++)
            for (int y = Mathf.Min(a.y, b.y) - r; y <= Mathf.Max(a.y, b.y) + r; y++)
            {
                if (!grid.Walkable(x, y)) continue;
                bool end = (x == a.x && y == a.y) || (x == b.x && y == b.y);
                if (end || DistanceToSegment(grid.CentreOf(x, y), from, to) <= radius) Hit(x, y, def, duration, caster);
            }
    }

    // A tile reached by a move: reacts with what is already there, possibly spreading over the connected area.
    void Hit(int x, int y, TerrainDef def, float duration, Combatant caster)
    {
        int i = y * size + x;
        var old = Live(i);
        if (old == null || !reactions.TryGetValue((old.key, def.key), out var r))
        {
            Set(i, def, duration, caster);
            return;
        }
        if (r.becomes == "=") return;
        var result = string.IsNullOrEmpty(r.becomes) ? null : Def(r.becomes);
        float left = until[i] - Time.time;
        float lasts = r.duration > 0f ? r.duration : (r.keepDuration ? left : duration) * r.durationMul;
        Set(i, result, lasts, caster); // whoever triggers a reaction owns what it gives
        if (r.spread) Spread(x, y, old, result, r, lasts, caster);
    }

    // Breadth-first over the tiles of the old terrain connected to (x, y): each turns later the farther it is.
    void Spread(int x, int y, TerrainDef from, TerrainDef to, TerrainReaction r, float lasts, Combatant caster)
    {
        var frontier = new Queue<(int x, int y, int steps)>();
        var seen = new HashSet<int> { y * size + x };
        frontier.Enqueue((x, y, 0));
        while (frontier.Count > 0)
        {
            var (cx, cy, steps) = frontier.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                int n = ny * size + nx;
                if (!seen.Add(n) || Live(n) != from || queued.Contains(n)) continue;
                float duration = r.duration > 0f ? r.duration : (r.keepDuration ? until[n] - Time.time : lasts) * (r.keepDuration ? r.durationMul : 1f);
                pending.Add(new Pending { at = Time.time + (steps + 1) * r.spreadDelay, cell = n, from = from, to = to, duration = duration, owner = caster });
                queued.Add(n);
                frontier.Enqueue((nx, ny, steps + 1));
            }
        }
    }

    void Set(int i, TerrainDef def, float duration, Combatant by)
    {
        if (def == null || duration <= 0f)
        {
            kind[i] = null;
            until[i] = 0f;
            owner[i] = null;
        }
        else
        {
            kind[i] = def;
            owner[i] = by;
            until[i] = Time.time + duration;
            length[i] = duration;
            active++;
        }
        dirty = true;
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

    /// Terrain of a tile that acts on this Pokémon: null when none, when immune, or when it left it itself.
    TerrainDef ActingAt(int x, int y, Combatant c)
    {
        var def = KindAt(x, y);
        if (def == null || c == null) return def;
        return owner[y * size + x] == c || Immune(c, def) ? null : def;
    }

    /// The terrain of the tile is there but spares this Pokémon (immune, or its own).
    bool SparedAt(int x, int y, Combatant c) => KindAt(x, y) != null && ActingAt(x, y, c) == null;

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
        return HasType(stats, def.immuneTypes);
    }

    static bool HasType(PokemonStats stats, int[] types)
    {
        if (stats?.types == null || types == null) return false;
        foreach (int type in stats.types)
            if (Array.IndexOf(types, type) >= 0) return true;
        return false;
    }

    /// Terrain that acts on this Pokémon where it stands (null when none, or immune).
    public static TerrainDef Affecting(Combatant c)
    {
        if (Current == null || Current.grid == null) return null;
        var cell = Current.grid.CellOf(c.transform.position);
        return Current.ActingAt(cell.x, cell.y, c);
    }

    public static float SpeedMul(Combatant c)
    {
        var def = Affecting(c);
        if (def == null) return 1f;
        return HasType(c.Stats, def.boostTypes) ? def.boostMul : def.speedMul;
    }

    public static float SlideAccel(Combatant c) => Affecting(c)?.slideAccel ?? 0f;

    public static float KnockbackMul(Combatant c) => Affecting(c)?.knockbackMul ?? 1f;

    /// Extra path cost of a tile for an AI (0 when the agent is immune).
    public static float NavCost(int x, int y, Combatant agent)
    {
        var def = Current != null ? Current.ActingAt(x, y, agent) : null;
        return def != null ? def.navCost : 0f;
    }

    /// True when that terrain would hurt or hinder this Pokémon enough to be avoided.
    public static bool Harmful(TerrainDef def, Combatant c) => def != null && def.navCost >= HazardCost && !Immune(c, def);

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

    /// Good for this Pokémon to stand on: heals it, cures a status it has, or keeps its opponents away (a
    /// harmful terrain that spares it: immune, or its own).
    bool RefugeAt(int x, int y, Combatant c)
    {
        var def = KindAt(x, y);
        if (def == null) return false;
        if (def.regenShare > 0f && HasType(c.Stats, def.regenTypes)) return true;
        if (!string.IsNullOrEmpty(def.cures) && c.HasStatus(def.cures)) return true;
        return def.navCost >= HazardCost && SparedAt(x, y, c);
    }

    /// Centre of the nearest refuge tile within maxDistance tiles, or null.
    public static Vector2? FindRefuge(Combatant c, float maxDistance)
    {
        if (Current == null || Current.grid == null || Current.active <= 0) return null;
        var grid = Current.grid;
        Vector2 position = c.transform.position;
        var cell = grid.CellOf(position);
        int r = Mathf.CeilToInt(maxDistance);
        Vector2? best = null;
        float bestDistance = maxDistance * maxDistance;
        for (int x = cell.x - r; x <= cell.x + r; x++)
            for (int y = cell.y - r; y <= cell.y + r; y++)
            {
                if (!Current.RefugeAt(x, y, c)) continue;
                Vector2 centre = grid.CentreOf(x, y);
                float d = (centre - position).sqrMagnitude;
                if (d <= bestDistance) { bestDistance = d; best = centre; }
            }
        return best;
    }

    // ---- Effects and drawing --------------------------------------------------------------------------------

    void FixedUpdate()
    {
        for (int k = 0; k < pending.Count; k++)
        {
            var p = pending[k];
            if (Time.time < p.at) continue;
            pending.RemoveAt(k--);
            queued.Remove(p.cell);
            if (Live(p.cell) == p.from) Set(p.cell, p.to, p.duration, p.owner); // unless something else got there first
        }
        if (active <= 0) return;
        foreach (var c in Combatant.All)
        {
            if (c == null || !c.IsAlive) continue;
            var def = Affecting(c);
            if (def == null) { nextTick.Remove(c); continue; }
            if (!string.IsNullOrEmpty(def.cures) && c.HasStatus(def.cures)) c.RemoveStatus(def.cures);
            if (!string.IsNullOrEmpty(def.status) && def.statusChance > 0f && !c.HasStatus(def.status)
                && UnityEngine.Random.value < 1f - Mathf.Pow(1f - def.statusChance, Time.fixedDeltaTime))
            {
                var status = StatusLibrary.Get(def.status);
                if (status != null) c.AddStatus(def.status, status.duration);
            }
            bool regen = def.regenShare > 0f && HasType(c.Stats, def.regenTypes) && c.hp < c.maxHp;
            if (def.damageShare <= 0f && !regen) { nextTick.Remove(c); continue; }
            if (!nextTick.TryGetValue(c, out float at)) nextTick[c] = at = Time.time + Tick;
            if (Time.time < at) continue;
            nextTick[c] = Time.time + Tick;
            if (def.damageShare > 0f) c.TakeFixedDamage(c.maxHp * def.damageShare * Tick);
            if (regen) c.Heal(c.maxHp * def.regenShare * Tick);
        }
    }

    void LateUpdate()
    {
        if (texture == null || (active <= 0 && !dirty)) return;
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
            var c = def.flickers ? Color.Lerp(def.tint, def.flicker, Mathf.PingPong(Time.time * def.flickerRate * 2f, 1f)) : def.tint;
            c.a *= Mathf.Clamp01(left / Mathf.Min(FadeTime, length[i]));
            pixels[i] = c;
        }
        dirty = live > 0 || active > 0; // one more frame to clear the last tiles
        active = live;
        texture.SetPixels32(pixels);
        texture.Apply();
    }
}
