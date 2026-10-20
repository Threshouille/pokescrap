using System;
using System.Collections.Generic;
using UnityEngine;

/// Electric pylons (obstacle "pylon", in pairs): an arc runs between the two of a pair in cycles — rest, a
/// crackling warning, then the arc, which hurts, paralyses and stuns whoever it touches (Électrik and Sol are
/// immune, Vol take more). An Électrik move on a pylon overcharges it (the arc stays on), a Sol move grounds it (no
/// arc for a while). An arc over water or metal electrifies the whole of it. Settings: "arc" in obstacles.json.
public class Pylons : MonoBehaviour
{
    [Serializable]
    public class TypeMul { public int type; public float mul = 1f; }

    [Serializable]
    public class Conduct { public string on; public string terrain; }

    [Serializable]
    public class ArcSettings
    {
        public float rest = 3f, warn = 0.8f, active = 1f; // seconds of each phase
        public float width = 0.4f; // tiles either side of the line that it touches
        public float damageShare = 0.06f; // of max HP, once per arc
        public string status = "paralysis";
        public float statusDuration = 3f;
        public float stun = 0.3f;
        public int[] immuneTypes = { 5, 9 };
        public TypeMul[] typeDamage = new TypeMul[0];
        public int overchargeType = 5;
        public float overchargeTime = 4f;
        public int groundType = 9;
        public float groundTime = 6f;
        public Conduct[] conducts = new Conduct[0];
        public float hazardCost = 20f; // extra path cost of a tile under a live (or warning) arc, for the AIs
    }

    [Serializable] class File { public ArcSettings arc; }

    class Pair
    {
        public Vector2Int a, b;
        public Vector2 from, to;
        public float phaseAt; // start of the current cycle
        public float overchargedUntil, groundedUntil;
        public SpriteRenderer line;
        public readonly HashSet<Combatant> hit = new();
        public bool wasActive;
    }

