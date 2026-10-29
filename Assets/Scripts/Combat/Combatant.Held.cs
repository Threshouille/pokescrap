using UnityEngine;

/// Moves that last while their button is held (realtime_moves.json: channel, chargeMax, link):
/// a stream of shots (Lance-Flamme), a charged shot released with the button (Ball'Ombre), a draining link to the
/// target (Méga-Sangsue). An AI holds them for aiChannel / aiCharge seconds, or the whole link.
public partial class Combatant
{
    /// Buttons of the move slots held right now by the player controlling this Pokémon (PlayerController).
    public readonly bool[] HeldButtons = new bool[MoveSlots];
    /// True while a player controls this Pokémon: held moves stop when their button is released.
    public bool PlayerDriven { get; set; }

    MoveDef channelMove, chargeMove, linkMove;
    int heldSlot;
    float heldStart, heldUntil, nextHeldTick;
    Vector2 heldAim;
    GameObject chargeOrb;
    Vector3 chargeOrbScale;
    Combatant linkTarget;
    LineRenderer linkLine; // without a sheet
    SpriteRenderer linkSprite; // the move's effect sheet, repeated along the link
    float linkAnimTime;

    /// True while a held move is going on (no other move can start).
    public bool Holding => channelMove != null || chargeMove != null || linkMove != null;

    float HeldSpeedMul => channelMove != null && channelMove.channelSpeedMul > 0f ? channelMove.channelSpeedMul
        : linkMove != null && linkMove.channelSpeedMul > 0f ? linkMove.channelSpeedMul : 1f;

    string HeldAnim
    {
        get
        {
            var move = channelMove ?? chargeMove ?? linkMove;
            if (move == null) return null;
            if (move == chargeMove && !string.IsNullOrEmpty(move.castAnim)) return move.castAnim;
            return string.IsNullOrEmpty(move.anim) ? "Shoot" : move.anim;
        }
    }

    bool ButtonUp => PlayerDriven && !HeldButtons[heldSlot];

    // Starts a held move; false when the move is not one (TryUseMove goes on). "refused": no target for a link.
    bool StartHeld(int slot, MoveDef move, Vector2 direction, out bool refused)
    {
        refused = false;
        if (move.channel <= 0f && move.chargeMax <= 0f && move.link <= 0f) return false;
        heldSlot = slot;
        heldStart = Time.time;
        heldAim = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        if (move.channel > 0f)
        {
            channelMove = move;
            heldUntil = Time.time + (!PlayerDriven && move.aiChannel > 0f ? move.aiChannel : move.channel);
            nextHeldTick = Time.time;
        }
        else if (move.chargeMax > 0f)
        {
            chargeMove = move;
            ShowChargeOrb(move);
        }
        else
        {
            var target = AimTarget != null && AimTarget.IsAlive && Vector2.Distance(AimTarget.transform.position, transform.position) <= move.range
                ? AimTarget
                : FindAimTarget(this, heldAim, SaveData.Settings.aimCone, move.range, SaveData.Settings.aimDistanceWeight);
            if (target == null)
            {
                refused = true; // nobody to drain: the move is not spent
                return true;
            }
            linkMove = move;
            linkTarget = target;
            heldUntil = Time.time + move.link;
            nextHeldTick = Time.time + move.linkInterval;
            ShowLink(move);
        }
        return true;
    }

    // Called from FixedUpdate: shots of a stream, growth of a charge, drains of a link.
    void UpdateHeld()
    {
        if (!Holding) return;
        if (!CanAttack)
        {
            StopHeld(false); // asleep, frozen...: a charge is lost
            return;
        }

        if (channelMove != null)
        {
            if (Time.time >= heldUntil || ButtonUp)
            {
                StopHeld(true);
                return;
            }
            while (Time.time >= nextHeldTick)
            {
                nextHeldTick += Mathf.Max(0.02f, channelMove.channelInterval);
                FireProjectiles(channelMove, Steer(CurrentAim()), -1f);
            }
            return;
        }

        if (chargeMove != null)
        {
            float charged = Mathf.Clamp01((Time.time - heldStart) / chargeMove.chargeMax);
            PlaceChargeOrb(charged);
            bool release = PlayerDriven ? ButtonUp : Time.time - heldStart >= (chargeMove.aiCharge > 0f ? chargeMove.aiCharge : chargeMove.chargeMax);
            if (release) ReleaseCharge(charged);
            return;
        }

        // Link: drains while the target stays close and the button is held.
        bool broken = linkTarget == null || !linkTarget.IsAlive
            || Vector2.Distance(linkTarget.transform.position, transform.position) > (linkMove.linkBreak > 0f ? linkMove.linkBreak : linkMove.range);
        if (broken || Time.time >= heldUntil || ButtonUp)
        {
            StopHeld(true);
            return;
        }
        PlaceLink();
        if (Time.time < nextHeldTick) return;
        nextHeldTick += linkMove.linkInterval;
        EffectPlayer.Spawn(linkMove.impactEffect, linkTarget.transform.position, Vector2.right);
        if (linkTarget.TakeHit(DamageOf(linkMove), linkMove, out float dealt, hitCap))
        {
            linkTarget.ApplyEffects(linkMove, false, this);
            float drained = dealt * (linkMove.drain > 0f ? linkMove.drain : 1f);
            if (dealt > 0f && DrainSign(linkTarget) < 0f) TakeIndirectDamage(drained); // Liquid Ooze
            else if (dealt > 0f) Heal(drained);
        }
    }

