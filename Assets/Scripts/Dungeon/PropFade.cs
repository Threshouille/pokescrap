using System;
using System.Collections.Generic;
using UnityEngine;

/// The island's tall decor elements (trees, big bushes: IslandRun.PlaceProps) go see-through while they hide a
/// Pokémon of the team standing behind them, so that nobody of it is lost under a crown. Settings: "fade" in
/// Resources/Data/map_props.json. Visual only.
public class PropFade : MonoBehaviour
{
    [Serializable] class Settings { public float alpha = 0.4f, minHeight = 1.2f, time = 0.2f; }
    [Serializable] class File { public Settings fade = new(); }

    const float BodyHeight = 0.5f; // tiles above the feet: where a Pokémon's body is looked for

    static Settings settings;
    static Settings Fade
    {
        get
        {
            if (settings != null) return settings;
            var asset = Resources.Load<TextAsset>("Data/map_props");
            settings = asset != null ? JsonUtility.FromJson<File>(asset.text).fade : new Settings();
            return settings;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => settings = null;

    readonly List<SpriteRenderer> tall = new();
    PlayerController team;

    /// Watches that element if it is tall enough to hide a Pokémon.
    public void Add(SpriteRenderer prop)
    {
        if (prop.sprite != null && prop.sprite.bounds.size.y >= Fade.minHeight) tall.Add(prop);
    }

    void LateUpdate()
    {
        if (team == null) team = FindAnyObjectByType<PlayerController>();
        float step = Fade.time > 0f ? Time.deltaTime * (1f - Fade.alpha) / Fade.time : 1f;
        foreach (var r in tall)
        {
            if (r == null) continue;
            bool hides = false;
            if (team != null)
            {
                var box = r.bounds;
                float foot = r.transform.position.y;
                foreach (var m in team.Members)
                {
                    if (m == null || !m.IsAlive) continue;
                    Vector2 body = (Vector2)m.transform.position + Vector2.up * BodyHeight;
                    // Behind it (its feet above the element's), and within its picture.
                    if (m.transform.position.y > foot && box.Contains(new Vector3(body.x, body.y, box.center.z))) { hides = true; break; }
                }
            }
            var c = r.color;
            c.a = Mathf.MoveTowards(c.a, hides ? Fade.alpha : 1f, step);
            r.color = c;
        }
    }
}
