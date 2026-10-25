using System;
using System.Collections.Generic;
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
    /// Secret stairs, drawn with a chance (chanceOverride when not negative): in the middle of a forest's clearing, or
    /// under a cracked rock on open ground, far enough from the landing. Sets "secret" and "secretSpot".
    public void PlaceSecret(System.Random rng, float chanceOverride = -1f)
    {
        var s = SecretRooms.Data;
        float chance = chanceOverride >= 0f ? chanceOverride : s.chance;
        if (rng.NextDouble() >= chance || s.spots.Length == 0) return;
        float total = 0f;
        foreach (var spot in s.spots) total += Mathf.Max(0f, spot.weight);
        float pick = (float)rng.NextDouble() * total;
        string key = s.spots[^1].key;
        foreach (var spot in s.spots)
        {
            pick -= Mathf.Max(0f, spot.weight);
            if (pick < 0f) { key = spot.key; break; }
        }
        if (key == "clearing" && clearings.Count > 0)
        {
            var c = Vector2Int.FloorToInt(clearings[rng.Next(clearings.Count)]);
            if (Walkable(Index(c.x, c.y)))
            {
                secret = c;
                secretSpot = "clearing";
                return;
            }
        }
        // A cracked rock on open ground: nothing around it (it never closes a way), off roads, cliffs and caves.
        bool Free(int x, int y)
        {
            if (!InMap(x, y)) return false;
            int i = Index(x, y);
            return region[i] == 0 && !wall[i] && obstacle[i] == null && (decor == null || decor[i] == 0)
                && (cut == null || cut[i] == 0) && (roof == null || !roof[i]) && (cliffStyle == null || cliffStyle[i] == 0);
        }
        for (int tries = 0; tries < 2000; tries++)
        {
            int x = rng.Next(size), y = rng.Next(size);
            if (Vector2.Distance(new Vector2(x, y), start) < s.minDistance) continue;
            bool clear = true;
            for (int dy = -1; dy <= 1 && clear; dy++)
                for (int dx = -1; dx <= 1 && clear; dx++)
                    clear = Free(x + dx, y + dy);
            if (!clear) continue;
            obstacle[Index(x, y)] = s.rock;
            secret = new Vector2Int(x, y);
            secretSpot = "rock";
            return;
        }
    }
}
