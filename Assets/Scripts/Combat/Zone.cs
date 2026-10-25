using System.Collections.Generic;
using UnityEngine;

/// A lingering area of the "zone" move family (a gas cloud, a pool of lava...): gives its status to the opponents
/// inside it and, when the move has power, hurts them at regular intervals.
public class Zone : MonoBehaviour
{
    const float DefaultDuration = 4f;
    const float FirstTick = 0.6f, TickInterval = 1f; // damaging zones leave a moment to step out

    static readonly List<Zone> active = new();

    Team team;
    Combatant owner;
    MoveDef move;
    float dieAt;
    float damage; // raw, per tick; 0 for zones that only carry a status
    float nextTick;
    float nextBolt;
    bool fallen; // fallEffect already launched

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    /// delay: seconds added before the first hit and to the zone's life (zones laid one after the other).
    public static void Spawn(Combatant owner, MoveDef move, Vector2 centre, float delay = 0f)
    {
        var go = new GameObject(move.name);
        go.transform.position = centre;

        // Without a sprite sheet the zone falls back to a translucent disc in the colour of its type.
        if (move.hideZone) { }
        else if (EffectPlayer.Attach(go, move.effect, Vector2.right))
        {
            if (move.effectScale > 0f) go.transform.localScale = Vector3.one * move.effectScale;
            if (move.zoneAlpha > 0f && go.TryGetComponent<SpriteRenderer>(out var sheet))
            {
                var c = sheet.color;
                c.a = move.zoneAlpha;
                sheet.color = c;
            }
        }
        else
        {
            go.transform.localScale = Vector3.one * move.radius * 2f;
            var renderer = go.AddComponent<SpriteRenderer>();
            Unlit.Apply(renderer); // keeps its colours at night (DayNight)
            renderer.sprite = Shapes.Circle;
            var color = GameData.TypeColor(move.type);
            color.a = 0.45f;
            renderer.color = color;
            renderer.sortingOrder = 0; // above the floor, under the Pokémon
        }

        var zone = go.AddComponent<Zone>();
        zone.team = owner.team;
        zone.owner = owner;
        zone.move = move;
        zone.dieAt = Time.time + delay + (move.zoneDuration > 0f ? move.zoneDuration : DefaultDuration);
        zone.nextBolt = Time.time + delay;
        zone.damage = owner.DamageOf(move);
        float firstTick = delay + (move.firstTick > 0f ? move.firstTick : FirstTick);
        zone.nextTick = Time.time + firstTick;
        // Opponents caught in it are frozen (Hurt animation) until the first hit.
        if (move.rootUntilHit)
            foreach (var other in Physics2D.OverlapCircleAll(centre, move.radius))
            {
                var target = other.GetComponentInParent<Combatant>();
                if (target != null && target.team != owner.team && target.IsAlive) target.Stun(firstTick);
            }
        Terrain.OnImpact(move, centre, owner);
        Terrain.OnAround(move, centre, owner);
    }

    /// True when the point lies in a zone that is harmful to the given team (margin widens the zones).
    public static bool Inside(Vector2 point, Team victim, float margin)
    {
        foreach (var zone in active)
            if (zone.move != null && zone.team != victim
                && Vector2.Distance(point, zone.transform.position) <= zone.move.radius + margin)
                return true;
        return false;
    }

    /// Direction that leaves the harmful zones the point is in, or zero outside. Overlapping zones each push away
    /// from their centre, the deeper the harder, so the way out of a cluster of clouds is the shortest one.
    public static Vector2 EscapeDirection(Vector2 point, Team victim, float margin)
    {
        Vector2 push = Vector2.zero;
        bool inside = false;
        foreach (var zone in active)
        {
            if (zone.move == null || zone.team == victim) continue;
            Vector2 away = point - (Vector2)zone.transform.position;
            float depth = zone.move.radius + margin - away.magnitude;
            if (depth < 0f) continue;
            inside = true;
            push += (away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right) * (depth + 0.1f);
        }
        if (!inside) return Vector2.zero;
        return push.sqrMagnitude > 0.0001f ? push.normalized : Vector2.right;
    }

    // Electrified ground where a bolt struck, under the Pokémon, fading out.
    void BoltGround(Vector2 spot)
    {
        if (string.IsNullOrEmpty(move.boltGroundEffect) || EffectLibrary.Get(move.boltGroundEffect) == null) return;
        var go = new GameObject(move.boltGroundEffect);
        go.transform.position = spot;
        EffectPlayer.Attach(go, move.boltGroundEffect, Vector2.right);
        var sheet = EffectLibrary.Get(move.boltGroundEffect);
        go.transform.localScale = Vector3.one * move.boltRadius * 2f * EffectLibrary.PixelsPerUnit / sheet.frame_width;
        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = 0; // on the ground, under the Pokémon
        go.AddComponent<FadeOut>().duration = move.boltGroundTime > 0f ? move.boltGroundTime : 1f;
    }

