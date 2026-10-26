using System.Collections.Generic;
using UnityEngine;

/// Moves that change the user's body for a moment (realtime_moves.json "body", melee family):
/// leap (Plaquage: jumps onto a spot, untouchable in the air, area hit on landing), grab (Frappe Atlas: a dash that
/// seizes the first opponent, jumps with it and slams it down, with a shockwave), liquid (Acidarmure: untouchable,
/// faster, goes through opponents and poisons them), spin (Gyroballe: hurts what it runs into, harder against fast
/// opponents), lift (Psyko: raises the target, then throws it away, harder into a wall), counter (Coup Bas: on guard;
/// struck meanwhile, it blinks behind the attacker and hits hard, else a small blow ahead).
public partial class Combatant
{
    /// Who is striking right now (set around TakeHit by shots, strikes and zones): the opponent a counter answers.
    public static Combatant HitSource;

    MoveDef bodyMove;
    float bodyStart, bodyUntil, nextBodyTick;
    Vector2 bodyFrom, bodyTo;
    Combatant bodyTarget;
    Vector3 bodyScale;
    bool bodyThrown;
    readonly Dictionary<Combatant, float> bodyTouched = new(); // opponents hit by a spin or poisoned by the liquid, until when they are spared

    /// True while a body move is going on (no other move can start).
    public bool InBody => bodyMove != null;

    // Leap, grab and guard keep the user from walking; liquid and spin change its speed.
    bool BodyRoots => bodyMove != null && (bodyMove.body == "leap" || bodyMove.body == "grab" || bodyMove.body == "counter");
    float BodySpeedMul => bodyMove != null && (bodyMove.body == "liquid" || bodyMove.body == "spin") && bodyMove.bodySpeedMul > 0f
        ? bodyMove.bodySpeedMul : 1f;
    string BodyAnim => bodyMove == null ? null
        : bodyMove.body == "spin" ? "Rotate" // whatever its cast looked like (Gyroballe hops first)
        : !string.IsNullOrEmpty(bodyMove.castAnim) ? bodyMove.castAnim
        : bodyMove.body == "leap" || bodyMove.body == "grab" ? "Idle" : null; // the PMD Hop bounces on the spot: a still pose, the sprite swells instead

    // Untouchable in the air and while liquid.
    bool BodyUntouchable => bodyMove != null && (bodyMove.body == "liquid" || (bodyMove.body == "leap" && !bodyThrown)
        || (bodyMove.body == "grab" && bodyTarget != null));

    // Starts a body move from Melee; false when the move is not one.
    bool StartBody(MoveDef move, Vector2 direction)
    {
        if (string.IsNullOrEmpty(move.body)) return false;
        if (move.body == "lift")
        {
            StartLift(move, direction); // the user stays free: it can go on with other moves meanwhile
            return true;
        }
        bodyMove = move;
        bodyStart = Time.time;
        bodyUntil = Time.time + move.bodyTime;
        bodyTarget = null;
        bodyThrown = false;
        bodyTouched.Clear();
        bodyScale = transform.localScale;
        switch (move.body)
        {
            case "leap":
                bodyFrom = transform.position;
                // Onto the target, which it follows through the air (only a last-moment dodge escapes it).
                bodyTarget = AimTarget != null && AimTarget.IsAlive && Vector2.Distance(AimTarget.transform.position, bodyFrom) <= move.range + 0.7f
                    ? AimTarget : FindAimTarget(this, direction, SaveData.Settings.aimCone, move.range + 0.7f, SaveData.Settings.aimDistanceWeight);
                bodyTo = bodyTarget != null ? LeapSpot(bodyTarget.transform.position) : SafeLanding(bodyFrom, bodyFrom + direction * move.range);
                PassThroughOpponents();
                break;
            case "grab":
                StartLunge(move, direction); // the grab happens when the dash meets someone (GrabFirst)
                bodyUntil = Time.time + LungeTime(move) + 0.05f;
                break;
            case "liquid":
                PassThroughOpponents();
                break;
        }
        return true;
    }

    const float LeapTrackShare = 0.85f; // share of the jump during which the landing spot follows the target

