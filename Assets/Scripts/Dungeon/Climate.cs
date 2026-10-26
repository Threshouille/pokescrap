using System;
using System.Collections.Generic;
using UnityEngine;

/// Weathers (Zénith, Danse Pluie, Grêle, Tempêtesable) and "sports" (Tourniquet, Lance-Boue), cast by moves of
/// family "climate", from climates.json. A weather covers the whole arena for a while, for both teams: some
/// terrains are bigger and last longer (the ones on the ground included), some shorter; some move types hit harder
/// or softer; it may hurt everyone but some types every second; it scatters small traces now and then. A sport is
/// a zone around its caster where some terrains are wiped out and cannot be laid, and where some move types hurt
/// less. One weather at a time: the last one cast replaces the other.
public class Climate : MonoBehaviour
{
    [Serializable] public class TerrainMul { public string terrain; public float radiusMul = 1f, durationMul = 1f; }
    [Serializable] public class TypeMul { public int type; public float mul = 1f; }
    [Serializable] public class Chip { public float share; public int[] immuneTypes = new int[0]; }
    [Serializable] public class Scatter { public string terrain; public float chance, radius = 0.5f, duration = 3f; }
    [Serializable] public class TypeShot { public int type; public float mul = 1f; } // speed and range of shots of a type

    /// The wind of a storm: a direction drawn when it starts; a shot with it gains up to "strength" of its speed and
    /// range, against it loses as much (sideways: nothing). "sensitive" types feel it more, "heavy" ones not at all.
    [Serializable]
    public class Wind
    {
        public float strength;
        public TypeShot[] sensitive = new TypeShot[0];
        public int[] heavyTypes = new int[0];
        public int[] pushTypes = new int[0]; // Pokémon of these types are carried along by it (Vol)...
        public float push; // ... at this many tiles/s
    }

    [Serializable]
    public class Weather
    {
        public string key, name;
        public int move;
        public float duration = 20f;
        public string tint = "#ffffff";
        public float alpha = 0.12f;
        public TerrainMul[] terrains = new TerrainMul[0];
        public TypeMul[] typeDamage = new TypeMul[0];
        public Chip chip;
        public Scatter scatter;
        public TypeShot[] shots = new TypeShot[0];
        public Wind wind;
        public float scroll = 2f; // tiles/s the pattern of its veil drifts (with the wind, else straight down)
    }

    [Serializable]
    public class Sport
    {
        public string key, name;
        public int move;
        public float radius = 4f, duration = 15f;
        public string tint = "#ffffff";
        public string[] blocks = new string[0];
        public TypeMul[] typeDamage = new TypeMul[0];
    }

    [Serializable] class File { public Weather[] weathers; public Sport[] sports; }

    class Zone
    {
        public Sport def;
        public Vector2 centre;
        public float until;
        public SpriteRenderer ring;
    }

    static File data;
    static Climate current;

