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
    public EvolutionDef evolution; // how the previous form turns into this one (method empty: not by itself)
}

/// Evolution condition from the ROM (extract_data.py). method: level, iq, item, party (a Pokémon in the team),
/// trade; condition, on top of it: move (knows that move), day, night, beauty, male, female, randomA, randomB,
/// atkAboveDef, atkBelowDef, atkEqualsDef, trade (an item evolution that also needs a trade).
[Serializable]
public class EvolutionDef
{
    public string method, condition, itemName;
    public int level, iq, item, party, move;
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
    public bool basic; // basic attack: short cooldown, low damage (one per type, see basicByType), used last by the AI
    public float damageScale; // multiplies the damage (1 when omitted)
    public string path; // projectile: straight (default), wave, zigzag, curve, orbit, arc, boomerang, homing
    public float pathAmplitude; // tiles: sideways swing (wave, zigzag, curve), orbit radius, or arc height
    public float pathPeriod; // tiles travelled per cycle (wave, zigzag, orbit)
    public int burst; // projectile: volleys fired one after the other (1 when omitted)
    public float burstInterval; // seconds between two volleys
    public float jitter; // degrees: random deviation of each shot, either side
    public float growth; // size multiplier reached at the end of the flight (unchanged when omitted)
    public float accel; // tiles per second², negative to slow down
    public bool pierce; // goes through opponents, hitting each once
    public int bounces; // times the shot bounces off walls
    public float splash; // arc: radius of the hit where it lands
    public float rangeStep; // projectile: each shot after the first flies this much further (tiles)
    public float puddle; // arc: seconds the puddle showing the splash area stays (fading out); none when 0
    public string puddleColor; // colour of the puddle (#rrggbb), the type colour when omitted
    public string mark; // arc: sheet drawn on the ground instead of the puddle disc (e.g. cracks), sized to the splash area
    public int markFrame; // frame of that sheet
    public float markFill; // share of the frame width the drawing covers (1 when omitted)
    public float effectScale; // size multiplier of the effect sheets (1 when omitted)
    public float homing; // homing: degrees per second the shot turns toward its target
    public float castTime; // projectile: seconds the user stands still, aim locked, before the shots leave
    public bool sequential; // projectile: the shots of a fan leave one by one (burstInterval apart), middle first, then left, right
    public string shape; // melee: circle (default: in front, range away), arc (sector around the user: range, arc), thrust (line from the user: range long, radius wide), around (circle around the user)
    public float arc; // melee arc: total angle in degrees
    public int hits; // melee: strikes in a row (1 when omitted)
    public float hitInterval; // seconds between two strikes
    public float[] hitAngles; // degrees: offset of each strike from the aim, in turn (alternating swings, crossed claws)
    public float step; // melee: tiles the user steps forward with each strike
    public float finishRadius; // melee dash: radius of a strike around the user where the dash ends
    public float radiusMin; // melee around: the radius is drawn between this and range
    public float chainBonus, chainMax, chainWindow; // melee: each strike landing within chainWindow seconds of the previous one adds chainBonus to the damage, up to ×chainMax
    public bool teleport; // melee: the user blinks lunge tiles forward (through opponents, not walls), then strikes
    public string finishEffect; // melee dash: sheet of the strike where the dash ends, stretched to finishRadius
    public string trailEffect; // melee teleport: sheet left where the user blinked from
    public float strikeOffset; // melee: tiles in front of the user where the strike area starts (arc, thrust)
    public float recovery; // seconds the user stays rooted after the move
    public float hop; // tiles: little jump in place during the cast (melee dash)
    public float knockback; // melee: tiles the target is pushed away when hit
    public float knockbackTime; // seconds the push lasts, slowing down (Combatant.KnockbackTime when omitted)
    public float teleportDelay; // melee teleport: seconds the user stays gone before reappearing
    public float stun; // melee: seconds a hit stuns its target (meleeStun of meta.json when omitted)
    public bool lungeHarmless; // melee dash that hits nobody on the way
    public bool strikeAfterLunge; // melee dash followed by the move's strikes, strikeDelay seconds after it ends
    public float strikeDelay;
    public float bounceBack; // melee dash: tiles the user is thrown back when the dash hits someone
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
    public float Reach => family == "melee" ? lunge + (shape == "arc" || shape == "thrust" ? range
            : shape == "around" ? (radiusMin > 0f ? radiusMin : range) * 0.9f : range + radius * 0.5f)
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
    [Serializable] class BasicEntry { public int type; public int pokemonId; public int move; }
    [Serializable] class MoveFile { public TypeColorEntry[] typeColors; public MoveDef[] moves; public Loadout[] loadouts; public BasicEntry[] basicByType, meleeBasicByType, basicByPokemon; }

