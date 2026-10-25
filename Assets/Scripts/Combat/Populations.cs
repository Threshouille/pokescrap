using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Who lives where on the island (Resources/Data/populations.json): the Pokémon of each sub-biome, weighted by their
/// types (more at night for some), and how strong they get with the quarter and the distance from its arrival.
/// Encounters draws its lots from Pool(habitat) instead of a décor's types when it is given a habitat.
public static class Populations
{
    [Serializable] class Weighted { public string type; public float weight = 1f; }

    [Serializable]
    class Habitat
    {
        public string key, name;
        public Weighted[] types = new Weighted[0];
        public string[] night = new string[0];
        public int[] include = new int[0], exclude = new int[0];
    }

    [Serializable]
    class Levels
    {
        public int stagePerQuarter = 1;
        public float distanceStages = 1f, distanceBonus = 4f, nightWeight = 2f;
        public int[] quarterBonus = { 0, 4, 8, 12 };
    }

    [Serializable]
    class File
    {
        public int minPool = 12;
        public Levels levels = new();
        public Habitat[] habitats = new Habitat[0];
    }

    /// Where an encounter is drawn: a sub-biome, by day or by night.
    public class Where
    {
        public string habitat;
        public bool night;
        public Where(string habitat, bool night = false) { this.habitat = habitat; this.night = night; }
    }

    static File data;
    static readonly Dictionary<(int depth, string habitat, bool night), List<int>> pools = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        data = null;
        pools.Clear();
    }

    static File Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/populations");
            data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return data;
        }
    }

    public static bool Has(string habitat) => Find(habitat) != null;

    public static IEnumerable<string> Habitats => Data.habitats.Select(h => h.key);

    /// Type names of the data that no type of the game carries (tests).
    public static IEnumerable<string> UnknownTypes() =>
        Data.habitats.SelectMany(h => h.types.Select(t => t.type).Concat(h.night)).Distinct().Where(n => TypeId(n) < 0);

    static Habitat Find(string key) => string.IsNullOrEmpty(key) ? null : Array.Find(Data.habitats, h => h.key == key);

    static int TypeId(string name)
    {
        for (int t = 0; t < 32; t++)
            if (GameData.TypeName(t) == name) return t;
        return -1;
    }

    /// Whether that Pokémon belongs to the sub-biome (one of its types weighs there, or it is forced in).
    public static bool Lives(string habitat, int pokemonId, bool night)
    {
        var h = Find(habitat);
        var p = GameData.Pokemon(pokemonId);
        if (h == null || p == null || Array.IndexOf(h.exclude, pokemonId) >= 0) return false;
        if (Array.IndexOf(h.include, pokemonId) >= 0) return true;
        var names = p.types.Select(GameData.TypeName).ToList();
        return h.types.Any(t => t.weight > 0f && names.Contains(t.type)) || (night && h.night.Any(names.Contains));
    }

    /// The Pokémon of that sub-biome among "all" (the encounters' filter at that evolution stage), each repeated as
    /// many times as its weight: a draw at random in the list follows the weights. Null when the habitat is unknown or
    /// too few of them fit (the encounters then use their own pool).
    public static List<int> Pool(int depth, string habitat, bool night, List<int> all)
    {
        if (pools.TryGetValue((depth, habitat, night), out var cached)) return cached;
        var h = Find(habitat);
        List<int> result = null;
        if (h != null)
        {
            var weights = new Dictionary<int, float>();
            foreach (var w in h.types) weights[TypeId(w.type)] = Mathf.Max(weights.GetValueOrDefault(TypeId(w.type)), w.weight);
            if (night)
                foreach (var n in h.night) weights[TypeId(n)] = Mathf.Max(weights.GetValueOrDefault(TypeId(n)), Data.levels.nightWeight);
            var picked = new List<int>();
            int distinct = 0;
            foreach (int id in all.Concat(h.include).Distinct())
            {
                if (Array.IndexOf(h.exclude, id) >= 0) continue;
                var p = GameData.Pokemon(id);
                if (p == null) continue;
                float weight = Array.IndexOf(h.include, id) >= 0 ? 1f : 0f;
                foreach (int t in p.types) weight = Mathf.Max(weight, weights.GetValueOrDefault(t));
                int copies = Mathf.RoundToInt(weight);
                if (copies <= 0) continue;
                distinct++;
                for (int k = 0; k < copies; k++) picked.Add(id);
            }
            if (distinct >= Data.minPool) result = picked;
        }
        pools[(depth, habitat, night)] = result;
        return result;
    }

    /// The encounters' stage in that quarter (0 to 3, in the run's order) at that distance from its arrival (0) to
    /// its farthest tile (1): rarities, threat budget, evolution stage.
    public static int Stage(int quarter, float distance) =>
        Mathf.Max(0, quarter) * Data.levels.stagePerQuarter + Mathf.RoundToInt(Mathf.Clamp01(distance) * Data.levels.distanceStages);

    /// Levels added to the Pokémon there.
    public static int LevelBonus(int quarter, float distance)
    {
        var q = Data.levels.quarterBonus;
        int bonus = q.Length == 0 ? 0 : q[Mathf.Clamp(quarter, 0, q.Length - 1)];
        return bonus + Mathf.RoundToInt(Mathf.Clamp01(distance) * Data.levels.distanceBonus);
    }
}
