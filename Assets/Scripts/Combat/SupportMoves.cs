using System.Collections.Generic;
using UnityEngine;

/// Status moves turned into things laid in the arena (realtime_moves.json, families "barrier", "decoy", "trap",
/// "wish"; Abri is a held move, Combatant.Held): a wall that sends special shots back (Mur Lumière) or stops the
/// opponents (Protection), a decoy that runs away (Clonage), spikes on the ground (Picots), a star that heals where
/// it falls (Vœu). Worth: when an AI uses them (RoleTactics).
public static class SupportMoves
{
    public static bool IsSupport(MoveDef move) => move != null
        && (move.family == "barrier" || move.family == "decoy" || move.family == "trap" || move.family == "wish");

    /// Lays the move toward a direction; false when it cannot be used now (the move is not spent).
    public static bool Cast(Combatant user, MoveDef move, Vector2 direction, float distance)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction = direction.normalized;
        switch (move.family)
        {
            case "barrier":
                Barrier.Spawn(user, move, direction);
                return true;
            case "decoy":
                return Decoy.Spawn(user, move, direction);
            case "trap":
                LaySpikes(user, move, direction);
                return true;
            case "wish":
                float reach = distance > 0f ? Mathf.Min(distance, move.range) : move.range;
                Wish.Spawn(user, move, (Vector2)user.transform.position + direction * reach);
                return true;
        }
        return false;
    }

    // Picots: spikes thrown in an arc in front of the user, each a patch of the move's terrain (terrains.json).
    static void LaySpikes(Combatant user, MoveDef move, Vector2 direction)
    {
        if (move.trapAround)
        {
            // Everywhere within range of the user.
            Terrain.PaintArea(move.trapTerrain, user.transform.position, move.range, move.zoneDuration, user);
            for (int i = 0; i < 8; i++) // puffs of dust here and there as they land
                EffectPlayer.Spawn(move.impactEffect, (Vector2)user.transform.position + Random.insideUnitCircle * move.range, Vector2.right);
            return;
        }
        int count = Mathf.Max(1, move.count);
        for (int i = 0; i < count; i++)
        {
            float angle = count == 1 ? 0f : Mathf.Lerp(-move.spread / 2f, move.spread / 2f, i / (count - 1f));
            float reach = Random.Range(move.lineStart > 0f ? move.lineStart : 1f, move.range);
            Vector2 at = (Vector2)user.transform.position + (Vector2)(Quaternion.Euler(0f, 0f, angle) * direction) * reach;
            Terrain.PaintArea(move.trapTerrain, at, move.radius, move.zoneDuration, user);
            EffectPlayer.Spawn(move.impactEffect, at, Vector2.right);
        }
    }

    /// For an AI: whether this support move is worth using now, and where.
    public static bool Worth(Combatant self, MoveDef move, Combatant foe, float foeDistance, Vector2 toFoe, out Vector2 aim, out float distance)
    {
        aim = toFoe == Vector2.zero ? Vector2.right : toFoe;
        distance = foeDistance;
        Vector2 position = self.transform.position;
        switch (move.family)
        {
            case "barrier":
                if (foe == null || Barrier.Has(self.team, move)) return false;
                // Mur Lumière against a shooter, Protection against one coming to close range.
                bool shooter = foe.Role != "melee";
                return move.reflectSpecial ? shooter && foeDistance <= 7f && foeDistance > 1.5f : !shooter && foeDistance <= 3.5f && foeDistance > 1.3f;
            case "decoy":
                return foe != null && foeDistance <= 5f && !Decoy.Has(self);
            case "trap":
                return foe != null && (move.trapAround ? foeDistance <= move.range : foeDistance <= move.range + 1f && foeDistance > 1f);
            case "wish":
                var wounded = Combatant.MostWounded(self.team, position, move.range + 3f, 0.6f);
                if (wounded == null || Wish.Pending(self.team)) return false;
                Vector2 toWounded = (Vector2)wounded.transform.position - position;
                aim = toWounded == Vector2.zero ? Vector2.right : toWounded;
                distance = Mathf.Min(toWounded.magnitude, move.range);
                return true;
        }
        return false;
    }

    /// For an AI under threat whose dodge is not ready: Abri if it has it ready (held aiChannel seconds).
    public static bool TryGuard(Combatant self, Vector2 direction)
    {
        for (int slot = 0; slot < Combatant.MoveSlots; slot++)
        {
            var move = self.Moves[slot];
            if (move == null || move.guard <= 0f || self.CooldownLeft(slot) > 0f) continue;
            return self.TryUseMove(slot, direction == Vector2.zero ? Vector2.right : direction);
        }
        return false;
    }
}

