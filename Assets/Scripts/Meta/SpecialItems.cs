using System;
using System.Collections.Generic;
using UnityEngine;

/// The special items (Assets/Resources/Data/items.json; GDD, "Objets spéciaux"): mega stones, Z crystals and Tera
/// shards. A Pokémon has one slot per family, filled before a run from the player's collection (SaveData), and one of
/// them active for the run: its ultimate (Ultimates) is then a mega evolution, a Z move or the Terastal phase.
/// Mega stones are unique and kept for good; Z crystals and Tera shards go up a level (to 3) with each duplicate.
public static class SpecialItems
{
    public enum Family { None, Mega, Z, Tera }

    [Serializable] public class MegaStone { public string key, name, icon; public int mega; }
    [Serializable] public class ZCrystal { public int type; public string name, move, icon; public ZHit hit = new(); }

    /// What a Z move does to each opponent it strikes besides damage (one per type): see Ultimates.StrikeZ.
    [Serializable]
    public class ZHit
    {
        public string effect, status, modStat, terrain, teamModStat;
        public float bonus = 1f, statusDuration, modValue = 1f, modDuration, stun, knockback, drain;
        public float terrainRadius = 1f, terrainDuration = 8f, teamModValue = 1f, teamModDuration = 10f;
    }

    /// An item given for good to a dev character (devMode), at level 1.
    [Serializable] public class DevItem { public int pokemon, type = -1; public string family; }
    [Serializable] public class TeraShard { public int type; public string name, icon; }
    [Serializable] public class Rayquaza { public int mega, move; public string icon; }
    [Serializable] public class Levels { public float[] zPower = { 3f, 3.5f, 4f }, zScale = { 1.6f, 1.8f, 2f }, teraTime = { 40f, 50f, 60f }; }

    [Serializable]
    public class LootSettings
    {
        public float bossMega = 1f, bossOther = 0.6f, eliteChance = 0.033f, eliteDrop = 0.5f, eliteHealth = 2f, teamTypeWeight = 3f;
    }

    [Serializable]
    class File
    {
        public MegaStone[] megaStones = new MegaStone[0];
        public ZCrystal[] zCrystals = new ZCrystal[0];
        public TeraShard[] teraShards = new TeraShard[0];
        public Rayquaza rayquaza = new();
        public Levels levels = new();
        public float zPose = 0.8f;
        public LootSettings loot = new();
        public DevItem[] dev = new DevItem[0];
        public string zFlash = "evolution_onde";
    }

    /// What a member brings into a run: the family of its active item and that item.
    public class Equipped
    {
        public Family family;
        public MegaStone stone; // Mega (null for Rayquaza, which needs none)
        public int type = -1; // Z and Tera
        public int level = 1;
    }