    // Base speed in the main games goes from 5 to 160; squeezed into a playable range.
    const float MinBaseSpeed = 5f, MaxBaseSpeed = 160f, MinMoveSpeed = 3f, MaxMoveSpeed = 6f;

    static Dictionary<int, PokemonStats> pokemon;
    static Dictionary<int, MoveDef> moves;
    static Dictionary<int, int[]> loadouts;
    static Dictionary<int, string> roles;
    static Dictionary<int, Color> typeColors;
    static Dictionary<int, int> basicByType, meleeBasicByType;
    static Dictionary<int, List<int>> basicByPokemon;
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
        basicByType = new Dictionary<int, int>();
        basicByPokemon = new Dictionary<int, List<int>>();
        foreach (var b in file.basicByType ?? new BasicEntry[0]) basicByType[b.type] = b.move;
        meleeBasicByType = new Dictionary<int, int>();
        foreach (var b in file.meleeBasicByType ?? new BasicEntry[0]) meleeBasicByType[b.type] = b.move;
        foreach (var b in file.basicByPokemon ?? new BasicEntry[0])
        {
            if (!basicByPokemon.ContainsKey(b.pokemonId)) basicByPokemon[b.pokemonId] = new List<int>();
            basicByPokemon[b.pokemonId].Add(b.move); // several entries for one Pokémon: several basic attacks
        }
        typeColors = new Dictionary<int, Color>();
        foreach (var t in file.typeColors)
            if (ColorUtility.TryParseHtmlString(t.color, out var c)) typeColors[t.type] = c;
    }

    public static PokemonStats Pokemon(int id)
    {
        Load();
        return pokemon.TryGetValue(id, out var p) ? p : null;
    }

    /// Moves this Pokémon can pick from: its basic attacks, then those it learns by level that the game can play,
    /// in level order.
    public static List<MoveDef> MovePool(int pokemonId)
    {
        Load();
        var pool = BasicMoves(pokemonId);
        var stats = Pokemon(pokemonId);
        if (stats?.levelUpMoves != null)
            foreach (int id in stats.levelUpMoves)
                if (moves.TryGetValue(id, out var move) && !pool.Contains(move)) pool.Add(move);
        return pool;
    }

    /// Basic attacks of a Pokémon: its own entries in basicByPokemon, else those of each of its types. style:
    /// "ranged", "melee", or null for both (the style matching its stats first).
    public static List<MoveDef> BasicMoves(int pokemonId, string style = null)
    {
        Load();
        var result = new List<MoveDef>();
        if (basicByPokemon.TryGetValue(pokemonId, out var own))
        {
            foreach (int id in own) Add(id);
            if (style != null) result.RemoveAll(m => (m.family == "melee") != (style == "melee"));
            return result;
        }
        var stats = Pokemon(pokemonId);
        if (stats?.types == null) return result;
        var styles = style != null ? new[] { style } : StatStyle(pokemonId) == "melee" ? new[] { "melee", "ranged" } : new[] { "ranged", "melee" };
        foreach (var st in styles)
            foreach (int type in stats.types)
                if ((st == "melee" ? meleeBasicByType : basicByType).TryGetValue(type, out int id)) Add(id);
        return result;

        void Add(int id)
        {
            var move = Move(id);
            if (move != null && !result.Contains(move)) result.Add(move);
        }
    }

    /// Style of basic attacks that suits a Pokémon: melee when its Attack beats its Special Attack, ranged otherwise.
    public static string StatStyle(int pokemonId)
    {
        var stats = Pokemon(pokemonId);
        return stats != null && stats.baseAtk > stats.baseSpAtk ? "melee" : "ranged";
    }

    /// Starting moves when the player has not chosen any: basic attacks, then the hand-written loadout where it
    /// fits, then the pool.
    public static int[] DefaultStartingMoves(int pokemonId, int count)
    {
        Load();
        var pool = MovePool(pokemonId).ConvertAll(m => m.id);
        var result = BasicMoves(pokemonId, StatStyle(pokemonId)).ConvertAll(m => m.id);
        if (result.Count > count) result.RemoveRange(count, result.Count - count);
        if (loadouts.TryGetValue(pokemonId, out var ids))
            foreach (int id in ids)
                if (result.Count < count && pool.Contains(id) && !result.Contains(id)) result.Add(id);
        foreach (int id in pool)
            if (result.Count < count && !result.Contains(id)) result.Add(id);
        return result.ToArray();
    }

    /// Forms this Pokémon can evolve into, whatever the condition. Only those with sprites in the project; forms
    /// without a condition of their own (Munja, which comes with Ninjask) are left out.
    public static List<PokemonStats> Evolutions(int pokemonId)
    {
        Load();
        var result = new List<PokemonStats>();
        var names = new HashSet<string>(); // alternative forms of a species (several Cheniselle) count once
        foreach (var p in pokemon.Values)
            if (p.evolvesFrom == pokemonId && p.id != pokemonId && !string.IsNullOrEmpty(p.evolution?.method)
                && PokemonRoster.Has(p.id) && names.Add(p.name))
                result.Add(p);
        return result;
    }

    /// "count" moves for an enemy: one of its basic attacks (style matching its stats), then moves drawn from the pool, the first one dealing
    /// damage when the pool has one.
    /// "role" (melee, ranged or support; null for any): the moves are drawn so that the Pokémon gets that role
    /// (see RoleFromMoves), when its list allows it (CanTakeRole).
    public static int[] RandomMoves(int pokemonId, int count, System.Random rng, string role = null)
    {
        var basics = BasicMoves(pokemonId, StatStyle(pokemonId));
        var pool = MovePool(pokemonId);
        pool.RemoveAll(m => m.basic);
        if (role != null && role != "support") pool.RemoveAll(IsSupportMove); // would turn it into a support
        var result = new List<int>();
        if (basics.Count > 0 && count > 0) result.Add(basics[rng.Next(basics.Count)].id);
        if (result.Count >= count) return result.ToArray();
        var first = pool.FindAll(m => role == null ? m.power > 0 : FitsRole(m, role));
        if (first.Count == 0) first = pool.FindAll(m => m.power > 0);
        if (first.Count > 0) result.Add(first[rng.Next(first.Count)].id);
        while (result.Count < count)
        {
            var left = pool.FindAll(m => !result.Contains(m.id));
            if (left.Count == 0) break;
            result.Add(left[rng.Next(left.Count)].id);
        }
        return result.ToArray();
    }

    /// True when RandomMoves can give the Pokémon that role.
    public static bool CanTakeRole(int pokemonId, string role)
    {
        var basics = BasicMoves(pokemonId);
        return MovePool(pokemonId).Exists(m => !basics.Contains(m) && FitsRole(m, role));
    }

    static bool IsSupportMove(MoveDef m) => m.family == "heal" || (m.family == "modifier" && m.target == "allies");

    // A move that makes a Pokémon take the role (with only a basic attack beside it).
    static bool FitsRole(MoveDef m, string role) => role switch
    {
        "support" => IsSupportMove(m),
        "ranged" => m.power > 0 && (m.family == "projectile" || m.family == "zone"),
        _ => m.power > 0 && (m.family == "melee" || m.family == "burst"),
    };

    public static MoveDef Move(int id)
    {
        Load();
        return moves.TryGetValue(id, out var move) ? move : null;
    }

    /// AI role deduced from the moves a Pokémon has: support with a heal or a bonus for its allies, ranged when
    /// shots and zones are at least as many as close-range moves, melee otherwise. The basic attack only counts
    /// when there is nothing else.
    public static string RoleFromMoves(IEnumerable<MoveDef> known)
    {
        int ranged = 0, close = 0;
        MoveDef basic = null;
        foreach (var move in known)
        {
            if (move == null) continue;
            if (move.basic) { basic = move; continue; }
            if (IsSupportMove(move)) return "support";
            if (move.family == "projectile" || move.family == "zone") ranged++;
            else if (move.family == "melee" || move.family == "burst") close++;
        }
        if (ranged == 0 && close == 0 && basic != null) return basic.family == "projectile" ? "ranged" : "melee";
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
