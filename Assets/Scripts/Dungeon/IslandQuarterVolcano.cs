using System;
using System.Collections.Generic;
using UnityEngine;

/// The volcano's test quarter (Resources/Data/quarters/volcano.json, layout "volcano"; GDD « Quart du volcan »): the
/// same fan as the forest's, but a cone of terraces near its point (the island's centre) with a lava lake for a crater
/// on its top level (LayRelief, relief.summit "crater"), and lava flows instead of the river: from the cone's foot down
/// to the coast and past it, fanning out, crossed by basalt bridges where the road meets them and further down. The
/// road runs from the portal to the previous quarter (left side) round the cone to the one to the next (bottom side).
/// Sub-biomes by place: the basalt shore near the coast, the flows along the lava, the ash slopes elsewhere; once the
/// relief is laid (FinishVolcano), the cone's levels are the magma caves and its top the crater.
public partial class IslandPlan
{
    [Serializable]
    public class Volcano
    {
        public float coneShare = 0.32f; // where the cone stands along the fan's middle (share of the radius)
        public int[] flows = { 2, 3 };
        public float[] flowWidth = { 3f, 5f };
        public float flowSpread = 70f; // degrees between the outermost flows, round the fan's middle
        public float flowStart = 3f; // tiles past the cone's radius where the flows start
        public float flowMeander = 6f, flowWavelength = 40f;
        public int extraBridges = 1; // per flow, besides the road's crossing
        public float roadBulge = 12f; // tiles past the cone's radius the road passes round it
        public float shoreShare = 0.86f; // beyond this share of the way to the coast: the basalt shore
        public float flowsBand = 3f; // tiles from a flow's edge: the flows sub-biome
        public string crater = "crater", caves = "magma_caves", ash = "ash", flowsBiome = "flows", shore = "basalt";
        public SubBiome[] subBiomes = new SubBiome[0];
    }

    [NonSerialized] Vector2 coneAt = new(-1f, -1f); // the volcano's cone, picked by LayRelief as its mountain

    /// A volcano's cone (its middle tile), for the checks.
    public bool HasCone => coneAt.x >= 0f;
    public Vector2Int ConeTile => Vector2Int.FloorToInt(coneAt);
    [NonSerialized] public List<List<Vector2>> lavaFlows = new(); // the middle lines of the volcano's flows

    SubBiome VolcanoBiome(Volcano v, string key)
    {
        foreach (var s in v.subBiomes) if (s.key == key) return s;
        return v.subBiomes.Length > 0 ? v.subBiomes[0] : null;
    }

    void BuildVolcano(QuarterTest t, System.Random rng)
    {
        var v = t.volcano;
        var offsets = LayFan(t, rng, v.subBiomes);
        int n = size * size;
        var point = centre;
        var along = new Vector2(1f, 1f).normalized;
        float cone = t.relief.mountainRadius;
        coneAt = point + along * (v.coneShare * t.radius);

        // The flows: from the cone's foot outwards, spread round the fan's middle, winding, on past the coast.
        int count = rng.Next(v.flows[0], v.flows[Mathf.Min(1, v.flows.Length - 1)] + 1);
        float spread = v.flowSpread * Mathf.Deg2Rad;
        float baseAngle = Mathf.Atan2(along.y, along.x);
        lavaFlows.Clear();
        var flowLines = new List<List<(Vector2 at, float width)>>();
        for (int f = 0; f < count; f++)
        {
            float angle = baseAngle + (count == 1 ? 0f : Mathf.Lerp(-spread / 2f, spread / 2f, f / (count - 1f)))
                + ((float)rng.NextDouble() - 0.5f) * spread * 0.15f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-dir.y, dir.x);
            float phase = (float)rng.NextDouble() * Mathf.PI * 2f;
            var line = new List<(Vector2 at, float width)>();
            var middle = new List<Vector2>();
            float startAt = cone + v.flowStart, end = t.radius * 1.3f;
            for (float d = startAt; d <= end; d += 0.5f)
            {
                float share = Mathf.Clamp01((d - startAt) / (t.radius - startAt));
                float swing = Mathf.Sin(d / v.flowWavelength * Mathf.PI * 2f + phase) * v.flowMeander * Mathf.Clamp01((d - startAt) / 15f);
                var at = coneAt + dir * d + side * swing;
                float width = Mathf.Lerp(v.flowWidth[0], v.flowWidth[Mathf.Min(1, v.flowWidth.Length - 1)], share)
                    + (Fbm(d * 0.06f, f * 13f, offsets[3], 2) * 2f - 1f) * t.widthNoise * 0.5f;
                line.Add((at, width));
                middle.Add(at);
            }
            flowLines.Add(line);
            lavaFlows.Add(middle);
            foreach (var (at, width) in line) Stamp(at, width / 2f, i => { if (region[i] == 0) obstacle[i] = t.riverKind; });
            Stamp(line[0].at, line[0].width * 0.8f, i => { if (region[i] == 0) obstacle[i] = t.riverKind; }); // where it wells up
        }

