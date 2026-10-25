using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// Point lights (Resources/Data/lights.json; GDD, "Jour et nuit"), added by PlayerController: halos of the Fire
/// Pokémon (and a few others by id), of the Fire and Electric shots and their impacts, of the lava and embers near the
/// camera, and of the things that ask for one (Attach: braziers, cave mark and mouth, stairs, pylon arcs). Their
/// intensity follows the night (DayNight.Darkness); only the nearest maxLights within range of the camera shine.
/// L in devMode shows how many.
public class PointLights : MonoBehaviour
{
    [Serializable]
    public class Glow
    {
        public string key, color = "#ffffff";
        public int type = -1, id;
        public float radius = 2f, intensity = 1f, flicker;
        [NonSerialized] public Color colour;
    }

    [Serializable]
    class Settings
    {
        public float dayShare = 0.15f, range = 16f, scanEvery = 0.25f, flashTime = 0.3f;
        public int maxLights = 48, lavaGrid = 3;
        public Glow[] pokemonTypes = new Glow[0], pokemon = new Glow[0], moves = new Glow[0], obstacles = new Glow[0], terrains = new Glow[0];
        public Glow brazier, caveMark, caveMouth, stairs, arc;
    }

    class Entry
    {
        public Light2D light;
        public Glow glow;
        public bool wanted = true;
        public float seed, dieAt = -1f, born;
    }

    static Settings settings;
    static readonly List<Entry> entries = new();
    static readonly Dictionary<Light2D, Entry> byLight = new();

    readonly Dictionary<Combatant, (Light2D light, Glow glow)> bodies = new();
    readonly HashSet<Projectile> shots = new();
    readonly Dictionary<Vector2Int, Light2D> lavaLights = new(); // by block: a block keeps its light while the camera moves
    readonly List<Light2D> lavaFree = new();
    readonly HashSet<Vector2Int> lavaSeen = new();
    float nextScan;
    bool showCount;
    int shining;

    static Settings Data
    {
        get
        {
            if (settings != null) return settings;
            var asset = Resources.Load<TextAsset>("Data/lights");
            settings = asset != null ? JsonUtility.FromJson<Settings>(asset.text) : new Settings();
            foreach (var list in new[] { settings.pokemonTypes, settings.pokemon, settings.moves, settings.obstacles, settings.terrains })
                foreach (var g in list) Parse(g);
            foreach (var g in new[] { settings.brazier, settings.caveMark, settings.caveMouth, settings.stairs, settings.arc }) Parse(g);
            return settings;
        }
    }

    static void Parse(Glow g)
    {
        if (g != null && !ColorUtility.TryParseHtmlString(g.color, out g.colour)) g.colour = Color.white;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        settings = null;
        entries.Clear();
        byLight.Clear();
    }

    /// A light on that object (offset in its space) for one of the named glows (brazier, caveMark, caveMouth, stairs,
    /// arc); null when it has none. Switch it with Want.
    public static Light2D Attach(GameObject on, string name, Vector2 offset = default)
    {
        var d = Data;
        var glow = name switch { "brazier" => d.brazier, "caveMark" => d.caveMark, "caveMouth" => d.caveMouth, "stairs" => d.stairs, "arc" => d.arc, _ => null };
        return glow != null && on != null ? Make(on.transform, glow, offset) : null;
    }

    /// Whether that light should shine now (a brazier lit, an arc active).
    public static void Want(Light2D light, bool wanted)
    {
        if (light != null && byLight.TryGetValue(light, out var e)) e.wanted = wanted;
    }

    static Light2D Make(Transform parent, Glow glow, Vector2 offset, float life = 0f)
    {
        var go = new GameObject("Lumière");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = offset;
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = glow.colour;
        light.pointLightOuterRadius = glow.radius;
        light.pointLightInnerRadius = glow.radius * 0.15f;
        light.falloffIntensity = 0.6f;
        light.intensity = 0f;
        var e = new Entry { light = light, glow = glow, seed = UnityEngine.Random.value * 100f, born = Time.time, dieAt = life > 0f ? Time.time + life : -1f };
        entries.Add(e);
        byLight[light] = e;
        return light;
    }

    static Glow Find(Glow[] list, Predicate<Glow> fits)
    {
        foreach (var g in list) if (fits(g)) return g;
        return null;
    }

    void OnEnable() => Obstacles.Hit += OnHit;
    void OnDisable() => Obstacles.Hit -= OnHit;

    // A Fire or Electric move lands: a brief flash where it hits.
    void OnHit(MoveDef move, Vector2 at, float radius, Combatant caster)
    {
        var glow = Find(Data.moves, g => g.type == move.type);
        if (glow == null || Camera.main == null || Vector2.Distance(at, Camera.main.transform.position) > Data.range) return;
        var holder = new GameObject("Éclat lumineux");
        holder.transform.position = at;
        Make(holder.transform, glow, Vector2.zero, Data.flashTime);
        Destroy(holder, Data.flashTime + 0.05f);
    }

