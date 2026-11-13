using System.Collections.Generic;
using UnityEngine;

/// How a Z move's strike is staged (GDD, "Objets spéciaux", mise en scène; items.json zCrystals[].show): the opponents
/// in view are all struck, as before, but each one when the type's show reaches it: a Z traced across the screen, a
/// tidal wave, a field spreading out, a dragon winding through them... Timing is pure (Delay, for the tests); the
/// visuals are ZChoreo's (Release at the release, Impact at each hit). Ultimates.StrikeZ applies the damage.
public static class ZStrike
{
    public enum Style
    {
        Rise, Fall, Bolt, Wave, Implode, Sweep, Shot, // the first, plain stagings
        ZDash, Sun, Tide, Vines, Chain, Freeze, Afterimage, Acid, Rift, Dive, Field, Web, Boulders, Hands, Serpent, BlackHole, Drill,
    }

    // The hits of a dodgeable strike (an enemy's) still to come: when each opponent is reached, and from where.
    static readonly Dictionary<Combatant, (float at, Vector2 from)> incoming = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => incoming.Clear();

    /// Notes when a dodgeable strike reaches that opponent ("time": Time.time of the hit).
    public static void Expect(Combatant foe, float time, Vector2 from) { if (foe != null) incoming[foe] = (time, from); }
    public static void Reached(Combatant foe) { if (foe != null) incoming.Remove(foe); }

    /// Seconds before a dodgeable Z strike reaches it (negative when none), and where it comes from.
    public static float IncomingIn(Combatant c, out Vector2 from)
    {
        from = Vector2.zero;
        if (c == null || !incoming.TryGetValue(c, out var hit)) return -1f;
        from = hit.from;
        return Mathf.Max(0f, hit.at - Time.time);
    }

    /// Whether a dodge "dodgedAt" (Time.time) spares from a hit at "hitAt": within "window" seconds before (pure).
    public static bool Dodged(float dodgedAt, float hitAt, float window) => hitAt - dodgedAt >= 0f && hitAt - dodgedAt <= window;

    public const int ShowOrder = 5; // above the Pokémon and the marks on the ground

    public static Style Parse(string style) => style switch
    {
        "fall" => Style.Fall,
        "bolt" => Style.Bolt,
        "wave" => Style.Wave,
        "implode" => Style.Implode,
        "sweep" => Style.Sweep,
        "shot" => Style.Shot,
        "zdash" => Style.ZDash,
        "sun" => Style.Sun,
        "tide" => Style.Tide,
        "vines" => Style.Vines,
        "chain" => Style.Chain,
        "freeze" => Style.Freeze,
        "afterimage" => Style.Afterimage,
        "acid" => Style.Acid,
        "rift" => Style.Rift,
        "dive" => Style.Dive,
        "field" => Style.Field,
        "web" => Style.Web,
        "boulders" => Style.Boulders,
        "hands" => Style.Hands,
        "serpent" => Style.Serpent,
        "blackhole" => Style.BlackHole,
        "drill" => Style.Drill,
        _ => Style.Rise,
    };

    /// The key of a style in items.json (its lower-case name).
    public static string Key(Style style) => style.ToString().ToLowerInvariant();

    /// Where the strike happens: the user, the screen (centre, half width and height, half diagonal), the opponents
    /// in view (nearest first) and what the paths through them are.
    public class Context
    {
        public Vector2 origin, centre, sweep;
        public float halfWidth = 10f, halfHeight = 6f;
        public List<Vector2> foes = new();
        public List<Vector2> zPath = new(); // the Z traced by a ZDash, from the user on
        public List<Vector2> serpent = new(); // the Serpent's path: the user, then each opponent in its visiting order
        public float[] serpentAt = new float[0]; // per opponent (index in foes): the length of the path up to it
        public Vector2 hole; // the BlackHole's spot

        public float Reach => Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);

