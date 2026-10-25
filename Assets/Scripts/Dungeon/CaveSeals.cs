using System;
using System.Collections.Generic;
using UnityEngine;

/// Sealed caves settings (Resources/Data/cave_seals.json; GDD, "Grottes scellées").
[Serializable]
public class CaveSeals
{
    [Serializable]
    public class Seal
    {
        public string key, name, kind = "element", hint;
        public string[] biomes = new string[0]; // sub-biome or theme keys; none: anywhere
        public float weight = 1f;
        public int[] types = new int[0], pokemon = new int[0];
        public string[] weathers = new string[0];
        public bool night;
        public int stages = 1, count;
        public float seconds;
    }

    public float chance = 0.5f, minDistance = 20f, secretDistance = 25f, biomeWeight = 3f;
    public SecretRooms.Spot[] spots = new SecretRooms.Spot[0];
    public SecretRooms.Room room = new();
    public Seal[] seals = new Seal[0];

    static CaveSeals data;

    public static CaveSeals Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/cave_seals");
            data = asset != null ? JsonUtility.FromJson<CaveSeals>(asset.text) : new CaveSeals();
            return data;
        }
    }

    public static Seal Get(string key)
    {
        foreach (var s in Data.seals) if (s.key == key) return s;
        return null;
    }

    /// A seal for a cave in that sub-biome and theme: those of the biome weigh biomeWeight times more, those of no
    /// biome may come anywhere.
    public static Seal Pick(string subBiome, string theme, System.Random rng)
    {
        var d = Data;
        var weights = new float[d.seals.Length];
        float total = 0f;
        for (int k = 0; k < d.seals.Length; k++)
        {
            var s = d.seals[k];
            bool anywhere = s.biomes.Length == 0;
            bool here = Array.IndexOf(s.biomes, subBiome) >= 0 || Array.IndexOf(s.biomes, theme) >= 0;
            weights[k] = anywhere ? Mathf.Max(0f, s.weight) : here ? Mathf.Max(0f, s.weight) * d.biomeWeight : 0f;
            total += weights[k];
        }
        if (total <= 0f) return null;
        float pick = (float)rng.NextDouble() * total;
        for (int k = 0; k < weights.Length; k++)
        {
            pick -= weights[k];
            if (pick < 0f) return d.seals[k];
        }
        return null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => data = null;
}

public partial class IslandPlan
{
    public Vector2Int sealedCave = new(-1, -1); // the sealed cave's stairs (a cliff's mouth or a mound), -1 none
    public Vector2Int sealedCaveFront = new(-1, -1); // the open ground in front of it, where the team stands
    public string sealedCaveSpot; // "cliff" or "open"
    public string caveSeal; // its seal (cave_seals.json key)

    /// A sealed cave, drawn with a chance (chanceOverride when not negative): its mouth at the bottom of a cliff face
    /// (5 columns of the same face, none cut, open ground of a lower level at its foot), or a mound on open ground;
    /// far enough from the landing and the secret stairs. The seal fits the sub-biome in front of it when it can.
    public void PlaceCave(System.Random rng, float chanceOverride = -1f)
    {
        var d = CaveSeals.Data;
        float chance = chanceOverride >= 0f ? chanceOverride : d.chance;
        if (rng.NextDouble() >= chance || d.spots.Length == 0) return;
        float total = 0f;
        foreach (var spot in d.spots) total += Mathf.Max(0f, spot.weight);
        float pick = (float)rng.NextDouble() * total;
        string key = d.spots[^1].key;
        foreach (var spot in d.spots)
        {
            pick -= Mathf.Max(0f, spot.weight);
            if (pick < 0f) { key = spot.key; break; }
        }
        bool Far(int x, int y) => Vector2.Distance(new Vector2(x, y), start) >= d.minDistance
            && (secret.x < 0 || Vector2.Distance(new Vector2(x, y), secret) >= d.secretDistance);

        Vector2Int at = new(-1, -1), front = at;
        string spotKey = key;
        if (key == "cliff" && !CliffMouth(rng, Far, out at, out front)) spotKey = "open";
        if (spotKey == "open" && !OpenMound(rng, Far, out at, out front))
        {
            if (key == "cliff" || !CliffMouth(rng, Far, out at, out front)) return;
            spotKey = "cliff";
        }
        int i = Index(front.x, front.y);
        var seal = CaveSeals.Pick(biome[i]?.key, region[i] >= 0 && region[i] < 4 ? themes[region[i]]?.key : null, rng);
        if (seal == null) return;
        sealedCave = at;
        sealedCaveFront = front;
        sealedCaveSpot = spotKey;
        caveSeal = seal.key;
    }

    bool CliffMouth(System.Random rng, Func<int, int, bool> far, out Vector2Int at, out Vector2Int front)
    {
        at = front = new Vector2Int(-1, -1);
        if (faceRow == null || cut == null || height == null) return false;
        bool Bottom(int x, int y, int h)
        {
            if (!InMap(x, y)) return false;
            int i = Index(x, y);
            return region[i] >= 0 && region[i] < 4 && cliffStyle[i] != 0 && faceRow[i] > 0 && faceRow[i] >= faceLen[i]
                && cut[i] == 0 && height[i] == h && (roof == null || !roof[i]);
        }
        // Open ground of a lower level at the foot (in the face's shadow: its cliff style is the shadow's).
        bool Foot(int x, int y, int h)
        {
            if (!InMap(x, y)) return false;
            int i = Index(x, y);
            return region[i] >= 0 && region[i] < 4 && !wall[i] && obstacle[i] == null && faceRow[i] == 0 && cut[i] == 0
                && height[i] < h && (roof == null || !roof[i]);
        }
        var found = new List<Vector2Int>();
        for (int y = 1; y < size; y++)
            for (int x = 2; x < size - 2; x++)
            {
                int h = height[Index(x, y)];
                bool ok = true;
                for (int dx = -2; dx <= 2 && ok; dx++) ok = Bottom(x + dx, y, h);
                for (int dx = -1; dx <= 1 && ok; dx++) ok = Foot(x + dx, y - 1, h);
                if (ok && far(x, y)) found.Add(new Vector2Int(x, y));
            }
        if (found.Count == 0) return false;
        at = found[rng.Next(found.Count)];
        front = at + Vector2Int.down;
        return true;
    }

    bool OpenMound(System.Random rng, Func<int, int, bool> far, out Vector2Int at, out Vector2Int front)
    {
        at = front = new Vector2Int(-1, -1);
        for (int tries = 0; tries < 2000; tries++)
        {
            int x = rng.Next(size), y = rng.Next(size);
            if (!far(x, y)) continue;
            bool clear = true;
            for (int dy = -2; dy <= 1 && clear; dy++)
                for (int dx = -1; dx <= 1 && clear; dx++)
                    clear = FreeGround(x + dx, y + dy);
            if (!clear) continue;
            at = new Vector2Int(x, y);
            front = at + Vector2Int.down;
            return true;
        }
        return false;
    }
}
