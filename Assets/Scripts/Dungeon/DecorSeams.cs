using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Soft seams between two ROM décors laid side by side (a dungeon level's sub-themes, DungeonGenerator.ZoneDecors):
/// the ROM has no tile joining one décor to another, so a tile on the seam (floor or wall alike) is remade: its own
/// chunk, cut back from the sides (and corners) that touch the other décor along a wavering, rounded line, the other
/// décor's chunk for the same tile showing through where it is cut. Only one of the two sides is cut (Bites), so the other's
/// floor seems to spill over in curves instead of meeting it along the tiles' square edges. Made once per (décor,
/// chunk, other décor, sides, corners, pattern) and kept.
public static class DecorSeams
{
    public const int North = 1, East = 2, South = 4, West = 8; // sides touching the other décor
    public const int NorthEast = 1, SouthEast = 2, SouthWest = 4, NorthWest = 8; // corners alone touching it
    public const int Patterns = 3;

    const float Depth = 8f, Wave = 6f, Round = 5f; // pixels of a 24-pixel tile

    static readonly Dictionary<Texture2D, Color32[]> sources = new();
    static readonly Dictionary<(DungeonTileset, int, DungeonTileset, int, int, int, int, bool), Tile> made = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        sources.Clear();
        made.Clear();
    }

    /// Whether a tile of décor "a" is the one cut where it meets "b" (the higher ROM number; never the same décor).
    public static bool Bites(DungeonTileset a, DungeonTileset b) =>
        a != null && b != null && a != b && (a.romId != b.romId ? a.romId > b.romId : string.CompareOrdinal(a.name, b.name) > 0);

    /// How far in (pixels) the cut runs at "t" (0-23) along a side, for a pattern: the same depth at both ends, so
    /// that the cuts of two tiles side by side join; a wave in between. Pure, for the tests.
    public static float Inset(float t, int pattern)
    {
        float u = Mathf.Clamp01(t / 23f);
        float bump = Mathf.Sin(u * Mathf.PI); // 0 at both ends
        float wave = Mathf.Sin(u * Mathf.PI * (2 + pattern) + pattern * 1.7f);
        return Depth + bump * Wave * wave;
    }

    /// Whether pixel (x, y) (y up, 0-23) of the cut tile keeps its own décor. Pure, for the tests.
    public static bool Keeps(int x, int y, int sides, int corners, int pattern)
    {
        const int S = 24;
        float n = (sides & North) != 0 ? (S - 1 - y) - Inset(x, pattern) : 99f;
        float s = (sides & South) != 0 ? y - Inset(x, pattern + 1) : 99f;
        float e = (sides & East) != 0 ? (S - 1 - x) - Inset(y, pattern + 2) : 99f;
        float w = (sides & West) != 0 ? x - Inset(y, pattern) : 99f;
        if (n < 0f || s < 0f || e < 0f || w < 0f) return false;
        // Two cut sides meeting: a rounded corner, not a square one.
        foreach (var (a, b) in new[] { (n, e), (e, s), (s, w), (w, n) })
            if (a < Round && b < Round && (Round - a) * (Round - a) + (Round - b) * (Round - b) > Round * Round) return false;
        // A corner alone touching the other décor: a quarter disc bitten out of it.
        float bite = Depth + 1.5f;
        if ((corners & NorthEast) != 0 && Sq(S - 1 - x) + Sq(S - 1 - y) < bite * bite) return false;
        if ((corners & SouthEast) != 0 && Sq(S - 1 - x) + Sq(y) < bite * bite) return false;
        if ((corners & SouthWest) != 0 && Sq(x) + Sq(y) < bite * bite) return false;
        if ((corners & NorthWest) != 0 && Sq(x) + Sq(S - 1 - y) < bite * bite) return false;
        return true;
    }

    static float Sq(float v) => v * v;

    /// Kept, with a dithered fringe two pixels wide along the cut (one pixel in two goes to the other décor). Pure.
    public static bool Own(int x, int y, int sides, int corners, int pattern)
    {
        if (!Keeps(x, y, sides, corners, pattern)) return false;
        if ((x + y) % 2 != 0) return true;
        return Keeps(x + 2, y, sides, corners, pattern) && Keeps(x - 2, y, sides, corners, pattern)
            && Keeps(x, y + 2, sides, corners, pattern) && Keeps(x, y - 2, sides, corners, pattern);
    }

    /// The tile of décor "a" (its chunk) cut where it meets décor "b", whose chunk for the same tile ("under": the
    /// same shape, floor or wall, so that edges and wall borders carry on) shows through; "solid": a wall's collider.
    public static Tile Cut(DungeonTileset a, int chunk, DungeonTileset b, int under, int sides, int corners, int pattern, bool solid)
    {
        var key = (a, chunk, b, under, sides, corners, pattern, solid);
        if (made.TryGetValue(key, out var tile) && tile != null) return tile;
        var pa = Pixels(a.texture);
        var pb = Pixels(b.texture);
        if (pa == null || pb == null || a.chunkSize != 24 || b.chunkSize != 24) return null;
        var result = new Color32[24 * 24];
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 24; x++)
                result[y * 24 + x] = Own(x, y, sides, corners, pattern) ? Pixel(a, pa, chunk, x, y) : Pixel(b, pb, under, x, y);
        var texture = new Texture2D(24, 24, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(result);
        texture.Apply();
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Sprite.Create(texture, new Rect(0, 0, 24, 24), new Vector2(0.5f, 0.5f), 24, 0, SpriteMeshType.FullRect);
        tile.colliderType = solid ? UnityEngine.Tilemaps.Tile.ColliderType.Grid : UnityEngine.Tilemaps.Tile.ColliderType.None;
        made[key] = tile;
        return tile;
    }

    // Pixel (x, y) (y up) of a chunk, as GetTile cuts it out of the décor's texture.
    static Color32 Pixel(DungeonTileset d, Color32[] pixels, int chunk, int x, int y)
    {
        int cell = d.chunkSize + 2 * d.padding;
        int px = chunk % d.columns * cell + d.padding + x;
        int py = d.texture.height - (chunk / d.columns + 1) * cell + d.padding + y;
        return pixels[py * d.texture.width + px];
    }

    // A texture's pixels, read back once through the GPU when it is not CPU-readable.
    static Color32[] Pixels(Texture2D src)
    {
        if (src == null) return null;
        if (sources.TryGetValue(src, out var known)) return known;
        Color32[] pixels;
        if (src.isReadable) pixels = src.GetPixels32();
        else
        {
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var copy = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            copy.Apply();
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            pixels = copy.GetPixels32();
            if (Application.isPlaying) Object.Destroy(copy);
            else Object.DestroyImmediate(copy);
        }
        sources[src] = pixels;
        return pixels;
    }
}
