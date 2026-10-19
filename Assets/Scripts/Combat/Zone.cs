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

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    /// delay: seconds added before the first hit and to the zone's life (zones laid one after the other).
    public static void Spawn(Combatant owner, MoveDef move, Vector2 centre, float delay = 0f)
    {
        var go = new GameObject(move.name);
        go.transform.position = centre;

        // Without a sprite sheet the zone falls back to a translucent disc in the colour of its type.
        if (EffectPlayer.Attach(go, move.effect, Vector2.right))
        {
            if (move.effectScale > 0f) go.transform.localScale = Vector3.one * move.effectScale;
        }
        else
        {
            go.transform.localScale = Vector3.one * move.radius * 2f;
            var renderer = go.AddComponent<SpriteRenderer>();
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

    void FixedUpdate()
    {
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
