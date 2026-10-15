using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PokemonStats
{
    public int id;
    public string name;
    public int[] types;
    public int baseHp, baseAtk, baseSpAtk, baseDef, baseSpDef, baseSpeed, movementType, expYield;
    public int baseForm; // first form of the evolution line
    public int evolvesFrom; // previous form, 0 when none
    public float recruitRate; // percent chance to join when knocked out, from the ROM (≤ 0: never)
}

[Serializable]
public class MoveDef
{
    public int id;
    public string name;
    public int type;
    public string category; // physical, special, status
    public int power;
    public string family; // projectile, melee, zone, burst, heal, modifier
    public float cooldown; // seconds
    public float speed; // tiles per second
    public float range; // tiles: flight distance (projectile) or distance of the hit zone (melee)
    public float radius; // tiles
    public float windup; // seconds an enemy announces the move before using it
    public float lunge; // melee only: tiles the attacker dashes forward while striking (0 = strikes in place)
    public float lungeTime; // seconds the dash lasts
    public string effect; // sprite sheet in Resources/Effects: flight (projectile), strike (melee) or dash trail (lunge)
    public string impactEffect; // sprite sheet played where the move hits
    public string status; // status given to the target: a key of statuses.json
    public float statusChance; // 0..1, defaults to 1
    public float statusDuration; // seconds
    public float zoneDuration; // zone only: seconds the area stays
    public int count; // projectile only: number of shots fired at once (1 when omitted)
    public float spread; // projectile only: total angle, in degrees, over which the shots are fanned out
    public float healShare; // heal only: share of the target's max HP given back
    public float drain; // share of the damage dealt that heals the most wounded ally
    public float recoil; // share of the damage dealt that the user loses
    public float statChance; // 0..1 chance of the on-hit modifier (always when omitted)
    public string selfModStat; // bonus the user gets when the move lands
    public float selfModValue, selfModDuration, selfStatChance;
    public string target; // modifier: self, allies or enemies (around the user, within radius); zone: "each" = one under every opponent
    public string modStat; // attack, defense or speed: temporary bonus or penalty (on hit, or cast by a modifier move)
    public float modValue; // multiplier, above 1 = bonus, below 1 = penalty
    public float modDuration; // seconds

    /// Distance from which an AI can expect the move to connect.
    public float Reach => family == "melee" ? lunge + range + radius * 0.5f
        : family == "heal" ? range
        : family == "modifier" || family == "burst" ? radius * 0.9f
        : range * 0.85f;
}

[Serializable]
public class UpgradeDef
{
    public string id;
    public string name;
    public string description;
    public string stat; // maxHp, attack, specialAttack, moveSpeed, cooldown, projectileSize, projectileSpeed, meleeArea
    public float value; // share added (or removed, for cooldown)
    public string family; // only offered to a Pokémon that has a move of this family; empty = always
}

/// Game data loaded from Resources/Data (pokemon_rt.json comes from the ROM, realtime_moves.json is hand-written).
public static class GameData
{
    [Serializable] class PokemonFile { public PokemonStats[] pokemon; }
    [Serializable] class TypeColorEntry { public int type; public string color; }
    [Serializable] class Loadout { public int pokemonId; public string role; public int[] moves; }
    [Serializable] class UpgradeFile { public UpgradeDef[] upgrades; }
    [Serializable] class MoveFile { public TypeColorEntry[] typeColors; public MoveDef[] moves; public Loadout[] loadouts; }

    // Base speed in the main games goes from 5 to 160; squeezed into a playable range.
    const float MinBaseSpeed = 5f, MaxBaseSpeed = 160f, MinMoveSpeed = 3f, MaxMoveSpeed = 6f;

    static Dictionary<int, PokemonStats> pokemon;
    static Dictionary<int, MoveDef> moves;
    static Dictionary<int, int[]> loadouts;
    static Dictionary<int, string> roles;
    static Dictionary<int, Color> typeColors;
    static UpgradeDef[] upgrades;

    // Statics can outlive a play session in the editor: reload the files each time a game starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => pokemon = null;

    static void Load()
    {
        if (pokemon != null) return;
        pokemon = new Dictionary<int, PokemonStats>();
        foreach (var p in JsonUtility.FromJson<PokemonFile>(Resources.Load<TextAsset>("Data/pokemon_rt").text).pokemon)
            pokemon[p.id] = p;

        var file = JsonUtility.FromJson<MoveFile>(Resources.Load<TextAsset>("Data/realtime_moves").text);
        moves = new Dictionary<int, MoveDef>();
        // Generic moves made from the ROM by generate_moves.py, then the hand-written ones on top.
        var generated = Resources.Load<TextAsset>("Data/generated_moves");
        if (generated != null)
            foreach (var m in JsonUtility.FromJson<MoveFile>(generated.text).moves) moves[m.id] = m;
        foreach (var m in file.moves) moves[m.id] = m;
        loadouts = new Dictionary<int, int[]>();
        roles = new Dictionary<int, string>();
        foreach (var l in file.loadouts)
        {
            loadouts[l.pokemonId] = l.moves;
            roles[l.pokemonId] = string.IsNullOrEmpty(l.role) ? "melee" : l.role;
        }
        var upgradeFile = Resources.Load<TextAsset>("Data/upgrades");
        upgrades = upgradeFile != null ? JsonUtility.FromJson<UpgradeFile>(upgradeFile.text).upgrades : new UpgradeDef[0];
        typeColors = new Dictionary<int, Color>();
        foreach (var t in file.typeColors)
            if (ColorUtility.TryParseHtmlString(t.color, out var c)) typeColors[t.type] = c;
    }

    public static PokemonStats Pokemon(int id)
    {
        Load();
        return pokemon.TryGetValue(id, out var p) ? p : null;
    }

    /// True when the Pokémon has starting moves, i.e. can be played.
    public static bool HasLoadout(int pokemonId)
    {
        Load();
        return loadouts.ContainsKey(pokemonId);
    }

    /// melee, ranged or support (melee when unknown).
    public static string Role(int pokemonId)
    {
        Load();
        return roles.TryGetValue(pokemonId, out var role) ? role : "melee";
    }

    /// Starting moves of a Pokémon, in slot order (missing slots are null).
    public static MoveDef[] StartingMoves(int pokemonId, int slots)
    {
        Load();
        var result = new MoveDef[slots];
        if (loadouts.TryGetValue(pokemonId, out var ids))
            for (int i = 0; i < slots && i < ids.Length; i++)
                moves.TryGetValue(ids[i], out result[i]);
        return result;
    }

    /// Up to "count" different upgrades that make sense for this Pokémon, picked at random.
    public static UpgradeDef[] RandomUpgrades(Combatant combatant, int count, System.Random rng)
    {
        Load();
        var pool = new List<UpgradeDef>();
        foreach (var upgrade in upgrades)
        {
            bool fits = string.IsNullOrEmpty(upgrade.family);
            if (!fits)
                foreach (var move in combatant.Moves)
                    if (move != null && move.family == upgrade.family) fits = true;
            if (fits) pool.Add(upgrade);
        }
        for (int i = pool.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }
        return pool.GetRange(0, Mathf.Min(count, pool.Count)).ToArray();
    }

    public static Color TypeColor(int type)
    {
        Load();
        return typeColors.TryGetValue(type, out var c) ? c : Color.white;
    }

    public static float MoveSpeed(int baseSpeed)
    {
        return Mathf.Lerp(MinMoveSpeed, MaxMoveSpeed, Mathf.InverseLerp(MinBaseSpeed, MaxBaseSpeed, baseSpeed));
    }
}