/// Mur Lumière / Protection: a straight wall across the aim, for a while. Mur Lumière sends the opponents' special
/// shots back the other way, now theirs to suffer (each one wears it a little); Protection stops their physical
/// shots and keeps the opponents off, stopping a dash short and pushing back whoever charges it.
public class Barrier : MonoBehaviour
{
    static readonly List<Barrier> active = new();
    const float Thickness = 0.3f; // tiles, for the bodies it holds back

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => active.Clear();

    Combatant owner;
    Team team;
    MoveDef move;
    Vector2 centre, along, normal;
    float half, until;

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    public static bool Has(Team team, MoveDef move) => active.Exists(b => b != null && b.team == team && b.move == move);

    public static void Spawn(Combatant owner, MoveDef move, Vector2 direction)
    {
        // One wall of a kind per user: a new one replaces the old.
        foreach (var old in active.ToArray())
            if (old != null && old.owner == owner && old.move == move) Destroy(old.gameObject);
        var go = new GameObject(move.name);
        var wall = go.AddComponent<Barrier>();
        wall.owner = owner;
        wall.team = owner.team;
        wall.move = move;
        wall.normal = direction;
        wall.along = new Vector2(-direction.y, direction.x);
        wall.half = Mathf.Max(0.5f, move.wallLength / 2f);
        wall.centre = (Vector2)owner.transform.position + direction * move.range;
        wall.until = Time.time + move.zoneDuration;
        go.transform.position = wall.centre;
        // The sheet is drawn lying across, pointing right: turned to stand across the aim, stretched along it.
        if (EffectPlayer.Attach(go, move.effect, wall.along))
        {
            var sheet = EffectLibrary.Get(move.effect);
            float tiles = sheet.frame_width / (float)EffectLibrary.PixelsPerUnit;
            go.transform.localScale = new Vector3(move.wallLength / tiles, 1f, 1f);
        }
        else
        {
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = Shapes.Square;
            var c = GameData.TypeColor(move.type);
            c.a = 0.5f;
            r.color = c;
            r.sortingOrder = 2;
            Unlit.Apply(r);
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(wall.along.y, wall.along.x) * Mathf.Rad2Deg);
            go.transform.localScale = new Vector3(move.wallLength, 0.15f, 1f);
        }
    }

    /// A shot of another team crossing a wall between from and to: what the wall does to it. reflect: sent back
    /// (Mur Lumière, special shots); stop: ends there (Protection, physical shots). point and wallNormal: where and
    /// how it meets the wall, the normal facing the shot.
    public static bool Meets(Team shotTeam, MoveDef shot, Vector2 from, Vector2 to, out bool reflect, out Vector2 point,
        out Vector2 wallNormal, out Combatant newOwner)
    {
        reflect = false;
        point = wallNormal = Vector2.zero;
        newOwner = null;
        foreach (var b in active)
        {
            if (b == null || b.team == shotTeam) continue;
            bool special = shot.category == "special";
            bool reflects = b.move.reflectSpecial && special;
            bool stops = b.move.blockPhysical && !special;
            if (!reflects && !stops) continue;
            float d0 = Vector2.Dot(from - b.centre, b.normal), d1 = Vector2.Dot(to - b.centre, b.normal);
            if (Mathf.Sign(d0) == Mathf.Sign(d1) && Mathf.Abs(d1) > 0.05f) continue; // does not cross its line
            float t = Mathf.Approximately(d0, d1) ? 0f : d0 / (d0 - d1);
            Vector2 at = Vector2.Lerp(from, to, Mathf.Clamp01(t));
            if (Mathf.Abs(Vector2.Dot(at - b.centre, b.along)) > b.half) continue; // past its ends
            reflect = reflects;
            point = at;
            wallNormal = d0 >= 0f ? b.normal : -b.normal;
            newOwner = b.owner;
            if (reflects) b.until -= b.move.reflectWear; // each shot sent back wears it
            EffectPlayer.Spawn(b.move.impactEffect, at, wallNormal);
            return true;
        }
        return false;
    }

    void FixedUpdate()
    {
        if (Time.time >= until || owner == null)
        {
            EffectPlayer.Spawn(move.endEffect, centre, along);
            Destroy(gameObject);
            return;
        }
        if (!move.blockBodies) return;
        // Protection: the opponents cannot cross it; one dashing into it is stopped and thrown back.
        foreach (var c in Combatant.All)
        {
            if (c == null || c.team == team || !c.IsAlive) continue;
            Vector2 p = c.transform.position;
            float side = Vector2.Dot(p - centre, normal);
            if (Mathf.Abs(Vector2.Dot(p - centre, along)) > half + 0.2f || Mathf.Abs(side) > Thickness + 0.3f) continue;
            Vector2 away = side >= 0f ? normal : -normal;
            bool charging = c.IsLunging;
            c.StopLunge();
            var body = c.GetComponent<Rigidbody2D>();
            Vector2 outside = p + away * (Thickness + 0.3f - Mathf.Abs(side));
            if (body != null) body.position = outside;
            else c.transform.position = outside;
            if (charging)
            {
                c.Knockback(away * move.knockback / 0.3f, 0.3f);
                EffectPlayer.Spawn(move.impactEffect, p, away);
            }
        }
    }
}

