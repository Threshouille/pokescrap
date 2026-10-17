using System.Collections.Generic;
using UnityEngine;

/// A number that rises above a Pokémon and fades: damage taken (coloured by type efficacy or by the status that
/// dealt it) or health given back. Drawn with a small pixel font, outlined, so it reads on any floor.
public class DamageNumber : MonoBehaviour
{
    const float Life = 0.7f, Rise = 0.5f, StartHeight = 0.9f, Jitter = 0.25f, MultiplierHeight = 0.45f;
    const float GlyphSpacing = 4f / PmdAnimSet.PixelsPerUnit; // 3 px wide glyphs, 1 px apart (outlines overlap)
    const int SortingOrder = 20; // above icons and health bars

    // 3×5 glyphs, rows top to bottom.
    static readonly Dictionary<char, string[]> Glyphs = new()
    {
        { '0', new[] { "###", "#.#", "#.#", "#.#", "###" } },
        { '1', new[] { ".#.", "##.", ".#.", ".#.", "###" } },
        { '2', new[] { "###", "..#", "###", "#..", "###" } },
        { '3', new[] { "###", "..#", ".##", "..#", "###" } },
        { '4', new[] { "#.#", "#.#", "###", "..#", "..#" } },
        { '5', new[] { "###", "#..", "###", "..#", "###" } },
        { '6', new[] { "###", "#..", "###", "#.#", "###" } },
        { '7', new[] { "###", "..#", ".#.", ".#.", ".#." } },
        { '8', new[] { "###", "#.#", "###", "#.#", "###" } },
        { '9', new[] { "###", "#.#", "###", "..#", "###" } },
        { '+', new[] { "...", ".#.", "###", ".#.", "..." } },
        { 'x', new[] { "...", "#.#", ".#.", "#.#", "..." } },
        { '.', new[] { "...", "...", "...", "...", ".#." } },
    };
    static readonly Dictionary<char, Sprite> sprites = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => sprites.Clear();

    readonly List<SpriteRenderer> renderers = new();
    Vector3 origin;
    float age;
    Color color;

    /// Damage dealt by a move, coloured and sized by its type efficacy.
    public static void Damage(Vector3 at, float amount, Efficacy efficacy)
    {
        var settings = SaveData.Settings;
        switch (efficacy)
        {
            case Efficacy.Super: Spawn(at, amount, settings.superEffectiveColor, settings.bigNumberScale, ""); break;
            case Efficacy.NotVery: Spawn(at, amount, settings.notVeryEffectiveColor, settings.smallNumberScale, ""); break;
            case Efficacy.None: Spawn(at, amount, settings.noEffectColor, 1f, ""); break;
            default: Spawn(at, amount, settings.normalDamageColor, 1f, ""); break;
        }
    }

    /// Damage over time from a status, in the status colour.
    public static void Status(Vector3 at, float amount, Color statusColor) =>
        Spawn(at, amount, statusColor, SaveData.Settings.smallNumberScale, "");

    public static void Heal(Vector3 at, float amount) => Spawn(at, amount, SaveData.Settings.healColor, 1f, "+");

    static void Spawn(Vector3 at, float amount, string hex, float scale, string prefix)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        Spawn(at, amount, c, scale, prefix);
    }

    /// Damage multiplier of a chained strike (Taillade), above the damage numbers: "x1.5".
    public static void Multiplier(Vector3 at, float factor)
    {
        ColorUtility.TryParseHtmlString(SaveData.Settings.multiplierColor, out var c);
        Show(at + Vector3.up * MultiplierHeight, "x" + factor.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), c,
            SaveData.Settings.bigNumberScale, false);
    }

    static void Spawn(Vector3 at, float amount, Color color, float scale, string prefix) =>
        Show(at, prefix + Mathf.Max(1, Mathf.RoundToInt(amount)), color, scale, true);

    static void Show(Vector3 at, string text, Color color, float scale, bool jitter)
    {
        var go = new GameObject("DamageNumber");
        var number = go.AddComponent<DamageNumber>();
        number.color = color;
        number.origin = at + (jitter ? new Vector3(Random.Range(-Jitter, Jitter), StartHeight + Random.Range(0f, Jitter), 0f) : Vector3.up * StartHeight);
        go.transform.position = number.origin;
        go.transform.localScale = Vector3.one * scale;
        float left = -(text.Length - 1) * GlyphSpacing / 2f;
        for (int i = 0; i < text.Length; i++)
        {
            var glyph = new GameObject(text[i].ToString());
            glyph.transform.SetParent(go.transform, false);
            glyph.transform.localPosition = new Vector3(left + i * GlyphSpacing, 0f, 0f);
            var renderer = glyph.AddComponent<SpriteRenderer>();
            renderer.sprite = SpriteOf(text[i]);
            renderer.color = color;
            renderer.sortingOrder = SortingOrder;
            number.renderers.Add(renderer);
        }
    }

    void Update()
    {
        age += Time.deltaTime;
        float t = age / Life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = origin + Vector3.up * Rise * (1f - (1f - t) * (1f - t)); // fast start, slows down
        var c = color;
        c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
        foreach (var r in renderers) r.color = c;
    }

    // White glyph with a black outline: the renderer colour tints the glyph and leaves the outline black.
    static Sprite SpriteOf(char ch)
    {
        if (sprites.TryGetValue(ch, out var cached) && cached != null) return cached;
        var rows = Glyphs[ch];
        int w = rows[0].Length + 2, h = rows.Length + 2;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[w * h];
        bool On(int x, int y) => x >= 0 && y >= 0 && x < rows[0].Length && y < rows.Length && rows[rows.Length - 1 - y][x] == '#';
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool on = On(x - 1, y - 1), near = false;
                for (int dy = -1; dy <= 1 && !near; dy++)
                    for (int dx = -1; dx <= 1 && !near; dx++)
                        near = On(x - 1 + dx, y - 1 + dy);
                pixels[y * w + x] = on ? new Color32(255, 255, 255, 255) : near ? new Color32(16, 16, 24, 255) : new Color32(0, 0, 0, 0);
            }
        tex.SetPixels32(pixels);
        tex.Apply();
        return sprites[ch] = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), PmdAnimSet.PixelsPerUnit);
    }
}
