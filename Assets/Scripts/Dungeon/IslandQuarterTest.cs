using System;
using System.Collections.Generic;
using UnityEngine;

/// A test quarter of island (Assets/Resources/Data/quarter_test.json; sandbox room 5), to work out how a quarter is
/// composed: one big open zone without walls, fan-shaped (its point, bottom left, stands for the centre of the island,
/// its arc for the coast), crossed by a wide winding river from a spring near the point to the coast, a sub-biome on
/// each side of it, a few bridges. Filled into an IslandPlan (a single quarter), so DungeonGenerator and IslandRun draw
/// and play it as an island zone.
public partial class IslandPlan
{
    [Serializable]
    public class QuarterTest
    {
        public int seed;
        public string themeName = "Nature luxuriante";
        public float radius = 120f, coastNoise = 0.1f, coastScale = 0.025f, sideNoise = 5f;
        public int voidMargin = 16;
        public float sourceDistance = 10f, sourceRadius = 6f;
        public float[] riverWidth = { 6f, 9f };
        public float widthNoise = 1.5f, meanderAmplitude = 14f, meanderWavelength = 60f, wobble = 0.35f;
        public int bridges = 3, bridgeWidth = 2;
        public string bridgeKind = "bridge", riverKind = "water";
        public SubBiome left = new(), right = new();
    }

    [Serializable] class QuarterTestFile { public QuarterTest test = new(); }

    /// The test quarter from quarter_test.json; a random seed when its seed is 0.
    public static IslandPlan QuarterTestPlan()
    {
        var asset = Resources.Load<TextAsset>("Data/quarter_test");
        var t = asset != null ? JsonUtility.FromJson<QuarterTestFile>(asset.text).test : new QuarterTest();
        var plan = new IslandPlan();
        plan.BuildQuarterTest(t, new System.Random(t.seed != 0 ? t.seed : Environment.TickCount));
        return plan;
    }

    void BuildQuarterTest(QuarterTest t, System.Random rng)
    {
        settings = new Settings { radius = t.radius, coastNoise = t.coastNoise, voidMargin = t.voidMargin };
        var theme = new Theme
        {
            key = "test", name = t.themeName, subbiomes = new[] { t.left, t.right },
            riverKind = t.riverKind, bridgeKind = t.bridgeKind, lakeKind = t.riverKind,
        };
        for (int q = 0; q < 4; q++) { themes[q] = theme; order[q] = q; }

        size = Mathf.CeilToInt(t.radius * (1f + t.coastNoise) + 2 * t.voidMargin);
        int n = size * size;
        region = new int[n];
        biome = new SubBiome[n];
        altitude = new float[n];
        moisture = new float[n];
        obstacle = new string[n];
        wall = new bool[n];
        var offsets = new Vector2[4];
        for (int i = 0; i < offsets.Length; i++) offsets[i] = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);

        // The fan: from the point, between the two sides (wavering), up to the coast.
        var point = new Vector2(t.voidMargin, t.voidMargin);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - point.x, dy = y + 0.5f - point.y;
                float coast = t.radius * (1f + t.coastNoise * (Fbm(x * t.coastScale, y * t.coastScale, offsets[0], 3) * 2f - 1f));
                bool land = dx * dx + dy * dy < coast * coast
                    && dx > t.sideNoise * Fbm(0f, y * 0.04f, offsets[1], 2) && dy > t.sideNoise * Fbm(x * 0.04f, 0f, offsets[2], 2);
                region[Index(x, y)] = land ? 0 : Void;
            }

        // The river's middle line: along the fan's middle (45°), swinging from side to side, a little at the spring and
        // fully further down; it carries on past the coast so that it pours into the sky.
        var along = new Vector2(1f, 1f).normalized;
        var across = new Vector2(-along.y, along.x); // left of the flow
        float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
        float Offset(float d)
        {
            float swing = Mathf.Sin(d / t.meanderWavelength * Mathf.PI * 2f + phase) * (1f - t.wobble)
                + (Fbm(d * 0.03f, 0f, offsets[3], 2) * 2f - 1f) * t.wobble;
            return t.meanderAmplitude * swing * Mathf.Clamp01((d - t.sourceDistance) / 30f + 0.25f);
        }
        float end = t.radius * 1.25f;
        var line = new List<(Vector2 at, float width)>();
        for (float d = t.sourceDistance; d <= end; d += 0.5f)
        {
            float share = Mathf.Clamp01((d - t.sourceDistance) / (t.radius - t.sourceDistance));
            float width = Mathf.Lerp(t.riverWidth[0], t.riverWidth[Mathf.Min(1, t.riverWidth.Length - 1)], share)
                + (Fbm(d * 0.05f, 7f, offsets[3], 2) * 2f - 1f) * t.widthNoise;
            line.Add((point + along * d + across * Offset(d), width));
        }
        foreach (var (at, width) in line) Stamp(at, width / 2f, i => { if (region[i] == 0) obstacle[i] = t.riverKind; });
        Stamp(line[0].at, t.sourceRadius, i => { if (region[i] == 0) obstacle[i] = t.riverKind; }); // the spring's pond

        // A sub-biome on each side of the river: left or right of its middle line.
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = Index(x, y);
                if (region[i] != 0) continue;
                var p = new Vector2(x + 0.5f, y + 0.5f) - point;
                float d = Vector2.Dot(p, along);
                biome[i] = Vector2.Dot(p, across) - Offset(Mathf.Max(d, t.sourceDistance)) > 0f ? t.left : t.right;
            }

        // Bridges spread along the river: a straight line of tiles across it, one row or column per tile of width,
        // going left to right where the river runs up and down there, up and down where it runs left to right.
        for (int b = 0; b < t.bridges; b++)
        {
            float d = Mathf.Lerp(t.sourceDistance + t.sourceRadius + 12f, t.radius * 0.85f, t.bridges == 1 ? 0.5f : b / (t.bridges - 1f));
            var middle = Vector2Int.FloorToInt(point + along * d + across * Offset(d));
            var flow = along * 2f + across * (Offset(d + 1f) - Offset(d - 1f));
            bool horizontal = Mathf.Abs(flow.y) >= Mathf.Abs(flow.x);
            for (int k = 0; k < t.bridgeWidth; k++)
            {
                var start = middle + (horizontal ? new Vector2Int(0, k) : new Vector2Int(k, 0));
                var step = horizontal ? Vector2Int.right : Vector2Int.up;
                foreach (int dir in new[] { 1, -1 })
                    for (var c = start; InMap(c.x, c.y) && region[Index(c.x, c.y)] == 0 && obstacle[Index(c.x, c.y)] != null; c += step * dir)
                        obstacle[Index(c.x, c.y)] = t.bridgeKind;
            }
        }

        // The team lands on the left bank, a little way from the spring.
        float landing = t.sourceDistance + t.sourceRadius + 6f;
        start = Vector2Int.FloorToInt(point + along * landing + across * (Offset(landing) + t.riverWidth[0]));
        for (int k = 0; k < 30 && (!InMap(start.x, start.y) || !Walkable(Index(start.x, start.y))); k++) start += new Vector2Int(-1, 1);
    }
}
