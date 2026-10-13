using UnityEngine;

/// Plain shapes generated in code, used as placeholders until real effects exist. Both are 1 unit wide.
public static class Shapes
{
    const int CircleSize = 32;

    static Sprite circle, square, arrow, poison;

    // Provisional status icon (an original drawing): p = outline, P = fill, d = dark.
    static readonly string[] PoisonPixels =
    {
        "..pppp..",
        ".pPPPPp.",
        "pPdPPdPp",
        "pPdPPdPp",
        "pPPPPPPp",
        ".pPddPp.",
        "..pPPp..",
        "...pp...",
    };

    /// Icon shown above a Pokémon for a status. A sheet named status_<name> in Resources/Effects takes precedence.
    public static Sprite StatusIcon(string status)
    {
        var sheet = EffectLibrary.Get("status_" + status);
        if (sheet != null) return sheet.Frame(0);
        if (status != "poison") return null;
        if (poison != null) return poison;
        int size = PoisonPixels.Length;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                char c = PoisonPixels[size - 1 - y][x]; // rows are written top to bottom
                tex.SetPixel(x, y, c == 'p' ? new Color32(74, 26, 107, 255) : c == 'P' ? new Color32(176, 79, 216, 255)
                    : c == 'd' ? new Color32(26, 10, 38, 255) : new Color32(0, 0, 0, 0));
            }
        tex.Apply();
        return poison = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
    }

    // Runtime sprites are destroyed when play mode ends while statics can survive: hence the null checks.
    public static Sprite Circle
    {
        get
        {
            if (circle != null) return circle;
            var tex = new Texture2D(CircleSize, CircleSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float r = CircleSize / 2f;
            for (int x = 0; x < CircleSize; x++)
                for (int y = 0; y < CircleSize; y++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, d <= r ? Color.white : Color.clear);
                }
            tex.Apply();
            return circle = Sprite.Create(tex, new Rect(0, 0, CircleSize, CircleSize), new Vector2(0.5f, 0.5f), CircleSize);
        }
    }

    /// Triangle pointing right (+X).
    public static Sprite Arrow
    {
        get
        {
            if (arrow != null) return arrow;
            var tex = new Texture2D(CircleSize, CircleSize, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int x = 0; x < CircleSize; x++)
                for (int y = 0; y < CircleSize; y++)
                {
                    // Half-height shrinks linearly from the base (x = 0) to the tip (x = size).
                    float half = (CircleSize - x - 0.5f) / 2f;
                    tex.SetPixel(x, y, Mathf.Abs(y + 0.5f - CircleSize / 2f) <= half ? Color.white : Color.clear);
                }
            tex.Apply();
            return arrow = Sprite.Create(tex, new Rect(0, 0, CircleSize, CircleSize), new Vector2(0.5f, 0.5f), CircleSize);
        }
    }

    public static Sprite Square
    {
        get
        {
            if (square != null) return square;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return square = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
