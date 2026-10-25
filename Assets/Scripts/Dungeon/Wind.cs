using System;
using System.Collections.Generic;
using UnityEngine;

/// Wind in the vegetation (Resources/Data/wind.json; GDD, "Jour et nuit"), added by PlayerController: gives the decor
/// elements of the story maps (IslandRun.PlaceProps) a material of the shader Pokescrap/Sprite-Lit-Wind with the
/// amplitude of their group, and sets the shader's global breeze each frame (stronger and leaning while a storm blows,
/// Climate.WindNow).
public class Wind : MonoBehaviour
{
    [Serializable] class Group { public string group; public float amplitude = 1f; }

    [Serializable]
    class Settings
    {
        public float calm = 0.8f, storm = 2f, lean = 0.8f, speed = 1.6f, max = 3f, @default = 1f;
        public Group[] groups = new Group[0];
    }

    [Serializable] class MapProp { public string key, group; }
    [Serializable] class MapPropFile { public MapProp[] props = new MapProp[0]; }

    static Settings data;
    static Dictionary<string, string> groupOf; // map_props.json: key → group
    static readonly Dictionary<float, Material> materials = new(); // by amplitude

    static readonly int Strength = Shader.PropertyToID("_WindStrength"), Speed = Shader.PropertyToID("_WindSpeed"),
        Lean = Shader.PropertyToID("_WindLean"), Max = Shader.PropertyToID("_WindMax"), Amp = Shader.PropertyToID("_WindAmp");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        data = null;
        groupOf = null;
        materials.Clear();
    }

    static Settings Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/wind");
            data = asset != null ? JsonUtility.FromJson<Settings>(asset.text) : new Settings();
            groupOf = new Dictionary<string, string>();
            var props = Resources.Load<TextAsset>("Data/map_props");
            if (props != null) foreach (var p in JsonUtility.FromJson<MapPropFile>(props.text).props) groupOf[p.key] = p.group;
            return data;
        }
    }

    /// The material swaying that decor element (map_props.json key), or null when it stays still.
    public static Material For(string key)
    {
        var d = Data;
        float amplitude = d.@default;
        if (groupOf.TryGetValue(key, out var group))
            foreach (var g in d.groups) if (g.group == group) { amplitude = g.amplitude; break; }
        if (amplitude <= 0f) return null;
        if (materials.TryGetValue(amplitude, out var found) && found != null) return found;
        var source = Resources.Load<Material>("Materials/SpriteWind");
        if (source == null) return null;
        found = new Material(source);
        found.SetFloat(Amp, amplitude);
        materials[amplitude] = found;
        return found;
    }

    void Update()
    {
        var d = Data;
        var wind = Climate.WindNow;
        bool storm = wind.sqrMagnitude > 0.0001f;
        Shader.SetGlobalFloat(Strength, storm ? d.storm : d.calm);
        Shader.SetGlobalFloat(Speed, storm ? d.speed * 1.8f : d.speed);
        Shader.SetGlobalFloat(Lean, storm ? Mathf.Sign(wind.x) * d.lean : 0f);
        Shader.SetGlobalFloat(Max, d.max);
    }
}
