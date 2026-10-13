using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PokemonStats
{
    public int id;
    public string name;
    public int[] types;
    public int baseHp, baseAtk, baseSpAtk, baseDef, baseSpDef, baseSpeed, movementType;
}

[Serializable]
public class MoveDef
{
    public int id;
    public string name;
    public int type;
    public string category; // physical, special, status
    public int power;
    public string family; // projectile, melee, zone
    public float cooldown; // seconds
    public float speed; // tiles per second
    public float range; // tiles: flight distance (projectile) or distance of the hit zone (melee)
    public float radius; // tiles
    public float windup; // seconds an enemy announces the move before using it
    public float lunge; // melee only: tiles the attacker dashes forward while striking (0 = strikes in place)
    public float lungeTime; // seconds the dash lasts
    public string effect; // sprite sheet in Resources/Effects: flight (projectile), strike (melee) or dash trail (lunge)
    public string impactEffect; // sprite sheet played where the move hits
    public string status; // status given to the target: "poison" (others to come)
    public float statusChance; // 0..1, defaults to 1
    public float statusDuration; // seconds
    public float zoneDuration; // zone only: seconds the area stays

    /// Distance from which an AI can expect the move to connect.
    public float Reach => family == "melee" ? lunge + range + radius * 0.5f : range * 0.85f;
}

/// Game data loaded from Resources/Data (pokemon_rt.json comes from the ROM, realtime_moves.json is hand-written).
public static class GameData
{
    [Serializable] class PokemonFile { public PokemonStats[] pokemon; }
    [Serializable] class TypeColorEntry { public int type; public string color; }
    [Serializable] class Loadout { public int pokemonId; public int[] moves; }
    [Serializable] class MoveFile { public TypeColorEntry[] typeColors; public MoveDef[] moves; public Loadout[] loadouts; }

    // Base speed in the main games goes from 5 to 160; squeezed into a playable range.
    const float MinBaseSpeed = 5f, MaxBaseSpeed = 160f, MinMoveSpeed = 3f, MaxMoveSpeed = 6f;

    static Dictionary<int, PokemonStats> pokemon;
    static Dictionary<int, MoveDef> moves;
    static Dictionary<int, int[]> loadouts;
    static Dictionary<int, Color> typeColors;

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
        foreach (var m in file.moves) moves[m.id] = m;
        loadouts = new Dictionary<int, int[]>();
        foreach (var l in file.loadouts) loadouts[l.pokemonId] = l.moves;
        typeColors = new Dictionary<int, Color>();
        foreach (var t in file.typeColors)
            if (ColorUtility.TryParseHtmlString(t.color, out var c)) typeColors[t.type] = c;
    }

    public static PokemonStats Pokemon(int id)
    {
        Load();
        return pokemon.TryGetValue(id, out var p) ? p : null;
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
