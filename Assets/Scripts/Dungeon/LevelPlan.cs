using System;
using System.Collections.Generic;
using UnityEngine;

/// A level in the manner of Minecraft Dungeons (Assets/Resources/Data/levels.json): one continuous map, crossed
/// without any fade. Zones (rooms of the catalogue, RoomCatalog) are laid along a main path from the start to the
/// boss, turning at random; side branches lead to optional zones (challenge, rest), and a branch may loop back to
/// the main path. Natural zones get an irregular outline, built ones (ruins, maze...) keep their rectangle and wall
/// plan; winding paths of varying width join them. DungeonGenerator carves it (Carve), then lays each zone's
/// obstacles (Obstacles.GenerateZone).
public class LevelPlan
{
    public class Zone
    {
        public string type;
        public RoomInstance room; // its content: size, wall plan, obstacles
        public Vector2 centre; // in tiles, on the canvas
        public Vector2 radius; // half width and half height
        public bool built; // rectangle with its wall plan, else an irregular outline
        public bool main; // on the main path
        public readonly List<Zone> links = new();
        public readonly List<Vector2Int> entries = new(); // where the paths come in (kept clear)
        public RectInt Rect => new(Mathf.RoundToInt(centre.x - radius.x), Mathf.RoundToInt(centre.y - radius.y),
            Mathf.RoundToInt(radius.x * 2f), Mathf.RoundToInt(radius.y * 2f));
    }

    [Serializable]
    public class Settings
    {
        public int[] mainZones = { 7, 9 }; // start and boss included
        public int[] branches = { 2, 3 };
        public int[] branchLength = { 1, 2 };
        public int[] loops = { 0, 1 };
        public int[] challenge = { 1, 2 };
        public int[] rest = { 0, 1 };
        public int[] monsterHouse = { 1, 1 };
        public float[] gap = { 7f, 13f }; // tiles of path between two zones' edges
        public float turn = 55f; // degrees the main path may turn at each zone
        public float zoneScale = 0.85f; // zone size from its room's size
        public int[] pathWidth = { 3, 5 };
        public float wiggle = 2.5f; // tiles a path strays from the straight line
        public int border = 4; // tiles of wall between the outermost zones and the edge of the map
        public float pocketChance = 0.4f; // chance, every pocketEvery tiles of path, of a recess on its side
        public float pocketEvery = 10f;
        public float[] pocketRadius = { 2f, 4f };
    }

    [Serializable] class File { public Settings level = new(); }

    public readonly List<Zone> zones = new();
    public Zone start, boss;
    public string theme; // the level's theme (rooms.json), one décor for the whole level for now
    public int tileset;
    public Vector2Int size; // canvas, in tiles

    public static Settings LoadSettings()
    {
        var asset = Resources.Load<TextAsset>("Data/levels");
        return asset != null ? JsonUtility.FromJson<File>(asset.text).level : new Settings();
    }

    public static LevelPlan Generate(System.Random rng, Settings s = null)
    {
        s ??= LoadSettings();
        for (int attempt = 0; attempt < 100; attempt++)
        {
            var plan = new LevelPlan();
            if (plan.Build(rng, s)) return plan;
        }
        throw new InvalidOperationException("LevelPlan: no layout found with these settings");
    }

    static int Pick(int[] range, System.Random rng) =>
        range == null || range.Length == 0 ? 0 : rng.Next(range[0], range[Mathf.Min(1, range.Length - 1)] + 1);

    static float Range(float[] range, System.Random rng) => range[0] + (float)rng.NextDouble() * (range[1] - range[0]);

