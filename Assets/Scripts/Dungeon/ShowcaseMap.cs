using System;
using System.Collections.Generic;
using UnityEngine;

/// The showcase of the new island art (GDD, "Refonte visuelle du terrain"; Assets/Resources/Data/showcase.json): a
/// small piece of island laid out by hand with lines and a polygon, in tiles from the bottom left corner. It gives the
/// gameplay grid (land, river, bridge, cells blocked by a trunk or a big rock) and where each decor element stands;
/// ShowcaseView draws it. The edges are smooth fields (signed distances, in tiles): the drawing makes them organic with
/// noise, the grid takes them at each tile's centre.
[Serializable]
public class ShowcaseMap
{
    [Serializable] public class Line { public Vector2[] points = new Vector2[0]; public float width = 1f; }
    [Serializable] public class Wood { public Vector2 centre; public float radius = 4f; public int trees = 3; }
    [Serializable]
    public class Scatter
    {
        public float tufts = 20f, tallGrass = 5f;
        public int flowerClusters = 10, bushes = 6, smallRocks = 6, mediumRocks = 3, largeRocks = 2, mushrooms = 4,
            pebbles = 6, reeds = 12, lilies = 5, stumps = 1, logs = 1;
    }

    public enum Ground { Sky, Grass, Path, Bank, Water }

    /// A decor element: its file in Resources/IslandArt, its foot in tiles.
    public struct Prop
    {
        public string file;
        public Vector2 at;
    }

    public int width = 40, height = 30, seed = 7;
    public Vector2[] sky = new Vector2[0];
    public Line river = new(), path = new();
    public float bankWidth = 0.75f;
    public Wood[] woods = new Wood[0];
    public Scatter scatter = new();
    public Vector2 spawn;

    [NonSerialized] public readonly List<Prop> props = new();
    [NonSerialized] public bool[] blocked; // per tile: a trunk, a big rock, a stump or a log stands there
    [NonSerialized] public Vector2 bridgeAt; // where the path crosses the river (tiles)
    [NonSerialized] public bool bridgeAcross = true; // the bridge goes from left to right (the river runs up and down)

    public static ShowcaseMap Load()
    {
        var asset = Resources.Load<TextAsset>("Data/showcase");
        var map = asset != null ? JsonUtility.FromJson<ShowcaseMap>(asset.text) : new ShowcaseMap();
        map.Lay();
        return map;
    }

    // ---- Fields (tiles; positive inside) ----

    /// Distance to the island edge: positive on land, negative in the sky.
    public float LandField(Vector2 p)
    {
        float d = DistanceToPolygon(sky, p);
        return Inside(sky, p) ? -d : d;
    }

    public float WaterField(Vector2 p) => river.width / 2f - DistanceToLine(river, p);
    public float BankField(Vector2 p) => river.width / 2f + bankWidth - DistanceToLine(river, p);
    public float PathField(Vector2 p) => path.width / 2f - DistanceToLine(path, p);

    /// What lies at a point, without the drawing's noise (water before path: the bridge is laid over).
    public Ground At(Vector2 p) =>
        LandField(p) <= 0f ? Ground.Sky : WaterField(p) > 0f ? Ground.Water : PathField(p) > 0f ? Ground.Path
        : BankField(p) > 0f ? Ground.Bank : Ground.Grass;

    public Ground Cell(int x, int y) => At(new Vector2(x + 0.5f, y + 0.5f));

    /// A river tile the path crosses: the bridge.
    public bool Bridge(int x, int y)
    {
        var p = new Vector2(x + 0.5f, y + 0.5f);
        return WaterField(p) > 0f && PathField(p) > -0.2f;
    }

    public bool Blocked(int x, int y) => blocked != null && x >= 0 && y >= 0 && x < width && y < height && blocked[y * width + x];

    // ---- Layout ----

