using System.Collections.Generic;
using UnityEngine;

/// Every dungeon tileset (décor) extracted from the ROM, by ROM number, filled by the "Pokescrap > Build Tilesets"
/// menu: the room generator picks a room's décor by number at run time (rooms.json).
public class TilesetRoster : ScriptableObject
{
    public const string ResourcePath = "Data/Tilesets";

    public List<DungeonTileset> tilesets = new();

    static TilesetRoster instance;
    static Dictionary<int, DungeonTileset> byId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        instance = null;
        byId = null;
    }

    /// The décor with that ROM number, or null when it was not extracted.
    public static DungeonTileset Get(int romId)
    {
        if (byId == null)
        {
            byId = new Dictionary<int, DungeonTileset>();
            if (instance == null) instance = Resources.Load<TilesetRoster>(ResourcePath);
            if (instance != null)
                foreach (var t in instance.tilesets)
                    if (t != null) byId[t.romId] = t;
        }
        return byId.TryGetValue(romId, out var found) ? found : null;
    }
}
