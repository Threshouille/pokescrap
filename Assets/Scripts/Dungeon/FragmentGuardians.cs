using System;
using System.Collections.Generic;
using System.Linq;

/// The guardians of a quarter's Time Gear fragments (GDD « Quart du volcan », gardiens des fragments): each one a
/// Pokémon of the quarter's theme types (Feu and Sol on the volcano), all different. First those that can mega
/// evolve (their mega is their second phase); otherwise one that Terastallizes, at half its health, into the type that
/// takes the advantage back: the type that beats the theme (Counter: Eau against Feu and Sol), then the one that beats
/// that counter (Answer: Plante), preferably a Pokémon with a damaging move of that type. Pure, for the tests.
public static class FragmentGuardians
{
    public const int FirstType = 1, LastType = 17; // ROM type ids (Normal to Acier; no Fairy in the generation 4 chart)

    public struct Pick
    {
        public int id; // the guardian
        public MegaDef mega; // its mega evolution, or null
        public int tera; // the type it Terastallizes into (no mega), or -1
    }

    /// The attacking type that hits the theme's types hardest (their multipliers multiplied), the lowest id on a tie.
    public static int Counter(int[] themeTypes, Func<int, int[], float> multiplier)
    {
        int best = -1;
        float bestScore = float.MinValue;
        for (int t = FirstType; t <= LastType; t++)
        {
            float score = multiplier(t, themeTypes);
            if (score > bestScore + 1e-4f) { best = t; bestScore = score; }
        }
        return best;
    }

    /// The defending type that best answers that attacking type: it takes little from it and hits it hard (its own
    /// multiplier against it, plus how much it resists it), the lowest id on a tie.
    public static int Answer(int counter, Func<int, int[], float> multiplier)
    {
        int best = -1;
        float bestScore = float.MinValue;
        for (int t = FirstType; t <= LastType; t++)
        {
            if (t == counter) continue;
            float taken = Math.Max(0.01f, multiplier(counter, new[] { t }));
            float score = multiplier(t, new[] { counter }) + 1f / taken;
            if (score > bestScore + 1e-4f) { best = t; bestScore = score; }
        }
        return best;
    }

    /// "count" guardians from the candidates (Pokémon of the theme's types): those with a mega first (in a random
    /// order), then the others, best first: a damaging move of the Answer type (moves), then the most evolved (depth).
    public static List<Pick> Choose(int[] themeTypes, int count, Random rng, IEnumerable<PokemonStats> candidates,
        Func<int, MegaDef> megaOf, Func<int, IEnumerable<MoveDef>> moves, Func<PokemonStats, int> depth, Func<int, int[], float> multiplier)
    {
        var pool = candidates.Where(p => p != null && p.types != null && p.types.Intersect(themeTypes).Any()).ToList();
        int tera = Answer(Counter(themeTypes, multiplier), multiplier);
        var picks = new List<Pick>();
        var withMega = pool.Where(p => megaOf(p.id) != null).OrderBy(_ => rng.Next()).ToList();
        foreach (var p in withMega)
        {
            if (picks.Count >= count) break;
            if (picks.Any(k => SameLine(k.id, p.id, megaOf))) continue;
            picks.Add(new Pick { id = p.id, mega = megaOf(p.id), tera = -1 });
        }
        bool Strikes(PokemonStats p) => moves(p.id).Any(m => m != null && m.power > 0 && m.type == tera);
        var others = pool.Where(p => megaOf(p.id) == null && !p.types.Contains(tera))
            .OrderByDescending(Strikes).ThenByDescending(depth).ThenBy(_ => rng.Next()).ToList();
        foreach (var p in others)
        {
            if (picks.Count >= count) break;
            if (picks.Any(k => k.id == p.id)) continue;
            picks.Add(new Pick { id = p.id, mega = null, tera = tera });
        }
        return picks;
    }

    // Two picks of the same mega line (Méga-Dracaufeu X and Y share their base).
    static bool SameLine(int a, int b, Func<int, MegaDef> megaOf) => a == b || (megaOf(a)?.@base is int x && megaOf(b)?.@base == x);
}
