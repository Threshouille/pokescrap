using System.Collections.Generic;
using UnityEngine;

/// Per-agent steering on the arena grid: walks straight when the way is free, otherwise follows an A* path
/// around walls. Hostile zones (gas clouds...) and harmful terrain (embers...) are expensive to cross, so the path
/// goes around them when it can. With "avoid", dangerous tiles are not crossed at all when a safe way exists (an
/// ally rejoining the leader); SafePath then says whether the way taken is free of danger.
public class Navigator
{
    const float RepathInterval = 0.3f; // seconds between two path searches
    const float AgentRadius = 0.3f;
    const float WaypointReached = 0.35f;
    const float HazardCost = 25f; // extra cost of a tile inside a hostile zone, in tiles of detour
    const float Diagonal = 1.41421356f;
    const int MaxExpanded = 4000; // tiles a search may look at (a level is a large map): beyond, a partial path
    /// Search limit in use (MaxExpanded; the test bench lowers it to play a large map on a small arena).
    public static int Limit = MaxExpanded;

    public static void ResetLimit() => Limit = MaxExpanded;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => ResetLimit();
    // Tiles all the searches of one frame may look at together (several AIs chasing across an island): beyond, a
    // search waits for the next frame and the AI keeps to its current path.
    const int FrameBudget = 4000;
    static int budgetFrame, spent;
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
    bool pathAvoids; // the mode the current path was searched with

    /// Whether the way taken by the last call avoids every danger (a straight line or a path around them).
    public bool SafePath { get; private set; } = true;

    /// Whether the last call found a way at all (false: water, lava or void in between, nothing to walk round by).
    public bool Reachable { get; private set; } = true;

    /// Direction to take from "from" to reach "to", or zero when already there or unreachable.
    /// avoid: never through a dangerous tile when there is another way (otherwise dangers are only expensive).
    public Vector2 Direction(Vector2 from, Vector2 to, Combatant agent, bool avoid = false)
    {
        var grid = DungeonGenerator.Current;
        if (grid == null) return (to - from).normalized;

        if (Clear(grid, from, to, agent, avoid))
        {
            path.Clear();
            SafePath = Reachable = true;
            return (to - from).normalized;
        }

        var cell = grid.CellOf(to);
        // An unreachable goal is searched again only every RepathInterval, not at every step.
        if (Time.frameCount != budgetFrame)
        {
            budgetFrame = Time.frameCount;
            spent = 0;
        }
        bool due = (path.Count == 0 && Reachable) || cell != goalCell || Time.time >= nextSearch || pathAvoids != avoid;
        if (due && spent >= FrameBudget && path.Count > 0) due = false; // over this frame's budget: next frame
        if (due)
        {
            SafePath = false;
            if (avoid) SafePath = FindPath(grid, from, to, agent, true);
            if (!SafePath) FindPath(grid, from, to, agent, false);
            pathAvoids = avoid;
            Reachable = path.Count > 0;
            goalCell = cell;
            nextSearch = Time.time + RepathInterval;
            index = 0;
        }

        // Skip waypoints already reached, and cut corners whenever the next one is directly reachable.
        while (index < path.Count - 1
            && (Vector2.Distance(from, path[index]) < WaypointReached || Clear(grid, from, path[index + 1], agent, pathAvoids && SafePath)))
            index++;
        if (index >= path.Count) return Vector2.zero;
        Vector2 step = path[index] - from;
        return step.sqrMagnitude < 0.0001f ? Vector2.zero : step.normalized;
    }

    /// Straight line free of walls, and of hostile zones and harmful terrain unless the agent already stands on them
    /// (then any way out is fine; not with "avoid", where the way must stay out of danger: the line may cross more).
    static bool Clear(DungeonGenerator grid, Vector2 from, Vector2 to, Combatant agent, bool avoid = false)
    {
        var team = agent.team;
        if (!grid.LineClear(from, to, AgentRadius)) return false;
        if (!avoid && (Zone.Inside(from, team, 0f) || Terrain.Hazard(from, agent))) return true;
        float length = Vector2.Distance(from, to);
        Vector2 side = length > 0f ? Vector2.Perpendicular((to - from) / length) * AgentRadius : Vector2.zero;
        for (float d = 0f; d <= length; d += 0.4f)
        {
            var point = Vector2.Lerp(from, to, length > 0f ? d / length : 0f);
            if (Zone.Inside(point, team, AgentRadius) || Terrain.Hazard(point, agent)) return false;
            // Avoiding: the edges of the body stay off harmful terrain too.
            if (avoid && (Terrain.Hazard(point + side, agent) || Terrain.Hazard(point - side, agent))) return false;
            if (Obstacles.BlocksAt(point, agent) || Pylons.Hazard(point, agent)) return false;
        }
        return !Zone.Inside(to, team, AgentRadius);
    }

