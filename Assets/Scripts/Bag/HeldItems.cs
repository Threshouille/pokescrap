using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Held items (Assets/Resources/Data/held_items.json; GDD, "Objets de partie"): one per Pokémon, passive, lost when
/// the run ends (Combatant.Hold). Stat changes while held; damage, drain, recoil, regeneration, a blow taken in melee
/// hurting the attacker, a last stand, obstacles and terrains it gets about. Enemies of the higher ranks hold one and
/// drop it; the controlled Pokémon takes one by walking onto it (Loot).
public static class HeldItems
{
    [Serializable]
    public class Item
    {
        public string key, name, letter, color, icon, desc;
        public float attackMul = 1f, specialAttackMul = 1f, speedMul = 1f, cooldownMul = 1f, damageMul = 1f;
        public float regenShare, drainShare, recoilShare, contactShare, bossContactShare = -1f, weight = 1f;
        public bool sash, popOnHit, regenOutOfFight, tiered;
        [NonSerialized] public int tier; // 1 to 3 for a tiered item at a tier (AtTier), 0 otherwise

        /// "Bandeau Choix II".
        public string FullName => tier > 0 ? $"{name} {TierOf(tier)?.name}" : name;
        public string[] crosses = new string[0], terrainImmune = new string[0];

        public Color Colour => ColorUtility.TryParseHtmlString(color, out var c) ? c : Color.white;
    }

    [Serializable] class RankChance { public string rank; public float chance; }
    [Serializable] class RankTiers { public string rank; public int[] tiers = { 1, 1 }; }
    [Serializable] class Enemies { public RankChance[] byRank = new RankChance[0]; public RankTiers[] tierByRank = new RankTiers[0]; }

    [Serializable]
    public class Tier
    {
        public string name, color;
        public float scale = 1f;

        public Color Colour => ColorUtility.TryParseHtmlString(color, out var c) ? c : Color.white;
    }

    [Serializable]
    class File
    {
        public Item[] items = new Item[0];
        public Enemies enemies = new();
        public float chestChance;
        public Tier[] tiers = new Tier[0];
        public int[] chestTiers = { 1, 1 };
        public int showcaseTier = 2;
    }

    static File file;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => file = null;

    static File Data
    {
        get
        {
            if (file != null) return file;
            var asset = Resources.Load<TextAsset>("Data/held_items");
            file = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return file;
        }
    }

    public static IReadOnlyList<Item> All => Data.items;
    public static float ChestChance => Data.chestChance;

    public static Item Get(string key) => Array.Find(Data.items, i => i.key == key);

    public static Tier TierOf(int tier) => tier >= 1 && tier <= Data.tiers.Length ? Data.tiers[tier - 1] : null;
    public static int MaxTier => Data.tiers.Length;
    public static int ShowcaseTier => Data.showcaseTier;

    static readonly Dictionary<(string, int), Item> atTier = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetTiers() => atTier.Clear();

    /// That item at a tier: the data's values are tier II's, the other tiers scale its bonuses (the multipliers'
    /// gain, the shares of health), not the recharge penalty. An untiered item comes back as it is.
    public static Item AtTier(Item item, int tier)
    {
        if (item == null || !item.tiered || TierOf(tier) == null) return item;
        if (atTier.TryGetValue((item.key, tier), out var cached)) return cached;
        float scale = TierOf(tier).scale / (TierOf(2)?.scale ?? 1f);
        var t = JsonUtility.FromJson<Item>(JsonUtility.ToJson(item));
        float Bonus(float mul) => 1f + (mul - 1f) * scale;
        t.attackMul = Bonus(item.attackMul);
        t.specialAttackMul = Bonus(item.specialAttackMul);
        t.speedMul = Bonus(item.speedMul);
        t.damageMul = Bonus(item.damageMul);
        t.regenShare = item.regenShare * scale;
        t.drainShare = item.drainShare * scale;
        t.contactShare = item.contactShare * scale;
        t.bossContactShare = item.bossContactShare >= 0f ? item.bossContactShare * scale : -1f;
        t.tier = tier;
        atTier[(item.key, tier)] = t;
        return t;
    }

    /// The tier an enemy of that rank's item gets ("elite" for an elite), drawn between the rank's bounds.
    public static int EnemyTier(string rank, System.Random rng)
    {
        var r = Array.Find(Data.enemies.tierByRank, x => x.rank == rank);
        var bounds = r != null ? r.tiers : new[] { 1, 1 };
        return rng.Next(bounds[0], bounds[Mathf.Min(1, bounds.Length - 1)] + 1);
    }

