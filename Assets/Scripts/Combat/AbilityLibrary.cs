using System;
using System.Collections.Generic;
using UnityEngine;

/// An ability (talent) of the main series, from Resources/Data/abilities_rt.json (extract_data.py). Data only for
/// now: no effect in game yet.
[Serializable]
public class AbilityDef
{
    public int id; // ROM id (1-123), or 1000 + PokéAPI id for an ability absent from the ROM
    public string name, nameEn;
    public string nameRom; // abridged name of the ROM when it differs
    public int generation; // generation that introduced it
    public bool rom; // present in Explorers of Sky
    public string descRom; // description of the ROM (Mystery Dungeon rules)
    public string desc; // official French description (latest games)
    public string effectEn; // short English effect (PokéAPI)
}

/// Ability catalogue, read once.
public static class AbilityLibrary
{
    [Serializable] class File { public AbilityDef[] abilities; }

    static List<AbilityDef> all;
    static Dictionary<int, AbilityDef> byId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { all = null; byId = null; }

    public static IReadOnlyList<AbilityDef> All
    {
        get
        {
            Load();
            return all;
        }
    }

    /// Null for an unknown id (0: none).
    public static AbilityDef Get(int id)
    {
        Load();
        return byId.TryGetValue(id, out var def) ? def : null;
    }

    static void Load()
    {
        if (all != null) return;
        all = new List<AbilityDef>();
        byId = new Dictionary<int, AbilityDef>();
        var asset = Resources.Load<TextAsset>("Data/abilities_rt");
        if (asset == null) return;
        foreach (var def in JsonUtility.FromJson<File>(asset.text).abilities)
        {
            all.Add(def);
            byId[def.id] = def;
        }
    }
}
