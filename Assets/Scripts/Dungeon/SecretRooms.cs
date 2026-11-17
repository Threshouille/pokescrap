using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Secret stairs settings (Resources/Data/secret_rooms.json; GDD, "Escaliers secrets").
[Serializable]
public class SecretRooms
{
    [Serializable]
    public class Spot
    {
        public string key;
        public float weight = 1f;
    }

    [Serializable]
    public class Room
    {
        public string type = "rest", theme = "", variant = "";
    }

    public float chance = 0.6f, minDistance = 15f;
    // lava_island: a pocket of land islandRadius tiles across, ringed by moatWidth tiles of the quarter's liquid (lava
    // on the volcano), on open ground; the stairs show once the team stands within islandReveal tiles of them.
    public float islandRadius = 2.2f, moatWidth = 2f, islandReveal = 2.5f;
    public Spot[] spots = new Spot[0];
    public string rock = "secret_rock";
    public Room room = new();

    static SecretRooms data;

    public static SecretRooms Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/secret_rooms");
            data = asset != null ? JsonUtility.FromJson<SecretRooms>(asset.text) : new SecretRooms();
            return data;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => data = null;
}

public partial class IslandPlan
{
    /// Secret stairs, drawn with a chance (chanceOverride when not negative): in the middle of a forest's clearing,
    /// under a cracked rock on open ground, or on a pocket of land ringed by lava (the volcano), far enough from the
    /// landing; spotsOverride: the hideouts a quarter profile allows, by weight (secret_rooms.json's otherwise). Sets
    /// "secret" and "secretSpot".
    public void PlaceSecret(System.Random rng, float chanceOverride = -1f, SecretRooms.Spot[] spotsOverride = null)
    {
        var s = SecretRooms.Data;
        var spots = spotsOverride != null && spotsOverride.Length > 0 ? spotsOverride : s.spots;
        float chance = chanceOverride >= 0f ? chanceOverride : s.chance;
        if (rng.NextDouble() >= chance || spots.Length == 0) return;
        float total = 0f;
        foreach (var spot in spots) total += Mathf.Max(0f, spot.weight);
        float pick = (float)rng.NextDouble() * total;
        string key = spots[^1].key;
        foreach (var spot in spots)
        {
            pick -= Mathf.Max(0f, spot.weight);
            if (pick < 0f) { key = spot.key; break; }
        }
        if (key == "clearing" && clearings.Count > 0)
        {
            var c = Vector2Int.FloorToInt(clearings[rng.Next(clearings.Count)]);
            if (Walkable(Index(c.x, c.y)) && !NearCave(c))
            {
                secret = c;
                secretSpot = "clearing";
                return;
            }
        }
        if (key == "lava_island" && PlaceLavaIsland(rng, s)) return;
        if (key == "nook" && PlaceNook(rng, s)) return;
        // A cracked rock on open ground: nothing around it (it never closes a way), off roads, cliffs and caves.
        for (int tries = 0; tries < 2000; tries++)
        {
            int x = rng.Next(size), y = rng.Next(size);
            if (Vector2.Distance(new Vector2(x, y), start) < s.minDistance || NearCave(new Vector2Int(x, y))) continue;
            bool clear = true;
            for (int dy = -1; dy <= 1 && clear; dy++)
                for (int dx = -1; dx <= 1 && clear; dx++)
                    clear = FreeGround(x + dx, y + dy);
            if (!clear) continue;
            obstacle[Index(x, y)] = s.rock;
            secret = new Vector2Int(x, y);
            secretSpot = "rock";
            return;
        }
    }

    /// Whether a ring of radius "outer" round that tile lies on open ground of one level (a moat there closes no way).
    public bool CanRing(Vector2Int c, float outer)
    {
        int reach = Mathf.CeilToInt(outer) + 1;
        if (!InMap(c.x, c.y)) return false;
        int level = height != null ? height[Index(c.x, c.y)] : 0;
        for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
                if (!FreeGround(c.x + dx, c.y + dy) || (height != null && height[Index(c.x + dx, c.y + dy)] != level)) return false;
        return true;
    }

    /// A moat of the quarter's liquid (lava on the volcano) between radius "inner" and "outer" round that tile.
    public void Ring(Vector2Int c, float inner, float outer)
    {
        string liquid = themes[0] != null && !string.IsNullOrEmpty(themes[0].riverKind) ? themes[0].riverKind : "lava";
        int reach = Mathf.CeilToInt(outer) + 1;
        for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > inner && d <= outer && InMap(c.x + dx, c.y + dy)) obstacle[Index(c.x + dx, c.y + dy)] = liquid;
            }
    }

    // A pocket of land ringed by lava on open ground of one level (the moat closes no way: all of it was free).
    bool PlaceLavaIsland(System.Random rng, SecretRooms s)
    {
        float outer = s.islandRadius + s.moatWidth;
        for (int tries = 0; tries < 3000; tries++)
        {
            int x = rng.Next(size), y = rng.Next(size);
            var c = new Vector2Int(x, y);
            if (Vector2.Distance(c, start) < s.minDistance + outer || NearCave(c) || !CanRing(c, outer)) continue;
            Ring(c, s.islandRadius, outer);
            secret = c;
            secretSpot = "lava_island";
            return true;
        }
        return false;
    }

    // A nook: open ground reached on foot, on the highest level there is, as far from the landing as it gets (the top of
    // a plateau, the end of a path), never in front of a sealed cave.
    bool PlaceNook(System.Random rng, SecretRooms s)
    {
        var reached = Reach(Index(start.x, start.y));
        var spots = new List<Vector2Int>();
        for (int i = 0; i < reached.Length; i++)
        {
            var c = new Vector2Int(i % size, i / size);
            if (!reached[i] || !FreeGround(c.x, c.y) || NearCave(c) || Vector2.Distance(c, start) < s.minDistance) continue;
            if (summitDungeon.x >= 0 && Vector2.Distance(c, summitDungeon) < 6f) continue; // the summit dungeon's entrance
            spots.Add(c);
        }
        if (spots.Count == 0) return false;
        int Height(Vector2Int c) => height != null ? height[Index(c.x, c.y)] : 0;
        int top = spots.Max(Height);
        var high = spots.Where(c => Height(c) == top).OrderByDescending(c => Vector2.Distance(c, start)).ToList();
        secret = high[rng.Next(Mathf.Max(1, high.Count / 5))];
        secretSpot = "nook";
        return true;
    }

    bool NearCave(Vector2Int p) => sealedCave.x >= 0 && Vector2.Distance(p, sealedCave) < CaveSeals.Data.secretDistance;

    /// Open ground of a quarter: no wall, obstacle, road, cliff or cave.
    bool FreeGround(int x, int y)
    {
        if (!InMap(x, y)) return false;
        int i = Index(x, y);
        return region[i] >= 0 && region[i] < 4 && !wall[i] && obstacle[i] == null && (decor == null || decor[i] == 0)
            && (cut == null || cut[i] == 0) && (roof == null || !roof[i]) && (cliffStyle == null || cliffStyle[i] == 0);
    }
}