        // Sub-biomes before the relief: the basalt shore near the coast, the flows along the lava, ash elsewhere.
        var ash = VolcanoBiome(v, v.ash);
        var shore = VolcanoBiome(v, v.shore);
        var flowsBiome = VolcanoBiome(v, v.flowsBiome);
        var nearLava = DistanceTo(i => obstacle[i] == t.riverKind);
        for (int i = 0; i < n; i++)
        {
            if (region[i] != 0) continue;
            float d = Vector2.Distance(new Vector2(i % size + 0.5f, i / size + 0.5f), point);
            biome[i] = d > t.radius * v.shoreShare ? shore : nearLava[i] <= v.flowsBand ? flowsBiome : ash;
        }

        // The road: from the portal on the left side round the cone to the one on the bottom side.
        var r = t.road;
        decor = new int[n];
        Vector2Int Inside(Vector2Int from, Vector2Int step)
        {
            var c = from;
            for (int k = 0; k < size && InMap(c.x, c.y) && region[Index(c.x, c.y)] != 0; k++) c += step;
            return c + step * 2;
        }
        int portalAt = Mathf.RoundToInt(point.x + t.radius * r.portalShare);
        var toPrevious = Inside(new Vector2Int(0, portalAt), Vector2Int.right);
        var toNext = Inside(new Vector2Int(portalAt, 0), Vector2Int.up);
        markers.Add(toPrevious);
        markers.Add(toNext);
        var round = coneAt + along * (cone + v.roadBulge);
        var points = new List<Vector2> { toPrevious, Vector2.Lerp(toPrevious, round, 0.5f) + Perp(toPrevious, round) * ((float)rng.NextDouble() - 0.5f) * r.wander,
            round, Vector2.Lerp(round, toNext, 0.5f) + Perp(round, toNext) * ((float)rng.NextDouble() - 0.5f) * r.wander, toNext };
        var road = Smooth(points);

        // Bridges: where the road crosses a flow, and a few further down each one.
        var crossings = new List<Vector2Int>();
        for (int k = 1; k < road.Count; k++)
        {
            var c = Vector2Int.FloorToInt(road[k]);
            if (!InMap(c.x, c.y) || obstacle[Index(c.x, c.y)] != t.riverKind) continue;
            if (crossings.Exists(o => Vector2Int.Distance(o, c) < 8f)) continue;
            crossings.Add(c);
        }
        foreach (var c in crossings) LayBridge(c, FlowAt(c), t);
        foreach (var middle in lavaFlows)
            for (int b = 0; b < v.extraBridges; b++)
            {
                int k = Mathf.Clamp(Mathf.RoundToInt(middle.Count * Mathf.Lerp(0.55f, 0.8f, (float)rng.NextDouble())), 1, middle.Count - 2);
                var c = Vector2Int.FloorToInt(middle[k]);
                if (crossings.Exists(o => Vector2Int.Distance(o, c) < 10f) || !InMap(c.x, c.y) || region[Index(c.x, c.y)] != 0) continue;
                LayBridge(c, middle[k + 1] - middle[k - 1], t);
            }