    // Just short of the target (bodies landing on top of each other fly apart), and short of walls.
    Vector2 LeapSpot(Vector2 target)
    {
        Vector2 to = target - bodyFrom;
        return SafeLanding(bodyFrom, bodyFrom + (to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.right) * Mathf.Max(0f, to.magnitude - 0.7f));
    }

    // Where a leap can end: short of walls, on walkable ground.
    Vector2 SafeLanding(Vector2 from, Vector2 to)
    {
        Vector2 way = to - from;
        float distance = way.magnitude;
        if (distance < 0.01f) return from;
        foreach (var hit in Physics2D.RaycastAll(from, way / distance, distance))
            if (!hit.collider.isTrigger && hit.collider.GetComponentInParent<Combatant>() == null)
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.4f));
        return from + way.normalized * distance;
    }

    // Called from FixedUpdate.
    void UpdateBody()
    {
        UpdateLift();
        if (bodyMove == null) return;
        float t = Mathf.Clamp01((Time.time - bodyStart) / Mathf.Max(0.01f, bodyMove.bodyTime));
        switch (bodyMove.body)
        {
            case "leap":
            {
                // Arc through the air (the sprite swells as it rises), then the landing hits all around. The landing
                // spot follows the target until the last moment of the jump.
                if (bodyTarget != null && bodyTarget.IsAlive && t < LeapTrackShare) bodyTo = LeapSpot(bodyTarget.transform.position);
                Vector2 at = Vector2.Lerp(bodyFrom, bodyTo, t);
                if (body != null) body.position = at;
                transform.localScale = bodyScale * (1f + 0.35f * Mathf.Sin(Mathf.PI * t));
                if (t < 1f) return;
                bodyThrown = true;
                Strike(bodyMove, Vector2.right, bodyMove.bodyRadius);
                CameraShake(0.15f);
                EndBody();
                return;
            }
            case "grab":
            {
                if (bodyTarget == null)
                {
                    if (Time.time >= bodyUntil && !IsLunging) EndBody(); // the dash met nobody
                    return;
                }
                if (!bodyTarget.IsAlive)
                {
                    EndBody();
                    return;
                }
                // Both rise, the target held just above the user, then crash down.
                transform.localScale = bodyScale * (1f + 0.6f * Mathf.Sin(Mathf.PI * t));
                bodyTarget.transform.localScale = bodyTarget.bodyScale * (1f + 0.4f * Mathf.Sin(Mathf.PI * t));
                bodyTarget.transform.position = transform.position + Vector3.up * (0.6f + 2.2f * Mathf.Sin(Mathf.PI * t));
                if (t < 1f) return;
                bodyTarget.transform.position = transform.position + Vector3.up * 0.4f;
                HitSource = this;
                if (bodyTarget.TakeHit(DamageOf(bodyMove) * Mathf.Max(1f, bodyMove.counterDamage), bodyMove, out float dealt, hitCap))
                {
                    bodyTarget.Stun(StunOf(bodyMove) + 0.4f);
                    bodyTarget.ApplyEffects(bodyMove, false, this);
                    HitLanded(bodyMove, dealt);
                }
                HitSource = null;
                // The shockwave hurts and pushes the others around the impact.
                foreach (var other in Physics2D.OverlapCircleAll(transform.position, bodyMove.bodyRadius))
                {
                    var c = other.GetComponentInParent<Combatant>();
                    if (c == null || c == bodyTarget || c.team == team || !c.IsAlive) continue;
                    HitSource = this;
                    if (c.TakeHit(DamageOf(bodyMove), bodyMove, out float wave, hitCap)) HitLanded(bodyMove, wave);
                    HitSource = null;
                    Vector2 away = c.transform.position - transform.position;
                    c.Knockback((away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right) * 1.2f / KnockbackTime, KnockbackTime);
                }
                EffectPlayer.Spawn(bodyMove.impactEffect, transform.position, Vector2.right, null, 0f, bodyMove.bodyRadius * 2f);
                CameraShake(0.2f);
                EndBody();
                return;
            }
            case "liquid":
            case "spin":
                TouchOpponents();
                if (Time.time >= bodyUntil) EndBody();
                return;
            case "counter":
                if (Time.time < bodyUntil) return;
                // Nobody struck: a small blow ahead.
                var aim = LiveAim != Vector2.zero ? LiveAim.normalized : (Vector2)transform.right;
                Strike(bodyMove, aim);
                EndBody();
                return;
        }
    }

    // A spin hurts what it runs into (harder against faster opponents); the liquid poisons what it goes through.
    void TouchOpponents()
    {
        foreach (var other in Physics2D.OverlapCircleAll(transform.position, bodyMove.bodyRadius))
        {
            var c = other.GetComponentInParent<Combatant>();
            if (c == null || c.team == team || !c.IsAlive) continue;
            if (bodyTouched.TryGetValue(c, out float spared) && Time.time < spared) continue;
            bodyTouched[c] = Time.time + Mathf.Max(0.1f, bodyMove.bodyTick);
            if (bodyMove.body == "liquid")
            {
                c.ApplyEffects(bodyMove, true, this);
                EffectPlayer.Spawn(bodyMove.impactEffect, c.transform.position, Vector2.right);
                continue;
            }
            // Gyro Ball: the slower the user next to its target, the harder it hits.
            float factor = Mathf.Clamp(c.moveSpeed / Mathf.Max(0.1f, moveSpeed), 0.6f, 2f);
            HitSource = this;
            if (c.TakeHit(DamageOf(bodyMove) * factor, bodyMove, out float dealt, hitCap))
            {
                c.Stun(StunOf(bodyMove));
                HitLanded(bodyMove, dealt);
            }
            HitSource = null;
            Vector2 away = c.transform.position - transform.position;
            c.Knockback((away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right) * 1f / KnockbackTime, KnockbackTime);
            EffectPlayer.Spawn(bodyMove.impactEffect, c.transform.position, away);
        }
    }

    // Psyko, apart from the other body moves (the user keeps acting): the target is raised, swelling, then thrown
    // away from the user; a wall on its way hurts it more.
    MoveDef liftMove;
    Combatant liftTarget;
    float liftStart, liftUntil;
    bool liftThrown;

    void StartLift(MoveDef move, Vector2 direction)
    {
        EndLift();
        liftTarget = AimTarget != null && AimTarget.IsAlive && Vector2.Distance(AimTarget.transform.position, transform.position) <= move.range
            ? AimTarget : FindAimTarget(this, direction, SaveData.Settings.aimCone, move.range, SaveData.Settings.aimDistanceWeight);
        if (liftTarget == null) return; // nobody to lift: nothing happens
        liftMove = move;
        liftStart = Time.time;
        liftUntil = Time.time + move.bodyTime;
        liftThrown = false;
        liftTarget.Stun(move.bodyTime + 0.1f);
        liftTarget.bodyScale = liftTarget.transform.localScale;
    }

    void UpdateLift()
    {
        if (liftMove == null) return;
        if (liftTarget == null || !liftTarget.IsAlive)
        {
            EndLift();
            return;
        }
        if (!liftThrown)
        {
            float t = Mathf.Clamp01((Time.time - liftStart) / Mathf.Max(0.01f, liftMove.bodyTime));
            liftTarget.transform.localScale = liftTarget.bodyScale * (1f + 0.3f * t);
            if (t < 1f) return;
            liftTarget.transform.localScale = liftTarget.bodyScale;
            Throw();
            liftThrown = true;
            liftUntil = Time.time + 0.45f;
            return;
        }
        if (Time.time >= liftUntil) EndLift();
    }

    void EndLift()
    {
        if (liftMove == null) return;
        if (liftTarget != null && !liftThrown) liftTarget.transform.localScale = liftTarget.bodyScale;
        liftMove = null;
        liftTarget = null;
    }

    void Throw()
    {
        Vector2 away = liftTarget.transform.position - transform.position;
        Vector2 direction = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;
        float distance = Mathf.Max(0.5f, liftMove.throwDistance);
        bool wall = false;
        foreach (var hit in Physics2D.RaycastAll(liftTarget.transform.position, direction, distance))
            if (!hit.collider.isTrigger && hit.collider.GetComponentInParent<Combatant>() == null) { wall = true; break; }
        HitSource = this;
        float damage = DamageOf(liftMove) * (wall ? Mathf.Max(1f, liftMove.wallDamage) : 1f);
        if (liftTarget.TakeHit(damage, liftMove, out float dealt, hitCap))
        {
            liftTarget.ApplyEffects(liftMove, false, this);
            HitLanded(liftMove, dealt);
            if (wall) liftTarget.Stun(0.8f);
        }
        HitSource = null;
        liftTarget.Knockback(direction * distance / 0.4f, 0.4f);
        EffectPlayer.Spawn(liftMove.impactEffect, liftTarget.transform.position, direction);
    }

    // Frappe Atlas: the dash seizes the first opponent it meets.
    bool GrabFirst()
    {
        if (bodyMove == null || bodyMove.body != "grab" || bodyTarget != null) return false;
        Vector2 centre = (Vector2)transform.position + lungeDirection * lungeMove.range;
        foreach (var other in Physics2D.OverlapCircleAll(centre, lungeMove.radius))
        {
            var c = other.GetComponentInParent<Combatant>();
            if (c == null || c.team == team || !c.IsAlive) continue;
            bodyTarget = c;
            c.bodyScale = c.transform.localScale;
            lungeUntil = 0f; // the dash stops here
            if (body != null) body.linearVelocity = Vector2.zero;
            bodyStart = Time.time;
            bodyUntil = Time.time + bodyMove.bodyTime;
            c.Stun(bodyMove.bodyTime + 0.2f);
            return true;
        }
        return false;
    }

    // TakeHit: untouchable states, and Coup Bas answering the blow. True when the hit is ignored.
    bool BodyIgnoresHit()
    {
        if (BodyUntouchable) return true;
        if (bodyMove == null || bodyMove.body != "counter") return false;
        var attacker = HitSource != null && HitSource.IsAlive && HitSource.team != team ? HitSource : NearestOpponent(8f);
        if (attacker == null) return false;
        // Behind the attacker, then a heavy blow.
        Vector2 side = attacker.transform.position - transform.position;
        Vector2 behind = (Vector2)attacker.transform.position + (side.sqrMagnitude > 0.0001f ? side.normalized : Vector2.right) * 0.8f;
        EffectPlayer.Spawn(bodyMove.trailEffect, transform.position, side, null, 0.25f);
        if (body != null) body.position = behind;
        transform.position = behind;
        var move = bodyMove;
        EndBody();
        var previous = HitSource;
        HitSource = this;
        EffectPlayer.Spawn(move.strikeEffect, attacker.transform.position, -side);
        if (attacker.TakeHit(DamageOf(move) * Mathf.Max(1f, move.counterDamage), move, out float dealt, hitCap))
        {
            attacker.Stun(0.8f);
            attacker.ApplyEffects(move, false, this);
            HitLanded(move, dealt);
            // Sent flying away from the user, now behind it (throwDistance tiles).
            if (move.throwDistance > 0f)
            {
                Vector2 away = (Vector2)attacker.transform.position - behind;
                const float FlyTime = 0.5f;
                attacker.Knockback((away.sqrMagnitude > 0.0001f ? away.normalized : -side.normalized) * move.throwDistance / FlyTime, FlyTime);
            }
        }
        HitSource = previous;
        return true; // the blow it answered does not land
    }

    Combatant NearestOpponent(float range)
    {
        Combatant best = null;
        float bestDistance = range;
        foreach (var c in all)
        {
            if (c.team == team || !c.IsAlive) continue;
            float d = Vector2.Distance(c.transform.position, transform.position);
            if (d < bestDistance) { best = c; bestDistance = d; }
        }
        return best;
    }

    void EndBody()
    {
        if (bodyMove == null) return;
        if (bodyMove.body == "leap" || bodyMove.body == "grab" || bodyMove.body == "liquid") EndDodge();
        transform.localScale = bodyScale;
        if (bodyMove.body == "grab" && bodyTarget != null) bodyTarget.transform.localScale = bodyTarget.bodyScale;
        bodyMove = null;
        bodyTarget = null;
    }

    static void CameraShake(float strength) => CameraFollow.Shake(strength, 0.3f);
}
