using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// The consumables of a run (Assets/Resources/Data/bag_items.json; GDD, "Objets de partie"): berries, seeds and orbs
/// picked up during a run into the team's shared bag (Bag), lost at its end.
public static class BagItems
{
    [Serializable]
    public class Item
    {
        public string key, name, letter, color, effect, status, stat, icon;
        public float value, radius, duration, weight = 1f;
        public float bossValue = -1f, bossSlow; // a boss: its share of a Blast Seed; the Petrify Orb slows it instead

        public Color Colour => ColorUtility.TryParseHtmlString(color, out var c) ? c : Color.white;
    }

    [Serializable] public class RankChance { public string rank; public float chance; }
    [Serializable] public class Drops { public RankChance[] byRank = new RankChance[0]; public int chest = 1; }
    [Serializable] public class Start { public string key; public int count = 1; }
    [Serializable] public class Ai { public float healBelow = 0.35f, cooldown = 3f; }

    [Serializable]
    class File
    {
        public int slots = 4, stack = 9;
        public Item[] items = new Item[0];
        public Drops drops = new();
        public Start[] sandboxStart = new Start[0];
        public Ai ai = new();
    }

    static File file;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => file = null;

    static File Data
    {
        get
        {
            if (file != null) return file;
            var asset = Resources.Load<TextAsset>("Data/bag_items");
            file = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return file;
        }
    }

    public static int Slots => Data.slots;
    public static int Stack => Data.stack;
    public static IReadOnlyList<Item> All => Data.items;
    public static IReadOnlyList<Start> SandboxStart => Data.sandboxStart;
    public static Ai AiSettings => Data.ai;
    public static int ChestCount => Data.drops.chest;

    public static Item Get(string key) => Array.Find(Data.items, i => i.key == key);

    /// Chance that an enemy of that rank ("elite" for an elite) drops a consumable.
    public static float DropChance(string rank) => Array.Find(Data.drops.byRank, r => r.rank == rank)?.chance ?? 0f;

    /// A consumable drawn by weight.
    public static Item Roll(System.Random rng)
    {
        float total = Data.items.Sum(i => i.weight);
        double roll = rng.NextDouble() * total;
        foreach (var i in Data.items)
            if ((roll -= i.weight) < 0) return i;
        return Data.items.Length > 0 ? Data.items[^1] : null;
    }
}