        /// Builds the paths (pure: no camera).
        public static Context Make(Vector2 origin, Vector2 centre, float halfWidth, float halfHeight, List<Vector2> foes)
        {
            var c = new Context { origin = origin, centre = centre, halfWidth = halfWidth, halfHeight = halfHeight, foes = foes ?? new List<Vector2>() };
            c.sweep = SweepDirection(origin, c.foes);
            float w = halfWidth * 0.8f, h = halfHeight * 0.7f;
            c.zPath = new List<Vector2> { origin, centre + new Vector2(-w, h), centre + new Vector2(w, h), centre + new Vector2(-w, -h), centre + new Vector2(w, -h) };
            // The serpent: from the user to the nearest opponent not visited yet, and so on.
            c.serpent = new List<Vector2> { origin };
            c.serpentAt = new float[c.foes.Count];
            var left = new List<int>();
            for (int i = 0; i < c.foes.Count; i++) left.Add(i);
            Vector2 at = origin;
            float length = 0f;
            while (left.Count > 0)
            {
                int best = 0;
                for (int k = 1; k < left.Count; k++)
                    if (Vector2.Distance(at, c.foes[left[k]]) < Vector2.Distance(at, c.foes[left[best]])) best = k;
                int i = left[best];
                left.RemoveAt(best);
                length += Vector2.Distance(at, c.foes[i]);
                c.serpentAt[i] = length;
                at = c.foes[i];
                c.serpent.Add(at);
            }
            Vector2 sum = Vector2.zero;
            foreach (var f in c.foes) sum += f;
            c.hole = c.foes.Count > 0 ? sum / c.foes.Count : origin + Vector2.up * 2f;
            return c;
        }
    }

    /// Where a sweep goes: towards the opponents' middle (to the right without any), as a unit vector.
    public static Vector2 SweepDirection(Vector2 origin, IList<Vector2> foes)
    {
        if (foes == null || foes.Count == 0) return Vector2.right;
        Vector2 sum = Vector2.zero;
        foreach (var f in foes) sum += f;
        Vector2 d = sum / foes.Count - origin;
        return d.sqrMagnitude < 0.01f ? Vector2.right : d.normalized;
    }

    /// The length along a path to its point nearest to "p" (pure).
    public static float AlongPath(IList<Vector2> path, Vector2 p)
    {
        float best = float.MaxValue, at = 0f, walked = 0f;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector2 a = path[i], b = path[i + 1];
            float length = Vector2.Distance(a, b);
            float t = length < 1e-4f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, b - a) / (length * length));
            float d = Vector2.Distance(p, a + (b - a) * t);
            if (d < best) { best = d; at = walked + t * length; }
            walked += length;
        }
        return at;
    }

    public static float PathLength(IList<Vector2> path)
    {
        float length = 0f;
        for (int i = 0; i + 1 < path.Count; i++) length += Vector2.Distance(path[i], path[i + 1]);
        return length;
    }

    /// Seconds after the release when the opponent "index" (nearest first, ctx.foes[index]) is struck.
    public static float Delay(Style style, SpecialItems.ZShow show, int index, Context ctx)
    {
        float speed = Mathf.Max(0.1f, show.speed), lead = Mathf.Max(0f, show.lead);
        Vector2 foe = ctx.foes[index];
        float distance = Vector2.Distance(ctx.origin, foe);
        float reach = ctx.Reach;
        return lead + style switch
        {
            Style.Fall or Style.Sun or Style.Acid or Style.Boulders => index * show.cascade + show.fallTime,
            Style.Wave or Style.Shot or Style.Freeze or Style.Rift or Style.Field or Style.Web or Style.Drill => distance / speed,
            Style.Implode => Mathf.Max(0f, reach - distance) / speed,
            Style.Sweep or Style.Tide => Mathf.Max(0f, Vector2.Dot(foe - (ctx.centre - ctx.sweep * reach), ctx.sweep)) / speed,
            Style.ZDash => AlongPath(ctx.zPath, foe) / speed,
            Style.Serpent => (index < ctx.serpentAt.Length ? ctx.serpentAt[index] : distance) / speed,
            _ => index * show.cascade, // Rise, Bolt, Chain, Vines, Afterimage, Dive, Hands, BlackHole
        };
    }

    /// A random tile in view, at least 3 tiles from the Pokémon, at its height (a Psychic field's throw): never a wall
    /// or out of the arena, nor inside a solid obstacle (a tree, a rock), but water, lava or the void may be where it
    /// lands, and it falls in.
    public static bool RandomSpotInView(Combatant who, out Vector2 spot)
    {
        spot = who.transform.position;
        var grid = DungeonGenerator.Current;
        if (grid == null) return false;
        var (centre, w, h) = View(spot);
        int level = grid.HeightAt(spot);
        for (int i = 0; i < 40; i++)
        {
            var p = centre + new Vector2(Random.Range(-w + 1f, w - 1f), Random.Range(-h + 1f, h - 1f));
            var cell = grid.CellOf(p);
            if (Vector2.Distance(p, spot) < 3f || !grid.WalkableAt(p, 0.3f) || grid.HeightAt(p) != level) continue;
            var obstacle = Obstacles.At(cell.x, cell.y);
            if (obstacle != null && obstacle.solid) continue;
            spot = p;
            return true;
        }
        return false;
    }

    /// The screen's centre and half extents (tiles).
    public static (Vector2 centre, float halfWidth, float halfHeight) View(Vector2 fallback)
    {
        var cam = Camera.main;
        if (cam == null) return (fallback, 10f, 6f);
        float h = cam.orthographicSize;
        return (cam.transform.position, h * cam.aspect, h);
    }
}