    static Vector2 Dir(float degrees) => new(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

    bool Build(System.Random rng, Settings s)
    {
        // One theme and one décor for the whole level (sub-biomes come next).
        var probe = RoomCatalog.Pick("combat", rng);
        if (probe == null) return false;
        theme = probe.theme.key;
        tileset = probe.tileset;

        // Main path.
        int mainCount = Pick(s.mainZones, rng);
        float heading = (float)rng.NextDouble() * 360f;
        Zone previous = null;
        for (int i = 0; i < mainCount; i++)
        {
            string type = i == 0 ? "start" : i == mainCount - 1 ? "boss" : i == mainCount - 2 ? "subboss" : "combat";
            var zone = MakeZone(type, rng, s);
            if (zone == null) return false;
            zone.main = true;
            if (previous == null) zone.centre = Vector2.zero;
            else if (!PlaceNext(zone, previous, ref heading, s.turn, rng, s)) return false;
            if (previous != null) Link(previous, zone);
            zones.Add(zone);
            previous = zone;
        }
        start = zones[0];
        boss = zones[^1];
        var plain = zones.FindAll(z => z.type == "combat");
        for (int i = 0, n = Pick(s.monsterHouse, rng); i < n && plain.Count > 0; i++)
        {
            var z = plain[rng.Next(plain.Count)];
            plain.Remove(z);
            Retype(z, "monster_house", rng, s);
        }

        // Branches from the middle of the main path, ending in the optional zones.
        var ends = new List<string>();
        for (int i = 0, n = Pick(s.challenge, rng); i < n; i++) ends.Add("challenge");
        for (int i = 0, n = Pick(s.rest, rng); i < n; i++) ends.Add("rest");
        int branches = Mathf.Max(Pick(s.branches, rng), ends.Count);
        var roots = zones.FindAll(z => z.main && z != start && z != boss && z.type != "subboss");
        for (int b = 0; b < branches && roots.Count > 0; b++)
        {
            var root = roots[rng.Next(roots.Count)];
            int length = Pick(s.branchLength, rng);
            var from = root;
            float bearing = Bearing(root) + (rng.Next(2) == 0 ? 90f : -90f);
            for (int k = 0; k < length; k++)
            {
                string type = k == length - 1 && b < ends.Count ? ends[b] : "combat";
                var zone = MakeZone(type, rng, s);
                if (zone == null || !PlaceNext(zone, from, ref bearing, s.turn * 0.6f, rng, s)) break;
                Link(from, zone);
                zones.Add(zone);
                from = zone;
            }
        }
        if (zones.FindAll(z => z.type == "challenge").Count < s.challenge[0]) return false;

        // Loops: a branch's end joined to a nearby zone of the main path.
        for (int l = 0, n = Pick(s.loops, rng); l < n; l++)
            foreach (var z in zones)
            {
                if (z.main || z.links.Count != 1) continue;
                var target = zones.Find(m => m.main && m != z.links[0] && m != boss && !m.links.Contains(z)
                    && Vector2.Distance(m.centre, z.centre) < Mathf.Max(m.radius.x, m.radius.y) + Mathf.Max(z.radius.x, z.radius.y) + s.gap[1] * 1.3f);
                if (target == null) continue;
                Link(z, target);
                break;
            }

        // Canvas: everything shifted into positive tiles with a border.
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var z in zones)
        {
            minX = Mathf.Min(minX, z.centre.x - z.radius.x); maxX = Mathf.Max(maxX, z.centre.x + z.radius.x);
            minY = Mathf.Min(minY, z.centre.y - z.radius.y); maxY = Mathf.Max(maxY, z.centre.y + z.radius.y);
        }
        var shift = new Vector2(s.border + s.wiggle + 1f - minX, s.border + s.wiggle + 1f - minY);
        foreach (var z in zones) z.centre += shift;
        size = new Vector2Int(Mathf.CeilToInt(maxX - minX + 2f * (s.border + s.wiggle + 1f)), Mathf.CeilToInt(maxY - minY + 2f * (s.border + s.wiggle + 1f)));
        return true;
    }

    Zone MakeZone(string type, System.Random rng, Settings s)
    {
        var room = RoomCatalog.Pick(type, rng, type == "boss" ? null : theme);
        if (room == null) return null;
        room.tileset = tileset; // one décor for the level
        if (room.obstacles != null) // its liquid drawn by the level's décor only where that décor's layer suits it
        {
            var t = room.theme;
            bool own = !string.IsNullOrEmpty(t.liquid) && (t.liquidDecors.Length == 0 || Array.IndexOf(t.liquidDecors, tileset) >= 0);
            room.obstacles.decorKind = own ? t.liquid : null;
        }
        string style = room.layout?.style ?? "";
        return new Zone
        {
            type = type,
            room = room,
            radius = new Vector2(room.size.x, room.size.y) * (0.5f * s.zoneScale),
            built = style == "ring" || style == "cross" || style == "maze" || style == "bars",
        };
    }

    void Retype(Zone z, string type, System.Random rng, Settings s)
    {
        var other = MakeZone(type, rng, s);
        if (other == null) return;
        z.type = type;
        z.room = other.room;
        z.built = other.built;
        z.radius = Vector2.Min(z.radius, other.radius); // never larger than the place it was given
    }

