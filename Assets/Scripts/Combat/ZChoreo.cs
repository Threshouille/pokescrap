using System.Collections.Generic;
using UnityEngine;
using Style = ZStrike.Style;

/// The visuals of each Z staging (ZStrike.Style; GDD, "Objets spéciaux", mise en scène): Release spawns what starts
/// at the release (a Z traced by the user, a sun, a tidal wave, a field, a web, a black hole...), timed on the
/// opponents' hits (plan: their spots and delays); Impact what each opponent sees as it is struck. Drawn by code
/// (ZFx) until hand-made sheets take the main parts' places: "travel" (the meteor, the wave, the dragon's head, the
/// drill...), "extra" (a second element), "impact" (on each opponent).
public static class ZChoreo
{
    static readonly Vector2 Drop = new(-1.5f, 6f); // where a fall starts, relative to its spot (tiles)

    public struct Hit { public Vector2 at; public float delay; public int index; }

    public static void Release(Style style, SpecialItems.ZShow show, ZStrike.Context ctx, List<Hit> plan, Combatant user, Color colour, float scale)
    {
        var flash = EffectPlayer.Spawn(SpecialItems.ZFlash, ctx.origin, Vector2.right, null, 0f, 8f * scale);
        if (flash != null) flash.GetComponent<SpriteRenderer>().color = colour;
        if (!string.IsNullOrEmpty(show.wave)) EffectPlayer.Spawn(show.wave, ctx.origin, Vector2.right, null, 0f, 6f * scale);
        CameraFollow.Shake(0.2f, 0.35f);
        float speed = Mathf.Max(0.1f, show.speed), lead = Mathf.Max(0f, show.lead), reach = ctx.Reach;
        Color tint = show.tint ? colour : Color.white;
        Color light = Color.Lerp(colour, Color.white, 0.5f);
        float last = 0f;
        foreach (var h in plan) last = Mathf.Max(last, h.delay);

        switch (style)
        {
            // ---- the first, plain stagings ----
            case Style.Wave:
                Ring(ctx.origin, 0f, reach, reach / speed, colour);
                break;
            case Style.Implode:
                Ring(ctx.origin, reach, 0f, reach / speed, colour);
                break;
            case Style.Sweep:
                SweepLine(show, ctx, speed, tint, colour, 1);
                break;
            case Style.Fall:
                foreach (var h in plan) FallOn(show, h.at, h.delay, scale, tint, colour);
                break;
            case Style.Shot:
                foreach (var h in plan)
                    ZFx.Sheet(show.travel, ZFx.SoftDisc, tint, colour, ctx.origin, h.at - ctx.origin, show.travelSize, 0f, Mathf.Max(0.05f, h.delay - lead))
                        .Along(new List<Vector2> { ctx.origin, h.at });
                break;

            // ---- Normal: the user dashes along a Z across the screen, the Z stays drawn a moment ----
            case Style.ZDash:
            {
                float total = ZStrike.PathLength(ctx.zPath) / speed;
                ZFx.Line(ctx.zPath, new Color(1f, 1f, 1f, 0.9f), 0.45f, lead, total + 0.7f, total);
                ZFx.Line(ctx.zPath, colour, 0.9f, lead + total, 0.45f); // the whole Z flashes at the end
                ZFx.Ghost(user, ctx.origin, Color.white, lead, total).Along(ctx.zPath);
                if (Has(show.travel)) ZFx.Sheet(show.travel, null, Color.white, Color.white, ctx.origin, Vector2.right, show.travelSize, lead, total).Along(ctx.zPath, 0, face: true);
                int ghosts = Mathf.CeilToInt(total / 0.04f);
                for (int i = 0; i < ghosts; i++)
                {
                    float u = (float)i / ghosts;
                    ZFx.Ghost(user, Piece.PointAlong(ctx.zPath, u), new Color(light.r, light.g, light.b, 0.5f), lead + u * total, 0.25f).Fade(0f, 1f);
                }
                break;
            }

            // ---- Fire: a sun gathers above the user, then hurls a meteor at each opponent ----
            case Style.Sun:
            {
                Vector2 sun = SunSpot(ctx);
                float life = last + 0.4f;
                ZFx.Draw(ZFx.SoftDisc, new Color(1f, 0.55f, 0.1f, 0.6f), sun, 0.2f, 0f, life).Grow(6f * scale).Fade(0.1f, 0.25f); // its glow
                if (Has(show.extra)) ZFx.Sheet(show.extra, null, Color.white, colour, sun, Vector2.right, 0.5f, 0f, life).Grow(3f * scale).Fade(0.1f, 0.25f);
                else ZFx.Draw(ZFx.SoftDisc, new Color(1f, 0.95f, 0.7f, 1f), sun, 0.1f, 0f, life).Grow(2f * scale).Fade(0.1f, 0.25f);
                for (int i = 0; i < 6; i++) // flares rising from it
                    ZFx.Burst(sun, 4, new Color(1f, 0.7f, 0.2f), 0.25f, 1f, 3f, 0.6f, -2f, Vector2.up, 360f, i * lead / 6f, ZFx.SoftDisc);
                foreach (var h in plan) // the meteors come down from the sky, out of the screen
                {
                    Vector2 sky = new(h.at.x - 3f, ctx.centre.y + ctx.halfHeight + 2.5f);
                    ZFx.Sheet(show.travel, ZFx.SoftDisc, tint, new Color(1f, 0.5f, 0.1f), sky, h.at - sky, show.travelSize * scale, h.delay - show.fallTime, show.fallTime)
                        .Along(new List<Vector2> { sky, h.at }, 2, face: true);
                }
                break;
            }

            // ---- Water: a tidal wave as high as the screen breaks across it ----
            case Style.Tide:
            {
                float time = 2f * reach / speed;
                Vector2 side = Vector2.Perpendicular(ctx.sweep);
                Vector2 start = ctx.centre - ctx.sweep * reach, end = ctx.centre + ctx.sweep * reach;
                var body = ZFx.Draw(Shapes.Square, new Color(colour.r, colour.g, colour.b, 0.45f), start - ctx.sweep * 1.4f, 1f, lead, time);
                body.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(ctx.sweep.y, ctx.sweep.x) * Mathf.Rad2Deg);
                body.transform.localScale = new Vector3(2.8f, 2f * reach + 2f, 1f);
                body.Along(new List<Vector2> { start - ctx.sweep * 1.4f, end - ctx.sweep * 1.4f }).Fade(0.1f, 0.15f);
                body.growTo = -1f;
                SweepLine(show, ctx, speed, tint, colour, 3);
                for (float s = -reach; s <= reach; s += 1.5f) // foam thrown up along the crest
                    for (float k = 0f; k < 1f; k += 0.2f)
                        ZFx.Burst(Vector2.Lerp(start, end, k) + side * s, 1, new Color(0.9f, 0.97f, 1f), 0.3f, 1f, 2.5f, 0.4f, 6f, ctx.sweep + Vector2.up, 70f, lead + k * time, ZFx.SoftDisc);
                break;
            }

            // ---- Grass: a storm of petals sweeps the screen (the vines come up under each opponent) ----
            case Style.Vines:
                for (int i = 0; i < 40; i++)
                {
                    Vector2 from = ctx.centre - ctx.sweep * ctx.Reach + Vector2.Perpendicular(ctx.sweep) * Random.Range(-ctx.Reach, ctx.Reach);
                    var petal = Has(show.extra) && i % 3 != 0
                        ? ZFx.Sheet(show.extra, null, Color.white, Color.white, from, Vector2.right, 0.4f, Random.Range(0f, 0.6f), 1.4f)
                        : ZFx.Draw(ZFx.Shard, i % 3 == 0 ? new Color(0.5f, 0.9f, 0.4f) : new Color(1f, 0.6f, 0.8f), from, 0.3f, Random.Range(0f, 0.6f), 1.4f);
                    petal.velocity = (ctx.sweep * Random.Range(10f, 16f)) + Random.insideUnitCircle * 2f;
                    petal.spin = Random.Range(-400f, 400f);
                    petal.fadeOut = 0.3f;
                }
                break;

            // ---- Ice: the ground freezes from the user (the spikes come up as it reaches each opponent) ----
            case Style.Freeze:
                ZFx.Draw(ZFx.Disk, new Color(0.75f, 0.95f, 1f, 0.55f), ctx.origin, 0f, lead, reach / speed + 0.8f, 0).Grow(2f * reach).Fade(0f, 0.3f);
                Ring(ctx.origin, 0f, reach, reach / speed, new Color(0.85f, 1f, 1f));
                break;

            // ---- Poison: an acid rain falls over the screen ----
            case Style.Acid:
            {
                float time = last + 0.3f;
                for (int i = 0; i < 70; i++)
                {
                    float wait = Random.Range(0f, time);
                    Vector2 to = ctx.centre + new Vector2(Random.Range(-ctx.halfWidth, ctx.halfWidth), Random.Range(-ctx.halfHeight, ctx.halfHeight));
                    var drop = ZFx.Draw(Shapes.Square, new Color(light.r, light.g, light.b, 0.8f), to + Drop * 0.6f, 1f, wait, 0.3f);
                    drop.transform.localScale = new Vector3(0.07f, 0.6f, 1f);
                    drop.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Drop.y, Drop.x) * Mathf.Rad2Deg - 90f);
                    drop.Along(new List<Vector2> { to + Drop * 0.6f, to }, 2);
                }
                foreach (var h in plan) FallOn(show, h.at, h.delay, scale, tint, colour);
                break;
            }

            // ---- Ground: a crack runs through the ground to each opponent ----
            case Style.Rift:
                foreach (var h in plan)
                {
                    var crack = ZFx.Jagged(ctx.origin, h.at, Mathf.Max(3, Mathf.RoundToInt(Vector2.Distance(ctx.origin, h.at) * 1.5f)), 0.35f);
                    ZFx.Line(crack, new Color(0.18f, 0.1f, 0.04f, 0.95f), 0.16f, lead, h.delay - lead + 1f, h.delay - lead, 0);
                    ZFx.Line(crack, new Color(1f, 0.6f, 0.2f, 0.7f), 0.05f, lead, h.delay - lead + 0.6f, h.delay - lead, 0);
                }
                break;

            // ---- Flying: the user flies up out of the screen (it dives on each opponent) ----
            case Style.Dive:
                ZFx.Ghost(user, ctx.origin, Color.white, 0f, Mathf.Max(0.1f, lead)).Along(new List<Vector2> { ctx.origin, ctx.origin + Vector2.up * (ctx.halfHeight * 2f + 2f) }, 2);
                ZFx.Burst(ctx.origin, 14, new Color(0.85f, 0.95f, 1f), 0.3f, 3f, 6f, 0.4f, 0f, Vector2.down, 120f, 0f, ZFx.SoftDisc);
                foreach (var h in plan) // the dive on each one, from above the screen
                {
                    Vector2 top = h.at + new Vector2(-1.5f, ctx.halfHeight * 2f);
                    const float Drop = 0.2f;
                    ZFx.Ghost(user, top, Color.white, h.delay - Drop, Drop).Along(new List<Vector2> { top, h.at }, 2);
                    ZFx.Sheet(show.travel, null, Color.white, Color.white, top, h.at - top, show.travelSize, h.delay - Drop, Drop).Along(new List<Vector2> { top, h.at }, 2, face: true);
                }
                break;

            // ---- Psychic: a field spreads out from the user, like the mega Mewtwo Y's ----
            case Style.Field:
            {
                float time = reach / speed;
                if (Has(show.extra)) ZFx.Sheet(show.extra, null, Color.white, colour, ctx.origin, Vector2.right, 0.2f, lead, time + 0.7f, 0).Grow(2f * reach).Fade(0f, 0.3f);
                else ZFx.Draw(ZFx.Disk, new Color(colour.r, colour.g, colour.b, 0.45f), ctx.origin, 0f, lead, time + 0.7f, 0).Grow(2f * reach).Fade(0f, 0.3f);
                Ring(ctx.origin, 0f, reach, time, light);
                for (int i = 1; i <= 3; i++) Ring(ctx.origin, 0f, reach * 0.9f, time * (1f + 0.25f * i), new Color(light.r, light.g, light.b, 0.5f), lead + 0.1f * i);
                break;
            }

            // ---- Bug: a web is woven over the screen from the user, a thread reaches each opponent ----
            case Style.Web:
            {
                Color silk = new(1f, 1f, 0.92f, 0.85f);
                if (Has(show.extra)) // the woven web grows over the screen
                    ZFx.Sheet(show.extra, null, Color.white, Color.white, ctx.origin, Vector2.right, 0.3f, lead, last + 0.9f, 0).Grow(2.2f * reach).Fade(0f, 0.25f);
                else for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.PI * 2f / 10f;
                    Vector2 tip = ctx.origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * reach;
                    ZFx.Line(new List<Vector2> { ctx.origin, tip }, silk, 0.05f, lead, last + 0.9f, reach / speed);
                }
                for (float r = 1.5f; r < reach && !Has(show.extra); r += 1.8f) // the rings, each when the spokes reach it
                {
                    var ring = new List<Vector2>();
                    for (int i = 0; i <= 10; i++)
                    {
                        float a = i * Mathf.PI * 2f / 10f;
                        ring.Add(ctx.origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                    }
                    ZFx.Line(ring, new Color(silk.r, silk.g, silk.b, 0.6f), 0.04f, lead + r / speed, last - r / speed + 0.9f, 0.2f);
                }
                foreach (var h in plan) ZFx.Line(new List<Vector2> { ctx.origin, h.at }, Color.white, 0.07f, lead, h.delay - lead + 0.6f, h.delay - lead);
                break;
            }

            // ---- Rock: boulders fall and stay a moment ----
            case Style.Boulders:
                foreach (var h in plan) FallOn(show, h.at, h.delay, scale, tint, colour);
                break;

            // ---- Ghost: shadow pools open under each opponent first ----
            case Style.Hands:
                foreach (var h in plan)
                    ZFx.Draw(ZFx.SoftDisc, new Color(0.15f, 0.05f, 0.25f, 0.85f), h.at, 0.2f, 0f, h.delay + 0.7f, 0).Grow(1.8f * scale).Fade(0.1f, 0.3f);
                break;

            // ---- Dragon: an energy dragon winds through every opponent, then out of the screen ----
            case Style.Serpent:
            {
                var path = new List<Vector2>(ctx.serpent);
                Vector2 tail = path.Count > 1 ? path[^1] - path[^2] : ctx.sweep;
                path.Add(path[^1] + tail.normalized * reach);
                float time = ZStrike.PathLength(path) / speed;
                ZFx.Sheet(show.travel, ZFx.SoftDisc, tint, light, path[0], tail, show.travelSize * scale, lead, time).Along(path, 0, face: true);
                for (int k = 1; k <= 14; k++)
                {
                    float size = (show.travelSize * 0.8f - k * 0.04f) * scale;
                    var seg = Has(show.piece)
                        ? ZFx.Sheet(show.piece, null, Color.white, colour, path[0], Vector2.right, size, lead + k * 0.035f, time)
                        : ZFx.Draw(ZFx.SoftDisc, new Color(colour.r, colour.g, colour.b, 0.9f - k * 0.04f), path[0], size, lead + k * 0.035f, time);
                    seg.Along(path);
                }
                break;
            }

            // ---- Dark: a black hole opens between the opponents and swallows the light, then bursts ----
            case Style.BlackHole:
            {
                Vector2 hole = ctx.hole;
                if (Has(show.extra)) ZFx.Sheet(show.extra, null, Color.white, colour, hole, Vector2.right, 0.3f, 0f, lead + 0.25f).Grow(4.5f * scale);
                else
                {
                    ZFx.Draw(ZFx.SoftDisc, new Color(0.05f, 0f, 0.08f, 1f), hole, 0f, 0f, lead + 0.25f).Grow(3.5f * scale);
                    ZFx.Draw(ZFx.WaveRing, new Color(0.6f, 0.2f, 0.8f, 0.9f), hole, 0f, 0f, lead + 0.25f).Grow(3.2f * scale);
                }
                for (int i = 0; i < 50; i++) // light drawn in, spiralling
                {
                    float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(4f, reach);
                    Vector2 from = hole + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    Vector2 mid = hole + new Vector2(Mathf.Cos(a + 1.2f), Mathf.Sin(a + 1.2f)) * r * 0.45f;
                    float wait = Random.Range(0f, lead * 0.7f);
                    ZFx.Draw(ZFx.SoftDisc, i % 2 == 0 ? new Color(0.7f, 0.4f, 1f) : Color.white, from, 0.25f, wait, Mathf.Max(0.15f, lead - wait))
                        .Along(new List<Vector2> { from, mid, hole }, 2);
                }
                Ring(hole, 0f, reach, 0.4f, new Color(0.55f, 0.15f, 0.75f), lead); // the burst
                ZFx.Burst(hole, 30, new Color(0.6f, 0.25f, 0.85f), 0.35f, 4f, 10f, 0.5f, 0f, Vector2.zero, 360f, lead, ZFx.SoftDisc);
                break;
            }

            // ---- Steel: a drill bores straight to each opponent, throwing sparks ----
            case Style.Drill:
                foreach (var h in plan)
                {
                    float time = Mathf.Max(0.05f, h.delay - lead);
                    var drill = ZFx.Sheet(show.travel, ZFx.Spike, tint, light, ctx.origin, h.at - ctx.origin, show.travelSize, lead, time);
                    drill.Along(new List<Vector2> { ctx.origin, h.at }, 0, face: true);
                    float length = Vector2.Distance(ctx.origin, h.at);
                    for (float d = 0.8f; d < length; d += 0.8f)
                        ZFx.Burst(Vector2.Lerp(ctx.origin, h.at, d / length), 3, new Color(1f, 0.9f, 0.5f), 0.15f, 2f, 5f, 0.25f, 4f, Vector2.zero, 360f, lead + time * d / length);
                }
                break;
        }
    }

    /// What is seen on an opponent as it is struck.
    public static void Impact(Style style, SpecialItems.ZShow show, ZStrike.Context ctx, int index, Vector2 at, Combatant user, string fallback, Color colour, float scale)
    {
        Color light = Color.Lerp(colour, Color.white, 0.5f);
        switch (style)
        {
            case Style.Bolt:
            case Style.Chain:
            {
                var sheet = EffectLibrary.Get(show.travel);
                if (sheet != null)
                {
                    float width = show.travelSize * scale, height = width * sheet.frame_height / sheet.frame_width;
                    ZFx.Sheet(show.travel, null, show.tint ? colour : Color.white, colour, at + Vector2.up * height / 2f, Vector2.right, width, 0f, 0.3f);
                }
                if (style == Style.Chain)
                {
                    var bolt = ZFx.Jagged(at + new Vector2(Random.Range(-1f, 1f), ctx.halfHeight * 2f), at, 9, 0.5f);
                    if (sheet == null) {
                    ZFx.Line(bolt, new Color(1f, 0.9f, 0.3f), 0.22f, 0f, 0.25f);
                    ZFx.Line(bolt, Color.white, 0.08f, 0f, 0.25f);
                    for (int b = 0; b < 2; b++)
                    {
                        Vector2 from = bolt[Random.Range(2, bolt.Count - 2)];
                        ZFx.Line(ZFx.Jagged(from, from + new Vector2(Random.Range(-1.5f, 1.5f), -Random.Range(0.8f, 2f)), 4, 0.3f), new Color(1f, 0.9f, 0.4f, 0.8f), 0.07f, 0f, 0.2f);
                    }
                    }
                    else ZFx.Line(bolt, new Color(1f, 0.95f, 0.5f, 0.6f), 0.08f, 0f, 0.15f); // the bolt's path down from the sky
                    if (index > 0) // an arc to the opponent struck before
                    {
                        var arc = ZFx.Jagged(ctx.foes[index - 1], at, 7, 0.4f);
                        ZFx.Line(arc, new Color(0.8f, 0.95f, 1f, 0.9f), 0.1f, 0f, 0.35f);
                    }
                }
                break;
            }
            case Style.ZDash:
                ZFx.Burst(at, 10, Color.white, 0.3f, 3f, 7f, 0.35f);
                break;
            case Style.Sun:
                ZFx.Draw(ZFx.SoftDisc, new Color(0.35f, 0.12f, 0.03f, 0.85f), at, 1.6f * scale, 0f, 1.6f, 0).Fade(0f, 0.5f); // the crater
                ZFx.Burst(at, 14, new Color(1f, 0.6f, 0.15f), 0.25f, 1.5f, 4f, 0.8f, -2f, Vector2.up, 160f, 0f, ZFx.SoftDisc);
                break;
            case Style.Tide:
            case Style.Sweep:
                ZFx.Burst(at, 12, new Color(0.75f, 0.9f, 1f), 0.3f, 2f, 5f, 0.5f, 8f, Vector2.up, 120f, 0f, ZFx.SoftDisc);
                break;
            case Style.Vines:
                for (int i = 0; i < 4 && Has(show.piece); i++)
                    Upright(show.piece, at + new Vector2((i - 1.5f) * 0.35f, Random.Range(-0.2f, 0.1f)), 1f * scale, i * 0.05f, 0.9f);
                for (int i = 0; i < 4 && !Has(show.piece); i++)
                {
                    var vine = ZFx.Draw(ZFx.Spike, i % 2 == 0 ? new Color(0.25f, 0.65f, 0.2f) : new Color(0.45f, 0.85f, 0.3f),
                        at + new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.3f, 0.2f)), 0f, i * 0.04f, 0.8f);
                    vine.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
                    vine.Grow(1.6f * scale).Fade(0f, 0.35f);
                }
                ZFx.Burst(at, 8, new Color(0.5f, 0.9f, 0.4f), 0.2f, 1f, 3f, 0.6f, 2f, Vector2.up, 140f);
                break;
            case Style.Freeze:
                for (int i = 0; i < 3 && Has(show.piece); i++)
                    Upright(show.piece, at + new Vector2((i - 1) * 0.45f, i == 1 ? 0.05f : -0.1f), (i == 1 ? 1.1f : 0.8f) * scale, i * 0.03f, 0.75f);
                for (int i = 0; i < 3 && !Has(show.piece); i++)
                {
                    var ice = ZFx.Draw(ZFx.Spike, new Color(0.8f, 0.97f, 1f, 0.95f), at + new Vector2((i - 1) * 0.45f, (i == 1 ? 0.1f : -0.1f)), 0f, i * 0.03f, 0.7f);
                    ice.transform.rotation = Quaternion.Euler(0f, 0f, (i - 1) * -18f);
                    ice.Grow((i == 1 ? 1.8f : 1.2f) * scale).Fade(0f, 0.25f);
                }
                ZFx.Burst(at, 12, new Color(0.85f, 1f, 1f), 0.2f, 2f, 5f, 0.4f, 6f, Vector2.up, 360f, 0.6f);
                break;
            case Style.Afterimage:
                for (int i = 0; i < 3; i++)
                {
                    float a = (i * 120f + Random.Range(0f, 40f)) * Mathf.Deg2Rad;
                    Vector2 spot = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.9f;
                    ZFx.Ghost(user, spot, new Color(light.r, light.g, light.b, 0.75f), i * 0.06f, 0.35f).Fade(0.03f, 0.5f);
                    EffectPlayer.Spawn(EffectLibrary.Get(show.impact) != null ? show.impact : fallback, Vector2.Lerp(spot, at, 0.6f), at - spot, null, 0f, show.size * 0.6f * scale);
                }
                break;
            case Style.Acid:
                if (Has(show.piece)) ZFx.Sheet(show.piece, null, Color.white, colour, at, Vector2.right, 0.4f, 0f, 2f, 0).Grow(2.2f * scale).Fade(0f, 0.3f);
                else ZFx.Draw(ZFx.Disk, new Color(colour.r, colour.g, colour.b, 0.7f), at, 0.2f, 0f, 2f, 0).Grow(1.8f * scale).Fade(0f, 0.3f);
                for (int i = 0; i < 4; i++) ZFx.Burst(at + Random.insideUnitCircle * 0.6f, 2, light, 0.2f, 0.3f, 0.8f, 0.5f, -1f, Vector2.up, 40f, 0.3f * i, ZFx.SoftDisc);
                break;
            case Style.Rift:
                ZFx.Burst(at, 14, new Color(0.55f, 0.35f, 0.18f), 0.35f, 3f, 7f, 0.7f, 14f, Vector2.up, 90f);
                ZFx.Draw(ZFx.SoftDisc, new Color(0.7f, 0.55f, 0.35f, 0.6f), at, 1f, 0f, 0.7f).Grow(2.5f * scale).Fade(0f, 0.6f);
                break;
            case Style.Dive:
            {
                Vector2 top = at + new Vector2(-1.5f, ctx.halfHeight * 2f);
                ZFx.Line(new List<Vector2> { top, at }, new Color(0.85f, 0.95f, 1f, 0.6f), 0.3f, 0f, 0.2f);
                ZFx.Burst(at, 12, new Color(0.9f, 0.97f, 1f), 0.3f, 3f, 6f, 0.35f, 0f, Vector2.zero, 360f, 0f, ZFx.SoftDisc);
                break;
            }
            case Style.Field:
                ZFx.Burst(at, 10, light, 0.25f, 1f, 3f, 0.5f, 0f, Vector2.zero, 360f, 0f, ZFx.SoftDisc);
                break;
            case Style.Web:
            {
                if (Has(show.piece)) ZFx.Sheet(show.piece, null, Color.white, Color.white, at, Vector2.right, 1.2f * scale, 0f, 0.9f).fadeOut = 0.2f; // bound
                else
                {
                    var knot = new List<Vector2>();
                    for (int i = 0; i <= 8; i++) knot.Add(at + new Vector2(Mathf.Cos(i * Mathf.PI / 4f), Mathf.Sin(i * Mathf.PI / 4f)) * 0.55f);
                    ZFx.Line(knot, Color.white, 0.06f, 0f, 0.9f);
                }
                ZFx.Burst(at, 12, new Color(0.85f, 1f, 0.6f), 0.18f, 4f, 8f, 0.35f, 0f, Vector2.zero, 360f, 0.3f); // the web bursts into needles
                break;
            }
            case Style.Boulders:
            {
                var rock = ZFx.Sheet(show.extra, ZFx.Shard, Color.white, new Color(0.55f, 0.45f, 0.35f), at, Vector2.right, 1.4f * scale, 0f, 2.2f, 0);
                rock.fadeOut = 0.2f;
                ZFx.Burst(at, 12, new Color(0.6f, 0.5f, 0.4f), 0.25f, 2f, 5f, 0.5f, 10f, Vector2.up, 160f);
                break;
            }
            case Style.Hands:
                for (int i = 0; i < 2 && Has(show.piece); i++)
                    Upright(show.piece, at + new Vector2(i == 0 ? -0.4f : 0.4f, -0.25f), 0.9f * scale, i * 0.06f, 0.75f);
                for (int i = 0; i < 2 && !Has(show.piece); i++)
                {
                    var hand = ZFx.Draw(ZFx.Spike, new Color(0.3f, 0.1f, 0.45f, 0.95f), at + new Vector2((i == 0 ? -0.35f : 0.35f), -0.2f), 0f, i * 0.05f, 0.7f);
                    hand.transform.rotation = Quaternion.Euler(0f, 0f, i == 0 ? 20f : -20f);
                    hand.Grow(1.5f * scale).Fade(0f, 0.4f);
                }
                break;
            case Style.Serpent:
            case Style.BlackHole:
            case Style.Drill:
                ZFx.Burst(at, 10, light, 0.25f, 2f, 6f, 0.4f, 0f, Vector2.zero, 360f, 0f, ZFx.SoftDisc);
                break;
        }
        string impact = EffectLibrary.Get(show.impact) != null ? show.impact : fallback;
        if (style != Style.Afterimage)
        {
            var go = EffectPlayer.Spawn(impact, at, Vector2.right, null, 0f, show.size * scale);
            if (go != null) go.GetComponent<SpriteRenderer>().sortingOrder = ZFx.Order;
            else Shapes.Pulse(at, 0.8f * scale, colour);
        }
        if (show.shake > 0f) CameraFollow.Shake(show.shake, 0.15f);
    }

    static bool Has(string sheet) => EffectLibrary.Get(sheet) != null;

    /// An upright sheet (a vine, a spike, a hand, a bolt) standing on a spot: "width" tiles wide, its foot there.
    static Piece Upright(string sheet, Vector2 at, float width, float wait, float life)
    {
        var s = EffectLibrary.Get(sheet);
        float height = s != null ? width * s.frame_height / s.frame_width : width;
        return ZFx.Sheet(sheet, null, Color.white, Color.white, at + Vector2.up * height / 2f, Vector2.right, width, wait, life);
    }

    /// Where the Fire staging's sun gathers: above the user.
    public static Vector2 SunSpot(ZStrike.Context ctx) => ctx.origin + Vector2.up * 3.5f;

    // ---- shared pieces ----

    static void FallOn(SpecialItems.ZShow show, Vector2 at, float delay, float scale, Color tint, Color colour) =>
        ZFx.Sheet(show.travel, ZFx.SoftDisc, tint, colour, at + Drop, -Drop, show.travelSize * scale, Mathf.Max(0f, delay - show.fallTime), show.fallTime)
            .Along(new List<Vector2> { at + Drop, at }, 2, face: true);

    // A line of "rows" rows of travel sheets across the whole screen, crossing it along the sweep.
    static void SweepLine(SpecialItems.ZShow show, ZStrike.Context ctx, float speed, Color tint, Color colour, int rows)
    {
        float reach = ctx.Reach, time = 2f * reach / speed, lead = Mathf.Max(0f, show.lead);
        Vector2 side = Vector2.Perpendicular(ctx.sweep);
        Vector2 start = ctx.centre - ctx.sweep * reach, end = ctx.centre + ctx.sweep * reach;
        float spacing = Mathf.Max(0.6f, show.travelSize * 0.8f);
        for (int row = 0; row < rows; row++)
        {
            Vector2 back = -ctx.sweep * row * spacing * 0.9f;
            float shift = row % 2 == 0 ? 0f : spacing / 2f;
            for (float s = -reach + shift; s <= reach; s += spacing)
                ZFx.Sheet(show.travel, ZFx.SoftDisc, tint, colour, start + side * s + back, ctx.sweep, show.travelSize * (1f - 0.15f * row), lead, time)
                    .Along(new List<Vector2> { start + side * s + back, end + side * s + back });
        }
    }

    // A ring of the type's colour from one radius to another (tiles).
    static void Ring(Vector2 at, float from, float to, float time, Color colour, float wait = 0f)
    {
        var piece = ZFx.Draw(ZFx.WaveRing, colour, at, from * 2f, wait, Mathf.Max(0.05f, time));
        piece.Grow(to * 2f);
        piece.fadeOut = 0.2f;
    }
}
