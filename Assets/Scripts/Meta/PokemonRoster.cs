using System.Collections.Generic;
using UnityEngine;

/// Every Pokémon that has sprites in the project, filled by the "Pokescrap > Build Anim Sets" menu.
/// Kept in Resources so the menu and the arena can find any Pokémon's sprites by id.
public class PokemonRoster : ScriptableObject
{
    public const string ResourcePath = "Data/Roster";

    public List<PmdAnimSet> sets = new();

    static PokemonRoster instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => instance = null;

    public static PokemonRoster Instance => instance != null ? instance : instance = Resources.Load<PokemonRoster>(ResourcePath);

    public static PmdAnimSet Get(int pokemonId)
    {
        if (Instance == null) return null;
        foreach (var set in Instance.sets)
            if (set != null && set.pokemonId == pokemonId) return set;
        return null;
    }

    /// Can be picked for a team: has sprites and enough moves to choose from.
    public static bool Playable(int pokemonId) => Get(pokemonId) != null
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

    /// Pokémon shown in the team menu: unlocked ones, plus first playable forms that can still be recruited.
    public static List<int> Selectable()
    {
        var result = new List<int>();
        if (Instance == null) return result;
        foreach (var set in Instance.sets)
        {
            if (set == null || result.Contains(set.pokemonId)) continue;
            var stats = GameData.Pokemon(set.pokemonId);
            if (stats == null || !Playable(stats.id)) continue;
            bool recruitable = FirstPlayableForm(stats.id) == stats.id && stats.recruitRate > 0f;
            if (!recruitable && !SaveData.IsUnlocked(stats.id)) continue;
            result.Add(stats.id);
        }
        result.Sort();
        return result;
    }
}
