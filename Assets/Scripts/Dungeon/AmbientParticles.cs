using System;
using System.Collections.Generic;
using UnityEngine;

/// Ambient particles (Resources/Data/ambient.json; GDD, "Jour et nuit"), added by PlayerController: fireflies at night
/// in the woods, falling leaves, petals, embers over the lava, snow, dust, the current of the rivers... Every
/// sampleEvery seconds the tiles in sight are sorted by what each effect wants (sub-biome, theme, décor, obstacle,
/// terrain); each effect is then born on its own tiles at its density, during its phases of the day. A river's
/// current runs down the water towards the sky at the coast (Flow).
public class AmbientParticles : MonoBehaviour
{
    [Serializable]
    class Effect
    {
        public string key, motion = "wander", sheet = "", shape = "dot";
        public string[] where = new string[0], phases = new string[0], colors = new string[0];
        public float density = 1f, speed = 0.5f, sway, spin, alpha = 1f, blink, grow = 1f;
        public float[] life = { 2f, 4f }, size = { 0.1f, 0.2f }, drift = { 0f, 0f };
        public int max = 100, order = 2;
        public bool lit = true;
        [NonSerialized] public HashSet<string> places;
        [NonSerialized] public Color[] palette;
        [NonSerialized] public ParticleSystem system;
        [NonSerialized] public readonly List<Vector2Int> cells = new();
        [NonSerialized] public float owed;
    }

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

    void Start()
    {
        var asset = Resources.Load<TextAsset>("Data/ambient");
        data = asset != null ? JsonUtility.FromJson<Settings>(asset.text) : new Settings();
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
        if (sheet != null && sheet.loop && frames.Count > 1)
        {
            tiles.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            tiles.cycleCount = Mathf.Max(1, Mathf.RoundToInt(e.life[0] * 1000f / Mathf.Max(1, Total(sheet))));
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
            if (!now || e.cells.Count == 0) { e.owed = 0f; continue; }
            e.owed += e.density * e.cells.Count / 100f * Time.deltaTime;
            for (; e.owed >= 1f; e.owed -= 1f) Emit(e, grid);
        }
    }

    // The tiles in sight (within range), sorted out by effect.
    void Sample(DungeonGenerator grid, Camera cam)
    {
        foreach (var e in data.effects) e.cells.Clear();
        if (DungeonGenerator.Generation != flowGeneration) Flow(grid);
        float h = Mathf.Min(cam.orthographicSize + 1f, data.range), w = Mathf.Min(h * cam.aspect + 1f, data.range);
        Vector2 c = cam.transform.position;
        var from = grid.CellOf(c - new Vector2(w, h));
        var to = grid.CellOf(c + new Vector2(w, h));
        var plan = grid.island;
        var origin = plan != null ? grid.IslandOrigin - grid.Interior.min : Vector2Int.zero;
        string decor = plan == null && grid.tileset != null ? "decor:" + grid.tileset.romId : null;
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
        var cell = e.cells[rng.Next(e.cells.Count)];
        Vector2 at = grid.CentreOf(cell.x, cell.y) + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f);
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
        var colour = EffectLibrary.Get(e.sheet) != null ? Color.white : e.palette[rng.Next(e.palette.Length)];
        colour.a *= e.alpha;
        var p = new ParticleSystem.EmitParams
        {
            position = at,
            velocity = velocity,
            startLifetime = Mathf.Lerp(e.life[0], e.life[Mathf.Min(1, e.life.Length - 1)], (float)rng.NextDouble()),
            startSize = Mathf.Lerp(e.size[0], e.size[Mathf.Min(1, e.size.Length - 1)], (float)rng.NextDouble()),
            startColor = colour,
            rotation = e.shape == "diamond" && EffectLibrary.Get(e.sheet) == null ? 45f : (float)rng.NextDouble() * (e.spin > 0f ? 360f : 0f),
            angularVelocity = e.spin * ((float)rng.NextDouble() * 2f - 1f),
        };
        e.system.Emit(p, 1);
    }

    // The way the water runs on each tile: down to where it pours (the sky at the coast), along the water. Lakes and
    // pools that pour nowhere stay still.
    void Flow(DungeonGenerator grid)
    {
        flowGeneration = DungeonGenerator.Generation;
        int n = grid.Size;
        flow = new Vector2[n * n];
        var dist = new int[n * n];
        var queue = new Queue<int>();
        bool Water(int x, int y) => x >= 0 && y >= 0 && x < n && y < n && Obstacles.At(x, y) is { } o && (o.key == "water" || o.key == "frozen" || o.Bridge);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                dist[i] = int.MaxValue;
                if (!Water(x, y)) continue;
                if (grid.IsSky(x + 1, y) || grid.IsSky(x - 1, y) || grid.IsSky(x, y + 1) || grid.IsSky(x, y - 1))
                {
                    dist[i] = 0;
                    queue.Enqueue(i);
                }
            }
        var steps = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            int cx = c % n, cy = c / n;
            foreach (var d in steps)
            {
                int x = cx + d.x, y = cy + d.y;
                if (!Water(x, y)) continue;
                int k = y * n + x;
                if (dist[k] <= dist[c] + 1) continue;
                dist[k] = dist[c] + 1;
                queue.Enqueue(k);
            }
        }
        // Downhill along the distance, smoothed over a few tiles around so that the current bends gently.
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                if (dist[i] == int.MaxValue) continue;
                Vector2 sum = Vector2.zero;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int qx = x + dx, qy = y + dy;
                        if ((dx == 0 && dy == 0) || qx < 0 || qy < 0 || qx >= n || qy >= n) continue;
                        int q = qy * n + qx;
                        if (dist[q] == int.MaxValue) continue;
                        sum += new Vector2(dx, dy).normalized * (dist[i] - dist[q]);
                    }
                flow[i] = sum.sqrMagnitude > 0.01f ? sum.normalized : Vector2.zero;
            }
    }

    Vector2 FlowAt(DungeonGenerator grid, Vector2Int cell)
    {
        int n = grid.Size, i = cell.y * n + cell.x;
        return flow != null && i >= 0 && i < flow.Length ? flow[i] : Vector2.zero;
    }
}
