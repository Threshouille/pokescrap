using System.Collections.Generic;
using UnityEngine;

/// A shot of the "projectile" move family. It follows a path (straight, wave, zigzag, curve, orbit, arc, boomerang,
/// homing) and hits the first opponent or wall, unless it pierces, bounces or flies over them (arc).
public class Projectile : MonoBehaviour
{
    const float SpawnOffset = 0.4f; // tiles in front of the shooter
    const float HomingRange = 5f; // tiles: how far a homing shot looks for a target
    const float MinSpeed = 1f; // tiles per second: a slowing shot never stops

    static readonly List<Projectile> active = new();

    Team team;
    Combatant owner;
    int level; // the level of ground it was shot from: it flies over the cliffs no higher (DungeonGenerator.ShotPasses)
    MoveDef move;
    float damage; // raw, before the target's defence
    float radius, baseRadius;
    Vector3 baseScale;
    float speed;
    float range; // tiles the path covers (arc: up to the landing point)
    float side; // +1 / -1: which way a curve bends, where an orbit starts
    float delay; // seconds before the shot leaves (bursts)
    float age; // seconds in flight (pulse)
    Vector2 aimAtSpawn; // the shooter's live aim when the shot was created (zero for an AI)
    float hold; // seconds left waiting in place, harmless (move.hold)
    Combatant lockedTarget; // a waiting shot leaves toward it

    // The path is an offset around a point moving straight ahead from "origin" along "forward".
    Vector2 origin, forward;
    float travelled; // tiles covered along "forward"
    bool returning; // boomerang on its way back
    int bouncesLeft;
    Combatant homingTarget;
    readonly HashSet<Combatant> hit = new();
    Renderer[] renderers;

    /// shotIndex: position in the volley (picks the side of curves and orbits); delay: seconds before leaving;
    /// distance: where the user aimed (arc lands there when closer than the range, ≤ 0 = full range).
    /// extraRange: tiles added to the range (shots of a volley landing further and further).
    /// at: where the shot appears (in front of the user by default); lockedTarget: where a waiting shot (move.hold) goes.
    public static void Spawn(Combatant owner, MoveDef move, Vector2 direction, int shotIndex = 0, float delay = 0f, float distance = -1f,
        float extraRange = 0f, Vector2? at = null, Combatant lockedTarget = null)
    {
        var go = new GameObject(move.name);
        go.transform.position = at ?? (Vector2)(owner.transform.position + (Vector3)(direction * SpawnOffset));

        // Without a sprite sheet the shot falls back to a plain disc in the colour of its type.
        if (EffectPlayer.Attach(go, move.effect, move.upright ? Vector2.right : direction))
        {
            if (move.effectScale > 0f) go.transform.localScale = Vector3.one * move.effectScale;
        }
        else
        {
            go.transform.localScale = Vector3.one * move.radius * 2f;
            var renderer = go.AddComponent<SpriteRenderer>();
            Unlit.Apply(renderer); // keeps its colours at night (DayNight)
            renderer.sprite = Shapes.Circle;
            renderer.color = GameData.TypeColor(move.type);
            renderer.sortingOrder = 2;
        }

        go.transform.localScale *= owner.projectileSizeMul; // "bigger projectiles" upgrades

        var projectile = go.AddComponent<Projectile>();
        projectile.team = owner.team;
        projectile.owner = owner;
        projectile.level = DungeonGenerator.Current != null && owner != null ? DungeonGenerator.Current.HeightAt(owner.transform.position) : 0;
        projectile.move = move;
        projectile.damage = owner.DamageOf(move);
        projectile.radius = projectile.baseRadius = move.radius * owner.projectileSizeMul;
        projectile.baseScale = go.transform.localScale;
        float weather = Climate.ShotMul(move, direction); // rain shortens Fire shots, a storm's wind carries or holds
        projectile.speed = move.speed * owner.projectileSpeedMul * weather;
        projectile.range = ((move.path == "arc" && distance > 0f ? Mathf.Min(distance, move.range) : move.range) + extraRange) * weather;
        projectile.side = shotIndex % 2 == 0 ? 1f : -1f;
        projectile.forward = direction.normalized;
        projectile.origin = go.transform.position;
        projectile.bouncesLeft = move.bounces;
        projectile.delay = delay;
        projectile.hold = move.hold;
        projectile.lockedTarget = lockedTarget;
        projectile.aimAtSpawn = owner.LiveAim;
        projectile.renderers = go.GetComponentsInChildren<Renderer>();
        if (delay > 0f) projectile.Show(false);
    }

