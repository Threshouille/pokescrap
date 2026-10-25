using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// The day and night cycle (Resources/Data/daynight.json; GDD, "Jour et nuit"), added by PlayerController: a clock
/// running with the game (stopped while paused), its phases in turn (dawn, day, dusk, night), the colour and intensity
/// of the scene's global light following them. IsNight drives the evolutions and seals of the night. H / Shift+H in
/// devMode: a minute later / the next phase.
public class DayNight : MonoBehaviour
{
    [Serializable]
    public class Phase
    {
        public string key, name, color = "#ffffff";
        public float minutes = 1f, intensity = 1f;
        public bool night;
        [NonSerialized] public Color colour;
    }

    [Serializable]
    class Settings
    {
        public bool randomStart = true;
        public float blend = 30f, clockStart = 5f;
        public Phase[] phases = new Phase[0];
    }

    static Settings settings;
    static float clock; // seconds since the start of the dawn

    Light2D sun;

    static Settings Data
    {
        get
        {
            if (settings != null) return settings;
            var asset = Resources.Load<TextAsset>("Data/daynight");
            settings = asset != null ? JsonUtility.FromJson<Settings>(asset.text) : new Settings();
            foreach (var p in settings.phases)
                if (!ColorUtility.TryParseHtmlString(p.color, out p.colour)) p.colour = Color.white;
            return settings;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        settings = null;
        clock = 0f;
    }

    /// Seconds in a whole day.
    public static float Length
    {
        get
        {
            float total = 0f;
            foreach (var p in Data.phases) total += Mathf.Max(0f, p.minutes) * 60f;
            return Mathf.Max(1f, total);
        }
    }

    /// The phase now, and how far into it (seconds).
    public static Phase Now(out float into)
    {
        into = Mathf.Repeat(clock, Length);
        var phases = Data.phases;
        for (int k = 0; k < phases.Length; k++)
        {
            float span = Mathf.Max(0f, phases[k].minutes) * 60f;
            if (into < span || k == phases.Length - 1) return phases[k];
            into -= span;
        }
        return null;
    }

    public static bool IsNight => Now(out _) is { night: true };

    /// How dark it is: 0 at the brightest phase's light, 1 at the darkest one (the point lights follow it).
    public static float Darkness
    {
        get
        {
            float low = float.MaxValue, high = float.MinValue;
            foreach (var p in Data.phases) { low = Mathf.Min(low, p.intensity); high = Mathf.Max(high, p.intensity); }
            if (high <= low) return 0f;
            Light(out _, out float intensity);
            return Mathf.Clamp01((high - intensity) / (high - low));
        }
    }

    /// The phase's name and the time of day (a day = 24 hours from clockStart at the dawn), for the HUD.
    public static string Label
    {
        get
        {
            var phase = Now(out _);
            float hours = Mathf.Repeat(Data.clockStart + Mathf.Repeat(clock, Length) / Length * 24f, 24f);
            int h = Mathf.FloorToInt(hours), m = Mathf.FloorToInt((hours - h) * 60f);
            return $"{phase?.name ?? "—"} {h:00}:{m:00}";
        }
    }

    /// The light of that moment: the phase's own, blending into the next one over its last `blend` seconds.
    static void Light(out Color colour, out float intensity)
    {
        colour = Color.white;
        intensity = 1f;
        var phases = Data.phases;
        if (phases.Length == 0) return;
        var phase = Now(out float into);
        int k = Array.IndexOf(phases, phase);
        var next = phases[(k + 1) % phases.Length];
        float span = Mathf.Max(0f, phase.minutes) * 60f;
        float blend = Mathf.Min(Data.blend, span / 2f);
        float t = blend > 0f ? Mathf.Clamp01((into - (span - blend)) / blend) : 0f;
        t = t * t * (3f - 2f * t);
        colour = Color.Lerp(phase.colour, next.colour, t);
        intensity = Mathf.Lerp(phase.intensity, next.intensity, t);
    }

    void Awake()
    {
        clock = Data.randomStart ? UnityEngine.Random.value * Length : 0f;
    }

    void Start()
    {
        foreach (var light in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (light.lightType == Light2D.LightType.Global) { sun = light; break; }
    }

    void FixedUpdate() => clock += Time.fixedDeltaTime;

    void Update()
    {
        if (SaveData.Settings.devMode && Keyboard.current is { } kb && kb.hKey.wasPressedThisFrame)
        {
            if (kb.shiftKey.isPressed)
            {
                Now(out float into);
                clock += Mathf.Max(0f, Now(out _).minutes) * 60f - into;
            }
            else clock += 60f;
        }
        if (sun == null) return;
        Light(out var colour, out float intensity);
        sun.color = colour;
        sun.intensity = intensity;
    }
}
