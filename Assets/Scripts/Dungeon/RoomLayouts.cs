using System;
using System.Collections.Generic;
using UnityEngine;

/// Wall plan of a room (rooms.json, "layout"): which tiles inside the room are walls. Styles:
/// - open: nothing; blocks: rectangular blocks here and there (count, sizes);
/// - pillars: a regular grid of pillars (spacing, sizes);
/// - cross: the four corners filled, a plus-shaped hall left (corridor: width of its arms);
/// - ring: a wall frame inside the room (inset from the outer wall, thickness) with openings (gaps);
/// - bars: parallel walls across the room (spacing), each with one or two openings;
/// - maze: corridors (corridor wide) between thin walls, as in the maze rooms of the original game.
/// The spawn point and the doors stay clear ("keep", clear tiles around each); a door cut off from the spawn point by
/// the plan gets a corridor to it; tiles cut off from it become walls afterwards.
[Serializable]
public class RoomLayout
{
    public string style = ""; // empty or "open": no walls
    public int[] count = { 2, 4 };
    public int[] sizes = { 2, 3 };
    public int[] spacing = { 4, 5 };
    public int[] corridor = { 3, 4 };
    public int[] inset = { 4, 5 };
    public int thickness = 1;
    public int[] gaps = { 2, 4 };
}

public static class RoomLayouts
{
    const int GapWidth = 3; // tiles of an opening in a wall line