    void FixedUpdate()
    {
        if (Time.time < nextScan) return;
        nextScan = Time.time + Data.scanEvery;
        var cam = Camera.main;
        if (cam == null) return;
        Vector2 centre = cam.transform.position;
        float range = Data.range;
        // Pokémon: by id first, then by type; dropped when gone, knocked out or far.
        foreach (var c in FindObjectsByType<Combatant>(FindObjectsSortMode.None))
        {
            bool near = c.IsAlive && Vector2.Distance(c.transform.position, centre) <= range;
            var stats = c.Stats;
            var glow = !near || stats == null ? null
                : Find(Data.pokemon, g => g.id == stats.id) ?? Find(Data.pokemonTypes, g => stats.types != null && Array.IndexOf(stats.types, g.type) >= 0);
            bodies.TryGetValue(c, out var had);
            if (had.glow == glow && (glow == null || had.light != null)) continue;
            if (had.light != null) Destroy(had.light.gameObject);
            if (glow == null) bodies.Remove(c);
            else bodies[c] = (Make(c.transform, glow, new Vector2(0f, 0.3f)), glow);
        }
        foreach (var gone in new List<Combatant>(bodies.Keys)) if (gone == null) bodies.Remove(gone);
        // Shots of the glowing types.
        foreach (var shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None))
        {
            if (shots.Contains(shot) || shot.Move == null) continue;
            shots.Add(shot);
            var glow = Find(Data.moves, g => g.type == shot.Move.type);
            if (glow != null) Make(shot.transform, glow, Vector2.zero);
        }
        shots.RemoveWhere(s => s == null);
        ScanLava(centre, range);
    }

    // One light per block of lavaGrid × lavaGrid tiles holding lava or embers, around the camera (a pool).
    void ScanLava(Vector2 centre, float range)
    {
        var grid = DungeonGenerator.Current;
        lavaSeen.Clear();
        if (grid != null && (Data.obstacles.Length > 0 || Data.terrains.Length > 0))
        {
            int step = Mathf.Max(1, Data.lavaGrid);
            var from = grid.CellOf(centre - Vector2.one * range);
            var to = grid.CellOf(centre + Vector2.one * range);
            for (int bx = from.x - Mod(from.x, step); bx <= to.x; bx += step)
                for (int by = from.y - Mod(from.y, step); by <= to.y; by += step)
                {
                    // The light sits in the middle of the block's glowing tiles, not of the block.
                    Glow glow = null;
                    Vector2 sum = Vector2.zero;
                    int glowing = 0;
                    for (int x = bx; x < bx + step; x++)
                        for (int y = by; y < by + step; y++)
                        {
                            Glow here = null;
                            var o = Obstacles.At(x, y);
                            if (o != null) here = Find(Data.obstacles, g => g.key == o.key);
                            if (here == null && Terrain.At(grid.CentreOf(x, y)) is { } t) here = Find(Data.terrains, g => g.key == t.key);
                            if (here == null) continue;
                            glow ??= here;
                            sum += grid.CentreOf(x, y);
                            glowing++;
                        }
                    if (glow == null) continue;
                    var block = new Vector2Int(bx, by);
                    lavaSeen.Add(block);
                    if (!lavaLights.TryGetValue(block, out var light) || light == null)
                    {
                        light = null;
                        while (light == null && lavaFree.Count > 0)
                        {
                            light = lavaFree[^1];
                            lavaFree.RemoveAt(lavaFree.Count - 1);
                        }
                        if (light == null) light = Make(null, glow, Vector2.zero); // at the scene's root: this object follows the team
                        lavaLights[block] = light;
                    }
                    var e = byLight[light];
                    e.glow = glow;
                    e.wanted = true;
                    light.color = glow.colour;
                    light.pointLightOuterRadius = glow.radius;
                    light.transform.position = sum / glowing;
                }
        }
        // Blocks out of range or without lava any more give their light back.
        var gone = new List<Vector2Int>();
        foreach (var (block, light) in lavaLights)
            if (!lavaSeen.Contains(block)) gone.Add(block);
        foreach (var block in gone)
        {
            var light = lavaLights[block];
            lavaLights.Remove(block);
            if (light == null) continue;
            Want(light, false);
            lavaFree.Add(light);
        }
    }

    static int Mod(int a, int m) => ((a % m) + m) % m;

    void Update()
    {
        if (SaveData.Settings.devMode && Keyboard.current is { } kb && kb.lKey.wasPressedThisFrame) showCount = !showCount;
        var cam = Camera.main;
        Vector2 centre = cam != null ? (Vector2)cam.transform.position : Vector2.zero;
        float strength = Mathf.Lerp(Data.dayShare, 1f, DayNight.Darkness);
        // The nearest wanted ones shine, up to maxLights.
        entries.RemoveAll(e => e.light == null);
        foreach (var e in entries)
            if (e.dieAt > 0f && Time.time >= e.dieAt) e.wanted = false;
        entries.Sort((a, b) => Distance(a, centre).CompareTo(Distance(b, centre)));
        shining = 0;
        foreach (var e in entries)
        {
            bool on = e.wanted && shining < Data.maxLights && Distance(e, centre) <= Data.range + e.glow.radius;
            if (e.light.enabled != on) e.light.enabled = on;
            if (!on) continue;
            shining++;
            float wave = 1f - e.glow.flicker * Mathf.PerlinNoise(e.seed, Time.time * 8f);
            float fade = e.dieAt > 0f ? Mathf.Clamp01((e.dieAt - Time.time) / Mathf.Max(0.01f, e.dieAt - e.born)) : 1f;
            e.light.intensity = e.glow.intensity * strength * wave * fade;
        }
        foreach (var gone in new List<Light2D>(byLight.Keys)) if (gone == null) byLight.Remove(gone);
    }

    static float Distance(Entry e, Vector2 centre) => Vector2.Distance(e.light.transform.position, centre);

    void OnGUI()
    {
        if (!showCount) return;
        GUI.Label(new Rect(10, Screen.height - 30, 400, 24), $"Lumières : {shining} / {entries.Count} (max {Data.maxLights}) · obscurité {DayNight.Darkness:0.00}");
    }
}