    void Lay()
    {
        blocked = new bool[width * height];
        props.Clear();
        var rng = new System.Random(seed);
        FindBridge();
        var taken = new List<(Vector2 at, float radius)>();

        // Trees in the woods, apart from each other.
        foreach (var wood in woods)
            for (int t = 0, tries = 0; t < wood.trees && tries < 200; tries++)
            {
                var p = wood.centre + RandomInDisc(rng) * wood.radius;
                if (!Clear(p, 1.2f) || Near(taken, p, 2.4f)) continue;
                int kind = rng.Next(1, 4);
                Add($"arbre_{kind}_tronc", p, true);
                taken.Add((p, 1.6f));
                t++;
            }
        // Elements that block their tile, then the others.
        Place(rng, taken, "rocher_grand", scatter.largeRocks, 1.2f, true, Ground.Grass);
        Place(rng, taken, "rocher_moyen", scatter.mediumRocks, 0.9f, true, Ground.Grass);
        Place(rng, taken, "souche", scatter.stumps, 1f, true, Ground.Grass);
        Place(rng, taken, "tronc_couche", scatter.logs, 1.4f, true, Ground.Grass);
        for (int k = 0; k < scatter.bushes; k++) Place(rng, taken, $"buisson_{rng.Next(1, 4)}", 1, 1f, false, Ground.Grass);
        Place(rng, taken, "rocher_petit", scatter.smallRocks, 0.6f, false, Ground.Grass);
        // Mushrooms near the trees.
        for (int k = 0, tries = 0; k < scatter.mushrooms && tries < 200 && woods.Length > 0; tries++)
        {
            var wood = woods[rng.Next(woods.Length)];
            var p = wood.centre + RandomInDisc(rng) * (wood.radius + 1f);
            if (!Clear(p, 0.4f) || Near(taken, p, 0.6f)) continue;
            Add($"champignons_{rng.Next(1, 3)}", p, false);
            taken.Add((p, 0.4f));
            k++;
        }
        // The banks: pebbles, reeds near the water; lily pads on it, off the bridge.
        for (int k = 0, tries = 0; k < scatter.pebbles && tries < 400; tries++)
        {
            var p = RandomPoint(rng);
            if (At(p) != Ground.Bank) continue;
            Add($"galets_{rng.Next(1, 3)}", p, false);
            k++;
        }
        for (int k = 0, tries = 0; k < scatter.reeds && tries < 600; tries++)
        {
            var p = RandomPoint(rng);
            if (At(p) != Ground.Bank || WaterField(p) < -0.45f || Vector2.Distance(p, bridgeAt) < 3f) continue;
            Add($"roseau_{rng.Next(1, 3)}", p, false);
            k++;
        }
        for (int k = 0, tries = 0; k < scatter.lilies && tries < 600; tries++)
        {
            var p = RandomPoint(rng);
            if (WaterField(p) < 0.35f || Vector2.Distance(p, bridgeAt) < 3.5f) continue;
            Add($"nenuphar_{rng.Next(1, 3)}", p, false);
            k++;
        }
        // Flowers in clusters of one kind.
        string[] kinds = { "blanche", "jaune", "rose", "bleue" };
        for (int c = 0, tries = 0; c < scatter.flowerClusters && tries < 300; tries++)
        {
            var centre = RandomPoint(rng);
            if (!Clear(centre, 0.6f)) continue;
            string kind = kinds[rng.Next(kinds.Length)];
            for (int f = 0, n = rng.Next(4, 9); f < n; f++)
            {
                var p = centre + RandomInDisc(rng) * 1.3f;
                if (Clear(p, 0.3f) && !Near(taken, p, 0.1f)) Add($"fleur_{kind}_{rng.Next(1, 3)}", p, false);
            }
            c++;
        }
        // Grass all over the meadow.
        int grassTiles = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (Cell(x, y) == Ground.Grass) grassTiles++;
        int tufts = Mathf.RoundToInt(grassTiles * scatter.tufts / 100f), tall = Mathf.RoundToInt(grassTiles * scatter.tallGrass / 100f);
        for (int k = 0, tries = 0; k < tufts && tries < tufts * 10; tries++)
        {
            var p = RandomPoint(rng);
            if (!Clear(p, 0.25f) || Near(taken, p, 0.1f)) continue;
            Add($"herbe_touffe_{rng.Next(1, 5)}", p, false);
            k++;
        }
        for (int k = 0, tries = 0; k < tall && tries < tall * 10; tries++)
        {
            var p = RandomPoint(rng);
            if (!Clear(p, 0.4f) || Near(taken, p, 0.2f)) continue;
            Add($"herbe_haute_{rng.Next(1, 3)}", p, false);
            k++;
        }
    }