    // A bolt falling from the sky onto the spot: its sheet stands on the spot (a thin yellow streak without one).
    void BoltFall(Vector2 spot)
    {
        var sheet = EffectLibrary.Get(move.boltFallEffect);
        if (sheet != null)
        {
            float height = sheet.frame_height / (float)EffectLibrary.PixelsPerUnit;
            EffectPlayer.Spawn(move.boltFallEffect, spot + Vector2.up * height / 2f, Vector2.right);
            return;
        }
        const float Height = 3f, Width = 0.12f;
        var go = new GameObject("Éclair");
        go.transform.position = spot + Vector2.up * Height / 2f;
        go.transform.localScale = new Vector3(Width, Height, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = Shapes.Square;
        renderer.color = new Color(1f, 0.95f, 0.5f, 0.9f);
        renderer.sortingOrder = 4;
        go.AddComponent<FadeOut>().duration = 0.15f;
    }

    void FixedUpdate()
    {
        if (move.followOwner && owner != null && owner.IsAlive) transform.position = owner.transform.position;
        if (Time.time >= dieAt)
        {
            Destroy(gameObject);
            return;
        }
        // Bolts striking random spots inside (they replace the regular hits).
        if (move.boltInterval > 0f)
        {
            while (Time.time >= nextBolt)
            {
                nextBolt += move.boltInterval;
                Vector2 spot = (Vector2)transform.position + Random.insideUnitCircle * move.radius;
                BoltFall(spot);
                BoltGround(spot);
                EffectPlayer.Spawn(move.impactEffect, spot, Vector2.right, null, 0f, move.boltRadius * 2f);
                foreach (var other in Physics2D.OverlapCircleAll(spot, move.boltRadius))
                {
                    var target = other.GetComponentInParent<Combatant>();
                    if (target == null || target.team == team || !target.IsAlive) continue;
                    if (target.TakeHit(damage, move, out float dealt))
                    {
                        target.ApplyEffects(move, false, owner);
                        if (owner != null) owner.HitLanded(move, dealt);
                    }
                }
            }
            return;
        }
        // Something falls from the sky so as to land right on the first hit.
        if (!fallen && !string.IsNullOrEmpty(move.fallEffect) && Time.time >= nextTick - move.fallTime)
        {
            fallen = true;
            SkyFall.Launch(move, transform.position, Mathf.Max(0.05f, nextTick - Time.time));
        }

        bool tick = damage > 0f && Time.time >= nextTick;
        if (tick)
        {
            nextTick += TickInterval;
            // The impact sheet covers the whole zone, or plays on each opponent hit (impactOnTargets).
            if (!move.impactOnTargets) EffectPlayer.Spawn(move.impactEffect, transform.position, Vector2.right, null, 0f, move.radius * 2f);
        }
        foreach (var other in Physics2D.OverlapCircleAll(transform.position, move.radius))
        {
            var target = other.GetComponentInParent<Combatant>();
            if (target == null || target.team == team) continue;
            target.ApplyEffects(move, true, owner);
            if (!tick) continue;
            if (move.impactOnTargets) EffectPlayer.Spawn(move.impactEffect, target.transform.position, Vector2.right);
            target.TakeHit(damage, move, out _);
        }
    }
}

/// Something falling from the sky onto a spot (Draco Météor): it comes down at a slant and vanishes on landing,
/// where the zone's impact takes over. Without its sheet, a disc in the colour of the move's type.
public class SkyFall : MonoBehaviour
{
    static readonly Vector2 Drop = new(-1.5f, 5f); // where it starts, relative to the spot (tiles)

    Vector2 from, to;
    float start, duration;

    public static void Launch(MoveDef move, Vector2 spot, float duration)
    {
        var go = new GameObject(move.name + " (chute)");
        Vector2 direction = -Drop.normalized;
        if (!EffectPlayer.Attach(go, move.fallEffect, direction))
        {
            go.transform.localScale = Vector3.one * move.radius;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.Circle;
            renderer.color = GameData.TypeColor(move.type);
            renderer.sortingOrder = 4;
        }
        var fall = go.AddComponent<SkyFall>();
        fall.from = spot + Drop;
        fall.to = spot;
        fall.start = Time.time;
        fall.duration = duration;
        go.transform.position = fall.from;
    }

    void Update()
    {
        float t = (Time.time - start) / duration;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = Vector2.Lerp(from, to, t * t); // speeds up as it falls
    }
}
