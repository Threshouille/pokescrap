using System;
using System.Collections.Generic;
using UnityEngine;

public struct LevelGrowth
{
    public int hp, atk, spAtk, def, spDef;
}

/// Stat growth per level of every Pokémon, read from Resources/Data/levels_rt.bytes (see Tools/pmd/extract_levels.py).
public static class LevelData
{
    const int Levels = 100, RecordSize = 10;

    static byte[] data;
    static Dictionary<int, int> offsets;

    // Statics can outlive a play session in the editor: reload the file each time a game starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => data = null;

    static void Load()
    {
        if (data != null) return;
        var asset = Resources.Load<TextAsset>("Data/levels_rt");
        data = asset != null ? asset.bytes : BitConverter.GetBytes(0);
        offsets = new Dictionary<int, int>();
        int count = BitConverter.ToInt32(data, 0);
        for (int i = 0, position = 4; i < count; i++, position += 4 + Levels * RecordSize)
            offsets[BitConverter.ToInt32(data, position)] = position + 4;
    }

    /// What a Pokémon gains when it reaches the given level (2 to 100). Zero when unknown.
    public static LevelGrowth Growth(int pokemonId, int level)
    {
        Load();
        if (level < 1 || level > Levels || !offsets.TryGetValue(pokemonId, out int start)) return default;
        int p = start + (level - 1) * RecordSize;
        return new LevelGrowth
        {
            hp = BitConverter.ToUInt16(data, p + 4),
            atk = data[p + 6], spAtk = data[p + 7], def = data[p + 8], spDef = data[p + 9],
        };
    }
}