    Weather weather;
    float weatherUntil;
    Vector2 windDirection; // of the weather on now (zero: no wind)
    SpriteRenderer veil, pattern;
    readonly List<Zone> zones = new();
    float nextChip;
    GUIStyle label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        data = null;
        current = null;
    }

    static File Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/climates");
            data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            data.weathers ??= new Weather[0];
            data.sports ??= new Sport[0];
            return data;
        }
    }

    static Climate Current
    {
        get
        {
            if (current == null) current = new GameObject("Climat").AddComponent<Climate>();
            return current;
        }
    }

    /// The weather on now, or null.
    public static Weather Now => current != null && current.weather != null && Time.time < current.weatherUntil ? current.weather : null;

    /// Clears the weather and the sports (a new arena).
    public static void Clear()
    {
        if (current == null) return;
        current.weather = null;
        foreach (var z in current.zones)
            if (z.ring != null) Destroy(z.ring.gameObject);
        current.zones.Clear();
    }

    static Weather WeatherOf(MoveDef move) => move == null ? null : Array.Find(Data.weathers, w => w.move == move.id);
    static Sport SportOf(MoveDef move) => move == null ? null : Array.Find(Data.sports, s => s.move == move.id);

    /// A climate move is used: its weather starts (replacing the one on), or its sport's zone opens around the user.
    public static void Cast(MoveDef move, Combatant caster)
    {
        var w = WeatherOf(move);
        if (w != null)
        {
            Current.weather = w;
            Current.weatherUntil = Time.time + w.duration;
            // A storm's wind blows one way for the whole weather: one of the 8 directions.
            float angle = UnityEngine.Random.Range(0, 8) * 45f * Mathf.Deg2Rad;
            Current.windDirection = w.wind != null && w.wind.strength > 0f ? new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) : Vector2.zero;
            foreach (var t in w.terrains) Terrain.ScaleRemaining(t.terrain, t.durationMul); // what lies there already
            return;
        }
        var s = SportOf(move);
        if (s == null) return;
        var zone = new Zone { def = s, centre = caster.transform.position, until = Time.time + s.duration };
        var go = new GameObject(s.name);
        go.transform.SetParent(Current.transform, false);
        go.transform.position = zone.centre;
        go.transform.localScale = Vector3.one * s.radius * 2f;
        zone.ring = go.AddComponent<SpriteRenderer>();
        zone.ring.sortingOrder = 0; // on the ground, under the Pokémon
        var sheet = EffectLibrary.Get("sport_" + s.key); // its own look, stretched over the zone
        if (sheet != null)
        {
            zone.ring.sprite = sheet.Frame(0);
            go.transform.localScale = Vector3.one * s.radius * 2f / (sheet.frame_width / (float)EffectLibrary.PixelsPerUnit);
        }
        else
        {
            zone.ring.sprite = Shapes.Circle;
            var tint = ColorUtility.TryParseHtmlString(s.tint, out var c) ? c : Color.white;
            tint.a = 0.18f;
            zone.ring.color = tint;
        }
        Current.zones.Add(zone);
        Current.Wipe(zone);
    }

    // ---- Effects on terrain and damage ------------------------------------------------------------------------

    /// Size multiplier of a trace of that terrain under the weather.
    public static float RadiusMul(string terrain) => Now is { } w && Array.Find(w.terrains, t => t.terrain == terrain) is { } m ? m.radiusMul : 1f;

    /// Duration multiplier of a trace of that terrain under the weather.
    public static float DurationMul(string terrain) => Now is { } w && Array.Find(w.terrains, t => t.terrain == terrain) is { } m ? m.durationMul : 1f;

    /// A sport zone forbids that terrain at that point.
    public static bool Blocks(string terrain, Vector2 point)
    {
        if (current == null) return false;
        foreach (var z in current.zones)
            if (Time.time < z.until && Array.IndexOf(z.def.blocks, terrain) >= 0 && Vector2.Distance(point, z.centre) <= z.def.radius) return true;
        return false;
    }

    /// Damage multiplier of a move of that type on a Pokémon standing there (weather, and sports around it).
    public static float DamageMul(int moveType, Vector2 target)
    {
        float mul = 1f;
        if (Now is { } w)
            foreach (var t in w.typeDamage)
                if (t.type == moveType) mul *= t.mul;
        if (current != null)
            foreach (var z in current.zones)
                if (Time.time < z.until && Vector2.Distance(target, z.centre) <= z.def.radius)
                    foreach (var t in z.def.typeDamage)
                        if (t.type == moveType) mul *= t.mul;
        return mul;
    }

    /// Speed and range multiplier of a shot of that move fired that way: the weather (Fire shots fall short in the
    /// rain...) and the wind of a storm (with it, faster and further; against it, slower and shorter).
    public static float ShotMul(MoveDef move, Vector2 direction)
    {
        var w = Now;
        if (w == null || move == null) return 1f;
        float mul = 1f;
        foreach (var s in w.shots)
            if (s.type == move.type) mul *= s.mul;
        if (w.wind != null && current.windDirection != Vector2.zero && Array.IndexOf(w.wind.heavyTypes, move.type) < 0)
        {
            float feel = 1f;
            foreach (var s in w.wind.sensitive)
                if (s.type == move.type) feel *= s.mul;
            float along = direction == Vector2.zero ? 0f : Vector2.Dot(direction.normalized, current.windDirection);
            mul *= 1f + w.wind.strength * feel * along;
        }
        return Mathf.Clamp(mul, 0.3f, 2f);
    }

    /// How the wind of a storm carries this Pokémon (tiles/s), for the types it pushes (Vol); zero otherwise.
    public static Vector2 WindPush(Combatant c)
    {
        var w = Now;
        if (w?.wind == null || w.wind.push <= 0f || current.windDirection == Vector2.zero || c.Stats?.types == null) return Vector2.zero;
        foreach (int type in c.Stats.types)
            if (Array.IndexOf(w.wind.pushTypes, type) >= 0) return current.windDirection * w.wind.push;
        return Vector2.zero;
    }

    /// Direction of the wind blowing now (zero: none).
    public static Vector2 WindNow => Now != null ? current.windDirection : Vector2.zero;

    // ---- AI ---------------------------------------------------------------------------------------------------

    /// Worth casting for this Pokémon now: a weather that helps its team's types more than the opponents' (and is
    /// not already on), a sport against opponents of the type it weakens that are close.
    public static bool Worth(Combatant self, MoveDef move)
    {
        var w = WeatherOf(move);
        if (w != null)
        {
            if (Now == w) return false;
            float score = 0f;
            foreach (var c in Combatant.All)
            {
                if (c == null || !c.IsAlive || c.Stats?.types == null) continue;
                float side = c.team == self.team ? 1f : -1f;
                foreach (var t in w.typeDamage)
                    if (Array.IndexOf(c.Stats.types, t.type) >= 0) score += side * (t.mul - 1f);
                if (w.chip != null && w.chip.share > 0f && !HasType(c, w.chip.immuneTypes)) score -= side * 0.3f;
            }
            // A weather that hurts (hail, sandstorm) only when the opponents clearly lose more than its team.
            return score > (w.chip != null && w.chip.share > 0f ? 0.6f : 0.25f);
        }
        var s = SportOf(move);
        if (s == null) return false;
        foreach (var c in Combatant.All)
        {
            if (c == null || !c.IsAlive || c.team == self.team) continue;
            if (Vector2.Distance(c.transform.position, self.transform.position) > s.radius + 2f) continue;
            foreach (var t in s.typeDamage)
                if (t.mul < 1f && HasType(c, new[] { t.type })) return true;
        }
        return false;
    }

    static bool HasType(Combatant c, int[] types)
    {
        foreach (int type in c.Stats?.types ?? new int[0])
            if (Array.IndexOf(types, type) >= 0) return true;
        return false;
    }

    // ---- Every frame ------------------------------------------------------------------------------------------

    // Terrains a sport forbids are wiped out inside its zone.
    void Wipe(Zone z)
    {
        foreach (string key in z.def.blocks) Terrain.ClearArea(key, z.centre, z.def.radius);
    }

    void Update()
    {
        // Animated sport zones.
        foreach (var z in zones)
            if (z.ring != null && EffectLibrary.Get("sport_" + z.def.key) is { frame_count: > 1 } sheet)
                z.ring.sprite = sheet.Frame(Obstacles.FrameOf(sheet));
    }

    void FixedUpdate()
    {
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var z = zones[i];
            if (Time.time >= z.until)
            {
                if (z.ring != null) Destroy(z.ring.gameObject);
                zones.RemoveAt(i);
            }
            else Wipe(z);
        }
        var w = Now;
        if (w == null) return;
        // A small trace now and then, somewhere on the open floor.
        if (w.scatter != null && w.scatter.chance > 0f && UnityEngine.Random.value < w.scatter.chance * Time.fixedDeltaTime)
        {
            var arena = DungeonGenerator.Current;
            if (arena != null)
            {
                var spot = arena.RandomOpenPosition(new System.Random(UnityEngine.Random.Range(0, int.MaxValue)), 0f);
                Terrain.PaintArea(w.scatter.terrain, spot, w.scatter.radius, w.scatter.duration);
            }
        }
        // Damage every second to all but the immune types.
        if (w.chip != null && w.chip.share > 0f && Time.time >= nextChip)
        {
            nextChip = Time.time + 1f;
            foreach (var c in new List<Combatant>(Combatant.All)) // a copy: the damage may knock one out
                if (c != null && c.IsAlive && !HasType(c, w.chip.immuneTypes)) c.TakeFixedDamage(c.maxHp * w.chip.share);
        }
    }

    // A coloured veil over the arena, and the weather's name and time left.
    void LateUpdate()
    {
        var w = Now;
        var cam = Camera.main;
        if (veil == null && cam != null)
        {
            var go = new GameObject("Voile météo");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            go.transform.localScale = new Vector3(200f, 200f, 1f);
            veil = go.AddComponent<SpriteRenderer>();
            veil.sprite = Shapes.Square;
            veil.sortingOrder = 2; // over the floor and the Pokémon, like the night
        }
        if (veil == null) return;
        veil.enabled = w != null;
        if (pattern != null) pattern.enabled = false;
        if (w == null) return;
        var tint = ColorUtility.TryParseHtmlString(w.tint, out var c) ? c : Color.white;
        tint.a = w.alpha;
        veil.color = tint;
        // The weather's own pattern (weather_<key>: rain, hail, sand, sun rays), tiled over the screen and drifting
        // with the wind (straight down without wind).
        var sheet = EffectLibrary.Get("weather_" + w.key);
        if (sheet == null || cam == null) return;
        if (pattern == null)
        {
            var go = new GameObject("Météo");
            go.transform.SetParent(cam.transform, false);
            pattern = go.AddComponent<SpriteRenderer>();
            pattern.drawMode = SpriteDrawMode.Tiled;
            pattern.sortingOrder = 3; // over the veil
        }
        pattern.enabled = true;
        pattern.sprite = sheet.Frame(Obstacles.FrameOf(sheet));
        float tile = sheet.frame_width / (float)EffectLibrary.PixelsPerUnit;
        float height = cam.orthographicSize * 2f + 2f * tile, width = height * cam.aspect + 2f * tile;
        pattern.size = new Vector2(width + tile, height + tile);
        Vector2 drift = (windDirection != Vector2.zero ? windDirection : Vector2.down) * w.scroll * Time.time;
        pattern.transform.localPosition = new Vector3(Mathf.Repeat(drift.x, tile) - tile / 2f, Mathf.Repeat(drift.y, tile) - tile / 2f, 1f);
    }

    static readonly string[] Arrows = { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" };

    void OnGUI()
    {
        var w = Now;
        if (w == null) return;
        float s = HudLayout.Scale;
        label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        label.fontSize = Mathf.RoundToInt(8f * s);
        string wind = "";
        if (windDirection != Vector2.zero)
        {
            int octant = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(windDirection.y, windDirection.x) * Mathf.Rad2Deg, 360f) / 45f) % 8;
            wind = $"  vent {Arrows[octant]}";
        }
        var rect = new Rect(0f, 4f * s, Screen.width - 8f * s, 12f * s);
        GUI.Label(rect, $"{w.name} — {Mathf.CeilToInt(weatherUntil - Time.time)} s{wind}", label);
        // Its icon (weather_icon_<key>), left of the text.
        var icon = EffectLibrary.Get("weather_icon_" + w.key);
        if (icon?.texture != null)
        {
            float size = 12f * s, textWidth = label.CalcSize(new GUIContent($"{w.name} — 00 s{wind}")).x;
            var tex = icon.texture;
            GUI.DrawTextureWithTexCoords(new Rect(rect.xMax - textWidth - size - 4f * s, rect.y, size, size), tex,
                new Rect(0f, 0f, icon.frame_width / (float)tex.width, icon.frame_height / (float)tex.height));
        }
    }
}
