using System;
using UnityEngine;

/// A black loading screen between two maps (island zones): fades to black, shows "Chargement…" and the name of what
/// loads, runs the loading once the screen is black (the game frozen meanwhile), then fades back in.
public class LoadingScreen : MonoBehaviour
{
    const float FadeTime = 0.3f, MinShown = 0.4f;

    static LoadingScreen instance;

    string title;
    Action load;
    float fadeOutAt, loadedAt = -1f, fadeInUntil;
    float previousTimeScale = 1f;
    bool loaded;

    /// True from the start of the fade out to the end of the fade in.
    public static bool Busy => instance != null && instance.load != null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    /// Starts a loading; ignored while another one runs.
    public static void Run(string title, Action load)
    {
        if (Busy || load == null) return;
        if (instance == null) instance = new GameObject("Loading screen").AddComponent<LoadingScreen>();
        instance.Begin(title, load);
    }

    void Begin(string text, Action action)
    {
        title = text;
        load = action;
        loaded = false;
        loadedAt = -1f;
        fadeOutAt = Time.unscaledTime;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (load == null) return;
        float now = Time.unscaledTime;
        // Black screen drawn for one frame at least before the (long) loading, so it shows during it.
        if (!loaded && now >= fadeOutAt + FadeTime && loadedAt < 0f)
        {
            loadedAt = now;
            return;
        }
        if (!loaded && loadedAt >= 0f)
        {
            loaded = true;
            try { load(); }
            catch (Exception e) { Debug.LogException(e); }
            loadedAt = Time.unscaledTime;
            fadeInUntil = Mathf.Max(loadedAt, fadeOutAt + FadeTime + MinShown) + FadeTime;
            return;
        }
        if (loaded && now >= fadeInUntil)
        {
            load = null;
            Time.timeScale = previousTimeScale;
        }
    }

    void OnGUI()
    {
        if (load == null) return;
        float now = Time.unscaledTime;
        float alpha = !loaded ? Mathf.Clamp01((now - fadeOutAt) / FadeTime)
            : now < fadeInUntil - FadeTime ? 1f : Mathf.Clamp01((fadeInUntil - now) / FadeTime);
        GUI.depth = -100;
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        if (alpha > 0.95f)
        {
            float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
            var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(14 * s) };
            GUI.color = Color.white;
            GUI.Label(new Rect(0f, Screen.height / 2f - 30f * s, Screen.width, 24f * s), title, style);
            style.fontSize = Mathf.RoundToInt(9 * s);
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(0f, Screen.height / 2f, Screen.width, 18f * s), "Chargement…", style);
        }
        GUI.color = previous;
    }
}
