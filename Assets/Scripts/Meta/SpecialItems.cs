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
    [Serializable] public class ZCrystal { public int type; public string name, move, icon; public ZHit hit = new(); public ZShow show = new(); }

    /// How a Z move's strike looks (one per type; ZStrike): "style" rise (the impact springs up under each opponent),
    /// fall (the "travel" sheet drops from the sky), bolt (a bolt from the sky), wave (a ring spreading out from the
    /// user), implode (a ring closing in on it), sweep (a line of "travel" sheets crossing the screen), shot (a "travel"
    /// sheet flying to each opponent). Each opponent is struck when the show reaches it. "wave": a sheet played once
    /// on the user at the release; "impact" (else hit.effect) on each opponent, "size" tiles wide (× zScale);
    /// "travel" "travelSize" tiles wide; "tint": the travel sheets take the type's colour; "speed": tiles per second
    /// (wave, implode, sweep, shot); "cascade": seconds between two opponents (rise, fall, bolt); "fallTime": seconds
    /// of a fall; "shake": the camera at each hit; "lead": seconds of build-up before the first hit (a sun forming, the
    /// user flying up, a black hole opening); "extra": a second sheet some stagings use (ZChoreo); "piece": a third (a vine, an ice spike, a hand, a dragon's scale, a puddle, a cocoon). The first styles
    /// were joined by one staging per type: zdash, sun, tide, vines, chain, freeze, afterimage, acid, rift, dive,
    /// field, web, boulders, hands, serpent, blackhole, drill (ZChoreo).
    [Serializable]
    public class ZShow
    {
        public string style = "rise", travel, wave, impact, extra, piece;
        public float size = 1.5f, travelSize = 1.5f, speed = 16f, cascade = 0.06f, fallTime = 0.45f, shake = 0.08f, lead;
        public bool tint;
    }

    /// The Z cut-scene (ZCutScene): seconds of the opening freeze, of the pose (dark, crystal, rings) and of the title;
    /// camera zoom, darkness of the veil, height of each black band (share of the screen); everybody else holds still
    /// strikeHold seconds more, during the strike. An enemy's Z has no cut-scene but a warning of enemyWarn seconds
    /// (crystal, rings, the fight goes on), and its strike can be dodged (EnemySettings.zDodgeWindow).
    [Serializable]
    public class ZCut
    {
        public float freeze = 0.2f, pose = 1.1f, title = 0.9f, zoom = 1.35f, dim = 0.7f, bands = 0.11f, enemyWarn = 1f, strikeHold = 1f;
    }

    /// What a Z move does to each opponent it strikes besides damage (one per type): see Ultimates.StrikeZ.
    [Serializable]
    public class ZHit
    {
        public string effect, status, modStat, terrain, teamModStat;
        public float bonus = 1f, statusDuration, modValue = 1f, modDuration, stun, knockback, drain;
        public float terrainRadius = 1f, terrainDuration = 8f, teamModValue = 1f, teamModDuration = 10f;
        // "pull": tiles each opponent is drawn towards the strike's centre (a black hole) before it is struck;
        // "teleport": each opponent struck is thrown onto a random free tile in view.
        public float pull;
        public bool teleport;
    }

    /// An item given for good to a dev character (devMode), at level 1.
    [Serializable]
    public class EnemySettings
    {
        public int zMinTargets = 2, level = 1;
        public float startDelay = 4f, checkEvery = 0.5f, teraMinGain = 0.2f, zRange = 8f, zKillShare = 0.6f, zMaxShare = 0.4f;
        public float megaChance = 0.6f, teraCrossBonus = 0.5f;
        // An enemy's Z strike spares an opponent that dodged at most zDodgeWindow seconds before it is reached; an ally
        // (AllyAI) dodges it with the chance aiZDodge, aiZDodgeLead seconds before.
        public float zDodgeWindow = 0.35f, aiZDodge = 0.8f, aiZDodgeLead = 0.25f;
        public float teraTerrainBonus = 0.6f, terrainRadius = 8f;
        public int terrainMinTiles = 4;
        // Second phase (mega or Tera): under phaseHealth of its HP; heal share by kind of enemy; recharges × phaseCooldownMul.
        public float phaseHealth = 0.5f, phaseHeal = 0.3f, phaseHealBoss = 0.2f, phaseHealRanked = 0.1f, phaseCooldownMul = 0.8f, phaseKnockback = 3f;
        // A boss's second phase stops the fight (BossCutScene): seconds in all, of the opening freeze and of the reveal;
        // camera zoom, darkening of the rest of the screen, seconds of the camera's slide.
        public float bossCutScene = 2f, cutFreeze = 0.2f, cutReveal = 0.7f, cutZoom = 1.2f, cutDim = 0.6f, cutSlide = 0.5f;
        // Its fall (BossDefeat): seconds, the team's heal (share of max HP), the title.
        public float bossDefeat = 1.6f, bossDefeatHeal = 0.4f;
        public string bossDefeatTitle = "BOSS VAINCU";
        public float guardianCutScene = 1.4f; // seconds of a key dungeon guardian's Tera on screen
        public float guardianDefeat = 1.2f, guardianDefeatHeal = 0.2f; // its fall
        public string guardianDefeatTitle = "GARDIEN VAINCU";
    }

    [Serializable] public class FamilyIcons { public string mega, z, tera; }

    [Serializable] public class DevItem { public int pokemon, type = -1; public string family; }
    [Serializable] public class TeraShard { public int type; public string name, icon; }
    [Serializable] public class Rayquaza { public int mega, move; public string icon; }
    [Serializable] public class Levels { public float[] zPower = { 3f, 3.5f, 4f }, zScale = { 1.6f, 1.8f, 2f }, teraTime = { 40f, 50f, 60f }; }

    [Serializable]
    public class LootSettings
    {
        public float bossMega = 1f, bossOther = 0.6f, eliteChance = 0.033f, eliteDrop = 0.5f, eliteHealth = 2f, teamTypeWeight = 3f;
        public string chestIcon = "pmd_Chest_Gold"; // the chest's sprite (Resources/Items), else a drawn box
    }

    [Serializable]
    class File
    {
        public MegaStone[] megaStones = new MegaStone[0];
        public ZCrystal[] zCrystals = new ZCrystal[0];
        public TeraShard[] teraShards = new TeraShard[0];
        public Rayquaza rayquaza = new();
        public Levels levels = new();
        public ZCut zCutScene = new();
        public LootSettings loot = new();
        public DevItem[] dev = new DevItem[0];
        public FamilyIcons familyIcons = new();
        public EnemySettings enemy = new();
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
    public static EnemySettings Enemy => Data.enemy;
    public static ZCut ZCutScene => Data.zCutScene;

    /// Seconds from the trigger to the release of a Z move (the cut-scene), the player's or an enemy's.
    public static float ZCastTime(bool enemy)
    {
        var c = Data.zCutScene;
        return enemy ? c.enemyWarn : c.freeze + c.pose + c.title;
    }

    /// Seconds from the trigger to the end of the strike's hold (the tests wait that long, plus the show's travel).
    public static float ZLength(bool enemy) => ZCastTime(enemy) + (enemy ? 0f : Data.zCutScene.strikeHold);
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

    /// Icon standing for a whole family (an empty notch of the menu shows it as a black silhouette).
    public static string FamilyIcon(Family family) => family switch
    {
        Family.Mega => Data.familyIcons.mega,
        Family.Z => Data.familyIcons.z,
        Family.Tera => Data.familyIcons.tera,
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
        if (h.pull > 0f) parts.Add("attire");
        if (h.teleport) parts.Add("téléporte");
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
