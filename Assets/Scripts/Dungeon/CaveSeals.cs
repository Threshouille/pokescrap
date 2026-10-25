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
        public string key, name, kind = "element", hint, door = "seal_door"; // door: its sheet (Resources/Effects)
        public string[] biomes = new string[0]; // sub-biome or theme keys; none: anywhere
        public float weight = 1f;
        public int[] types = new int[0], pokemon = new int[0];
        public string[] weathers = new string[0];
        public bool night;
        public int stages = 1, count;
        public float seconds;
    }

    public float chance = 0.5f, minDistance = 20f, secretDistance = 25f, biomeWeight = 3f;
    public int width = 7, depth = 4, clearance = 3, front = 4, mechanismGap = 2, tries = 4000;
    public string mechanism = "cave_mechanism";
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
    public Vector2Int sealedCave = new(-1, -1); // the sealed cave's stairs (inside, at the back), -1 none
    public Vector2Int sealedCaveFront = new(-1, -1); // the ground in front of its door
    public Vector2Int caveMechanism = new(-1, -1); // the mechanism that opens it, in front of the door (the middle one)
    public readonly List<Vector2Int> caveParts = new(); // its parts in a row (braziers, plates, shells; else the mechanism)
    public float caveProgress; // seconds spent near it ('time' seals)
    public RectInt sealedCaveRect; // its tiles: the plateau and its face
    public readonly List<int> caveDoor = new(); // the face tiles that open (3 middle columns)
    public bool caveOpen;
    public string caveSeal; // its seal (cave_seals.json key)

    /// A sealed cave (LayRelief), drawn with a chance (chanceOverride when not negative): a plateau of one level,
    /// width × depth tiles (a rock rim around a hollow under a roof), its face below it, on flat open ground with
    /// clearance tiles from what the relief keeps away from (dist) and `front` rows of open ground before the face;
    /// far enough from the landing; its seal fits the sub-biome in front (forcedSeal: that one, for tests). Raises the plateau; returns its tiles (plateau and face), or null.
    bool[] ReserveCave(System.Random rng, float chanceOverride, int[] dist, int faceRows, string forcedSeal = null)
    {
        var d = CaveSeals.Data;
        float chance = chanceOverride >= 0f ? chanceOverride : d.chance;
        if (rng.NextDouble() >= chance) return null;
        int w = Mathf.Max(3, d.width), h = Mathf.Max(2, d.depth), c = Mathf.Max(0, d.clearance);
        int below = faceRows + Mathf.Max(d.front, d.mechanismGap + 2);
        for (int tries = 0; tries < d.tries; tries++)
        {
            int x0 = rng.Next(size), y0 = rng.Next(size);
            var door = new Vector2Int(x0 + w / 2, y0 - faceRows);
            if (Vector2.Distance(door, start) < d.minDistance) continue;
            bool clear = true;
            for (int y = y0 - below - c; y < y0 + h + c && clear; y++)
                for (int x = x0 - c; x < x0 + w + c && clear; x++)
                {
                    if (!InMap(x, y)) { clear = false; break; }
                    int i = Index(x, y);
                    bool core = x >= x0 && x < x0 + w && y >= y0 - below && y < y0 + h;
                    clear = region[i] == 0 && height[i] == 0 && !wall[i] && dist[i] != int.MaxValue && dist[i] >= (core ? c : 1);
                }
            if (!clear) continue;
            var mask = new bool[size * size];
            sealedCaveRect = new RectInt(x0, y0 - faceRows, w, h + faceRows);
            foreach (var p in sealedCaveRect.allPositionsWithin)
            {
                int i = Index(p.x, p.y);
                mask[i] = true;
                if (p.y >= y0) height[i] = 1;
            }
            sealedCave = new Vector2Int(x0 + w / 2, y0 + h - 2);
            sealedCaveFront = new Vector2Int(x0 + w / 2, y0 - faceRows - 1);
            caveMechanism = new Vector2Int(x0 + w / 2, y0 - faceRows - 1 - d.mechanismGap);
            caveDoor.Clear();
            for (int x = door.x - 1; x <= door.x + 1; x++)
                for (int y = y0 - faceRows; y < y0; y++) caveDoor.Add(Index(x, y));
            caveOpen = false;
            caveProgress = 0f;
            int f = Index(sealedCaveFront.x, sealedCaveFront.y);
            var seal = CaveSeals.Get(forcedSeal) ?? CaveSeals.Pick(biome[f]?.key, themes[0]?.key, rng);
            caveSeal = seal?.key;
            // Its parts in a row across the door (a few, apart; spread more for the braziers), solid but the plates.
            caveParts.Clear();
            bool many = seal != null && seal.kind is "braziers" or "plates" or "sequence";
            int count = many ? Mathf.Clamp(seal.count, 1, w / 2 + 1) : 1, spacing = seal?.kind == "braziers" ? 3 : 2;
            while (count > 1 && (count - 1) * spacing > w - 1) spacing--;
            for (int k = 0; k < count; k++)
            {
                var part = caveMechanism + Vector2Int.right * Mathf.RoundToInt((k - (count - 1) / 2f) * spacing);
                caveParts.Add(part);
                if (seal?.kind != "plates") obstacle[Index(part.x, part.y)] = d.mechanism;
            }
            return mask;
        }
        return null;
    }

    /// The cave's plateau hollowed (after the cliffs are laid): its rim and floor under a roof, with the top row of its
    /// face (the roof thus comes down to the rock); its floor of the cave's décor, darker.
    void HollowSealedCave(Cave c, bool[] mask, int[] face)
    {
        int n = size * size;
        roof ??= new bool[n];
        roofDecor = c.roofDecor;
        foreach (var p in sealedCaveRect.allPositionsWithin)
        {
            int i = Index(p.x, p.y);
            if (face[i] > 1 || caveDoor.Contains(i)) continue; // the door stays in sight
            roof[i] = true;
            if (face[i] == 1 || wall[i]) continue;
            decor[i] = c.decor;
            shadeOverride[i] = c.shade;
        }
    }

    /// The door drawn as the cave's mouth (dark, cut in the face) from the start, still a wall: the seal's sheet
    /// (CaveSeal) shuts it, and the dark mouth shows where it does not cover it all.
    void MarkCaveDoor(Cave c)
    {
        int left = int.MaxValue, right = int.MinValue;
        foreach (int i in caveDoor) { left = Mathf.Min(left, i % size); right = Mathf.Max(right, i % size); }
        foreach (int i in caveDoor)
        {
            cut[i] = 2;
            cutPart[i] = (sbyte)(i % size == left ? -1 : i % size == right ? 1 : 0);
            decor[i] = c.decor;
            shadeOverride[i] = c.mouthShade;
        }
    }

    /// Opens the door: its tiles become walkable. Returns them, to be drawn again.
    public List<int> OpenCave()
    {
        var changed = new List<int>();
        if (caveOpen || caveDoor.Count == 0) return changed;
        caveOpen = true;
        foreach (int i in caveDoor)
        {
            wall[i] = false;
            changed.Add(i);
        }
        return changed;
    }
}
