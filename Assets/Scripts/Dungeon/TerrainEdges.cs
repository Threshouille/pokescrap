using System.Collections.Generic;
using UnityEngine;

/// Cut-out borders of the terrain tiles: the sides of a cell that open onto the floor (or another terrain) are
/// trimmed in the style of the element (round water, spiky ice, jagged embers, thorny brambles, soft mud), so a
/// patch does not show the square corners of its cells. The trimmed frames are made once and kept.
public static class TerrainEdges
{
    public const int North = 1, East = 2, South = 4, West = 8; // sides of a cell open onto something else
    public const int Variations = 3; // patterns of the irregular styles, picked by cell so two edges differ

    const int Size = 24; // a tile, in pixels

    static readonly Dictionary<EffectLibrary.Sheet, Color32[]> sources = new();
    static readonly Dictionary<(EffectLibrary.Sheet, int, int, int), Sprite> cut = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        sources.Clear();
        cut.Clear();
    }

    /// Frame of a tile sheet with its open sides trimmed ("sides": North | East | South | West).
    public static Sprite Frame(EffectLibrary.Sheet sheet, int frame, int sides, TerrainDef def, int variation)
    {
        if (sides == 0 && !def.fills) return sheet.Frame(frame);
        var key = (sheet, frame, sides, variation);
        if (cut.TryGetValue(key, out var sprite) && sprite != null) return sprite;

        var pixels = Pixels(sheet);
        if (pixels == null) return sheet.Frame(frame);
        int width = sheet.texture.width;
        var result = new Color32[Size * Size];
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var c = pixels[y * width + frame * sheet.frame_width + x];
                if (c.a == 0 && def.fills) c = def.fill; // the floor no longer shows through the holes
                result[y * Size + x] = c.a > 0 && Keep(x, y, sides, def.edge, Mathf.Max(1f, def.edgeDepth), variation) ? c : default;
            }
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(result);
        texture.Apply();
        sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), EffectLibrary.PixelsPerUnit, 0, SpriteMeshType.FullRect);
        cut[key] = sprite;
        return sprite;
    }

    // The sheet's pixels, read back once through the GPU (the imported textures are not CPU-readable).
    static Color32[] Pixels(EffectLibrary.Sheet sheet)
    {
        if (sources.TryGetValue(sheet, out var known)) return known;
        var src = sheet.texture;
        if (src == null) return null;
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
            Object.Destroy(copy);
        }
        sources[sheet] = pixels;
        return pixels;
    }

    // Whether pixel (x, y) of the tile (y up) stays: far enough from every open side for the style's profile, and
    // inside the rounded or cut corner where two open sides meet.
    static bool Keep(int x, int y, int sides, string style, float depth, int variation)
    {
        if ((sides & North) != 0 && Size - 1 - y < Inset(style, x, depth, variation, 0)) return false;
        if ((sides & South) != 0 && y < Inset(style, x, depth, variation, 1)) return false;
        if ((sides & East) != 0 && Size - 1 - x < Inset(style, y, depth, variation, 2)) return false;
        if ((sides & West) != 0 && x < Inset(style, y, depth, variation, 3)) return false;
        return Corner(x, y, sides, North, East, Size - 1, Size - 1, style, depth)
            && Corner(x, y, sides, North, West, 0, Size - 1, style, depth)
            && Corner(x, y, sides, South, East, Size - 1, 0, style, depth)
            && Corner(x, y, sides, South, West, 0, 0, style, depth);
    }

    // How deep the border bites into the tile at position t along a side (pixels).
    static float Inset(string style, int t, float depth, int variation, int side)
    {
        int seed = variation * 4 + side;
        switch (style)
        {
            case "spiky": // ice shards: triangles pointing outward
            {
                float u = Mathf.Repeat(t + seed * 2f, 6f) / 6f;
                return 1f + (depth - 1f) * Mathf.Abs(u * 2f - 1f);
            }
            case "jagged": // embers, sparks: an irregular bite every two pixels
                return 1f + Hash(t / 2, seed) % Mathf.Max(1, Mathf.RoundToInt(depth));
            case "thorny": // brambles: a thick edge with thorns sticking out
            {
                int u = (t + seed * 3) % 5;
                return u == 0 ? 0f : u == 1 || u == 4 ? depth * 0.5f : depth - 1f;
            }
            case "blob": // mud, poison: soft bulges
                return 1.5f + (depth * 0.5f) * (0.5f + 0.5f * Mathf.Sin((t + seed * 5f) * Mathf.PI * 2f / 12f));
            default: // round: a thin clean edge, the corners do the rest
                return 1f;
        }
    }

    // Where two open sides meet: a quarter circle for the soft styles, a cut corner for the sharp ones.
    static bool Corner(int x, int y, int sides, int a, int b, int cx, int cy, string style, float depth)
    {
        if ((sides & a) == 0 || (sides & b) == 0) return true;
        int dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy); // distance from that corner of the tile
        if (style == "round" || style == "blob")
        {
            const float r = Size / 2f; // half a tile: a lone cell turns into a disc
            if (dx >= r || dy >= r) return true;
            return (r - dx) * (r - dx) + (r - dy) * (r - dy) <= r * r;
        }
        return dx + dy >= depth + 7f;
    }

    static int Hash(int a, int b)
    {
        unchecked
        {
            int h = a * 73856093 ^ b * 19349663;
            h ^= h >> 13;
            h *= 1274126177;
            return (h ^ (h >> 16)) & 0x7fffffff;
        }
    }
}