    void Show(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
    }

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    /// Sideways direction that gets out of the way of an opponent's shot about to reach the point (within
    /// "lookahead" seconds), or zero. A shot in an arc is avoided by leaving the area where it will land.
    public static Vector2 Threat(Vector2 point, Team victim, float bodyRadius, float lookahead)
    {
        foreach (var shot in active)
        {
            if (shot.team == victim || shot.delay > 0f || shot.hold > 0f || shot.returning || shot.move == null) continue;
            if (shot.move.path == "arc")
            {
                Vector2 landing = shot.origin + shot.forward * shot.range;
                Vector2 away = point - landing;
                bool soon = (shot.range - shot.travelled) / Mathf.Max(shot.speed, MinSpeed) <= lookahead;
                if (soon && away.magnitude <= Mathf.Max(shot.radius, shot.move.splash) + bodyRadius)
                    return away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;
                continue;
            }
            Vector2 rel = point - (Vector2)shot.transform.position;
            float along = Vector2.Dot(rel, shot.forward);
            if (along < 0f || along > shot.speed * lookahead) continue;
            var normal = new Vector2(-shot.forward.y, shot.forward.x);
            float lateral = Vector2.Dot(rel, normal);
            if (Mathf.Abs(lateral) > shot.radius + bodyRadius) continue;
            return lateral >= 0f ? normal : -normal;
        }
        return Vector2.zero;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (delay > 0f)
        {
            // Waiting for its turn in a burst: leaves from where the user stands at that moment.
            delay -= dt;
            if (owner == null || !owner.IsAlive) { Destroy(gameObject); return; }
            if (delay > 0f) return;
            // A player's shot keeps its angle in the volley but follows where the player aims now.
            if (aimAtSpawn != Vector2.zero && owner.LiveAim != Vector2.zero)
            {
                float offset = Vector2.SignedAngle(aimAtSpawn, forward);
                forward = (Quaternion.Euler(0f, 0f, offset) * owner.LiveAim.normalized);
                if (!move.upright) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
            }
            origin = owner.transform.position + (Vector3)(forward * SpawnOffset);
            transform.position = origin;
            Show(true);
        }

        if (hold > 0f)
        {
            // Waits in place, then leaves toward the opponent targeted when the move started.
            hold -= dt;
            if (hold > 0f) return;
            if (lockedTarget != null && lockedTarget.IsAlive)
            {
                Vector2 toTarget = lockedTarget.transform.position - transform.position;
                if (toTarget.sqrMagnitude > 0.0001f) forward = toTarget.normalized;
                if (!move.upright) transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg);
            }
            origin = transform.position;
        }

        speed = Mathf.Max(MinSpeed, speed + move.accel * dt);
        if (move.path == "homing") Steer(dt);

        Vector2 from = transform.position;
        Vector2 to;
        bool done = false;
        if (returning)
        {
            // Boomerang: straight back to the user, through walls.
            if (owner == null) { Destroy(gameObject); return; }
            Vector2 home = owner.transform.position;
            to = Vector2.MoveTowards(from, home, speed * dt);
            done = to == home;
        }
        else
        {
            float step = Mathf.Min(speed * dt, range - travelled);
            travelled += step;
            to = PathPoint(travelled);
            done = travelled >= range;
        }