        // The road's tiles: land only (the bridges carry it over the lava), earth with stones here and there.
        var tiles = new HashSet<int>();
        foreach (var p in road) Stamp(p, r.width / 2f, i => { if (region[i] == 0 && obstacle[i] == null) tiles.Add(i); });
        var noise = new Vector2((float)rng.NextDouble() * 1000f, (float)rng.NextDouble() * 1000f);
        foreach (int i in tiles)
        {
            float clump = Fbm(i % size * 0.3f, i / size * 0.3f, noise, 1);
            decor[i] = rng.NextDouble() < r.stoneShare * (0.4f + 1.2f * clump) ? r.stoneDecor : r.earthDecor;
        }
        start = toPrevious + Vector2Int.right * 2; // arriving from the previous quarter
    }

    static Vector2 Perp(Vector2 a, Vector2 b)
    {
        var d = (b - a).normalized;
        return new Vector2(-d.y, d.x);
    }

    // The direction a flow runs at a tile (along its nearest middle line).
    Vector2 FlowAt(Vector2Int c)
    {
        var p = new Vector2(c.x + 0.5f, c.y + 0.5f);
        float best = float.MaxValue;
        var dir = Vector2.right;
        foreach (var middle in lavaFlows)
            for (int k = 1; k + 1 < middle.Count; k++)
            {
                float d = Vector2.Distance(middle[k], p);
                if (d < best) { best = d; dir = middle[k + 1] - middle[k - 1]; }
            }
        return dir;
    }

    // A bridge across a flow at that tile: straight rows of tiles across it (left to right where the flow runs up and
    // down, up and down where it runs left to right), bridgeWidth wide, as long as the lava.
    void LayBridge(Vector2Int middle, Vector2 flow, QuarterTest t)
    {
        bool horizontal = Mathf.Abs(flow.y) >= Mathf.Abs(flow.x);
        var step = horizontal ? Vector2Int.right : Vector2Int.up;
        for (int k = 0; k < t.bridgeWidth; k++)
        {
            var from = middle + (horizontal ? new Vector2Int(0, k) : new Vector2Int(k, 0));
            foreach (int dir in new[] { 1, -1 })
                for (var c = from; InMap(c.x, c.y) && region[Index(c.x, c.y)] == 0 && obstacle[Index(c.x, c.y)] == t.riverKind; c += step * dir)
                    obstacle[Index(c.x, c.y)] = t.bridgeKind;
        }
    }

    // Distance (tiles, 4-way steps) from every tile to the nearest one that matches.
    int[] DistanceTo(Func<int, bool> from)
    {
        int n = size * size;
        var dist = new int[n];
        var queue = new Queue<int>();
        for (int i = 0; i < n; i++)
        {
            dist[i] = from(i) ? 0 : int.MaxValue;
            if (dist[i] == 0) queue.Enqueue(i);
        }
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
        return dist;
    }

    // The mountain's top level as a crater: lava over its middle, a rim of craterMargin tiles left to walk round it
    // (its stairs and its faces untouched).
    void Crater(Relief r, Vector2 centre, float radius, bool[] cliff, bool[] ramps, int[] face)
    {
        int top = r.mountainLevels;
        var rim = DistanceTo(i => height[i] < top || cliff[i] || ramps[i] || face[i] > 0);
        string lava = themes[0].riverKind;
        Stamp(centre, radius + r.faceRows * top, i =>
        {
            if (height[i] == top && face[i] == 0 && !cliff[i] && rim[i] > r.craterMargin) obstacle[i] = lava;
        });
    }

    // After the relief: the cone's levels are the magma caves, its top the crater.
    void FinishVolcano(QuarterTest t)
    {
        var v = t.volcano;
        var caves = VolcanoBiome(v, v.caves);
        var crater = VolcanoBiome(v, v.crater);
        int top = t.relief.mountainLevels;
        float reach = t.relief.mountainRadius * 1.3f + t.relief.faceRows * top;
        for (int i = 0; i < size * size; i++)
        {
            if (region[i] != 0 || height == null || height[i] == 0) continue;
            if (Vector2.Distance(new Vector2(i % size + 0.5f, i / size + 0.5f), coneAt) > reach) continue;
            biome[i] = height[i] >= top ? crater : caves;
        }
    }
}