    /// Lays the walls of "layout" in "open" (true: floor) over the "inside" rectangle, "spawn" kept clear.
    public static void Apply(bool[,] open, RectInt inside, List<Vector2Int> keep, RoomLayout layout, System.Random rng, int clear)
    {
        var spawn = keep[0];
        if (layout == null) return;
        var walls = new bool[open.GetLength(0), open.GetLength(1)];
        switch (layout.style)
        {
            case "blocks": Blocks(walls, inside, layout, rng, spawn, clear); break;
            case "pillars": Pillars(walls, inside, layout, rng); break;
            case "cross": Cross(walls, inside, spawn, Pick(layout.corridor, rng)); break;
            case "ring": Ring(walls, inside, layout, rng); break;
            case "bars": Bars(walls, inside, layout, rng); break;
            case "maze": Maze(walls, inside, spawn, Pick(layout.corridor, rng), rng); break;
        }
        for (int x = inside.xMin; x < inside.xMax; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                if (walls[x, y] && !NearAny(keep, x, y, clear)) open[x, y] = false;
        for (int k = 1; k < keep.Count; k++)
            if (!Reaches(open, inside, spawn, keep[k])) Corridor(open, inside, keep[k], spawn);
        SealUnreachable(open, inside, spawn);
    }

    static bool NearAny(List<Vector2Int> keep, int x, int y, int clear)
    {
        foreach (var k in keep)
            if (Mathf.Max(Mathf.Abs(x - k.x), Mathf.Abs(y - k.y)) <= clear) return true;
        return false;
    }

    static bool Reaches(bool[,] open, RectInt inside, Vector2Int from, Vector2Int to)
    {
        if (!open[from.x, from.y] || !open[to.x, to.y]) return false;
        var seen = new HashSet<Vector2Int> { from };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c == to) return true;
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = c + d;
                if (inside.Contains(n) && open[n.x, n.y] && seen.Add(n)) queue.Enqueue(n);
            }
        }
        return false;
    }

    // An L-shaped corridor two tiles wide, first along x then along y.
    static void Corridor(bool[,] open, RectInt inside, Vector2Int a, Vector2Int b)
    {
        var c = a;
        while (c.x != b.x) { Open2(open, inside, c); c.x += Math.Sign(b.x - c.x); }
        while (c.y != b.y) { Open2(open, inside, c); c.y += Math.Sign(b.y - c.y); }
        Open2(open, inside, c);
    }

    static void Open2(bool[,] open, RectInt inside, Vector2Int c)
    {
        for (int dx = 0; dx < 2; dx++)
            for (int dy = 0; dy < 2; dy++)
                if (inside.Contains(new Vector2Int(c.x + dx, c.y + dy))) open[c.x + dx, c.y + dy] = true;
    }

    static int Pick(int[] range, System.Random rng) =>
        range == null || range.Length == 0 ? 0 : rng.Next(range[0], range[Mathf.Min(1, range.Length - 1)] + 1);

    static void Fill(bool[,] walls, RectInt r)
    {
        for (int x = r.xMin; x < r.xMax; x++)
            for (int y = r.yMin; y < r.yMax; y++)
                walls[x, y] = true;
    }

    // Blocks with a free ring around each, so nothing is sealed off.
    static void Blocks(bool[,] walls, RectInt inside, RoomLayout l, System.Random rng, Vector2Int spawn, int clear)
    {
        int wanted = Pick(l.count, rng), placed = 0;
        for (int attempt = 0; attempt < wanted * 20 && placed < wanted; attempt++)
        {
            int w = Pick(l.sizes, rng), h = Pick(l.sizes, rng);
            if (w + 2 >= inside.width || h + 2 >= inside.height) continue;
            var b = new RectInt(rng.Next(inside.xMin + 1, inside.xMax - w), rng.Next(inside.yMin + 1, inside.yMax - h), w, h);
            if (!Free(walls, new RectInt(b.xMin - 1, b.yMin - 1, w + 2, h + 2))) continue;
            if (b.xMin - clear <= spawn.x && spawn.x <= b.xMax + clear && b.yMin - clear <= spawn.y && spawn.y <= b.yMax + clear) continue;
            Fill(walls, b);
            placed++;
        }
    }

    static bool Free(bool[,] walls, RectInt r)
    {
        for (int x = r.xMin; x < r.xMax; x++)
            for (int y = r.yMin; y < r.yMax; y++)
                if (walls[x, y]) return false;
        return true;
    }

    static void Pillars(bool[,] walls, RectInt inside, RoomLayout l, System.Random rng)
    {
        int step = Mathf.Max(3, Pick(l.spacing, rng)), s = Mathf.Max(1, Pick(l.sizes, rng));
        // Centred grid, a free row along the outer walls.
        int nx = (inside.width - 2) / step, ny = (inside.height - 2) / step;
        int x0 = inside.xMin + (inside.width - (nx - 1) * step - s) / 2, y0 = inside.yMin + (inside.height - (ny - 1) * step - s) / 2;
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
                Fill(walls, new RectInt(x0 + i * step, y0 + j * step, s, s));
    }

    static void Cross(bool[,] walls, RectInt inside, Vector2Int spawn, int arm)
    {
        int half = Mathf.Max(1, arm / 2);
        int left = spawn.x - half, right = spawn.x + (arm - half), bottom = spawn.y - half, top = spawn.y + (arm - half);
        Fill(walls, new RectInt(inside.xMin, inside.yMin, left - inside.xMin, bottom - inside.yMin));
        Fill(walls, new RectInt(right, inside.yMin, inside.xMax - right, bottom - inside.yMin));
        Fill(walls, new RectInt(inside.xMin, top, left - inside.xMin, inside.yMax - top));
        Fill(walls, new RectInt(right, top, inside.xMax - right, inside.yMax - top));
    }

    static void Ring(bool[,] walls, RectInt inside, RoomLayout l, System.Random rng)
    {
        int inset = Pick(l.inset, rng), t = Mathf.Max(1, l.thickness);
        var outer = new RectInt(inside.xMin + inset, inside.yMin + inset, inside.width - 2 * inset, inside.height - 2 * inset);
        if (outer.width < 2 * t + 4 || outer.height < 2 * t + 4) return;
        var cells = new List<Vector2Int>();
        for (int x = outer.xMin; x < outer.xMax; x++)
            for (int y = outer.yMin; y < outer.yMax; y++)
                if (x < outer.xMin + t || x >= outer.xMax - t || y < outer.yMin + t || y >= outer.yMax - t)
                {
                    walls[x, y] = true;
                    cells.Add(new Vector2Int(x, y));
                }
        // Openings: gaps spread over the four sides.
        int gaps = Mathf.Max(1, Pick(l.gaps, rng));
        int first = rng.Next(4);
        for (int g = 0; g < gaps; g++)
        {
            int side = (first + g) % 4; // 0 bottom, 1 top, 2 left, 3 right
            bool horizontal = side < 2;
            int length = horizontal ? outer.width : outer.height;
            int at = rng.Next(t + 1, Mathf.Max(t + 2, length - t - GapWidth));
            for (int k = 0; k < GapWidth; k++)
                for (int d = 0; d < t; d++)
                {
                    int x = horizontal ? outer.xMin + at + k : side == 2 ? outer.xMin + d : outer.xMax - 1 - d;
                    int y = horizontal ? (side == 0 ? outer.yMin + d : outer.yMax - 1 - d) : outer.yMin + at + k;
                    walls[x, y] = false;
                }
        }
    }

    static void Bars(bool[,] walls, RectInt inside, RoomLayout l, System.Random rng)
    {
        bool vertical = rng.Next(2) == 0;
        int step = Mathf.Max(4, Pick(l.spacing, rng));
        int length = vertical ? inside.height : inside.width, across = vertical ? inside.width : inside.height;
        for (int p = step; p < across - 2; p += step)
        {
            var line = new bool[length];
            for (int i = 2; i < length - 2; i++) line[i] = true; // ends left open along the outer walls
            int openings = 1 + rng.Next(2);
            for (int o = 0; o < openings; o++)
            {
                int at = rng.Next(2, Mathf.Max(3, length - 2 - GapWidth));
                for (int k = 0; k < GapWidth && at + k < length; k++) line[at + k] = false;
            }
            for (int i = 0; i < length; i++)
                if (line[i])
                {
                    if (vertical) walls[inside.xMin + p, inside.yMin + i] = true;
                    else walls[inside.xMin + i, inside.yMin + p] = true;
                }
        }
    }

    // Recursive backtracker on a coarse grid: cells of "corridor" tiles, walls of one tile between them.
    static void Maze(bool[,] walls, RectInt inside, Vector2Int spawn, int corridor, System.Random rng)
    {
        corridor = Mathf.Max(2, corridor);
        int step = corridor + 1;
        int cols = (inside.width - 1) / step, rows = (inside.height - 1) / step;
        if (cols < 2 || rows < 2) return;
        int x0 = inside.xMin + (inside.width - (cols * step + 1)) / 2, y0 = inside.yMin + (inside.height - (rows * step + 1)) / 2;
        // Everything is wall, then the cells and the passages between them are carved.
        Fill(walls, new RectInt(x0, y0, cols * step + 1, rows * step + 1));
        var seen = new bool[cols, rows];
        var stack = new Stack<Vector2Int>();
        var start = new Vector2Int(Mathf.Clamp((spawn.x - x0) / step, 0, cols - 1), Mathf.Clamp((spawn.y - y0) / step, 0, rows - 1));
        seen[start.x, start.y] = true;
        stack.Push(start);
        Carve(walls, x0, y0, step, corridor, start);
        var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (stack.Count > 0)
        {
            var c = stack.Peek();
            var options = new List<Vector2Int>();
            foreach (var d in dirs)
            {
                var n = c + d;
                if (n.x >= 0 && n.y >= 0 && n.x < cols && n.y < rows && !seen[n.x, n.y]) options.Add(n);
            }
            if (options.Count == 0) { stack.Pop(); continue; }
            var next = options[rng.Next(options.Count)];
            seen[next.x, next.y] = true;
            Carve(walls, x0, y0, step, corridor, next);
            // The wall between the two cells.
            var a = new Vector2Int(x0 + 1 + c.x * step, y0 + 1 + c.y * step);
            var b = new Vector2Int(x0 + 1 + next.x * step, y0 + 1 + next.y * step);
            var lo = Vector2Int.Min(a, b);
            var hi = Vector2Int.Max(a, b) + new Vector2Int(corridor, corridor);
            for (int x = lo.x; x < hi.x; x++)
                for (int y = lo.y; y < hi.y; y++)
                    walls[x, y] = false;
            stack.Push(next);
        }
        // A few extra openings, so that it is not one single winding path (loops to get round an opponent).
        for (int k = 0; k < cols * rows / 4; k++)
        {
            int cx = rng.Next(cols), cy = rng.Next(rows);
            bool right = rng.Next(2) == 0;
            if (right ? cx + 1 >= cols : cy + 1 >= rows) continue;
            int bx = x0 + 1 + cx * step, by = y0 + 1 + cy * step;
            for (int i = 0; i < corridor; i++)
                if (right) walls[bx + corridor, by + i] = false;
                else walls[bx + i, by + corridor] = false;
        }
    }

    static void Carve(bool[,] walls, int x0, int y0, int step, int corridor, Vector2Int cell)
    {
        for (int x = 0; x < corridor; x++)
            for (int y = 0; y < corridor; y++)
                walls[x0 + 1 + cell.x * step + x, y0 + 1 + cell.y * step + y] = false;
    }

    // Floor tiles that cannot be reached from the spawn point become walls (no unreachable pocket).
    static void SealUnreachable(bool[,] open, RectInt inside, Vector2Int spawn)
    {
        if (!open[spawn.x, spawn.y]) return;
        var reached = new bool[open.GetLength(0), open.GetLength(1)];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(spawn);
        reached[spawn.x, spawn.y] = true;
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = c + d;
                if (!inside.Contains(n) || reached[n.x, n.y] || !open[n.x, n.y]) continue;
                reached[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        for (int x = inside.xMin; x < inside.xMax; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                if (open[x, y] && !reached[x, y]) open[x, y] = false;
    }
}