/// Clonage: a copy of its user that runs from the nearest opponent and dodges what comes, drawing the
/// opponents' attention (TargetChoice.Taunt); the first blow it takes makes it vanish in smoke.
[DefaultExecutionOrder(10)] // after Combatant.LateUpdate, which sets its user's colour each frame
public class Decoy : MonoBehaviour
{

    static readonly Dictionary<Combatant, Decoy> byUser = new();
    const float Flee = 4f; // tiles: where it heads, away from the nearest opponent
    const float BodyRadius = 0.35f, ShotLookahead = 0.5f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => byUser.Clear();

    Combatant user, self;
    MoveDef move;
    PmdAnimator animator;
    float until;
    readonly Navigator navigator = new();

    public static bool Has(Combatant user) => byUser.TryGetValue(user, out var d) && d != null;

    public static bool Spawn(Combatant user, MoveDef move, Vector2 direction)
    {
        if (Has(user)) Burst(byUser[user].self);
        var go = new GameObject(user.name + " (clone)");
        Vector2 side = new Vector2(-direction.y, direction.x) * (Random.value < 0.5f ? 1f : -1f);
        go.transform.position = (Vector2)user.transform.position + side * 0.6f;
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sortingOrder = 1;
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var animator = go.AddComponent<PmdAnimator>();
        var userAnimator = user.GetComponent<PmdAnimator>();
        animator.SetAnimSet(userAnimator != null ? userAnimator.animSet : PokemonRoster.Get(user.OriginId));
        var c = go.AddComponent<Combatant>();
        c.Init(user.OriginId, user.team, new int[0]);
        c.IsDecoy = true;
        c.maxHp = c.hp = user.maxHp;
        var decoy = go.AddComponent<Decoy>();
        decoy.user = user;
        decoy.self = c;
        decoy.move = move;
        decoy.animator = animator;
        decoy.until = Time.time + move.zoneDuration;
        byUser[user] = decoy;
        EffectPlayer.Spawn(move.effect, go.transform.position, Vector2.right);
        return true;
    }

    /// Gone in a puff of smoke (hit, or its time is up).
    public static void Burst(Combatant decoy)
    {
        if (decoy == null) return;
        var d = decoy.GetComponent<Decoy>();
        if (d != null) EffectPlayer.Spawn(d.move.endEffect, decoy.transform.position, Vector2.right);
        decoy.IsDecoy = false; // no second burst while it is destroyed
        Destroy(decoy.gameObject);
    }

    void OnDestroy()
    {
        if (user != null && byUser.TryGetValue(user, out var d) && d == this) byUser.Remove(user);
    }

