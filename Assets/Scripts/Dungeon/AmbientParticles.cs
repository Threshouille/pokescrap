using System;
using System.Collections.Generic;
using UnityEngine;

/// Ambient particles (Resources/Data/ambient.json; GDD, "Jour et nuit"), added by PlayerController: fireflies at night
/// in the woods, falling leaves, petals, embers over the lava, snow, dust, the current of the rivers... Every
/// sampleEvery seconds the tiles in sight are sorted by what each effect wants (sub-biome, theme, décor, obstacle,
/// terrain); each effect is then born on its own tiles at its density, during its phases of the day. A river's
/// current runs away from the island's centre, along the water (Flow); leaves fall from the trees' crowns; fireflies
/// carry lights (PointLights).
public class AmbientParticles : MonoBehaviour
{
    [Serializable]
    class Effect
    {
        public string key, motion = "wander", sheet = "", shape = "dot";
        public string[] where = new string[0], phases = new string[0], colors = new string[0], props = new string[0];
        public string light; // a glow of lights.json on some of its particles (fireflies)
        public int lights;
        public float density = 1f, speed = 0.5f, sway, spin, alpha = 1f, blink, grow = 1f;
        public float[] life = { 2f, 4f }, size = { 0.1f, 0.2f }, drift = { 0f, 0f };
        public int max = 100, order = 2;
        public bool lit = true;
        [NonSerialized] public HashSet<string> places;
        [NonSerialized] public Color[] palette;
        [NonSerialized] public ParticleSystem system;
        [NonSerialized] public readonly List<Vector2Int> cells = new();
        [NonSerialized] public readonly List<(Vector2 at, float radius)> spots = new(); // crowns of the trees in sight
        [NonSerialized] public readonly List<UnityEngine.Rendering.Universal.Light2D> glows = new();
        [NonSerialized] public ParticleSystem.Particle[] buffer;
        [NonSerialized] public float owed;
    }

    [Serializable] class MapProp { public string key, group; public int[] rect = new int[4]; }
    [Serializable] class MapPropFile { public MapProp[] props = new MapProp[0]; }

    [Serializable]
    class Settings
    {
        public float range = 14f, sampleEvery = 0.5f;
        public Effect[] effects = new Effect[0];
    }

    Settings data;
    float nextSample;
    int flowGeneration = -1;
    Vector2[] flow; // per grid tile: the way the water runs (rivers), zero elsewhere
    readonly System.Random rng = new();
    readonly Dictionary<string, string> obstacleTags = new(), terrainTags = new();
    readonly Dictionary<string, MapProp> mapProps = new(); // map_props.json by key: their group and size

    void Start()
    {
        var asset = Resources.Load<TextAsset>("Data/ambient");
        data = asset != null ? JsonUtility.FromJson<Settings>(asset.text) : new Settings();
        var props = Resources.Load<TextAsset>("Data/map_props");
        if (props != null) foreach (var p in JsonUtility.FromJson<MapPropFile>(props.text).props) mapProps[p.key] = p;
        foreach (var e in data.effects)
        {
            e.places = new HashSet<string>(e.where);
            var list = new List<Color>();
            foreach (var c in e.colors) if (ColorUtility.TryParseHtmlString(c, out var colour)) list.Add(colour);
            if (list.Count == 0) list.Add(Color.white);
            e.palette = list.ToArray();
            e.system = MakeSystem(e);
        }
    }

