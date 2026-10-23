using System;
using System.Collections.Generic;
using UnityEngine;

/// The floating island a run takes place on (GDD, "L'île flottante"; Assets/Resources/Data/island.json and
/// biomes.json). Data only, one value per tile: four quarters around a central zone, each with a theme of its own
/// (four different ones per run) and played clockwise from a random first one; a ring of void around the central zone;
/// a barrier between two quarters (all played as walls: each zone is a map of its own, see passages). Inside a quarter, two noise fields (altitude, with a peak for a volcano or a
/// glacier, and moisture), ranked within the quarter, pick each tile's sub-biome. Big features (rivers, lakes,
/// cliffs), the drawing and the islet chains come next.
public partial class IslandPlan
{
    public const int Void = -1, Border = -2, Moat = -3, Centre = 4;

    [Serializable]
    public class Settings
    {
        public float radius = 190f, coastNoise = 0.1f, coastScale = 0.012f;
        public int voidMargin = 24;
        public float centreRatio = 0.22f;
        public int moat = 6, borderWidth = 6;
        public float borderNoise = 0.35f, fieldScale = 0.018f;
        public int octaves = 4;
    }

    [Serializable]
    public class SubBiome
    {
        public string key, name, color;
        public float[] alt = { 0f, 1f }, moist = { 0f, 1f };
        public int[] tilesets = new int[0];
        public float walls; // share taken by thickets or rock masses
    }

    [Serializable]
    public class Theme
    {
        public string key, name;
        public float peak, peakRadius = 0.2f;
        public SubBiome[] subbiomes = new SubBiome[0];
        // Big features (IslandFeatures.cs).
        public int[] rivers = { 1, 2 }, riverWidth = { 2, 3 }, lakes = { 1, 2 }, lakeSize = { 3, 6 };
        public string riverKind = "water", bridgeKind = "bridge", lakeKind = "water", crater;
        public float[] cliffLevels = new float[0];
    }

    [Serializable] class IslandFile { public Settings island = new(); }
    [Serializable] class BiomeFile { public Theme[] themes = new Theme[0]; }

    public int size; // the map is size × size tiles
    public int[] region; // Void, Border, Moat, Centre, or the quarter (0 to 3)
    public SubBiome[] biome; // per tile, null off the quarters
    public float[] altitude, moisture; // per tile, 0 to 1 (ranked within the quarter)
    public int[] decor; // per tile: a ROM décor drawing it instead of its sub-biome's (a road), 0 for none; may be null
    public readonly List<Vector2Int> markers = new(); // landmarks shown as passage ends that lead nowhere yet (test quarter's portals)
    public readonly List<Vector2Int> caves = new(); // cave mouths (test quarter's mountain top), leading nowhere yet
    public int[] height; // per tile: level of the ground (0 = plain, 1, 2... on hills), may be null
    public bool[] wallLook; // per tile: walkable but drawn as a wall (a ledge looks like its cliff), may be null
    public bool[] roof; // per tile: under a roof that hides it until the controlled Pokémon goes under (a cave), may be null
    public int roofDecor; // ROM décor whose walls draw the roof
    public readonly Theme[] themes = new Theme[4]; // per quarter
    public readonly int[] order = new int[4]; // quarters in the order they open (clockwise)
    public Vector2Int start; // where the team lands, on the first quarter's coast
    public Settings settings;
    float rotation; // degrees: where the first quarter starts

    /// The zones, each played as a map of its own (DungeonGenerator.islandZone): the quarters 0 to 3, the centre.
    public const int ZoneCount = 5;

    /// Name shown while a zone loads.
    public string ZoneName(int zone) => zone == Centre ? "Centre de l'île" : themes[zone].name;