    static ArcSettings settings;
    static Pylons current;
    readonly List<Pair> pairs = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        settings = null;
        current = null;
    }

    static ArcSettings Settings
    {
        get
        {
            if (settings != null) return settings;
            var asset = Resources.Load<TextAsset>("Data/obstacles");
            settings = asset != null ? JsonUtility.FromJson<File>(asset.text).arc : null;
            return settings ??= new ArcSettings();
        }
    }

    /// Forgets every pair (a new arena), keeping the component on the obstacles' object.
    public static void Clear(Obstacles host)
    {
        if (current == null) current = host.gameObject.GetComponent<Pylons>();
        if (current == null) current = host.gameObject.AddComponent<Pylons>();
        foreach (var p in current.pairs)
            if (p.line != null) Destroy(p.line.gameObject);
        current.pairs.Clear();
    }

    /// Two pylon tiles linked by an arc.
    public static void AddPair(Vector2Int a, Vector2Int b)
    {
        var grid = DungeonGenerator.Current;
        if (current == null || grid == null) return;
        var pair = new Pair { a = a, b = b, from = grid.CentreOf(a.x, a.y), to = grid.CentreOf(b.x, b.y), phaseAt = Time.time - UnityEngine.Random.value * Settings.rest };
        var go = new GameObject("Arc électrique");
        go.transform.SetParent(current.transform, false);
        pair.line = go.AddComponent<SpriteRenderer>();
        pair.line.sprite = Shapes.Square;
        pair.line.sortingOrder = 2; // over the Pokémon
        pair.line.enabled = false;
        current.pairs.Add(pair);
    }

    enum Phase { Rest, Warn, Active, Grounded }

    static Phase PhaseOf(Pair p)
    {
        var s = Settings;
        if (Time.time < p.groundedUntil) return Phase.Grounded;
        if (Time.time < p.overchargedUntil) return Phase.Active;
        float t = Mathf.Repeat(Time.time - p.phaseAt, s.rest + s.warn + s.active);
        return t < s.rest ? Phase.Rest : t < s.rest + s.warn ? Phase.Warn : Phase.Active;
    }

    /// A move acted on the pylon at (x, y): "overcharge" keeps its arc on, "ground" cuts it.
    public static void Act(int x, int y, string action)
    {
        if (current == null) return;
        var cell = new Vector2Int(x, y);
        foreach (var p in current.pairs)
        {
            if (p.a != cell && p.b != cell) continue;
            if (action == "overcharge") p.overchargedUntil = Time.time + Settings.overchargeTime;
            else if (action == "ground")
            {
                p.groundedUntil = Time.time + Settings.groundTime;
                p.overchargedUntil = 0f;
            }
        }
    }

    static bool Immune(Combatant c)
    {
        foreach (int type in c.Stats?.types ?? new int[0])
            if (Array.IndexOf(Settings.immuneTypes, type) >= 0) return true;
        return false;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    /// True when a point lies under an arc that is on or about to be (warning, or the last second of rest), for
    /// a Pokémon it would hurt: the AIs keep out.
    public static bool Hazard(Vector2 point, Combatant agent)
    {
        if (current == null || (agent != null && Immune(agent))) return false;
        var s = Settings;
        foreach (var p in current.pairs)
        {
            var phase = PhaseOf(p);
            float t = Mathf.Repeat(Time.time - p.phaseAt, s.rest + s.warn + s.active);
            bool soon = phase == Phase.Warn || phase == Phase.Active || (phase == Phase.Rest && t > s.rest - 1f);
            if (soon && DistanceToSegment(point, p.from, p.to) <= s.width + 0.3f) return true;
        }
        return false;
    }

    /// Extra path cost of a tile for an AI (under a dangerous arc).
    public static float NavCost(Vector2 point, Combatant agent) => Hazard(point, agent) ? Settings.hazardCost : 0f;

    /// Direction out from under a dangerous arc, zero when not under one.
    public static Vector2 EscapeDirection(Vector2 point, Combatant agent)
    {
        if (current == null || !Hazard(point, agent)) return Vector2.zero;
        foreach (var p in current.pairs)
        {
            Vector2 ab = p.to - p.from;
            if (DistanceToSegment(point, p.from, p.to) > Settings.width + 0.3f) continue;
            var side = new Vector2(-ab.y, ab.x).normalized;
            return Vector2.Dot(point - p.from, side) >= 0f ? side : -side;
        }
        return Vector2.zero;
    }

    void FixedUpdate()
    {
        var s = Settings;
        foreach (var p in pairs)
        {
            var phase = PhaseOf(p);
            bool active = phase == Phase.Active;
            if (active && !p.wasActive)
            {
                p.hit.Clear();
                ConductAlong(p);
            }
            p.wasActive = active;
            if (!active) continue;
            foreach (var c in Combatant.All)
            {
                if (c == null || !c.IsAlive || p.hit.Contains(c) || Immune(c)) continue;
                if (DistanceToSegment(c.transform.position, p.from, p.to) > s.width) continue;
                p.hit.Add(c); // once per arc
                float mul = 1f;
                foreach (var t in s.typeDamage)
                    if (Array.IndexOf(c.Stats?.types ?? new int[0], t.type) >= 0) mul *= t.mul;
                c.TakeFixedDamage(c.maxHp * s.damageShare * mul);
                if (!string.IsNullOrEmpty(s.status) && StatusLibrary.Get(s.status) != null) c.AddStatus(s.status, s.statusDuration);
                c.Stun(s.stun);
            }
        }
    }

    // The arc electrifies the water and metal it passes over, all of it.
    void ConductAlong(Pair p)
    {
        var s = Settings;
        var grid = DungeonGenerator.Current;
        if (grid == null || s.conducts.Length == 0) return;
        var done = new HashSet<string>();
        float length = Vector2.Distance(p.from, p.to);
        for (float d = 0f; d <= length; d += 0.5f)
        {
            var cell = grid.CellOf(Vector2.Lerp(p.from, p.to, length > 0f ? d / length : 0f));
            var under = Obstacles.At(cell.x, cell.y);
            if (under == null || done.Contains(under.key)) continue;
            foreach (var c in s.conducts)
                if (c.on == under.key)
                {
                    Terrain.PaintCells(Obstacles.FloodFrom(cell.x, cell.y), c.terrain, s.active + 2f, null);
                    done.Add(under.key);
                }
        }
    }

    // The arc: sheet obstacle_arc (on) or obstacle_arc_warn (warning) tiled along the line; without them, a thin
    // flickering line while warning and a thick jittering one while on. The pylons show obstacle_pylon_charged while
    // overcharged and obstacle_pylon_grounded while grounded.
    void LateUpdate()
    {
        var arcSheet = EffectLibrary.Get("obstacle_arc");
        var warnSheet = EffectLibrary.Get("obstacle_arc_warn");
        var charged = EffectLibrary.Get("obstacle_pylon_charged");
        var grounded = EffectLibrary.Get("obstacle_pylon_grounded");
        foreach (var p in pairs)
        {
            if (p.line == null) continue;
            var phase = PhaseOf(p);
            var look = phase == Phase.Grounded ? grounded : Time.time < p.overchargedUntil ? charged : null;
            Obstacles.OverrideSheet(p.a.x, p.a.y, look);
            Obstacles.OverrideSheet(p.b.x, p.b.y, look);
            bool warn = phase == Phase.Warn, on = phase == Phase.Active;
            p.line.enabled = warn || on;
            if (!p.line.enabled) continue;
            Vector2 ab = p.to - p.from;
            var sheet = on ? arcSheet : warnSheet;
            if (sheet != null)
            {
                p.line.drawMode = SpriteDrawMode.Tiled;
                p.line.sprite = sheet.Frame(Obstacles.FrameOf(sheet));
                p.line.color = Color.white;
                p.line.transform.position = (p.from + p.to) / 2f;
                p.line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ab.y, ab.x) * Mathf.Rad2Deg);
                p.line.transform.localScale = Vector3.one;
                p.line.size = new Vector2(ab.magnitude, sheet.frame_height / (float)EffectLibrary.PixelsPerUnit);
                continue;
            }
            p.line.drawMode = SpriteDrawMode.Simple;
            p.line.sprite = Shapes.Square;
            float width = on ? 0.22f + 0.1f * Mathf.PingPong(Time.time * 20f, 1f) : 0.06f;
            p.line.transform.position = (p.from + p.to) / 2f;
            p.line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ab.y, ab.x) * Mathf.Rad2Deg);
            p.line.transform.localScale = new Vector3(ab.magnitude, width, 1f);
            p.line.color = on ? Color.Lerp(new Color(1f, 0.95f, 0.5f), Color.white, Mathf.PingPong(Time.time * 12f, 1f))
                : new Color(1f, 0.9f, 0.3f, Mathf.PingPong(Time.time * 8f, 1f));
        }
    }
}
