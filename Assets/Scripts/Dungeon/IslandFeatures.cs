using System.Collections.Generic;
using UnityEngine;

/// The island's big features (step 2), laid on the biome fields of IslandPlan, per quarter as its theme says
/// (biomes.json): the crater at the top of the peak; lakes in the lowest hollows; rivers from the heights down the
/// slope to the coast, a lake or the border, crossed by bridges every so often; cliffs where the slope crosses given
/// altitudes, with gaps; thickets and rock masses as each sub-biome asks. A passage through the barrier joins each
/// quarter to the next. Last, every walkable tile is made reachable from the landing point: an area cut off gets a
/// way through (walls opened, a bridge laid), a tiny one is walled up.
public partial class IslandPlan
{
    public string[] obstacle; // per tile: an obstacle key of obstacles.json, or null
    public bool[] wall; // per tile: a wall (cliff, thicket, rock mass)
    public readonly List<Vector2Int> gates = new(); // passage from order[k] to order[k + 1]

    const int KeepClear = 5; // tiles kept free around the landing point and the passages
    const int BridgeEvery = 22, TinyArea = 20;

    static readonly HashSet<string> Blocking = new() { "water", "lava", "void" };
    static readonly Vector2Int[] Around8 =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    };
    static readonly Vector2Int[] Around4 = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    bool Land(int i) => region[i] >= 0 && region[i] < 4;
    bool InMap(int x, int y) => x >= 0 && y >= 0 && x < size && y < size;

    /// A tile one can walk on: land, no wall, no blocking obstacle.
    public bool Walkable(int i) => Land(i) && !wall[i] && (obstacle[i] == null || !Blocking.Contains(obstacle[i]));

    static int Pick2(int[] range, System.Random rng) =>
        range == null || range.Length == 0 ? 0 : rng.Next(range[0], range[Mathf.Min(1, range.Length - 1)] + 1);

    void BuildFeatures(System.Random rng, Vector2 noise)
    {
        obstacle = new string[size * size];
        wall = new bool[size * size];
        // Every barrier tile is wall; the passages open it.
        for (int i = 0; i < wall.Length; i++) wall[i] = region[i] == Border;
        Gates();
        var keep = new List<Vector2Int>(gates) { start };

        for (int q = 0; q < 4; q++)
        {
            var t = themes[q];
            if (!string.IsNullOrEmpty(t.crater)) Crater(q, t.crater, keep);
            for (int n = 0, count = Pick2(t.lakes, rng); n < count; n++) Lake(q, t.lakeKind, Pick2(t.lakeSize, rng), rng, keep);
            for (int n = 0, count = Pick2(t.rivers, rng); n < count; n++) River(q, t, rng, keep);
            Cliffs(q, t, noise, keep);
            Thickets(q, noise, keep);
        }
        DecorEdges(noise, keep);
        Connect();
    }

    // A passage through each barrier between two quarters that open one after the other, halfway to the coast.
    void Gates()
    {
        float c = size / 2f;
        for (int k = 0; k < 3; k++)
        {
            int a = order[k], b = order[k + 1];
            Vector2Int best = default;
            float bestScore = float.MaxValue;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    if (region[Index(x, y)] != Border || !Touches(x, y, a, 5) || !Touches(x, y, b, 5)) continue;
                    float r = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / settings.radius;
                    float score = Mathf.Abs(r - 0.6f);
                    if (score < bestScore) { bestScore = score; best = new Vector2Int(x, y); }
                }
            if (bestScore == float.MaxValue) continue;
            gates.Add(best);
            // A corridor through the barrier, from the nearest tile of one quarter to the nearest of the other.
            var na = Nearest(best, a, 14);
            var nb = Nearest(best, b, 14);
            if (na == null || nb == null) continue;
            float length = Vector2.Distance(na.Value, nb.Value);
            for (float t = 0f; t <= 1f; t += 0.5f / Mathf.Max(1f, length))
                Stamp(Vector2.Lerp(na.Value, nb.Value, t), 2.2f, i => { if (region[i] == Border) { region[i] = a; wall[i] = false; } });
        }
    }

    // The tile of that quarter nearest to a point, within reach, or null.
    Vector2Int? Nearest(Vector2Int p, int quarter, int reach)
    {
        Vector2Int? best = null;
        float bestDistance = float.MaxValue;
        for (int x = p.x - reach; x <= p.x + reach; x++)
            for (int y = p.y - reach; y <= p.y + reach; y++)
            {
                if (!InMap(x, y) || region[Index(x, y)] != quarter) continue;
                float d = (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y);
                if (d < bestDistance) { bestDistance = d; best = new Vector2Int(x, y); }
            }
        return best;
    }

    bool Touches(int x, int y, int quarter, int reach)
    {
        for (int dx = -reach; dx <= reach; dx += reach)
            for (int dy = -reach; dy <= reach; dy += reach)
                if (InMap(x + dx, y + dy) && region[Index(x + dx, y + dy)] == quarter) return true;
        return false;
    }

    void Stamp(Vector2 p, float r, System.Action<int> act)
    {
        for (int x = Mathf.FloorToInt(p.x - r); x <= Mathf.CeilToInt(p.x + r); x++)
            for (int y = Mathf.FloorToInt(p.y - r); y <= Mathf.CeilToInt(p.y + r); y++)
                if (InMap(x, y) && (x - p.x) * (x - p.x) + (y - p.y) * (y - p.y) <= r * r) act(Index(x, y));
    }

    static bool NearAny(List<Vector2Int> keep, int x, int y, int reach)
    {
        foreach (var k in keep)
            if (Mathf.Abs(k.x - x) <= reach && Mathf.Abs(k.y - y) <= reach) return true;
        return false;
    }

    void Crater(int q, string kind, List<Vector2Int> keep)
    {
        for (int i = 0; i < region.Length; i++)
            if (region[i] == q && altitude[i] > 0.94f && !NearAny(keep, i % size, i / size, KeepClear)) obstacle[i] = kind;
    }

    // A lake in one of the quarter's lowest hollows, its edge wavering.
    void Lake(int q, string kind, int radius, System.Random rng, List<Vector2Int> keep)
    {
        var low = new List<int>();
        for (int i = 0; i < region.Length; i++)
            if (region[i] == q && altitude[i] < 0.18f && obstacle[i] == null) low.Add(i);
        for (int attempt = 0; attempt < 20 && low.Count > 0; attempt++)
        {
            int seed = low[rng.Next(low.Count)];
            var c = new Vector2(seed % size, seed / size);
            if (NearAny(keep, (int)c.x, (int)c.y, radius + KeepClear)) continue;
            float phase = (float)rng.NextDouble() * 6.3f;
            for (int x = (int)c.x - radius - 2; x <= c.x + radius + 2; x++)
                for (int y = (int)c.y - radius - 2; y <= c.y + radius + 2; y++)
                {
                    if (!InMap(x, y)) continue;
                    int i = Index(x, y);
                    float a = Mathf.Atan2(y - c.y, x - c.x);
                    float edge = radius * (1f + 0.2f * Mathf.Sin(3f * a + phase) + 0.1f * Mathf.Sin(5f * a + phase * 2f));
                    if (region[i] == q && obstacle[i] == null && Vector2.Distance(new Vector2(x, y), c) <= edge) obstacle[i] = kind;
                }
            return;
        }
    }

    // From a high tile of the inner half of the quarter toward the coast: the overall slope of an island (the
    // distance from the centre) leads, the local altitude bends it, a slow sway makes it meander; it ends at the coast,
    // a lake or the border.
    void River(int q, Theme t, System.Random rng, List<Vector2Int> keep)
    {
        float c = size / 2f;
        float Out(int i) => Vector2.Distance(new Vector2(i % size, i / size), new Vector2(c, c)) / settings.radius;
        var high = new List<int>();
        for (int i = 0; i < region.Length; i++)
            if (region[i] == q && altitude[i] > 0.6f && altitude[i] < 0.92f && obstacle[i] == null && Out(i) < 0.6f) high.Add(i);
        if (high.Count == 0) return;
        int at = high[rng.Next(high.Count)];
        var path = new List<int> { at };
        var seen = new HashSet<int> { at };
        float sway = (float)rng.NextDouble() * 6.3f, swayRate = 0.04f + (float)rng.NextDouble() * 0.05f;
        for (int step = 0; step < 600; step++)
        {
            int x = at % size, y = at / size, next = -1;
            float best = float.MaxValue;
            foreach (var d in Around8)
            {
                int nx = x + d.x, ny = y + d.y;
                if (!InMap(nx, ny)) continue;
                int n = Index(nx, ny);
                if (seen.Contains(n)) continue;
                if (region[n] != q) { next = -2; break; } // reached the coast, the border or the central void
                // Outward first; the altitude and a slow sideways sway bend the course.
                var outward = new Vector2(nx - c, ny - c).normalized;
                var side = new Vector2(-outward.y, outward.x) * Mathf.Sin(step * swayRate + sway);
                var move = new Vector2(d.x, d.y).normalized;
                float score = -Vector2.Dot(move, outward) * 1.2f - Vector2.Dot(move, side) * 0.6f + altitude[n] * 0.6f
                    + (float)rng.NextDouble() * 0.15f;
                if (score < best) { best = score; next = n; }
            }
            if (next < 0) break;
            at = next;
            seen.Add(at);
            path.Add(at);
            if (obstacle[at] != null && obstacle[at] == t.lakeKind) break; // into a lake
        }
        if (path.Count < 12) return;
        int width = Pick2(t.riverWidth, rng);
        for (int k = 0; k < path.Count; k++)
        {
            var p = new Vector2(path[k] % size, path[k] / size);
            if (NearAny(keep, (int)p.x, (int)p.y, KeepClear)) continue;
            Stamp(p, width / 2f + 0.3f, i => { if (region[i] == q && obstacle[i] == null) obstacle[i] = t.riverKind; });
        }
        // Bridges: straight bands two tiles wide across the river, where it runs, every BridgeEvery tiles.
        for (int k = BridgeEvery / 2; k < path.Count - 3; k += BridgeEvery)
        {
            var a = new Vector2(path[Mathf.Max(0, k - 3)] % size, path[Mathf.Max(0, k - 3)] / size);
            var b = new Vector2(path[k + 3] % size, path[k + 3] / size);
            var along = (b - a).normalized;
            var across = new Vector2(-along.y, along.x);
            var p = new Vector2(path[k] % size, path[k] / size);
            for (float u = -(width / 2f + 2f); u <= width / 2f + 2f; u += 0.5f)
                for (float v = 0f; v <= 1.5f; v += 0.5f)
                {
                    var tile = Vector2Int.RoundToInt(p + across * u + along * v);
                    if (!InMap(tile.x, tile.y)) continue;
                    int i = Index(tile.x, tile.y);
                    if (region[i] == q && obstacle[i] == t.riverKind) obstacle[i] = t.bridgeKind;
                }
        }
    }

    /// The ROM décor drawing a land tile (its sub-biome's, one of them picked by position), or -1.
    public int DecorOf(int i)
    {
        var b = biome[i];
        return b != null && b.tilesets.Length > 0 ? b.tilesets[(i * 7919) % b.tilesets.Length] : -1;
    }

    // Where the décor changes, a hedge or a ledge one tile thick, drawn with the walls of the décor (the ROM's tilesets
    // have no tile blending two décors: a wall between them gives a clean edge); coarse noise leaves wide gaps.
    void DecorEdges(Vector2 noise, List<Vector2Int> keep)
    {
        var edges = new List<int>();
        for (int y = 1; y < size - 1; y++)
            for (int x = 1; x < size - 1; x++)
            {
                int i = Index(x, y);
                if (!Land(i) || wall[i] || obstacle[i] != null) continue;
                int mine = DecorOf(i);
                bool edge = false;
                foreach (var d in Around4)
                {
                    int n = Index(x + d.x, y + d.y);
                    // One side of the line only (the tile with the higher décor number), so that it stays one tile thick.
                    if (Land(n) && DecorOf(n) >= 0 && DecorOf(n) != mine && mine > DecorOf(n)) edge = true;
                }
                if (!edge || NearAny(keep, x, y, KeepClear)) continue;
                if (Mathf.PerlinNoise(x * 0.07f + noise.y * 1.7f, y * 0.07f + noise.x * 1.7f) > 0.62f) continue; // a gap
                edges.Add(i);
            }
        foreach (int i in edges) wall[i] = true;
    }

    // Walls where the slope crosses the cliff altitudes; coarse noise leaves gaps to climb through.
    void Cliffs(int q, Theme t, Vector2 noise, List<Vector2Int> keep)
    {
        if (t.cliffLevels == null || t.cliffLevels.Length == 0) return;
        for (int y = 1; y < size - 1; y++)
            for (int x = 1; x < size - 1; x++)
            {
                int i = Index(x, y);
                if (region[i] != q || obstacle[i] != null) continue;
                if (Mathf.PerlinNoise(x * 0.05f + noise.x, y * 0.05f + noise.y) > 0.55f) continue; // a gap
                if (NearAny(keep, x, y, KeepClear)) continue;
                foreach (float level in t.cliffLevels)
                {
                    bool edge = false;
                    foreach (var d in Around4)
                    {
                        int n = Index(x + d.x, y + d.y);
                        if (region[n] == q && altitude[n] < level && altitude[i] >= level) edge = true;
                    }
                    if (edge) { wall[i] = true; break; }
                }
            }
    }

    // Thickets and rock masses: a coarse noise ranked within the quarter, below each sub-biome's share.
    void Thickets(int q, Vector2 noise, List<Vector2Int> keep)
    {
        var cells = new List<int>();
        var value = new Dictionary<int, float>();
        for (int i = 0; i < region.Length; i++)
        {
            if (region[i] != q || obstacle[i] != null) continue;
            int x = i % size, y = i / size;
            value[i] = Mathf.PerlinNoise(x * 0.09f + noise.y, y * 0.09f + noise.x) * 0.7f
                + Mathf.PerlinNoise(x * 0.25f + noise.x * 2f, y * 0.25f + noise.y * 2f) * 0.3f;
            cells.Add(i);
        }
        cells.Sort((a, b) => value[a].CompareTo(value[b]));
        for (int k = 0; k < cells.Count; k++)
        {
            int i = cells[k];
            float rank = k / (float)Mathf.Max(1, cells.Count - 1);
            if (biome[i] != null && rank < biome[i].walls && !NearAny(keep, i % size, i / size, KeepClear)) wall[i] = true;
        }
    }

    // Everything walkable reached from the landing point: each area cut off is joined by the shortest way through
    // walls (opened) and water, lava or void (bridged); tiny ones are walled up.
    void Connect()
    {
        for (int round = 0; round < 400; round++)
        {
            var reached = Reach(Index(start.x, start.y));
            int lost = -1;
            for (int i = 0; i < region.Length && lost < 0; i++)
                if (Walkable(i) && !reached[i]) lost = i;
            if (lost < 0) return;
            // That area.
            var area = new List<int>();
            var inArea = new HashSet<int> { lost };
            var queue = new Queue<int>();
            queue.Enqueue(lost);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue();
                area.Add(c);
                foreach (var d in Around4)
                {
                    int nx = c % size + d.x, ny = c / size + d.y;
                    if (!InMap(nx, ny)) continue;
                    int n = Index(nx, ny);
                    if (Walkable(n) && inArea.Add(n)) queue.Enqueue(n);
                }
            }
            if (area.Count < TinyArea)
            {
                foreach (int i in area) wall[i] = true;
                continue;
            }
            // Shortest way from the area to a reached tile, over land only.
            var parent = new Dictionary<int, int>();
            queue.Clear();
            foreach (int i in area) { parent[i] = -1; queue.Enqueue(i); }
            int found = -1;
            while (queue.Count > 0 && found < 0)
            {
                int c = queue.Dequeue();
                foreach (var d in Around4)
                {
                    int nx = c % size + d.x, ny = c / size + d.y;
                    if (!InMap(nx, ny)) continue;
                    int n = Index(nx, ny);
                    if (parent.ContainsKey(n) || !Land(n)) continue;
                    parent[n] = c;
                    if (reached[n]) { found = n; break; }
                    queue.Enqueue(n);
                }
            }
            if (found < 0)
            {
                foreach (int i in area) wall[i] = true;
                continue;
            }
            for (int c = found; c >= 0; c = parent[c])
            {
                // Two tiles wide: the tile and its right-hand or upper neighbour.
                foreach (int i in new[] { c, Mathf.Min(c + 1, region.Length - 1) })
                {
                    if (!Land(i)) continue;
                    wall[i] = false;
                    if (obstacle[i] != null && Blocking.Contains(obstacle[i])) obstacle[i] = themes[region[i]].bridgeKind;
                }
            }
        }
    }

    bool[] Reach(int from)
    {
        var reached = new bool[region.Length];
        if (!Walkable(from)) return reached;
        var queue = new Queue<int>();
        reached[from] = true;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            foreach (var d in Around4)
            {
                int nx = c % size + d.x, ny = c / size + d.y;
                if (!InMap(nx, ny)) continue;
                int n = Index(nx, ny);
                if (!reached[n] && Walkable(n)) { reached[n] = true; queue.Enqueue(n); }
            }
        }
        return reached;
    }
}
