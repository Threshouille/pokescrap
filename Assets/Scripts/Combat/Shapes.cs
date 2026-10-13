using System.Collections.Generic;
using UnityEngine;

/// Plain shapes generated in code, used as placeholders until real effects exist. Both are 1 unit wide.
public static class Shapes
{
    const int CircleSize = 32;

    static Sprite circle, square, arrow;
    static readonly Dictionary<string, Sprite> icons = new();

    // Provisional status icons (original drawings). Letters are looked up in each icon's palette.
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
    static readonly string[] ParalysisPixels =
    {
        "....ppp.",
        "...pPPp.",
        "..pPPp..",
        ".pPPPPpp",
        "ppPPPPp.",
        "..pPPp..",
        ".pPPp...",
        ".ppp....",
    };

    static readonly string[] ArrowPixels = // pointing up; flipped for penalties
    {
        "...pp...",
        "..pPPp..",
        ".pPPPPp.",
        "pPPPPPPp",
        "pppPPppp",
        "..pPPp..",
        "..pPPp..",
        "..pppp..",
    };

    /// Icon shown above a Pokémon for a status or a stat change (attack_up, defense_down...). A sheet named status_<name> in Resources/Effects takes precedence.
    public static Sprite StatusIcon(string status)
    {
        var sheet = EffectLibrary.Get("status_" + status);
        if (sheet != null) return sheet.Frame(0);
        if (icons.TryGetValue(status, out var cached) && cached != null) return cached;
        bool poison = status == "poison";
        bool up = status.EndsWith("_up"), down = status.EndsWith("_down");
        var pixels = poison ? PoisonPixels : up || down ? ArrowPixels : ParalysisPixels;
        Color32 outline = poison ? new Color32(74, 26, 107, 255) : new Color32(110, 80, 0, 255);
        Color32 fill = poison ? new Color32(176, 79, 216, 255) : new Color32(255, 222, 60, 255);
        if (up || down)
        {
            // Provisional: an arrow in the colour of the stat (red attack, blue defence, green speed).
            outline = new Color32(30, 30, 40, 255);
            fill = status.StartsWith("attack") ? new Color32(235, 80, 60, 255)
                : status.StartsWith("defense") ? new Color32(80, 140, 240, 255)
                : new Color32(90, 210, 90, 255);
        }
        int size = pixels.Length;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                char c = pixels[down ? y : size - 1 - y][x]; // rows are written top to bottom; penalties point down
                tex.SetPixel(x, y, c == 'p' ? outline : c == 'P' ? fill
                    : c == 'd' ? new Color32(26, 10, 38, 255) : new Color32(0, 0, 0, 0));
            }
        tex.Apply();
        return icons[status] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 24f);
    }

    /// Brief translucent disc: provisional feedback for moves that have no sprite sheet (heals, bonuses, penalties).
    public static void Pulse(Vector3 position, float radius, Color color)
    {
        var go = new GameObject("Pulse");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * radius * 2f;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Circle;
        color.a = 0.35f;
        renderer.color = color;
        renderer.sortingOrder = 0; // above the floor, under the Pokémon
        Object.Destroy(go, 0.35f);
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
