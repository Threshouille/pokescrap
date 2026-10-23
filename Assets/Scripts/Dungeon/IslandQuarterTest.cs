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
        public Road road = new();
        public Relief relief = new();
    }

    /// Hills, plateaus and a mountain: levels of ground whose edges are cliffs (walls), crossed by ramps and, going
    /// down only, by ledges; a cave at the top of the mountain, a branch of the road to its foot.
    [Serializable]
    public class Relief
    {
        public int hills = 6;
        public float[] hillRadius = { 5f, 9f };
        public float upperChance = 0.5f, mountainRadius = 24f;
        public int mountainLevels = 3, clearance = 3;
        public int[] rockDecors = { 49, 9 };
        public int rampDecor = 35, rampWidth = 3, ledges = 4;
        public int[] ledgeLength = { 3, 6 };
        public float[] levelShade = { 0.93f, 0.96f, 0.98f, 1f }, footShadow = { 0.7f, 0.85f };
    }

    /// The test quarter's relief settings (tints), kept for the drawing.
    [NonSerialized] public Relief relief;

    /// The main road: from the portal to the previous quarter (left side) over the middle bridge to the portal to the
    /// next one (bottom side); earth with stones scattered over it, broken columns along it.
    [Serializable]
    public class Road
    {
        public float width = 3f, portalShare = 0.55f, wander = 5f;
        public int earthDecor = 35, stoneDecor = 30;
        public float stoneShare = 0.15f;
        public string column = "column";
        public float[] columnSpacing = { 9f, 15f };
        public float pairChance = 0.3f, columnOffset = 1.2f;
    }

    [Serializable] class QuarterTestFile { public QuarterTest test = new(); }

    /// Seed of the last test quarter drawn (to draw it again with another rock).
    public static int LastQuarterSeed { get; private set; }

    /// The test quarter from quarter_test.json: seed (0: the file's, or a new one when that is 0 too); rock: which of
    /// relief.rockDecors draws the cliffs.
    public static IslandPlan QuarterTestPlan(int seed = 0, int rock = 0)
    {
        var asset = Resources.Load<TextAsset>("Data/quarter_test");
        var t = asset != null ? JsonUtility.FromJson<QuarterTestFile>(asset.text).test : new QuarterTest();
        if (seed == 0) seed = t.seed != 0 ? t.seed : Environment.TickCount;
        LastQuarterSeed = seed;
        var plan = new IslandPlan();
        var rng = new System.Random(seed);
        plan.BuildQuarterTest(t, rng);
        plan.LayRelief(t, rng, rock);
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

        var bridges = new List<(List<Vector2Int> tiles, bool horizontal)>();
        // Bridges spread along the river: a straight line of tiles across it, one row or column per tile of width,
        // going left to right where the river runs up and down there, up and down where it runs left to right.
        for (int b = 0; b < t.bridges; b++)
        {
            float d = Mathf.Lerp(t.sourceDistance + t.sourceRadius + 12f, t.radius * 0.85f, t.bridges == 1 ? 0.5f : b / (t.bridges - 1f));
            var middle = Vector2Int.FloorToInt(point + along * d + across * Offset(d));
            var flow = along * 2f + across * (Offset(d + 1f) - Offset(d - 1f));
            bool horizontal = Mathf.Abs(flow.y) >= Mathf.Abs(flow.x);
            var tiles = new List<Vector2Int>();
            bridges.Add((tiles, horizontal));
            for (int k = 0; k < t.bridgeWidth; k++)
            {
                var start = middle + (horizontal ? new Vector2Int(0, k) : new Vector2Int(k, 0));
                var step = horizontal ? Vector2Int.right : Vector2Int.up;
                foreach (int dir in new[] { 1, -1 })
                    for (var c = start; InMap(c.x, c.y) && region[Index(c.x, c.y)] == 0 && obstacle[Index(c.x, c.y)] != null; c += step * dir)
                    {
                        obstacle[Index(c.x, c.y)] = t.bridgeKind;
                        if (!tiles.Contains(c)) tiles.Add(c);
                    }
            }
        }

        // The team lands on the left bank, a little way from the spring; by the first portal when there is a road.
        float landing = t.sourceDistance + t.sourceRadius + 6f;
        start = Vector2Int.FloorToInt(point + along * landing + across * (Offset(landing) + t.riverWidth[0]));
        for (int k = 0; k < 30 && (!InMap(start.x, start.y) || !Walkable(Index(start.x, start.y))); k++) start += new Vector2Int(-1, 1);
        if (bridges.Count > 0 && bridges[bridges.Count / 2].tiles.Count > 0) LayRoad(t, rng, point, bridges[bridges.Count / 2]);
    }

    // The road: a smooth line from each portal to its end of the middle bridge, through a few points pushed aside at
    // random; its tiles earth with a few stones, broken columns on its sides now and then.
    void LayRoad(QuarterTest t, System.Random rng, Vector2 point, (List<Vector2Int> tiles, bool horizontal) bridge)
    {
        var r = t.road;
        decor = new int[size * size];
        // The portals: on each straight side, a share of the way from the point, two tiles inside the land.
        Vector2Int Inside(Vector2Int from, Vector2Int step)
        {
            var c = from;
            for (int k = 0; k < size && InMap(c.x, c.y) && region[Index(c.x, c.y)] != 0; k++) c += step;
            return c + step * 2;
        }
        int along = Mathf.RoundToInt(point.x + t.radius * r.portalShare);
        var toPrevious = Inside(new Vector2Int(0, along), Vector2Int.right); // left side: jungle
        var toNext = Inside(new Vector2Int(along, 0), Vector2Int.up); // bottom side: meadow
        markers.Add(toPrevious);
        markers.Add(toNext);

        // The bridge's two ends, a tile past its last planks; the one on the left sub-biome's side joins the first portal.
        var tiles = bridge.tiles;
        var axis = bridge.horizontal ? Vector2Int.right : Vector2Int.up;
        Vector2Int low = tiles[0], high = tiles[0];
        foreach (var c in tiles)
        {
            if (Vector2.Dot(c, (Vector2)axis) < Vector2.Dot(low, (Vector2)axis)) low = c;
            if (Vector2.Dot(c, (Vector2)axis) > Vector2.Dot(high, (Vector2)axis)) high = c;
        }
        var endLow = low - axis;
        var endHigh = high + axis;
        bool lowIsLeft = InMap(endLow.x, endLow.y) && biome[Index(endLow.x, endLow.y)] == t.left;
        var (jungleEnd, meadowEnd) = lowIsLeft ? (endLow, endHigh) : (endHigh, endLow);
        var jungleOut = lowIsLeft ? -axis : axis;

        var line = new List<Vector2>();
        line.AddRange(Wander(toPrevious, jungleEnd, jungleOut, false, r.wander, rng));
        line.AddRange(Wander(meadowEnd, toNext, -jungleOut, true, r.wander, rng));

        // The road's tiles: land only (the bridge carries it over the river).
        float half = r.width / 2f;
        var road = new HashSet<int>();
        foreach (var p in line)
            Stamp(p, half, i => { if (region[i] == 0 && obstacle[i] == null) road.Add(i); });
        var offsets = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
        foreach (int i in road)
        {
            // Stones here and there, in small loose groups.
            float clump = Fbm(i % size * 0.3f, i / size * 0.3f, offsets, 1);
            decor[i] = rng.NextDouble() < r.stoneShare * (0.4f + 1.2f * clump) ? r.stoneDecor : r.earthDecor;
        }

        // Broken columns along the road, on one side or both, off the road and the water, away from portals and bridge.
        float next = Mathf.Lerp(r.columnSpacing[0], r.columnSpacing[1], (float)rng.NextDouble()), run = 0f;
        for (int k = 1; k < line.Count; k++)
        {
            run += Vector2.Distance(line[k - 1], line[k]);
            if (run < next) continue;
            run = 0f;
            next = Mathf.Lerp(r.columnSpacing[0], r.columnSpacing[1], (float)rng.NextDouble());
            var dir = (line[k] - line[k - 1]).normalized;
            var side = new Vector2(-dir.y, dir.x);
            bool pair = rng.NextDouble() < r.pairChance;
            float first = rng.Next(2) == 0 ? 1f : -1f;
            foreach (float s in pair ? new[] { 1f, -1f } : new[] { first })
            {
                var c = Vector2Int.FloorToInt(line[k] + side * s * (half + r.columnOffset));
                if (!InMap(c.x, c.y)) continue;
                int i = Index(c.x, c.y);
                if (region[i] != 0 || obstacle[i] != null || road.Contains(i)) continue;
                if (Vector2.Distance(c, toPrevious) < 4f || Vector2.Distance(c, toNext) < 4f
                    || Vector2.Distance(c, jungleEnd) < 4f || Vector2.Distance(c, meadowEnd) < 4f) continue;
                obstacle[i] = r.column;
            }
        }
        start = toPrevious + Vector2Int.right * 2; // arriving from the previous quarter
    }

    // A smooth winding line from one point to another (Catmull-Rom through points pushed aside at random). One of its
    // ends is a bridge's: the line runs straight along "away" (away from the bridge) for a few tiles there.
    static List<Vector2> Wander(Vector2 from, Vector2 to, Vector2 away, bool bridgeAtStart, float wander, System.Random rng)
    {
        const float Straight = 4f;
        var a = bridgeAtStart ? from + away * Straight : from;
        var b = bridgeAtStart ? to : to + away * Straight;
        var dir = (b - a).normalized;
        var side = new Vector2(-dir.y, dir.x);
        int count = Mathf.Max(2, Mathf.RoundToInt(Vector2.Distance(a, b) / 14f));
        var points = new List<Vector2>();
        if (bridgeAtStart) points.Add(from);
        points.Add(a);
        for (int k = 1; k < count; k++)
            points.Add(Vector2.Lerp(a, b, k / (float)count) + side * ((float)rng.NextDouble() * 2f - 1f) * wander);
        points.Add(b);
        if (!bridgeAtStart) points.Add(to);
        return Smooth(points);
    }

    static List<Vector2> Smooth(List<Vector2> pts)
    {
        var line = new List<Vector2>();
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 p0 = pts[Mathf.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(pts.Count - 1, i + 2)];
            int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(p1, p2) * 3f));
            for (int k = 0; k < steps; k++)
            {
                float u = k / (float)steps, u2 = u * u, u3 = u2 * u;
                line.Add(0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3));
            }
        }
        line.Add(pts[^1]);
        return line;
    }

    // ---- Relief ----

    void LayRelief(QuarterTest t, System.Random rng, int rock)
    {
        var r = t.relief;
        relief = r;
        int n = size * size;
        height = new int[n];
        wallLook = new bool[n];
        decor ??= new int[n];
        int rockDecor = r.rockDecors.Length > 0 ? r.rockDecors[Mathf.Abs(rock) % r.rockDecors.Length] : 9;

        // Distance (tiles) to what the relief keeps away from: sky, river, bridges, road, columns, portals.
        var dist = new int[n];
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            bool keep = region[i] != 0 || obstacle[i] != null || decor[i] > 0;
            dist[i] = keep ? 0 : int.MaxValue;
            if (keep) queue.Enqueue(i);
        }
        foreach (var m in markers)
            Stamp(m, 5f, i => { if (dist[i] != 0) { dist[i] = 0; queue.Enqueue(i); } });
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            foreach (var d in Around4)
            {
                int x = c % size + d.x, y = c / size + d.y;
                if (!InMap(x, y)) continue;
                int k = Index(x, y);
                if (dist[k] <= dist[c] + 1) continue;
                dist[k] = dist[c] + 1;
                queue.Enqueue(k);
            }
        }

        // The mountain, in the most open spot; hills elsewhere, apart from it and from each other.
        var shapes = new List<(Vector2 centre, float radius, int level)>();
        int open = 0;
        for (int i = 1; i < n; i++) if (dist[i] != int.MaxValue && dist[i] > dist[open]) open = i;
        var mountain = new Vector2(open % size + 0.5f, open / size + 0.5f);
        float big = Mathf.Min(r.mountainRadius, dist[open] - r.clearance);
        bool hasMountain = big >= 8f;
        if (hasMountain)
            for (int level = 1; level <= r.mountainLevels; level++)
                shapes.Add((mountain, big * (1f - (level - 1f) / r.mountainLevels), level));
        var hills = new List<(Vector2 centre, float radius)>();
        for (int k = 0, tries = 0; k < r.hills && tries < 400; tries++)
        {
            int i = rng.Next(n);
            float radius = Mathf.Lerp(r.hillRadius[0], r.hillRadius[1], (float)rng.NextDouble());
            var c = new Vector2(i % size + 0.5f, i / size + 0.5f);
            if (dist[i] == int.MaxValue || dist[i] < radius + r.clearance) continue;
            if (hasMountain && Vector2.Distance(c, mountain) < big + radius + 6f) continue;
            if (hills.Exists(h => Vector2.Distance(h.centre, c) < h.radius + radius + 6f)) continue;
            hills.Add((c, radius));
            shapes.Add((c, radius, 1));
            if (radius > (r.hillRadius[0] + r.hillRadius[1]) / 2f && rng.NextDouble() < r.upperChance) shapes.Add((c, radius * 0.5f, 2));
            k++;
        }
        foreach (var (centre, radius, level) in shapes)
        {
            float phase = (float)rng.NextDouble() * 6.3f, phase2 = (float)rng.NextDouble() * 6.3f;
            Stamp(centre, radius * 1.25f, i =>
            {
                var p = new Vector2(i % size + 0.5f, i / size + 0.5f) - centre;
                float a = Mathf.Atan2(p.y, p.x);
                float edge = radius * (1f + 0.12f * Mathf.Sin(3f * a + phase) + 0.07f * Mathf.Sin(5f * a + phase2));
                if (p.magnitude < edge && dist[i] >= r.clearance && dist[i] != int.MaxValue) height[i] = Mathf.Max(height[i], level);
            });
        }

        // Cliffs: the edge of each level, where the ground next to it is lower.
        var cliff = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (height[i] == 0) continue;
            foreach (var d in Around4)
            {
                int x = i % size + d.x, y = i / size + d.y;
                if (InMap(x, y) && height[Index(x, y)] < height[i]) { cliff[i] = true; break; }
            }
        }
        var ramps = new bool[n];
        // Opens a ramp in the cliff of that level around a centre, on the tile nearest that direction.
        Vector2Int Ramp(Vector2 centre, float reach, int level, float angle)
        {
            int found = -1;
            float bestGap = float.MaxValue;
            Stamp(centre, reach, i =>
            {
                if (!cliff[i] || height[i] != level) return;
                var p = new Vector2(i % size + 0.5f, i / size + 0.5f) - centre;
                float gap = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, angle * Mathf.Rad2Deg));
                if (gap < bestGap) { bestGap = gap; found = i; }
            });
            if (found < 0) return new Vector2Int(-1, -1);
            var at = new Vector2(found % size + 0.5f, found / size + 0.5f);
            Stamp(at, r.rampWidth / 2f, i => { if (cliff[i] && height[i] == level) ramps[i] = true; });
            return new Vector2Int(found % size, found / size);
        }
        // The mountain's ramps go round it: the first one facing the road, each next one further round.
        Vector2Int foot = new(-1, -1);
        if (hasMountain)
        {
            float toRoad = 0f, nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (decor[i] <= 0 || region[i] != 0) continue;
                var p = new Vector2(i % size + 0.5f, i / size + 0.5f);
                float d = Vector2.Distance(p, mountain);
                if (d < nearest) { nearest = d; toRoad = Mathf.Atan2(p.y - mountain.y, p.x - mountain.x); }
            }
            for (int level = 1; level <= r.mountainLevels; level++)
            {
                var ramp = Ramp(mountain, big * 1.3f, level, toRoad + (level - 1) * 150f * Mathf.Deg2Rad);
                if (level == 1) foot = ramp;
            }
            var top = Vector2Int.FloorToInt(mountain);
            caves.Add(top);
        }
        foreach (var (centre, radius) in hills)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            Ramp(centre, radius * 1.3f, 1, a);
            if (radius > 7f) Ramp(centre, radius * 1.3f, 1, a + Mathf.PI);
            Ramp(centre, radius * 0.7f, 2, a + Mathf.PI * 0.8f);
        }

        // Ledges: a few stretches of cliff facing down (lower ground below, the top above), crossed going down only.
        var runs = new List<List<int>>();
        for (int y = 1; y + 1 < size; y++)
        {
            List<int> run = null;
            for (int x = 0; x < size; x++)
            {
                int i = Index(x, y), below = Index(x, y - 1), above = Index(x, y + 1);
                bool fits = cliff[i] && !ramps[i] && height[below] < height[i] && !cliff[below] && obstacle[below] == null
                    && region[below] == 0 && height[above] == height[i] && !cliff[above];
                if (fits) { run ??= new List<int>(); run.Add(i); }
                else { if (run != null && run.Count >= r.ledgeLength[0]) runs.Add(run); run = null; }
            }
        }
        for (int k = 0; k < r.ledges && runs.Count > 0; k++)
        {
            var run = runs[rng.Next(runs.Count)];
            runs.Remove(run);
            int length = Mathf.Min(run.Count, rng.Next(r.ledgeLength[0], r.ledgeLength[1] + 1));
            int from = rng.Next(run.Count - length + 1);
            for (int j = from; j < from + length; j++)
            {
                int i = run[j];
                cliff[i] = false;
                wallLook[i] = true;
                obstacle[i] = "ledge";
                decor[i] = rockDecor;
            }
        }

        for (int i = 0; i < n; i++)
        {
            if (ramps[i]) { decor[i] = r.rampDecor; continue; }
            if (!cliff[i]) continue;
            wall[i] = true;
            decor[i] = rockDecor;
        }

        // A branch of the road to the mountain's first ramp.
        if (foot.x >= 0)
        {
            var outward = ((Vector2)foot + Vector2.one * 0.5f - mountain).normalized;
            var from = (Vector2)foot + Vector2.one * 0.5f + outward * 2f;
            Vector2 to = from;
            float nearest = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                if (decor[i] != t.road.earthDecor && decor[i] != t.road.stoneDecor || height[i] != 0) continue;
                var p = new Vector2(i % size + 0.5f, i / size + 0.5f);
                float d = Vector2.Distance(p, from);
                if (d < nearest) { nearest = d; to = p; }
            }
            foreach (var p in Wander(from, to, -outward, true, t.road.wander * 0.5f, rng))
                Stamp(p, t.road.width / 2f, i =>
                {
                    if (region[i] != 0 || obstacle[i] != null || height[i] != 0 || decor[i] > 0) return;
                    decor[i] = rng.NextDouble() < t.road.stoneShare ? t.road.stoneDecor : t.road.earthDecor;
                });
        }
    }

    /// Tint of a tile's ground: lighter higher up, darker at the foot of a cliff (1: none).
    public float Shade(int i)
    {
        if (relief == null || height == null) return 1f;
        var shades = relief.levelShade;
        float shade = shades.Length > 0 ? shades[Mathf.Min(height[i], shades.Length - 1)] : 1f;
        // The rows below a cliff of a higher level (the light comes from the top).
        for (int k = 1; k <= relief.footShadow.Length; k++)
        {
            int y = i / size + k;
            if (y >= size) break;
            int above = Index(i % size, y);
            if (wall[above] && height[above] > height[i]) { shade *= relief.footShadow[k - 1]; break; }
            if (height[above] != height[i]) break;
        }
        return shade;
    }
}
