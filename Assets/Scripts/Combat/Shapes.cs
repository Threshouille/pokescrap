using System.Collections.Generic;
using UnityEngine;

/// Plain shapes generated in code, used as placeholders until real effects exist. Both are 1 unit wide.
public static class Shapes
{
    const int CircleSize = 32;

    static Sprite circle, square, arrow;
    static readonly Dictionary<string, Sprite> icons = new();

    // Fallback icon (original drawing) for a status without its own sheet, in the status tint.
    static readonly string[] DropPixels =
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
    /// Without a sheet: an arrow for stat changes, a drop in the status tint otherwise.
    public static Sprite StatusIcon(string status, Color? tint = null)
    {
        var sheet = EffectLibrary.Get("status_" + status);
        if (sheet != null) return sheet.Frame(0);
        if (icons.TryGetValue(status, out var cached) && cached != null) return cached;
        bool up = status.EndsWith("_up"), down = status.EndsWith("_down");
        var pixels = up || down ? ArrowPixels : DropPixels;
        Color32 outline = new(30, 30, 40, 255);
        // Provisional: an arrow in the colour of the stat (red attack, blue defence, green speed).
        Color32 fill = !up && !down ? (Color32)(tint ?? Color.white)
            : status.StartsWith("attack") ? new Color32(235, 80, 60, 255)
            : status.StartsWith("defense") ? new Color32(80, 140, 240, 255)
            : new Color32(90, 210, 90, 255);
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

    static readonly string[][] Digits =
    {
        new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
        new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
        new[] { ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###." },
        new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
    };
    static readonly Sprite[] bubbles = new Sprite[4];

    /// Speech bubble showing a number from 1 to 4 (a boss announcing which of its moves comes next). 1 unit wide.
    public static Sprite NumberBubble(int number)
    {
        int index = Mathf.Clamp(number, 1, 4) - 1;
        if (bubbles[index] != null) return bubbles[index];
        const int size = 16;
        Color32 ink = new(30, 30, 40, 255), paper = new(255, 255, 255, 255), none = new(0, 0, 0, 0);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                // Rounded box on rows 4-15, with a small tail underneath pointing at the speaker.
                bool corner = (x == 1 || x == 14) && (y == 4 || y == 15);
                bool box = x >= 1 && x <= 14 && y >= 4 && y <= 15 && !corner;
                bool edge = box && (x == 1 || x == 14 || y == 4 || y == 15
                    || ((x == 2 || x == 13) && (y == 5 || y == 14)));
                bool tail = y >= 1 && y <= 3 && Mathf.Abs(x - 7.5f) <= (y - 0.5f) * 0.75f;
                bool tailEdge = tail && (y == 1 || Mathf.Abs(x - 7.5f) > (y - 1.5f) * 0.75f);
                tex.SetPixel(x, y, edge || tailEdge ? ink : box || tail ? paper : none);
            }
        var digit = Digits[index];
        for (int row = 0; row < digit.Length; row++)
            for (int col = 0; col < digit[row].Length; col++)
                if (digit[row][col] == '#') tex.SetPixel(5 + col, 13 - row, ink);
        tex.Apply();
        return bubbles[index] = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// Flat disc on the floor that fades out over "duration" seconds (e.g. the puddle left where an arc shot lands).
    /// With a sprite instead (e.g. cracks on the ground), "fill" is the share of its width that the drawing covers,
    /// so the drawing matches the radius.
    public static void Puddle(Vector3 position, float radius, Color color, float duration, Sprite sprite = null, float fill = 1f)
    {
        var go = new GameObject("Puddle");
        go.transform.position = position;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null ? sprite : Circle;
        go.transform.localScale = Vector3.one * radius * 2f / (renderer.sprite.bounds.size.x * (fill > 0f ? fill : 1f));
        color.a = sprite != null ? 1f : 0.45f;
        renderer.color = sprite != null ? Color.white : color;
        renderer.sortingOrder = 0; // above the floor, under the Pokémon
        go.AddComponent<FadeOut>().duration = duration;
    }

    /// Brief translucent disc: provisional feedback for moves that have no sprite sheet (heals, bonuses, penalties).
    public static void Pulse(Vector3 position, float radius, Color color)
    {
        var go = new GameObject("Pulse");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * radius * 2f;
        var renderer = go.AddComponent<SpriteRenderer>();
        Unlit.Apply(renderer); // keeps its colours at night (DayNight)
        renderer.sprite = Circle;
        color.a = 0.35f;
        renderer.color = color;
        renderer.sortingOrder = 0; // above the floor, under the Pokémon
        Object.Destroy(go, 0.35f);
    }

    /// Faint ring under "parent" marking the edge of a zone (removed with it); its scale ignores the parent's.
    public static void ZoneRing(Transform parent, float radius, Color color)
    {
        var go = new GameObject("Bord de zone");
        go.transform.SetParent(parent, false);
        Vector3 parentScale = parent.lossyScale;
        go.transform.localScale = new Vector3(radius * 2f / Mathf.Max(0.001f, parentScale.x), radius * 2f / Mathf.Max(0.001f, parentScale.y), 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = ThinRing;
        renderer.color = color;
        renderer.sortingOrder = 0; // on the ground, under the Pokémon
        Unlit.Apply(renderer);
    }

    /// Ring on the ground showing the reach of an area move, fading out over "duration" seconds.
    public static void AreaRing(Vector3 position, float radius, Color color, float duration)
    {
        var go = new GameObject("AreaRing");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * radius * 2f;
        var renderer = go.AddComponent<SpriteRenderer>();
        Unlit.Apply(renderer); // keeps its colours at night (DayNight)
        renderer.sprite = ThinRing;
        color.a = 0.7f;
        renderer.color = color;
        renderer.sortingOrder = 0; // on the ground, under the Pokémon
        go.AddComponent<FadeOut>().duration = duration;
    }

    // Large ring with a fine line, for areas several tiles wide.
    static Sprite ThinRing
    {
        get
        {
            if (thinRing != null) return thinRing;
            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float r = size / 2f;
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, d <= r && d >= r - 2f ? Color.white : Color.clear);
                }
            tex.Apply();
            return thinRing = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
    static Sprite thinRing;

    /// Thin ring (target marker), white to be tinted.
    public static Sprite Ring
    {
        get
        {
            if (ring != null) return ring;
            const int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float r = size / 2f;
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    tex.SetPixel(x, y, d <= r && d >= r - 3f ? Color.white : Color.clear);
                }
            tex.Apply();
            return ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
    static Sprite ring;

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

/// Fades a sprite out over its duration, then removes it (purely visual).
public class FadeOut : MonoBehaviour
{
    public float duration = 1f;
    SpriteRenderer spriteRenderer;
    float startAlpha, elapsed;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        startAlpha = spriteRenderer.color.a;
    }

    void Update()
    {
        elapsed += Time.deltaTime;
        var c = spriteRenderer.color;
        c.a = startAlpha * (1f - Mathf.Clamp01(elapsed / duration));
        spriteRenderer.color = c;
        if (elapsed >= duration) Destroy(gameObject);
    }
}
