using System.Collections.Generic;
using UnityEngine;

/// Moves used on obstacles on purpose (GDD, AI lot C), for the allies:
/// - Repair: cut off from the leader by water, lava, void or a burnt bridge, an ally looks for the way across that
///   needs the fewest changes it can make with its own moves (Ice freezes water, Grass grows lilies or a vine
///   bridge, Water or Ice cools lava), walks to where its move reaches the first tile to change, and uses it.
/// - Cut: an opponent fighting at close range stands on (or is about to cross) a walkway its move would undo
///   (Fire burns a bridge, melts ice, burns lilies): the ally undoes it, the opponent falls or is cut off. Never when
///   a team-mate stands on what would give way.
/// Interactions come from obstacles.json (Obstacles.TurnedBy / Result).
public static class ObstacleTactics
{
    const int ChangeCost = 4; // a tile to change counts as this many tiles of walking
    const float CutWatch = 8f; // tiles: opponents this close are looked at
    const float CutAhead = 3f; // tiles ahead of an opponent checked for a walkway to undo
    const float StandReached = 0.35f;
    const float RangeSlack = 0.5f; // a straight shot without "path" lands where its range ends: stands about that far

    public struct Plan
    {
        public int slot;
        public Vector2 stand; // where to use the move from
        public Vector2 aim;
        public float distance;
        public bool ready; // at the stand, move ready: use it now
    }

