using System.Collections.Generic;
using UnityEngine;

/// Per-agent steering on the arena grid: walks straight when the way is free, otherwise follows an A* path
/// around walls. Hostile zones (gas clouds...) and harmful terrain (embers...) are expensive to cross, so the path
/// goes around them when it can.
public class Navigator
{
    const float RepathInterval = 0.3f; // seconds between two path searches
    const float AgentRadius = 0.3f;
    const float WaypointReached = 0.35f;
    const float HazardCost = 25f; // extra cost of a tile inside a hostile zone, in tiles of detour
    const float Diagonal = 1.41421356f;
    static readonly Vector2Int[] Steps =
    {
        new(1, 0), new(-1, 0), new(0, 1), new(0, -1), new(1, 1), new(1, -1), new(-1, 1), new(-1, -1),
    };

    // Search buffers shared by every agent (searches never overlap); "stamp" marks what belongs to the current one.
    static float[] cost, priority;
    static int[] parent, stamp;
    static bool[] closed;
    static int search;
    static readonly List<int> heap = new();

    readonly List<Vector2> path = new();
    int index;
    float nextSearch;
    Vector2Int goalCell;

    /// Direction to take from "from" to reach "to", or zero when already there or unreachable.
    public Vector2 Direction(Vector2 from, Vector2 to, Combatant agent)
    {
        var team = agent.team;
        var grid = DungeonGenerator.Current;
        if (grid == null) return (to - from).normalized;

        if (Clear(grid, from, to, agent))
        {
            path.Clear();
            return (to - from).normalized;
        }

        var cell = grid.CellOf(to);
        if (path.Count == 0 || cell != goalCell || Time.time >= nextSearch)
        {
            FindPath(grid, from, to, agent);
            goalCell = cell;
            nextSearch = Time.time + RepathInterval;
            index = 0;
        }

        // Skip waypoints already reached, and cut corners whenever the next one is directly reachable.
        while (index < path.Count - 1
            && (Vector2.Distance(from, path[index]) < WaypointReached || Clear(grid, from, path[index + 1], agent)))
            index++;
        if (index >= path.Count) return Vector2.zero;
        Vector2 step = path[index] - from;
        return step.sqrMagnitude < 0.0001f ? Vector2.zero : step.normalized;
    }

    /// Straight line free of walls, and of hostile zones and harmful terrain unless the agent already stands on them.
    static bool Clear(DungeonGenerator grid, Vector2 from, Vector2 to, Combatant agent)
    {
        var team = agent.team;
        if (!grid.LineClear(from, to, AgentRadius)) return false;
        if (Zone.Inside(from, team, 0f) || Terrain.Hazard(from, agent)) return true; // already in it: any way out is fine
        float length = Vector2.Distance(from, to);
        for (float d = 0f; d <= length; d += 0.4f)
        {
            var point = Vector2.Lerp(from, to, length > 0f ? d / length : 0f);
            if (Zone.Inside(point, team, AgentRadius) || Terrain.Hazard(point, agent)) return false;
        }
        return !Zone.Inside(to, team, AgentRadius);
    }

    void FindPath(DungeonGenerator grid, Vector2 from, Vector2 to, Combatant agent)
    {
        var team = agent.team;
        path.Clear();
        int size = grid.Size;
        var start = grid.CellOf(from);
        var goal = grid.CellOf(to);
        if (!grid.Walkable(start.x, start.y) || !grid.Walkable(goal.x, goal.y)) return;

        if (cost == null || cost.Length != size * size)
        {
            cost = new float[size * size];
            priority = new float[size * size];
            parent = new int[size * size];
            stamp = new int[size * size];
            closed = new bool[size * size];
        }
        search++;
        heap.Clear();

        int startNode = start.y * size + start.x, goalNode = goal.y * size + goal.x;
        Visit(startNode, 0f, -1, Heuristic(start, goal));
        while (heap.Count > 0)
        {
            int node = Pop();
            if (closed[node]) continue;
            closed[node] = true;
            if (node == goalNode) break;

            int x = node % size, y = node / size;
            foreach (var step in Steps)
            {
                int nx = x + step.x, ny = y + step.y;
                if (!grid.Walkable(nx, ny)) continue;
                // No cutting through the corner of a wall.
                if (step.x != 0 && step.y != 0 && (!grid.Walkable(nx, y) || !grid.Walkable(x, ny))) continue;
                int next = ny * size + nx;
                if (stamp[next] == search && closed[next]) continue;

                float stepCost = step.x != 0 && step.y != 0 ? Diagonal : 1f;
                if (Zone.Inside(grid.CentreOf(nx, ny), team, 0.5f)) stepCost += HazardCost;
                stepCost += Terrain.NavCost(nx, ny, agent);
                float total = cost[node] + stepCost;
                if (stamp[next] == search && total >= cost[next]) continue;
                Visit(next, total, node, Heuristic(new Vector2Int(nx, ny), goal));
            }
        }

        if (stamp[goalNode] != search || !closed[goalNode]) return; // unreachable
        for (int node = goalNode; node != startNode && node >= 0; node = parent[node])
            path.Add(grid.CentreOf(node % size, node / size));
        path.Reverse();
        if (path.Count > 0) path[path.Count - 1] = to;
    }

    static float Heuristic(Vector2Int a, Vector2Int b)
    {
        int dx = Mathf.Abs(a.x - b.x), dy = Mathf.Abs(a.y - b.y);
        return Mathf.Max(dx, dy) + (Diagonal - 1f) * Mathf.Min(dx, dy);
    }

    static void Visit(int node, float total, int from, float heuristic)
    {
        stamp[node] = search;
        closed[node] = false;
        cost[node] = total;
        parent[node] = from;
        priority[node] = total + heuristic;
        heap.Add(node);
        for (int i = heap.Count - 1; i > 0;)
        {
            int up = (i - 1) / 2;
            if (priority[heap[up]] <= priority[heap[i]]) break;
            (heap[up], heap[i]) = (heap[i], heap[up]);
            i = up;
        }
    }

    static int Pop()
    {
        int top = heap[0];
        int last = heap.Count - 1;
        heap[0] = heap[last];
        heap.RemoveAt(last);
        for (int i = 0; ;)
        {
            int left = 2 * i + 1, right = left + 1, best = i;
            if (left < heap.Count && priority[heap[left]] < priority[heap[best]]) best = left;
            if (right < heap.Count && priority[heap[right]] < priority[heap[best]]) best = right;
            if (best == i) break;
            (heap[best], heap[i]) = (heap[i], heap[best]);
            i = best;
        }
        return top;
    }
}