    // Where a held move aims now: the player's aim (toward the target, or where they point for a noAutoAim move),
    // else the AI's target, else the first direction.
    Vector2 CurrentAim()
    {
        var move = channelMove ?? chargeMove ?? linkMove;
        if (move != null && move.noAutoAim && RawAim != Vector2.zero) return RawAim.normalized;
        if (LiveAim != Vector2.zero) return LiveAim.normalized;
        var foe = FindAimTarget(this, heldAim, SaveData.Settings.aimCone, channelMove != null ? channelMove.range + 2f : 8f, SaveData.Settings.aimDistanceWeight);
        if (foe != null) heldAim = ((Vector2)(foe.transform.position - transform.position)).normalized;
        return heldAim;
    }

    void ReleaseCharge(float charged)
    {
        var move = chargeMove;
        Vector2 direction = Steer(CurrentAim());
        float distance = -1f;
        if (!move.noAutoAim) AutoAim(move, 1, ref direction, ref distance);
        float size = Mathf.Lerp(1f, move.chargeSize > 0f ? move.chargeSize : 1f, charged);
        float damage = Mathf.Lerp(1f, move.chargeDamage > 0f ? move.chargeDamage : 1f, charged);
        Projectile.Spawn(this, move, direction, 0, 0f, -1f, 0f, null, null, size, damage);
        StopHeld(true);
    }

    // Ends the held move; its cooldown starts now (spent) or right away (lost charge, it was not used).
    void StopHeld(bool spent)
    {
        var move = channelMove ?? chargeMove ?? linkMove;
        if (move == null) return;
        readyAt[heldSlot] = Time.time + (spent ? CooldownOf(move) : 0f);
        channelMove = chargeMove = linkMove = null;
        linkTarget = null;
        if (chargeOrb != null) Destroy(chargeOrb);
        if (linkLine != null) Destroy(linkLine.gameObject);
        if (linkSprite != null) Destroy(linkSprite.gameObject);
    }

    // The ball growing in front of the user while it charges (its flight sheet, or a disc of the type's colour).
    void ShowChargeOrb(MoveDef move)
    {
        chargeOrb = new GameObject(move.name + " (charge)");
        if (EffectPlayer.Attach(chargeOrb, move.effect, Vector2.right))
        {
            if (move.effectScale > 0f) chargeOrb.transform.localScale = Vector3.one * move.effectScale;
        }
        else
        {
            chargeOrb.transform.localScale = Vector3.one * move.radius * 2f;
            var renderer = chargeOrb.AddComponent<SpriteRenderer>();
            Unlit.Apply(renderer);
            renderer.sprite = Shapes.Circle;
            renderer.color = GameData.TypeColor(move.type);
            renderer.sortingOrder = 2;
        }
        chargeOrbScale = chargeOrb.transform.localScale;
        PlaceChargeOrb(0f);
    }

    void PlaceChargeOrb(float charged)
    {
        if (chargeOrb == null) return;
        Vector2 aim = CurrentAim();
        chargeOrb.transform.position = transform.position + (Vector3)(aim * 0.6f);
        float size = Mathf.Lerp(0.6f, chargeMove.chargeSize > 0f ? chargeMove.chargeSize : 1f, charged);
        // Full charge: it throbs to say so.
        if (charged >= 1f) size *= 1f + 0.08f * Mathf.Sin(Time.time * 14f);
        chargeOrb.transform.localScale = chargeOrbScale * size;
    }

    // The link between the user and its target: the move's sheet repeated along it (a line of the type's colour
    // without one), animated by hand since a tiled sprite cannot play through EffectPlayer.
    void ShowLink(MoveDef move)
    {
        var go = new GameObject(move.name + " (lien)");
        var sheet = EffectLibrary.Get(move.effect);
        if (sheet != null)
        {
            linkSprite = go.AddComponent<SpriteRenderer>();
            linkSprite.sprite = sheet.Frame(0);
            linkSprite.drawMode = SpriteDrawMode.Tiled;
            linkSprite.sortingOrder = 2;
            Unlit.Apply(linkSprite);
            linkAnimTime = 0f;
        }
        else
        {
            linkLine = go.AddComponent<LineRenderer>();
            linkLine.material = new Material(Shader.Find("Sprites/Default"));
            var colour = GameData.TypeColor(move.type);
            linkLine.startColor = linkLine.endColor = colour;
            linkLine.startWidth = 0.14f;
            linkLine.endWidth = 0.08f;
            linkLine.positionCount = 2;
            linkLine.sortingOrder = 2;
        }
        PlaceLink();
    }

    void PlaceLink()
    {
        Vector2 from = transform.position, to = linkTarget.transform.position;
        if (linkLine != null)
        {
            linkLine.SetPosition(0, from);
            linkLine.SetPosition(1, to);
            return;
        }
        if (linkSprite == null) return;
        var sheet = EffectLibrary.Get(linkMove.effect);
        Vector2 along = to - from;
        var t = linkSprite.transform;
        t.position = (from + to) / 2f;
        t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
        linkSprite.size = new Vector2(along.magnitude, sheet.frame_height / (float)EffectLibrary.PixelsPerUnit);
        // Frames in turn, with their own durations.
        linkAnimTime += Time.fixedDeltaTime * 1000f;
        float total = 0f;
        foreach (int ms in sheet.frame_duration_ms) total += Mathf.Max(1, ms);
        float at = linkAnimTime % Mathf.Max(1f, total);
        int frame = 0;
        for (; frame < sheet.frame_count - 1; frame++)
        {
            at -= Mathf.Max(1, sheet.frame_duration_ms[frame]);
            if (at < 0f) break;
        }
        linkSprite.sprite = sheet.Frame(frame);
    }
}