    static void Link(Zone a, Zone b)
    {
        a.links.Add(b);
        b.links.Add(a);
    }

    // Direction the main path came in by (for branches to leave sideways).
    static float Bearing(Zone z)
    {
        var from = z.links.Find(l => l.main);
        if (from == null) return 0f;
        var d = z.centre - from.centre;
        return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
    }

    // Puts "zone" a path's length from "from", turning by up to "turn" degrees, clear of every other zone.
    bool PlaceNext(Zone zone, Zone from, ref float heading, float turn, System.Random rng, Settings s)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            float h = heading + ((float)rng.NextDouble() * 2f - 1f) * turn * (1f + attempt * 0.25f);
            var d = Dir(h);
            float reach = Extent(from, d) + Extent(zone, -d) + Range(s.gap, rng);
            zone.centre = from.centre + d * reach;
            bool clear = true;
            foreach (var other in zones)
            {
                if (other == from) continue;
                var gap = other.centre - zone.centre;
                if (Mathf.Abs(gap.x) < other.radius.x + zone.radius.x + s.gap[0] && Mathf.Abs(gap.y) < other.radius.y + zone.radius.y + s.gap[0])
                {
                    clear = false;
                    break;
                }
            }
            if (!clear) continue;
            heading = h;
            return true;
        }
        return false;
    }

    // Distance from a zone's centre to its edge in a direction (its bounding ellipse).
    static float Extent(Zone z, Vector2 d) => 1f / Mathf.Sqrt(d.x * d.x / (z.radius.x * z.radius.x) + d.y * d.y / (z.radius.y * z.radius.y));

    // ---------------------------------------------------------------------------------------------- carving

    /// Opens the zones and the paths in "open" (true: floor), the canvas placed at "origin" of the grid; then walls up
    /// what the start cannot reach. Fills each zone's entries (in grid tiles).
    public void Carve(bool[,] open, Vector2Int origin, System.Random rng, int clear)
    {
        var s = LoadSettings();
        // Entries: on each zone's edge toward each linked zone.
        foreach (var z in zones)
        {
            z.entries.Clear();
            foreach (var l in z.links)
            {
                var d = (l.centre - z.centre).normalized;
                var p = z.centre + d * (Extent(z, d) * 0.8f);
                z.entries.Add(origin + Vector2Int.RoundToInt(p));
            }
        }
        foreach (var z in zones)
        {
            var rect = Shift(z.Rect, origin);
            if (z.built) Fill(open, rect);
            else Blob(open, z, origin, rng);
            var keep = new List<Vector2Int> { origin + Vector2Int.RoundToInt(z.centre) };
            keep.AddRange(z.entries);
            RoomLayouts.Apply(open, rect, keep, z.room.layout, rng, clear);
        }
        // Paths between linked zones, each drawn once.
        var done = new HashSet<(Zone, Zone)>();
        foreach (var z in zones)
            for (int i = 0; i < z.links.Count; i++)
            {
                var l = z.links[i];
                if (done.Contains((l, z))) continue;
                done.Add((z, l));
                var a = z.entries[i];
                var b = l.entries[l.links.IndexOf(z)];
                Path(open, origin + Vector2Int.RoundToInt(z.centre), a, b, origin + Vector2Int.RoundToInt(l.centre), rng, s);
            }
        SealFrom(open, origin + Vector2Int.RoundToInt(start.centre));
    }

    static RectInt Shift(RectInt r, Vector2Int by) => new(r.xMin + by.x, r.yMin + by.y, r.width, r.height);

    static void Fill(bool[,] open, RectInt r)
    {
        for (int x = r.xMin; x < r.xMax; x++)
            for (int y = r.yMin; y < r.yMax; y++)
                if (In(open, x, y)) open[x, y] = true;
    }

    static bool In(bool[,] open, int x, int y) => x > 0 && y > 0 && x < open.GetLength(0) - 1 && y < open.GetLength(1) - 1;

    // An irregular outline: the zone's ellipse with a radius that wavers around it.
    static void Blob(bool[,] open, Zone z, Vector2Int origin, System.Random rng)
    {
        var waves = new (int k, float a, float phase)[4];
        for (int i = 0; i < waves.Length; i++)
            waves[i] = (2 + i, 0.05f + (float)rng.NextDouble() * 0.08f, (float)rng.NextDouble() * Mathf.PI * 2f);
        var r = z.Rect;
        for (int x = r.xMin - 2; x <= r.xMax + 2; x++)
            for (int y = r.yMin - 2; y <= r.yMax + 2; y++)
            {
                float dx = (x + 0.5f - z.centre.x) / z.radius.x, dy = (y + 0.5f - z.centre.y) / z.radius.y;
                float angle = Mathf.Atan2(dy, dx), edge = 1f;
                foreach (var (k, a, phase) in waves) edge += a * Mathf.Sin(k * angle + phase);
                if (Mathf.Sqrt(dx * dx + dy * dy) <= edge && In(open, x + origin.x, y + origin.y)) open[x + origin.x, y + origin.y] = true;
            }
    }

    // A winding path from a zone's centre to the next one's, through their entries: discs of a varying width along a
    // line that strays sideways (never at its ends).
    static void Path(bool[,] open, Vector2Int from, Vector2Int a, Vector2Int b, Vector2Int to, System.Random rng, Settings s)
    {
        Disc(open, from, a, Pick(s.pathWidth, rng) / 2f);
        Disc(open, b, to, Pick(s.pathWidth, rng) / 2f);
        Vector2 pa = a, pb = b;
        float length = Vector2.Distance(pa, pb);
        if (length < 0.5f) return;
        var side = Vector2.Perpendicular((pb - pa) / length);
        float freq = 0.5f + (float)rng.NextDouble(), phase = (float)rng.NextDouble() * Mathf.PI * 2f;
        float w0 = s.pathWidth[0], w1 = s.pathWidth[Mathf.Min(1, s.pathWidth.Length - 1)];
        float nextPocket = s.pocketEvery * (0.5f + (float)rng.NextDouble());
        for (float t = 0f; t <= 1f; t += 0.5f / length)
        {
            float sway = Mathf.Sin(t * Mathf.PI) * s.wiggle * Mathf.Sin(t * Mathf.PI * 2f * freq + phase);
            var p = Vector2.Lerp(pa, pb, t) + side * sway;
            float width = Mathf.Lerp(w0, w1, 0.5f + 0.5f * Mathf.Sin(t * 7f + phase));
            Stamp(open, p, width / 2f);
            // Now and then a recess on one side: a nook to look into, a wider spot to fight in.
            if (t * length >= nextPocket)
            {
                nextPocket += s.pocketEvery;
                if (rng.NextDouble() < s.pocketChance && t > 0.1f && t < 0.9f)
                {
                    float r = Range(s.pocketRadius, rng);
                    Stamp(open, p + side * ((rng.Next(2) == 0 ? 1f : -1f) * (width / 2f + r * 0.6f)), r);
                }
            }
        }
    }

    // Discs along a straight segment.
    static void Disc(bool[,] open, Vector2 a, Vector2 b, float r)
    {
        float length = Vector2.Distance(a, b);
        for (float t = 0f; t <= 1f; t += length > 0f ? 0.5f / length : 1f)
            Stamp(open, Vector2.Lerp(a, b, t), r);
    }

    static void Stamp(bool[,] open, Vector2 p, float r)
    {
        for (int x = Mathf.FloorToInt(p.x - r); x <= Mathf.CeilToInt(p.x + r); x++)
            for (int y = Mathf.FloorToInt(p.y - r); y <= Mathf.CeilToInt(p.y + r); y++)
                if ((x + 0.5f - p.x) * (x + 0.5f - p.x) + (y + 0.5f - p.y) * (y + 0.5f - p.y) <= r * r && In(open, x, y))
                    open[x, y] = true;
    }

    // Floor the start cannot reach becomes wall.
    static void SealFrom(bool[,] open, Vector2Int startCell)
    {
        int w = open.GetLength(0), h = open.GetLength(1);
        var reached = new bool[w, h];
        var queue = new Queue<Vector2Int>();
        if (!open[startCell.x, startCell.y]) return;
        reached[startCell.x, startCell.y] = true;
        queue.Enqueue(startCell);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = c + d;
                if (n.x < 0 || n.y < 0 || n.x >= w || n.y >= h || reached[n.x, n.y] || !open[n.x, n.y]) continue;
                reached[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (open[x, y] && !reached[x, y]) open[x, y] = false;
    }
}