    static readonly Vector2Int[] Steps = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };

    // ------------------------------------------------------------------------------------------------- repair

    /// A way to reach "goal" by changing obstacles with self's moves, when walking cannot.
    public static bool Repair(Combatant self, Vector2 goal, out Plan plan)
    {
        plan = default;
        var grid = DungeonGenerator.Current;
        if (grid == null || Obstacles.Current == null) return false;
        int size = grid.Size;
        var start = grid.CellOf(self.transform.position);
        var end = grid.CellOf(goal);

        // Dijkstra on 4-neighbours: walkable tiles cost 1, tiles one of self's moves would make walkable ChangeCost.
        // Buffers shared by every search (a level is a large map), "stamp" marks what belongs to this one; at most
        // MaxSearched tiles are looked at.
        Prepare(size * size);
        int startNode = start.y * size + start.x, endNode = end.y * size + end.x;
        Touch(startNode, 0, -1, -1);
        int searched = 0;
        while (open.Count > 0 && searched < MaxSearched)
        {
            var (at, node) = PopMin();
            if (at > cost[node]) continue; // a later, cheaper entry was already taken
            searched++;
            if (node == endNode) break;
            int x = node % size, y = node / size;
            foreach (var step in Steps)
            {
                int nx = x + step.x, ny = y + step.y;
                if (!grid.Walkable(nx, ny)) continue;
                int next = ny * size + nx, slot = -1, add = 1;
                if (Obstacles.Blocks(nx, ny, self))
                {
                    slot = ChangingSlot(self, Obstacles.At(nx, ny), turnsWalkable: true);
                    if (slot < 0) continue;
                    add = ChangeCost;
                }
                else if (Navigator.Dangerous(grid, nx, ny, self)) add = 8;
                if (cost[node] + add >= CostOf(next)) continue;
                Touch(next, cost[node] + add, node, slot);
            }
        }
        if (CostOf(endNode) == int.MaxValue) return false;

        // The first tile to change on the way, and the walkable tile before it.
        var way = new List<int>();
        for (int node = endNode; node >= 0; node = parent[node]) way.Add(node);
        way.Reverse();
        int first = way.FindIndex(n => changeSlot[n] >= 0);
        if (first <= 0) return false; // nothing to change: walking does it
        int target = way[first], before = way[first - 1];
        // Aimed across: toward the first walkable tile after the run of tiles to change.
        int after = first;
        while (after < way.Count - 1 && changeSlot[way[after]] >= 0) after++;
        Vector2 cell = grid.CentreOf(target % size, target / size);
        Vector2 across = grid.CentreOf(way[after] % size, way[after] / size) - cell;
        Vector2 bank = grid.CentreOf(before % size, before / size);
        var move = self.Moves[changeSlot[target]];
        var it = Obstacles.TurnedBy(move.type, Obstacles.At(target % size, target / size));

        Vector2 stand = bank;
        // A straight shot only changes what it crosses when the interaction runs along its path; otherwise it acts
        // where it lands, at the end of its range: stand about that far away, on walkable ground.
        if (move.family == "projectile" && move.path != "arc" && !it.path)
        {
            if (!FindStand(self, cell, move.range, size, out stand)) return false;
        }
        plan.slot = changeSlot[target];
        plan.stand = stand;
        plan.aim = move.family == "projectile" && it.path ? (across.sqrMagnitude > 0.01f ? across : cell - stand) : cell - stand;
        if (plan.aim == Vector2.zero) plan.aim = cell - bank;
        plan.distance = Vector2.Distance(stand, cell);
        plan.ready = Vector2.Distance(self.transform.position, stand) <= StandReached && self.CooldownLeft(plan.slot) <= 0f;
        return true;
    }

    const int MaxSearched = 30000; // tiles a repair search looks at

    static int[] cost, parent, changeSlot, stamp;
    static int search;
    static readonly List<int> touched = new(); // tiles reached by the current search
    static readonly List<(int cost, int node)> open = new(); // binary heap on cost

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        cost = parent = changeSlot = stamp = null;
        search = 0;
    }

    static void Prepare(int cells)
    {
        if (cost == null || cost.Length != cells)
        {
            cost = new int[cells];
            parent = new int[cells];
            changeSlot = new int[cells];
            stamp = new int[cells];
        }
        search++;
        touched.Clear();
        open.Clear();
    }

    static int CostOf(int node) => stamp[node] == search ? cost[node] : int.MaxValue;

    static void Touch(int node, int value, int from, int slot)
    {
        if (stamp[node] != search) touched.Add(node);
        stamp[node] = search;
        cost[node] = value;
        parent[node] = from;
        changeSlot[node] = slot;
        open.Add((value, node));
        for (int i = open.Count - 1; i > 0;)
        {
            int up = (i - 1) / 2;
            if (open[up].cost <= open[i].cost) break;
            (open[up], open[i]) = (open[i], open[up]);
            i = up;
        }
    }

    static (int, int) PopMin()
    {
        var top = open[0];
        int last = open.Count - 1;
        open[0] = open[last];
        open.RemoveAt(last);
        for (int i = 0; ;)
        {
            int l = 2 * i + 1, r = l + 1, best = i;
            if (l < open.Count && open[l].cost < open[best].cost) best = l;
            if (r < open.Count && open[r].cost < open[best].cost) best = r;
            if (best == i) break;
            (open[best], open[i]) = (open[i], open[best]);
            i = best;
        }
        return top;
    }

    // A walkable tile already reached by the search, "range" (± RangeSlack) from the cell, nearest to self.
    static bool FindStand(Combatant self, Vector2 cell, float range, int size, out Vector2 stand)
    {
        var grid = DungeonGenerator.Current;
        stand = Vector2.zero;
        int bestCost = int.MaxValue;
        foreach (int node in touched)
        {
            if (cost[node] >= bestCost) continue;
            int x = node % size, y = node / size;
            if (Obstacles.Blocks(x, y, self)) continue;
            Vector2 at = grid.CentreOf(x, y);
            if (Mathf.Abs(Vector2.Distance(at, cell) - range) > RangeSlack) continue;
            bestCost = cost[node];
            stand = at;
        }
        return bestCost < int.MaxValue;
    }

    // Slot of a move that changes this obstacle: into something self walks on (repair), or into something that
    // stops "foe" (cut). -1 when none.
    static int ChangingSlot(Combatant self, ObstacleDef on, bool turnsWalkable, Combatant foe = null)
    {
        if (on == null) return -1;
        for (int slot = 0; slot < Combatant.MoveSlots; slot++)
        {
            var move = self.Moves[slot];
            if (move == null || move.family == "heal" || move.family == "modifier" || move.family == "climate") continue;
            var it = Obstacles.TurnedBy(move.type, on);
            if (it == null) continue;
            var result = Obstacles.Result(it);
            bool fits = turnsWalkable ? !Obstacles.WouldBlock(result, self) : Obstacles.WouldBlock(result, foe);
            if (fits) return slot;
        }
        return -1;
    }

    // ------------------------------------------------------------------------------------------------- cut

    /// A walkway to undo under (or just ahead of) an opponent fighting at close range, with a move ready now.
    public static bool Cut(Combatant self, out Plan plan, out Combatant foe)
    {
        plan = default;
        foe = null;
        var grid = DungeonGenerator.Current;
        if (grid == null || Obstacles.Current == null || Obstacles.Current.Count <= 0) return false;
        Vector2 me = self.transform.position;
        Team opponents = self.team == Team.Player ? Team.Enemy : Team.Player;
        foreach (var c in Combatant.All)
        {
            if (c == null || c.team != opponents || !c.IsAlive || c.Role != "melee") continue;
            Vector2 at = c.transform.position;
            float distance = Vector2.Distance(me, at);
            if (distance > CutWatch) continue;
            // Its own tile first, then the tiles on its way to self.
            Vector2 toMe = distance > 0.01f ? (me - at) / distance : Vector2.zero;
            for (float d = 0f; d <= Mathf.Min(CutAhead, distance - 1f); d += 0.5f)
            {
                Vector2 point = at + toMe * d;
                var cell = grid.CellOf(point);
                var on = Obstacles.At(cell.x, cell.y);
                if (on == null || Obstacles.WouldBlock(on, c)) continue; // nothing it walks on there
                int slot = ChangingSlot(self, on, turnsWalkable: false, foe: c);
                if (slot < 0 || self.CooldownLeft(slot) > 0f) continue;
                var move = self.Moves[slot];
                var it = Obstacles.TurnedBy(move.type, on);
                if (MateOnIt(self, cell, it, on)) continue;
                if (!Reaches(self, move, point, onFoe: d == 0f, out Vector2 aim, out float reach)) continue;
                plan = new Plan { slot = slot, stand = me, aim = aim, distance = reach, ready = true };
                foe = c;
                return true;
            }
        }
        return false;
    }

    // A team-mate stands on what would give way (the whole connected walkway when the change spreads).
    static bool MateOnIt(Combatant self, Vector2Int cell, ObstacleInteraction it, ObstacleDef on)
    {
        var grid = DungeonGenerator.Current;
        var tiles = it.spread ? Obstacles.FloodFrom(cell.x, cell.y) : null;
        Vector2 centre = grid.CentreOf(cell.x, cell.y);
        foreach (var mate in Combatant.All)
        {
            if (mate == null || mate.team != self.team || !mate.IsAlive) continue;
            var at = grid.CellOf(mate.transform.position);
            if (tiles != null ? tiles.Contains((at.x, at.y)) : Vector2.Distance(mate.transform.position, centre) <= it.radius + 0.6f)
                return true;
        }
        return false;
    }

    // Whether the move, used from where self stands, acts on that point; aim and distance to use it with.
    static bool Reaches(Combatant self, MoveDef move, Vector2 point, bool onFoe, out Vector2 aim, out float distance)
    {
        Vector2 me = self.transform.position;
        aim = point - me;
        distance = aim.magnitude;
        if (aim == Vector2.zero) aim = Vector2.right;
        var grid = DungeonGenerator.Current;
        bool sight = grid.LineClear(me, point, 0.2f);
        switch (move.family)
        {
            case "melee": return distance <= move.Reach && sight;
            case "zone": return distance <= move.range && sight;
            case "burst": return distance <= move.radius * 0.9f;
            case "projectile":
                // A shot stops on the opponent it hits (and acts there), an arc lands where aimed; otherwise it acts
                // only where its range ends.
                if (!sight) return false;
                if (onFoe || move.path == "arc") return distance <= move.range * 0.95f;
                return Mathf.Abs(distance - move.range) <= RangeSlack;
            default: return false;
        }
    }

    public static bool AtStand(Combatant self, Plan plan) => Vector2.Distance(self.transform.position, plan.stand) <= StandReached;
}
