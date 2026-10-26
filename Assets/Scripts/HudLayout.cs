using UnityEngine;

/// Shared IMGUI toolkit of the screens (scale, boxes, panels, cached styles, colours) and layout of the top of the
/// in-run HUD, so its pieces never overlap: the stage box top left, the lots panel top
/// right (under the weather's name), the sandbox room line then the banners top centre, between the two.
public static class HudLayout
{
    public const float Top = 6f, Side = 8f, StageWidth = 130f, LotsWidth = 160f, BannerHeight = 14f, BannerGap = 3f;
    public static readonly Color PanelColour = new Color(0.07f, 0.08f, 0.11f, 0.88f);
    public static readonly Color BorderColour = new Color(1f, 1f, 1f, 0.18f);
    public static readonly Color Secondary = new Color(0.72f, 0.75f, 0.82f);
    public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);

    /// Height of the sandbox room line drawn last frame (0 outside the sandbox), the banners start below it.
    public static float SandboxLine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        SandboxLine = 0f;
        styles.Clear();
    }

    public static float Scale => Mathf.Max(1f, Mathf.Round(Screen.height / 360f));

    /// Room left between the stage box and the lots panel.
    public static float CenterWidth(float s) => Mathf.Max(160f * s, Screen.width - 2f * (Side + LotsWidth + Side) * s);

    // Reserved this frame or the last one by what draws there (IMGUI calls the screens in no set order).
    static float bossBar, rightColumn;
    static int bossBarFrame = -10, rightColumnFrame = -10;

    /// A boss bar takes this height at the top centre (EnemySpawner's stage boss, TimeGears' islet boss).
    public static void ReserveBossBar(float height)
    {
        bossBar = height;
        bossBarFrame = Time.frameCount;
    }

    /// The right column (the lots panel) ends this low: the island's mini-map goes below.
    public static void ReserveRightColumn(float bottom)
    {
        rightColumn = bottom;
        rightColumnFrame = Time.frameCount;
    }

    static float BossBarHeight => Time.frameCount - bossBarFrame <= 1 ? bossBar : 0f;

    /// Where a boss bar goes: top centre, below the sandbox room line.
    public static float BossBarTop(float s) => Top * s + (SandboxLine > 0f ? SandboxLine + 4f * s : 0f);

    /// The top of the right column: below the weather's name when there is a weather.
    public static float RightTop(float s) => (Climate.Now != null ? Top + 14f : Top) * s;

    /// Below the right column (the island's mini-map).
    public static float BelowRightColumn(float s) => Time.frameCount - rightColumnFrame <= 1 ? rightColumn + 4f * s : RightTop(s);

    public static float BannerTop(float s) => BossBarTop(s) + (BossBarHeight > 0f ? BossBarHeight + 6f * s : 0f);

    /// Below the banners at their most (three of them).
    public static float BelowBanners(float s) => BannerTop(s) + 3f * (BannerHeight + BannerGap) * s + 2f * s;

    /// A filled box with a one-pixel (× s) border, faded with the current GUI.color (banners fading out).
    public static void Panel(Rect rect, Color fill, Color border, float s)
    {
        Tinted(rect, fill);
        Tinted(new Rect(rect.x, rect.y, rect.width, s), border);
        Tinted(new Rect(rect.x, rect.yMax - s, rect.width, s), border);
        Tinted(new Rect(rect.x, rect.y, s, rect.height), border);
        Tinted(new Rect(rect.xMax - s, rect.y, s, rect.height), border);
    }

    public static void Panel(Rect rect, float s) => Panel(rect, PanelColour, BorderColour, s);

    /// A health bar's shown share, eased toward the real one: it fills up visibly (a boss's heal at its second phase)
    /// at "fill" bars a second, and drops quickly. Moves once a frame (OnGUI runs several times).
    public static float Ease(ref float shown, ref int frame, float target, float fill = 1f)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint || frame == Time.frameCount) return shown;
        frame = Time.frameCount;
        if (shown < 0f || target < shown - 0.25f) shown = target; // a new bar, or a big blow: at once
        else shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * (target > shown ? fill : 3f));
        return shown;
    }

    /// A plain box of that colour (the current GUI.color is ignored).
    public static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }

    /// A box of that colour, multiplied by the current GUI.color.
    public static void Tinted(Rect rect, Color color) => Fill(rect, color * GUI.color);

    /// A label style kept between frames (OnGUI runs several times a frame: no style built each time), at this
    /// size for the current scale.
    public static GUIStyle Style(string key, float size, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft,
        bool wrap = false, bool clip = false)
    {
        if (!styles.TryGetValue(key, out var style))
            styles[key] = style = new GUIStyle(GUI.skin.label)
            {
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal, alignment = anchor, wordWrap = wrap,
                clipping = clip ? TextClipping.Clip : TextClipping.Overflow,
            };
        style.fontSize = Mathf.RoundToInt(size * Scale);
        return style;
    }

    static readonly System.Collections.Generic.Dictionary<string, GUIStyle> styles = new();
}
