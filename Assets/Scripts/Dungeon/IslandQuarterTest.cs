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
        public int rampDecor = 35, rampWidth = 3, ledges = 4, faceRows = 2;
        public float faceShade = 0.12f; // a face's rows darker by that much each, towards its foot
        public string hillStyle = "meadow", mountainStyle = "rock"; // cliff_styles.json: how their cliffs are drawn
        public int[] ledgeLength = { 3, 6 };
        public float[] levelShade = { 0.93f, 0.96f, 0.98f, 1f }, footShadow = { 0.7f, 0.85f };
        public Cave cave = new();
    }

    /// The mountain's top level as a cave under a roof (see DungeonGenerator: the roof fades while the controlled
    /// Pokémon is under it).
    [Serializable]
    public class Cave
    {
        public int decor = 9, pillars = 4, roofDecor = 49;
        public float shade = 0.8f, mouthShade = 0.5f, roofAlpha = 0.15f, fadeTime = 0.25f;
    }

    [NonSerialized] float[] shadeOverride; // per tile: a tint of its own (the cave), 0 = none

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

        // Cliffs, as seen in 3/4 view: the side of each level facing the camera (south) is a face faceRows tiles tall
        // per level climbed, standing on the lower ground below it; the other sides a thin rim (the level's own edge).
        var cliff = new bool[n];
        var face = new int[n]; // per face tile: its row from the top of the face (1, 2...); 0 elsewhere
        var faceOf = new int[n]; // the level whose face it is
        for (int i = 0; i < n; i++)
        {
            int level = height[i];
            if (level == 0) continue;
            int x = i % size, y = i / size;
            if (y == 0 || height[Index(x, y - 1)] >= level) continue;
            int rows = r.faceRows * (level - height[Index(x, y - 1)]);
            for (int k = 1; k <= rows && y - k >= 0; k++)
            {
                int j = Index(x, y - k);
                if (region[j] != 0 || obstacle[j] != null || decor[j] > 0 || height[j] >= level) break; // river, road...
                if (faceOf[j] < level) { face[j] = k; faceOf[j] = level; }
            }
        }
        // The rims: an edge of a level with lower ground behind or beside it, unless that ground is the level's own
        // face (along the steps of a round hill's bottom, the face carries on).
        for (int i = 0; i < n; i++)
        {
            int level = height[i];
            if (level == 0) continue;
            int x = i % size, y = i / size;
            foreach (var d in new[] { Vector2Int.up, Vector2Int.left, Vector2Int.right })
            {
                if (!InMap(x + d.x, y + d.y)) continue;
                int j = Index(x + d.x, y + d.y);
                if (height[j] < level && faceOf[j] < level) cliff[i] = true;
            }
        }
        for (int i = 0; i < n; i++)
            if (face[i] > 0) { cliff[i] = true; height[i] = faceOf[i]; }

        var ramps = new bool[n];
        // A column of a level's face fit for stairs: the face whole and in one piece, the level's ground right above it,
        // free lower ground right below it. Returns the tile at its foot, or y = -1.
        Vector2Int Column(int x, int level, Vector2 centre, float reach)
        {
            int top = -1, bottom = int.MaxValue, count = 0;
            for (int y = 0; y < size; y++)
            {
                int i = Index(x, y);
                if (face[i] == 0 || faceOf[i] != level || Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) > reach) continue;
                top = Mathf.Max(top, y);
                bottom = Mathf.Min(bottom, y);
                count++;
            }
            if (top < 0 || count != top - bottom + 1 || top + 1 >= size || bottom < 1) return new Vector2Int(x, -1);
            int above = Index(x, top + 1), below = Index(x, bottom - 1);
            bool upOk = height[above] == level && !cliff[above] && region[above] == 0;
            bool downOk = region[below] == 0 && !cliff[below] && height[below] < level && (obstacle[below] == null || obstacle[below] == t.bridgeKind);
            return upOk && downOk ? new Vector2Int(x, bottom - 1) : new Vector2Int(x, -1);
        }
        // Stairs cut into a level's face (3 columns, the whole face), on the fit column nearest targetX; returns the
        // tile at their foot, or (-1, -1).
        Vector2Int Stairs(Vector2 centre, float reach, int level, float targetX)
        {
            var xs = new SortedSet<int>();
            Stamp(centre, reach, i => { if (face[i] > 0 && faceOf[i] == level) xs.Add(i % size); });
            var order = new List<int>(xs);
            order.Sort((a, b) => Mathf.Abs(a + 0.5f - targetX).CompareTo(Mathf.Abs(b + 0.5f - targetX)));
            foreach (int x in order)
            {
                var left = Column(x - 1, level, centre, reach);
                var middle = Column(x, level, centre, reach);
                var right = Column(x + 1, level, centre, reach);
                if (left.y < 0 || middle.y < 0 || right.y < 0) continue;
                for (int dx = -1; dx <= 1; dx++)
                    for (int y = 0; y < size; y++)
                    {
                        int i = Index(x + dx, y);
                        if (face[i] > 0 && faceOf[i] == level && Vector2.Distance(new Vector2(x + dx + 0.5f, y + 0.5f), centre) <= reach) ramps[i] = true;
                    }
                return middle;
            }
            return new Vector2Int(-1, -1);
        }
        // The mountain is climbed in zigzags towards the top of the screen: the first stairs on the left of its face,
        // the next ones on the right, the cave's mouth in the middle of the last face.
        Vector2Int foot = new(-1, -1);
        float mountainReach = big * 1.3f + r.faceRows * r.mountainLevels;
        if (hasMountain)
            for (int level = 1; level <= r.mountainLevels; level++)
            {
                float target = level == r.mountainLevels ? mountain.x : mountain.x + (level % 2 == 1 ? -0.45f : 0.35f) * big;
                var stairs = Stairs(mountain, mountainReach, level, target);
                if (level == 1) foot = stairs;
            }
        foreach (var (centre, radius) in hills)
        {
            Stairs(centre, radius * 1.3f + r.faceRows * 2, 1, centre.x + ((float)rng.NextDouble() - 0.5f) * radius);
            Stairs(centre, radius * 0.7f + r.faceRows * 2, 2, centre.x + ((float)rng.NextDouble() - 0.5f) * radius * 0.5f);
        }
        // Whatever high ground is still out of reach (a face cut by the river, too narrow...) gets stairs of its own,
        // on the nearest fit face of its level.
        for (int round = 0; round < 40; round++)
        {
            var reached = ReachWithout(cliff, ramps);
            int lost = -1;
            for (int i = 0; i < n && lost < 0; i++)
                if (height[i] > 0 && !cliff[i] && region[i] == 0 && obstacle[i] == null && !reached[i]) lost = i;
            if (lost < 0) break;
            var at = new Vector2(lost % size + 0.5f, lost / size + 0.5f);
            if (Stairs(at, 40f, height[lost], at.x).y >= 0) continue;
            // No way found: that piece of high ground is left be (marked, then unmarked below).
            int level = height[lost];
            var piece = new Queue<int>();
            height[lost] = -level;
            piece.Enqueue(lost);
            while (piece.Count > 0)
            {
                int c = piece.Dequeue();
                foreach (var d in Around4)
                {
                    int x = c % size + d.x, y = c / size + d.y;
                    if (!InMap(x, y)) continue;
                    int j = Index(x, y);
                    if (height[j] != level || cliff[j]) continue;
                    height[j] = -level;
                    piece.Enqueue(j);
                }
            }
        }
        for (int i = 0; i < n; i++) if (height[i] < 0) height[i] = -height[i];
        if (hasMountain)
        {
            int top = r.mountainLevels;
            Vector2Int mouth = new(-1, -1);
            for (int i = 0; i < n && mouth.x < 0; i++)
                if (ramps[i] && faceOf[i] == top && Vector2.Distance(new Vector2(i % size, i / size), mountain) < mountainReach) mouth = new Vector2Int(i % size, i / size);
            caves.Add(Vector2Int.FloorToInt(mountain));
            Hollow(r, rng, mountain, big, mouth, cliff, ramps, face, faceOf);
        }

        // Ledges: a few stretches of face (3 to 6 columns wide), jumped down, never climbed.
        var runs = new List<List<int>>();
        for (int y = 1; y + 1 < size; y++)
        {
            List<int> run = null;
            for (int x = 0; x < size; x++)
            {
                int i = Index(x, y), above = Index(x, y + 1);
                // The top row of a face: the level's ground just above it.
                bool fits = face[i] == 1 && !ramps[i] && faceOf[i] == height[above] && !cliff[above] && (roof == null || !roof[above]);
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
                for (int i = run[j]; i >= 0 && face[i] > 0 && faceOf[i] == faceOf[run[j]]; i -= size) // down the face
                {
                    cliff[i] = false;
                    wallLook[i] = true;
                    obstacle[i] = "ledge";
                    decor[i] = rockDecor;
                }
        }

        // Faces darker towards their foot (the light comes from the top).
        shadeOverride ??= new float[n];
        for (int i = 0; i < n; i++)
            if (face[i] > 0 && shadeOverride[i] == 0f && !ramps[i]) shadeOverride[i] = Mathf.Max(0.55f, 1f - r.faceShade * face[i]);

        for (int i = 0; i < n; i++)
        {
            if (ramps[i]) { if (decor[i] == 0) decor[i] = r.rampDecor; continue; }
            if (!cliff[i]) continue;
            wall[i] = true;
            if (decor[i] == 0) decor[i] = rockDecor;
        }

        // How each cliff tile is drawn: its style (the mountain's or the hills'), its row in its face, stairs or the
        // cave's mouth and their column. Their own ground shows the sub-biome's (no décor of their own).
        cliffStyles = new[] { "", r.hillStyle, r.mountainStyle };
        cliffStyle = new byte[n];
        faceRow = new int[n];
        faceLen = new int[n];
        cut = new sbyte[n];
        cutPart = new sbyte[n];
        for (int i = 0; i < n; i++)
        {
            bool drawn = cliff[i] || obstacle[i] == "ledge" || (ramps[i] && face[i] > 0);
            if (!drawn || region[i] != 0) continue;
            bool onMountain = hasMountain && Vector2.Distance(new Vector2(i % size + 0.5f, i / size + 0.5f), mountain) < mountainReach;
            cliffStyle[i] = (byte)(onMountain ? 2 : 1);
            faceRow[i] = face[i];
            if (face[i] > 0)
            {
                int length = face[i];
                for (int j = i - size; j >= 0 && face[j] > 0 && faceOf[j] == faceOf[i] && face[j] > face[j + size]; j -= size) length = face[j];
                faceLen[i] = length;
            }
            if (ramps[i] && face[i] > 0)
            {
                cut[i] = (sbyte)(onMountain && faceOf[i] == r.mountainLevels ? 2 : 1);
                bool left = i % size > 0 && ramps[i - 1] && faceOf[i - 1] == faceOf[i];
                bool right = i % size < size - 1 && ramps[i + 1] && faceOf[i + 1] == faceOf[i];
                cutPart[i] = (sbyte)(!left ? -1 : !right ? 1 : 0);
            }
            else if (decor[i] == rockDecor) decor[i] = 0;
        }

        // A branch of the road to the foot of the mountain's first stairs.
        if (foot.x >= 0)
        {
            var outward = Vector2.down; // the stairs face the camera
            var from = (Vector2)foot + Vector2.one * 0.5f;
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

    // The mountain's top level hollowed into a cave: its floor and its rim under a roof, the stairs of its face its
    // mouth, a few rock pillars inside.
    void Hollow(Relief r, System.Random rng, Vector2 centre, float radius, Vector2Int mouth, bool[] cliff, bool[] ramps,
        int[] face, int[] faceOf)
    {
        var c = r.cave;
        int top = r.mountainLevels, n = size * size;
        roof = new bool[n];
        roofDecor = c.roofDecor;
        shadeOverride ??= new float[n];
        var inside = new List<int>();
        Stamp(centre, radius + r.faceRows * top, i =>
        {
            if (height[i] != top) return;
            if (face[i] > 0)
            {
                // The summit's face stays outside; the stairs cut into it are the cave's mouth, dark.
                if (ramps[i]) { decor[i] = c.decor; shadeOverride[i] = c.mouthShade; }
                return;
            }
            roof[i] = true;
            if (cliff[i]) return; // the cave's wall: the cliff ring (rock outside), under the roof too
            decor[i] = c.decor;
            shadeOverride[i] = c.shade;
            inside.Add(i);
        });
        // Pillars: single rocks inside, off the walls and the mouth.
        var mouthAt = new Vector2(mouth.x + 0.5f, mouth.y + 0.5f);
        for (int k = 0, tries = 0; k < c.pillars && tries < 200 && inside.Count > 0; tries++)
        {
            int i = inside[rng.Next(inside.Count)];
            var p = new Vector2(i % size + 0.5f, i / size + 0.5f);
            if (Vector2.Distance(p, mouthAt) < 4f) continue;
            bool clear = true;
            Stamp(p, 2.2f, j => { if (cliff[j] || wall[j]) clear = false; });
            if (!clear) continue;
            wall[i] = true;
            cliff[i] = true;
            decor[i] = c.decor;
            k++;
        }
    }

    /// Tint of a tile's ground: lighter higher up, darker at the foot of a cliff (1: none).
    public float Shade(int i)
    {
        if (shadeOverride != null && shadeOverride[i] > 0f) return shadeOverride[i];
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

    // Tiles reached on foot from the landing point, cliffs blocking but their stairs open (no ledges yet).
    bool[] ReachWithout(bool[] cliff, bool[] ramps)
    {
        var reached = new bool[size * size];
        var queue = new Queue<int>();
        int from = Index(start.x, start.y);
        reached[from] = true;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            foreach (var d in Around4)
            {
                int x = c % size + d.x, y = c / size + d.y;
                if (!InMap(x, y)) continue;
                int i = Index(x, y);
                if (reached[i] || region[i] != 0 || (cliff[i] && !ramps[i]) || wall[i]) continue;
                if (obstacle[i] != null && obstacle[i] != themes[0].bridgeKind) continue;
                reached[i] = true;
                queue.Enqueue(i);
            }
        }
        return reached;
    }
}