        if (move.growth > 0f || move.pulse > 0f)
        {
            float grown = move.growth > 0f ? Mathf.Lerp(1f, move.growth, Mathf.Clamp01(travelled / range)) : 1f;
            age += dt;
            if (move.pulse > 0f) grown *= 1f + move.pulse * Mathf.Sin(2f * Mathf.PI * age / (move.pulsePeriod > 0f ? move.pulsePeriod : 1f));
            radius = baseRadius * grown;
            transform.localScale = baseScale * grown;
        }

        if (move.path == "arc")
        {
            // Flies over everything and only hits where it lands; it is drawn higher up in the middle of its flight.
            float t = Mathf.Clamp01(travelled / range);
            transform.position = to + Vector2.up * (move.pathAmplitude * 4f * t * (1f - t));
            if (done) Land(to);
            return;
        }

        Terrain.OnTrail(move, from, to, owner);
        if (Obstacles.Capture(move, to, out var magnet))
        {
            // Caught by a magnet: the shot ends on it (and may charge it).
            EffectPlayer.Spawn(move.impactEffect, magnet, forward);
            Terrain.OnImpact(move, magnet, owner);
            Destroy(gameObject);
            return;
        }
        if (Sweep(from, to)) return;
        Vector2 moved = to - from;
        if (moved.sqrMagnitude > 0.000001f && !move.upright)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(moved.y, moved.x) * Mathf.Rad2Deg);
        transform.position = to;

        if (!done) return;
        if (!returning && move.path != "boomerang") Terrain.OnImpact(move, to, owner); // spent: lands where it stops
        if (move.path == "boomerang" && !returning)
        {
            returning = true;
            hit.Clear(); // can hit the same opponents again on the way back
        }
        else
        {
            var end = EffectLibrary.Get(move.endEffect);
            if (end != null) EffectPlayer.Spawn(move.endEffect, transform.position, Vector2.right, null, 0f,
                end.frame_width / (float)EffectLibrary.PixelsPerUnit * transform.localScale.x);
            Destroy(gameObject);
        }
    }

    /// Point of the path after "distance" tiles along "forward".
    Vector2 PathPoint(float distance)
    {
        Vector2 normal = new Vector2(-forward.y, forward.x);
        float a = move.pathAmplitude, period = move.pathPeriod > 0f ? move.pathPeriod : 2f;
        float cycle = distance / period;
        float sideways = 0f, ahead = 0f;
        switch (move.path)
        {
            case "wave": sideways = a * Mathf.Sin(2f * Mathf.PI * cycle); break;
            case "zigzag": sideways = a * (1f - 4f * Mathf.Abs(Mathf.Repeat(cycle + 0.25f, 1f) - 0.5f)); break;
            case "curve": sideways = side * a * Mathf.Sin(Mathf.PI * Mathf.Clamp01(distance / range)); break;
            case "orbit":
                // Shots of a volley turn around a point moving ahead, evenly spaced (two shots: face to face).
                float angle = 2f * Mathf.PI * cycle + (side > 0f ? 0f : Mathf.PI);
                sideways = a * Mathf.Sin(angle);
                ahead = a * (Mathf.Cos(angle) - Mathf.Cos(side > 0f ? 0f : Mathf.PI));
                break;
        }
        return origin + forward * (distance + ahead) + normal * sideways;
    }

    // Homing: turns toward the nearest opponent in front of it.
    void Steer(float dt)
    {
        // A player's shot homes in on the aimed target.
        if (owner != null && owner.AimTarget != null && owner.AimTarget.IsAlive) homingTarget = owner.AimTarget;
        if (homingTarget == null || !homingTarget.IsAlive)
        {
            homingTarget = null;
            float best = HomingRange;
            foreach (var c in FindObjectsByType<Combatant>())
            {
                if (c.team == team || !c.IsAlive) continue;
                Vector2 to = c.transform.position - transform.position;
                if (to.magnitude < best && Vector2.Dot(to.normalized, forward) > 0.3f)
                {
                    best = to.magnitude;
                    homingTarget = c;
                }
            }
        }
        if (homingTarget == null) return;
        Vector2 wanted = ((Vector2)homingTarget.transform.position - (Vector2)transform.position).normalized;
        float turn = Mathf.Clamp(Vector2.SignedAngle(forward, wanted), -move.homing * dt, move.homing * dt);
        // The path restarts as a straight line through the current position, in the new direction.
        forward = (Vector2)(Quaternion.Euler(0f, 0f, turn) * forward);
        origin = (Vector2)transform.position - forward * travelled;
    }

    // The whole move is swept with a circle cast, so a fast shot or a slow frame can never skip over a target.
    // Returns true when the shot is gone or its move for this step is over (bounce, boomerang turning back).
    bool Sweep(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length <= 0f) return false;
        foreach (var h in Physics2D.CircleCastAll(from, radius, delta / length, length))
        {
            if (h.collider.isTrigger) continue;
            var target = h.collider.GetComponentInParent<Combatant>();
            Vector2 at = h.distance > 0f ? h.point : from; // a cast that starts inside a collider has no usable point
            if (target != null)
            {
                if (target.team == team || !hit.Add(target)) continue;
                Strike(target);
                EffectPlayer.Spawn(move.impactEffect, at, delta);
                if (move.pierce || move.path == "boomerang") continue;
                Terrain.OnImpact(move, at, owner);
                Destroy(gameObject);
                return true;
            }

            // A wall: a cliff no higher than where it was shot from is flown over.
            if (returning) continue;
            if (FliesOver(h, from, delta / length)) continue;
            if (move.path == "boomerang")
            {
                transform.position = from;
                returning = true;
                hit.Clear();
                return true;
            }
            if (bouncesLeft > 0 && h.distance > 0f)
            {
                bouncesLeft--;
                forward = Vector2.Reflect(forward, h.normal).normalized;
                origin = h.centroid + h.normal * 0.01f;
                range -= travelled;
                travelled = 0f;
                transform.position = origin;
                return true;
            }
            EffectPlayer.Spawn(move.impactEffect, at, delta);
            Terrain.OnImpact(move, at, owner);
            Destroy(gameObject);
            return true;
        }
        return false;
    }

    // The wall hit is a cliff no higher than the level it was shot from.
    bool FliesOver(RaycastHit2D h, Vector2 from, Vector2 direction)
    {
        var grid = DungeonGenerator.Current;
        if (grid == null) return false;
        Vector2 p;
        if (h.distance > 0f) p = h.point + direction * 0.05f;
        else
        {
            // Already touching it: the nearest point of the wall, a little inside.
            Vector2 near = h.collider.ClosestPoint(from);
            p = near == from ? from : near + (near - from).normalized * 0.05f;
        }
        var cell = grid.CellOf(p);
        return grid.ShotPasses(cell.x, cell.y, level);
    }

    // Arc: hits every opponent within the splash radius where it lands.
    void Land(Vector2 at)
    {
        float area = Mathf.Max(radius, move.splash);
        if (move.puddle > 0f)
            Shapes.Puddle(at, area, ColorUtility.TryParseHtmlString(move.puddleColor, out var tint) ? tint : GameData.TypeColor(move.type), move.puddle, EffectLibrary.Get(move.mark)?.Frame(move.markFrame), move.markFill);
        EffectPlayer.Spawn(move.impactEffect, at, forward);
        Terrain.OnImpact(move, at, owner);
        foreach (var c in Physics2D.OverlapCircleAll(at, area))
        {
            if (c.isTrigger) continue;
            var target = c.GetComponentInParent<Combatant>();
            if (target != null && target.team != team && hit.Add(target)) Strike(target);
        }
        Destroy(gameObject);
    }

    void Strike(Combatant target)
    {
        if (!target.TakeHit(damage, move, out float dealt)) return;
        if (move.stun > 0f && (move.stunChance <= 0f || Random.value < move.stunChance)) target.Stun(move.stun);
        target.ApplyEffects(move, false, owner);
        if (owner != null) owner.HitLanded(move, dealt);
    }
}
