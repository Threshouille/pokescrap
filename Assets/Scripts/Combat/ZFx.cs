using System.Collections.Generic;
using UnityEngine;

/// The building blocks of the Z stagings (ZChoreo), all visual: sprites drawn by code (soft disc, spike, ring, fang,
/// shard), and small objects that move, grow, fade and go: a sprite (Sprite), a particle (Bit), a line (Line).
/// Hand-made sheets (EffectLibrary) take a part's place when they exist (ZChoreo).
public static class ZFx
{
    public const int Order = ZStrike.ShowOrder;

    static Sprite softDisc, spike, waveRing, disk, shard;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { softDisc = spike = waveRing = disk = shard = null; }

    static Sprite Make(int width, int height, System.Func<float, float, float> alpha, Vector2 pivot, FilterMode filter = FilterMode.Bilinear)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = filter, wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha((x + 0.5f) / width, (y + 0.5f) / height))));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, height), pivot, Mathf.Max(width, height));
    }

    /// A glow: a disc fading out from its middle, one unit across.
    public static Sprite SoftDisc => softDisc != null ? softDisc : softDisc = Make(64, 64, (u, v) =>
    {
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        return Mathf.Pow(Mathf.Clamp01(1f - d), 1.6f);
    }, new Vector2(0.5f, 0.5f));

    /// A plain disc with a brighter rim, one unit across (fields, frozen ground).
    public static Sprite Disk => disk != null ? disk : disk = Make(128, 128, (u, v) =>
    {
        float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
        return d > 1f ? 0f : d > 0.94f ? 1f : 0.35f + 0.25f * d;
    }, new Vector2(0.5f, 0.5f));

    /// A soft ring, one unit across.
    public static Sprite WaveRing => waveRing != null ? waveRing : waveRing = Make(128, 128, (u, v) =>
    {
        float edge = 1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f; // 0 at the rim
        return edge < 0f ? 0f : edge < 0.03f ? 1f : Mathf.Clamp01(1f - (edge - 0.03f) / 0.12f) * 0.6f;
    }, new Vector2(0.5f, 0.5f));

    /// A spike pointing up, its pivot at its foot (ice, hands, vines, fangs): one unit high, half as wide.
    public static Sprite Spike => spike != null ? spike : spike = Make(32, 64, (u, v) =>
    {
        float half = 0.5f * (1f - v); // narrows to the top
        float d = Mathf.Abs(u - 0.5f);
        return d > half ? 0f : 0.55f + 0.45f * (1f - d / Mathf.Max(0.01f, half));
    }, new Vector2(0.5f, 0f));

    /// A small rough shard (rocks, needles, sparks), one unit across.
    public static Sprite Shard => shard != null ? shard : shard = Make(16, 16, (u, v) =>
    {
        float d = Mathf.Abs(u - 0.5f) + Mathf.Abs(v - 0.5f);
        return d < 0.45f ? 1f : 0f;
    }, new Vector2(0.5f, 0.5f), FilterMode.Point);

    static SpriteRenderer Renderer(string name, Sprite sprite, Color colour, Vector2 at, int order = Order)
    {
        var go = new GameObject(name);
        go.transform.position = at;
        var r = go.AddComponent<SpriteRenderer>();
        Unlit.Apply(r);
        r.sprite = sprite;
        r.color = colour;
        r.sortingOrder = order;
        return r;
    }

    /// A sprite at a spot (scale: tiles), shown from "wait" s on, for "life" s: the returned Piece moves it.
    public static Piece Draw(Sprite sprite, Color colour, Vector2 at, float scale, float wait, float life, int order = Order)
    {
        var r = Renderer("Z", sprite, colour, at, order);
        r.transform.localScale = Vector3.one * scale;
        var piece = r.gameObject.AddComponent<Piece>();
        piece.Begin(wait, life);
        return piece;
    }

    /// A sheet (or, without it, the fallback sprite) at a spot: "size" tiles wide. The returned Piece moves it.
    public static Piece Sheet(string sheet, Sprite fallback, Color tint, Color fallbackColour, Vector2 at, Vector2 facing, float size, float wait, float life, int order = Order)
    {
        var go = EffectPlayer.Spawn(sheet, at, facing == Vector2.zero ? Vector2.right : facing, null, wait + life + 0.05f, size);
        if (go == null) return Draw(fallback, fallbackColour, at, size * 0.6f, wait, life, order);
        var r = go.GetComponent<SpriteRenderer>();
        r.sortingOrder = order;
        r.color = tint;
        var piece = go.AddComponent<Piece>();
        piece.Begin(wait, life);
        piece.unit = EffectLibrary.PixelsPerUnit / (float)EffectLibrary.Get(sheet).frame_width; // Grow takes tiles
        return piece;
    }

    /// A burst of "count" particles from a spot: speeds between min and max (tiles/s), within "spread" degrees around
    /// "direction" (360: all around), falling with "gravity", fading over "life" s.
    public static void Burst(Vector2 at, int count, Color colour, float size, float minSpeed, float maxSpeed, float life,
        float gravity = 0f, Vector2 direction = default, float spread = 360f, float wait = 0f, Sprite sprite = null)
    {
        float facing = direction == Vector2.zero ? 0f : Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        for (int i = 0; i < count; i++)
        {
            float angle = (facing + Random.Range(-spread / 2f, spread / 2f)) * Mathf.Deg2Rad;
            var v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Random.Range(minSpeed, maxSpeed);
            var piece = Draw(sprite != null ? sprite : Shard, colour, at, size * Random.Range(0.6f, 1.2f), wait, life * Random.Range(0.7f, 1.1f));
            piece.velocity = v;
            piece.gravity = gravity;
            piece.spin = Random.Range(-360f, 360f);
            piece.fadeOut = 0.6f;
        }
    }

    /// A line through the points (tiles), "width" tiles thick, shown from "wait" s on for "life" s; "grow": seconds
    /// over which it draws itself from its first point to its last.
    public static LineRenderer Line(IList<Vector2> points, Color colour, float width, float wait, float life, float grow = 0f, int order = Order)
    {
        var go = new GameObject("Trait Z");
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = Unlit.Material;
        line.useWorldSpace = true;
        line.startWidth = line.endWidth = width;
        line.startColor = line.endColor = colour;
        line.sortingOrder = order;
        line.numCapVertices = 2;
        line.positionCount = points.Count;
        for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
        var piece = go.AddComponent<LinePiece>();
        piece.Begin(points, colour, wait, life, grow);
        return line;
    }

    /// A jagged line from a to b (a bolt, a crack): "segments" pieces, each bent sideways by up to "jag" tiles.
    public static List<Vector2> Jagged(Vector2 a, Vector2 b, int segments, float jag)
    {
        var points = new List<Vector2> { a };
        Vector2 side = Vector2.Perpendicular((b - a).normalized);
        for (int i = 1; i < segments; i++)
            points.Add(Vector2.Lerp(a, b, (float)i / segments) + side * Random.Range(-jag, jag));
        points.Add(b);
        return points;
    }

    /// A copy of a Pokémon's current frame (afterimages, the user's dash), tinted.
    public static Piece Ghost(Combatant who, Vector2 at, Color colour, float wait, float life)
    {
        var body = who != null ? who.GetComponent<SpriteRenderer>() : null;
        var piece = Draw(body != null ? body.sprite : SoftDisc, colour, at, 1f, wait, life, Order);
        if (body != null)
        {
            piece.Body.flipX = body.flipX;
            piece.transform.localScale = who.transform.lossyScale;
        }
        return piece;
    }
}