    static File file;
    static readonly Dictionary<string, Sprite> icons = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        file = null;
        icons.Clear();
    }

    static File Data
    {
        get
        {
            if (file != null) return file;
            var asset = Resources.Load<TextAsset>("Data/items");
            file = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return file;
        }
    }

    public static IReadOnlyList<MegaStone> Stones => Data.megaStones;
    public static IReadOnlyList<ZCrystal> Crystals => Data.zCrystals;
    public static IReadOnlyList<TeraShard> Shards => Data.teraShards;
    public static LootSettings Loot => Data.loot;
    public static float ZPose => Data.zPose;
    public static string ZFlash => Data.zFlash;
    public static int RayquazaMega => Data.rayquaza.mega;
    public static int RayquazaMove => Data.rayquaza.move;

    public static MegaStone Stone(string key) => Array.Find(Data.megaStones, s => s.key == key);
    public static MegaStone StoneOf(int megaId) => Array.Find(Data.megaStones, s => s.mega == megaId);
    public static ZCrystal Crystal(int type) => Array.Find(Data.zCrystals, c => c.type == type);
    public static TeraShard Shard(int type) => Array.Find(Data.teraShards, t => t.type == type);

    static float AtLevel(float[] values, int level) => values[Mathf.Clamp(level - 1, 0, values.Length - 1)];
    public static float ZPower(int level) => AtLevel(Data.levels.zPower, level);
    public static float ZScale(int level) => AtLevel(Data.levels.zScale, level);
    public static float TeraTime(int level) => AtLevel(Data.levels.teraTime, level);

    /// The mega evolutions of this Pokémon's evolution line (any form of it), with sprites.
    public static List<MegaDef> MegasOfLine(int pokemonId)
    {
        var result = new List<MegaDef>();
        var me = GameData.Pokemon(pokemonId);
        if (me == null) return result;
        foreach (var stone in Data.megaStones)
            if (GameData.Mega(stone.mega) is { } m && GameData.Pokemon(m.@base)?.baseForm == me.baseForm) result.Add(m);
        if (GameData.Mega(RayquazaMega) is { } r && GameData.Pokemon(r.@base)?.baseForm == me.baseForm) result.Add(r);
        return result;
    }

    /// Whether the families of the menu's notches apply to this Pokémon: Z and Tera always, Mega when its line has one.
    public static bool HasMegaLine(int pokemonId) => MegasOfLine(pokemonId).Count > 0;

    /// A Z crystal is usable when the Pokémon knows a damaging move of its type.
    public static bool KnowsMoveOfType(IEnumerable<MoveDef> moves, int type)
    {
        foreach (var m in moves)
            if (m != null && m.power > 0 && m.type == type && m.family != "heal" && m.family != "modifier" && m.family != "climate") return true;
        return false;
    }

    /// The active item a Pokémon brings into a run (its loadout, if still owned), or null.
    public static Equipped ActiveFor(int pokemonId)
    {
        if (GameData.IsDev(pokemonId))
        {
            var d = Array.Find(Data.dev, x => x.pokemon == pokemonId);
            return d == null ? null : d.family switch
            {
                "z" => new Equipped { family = Family.Z, type = d.type },
                "tera" => new Equipped { family = Family.Tera, type = d.type },
                _ => null,
            };
        }
        var l = SaveData.LoadoutOf(pokemonId);
        switch (l.active)
        {
            case "mega":
                if (l.mega == RayquazaKey) return new Equipped { family = Family.Mega };
                var stone = Stone(l.mega);
                return stone != null && SaveData.HasStone(stone.key) ? new Equipped { family = Family.Mega, stone = stone } : null;
            case "z":
                int z = SaveData.ZLevel(l.z);
                return l.z >= 0 && z > 0 ? new Equipped { family = Family.Z, type = l.z, level = z } : null;
            case "tera":
                int t = SaveData.TeraLevel(l.tera);
                return l.tera >= 0 && t > 0 ? new Equipped { family = Family.Tera, type = l.tera, level = t } : null;
            default:
                return null;
        }
    }

    /// Icon file (Assets/Resources/Items, from Tools/pmd/import_items.py) of an item: a mega stone's key (or
    /// RayquazaKey), else the type of a Z crystal or Tera shard. Null when unknown.
    public static string IconName(Family family, string stone, int type) => family switch
    {
        Family.Mega => stone == RayquazaKey ? Data.rayquaza.icon : Stone(stone)?.icon,
        Family.Z => Crystal(type)?.icon,
        Family.Tera => Shard(type)?.icon,
        _ => null,
    };

    public static string IconName(Equipped e) =>
        e == null ? null : IconName(e.family, e.family == Family.Mega ? e.stone?.key ?? RayquazaKey : null, e.type);

    /// The icon as a sprite one tile wide, or null when its file is missing (setup not run).
    public static Sprite Icon(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (icons.TryGetValue(name, out var sprite) && sprite != null) return sprite;
        var texture = Resources.Load<Texture2D>("Items/" + name);
        sprite = texture == null ? null
            : Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width);
        icons[name] = sprite;
        return sprite;
    }

    static readonly Dictionary<string, string> StatNames = new() { { "attack", "Attaque" }, { "defense", "Défense" }, { "speed", "Vitesse" } };

    /// What a Z crystal's move does besides damage, in a few words (the Z demo room).
    public static string ZDescription(int type)
    {
        var h = Crystal(type)?.hit;
        if (h == null) return "";
        var parts = new List<string>();
        if (h.bonus > 1f) parts.Add($"dégâts +{(h.bonus - 1f) * 100f:0} %");
        if (!string.IsNullOrEmpty(h.status)) parts.Add(StatusLibrary.Get(h.status)?.name ?? h.status);
        if (!string.IsNullOrEmpty(h.modStat)) parts.Add($"{StatNames.GetValueOrDefault(h.modStat, h.modStat)} {(h.modValue - 1f) * 100f:0} %");
        if (h.stun > 0f) parts.Add($"étourdit {h.stun:0.#} s");
        if (h.knockback > 0f) parts.Add("repousse");
        if (h.knockback < 0f) parts.Add("attire");
        if (h.drain > 0f) parts.Add("soigne l'équipe");
        if (!string.IsNullOrEmpty(h.terrain)) parts.Add($"terrain {h.terrain}");
        if (!string.IsNullOrEmpty(h.teamModStat)) parts.Add($"équipe : {StatNames.GetValueOrDefault(h.teamModStat, h.teamModStat)} +{(h.teamModValue - 1f) * 100f:0} %");
        return string.Join(", ", parts);
    }

    /// The mega slot of Rayquaza, which needs no stone.
    public const string RayquazaKey = "rayquaza";

    /// Name of what a member brings, for the menu and the banners.
    public static string NameOf(Equipped e) => e == null ? "" : e.family switch
    {
        Family.Mega => e.stone != null ? e.stone.name : "Draco Météor",
        Family.Z => $"{Crystal(e.type)?.name} (niv. {e.level})",
        Family.Tera => $"{Shard(e.type)?.name} (niv. {e.level})",
        _ => "",
    };
}
