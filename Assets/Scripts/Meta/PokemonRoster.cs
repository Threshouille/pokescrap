using System.Collections.Generic;
using UnityEngine;

/// Ids of every Pokémon that has sprites in the project, filled by the "Pokescrap > Build Anim Sets" menu.
/// Only ids are kept here: each anim set lives in Resources/AnimSets/<id> and is loaded on demand, since loading
/// every sprite sheet at once takes gigabytes.
public class PokemonRoster : ScriptableObject
{
    public const string ResourcePath = "Data/Roster";
    public const string AnimSetPath = "AnimSets";

    public List<int> ids = new();

    static PokemonRoster instance;
    static HashSet<int> known;
    static readonly Dictionary<int, PmdAnimSet> loaded = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;
        known = null;
        loaded.Clear();
    }

    public static PokemonRoster Instance => instance != null ? instance : instance = Resources.Load<PokemonRoster>(ResourcePath);

    /// Has sprites, without loading them.
    public static bool Has(int pokemonId)
    {
        if (known == null) known = Instance != null ? new HashSet<int>(Instance.ids) : new HashSet<int>();
        return known.Contains(pokemonId);
    }

    public static PmdAnimSet Get(int pokemonId)
    {
        if (!Has(pokemonId)) return null;
        if (!loaded.TryGetValue(pokemonId, out var set) || set == null)
            loaded[pokemonId] = set = Resources.Load<PmdAnimSet>($"{AnimSetPath}/{pokemonId}");
        return set;
    }

    /// Can be picked for a team: has sprites and enough moves to choose from.
    public static bool Playable(int pokemonId) => Has(pokemonId)
        && GameData.MovePool(pokemonId).Count >= SaveData.Settings.startingMoveCount;

    /// Earliest playable form of a Pokémon's evolution line, going back through its previous forms
    /// (Charizard → Charmander; Raichu → Pikachu while Pichu cannot be played). -1 when none is playable.
    public static int FirstPlayableForm(int pokemonId)
    {
        int found = -1;
        for (var stats = GameData.Pokemon(pokemonId); stats != null; stats = stats.evolvesFrom > 0 && stats.evolvesFrom != stats.id ? GameData.Pokemon(stats.evolvesFrom) : null)
            if (Playable(stats.id)) found = stats.id;
        return found;
    }

    /// Pokémon shown in the team menu: first playable forms of each evolution line, unlocked or still to recruit.
    public static List<int> Selectable()
    {
        var result = new List<int>();
        if (Instance == null) return result;
        foreach (int id in Instance.ids)
        {
            if (result.Contains(id)) continue;
            var stats = GameData.Pokemon(id);
            if (stats == null || !Playable(stats.id)) continue;
            if (FirstPlayableForm(stats.id) != stats.id) continue; // evolved forms are only reached during a run
            if (stats.baseForm > 0 && stats.baseForm != stats.id && Playable(stats.baseForm)) continue; // other forms (Unown letters...)
            if (stats.recruitRate <= 0f && !SaveData.IsUnlocked(stats.id)) continue;
            result.Add(stats.id);
        }
        result.Sort();
        return result;
    }
}