    /// The tiles of a zone and a few around (its walls), within the map.
    public RectInt ZoneBounds(int zone)
    {
        int minX = size, minY = size, maxX = -1, maxY = -1;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                if (region[Index(x, y)] != zone) continue;
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
        const int Around = 3;
        minX = Mathf.Max(0, minX - Around); minY = Mathf.Max(0, minY - Around);
        maxX = Mathf.Min(size - 1, maxX + Around); maxY = Mathf.Min(size - 1, maxY + Around);
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    public int Index(int x, int y) => y * size + x;

    public static IslandPlan Generate(System.Random rng)
    {
        var plan = new IslandPlan();
        // System.Random's first draws follow its seed closely (seeds 1, 2, 3 give alike islands): stir it first.
        uint h = (uint)rng.Next();
        h ^= h >> 16; h *= 0x7feb352d; h ^= h >> 15; h *= 0x846ca68b; h ^= h >> 16;
        plan.Build(new System.Random((int)(h & 0x7fffffff)));
        return plan;
    }

    static T Load<T>(string path) where T : new()
    {
        var asset = Resources.Load<TextAsset>(path);
        return asset != null ? JsonUtility.FromJson<T>(asset.text) : new T();
    }

    void Build(System.Random rng)
    {
        settings = Load<IslandFile>("Data/island").island;
        var all = new List<Theme>(Load<BiomeFile>("Data/biomes").themes);
        var s = settings;
        size = Mathf.CeilToInt(2f * (s.radius * (1f + s.coastNoise) + s.voidMargin));
        int n = size * size;
        region = new int[n];
        biome = new SubBiome[n];
        altitude = new float[n];
        moisture = new float[n];

        // Four different themes; the quarters open clockwise from a random one.
        for (int q = 0; q < 4; q++)
        {
            var t = all[rng.Next(all.Count)];
            all.Remove(t);
            themes[q] = t;
        }
        int first = rng.Next(4);
        for (int k = 0; k < 4; k++) order[k] = (first - k + 4) % 4; // quarter angles grow anticlockwise
        rotation = (float)rng.NextDouble() * 90f;
        var offsets = new Vector2[6];
        for (int i = 0; i < offsets.Length; i++) offsets[i] = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);

        float c = size / 2f;
        // Peaks: in the middle of each quarter, two thirds of the way to the coast.
        var peaks = new Vector2[4];
        for (int q = 0; q < 4; q++)
        {
            float a = (rotation + q * 90f + 45f) * Mathf.Deg2Rad;
            peaks[q] = new Vector2(c, c) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s.radius * 0.6f;
        }

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = Index(x, y);
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / s.radius;
                float coast = 1f + s.coastNoise * (Fbm(x * s.coastScale, y * s.coastScale, offsets[0], 3) * 2f - 1f);
                if (r > coast) { region[i] = Void; continue; }
                float centre = s.centreRatio * (1f + 0.15f * (Fbm(x * 0.03f, y * 0.03f, offsets[1], 2) - 0.5f));
                if (r < centre) { region[i] = Centre; continue; }
                if (r < centre + s.moat / s.radius) { region[i] = Moat; continue; }
                // Quarter by angle, its edges wavering; a barrier along them.
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - rotation;
                angle += s.borderNoise * 90f * (Fbm(x * 0.01f, y * 0.01f, offsets[2], 3) - 0.5f);
                angle = Mathf.Repeat(angle, 360f);
                int q = Mathf.FloorToInt(angle / 90f) % 4;
                float toEdge = Mathf.Min(angle - q * 90f, (q + 1) * 90f - angle) * Mathf.Deg2Rad * r * s.radius;
                if (toEdge < s.borderWidth / 2f) { region[i] = Border; continue; }
                region[i] = q;
                // Fields.
                float alt = Fbm(x * s.fieldScale, y * s.fieldScale, offsets[3], s.octaves);
                var t = themes[q];
                if (t.peak > 0f)
                {
                    float d = Vector2.Distance(new Vector2(x, y), peaks[q]) / (t.peakRadius * s.radius);
                    alt += t.peak * Mathf.Clamp01(1f - d);
                }
                alt -= 0.25f * Mathf.Clamp01((r - 0.85f) / 0.15f); // lower near the coast
                altitude[i] = alt;
                moisture[i] = Fbm(x * s.fieldScale * 1.3f, y * s.fieldScale * 1.3f, offsets[4], s.octaves);
            }

        // Ranked within each quarter, so that every quarter spreads over its whole range of sub-biomes.
        for (int q = 0; q < 4; q++)
        {
            Rank(altitude, q);
            Rank(moisture, q);
        }
        for (int i = 0; i < n; i++)
            if (region[i] >= 0 && region[i] < 4) biome[i] = Pick(themes[region[i]], altitude[i], moisture[i]);

        // The landing point: on the first quarter's coast, in the middle of its arc.
        float landing = (rotation + order[0] * 90f + 45f) * Mathf.Deg2Rad;
        var dir = new Vector2(Mathf.Cos(landing), Mathf.Sin(landing));
        start = Vector2Int.RoundToInt(new Vector2(c, c) + dir * s.radius * 0.95f);
        for (int k = 0; k < 40 && (Outside(start) || region[Index(start.x, start.y)] != order[0]); k++)
            start = Vector2Int.RoundToInt(new Vector2(c, c) + dir * s.radius * (0.95f - k * 0.02f));
        BuildFeatures(rng, offsets[5]);
    }

    bool Outside(Vector2Int p) => p.x < 0 || p.y < 0 || p.x >= size || p.y >= size;

    void Rank(float[] field, int q)
    {
        var cells = new List<int>();
        for (int i = 0; i < field.Length; i++)
            if (region[i] == q) cells.Add(i);
        cells.Sort((a, b) => field[a].CompareTo(field[b]));
        for (int k = 0; k < cells.Count; k++) field[cells[k]] = cells.Count > 1 ? k / (float)(cells.Count - 1) : 0f;
    }

    static SubBiome Pick(Theme t, float alt, float moist)
    {
        foreach (var b in t.subbiomes)
            if (alt >= b.alt[0] && alt <= b.alt[1] && moist >= b.moist[0] && moist <= b.moist[1]) return b;
        return t.subbiomes.Length > 0 ? t.subbiomes[^1] : null;
    }

    // Fractal noise, 0 to 1.
    static float Fbm(float x, float y, Vector2 offset, int octaves)
    {
        float sum = 0f, amp = 1f, total = 0f, freq = 1f;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Mathf.PerlinNoise(x * freq + offset.x, y * freq + offset.y);
            total += amp;
            amp *= 0.5f;
            freq *= 2f;
        }
        return sum / total;
    }
}