    /// True when the tile would hurt the agent: hostile zone, harmful terrain, electric arc about to strike.
    public static bool Dangerous(DungeonGenerator grid, int x, int y, Combatant agent)
    {
        Vector2 centre = grid.CentreOf(x, y);
        return Zone.Inside(centre, agent.team, 0.5f) || Terrain.Hazard(centre, agent) || Pylons.Hazard(centre, agent);
    }

    static Vector2Int Snap(DungeonGenerator grid, Vector2Int cell)
    {
        if (grid.Walkable(cell.x, cell.y)) return cell;
        int level = grid.HeightOf(cell.x, cell.y);
        for (int pass = 0; pass < 2; pass++) // on its own level first (not down the cliff)
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (grid.Walkable(cell.x + dx, cell.y + dy) && (pass == 1 || grid.HeightOf(cell.x + dx, cell.y + dy) == level))
                        return new Vector2Int(cell.x + dx, cell.y + dy);
        return cell;
    }

    /// Fills "path"; false when the goal cannot be reached (with "avoid": not without crossing a danger).
    bool FindPath(DungeonGenerator grid, Vector2 from, Vector2 to, Combatant agent, bool avoid)
    {
        var team = agent.team;
        path.Clear();
        int size = grid.Size;
        // A point on a tile the grid counts as wall (a cliff's edge seen from above: walkable up to its drawn edge)
        // stands for the nearest walkable tile beside it.
        var start = Snap(grid, grid.CellOf(from));
        var goal = Snap(grid, grid.CellOf(to));
        if (!grid.Walkable(start.x, start.y) || !grid.Walkable(goal.x, goal.y)) return false;

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
        int expanded = 0, closest = startNode;
        float closestLeft = Heuristic(start, goal);
        while (heap.Count > 0 && expanded < Limit)
        {
            int node = Pop();
            if (closed[node]) continue;
            closed[node] = true;
            expanded++;
            spent++;
            if (node == goalNode) break;
            float left = priority[node] - cost[node];
            if (left < closestLeft) { closestLeft = left; closest = node; }

            int x = node % size, y = node / size;
            foreach (var step in Steps)
            {
                int nx = x + step.x, ny = y + step.y;
                if (!grid.Walkable(nx, ny) || Obstacles.Blocks(nx, ny, agent)) continue;
                // No cutting through the corner of a wall.
                if (step.x != 0 && step.y != 0 && (!grid.Walkable(nx, y) || !grid.Walkable(x, ny)
                    || Obstacles.Blocks(nx, y, agent) || Obstacles.Blocks(x, ny, agent))) continue;
                int next = ny * size + nx;
                if (stamp[next] == search && closed[next]) continue;
                if (avoid && Dangerous(grid, nx, ny, agent)) continue;
                // Nor through the corner of a dangerous tile: the body would graze it.
                if (avoid && step.x != 0 && step.y != 0 && (Dangerous(grid, nx, y, agent) || Dangerous(grid, x, ny, agent))) continue;

                float stepCost = step.x != 0 && step.y != 0 ? Diagonal : 1f;
                if (Zone.Inside(grid.CentreOf(nx, ny), team, 0.5f)) stepCost += HazardCost;
                stepCost += Terrain.NavCost(nx, ny, agent) + Pylons.NavCost(grid.CentreOf(nx, ny), agent) + Obstacles.PathCost(nx, ny, agent);
                float total = cost[node] + stepCost;
                if (stamp[next] == search && total >= cost[next]) continue;
                Visit(next, total, node, Heuristic(new Vector2Int(nx, ny), goal));
            }
        }

        bool reached = stamp[goalNode] == search && closed[goalNode];
        // Every reachable tile looked at without finding the goal: unreachable. The search stopped by its limit
        // (the goal is far, on a large map): a partial path to the tile closest to the goal, walked meanwhile.
        if (!reached && heap.Count == 0) return false;
        int end = reached ? goalNode : closest;
        for (int node = end; node != startNode && node >= 0; node = parent[node])
            path.Add(grid.CentreOf(node % size, node / size));
        path.Reverse();
        if (reached && path.Count > 0) path[path.Count - 1] = to;
        return true;
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