    // Count elements of one file on the grass, apart from the others; block: they block their tile.
    void Place(System.Random rng, List<(Vector2 at, float radius)> taken, string file, int count, float radius, bool block, Ground on)
    {
        for (int k = 0, tries = 0; k < count && tries < 300; tries++)
        {
            var p = RandomPoint(rng);
            if (!Clear(p, radius * 0.8f) || Near(taken, p, radius) || Vector2.Distance(p, spawn) < 2.5f) continue;
            Add(file, p, block);
            taken.Add((p, radius));
            k++;
        }
    }

    void Add(string file, Vector2 at, bool block)
    {
        at = new Vector2(Mathf.Round(at.x * 24f) / 24f, Mathf.Round(at.y * 24f) / 24f); // on the art's pixels
        props.Add(new Prop { file = file, at = at });
        int x = Mathf.FloorToInt(at.x), y = Mathf.FloorToInt(at.y);
        if (block && x >= 0 && y >= 0 && x < width && y < height) blocked[y * width + x] = true;
    }

    // Grass all around a point (no path, bank, water or sky within the radius), away from the arrival point.
    bool Clear(Vector2 p, float radius)
    {
        if (p.x < 0.5f || p.y < 0.5f || p.x > width - 0.5f || p.y > height - 0.5f) return false;
        if (LandField(p) < radius + 0.3f || BankField(p) > -radius || PathField(p) > -radius) return false;
        return Vector2.Distance(p, spawn) > 1.5f;
    }

    static bool Near(List<(Vector2 at, float radius)> taken, Vector2 p, float radius)
    {
        foreach (var (at, r) in taken)
            if (Vector2.Distance(at, p) < r + radius) return true;
        return false;
    }

    Vector2 RandomPoint(System.Random rng) => new((float)rng.NextDouble() * width, (float)rng.NextDouble() * height);

    static Vector2 RandomInDisc(System.Random rng)
    {
        float a = (float)rng.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rng.NextDouble());
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
    }

    // Where the path line crosses the river line, and which way the bridge goes.
    void FindBridge()
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < path.points.Length; i++)
            for (float t = 0f; t <= 1f; t += 0.02f)
            {
                var p = Vector2.Lerp(path.points[i], path.points[i + 1], t);
                float d = DistanceToLine(river, p);
                if (d < best) { best = d; bridgeAt = p; }
            }
        // The river's direction there: mostly up and down means a bridge going across.
        var a = bridgeAt + Vector2.up * 0.5f;
        var b = bridgeAt - Vector2.up * 0.5f;
        bridgeAcross = Mathf.Abs(DistanceToLine(river, a) - DistanceToLine(river, b)) < 0.7f;
    }

    // ---- Geometry ----

    static float DistanceToLine(Line line, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < line.points.Length; i++) best = Mathf.Min(best, DistanceToSegment(p, line.points[i], line.points[i + 1]));
        return best;
    }

    static float DistanceToPolygon(Vector2[] polygon, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i < polygon.Length; i++) best = Mathf.Min(best, DistanceToSegment(p, polygon[i], polygon[(i + 1) % polygon.Length]));
        return best;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        var ab = b - a;
        float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    static bool Inside(Vector2[] polygon, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            if ((polygon[i].y > p.y) != (polygon[j].y > p.y)
                && p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x)
                inside = !inside;
        return inside;
    }
}
