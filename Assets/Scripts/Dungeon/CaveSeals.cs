using System;
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
    public int width = 5, height = 3, clearance = 3, tries = 4000;
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
    public Vector2Int sealedCave = new(-1, -1); // the sealed cave's stairs (the middle of its mouth), -1 none
    public Vector2Int sealedCaveFront = new(-1, -1); // the open ground in front of it, where the team stands
    public RectInt sealedCaveRect; // the tiles of the cave's rock (width × height, the mouth on its bottom row)
    public string caveSeal; // its seal (cave_seals.json key)

    /// A sealed cave, drawn with a chance (chanceOverride when not negative): a rock of its own (width × height tiles,
    /// its mouth in the middle of its bottom row) on flat open ground, clearance tiles from any cliff, wall, obstacle,
    /// road or other structure (small decor elements there are removed); far enough from the landing and the secret stairs. Its seal fits the sub-biome.
    public void PlaceCave(System.Random rng, float chanceOverride = -1f)
    {
        var d = CaveSeals.Data;
        float chance = chanceOverride >= 0f ? chanceOverride : d.chance;
        if (rng.NextDouble() >= chance) return;
        int w = Mathf.Max(1, d.width), h = Mathf.Max(1, d.height), c = Mathf.Max(0, d.clearance);
        for (int tries = 0; tries < d.tries; tries++)
        {
            int x0 = rng.Next(size - w), y0 = rng.Next(size - h);
            var mouth = new Vector2Int(x0 + w / 2, y0);
            if (Vector2.Distance(mouth, start) < d.minDistance
                || (secret.x >= 0 && Vector2.Distance(mouth, secret) < d.secretDistance)) continue;
            int level = height != null ? height[Index(mouth.x, mouth.y)] : 0;
            bool clear = true;
            for (int y = y0 - c; y < y0 + h + c && clear; y++)
                for (int x = x0 - c; x < x0 + w + c && clear; x++)
                    clear = FreeGround(x, y) && (height == null || height[Index(x, y)] == level)
                        && (secret.x != x || secret.y != y);
            if (!clear) continue;
            var front = mouth + Vector2Int.down;
            int i = Index(front.x, front.y);
            var seal = CaveSeals.Pick(biome[i]?.key, region[i] >= 0 && region[i] < 4 ? themes[region[i]]?.key : null, rng);
            if (seal == null) return;
            sealedCave = mouth;
            sealedCaveFront = front;
            sealedCaveRect = new RectInt(x0, y0, w, h);
            caveSeal = seal.key;
            // Flowers and the like (blocking ones are obstacles, kept away above) make room for it.
            var room = new Rect(x0 - 1, y0 - 1, w + 2, h + 2);
            decorProps.RemoveAll(p => room.Contains(p.at));
            return;
        }
    }
}
