using System;
using System.Collections.Generic;
using UnityEngine;

/// The rooms a floor is made of (Assets/Resources/Data/rooms.json), in the spirit of the original game's dungeons:
/// - types: what the room is for (start, combat, monster house, challenge, rest, sub-boss, boss), with the sizes
///   it comes in, the themes it may take, and optionally a wall plan of its own or no obstacles at all;
/// - themes: lava, ice, water, forest, cave, desert, electric, void, ruins, maze, boss..., each with the ROM décors
///   it is drawn with (TilesetRoster), the obstacle its décor draws itself (liquid: lava for a volcano), and
///   variants: different rooms of the same theme (a lava lake, a lava flow with cooled-rock crossings, an archipelago).
/// Pick draws a type's room at random (theme, variant, size, décor), then the generator lays it out at random too.
public static class RoomCatalog
{
    [Serializable]
    public class Size
    {
        public string key;
        public int width = 24, height = 18;
    }

    [Serializable]
    public class Type
    {
        public string key, name;
        public string[] sizes = new string[0];
        public string[] themes = new string[0]; // empty: every theme open to all types
        public bool obstacles = true;
        public RoomLayout layout; // replaces the variant's wall plan when its style is set
        public int weight = 1;
    }

    [Serializable]
    public class Variant
    {
        public string key, name;
        public int weight = 1;
        public string[] sizes = new string[0]; // empty: any of the type's
        public RoomLayout layout;
        public Obstacles.Room obstacles;
    }

    [Serializable]
    public class Theme
    {
        public string key, name;
        public int weight = 1;
        public bool restricted; // only for the types that name it (boss arenas)
        public int[] tilesets = new int[0];
        public string liquid; // obstacle drawn with the décor's own secondary layer (lava, void); none when empty
        public int[] liquidDecors = new int[0]; // the décors whose secondary layer really is that liquid (empty: all)
        public Variant[] variants = new Variant[0];
    }

    [Serializable]
    class File
    {
        public Size[] sizes = new Size[0];
        public Type[] types = new Type[0];
        public Theme[] themes = new Theme[0];
    }

    static File file;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => file = null;

    static File Data
    {
        get
        {
            if (file != null) return file;
            var asset = Resources.Load<TextAsset>("Data/rooms");
            file = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return file;
        }
    }

    public static IReadOnlyList<Type> Types => Data.types;

    /// A room of that type (any type when null), drawn at random; null when the catalogue has none.
    /// themeKey / variantKey force a theme or a variant (tests).
    public static RoomInstance Pick(string typeKey, System.Random rng, string themeKey = null, string variantKey = null)
    {
        var types = new List<Type>();
        foreach (var t in Data.types)
            if (typeKey == null || t.key == typeKey) types.Add(t);
        var type = Weighted(types, t => t.weight, rng);
        if (type == null) return null;

        var themes = new List<Theme>();
        foreach (var th in Data.themes)
        {
            bool named = Array.IndexOf(type.themes, th.key) >= 0;
            bool open = type.themes.Length == 0 && !th.restricted;
            if ((named || open || th.key == themeKey) && th.variants.Length > 0 && HasDecor(th) && (themeKey == null || th.key == themeKey))
                themes.Add(th);
        }
        var theme = Weighted(themes, th => th.weight, rng);
        if (theme == null) return null;

        // Variants that come in one of the type's sizes.
        var variants = new List<Variant>();
        foreach (var v in theme.variants)
            if ((variantKey == null || v.key == variantKey) && (Sizes(type, v).Count > 0 || variantKey != null)) variants.Add(v);
        var variant = Weighted(variants, v => v.weight, rng) ?? theme.variants[rng.Next(theme.variants.Length)];
        if (Sizes(type, variant).Count == 0 && variant.sizes.Length > 0) type = TypeWithSize(variant) ?? type;
        var sizes = Sizes(type, variant);
        var size = sizes.Count > 0 ? sizes[rng.Next(sizes.Count)] : new Size();

        var decors = new List<int>();
        foreach (int id in theme.tilesets)
            if (TilesetRoster.Get(id) != null) decors.Add(id);

        int decor = decors[rng.Next(decors.Count)];
        Obstacles.Room obstacles = null;
        if (type.obstacles && variant.obstacles != null && variant.obstacles.pool != null && variant.obstacles.pool.Length > 0)
        {
            obstacles = JsonUtility.FromJson<Obstacles.Room>(JsonUtility.ToJson(variant.obstacles)); // a copy
            // Water and ice keep their standard look (readable on any floor); a volcano's lava or a rift's void are
            // drawn with the décor's own layer, when that layer really is it.
            obstacles.decorWater = false;
            bool own = !string.IsNullOrEmpty(theme.liquid) && (theme.liquidDecors.Length == 0 || Array.IndexOf(theme.liquidDecors, decor) >= 0);
            obstacles.decorKind = own ? theme.liquid : null;
        }
        return new RoomInstance
        {
            type = type,
            theme = theme,
            variant = variant,
            size = new Vector2Int(size.width, size.height),
            tileset = decor,
            layout = type.layout != null && !string.IsNullOrEmpty(type.layout.style) ? type.layout : variant.layout,
            obstacles = obstacles,
        };
    }

    static Type TypeWithSize(Variant variant)
    {
        foreach (var t in Data.types)
            if (Sizes(t, variant).Count > 0) return t;
        return null;
    }

    static bool HasDecor(Theme theme)
    {
        foreach (int id in theme.tilesets)
            if (TilesetRoster.Get(id) != null) return true;
        return false;
    }

    static List<Size> Sizes(Type type, Variant variant)
    {
        var result = new List<Size>();
        foreach (var s in Data.sizes)
            if (Array.IndexOf(type.sizes, s.key) >= 0 && (variant.sizes.Length == 0 || Array.IndexOf(variant.sizes, s.key) >= 0))
                result.Add(s);
        return result;
    }

    static T Weighted<T>(List<T> items, Func<T, int> weight, System.Random rng) where T : class
    {
        int total = 0;
        foreach (var i in items) total += Mathf.Max(0, weight(i));
        if (total <= 0) return items.Count > 0 ? items[rng.Next(items.Count)] : null;
        int roll = rng.Next(total);
        foreach (var i in items)
        {
            roll -= Mathf.Max(0, weight(i));
            if (roll < 0) return i;
        }
        return items[items.Count - 1];
    }
}

/// One room drawn from the catalogue, ready for DungeonGenerator.Generate.
public class RoomInstance
{
    public RoomCatalog.Type type;
    public RoomCatalog.Theme theme;
    public RoomCatalog.Variant variant;
    public Vector2Int size;
    public int tileset; // ROM décor number
    public RoomLayout layout;
    public Obstacles.Room obstacles; // null: none
    public int seed; // the same room is laid out the same way each time (0: anew)

    public string Describe() =>
        $"{type.name} — {theme.name} : {variant.name}   ({size.x} × {size.y}, décor {tileset}, plan : {(string.IsNullOrEmpty(layout?.style) ? "open" : layout.style)})";
}