    ParticleSystem MakeSystem(Effect e)
    {
        var go = new GameObject("Particules " + e.key);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Max(1, e.max);
        main.startSpeed = 0f;
        main.startSize3D = true; // a sheet's frames keep their own width and height
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        if (e.sway > 0f)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = e.sway;
            noise.frequency = 0.6f;
            noise.scrollSpeed = 0.4f;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }
        // Fades in and out; fireflies blink on and off in between.
        var fade = ps.colorOverLifetime;
        fade.enabled = true;
        fade.color = new ParticleSystem.MinMaxGradient(Fade(e.blink));
        if (!Mathf.Approximately(e.grow, 1f))
        {
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, e.grow));
        }
        // Its sprites: a sheet of Resources/Effects (frames at random, or playing when it loops), else a shape.
        var frames = new List<Sprite>();
        var sheet = EffectLibrary.Get(e.sheet);
        if (sheet != null) for (int k = 0; k < sheet.frame_count; k++) frames.Add(sheet.Frame(k));
        else frames.Add(e.shape == "dot" ? Shapes.Circle : Shapes.Square);
        var tiles = ps.textureSheetAnimation;
        tiles.enabled = true;
        tiles.mode = ParticleSystemAnimationMode.Sprites;
        foreach (var f in frames) tiles.AddSprite(f);
        // A looping sheet plays over and over, one with durations plays once over the life (a bubble that bursts),
        // the others are variants (one at random).
        if (sheet != null && frames.Count > 1 && Total(sheet) > 0)
        {
            tiles.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            tiles.cycleCount = sheet.loop ? Mathf.Max(1, Mathf.RoundToInt(e.life[0] * 1000f / Mathf.Max(1, Total(sheet)))) : 1;
        }
        else
        {
            tiles.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            tiles.startFrame = new ParticleSystem.MinMaxCurve(0f, frames.Count - 0.01f);
        }
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = Resources.Load<Material>(e.lit ? "Materials/SpriteLit" : "Materials/SpriteUnlit");
        renderer.sortingOrder = e.order;
        ps.Play();
        return ps;
    }

    static int Total(EffectLibrary.Sheet sheet)
    {
        int total = 0;
        foreach (int d in sheet.frame_duration_ms) total += d;
        return total;
    }

    static Gradient Fade(float blink)
    {
        var alpha = new List<GradientAlphaKey> { new(0f, 0f) };
        int pulses = Mathf.Clamp(Mathf.RoundToInt(blink), 0, 3);
        if (pulses == 0)
        {
            alpha.Add(new GradientAlphaKey(1f, 0.15f));
            alpha.Add(new GradientAlphaKey(1f, 0.8f));
        }
        else
            for (int k = 0; k < pulses; k++)
            {
                alpha.Add(new GradientAlphaKey(1f, (k + 0.35f) / pulses));
                alpha.Add(new GradientAlphaKey(0.1f, (k + 0.85f) / pulses));
            }
        alpha.Add(new GradientAlphaKey(0f, 1f));
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, alpha.ToArray());
        return g;
    }

    void Update()
    {
        var grid = DungeonGenerator.Current;
        var cam = Camera.main;
        if (data == null || grid == null || cam == null) return;
        if (Time.time >= nextSample)
        {
            nextSample = Time.time + data.sampleEvery;
            Sample(grid, cam);
        }
        var phase = DayNight.Now(out _)?.key;
        foreach (var e in data.effects)
        {
            if (e.system == null) continue;
            bool now = e.phases.Length == 0 || Array.IndexOf(e.phases, phase) >= 0;
            int places = e.cells.Count + e.spots.Count;
            if (now && places > 0)
            {
                e.owed += e.density * places / 100f * Time.deltaTime;
                for (; e.owed >= 1f; e.owed -= 1f) Emit(e, grid);
            }
            else e.owed = 0f;
            if (!string.IsNullOrEmpty(e.light)) Glow(e);
        }
    }

    // Lights following some of its particles, as bright as they are (fireflies blink).
    void Glow(Effect e)
    {
        int count = Mathf.Min(e.system.particleCount, e.lights);
        if (e.buffer == null || e.buffer.Length < e.system.main.maxParticles) e.buffer = new ParticleSystem.Particle[e.system.main.maxParticles];
        int alive = e.system.GetParticles(e.buffer);
        while (e.glows.Count < e.lights)
        {
            var holder = new GameObject("Lueur " + e.key);
            holder.transform.SetParent(transform, false);
            var light = PointLights.Attach(holder, e.light);
            if (light == null) break;
            e.glows.Add(light);
        }
        for (int k = 0; k < e.glows.Count; k++)
        {
            var light = e.glows[k];
            if (light == null) continue;
            bool on = k < count && k < alive;
            PointLights.Want(light, on);
            if (!on) continue;
            light.transform.parent.position = e.buffer[k].position;
            PointLights.Scale(light, e.buffer[k].GetCurrentColor(e.system).a / 255f);
        }
    }

    // The tiles in sight (within range), sorted out by effect.
    void Sample(DungeonGenerator grid, Camera cam)
    {
        foreach (var e in data.effects) { e.cells.Clear(); e.spots.Clear(); }
        if (DungeonGenerator.Generation != flowGeneration) Flow(grid);
        float h = Mathf.Min(cam.orthographicSize + 1f, data.range), w = Mathf.Min(h * cam.aspect + 1f, data.range);
        Vector2 c = cam.transform.position;
        var from = grid.CellOf(c - new Vector2(w, h));
        var to = grid.CellOf(c + new Vector2(w, h));
        var plan = grid.island;
        var origin = plan != null ? grid.IslandOrigin - grid.Interior.min : Vector2Int.zero;
        string decor = plan == null && grid.tileset != null ? "decor:" + grid.tileset.romId : null;
        // The trees in sight: their crowns (a little above the middle of their sprite).
        if (plan != null)
        {
            Vector2 low = c - new Vector2(w, h), high = c + new Vector2(w, h);
            Vector2 zero = grid.CentreOf(0, 0) - Vector2.one * 0.5f;
            foreach (var (key, foot) in plan.decorProps)
            {
                if (!mapProps.TryGetValue(key, out var prop)) continue;
                Vector2 at = zero + foot - (Vector2)origin;
                if (at.x < low.x || at.y < low.y || at.x > high.x || at.y > high.y) continue;
                float width = prop.rect[2] / 24f, height = prop.rect[3] / 24f;
                foreach (var e in data.effects)
                    if (Array.IndexOf(e.props, prop.group) >= 0) e.spots.Add((at + Vector2.up * height * 0.65f, width * 0.35f));
            }
        }
        for (int y = from.y; y <= to.y; y++)
            for (int x = from.x; x <= to.x; x++)
            {
                if (x < 0 || y < 0 || x >= grid.Size || y >= grid.Size) continue;
                string obstacle = Obstacles.At(x, y)?.key, terrain = Terrain.At(grid.CentreOf(x, y))?.key;
                string ob = obstacle != null ? Tag(obstacleTags, "obstacle:", obstacle) : null;
                string te = terrain != null ? Tag(terrainTags, "terrain:", terrain) : null;
                string sub = null, theme = null;
                if (plan != null)
                {
                    int px = x + origin.x, py = y + origin.y;
                    if (px >= 0 && py >= 0 && px < plan.size && py < plan.size)
                    {
                        int i = plan.Index(px, py);
                        sub = plan.biome[i]?.key;
                        int r = plan.region[i];
                        theme = r >= 0 && r < 4 ? plan.themes[r]?.key : null;
                    }
                }
                bool ground = grid.Walkable(x, y);
                foreach (var e in data.effects)
                {
                    var p = e.places;
                    // The biome's effects on open ground; the obstacles' and terrains' on them.
                    if ((ob != null && p.Contains(ob)) || (te != null && p.Contains(te))
                        || (ground && ((sub != null && p.Contains(sub)) || (theme != null && p.Contains(theme)) || (decor != null && p.Contains(decor)))))
                        e.cells.Add(new Vector2Int(x, y));
                }
            }
    }

    static string Tag(Dictionary<string, string> cache, string prefix, string key)
    {
        if (!cache.TryGetValue(key, out var tag)) cache[key] = tag = prefix + key;
        return tag;
    }

    void Emit(Effect e, DungeonGenerator grid)
    {
        int pick = rng.Next(e.cells.Count + e.spots.Count);
        var cell = pick < e.cells.Count ? e.cells[pick] : new Vector2Int(-1, -1);
        Vector2 at;
        if (pick < e.cells.Count) at = grid.CentreOf(cell.x, cell.y) + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
        else
        {
            var spot = e.spots[pick - e.cells.Count];
            float turn = (float)rng.NextDouble() * Mathf.PI * 2f, reach = spot.radius * Mathf.Sqrt((float)rng.NextDouble());
            at = spot.at + new Vector2(Mathf.Cos(turn), Mathf.Sin(turn) * 0.6f) * reach;
        }
        float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
        Vector2 velocity = e.motion switch
        {
            "fall" => new Vector2(0f, -e.speed),
            "rise" => new Vector2(0f, e.speed),
            "drift" => new Vector2(e.drift.Length > 0 ? e.drift[0] : 0f, e.drift.Length > 1 ? e.drift[1] : 0f) + Climate.WindNow,
            "flow" => FlowAt(grid, cell) * e.speed,
            _ => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * e.speed,
        };
        // Still water barely moves.
        if (e.motion == "flow" && velocity == Vector2.zero) velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.05f;
        // A sheet keeps its own colours; a fallback shape takes one of the effect's.
        var sheet = EffectLibrary.Get(e.sheet);
        var colour = sheet != null ? Color.white : e.palette[rng.Next(e.palette.Length)];
        colour.a *= e.alpha;
        // A sheet at its own pixel size (sharp pixel art); a fallback shape at the effect's size.
        float size = Mathf.Lerp(e.size[0], e.size[Mathf.Min(1, e.size.Length - 1)], (float)rng.NextDouble());
        var size3 = sheet != null
            ? new Vector3(sheet.frame_width, sheet.frame_height, EffectLibrary.PixelsPerUnit) / EffectLibrary.PixelsPerUnit
            : new Vector3(size, size, 1f);
        // Foam lies along the current; spinning things start at any angle (particles turn clockwise).
        float rotation = e.motion == "flow" && velocity.sqrMagnitude > 0.01f ? -Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg
            : e.shape == "diamond" && sheet == null ? 45f : (float)rng.NextDouble() * (e.spin > 0f ? 360f : 0f);
        var p = new ParticleSystem.EmitParams
        {
            position = at,
            velocity = velocity,
            startLifetime = Mathf.Lerp(e.life[0], e.life[Mathf.Min(1, e.life.Length - 1)], (float)rng.NextDouble()),
            startSize3D = size3,
            startColor = colour,
            rotation = rotation,
            angularVelocity = e.spin * ((float)rng.NextDouble() * 2f - 1f),
        };
        e.system.Emit(p, 1);
    }

    // The way the water runs on each tile: away from the island's centre (the arena's), along the water, smoothed over
    // a few tiles around so that the current follows the bends of a river.
    void Flow(DungeonGenerator grid)
    {
        flowGeneration = DungeonGenerator.Generation;
        int n = grid.Size;
        flow = new Vector2[n * n];
        var plan = grid.island;
        Vector2 centre = plan != null ? plan.centre - (Vector2)(grid.IslandOrigin - grid.Interior.min) : grid.Interior.center;
        bool Water(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && Obstacles.At(x, y) is { } o && (o.key == "water" || o.key == "frozen" || o.Bridge);
        float Out(int x, int y) => Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                if (!Water(x, y)) continue;
                float here = Out(x, y);
                Vector2 sum = Vector2.zero;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                        if ((dx != 0 || dy != 0) && Water(x + dx, y + dy))
                            sum += new Vector2(dx, dy).normalized * (Out(x + dx, y + dy) - here);
                flow[y * n + x] = sum.sqrMagnitude > 0.01f ? sum.normalized : Vector2.zero;
            }
    }

    Vector2 FlowAt(DungeonGenerator grid, Vector2Int cell)
    {
        int n = grid.Size, i = cell.y * n + cell.x;
        return flow != null && i >= 0 && i < flow.Length ? flow[i] : Vector2.zero;
    }
}