/// A piece of a Z staging: hidden "wait" seconds, then for "life" seconds it may follow a path or move (velocity,
/// gravity), spin, grow, fade in and out; then it goes.
public class Piece : MonoBehaviour
{
    public Vector2 velocity;
    public float gravity, spin, fadeIn, fadeOut, growTo = -1f;
    public List<Vector2> path; // followed over its life (accelerating when "ease" is 2)
    public int ease;
    public bool faceAlong; // turns along its path or velocity
    public float unit = 1f; // scale of one tile (a sheet's: 24 / its frame width), for Grow

    float start, wait, life, growFrom;
    bool shown;
    SpriteRenderer body;
    Color colour;

    public SpriteRenderer Body => body;

    public void Begin(float delay, float length)
    {
        start = Time.time;
        wait = Mathf.Max(0f, delay);
        life = Mathf.Max(0.01f, length);
        body = GetComponent<SpriteRenderer>();
        if (body != null) { colour = body.color; body.enabled = wait <= 0f; }
        shown = wait <= 0f;
        growFrom = transform.localScale.x;
    }

    /// Follows that path over its life ("ease" 1: slows down at the end, 2: speeds up).
    public Piece Along(List<Vector2> points, int easing = 0, bool face = false)
    {
        path = points;
        ease = easing;
        faceAlong = face;
        if (points != null && points.Count > 0) transform.position = points[0];
        return this;
    }

