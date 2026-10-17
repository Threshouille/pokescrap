using System;
using System.Collections.Generic;
using UnityEngine;

public enum Efficacy { Normal, Super, NotVery, None }

/// Type matchups read from Resources/Data/type_chart.json (generation 4 chart, ROM type ids, written by
/// Tools/pmd/extract_type_chart.py). The chart's ×2 / ×0.5 / ×0 become the softer multipliers of meta.json.
public static class TypeChart
{
    [Serializable] class Matchup { public int attack, defend; public float factor; }
    [Serializable] class File { public Matchup[] matchups; }

    static Dictionary<(int, int), float> factors;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => factors = null;

    /// Damage multiplier of a move type against a Pokémon's types, and how the hit should read.
    public static float Multiplier(int moveType, int[] defenderTypes, out Efficacy efficacy)
    {
        Load();
        var settings = SaveData.Settings;
        // A weakness and a resistance cancel out (as ×2 × ×0.5 does), even though the softer multipliers do not.
        int net = 0;
        bool immune = false;
        if (defenderTypes != null)
            foreach (int type in defenderTypes)
            {
                if (!factors.TryGetValue((moveType, type), out float factor)) continue;
                if (factor <= 0f) immune = true;
                else net += factor > 1f ? 1 : -1;
            }
        float result = net >= 0 ? Mathf.Pow(settings.superEffective, net) : Mathf.Pow(settings.notVeryEffective, -net);
        if (immune) result *= settings.noEffect;
        result = Mathf.Clamp(result, settings.typeFactorMin, settings.typeFactorMax);
        efficacy = immune ? Efficacy.None : net > 0 ? Efficacy.Super : net < 0 ? Efficacy.NotVery : Efficacy.Normal;
        return result;
    }

    static void Load()
    {
        if (factors != null) return;
        factors = new Dictionary<(int, int), float>();
        var asset = Resources.Load<TextAsset>("Data/type_chart");
        if (asset == null) return;
        foreach (var m in JsonUtility.FromJson<File>(asset.text).matchups) factors[(m.attack, m.defend)] = m.factor;
    }
}
