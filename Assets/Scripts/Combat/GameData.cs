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
    public int evolveLevel; // level at which the previous form turns into this one (0: not by level)
    public float recruitRate; // percent chance to join when knocked out, from the ROM (≤ 0: never)
    public int[] levelUpMoves; // moves learnt by level in the ROM, in level order (TMs and egg moves left out)
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

    /// Moves this Pokémon can pick from: those it learns by level that the game can play, in level order.
    public static List<MoveDef> MovePool(int pokemonId)
    {
        Load();
        var pool = new List<MoveDef>();
        var stats = Pokemon(pokemonId);
        if (stats?.levelUpMoves != null)
            foreach (int id in stats.levelUpMoves)
                if (moves.TryGetValue(id, out var move)) pool.Add(move);
        return pool;
    }

    /// Starting moves when the player has not chosen any: the hand-written loadout where it fits, then the pool.
    public static int[] DefaultStartingMoves(int pokemonId, int count)
    {
        Load();
        var pool = MovePool(pokemonId).ConvertAll(m => m.id);
        var result = new List<int>();
        if (loadouts.TryGetValue(pokemonId, out var ids))
            foreach (int id in ids)
                if (result.Count < count && pool.Contains(id)) result.Add(id);
        foreach (int id in pool)
            if (result.Count < count && !result.Contains(id)) result.Add(id);
        return result.ToArray();
    }

    /// Form this Pokémon turns into at that level, or 0. Only evolutions by level with sprites in the project.
    public static int LevelEvolution(int pokemonId, int level)
    {
        Load();
        foreach (var p in pokemon.Values)
            if (p.evolvesFrom == pokemonId && p.id != pokemonId && p.evolveLevel > 0 && level >= p.evolveLevel
                && PokemonRoster.Get(p.id) != null)
                return p.id;
        return 0;
    }

    /// "count" moves drawn from the pool, the first one dealing damage when the pool has one (enemies).
    public static int[] RandomMoves(int pokemonId, int count, System.Random rng)
    {
        var pool = MovePool(pokemonId);
        var result = new List<int>();
        var damaging = pool.FindAll(m => m.power > 0);
        if (damaging.Count > 0) result.Add(damaging[rng.Next(damaging.Count)].id);
        while (result.Count < count)
        {
            var left = pool.FindAll(m => !result.Contains(m.id));
            if (left.Count == 0) break;
            result.Add(left[rng.Next(left.Count)].id);
        }
        return result.ToArray();
    }

    public static MoveDef Move(int id)
    {
        Load();
        return moves.TryGetValue(id, out var move) ? move : null;
    }

    /// AI role deduced from the moves a Pokémon has: support with a heal or a bonus for its allies, ranged when
    /// shots and zones are at least as many as close-range moves, melee otherwise.
    public static string RoleFromMoves(IEnumerable<MoveDef> known)
    {
        int ranged = 0, close = 0;
        foreach (var move in known)
        {
            if (move == null) continue;
            if (move.family == "heal" || (move.family == "modifier" && move.target == "allies")) return "support";
            if (move.family == "projectile" || move.family == "zone") ranged++;
            else if (move.family == "melee" || move.family == "burst") close++;
        }
        return ranged > 0 && ranged >= close ? "ranged" : "melee";
    }

    /// Role written in realtime_moves.json (bosses): melee, ranged or support (melee when unknown).
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