    /// Grows to that size in tiles over its life (eased).
    public Piece Grow(float to) { growTo = to * unit; return this; }
    public Piece Fade(float inTime, float outShare) { fadeIn = inTime; fadeOut = outShare; return this; }

    void Update()
    {
        float age = Time.time - start - wait;
        if (age < 0f) return;
        if (!shown) { shown = true; if (body != null) body.enabled = true; }
        float t = age / life;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        if (path != null && path.Count > 1)
        {
            float u = ease == 1 ? 1f - (1f - t) * (1f - t) : ease == 2 ? t * t : t;
            Vector2 before = transform.position;
            transform.position = PointAlong(path, u);
            Vector2 moved = (Vector2)transform.position - before;
            if (faceAlong && moved.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(moved.y, moved.x) * Mathf.Rad2Deg);
        }
        else if (velocity != Vector2.zero || gravity != 0f)
        {
            velocity += Vector2.down * gravity * Time.deltaTime;
            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (faceAlong && velocity.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        }
        if (spin != 0f) transform.Rotate(0f, 0f, spin * Time.deltaTime);
        if (growTo >= 0f) transform.localScale = Vector3.one * Mathf.Lerp(growFrom, growTo, 1f - (1f - t) * (1f - t));
        if (body != null)
        {
            float a = 1f;
            if (fadeIn > 0f) a *= Mathf.Clamp01(age / fadeIn);
            if (fadeOut > 0f && t > 1f - fadeOut) a *= (1f - t) / fadeOut;
            body.color = new Color(colour.r, colour.g, colour.b, colour.a * a);
        }
    }

    /// The point at share "u" (0 to 1) of a path's length.
    public static Vector2 PointAlong(IList<Vector2> path, float u)
    {
        float total = ZStrike.PathLength(path), goal = Mathf.Clamp01(u) * total;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            float length = Vector2.Distance(path[i], path[i + 1]);
            if (goal <= length || i + 2 == path.Count)
                return Vector2.Lerp(path[i], path[i + 1], length < 1e-4f ? 1f : Mathf.Clamp01(goal / length));
            goal -= length;
        }
        return path.Count > 0 ? path[^1] : Vector2.zero;
    }

}

/// A line of a Z staging: hidden "wait" seconds, drawn from its start over "grow" seconds, fading at the end of "life".
public class LinePiece : MonoBehaviour
{
    LineRenderer line;
    List<Vector2> points;
    Color colour;
    float start, wait, life, grow;

    public void Begin(IList<Vector2> along, Color c, float delay, float length, float growTime)
    {
        line = GetComponent<LineRenderer>();
        points = new List<Vector2>(along);
        colour = c;
        start = Time.time;
        wait = delay;
        life = Mathf.Max(0.01f, length);
        grow = growTime;
        line.enabled = wait <= 0f;
        Draw(0f);
    }

    void Draw(float share)
    {
        if (grow <= 0f || points.Count < 2) return;
        float total = ZStrike.PathLength(points), goal = share * total, walked = 0f;
        var shown = new List<Vector3> { points[0] };
        for (int i = 0; i + 1 < points.Count && walked < goal; i++)
        {
            float length = Vector2.Distance(points[i], points[i + 1]);
            if (walked + length <= goal) shown.Add(points[i + 1]);
            else shown.Add(Vector2.Lerp(points[i], points[i + 1], (goal - walked) / Mathf.Max(1e-4f, length)));
            walked += length;
        }
        if (shown.Count == 1) shown.Add(points[0]);
        line.positionCount = shown.Count;
        line.SetPositions(shown.ToArray());
    }

    void Update()
    {
        float age = Time.time - start - wait;
        if (age < 0f) return;
        line.enabled = true;
        float t = age / life;
        if (t >= 1f) { Destroy(gameObject); return; }
        if (grow > 0f) Draw(Mathf.Clamp01(age / grow));
        float a = t > 0.7f ? (1f - t) / 0.3f : 1f;
        line.startColor = line.endColor = new Color(colour.r, colour.g, colour.b, colour.a * a);
    }
}
