using UnityEngine;

/// Shared layout of the top of the in-run HUD, so its pieces never overlap: the stage box top left, the lots panel top
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
    static void ResetStatics() => SandboxLine = 0f;

    public static float Scale => Mathf.Max(1f, Mathf.Round(Screen.height / 360f));

    /// Room left between the stage box and the lots panel.
    public static float CenterWidth(float s) => Mathf.Max(160f * s, Screen.width - 2f * (Side + LotsWidth + Side) * s);

    public static float BannerTop(float s) => Top * s + (SandboxLine > 0f ? SandboxLine + 4f * s : 0f);

    /// Below the banners at their most (three of them).
    public static float BelowBanners(float s) => BannerTop(s) + 3f * (BannerHeight + BannerGap) * s + 2f * s;

    public static void Panel(Rect rect, Color fill, Color border, float s)
    {
        Fill(rect, fill);
        Fill(new Rect(rect.x, rect.y, rect.width, s), border);
        Fill(new Rect(rect.x, rect.yMax - s, rect.width, s), border);
        Fill(new Rect(rect.x, rect.y, s, rect.height), border);
        Fill(new Rect(rect.xMax - s, rect.y, s, rect.height), border);
    }

    public static void Panel(Rect rect, float s) => Panel(rect, PanelColour, BorderColour, s);

    public static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color * previous;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
