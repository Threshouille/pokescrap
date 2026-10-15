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

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    public static void Spawn(Combatant owner, MoveDef move, Vector2 centre)
    {
        var go = new GameObject(move.name);
        go.transform.position = centre;

        // Without a sprite sheet the zone falls back to a translucent disc in the colour of its type.
        if (!EffectPlayer.Attach(go, move.effect, Vector2.right))
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
        zone.dieAt = Time.time + (move.zoneDuration > 0f ? move.zoneDuration : DefaultDuration);
        zone.damage = owner.DamageOf(move);
        zone.nextTick = Time.time + FirstTick;
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

    /// Direction that leaves the harmful zone the point is in (straight away from its centre), or zero outside.
    public static Vector2 EscapeDirection(Vector2 point, Team victim, float margin)
    {
        foreach (var zone in active)
        {
            if (zone.move == null || zone.team == victim) continue;
            Vector2 away = point - (Vector2)zone.transform.position;
            if (away.magnitude > zone.move.radius + margin) continue;
            return away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;
        }
        return Vector2.zero;
    }

    void FixedUpdate()
    {
        if (Time.time >= dieAt)
        {
            Destroy(gameObject);
            return;
        }
        bool tick = damage > 0f && Time.time >= nextTick;
        if (tick) nextTick += TickInterval;
        foreach (var other in Physics2D.OverlapCircleAll(transform.position, move.radius))
        {
            var target = other.GetComponentInParent<Combatant>();
            if (target == null || target.team == team) continue;
            target.ApplyEffects(move, true, owner);
            if (tick) target.TakeHit(damage, move.category, out _);
        }
    }
}