    public static int ChestTier(System.Random rng) => rng.Next(Data.chestTiers[0], Data.chestTiers[Mathf.Min(1, Data.chestTiers.Length - 1)] + 1);

    /// What a Pokémon holding "old" ends up with when it picks up "found": the same item gains a tier when both are
    /// of the same tier, else keeps the higher one (dropped: nothing); another item replaces it (dropped: the old one).
    public static Item Merge(Item old, Item found, out Item dropped)
    {
        dropped = null;
        if (old == null) return found;
        if (old.key != found.key) { dropped = old; return found; }
        if (!found.tiered) return old;
        int tier = old.tier == found.tier ? Mathf.Min(MaxTier, old.tier + 1) : Mathf.Max(old.tier, found.tier);
        return AtTier(Get(found.key), tier);
    }

    /// Chance that an enemy of that rank ("elite" for an elite) holds an item.
    public static float EnemyChance(string rank) => Array.Find(Data.enemies.byRank, r => r.rank == rank)?.chance ?? 0f;

    public static Item Roll(System.Random rng)
    {
        float total = Data.items.Sum(i => i.weight);
        double roll = rng.NextDouble() * total;
        foreach (var i in Data.items)
            if ((roll -= i.weight) < 0) return i;
        return Data.items.Length > 0 ? Data.items[^1] : null;
    }

    static string Pct(float share) => $"{share * 100f:0.#} %";

    /// What the item does, in a few words, from its values (so tiers and settings show as they are): "Attaque +50 %,
    /// recharges +25 %". The data's "desc" replaces it when given.
    public static string Describe(Item item)
    {
        if (item == null) return "";
        if (!string.IsNullOrEmpty(item.desc)) return item.desc;
        var parts = new List<string>();
        void Mul(float mul, string what) { if (Mathf.Abs(mul - 1f) > 0.001f) parts.Add($"{what} {(mul > 1f ? "+" : "−")}{Pct(Mathf.Abs(mul - 1f))}"); }
        Mul(item.attackMul, "Attaque");
        Mul(item.specialAttackMul, "Attaque Spé");
        Mul(item.speedMul, "vitesse");
        Mul(item.damageMul, "dégâts");
        Mul(item.cooldownMul, "recharges");
        if (item.regenShare > 0f) parts.Add($"+{Pct(item.regenShare)} des PV par seconde{(item.regenOutOfFight ? " hors combat" : "")}");
        if (item.drainShare > 0f) parts.Add($"regagne {Pct(item.drainShare)} des dégâts infligés");
        if (item.recoilShare > 0f) parts.Add($"perd {Pct(item.recoilShare)} des PV par attaque");
        if (item.contactShare > 0f) parts.Add($"l'attaquant au contact perd {Pct(item.contactShare)} de ses PV");
        if (item.sash) parts.Add("survit une fois à un coup fatal");
        var over = item.crosses.Select(k => k switch { "water" => "l'eau", "void" => "le vide", "lava" => "la lave", _ => null }).Where(n => n != null).ToList();
        if (over.Count > 0) parts.Add((item.popOnHit ? "flotte au-dessus de " : "marche sur ") + string.Join(", ", over));
        if (item.popOnHit) parts.Add("éclate au premier coup");
        if (item.terrainImmune.Length > 0) parts.Add("insensible aux terrains au sol");
        return string.Join(", ", parts);
    }

    /// Its held item lets it over that obstacle (a Balloon over water, Magma Boots on lava).
    public static bool Crosses(Combatant c, string obstacle) =>
        c != null && c.Held != null && Array.IndexOf(c.Held.crosses, obstacle) >= 0;

    /// Its held item keeps that terrain off it (a Sand Cloak).
    public static bool TerrainImmune(Combatant c, string terrain) =>
        c != null && c.Held != null && Array.IndexOf(c.Held.terrainImmune, terrain) >= 0;

    /// A blow at close range from "attacker" on "target": a Rocky Helmet hurts the attacker.
    public static void OnContact(Combatant attacker, Combatant target)
    {
        if (attacker == null || target == null || target.Held == null || target.Held.contactShare <= 0f) return;
        float share = attacker.isBoss && target.Held.bossContactShare >= 0f ? target.Held.bossContactShare : target.Held.contactShare;
        attacker.TakeFixedDamage(attacker.maxHp * share);
    }
}
