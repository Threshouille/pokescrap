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
        public float secretChance = -1f; // chance of secret stairs here (-1: secret_rooms.json's)
        public float caveChance = -1f; // chance of a sealed cave here (-1: cave_seals.json's)
        public string caveSeal = ""; // tests: the seal of the cave (cave_seals.json key), drawn when empty
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
        public Props props = new();
    }

    /// Decor elements cut from the story maps (map_props.json) scattered on each sub-biome's open ground.
    [Serializable]
    public class Props
    {
        public float clearance = 3f, blockSpacing = 2.5f;
        public SubBiomeProps[] bySubBiome = new SubBiomeProps[0]; // the sparse ground cover, outside the patches
        public Patch[] patches = new Patch[0];
    }

    /// Patches of a sub-biome with decor of their own: "scatter" (groups at their density: a flower field, a fern
    /// thicket, scree) or "forest" (a solid mass of trees, crossed by `paths` paths from its edge to a clearing in its
    /// middle). count: patches per sub-biome; radius in tiles.
    [Serializable]
    public class Patch
    {
        public string subBiome, kind = "scatter";
        public int[] count = { 1, 2 };
        public float[] radius = { 5f, 9f };
        public PropGroup[] groups = new PropGroup[0];
        public string[] trees = new string[0];
        public float treeSpacing = 2.2f, pathWidth = 2f, clearing = 3f;
        public int paths = 2;
    }

    [Serializable]
    public class SubBiomeProps
    {
        public string subBiome;
        public PropGroup[] groups = new PropGroup[0];
    }

    [Serializable]
    public class PropGroup
    {
        public string group;
        public float per100;
    }

    [Serializable]
    class MapProp
    {
        public string key, group;
        public bool block;
    }

    [Serializable] class MapPropFile { public MapProp[] props = new MapProp[0]; }

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
        // Shapes of the levels: rectangles on a grid of `grid` tiles, the main one terraceWidth × terraceHeight of the
        // radius (half sizes), terraceExtras more around it.
        public int grid = 3, terraceExtras = 2;
        public float[] terraceWidth = { 0.75f, 0.95f }, terraceHeight = { 0.5f, 0.7f };
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
        plan.ScatterProps(t, rng);
        plan.PlaceSecret(rng, t.secretChance);
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
        centre = point; // the fan's point stands for the island's centre
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
        sealedCave = new Vector2Int(-1, -1);
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
        // Levels with straight edges: each a few rectangles on a grid of r.grid tiles around its centre (so that every
        // stretch of edge and every step is at least that long), shrunk until clear of what the relief keeps away from.
        int grid = Mathf.Max(1, r.grid);
        foreach (var (centre, radius, level) in shapes)
        {
            var origin = Vector2Int.FloorToInt(centre);
            int Snap(float v) => Mathf.RoundToInt(v / grid) * grid;
            var rects = new List<RectInt>();
            float halfWidth = radius * Mathf.Lerp(r.terraceWidth[0], r.terraceWidth[1], (float)rng.NextDouble());
            float halfHeight = radius * Mathf.Lerp(r.terraceHeight[0], r.terraceHeight[1], (float)rng.NextDouble());
            rects.Add(new RectInt(Snap(-halfWidth), Snap(-halfHeight), Snap(2 * halfWidth), Snap(2 * halfHeight)));
            for (int k = 0; k < r.terraceExtras; k++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f, d = radius * 0.55f;
                float w = radius * Mathf.Lerp(0.3f, 0.5f, (float)rng.NextDouble()), h = radius * Mathf.Lerp(0.25f, 0.45f, (float)rng.NextDouble());
                var c = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                rects.Add(new RectInt(Snap(c.x - w), Snap(c.y - h), Snap(2 * w), Snap(2 * h)));
            }
            foreach (var rect0 in rects)
            {
                var rect = rect0;
                bool Fits(RectInt q)
                {
                    for (int y = q.yMin; y < q.yMax; y++)
                        for (int x = q.xMin; x < q.xMax; x++)
                        {
                            int gx = origin.x + x, gy = origin.y + y;
                            if (!InMap(gx, gy)) return false;
                            int i = Index(gx, gy);
                            if (dist[i] == int.MaxValue || dist[i] < r.clearance) return false;
                        }
                    return true;
                }
                while (rect.width >= 2 * grid && rect.height >= 2 * grid && !Fits(rect))
                    rect = new RectInt(rect.xMin + grid, rect.yMin + grid, rect.width - 2 * grid, rect.height - 2 * grid);
                if (rect.width < 2 * grid || rect.height < 2 * grid) continue;
                for (int y = rect.yMin; y < rect.yMax; y++)
                    for (int x = rect.xMin; x < rect.xMax; x++)
                    {
                        int i = Index(origin.x + x, origin.y + y);
                        height[i] = Mathf.Max(height[i], level);
                    }
            }
        }
        // Notches narrower than two grids filled (across rows, then columns): they would break a face into stubs.
        for (int level = 1; level <= Mathf.Max(2, r.mountainLevels); level++)
            for (int pass = 0; pass < 2; pass++)
                for (int line = 0; line < size; line++)
                {
                    int last = -1; // the last tile of the level met along the line
                    for (int k = 0; k < size; k++)
                    {
                        int i = pass == 0 ? Index(k, line) : Index(line, k);
                        if (height[i] < level) continue;
                        if (last >= 0 && k - last - 1 > 0 && k - last - 1 < 2 * grid)
                        {
                            bool fits = true;
                            for (int m = last + 1; m < k && fits; m++)
                            {
                                int j = pass == 0 ? Index(m, line) : Index(line, m);
                                fits = dist[j] != int.MaxValue && dist[j] >= r.clearance;
                            }
                            if (fits)
                                for (int m = last + 1; m < k; m++) height[pass == 0 ? Index(m, line) : Index(line, m)] = level;
                        }
                        last = k;
                    }
                }
        // Each level well inside the one below: a grid's width on the back and the sides, two in front (its face and
        // room to walk at its foot).
        for (int level = 2; level <= Mathf.Max(2, r.mountainLevels); level++)
        {
            var keep = (int[])height.Clone();
            for (int i = 0; i < n; i++)
            {
                if (keep[i] < level) continue;
                int x0 = i % size, y0 = i / size;
                bool inside = true;
                for (int y = y0 - 2 * grid; y <= y0 + grid && inside; y++)
                    for (int x = x0 - grid; x <= x0 + grid && inside; x++)
                        inside = InMap(x, y) && keep[Index(x, y)] >= level - 1;
                if (!inside) height[i] = level - 1;
            }
        }
        // The sealed cave: a plateau of its own (its stairs, ledges and access left to its door).
        var caveMask = ReserveCave(rng, t.caveChance, dist, r.faceRows, t.caveSeal);
        bool InCave(int i) => caveMask != null && caveMask[i];

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
                if (face[i] == 0 || faceOf[i] != level || InCave(i) || Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) > reach) continue;
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
            Stamp(centre, reach, i => { if (face[i] > 0 && faceOf[i] == level && !InCave(i)) xs.Add(i % size); });
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
                if (height[i] > 0 && !cliff[i] && region[i] == 0 && obstacle[i] == null && !reached[i] && !InCave(i)) lost = i;
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
                bool fits = face[i] == 1 && !ramps[i] && faceOf[i] == height[above] && !cliff[above] && (roof == null || !roof[above]) && !InCave(i);
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

        if (caveMask != null) HollowSealedCave(r.cave, caveMask, face);

        // How each cliff tile is drawn: its style (the mountain's or the hills'), its row in its face, stairs or the
        // cave's mouth and their column. Their own ground shows the sub-biome's (no décor of their own).
        cliffStyles = new[] { "", r.hillStyle, r.mountainStyle };
        cliffStyle = new byte[n];
        faceRow = new int[n];
        faceLen = new int[n];
        cut = new sbyte[n];
        cutPart = new sbyte[n];
        for (int i = 0; i < n; i++) if (region[i] == 0) faceRow[i] = face[i]; // read by EdgeRole below
        for (int i = 0; i < n; i++)
        {
            // Also the inner corners of a level's edge (walkable, the corner of its lip drawn).
            bool inner = height[i] > 0 && face[i] == 0 && !ramps[i] && EdgeRole(i) != null;
            bool drawn = cliff[i] || obstacle[i] == "ledge" || (ramps[i] && face[i] > 0) || inner;
            if (!drawn || region[i] != 0) continue;
            bool onMountain = hasMountain && Vector2.Distance(new Vector2(i % size + 0.5f, i / size + 0.5f), mountain) < mountainReach || InCave(i);
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
        if (caveMask != null) MarkCaveDoor(r.cave);
        // The ground at the foot of a face, in its shadow (drawn in the face's style).
        for (int i = 0; i < n - size; i++)
            if (cliffStyle[i] == 0 && region[i] == 0 && AtFoot(i)) cliffStyle[i] = cliffStyle[i + size];

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
                // The summit's face stays outside; the stairs cut into it are the cave's mouth, dark. Its top row
                // (the edge of the summit, above the rock) goes under the roof, which thus comes down to the rock.
                if (ramps[i]) { decor[i] = c.decor; shadeOverride[i] = c.mouthShade; }
                else if (face[i] == 1 && faceOf[i] == top) roof[i] = true;
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

    // ---- Decor elements ----

    void ScatterProps(QuarterTest t, System.Random rng)
    {
        var asset = Resources.Load<TextAsset>("Data/map_props");
        if (asset == null) return;
        var groups = new Dictionary<string, List<MapProp>>();
        foreach (var p in JsonUtility.FromJson<MapPropFile>(asset.text).props)
        {
            if (!groups.TryGetValue(p.group, out var list)) groups[p.group] = list = new List<MapProp>();
            list.Add(p);
        }
        // Distance (tiles) to what they keep away from: sky, river, road, cliffs, stairs, the cave, portals, landing.
        int n = size * size;
        var dist = new int[n];
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            bool keep = region[i] != 0 || obstacle[i] != null || wall[i] || (decor != null && decor[i] > 0)
                || (cut != null && cut[i] != 0) || (roof != null && roof[i]);
            dist[i] = keep ? 0 : int.MaxValue;
            if (keep) queue.Enqueue(i);
        }
        var landmarks = new List<Vector2Int>(markers) { start };
        if (sealedCave.x >= 0) { landmarks.Add(sealedCaveFront); landmarks.AddRange(caveParts); }
        foreach (var m in landmarks) Stamp(m, 3.5f, i => { if (dist[i] != 0) { dist[i] = 0; queue.Enqueue(i); } });
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
        var blockers = new List<Vector2>();
        // Scatters the groups over these tiles at their density.
        void Scatter(List<int> tiles, PropGroup[] wanted)
        {
            if (tiles.Count == 0) return;
            foreach (var g in wanted)
            {
                if (!groups.TryGetValue(g.group, out var choices)) continue;
                int count = Mathf.RoundToInt(tiles.Count * g.per100 / 100f);
                for (int k = 0, tries = 0; k < count && tries < count * 20; tries++)
                {
                    int i = tiles[rng.Next(tiles.Count)];
                    var prop = choices[rng.Next(choices.Count)];
                    var tile = new Vector2(i % size + 0.5f, i / size + 0.5f);
                    if (obstacle[i] != null) continue;
                    if (prop.block)
                    {
                        if (dist[i] < t.props.clearance || blockers.Exists(b => Vector2.Distance(b, tile) < t.props.blockSpacing)) continue;
                        obstacle[i] = "blocker";
                        blockers.Add(tile);
                        decorProps.Add((prop.key, new Vector2(tile.x, tile.y - 0.3f)));
                    }
                    else
                        decorProps.Add((prop.key, tile + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * 0.7f));
                    k++;
                }
            }
        }

        var inPatch = new bool[n];
        foreach (var patch in t.props.patches)
        {
            var own = new List<int>();
            for (int i = 0; i < n; i++)
                if (region[i] == 0 && biome[i] != null && biome[i].key == patch.subBiome && dist[i] >= 2) own.Add(i);
            if (own.Count == 0) continue;
            int wanted = rng.Next(patch.count[0], patch.count[Mathf.Min(1, patch.count.Length - 1)] + 1);
            for (int made = 0, tries = 0; made < wanted && tries < 200; tries++)
            {
                int c = own[rng.Next(own.Count)];
                float radius = Mathf.Lerp(patch.radius[0], patch.radius[Mathf.Min(1, patch.radius.Length - 1)], (float)rng.NextDouble());
                if (dist[c] < radius * 0.6f || inPatch[c]) continue;
                var centre = new Vector2(c % size + 0.5f, c / size + 0.5f);
                string key = biome[c].key;
                float phase = (float)rng.NextDouble() * 6.3f, phase2 = (float)rng.NextDouble() * 6.3f;
                var tiles = new List<int>();
                Stamp(centre, radius * 1.4f, i =>
                {
                    var p = new Vector2(i % size + 0.5f, i / size + 0.5f) - centre;
                    float a = Mathf.Atan2(p.y, p.x);
                    float edge = radius * (1f + 0.25f * Mathf.Sin(3f * a + phase) + 0.15f * Mathf.Sin(5f * a + phase2));
                    if (p.magnitude < edge && region[i] == 0 && biome[i] != null && biome[i].key == key && dist[i] >= 2 && !inPatch[i] && obstacle[i] == null)
                        tiles.Add(i);
                });
                if (tiles.Count < radius * radius) continue; // mostly cut off by the road, the river...
                bool done = patch.kind == "forest" ? Forest(patch, centre, tiles) : true;
                if (!done) continue;
                foreach (int i in tiles) inPatch[i] = true;
                if (patch.kind != "forest") Scatter(tiles, patch.groups);
                made++;
            }
        }

        // A solid mass of trees on these tiles (invisible blockers under their crowns), crossed by paths from its edge
        // to a clearing in its middle; given up (false) if it would cut off any ground reached before.
        bool Forest(Patch patch, Vector2 centre, List<int> tiles)
        {
            var trees = new List<MapProp>();
            foreach (var g in patch.trees) if (groups.TryGetValue(g, out var list)) trees.AddRange(list);
            if (trees.Count == 0) return false;
            var inside = new HashSet<int>(tiles);
            var open = new HashSet<int>(); // paths and clearing
            Stamp(centre, patch.clearing, i => { if (inside.Contains(i)) open.Add(i); });
            // Path ends: tiles of its edge beside ground reached from the landing, spread around it.
            var before = ReachWithout(new bool[n], new bool[n]);
            var edges = new List<int>();
            foreach (int i in tiles)
                foreach (var d in Around4)
                {
                    int x = i % size + d.x, y = i / size + d.y;
                    if (InMap(x, y) && !inside.Contains(Index(x, y)) && before[Index(x, y)]) { edges.Add(i); break; }
                }
            if (edges.Count == 0) return false;
            var ends = new List<int>();
            float firstAngle = (float)rng.NextDouble() * 360f;
            for (int k = 0; k < patch.paths; k++)
            {
                float want = firstAngle + k * 360f / patch.paths;
                int best = -1;
                float bestGap = float.MaxValue;
                foreach (int i in edges)
                {
                    float a = Mathf.Atan2(i / size + 0.5f - centre.y, i % size + 0.5f - centre.x) * Mathf.Rad2Deg;
                    float gap = Mathf.Abs(Mathf.DeltaAngle(a, want));
                    if (gap < bestGap) { bestGap = gap; best = i; }
                }
                if (best >= 0 && !ends.Contains(best)) ends.Add(best);
            }
            foreach (int e in ends)
            {
                var from = new Vector2(e % size + 0.5f, e / size + 0.5f);
                var dir = (centre - from).normalized;
                var side = new Vector2(-dir.y, dir.x);
                var points = new List<Vector2> { from, Vector2.Lerp(from, centre, 0.5f) + side * ((float)rng.NextDouble() * 2f - 1f) * 2.5f, centre };
                foreach (var p in Smooth(points))
                    Stamp(p, patch.pathWidth / 2f, i => { if (inside.Contains(i)) open.Add(i); });
            }
            // Trees on a jittered grid, off the paths; each blocks its trunk and what its crown hides above it.
            var feet = new List<Vector2>();
            float step = patch.treeSpacing;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (int i in tiles)
            {
                minX = Mathf.Min(minX, i % size); maxX = Mathf.Max(maxX, i % size + 1);
                minY = Mathf.Min(minY, i / size); maxY = Mathf.Max(maxY, i / size + 1);
            }
            for (float y = minY + step / 2f; y < maxY; y += step)
                for (float x = minX + step / 2f; x < maxX; x += step)
                {
                    var foot = new Vector2(x, y) + new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * step * 0.4f;
                    int i = Index(Mathf.FloorToInt(foot.x), Mathf.FloorToInt(foot.y));
                    if (!inside.Contains(i) || open.Contains(i)) continue;
                    bool nearOpen = false;
                    Stamp(foot, 0.8f, j => { if (open.Contains(j)) nearOpen = true; });
                    if (!nearOpen) feet.Add(foot);
                }
            // Blocked: each trunk's tile, and the depth of the mass (tiles of the forest all round, off the paths); its
            // edge stays walkable between the trunks, under the crowns.
            var trunks = new HashSet<int>();
            foreach (var f in feet) trunks.Add(Index(Mathf.FloorToInt(f.x), Mathf.FloorToInt(f.y)));
            var blocked = new List<int>();
            foreach (int i in tiles)
            {
                if (open.Contains(i)) continue;
                bool deep = true;
                foreach (var d in Around4)
                {
                    int x = i % size + d.x, y = i / size + d.y;
                    int j = InMap(x, y) ? Index(x, y) : -1;
                    if (j < 0 || !inside.Contains(j) || open.Contains(j)) { deep = false; break; }
                }
                if (deep || trunks.Contains(i)) blocked.Add(i);
            }
            foreach (int i in blocked) obstacle[i] = "blocker";
            var after = ReachWithout(new bool[n], new bool[n]);
            for (int i = 0; i < n; i++)
                if (before[i] && !after[i] && !inside.Contains(i))
                {
                    foreach (int j in blocked) obstacle[j] = null;
                    return false;
                }
            foreach (var f in feet) decorProps.Add((trees[rng.Next(trees.Count)].key, f));
            clearings.Add(centre);
            return true;
        }

        // The sparse ground cover, outside the patches.
        foreach (var sub in t.props.bySubBiome)
        {
            var tiles = new List<int>();
            for (int i = 0; i < n; i++)
                if (region[i] == 0 && biome[i] != null && biome[i].key == sub.subBiome && dist[i] >= 1 && !inPatch[i]) tiles.Add(i);
            Scatter(tiles, sub.groups);
        }
    }
}