    void LateUpdate()
    {
        // The decoy looks just like its user; the user, out of the opponents' sight meanwhile, shows half see-through.
        if (user != null && user.TryGetComponent<SpriteRenderer>(out var u))
        {
            var c = u.color;
            c.a = 0.5f;
            u.color = c;
        }
    }

    void FixedUpdate()
    {
        if (Time.time >= until || user == null || !user.IsAlive)
        {
            Burst(self);
            return;
        }
        TargetChoice.Taunt(self); // renewed while it stands
        Vector2 position = transform.position;
        var grid = DungeonGenerator.Current;

        // What comes at it: a dodge when it can, else a step aside.
        Vector2 threat = DangerArea.Escape(position, self.team, BodyRadius);
        if (threat == Vector2.zero) threat = Projectile.Threat(position, self.team, BodyRadius, ShotLookahead);
        if (threat != Vector2.zero)
        {
            Vector2 way = RoleTactics.EscapeStep(grid, position, threat);
            self.TryDodge(way);
            Move(way);
            return;
        }

        // Away from the nearest opponent, around what stands in the way.
        Combatant nearest = null;
        float best = float.MaxValue;
        foreach (var c in Combatant.All)
        {
            if (c == null || c.team == self.team || !c.IsAlive) continue;
            float d = Vector2.Distance(position, c.transform.position);
            if (d < best) { best = d; nearest = c; }
        }
        if (nearest == null || best > move.radius) { Move(Vector2.zero); return; }
        Vector2 away = position - (Vector2)nearest.transform.position;
        if (away.sqrMagnitude < 0.0001f) away = Random.insideUnitCircle;
        Vector2 goal = position + away.normalized * Flee;
        Move(navigator.Direction(position, goal, self));
    }

    void Move(Vector2 direction)
    {
        self.Drive(direction == Vector2.zero ? Vector2.zero : direction.normalized * self.CurrentSpeed);
        if (direction == Vector2.zero) animator.Play("Idle");
        else
        {
            animator.Face(direction);
            animator.Play("Walk");
        }
    }
}

/// Vœu: a little star above a spot that falls a moment later and heals whoever stands under it, of any team.
public class Wish : MonoBehaviour
{
    static readonly List<Wish> active = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => active.Clear();

    Combatant owner;
    Team team;
    MoveDef move;
    float landAt;
    bool falling;

    void OnEnable() => active.Add(this);
    void OnDisable() => active.Remove(this);

    public static bool Pending(Team team) => active.Exists(w => w != null && w.team == team);

    public static void Spawn(Combatant owner, MoveDef move, Vector2 spot)
    {
        var go = new GameObject(move.name);
        go.transform.position = spot;
        var wish = go.AddComponent<Wish>();
        wish.owner = owner;
        wish.team = owner.team;
        wish.move = move;
        wish.landAt = Time.time + move.wishDelay;
        // Where it will land: a ring on the ground (its sheet, else a white circle), and the star waiting above.
        if (!EffectPlayer.Attach(go, move.effect, Vector2.right))
            Shapes.AreaRing(spot, move.radius, new Color(1f, 0.95f, 0.6f, 0.6f), move.wishDelay);
        else
        {
            var sheet = EffectLibrary.Get(move.effect);
            go.transform.localScale = Vector3.one * (move.radius * 2f) / (sheet.frame_width / (float)EffectLibrary.PixelsPerUnit);
            var r = go.GetComponent<SpriteRenderer>();
            if (r != null) r.sortingOrder = 0; // a mark on the ground
        }
    }

    void FixedUpdate()
    {
        float left = landAt - Time.time;
        if (!falling && left <= move.fallTime)
        {
            falling = true;
            SkyFall.Launch(move, transform.position, Mathf.Max(0.05f, left));
        }
        if (left > 0f) return;
        float heal = (owner != null ? owner.maxHp : 0f) * move.healShare;
        foreach (var c in Combatant.All)
            if (c != null && c.IsAlive && !c.IsDecoy && Vector2.Distance(c.transform.position, transform.position) <= move.radius)
                c.Heal(heal);
        EffectPlayer.Spawn(move.impactEffect, transform.position, Vector2.right);
        Destroy(gameObject);
    }
}
