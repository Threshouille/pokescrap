using System.Collections.Generic;
using UnityEngine;

/// What an AI-driven Pokémon should do right now, according to its role (melee, ranged or support).
/// Shared by the player's allies and by the enemies.
public static class RoleTactics
{
    public const float ZoneMargin = 0.4f; // tiles kept from the edge of a harmful zone
    public const float PersonalSpace = 1.5f; // tiles an AI tries to keep from the other Pokémon of its team
    const float ShotRadius = 0.2f; // clearance needed along a line of fire
    const float MeleeStandOff = 1.5f; // melee: distance kept while every move recharges
    const float RangedMax = 6f, RangedRetreat = 3.5f; // ranged: stays within Max, backs off under Retreat
    const float SupportMax = 7f, SupportRetreat = 4f; // support: same idea, farther away
    const float SelfDefence = 1.5f; // ranged and support only use their melee moves on an opponent this close
    const float RegroupDistance = 5f; // support: comes back when its nearest ally is farther than this
    const float HealThreshold = 0.7f; // heals allies under this share of their health
    const float BuffRange = 6f; // bonuses are used when an opponent is within this distance
    // A hurt Pokémon may fall back to a terrain that helps it (Terrain.Refuge): under RefugeEnter of its health,
    // with no opponent within RefugeSafe tiles, a refuge within RefugeSearch tiles. It stays at most RefugeTime
    // seconds or until RefugeLeave of its health, then not again for RefugeCooldown seconds.
    const float RefugeEnter = 0.35f, RefugeLeave = 0.6f, RefugeTime = 8f, RefugeCooldown = 12f;
    const float RefugeSearch = 6f, RefugeSafe = 2f, RefugeReached = 0.4f;
    const float SpreadReach = 8f; // a reaction spreading over a puddle may reach a team-mate this far from the target

    class RefugeState { public float until, cooldownUntil; }
    static readonly Dictionary<Combatant, RefugeState> refuges = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => refuges.Clear();

    public struct Decision
    {
        public bool engaged; // false when there is nothing to do (no opponent, nobody to heal)
        public int slot; // move to use now, or -1
        public Vector2 aim;
        public float distance; // where a zone move should land
        public Vector2 move; // direction to walk when no move is used
        public Vector2 look;
        public Combatant foe; // opponent the decision is about, or null
    }

    /// "target": opponent to fight (an enemy group's shared target); the nearest one within aggroRange when null.
    /// "canAttack" false: only where to stand (an enemy between two attacks).
    public static Decision Decide(Combatant self, Navigator navigator, float aggroRange, Combatant target = null, bool canAttack = true)
    {
        var decision = new Decision { slot = -1 };
        Vector2 position = self.transform.position;
        var grid = DungeonGenerator.Current;
        Team opponents = self.team == Team.Player ? Team.Enemy : Team.Player;
        bool support = self.Role == "support", ranged = self.Role == "ranged", melee = !support && !ranged;

        var foe = target != null && target.IsAlive ? target : Combatant.Nearest(opponents, position, aggroRange);
        decision.foe = foe;
        Vector2 toFoe = foe != null ? (Vector2)foe.transform.position - position : Vector2.zero;
        float foeDistance = toFoe.magnitude;
        bool lineOfFire = foe != null && (grid == null || grid.LineClear(position, foe.transform.position, ShotRadius));
        decision.look = toFoe;

        // Ranged and support Pokémon back away from an opponent that gets too close. When cornered (no way to
        // step back), "retreat" stays zero and they fight where they stand.
        bool tooClose = !melee && foe != null && foeDistance < (support ? SupportRetreat : RangedRetreat);
        Vector2 retreat = tooClose ? Away(self, grid, position, toFoe) : Vector2.zero;

        Combatant patient = null; // wounded ally that is out of reach
        float wanted = float.MaxValue; // shortest reach among the ready moves worth walking for
        var refuge = RefugeTarget(self, foeDistance, foe != null);
        // An attack whose ground effect does nothing special is kept aside while the others are looked at: one
        // whose trace turns against the opponents goes first, one that would hurt a team-mate is not used.
        int fallback = -1;
        Vector2 fallbackAim = Vector2.zero;
        float fallbackDistance = 0f;

        // A support looks at its heals and bonuses before anything else (pass 0), then at its attacks (pass 1).
        // The other roles take their moves in slot order (basic attack last), so a tank can still raise its defence between two blows.
        for (int pass = 0; canAttack && pass < 2; pass++)
        {
            for (int k = 0; k < 2 * Combatant.MoveSlots; k++)
            {
                int slot = k % Combatant.MoveSlots;
                var move = self.Moves[slot];
                if (move == null || move.basic != (k >= Combatant.MoveSlots)) continue;
                bool care = move.family == "heal" || move.family == "modifier";
                if (support && care != (pass == 0)) continue;
                if (care && fallback >= 0) continue; // an attack is already chosen
                if (!support && pass == 1) continue;
                if (self.CooldownLeft(slot) > 0f) continue;

                if (move.family == "heal")
                {
                    var wounded = Combatant.MostWounded(self.team, position, float.MaxValue, HealThreshold);
                    if (wounded == null) continue;
                    Vector2 toWounded = (Vector2)wounded.transform.position - position;
                    if (toWounded.magnitude <= move.range) return Use(decision, slot, toWounded == Vector2.zero ? Vector2.right : toWounded, toWounded.magnitude);
                    if (support) patient = wounded;
                    continue;
                }

                if (move.family == "modifier")
                {
                    if (foe == null) continue;
                    if (move.target == "enemies")
                    {
                        if (foe.HasModifier(move.modStat, false)) continue;
                        if (foeDistance <= move.radius * 0.9f) return Use(decision, slot, toFoe, foeDistance);
                        if (melee) wanted = Mathf.Min(wanted, move.radius * 0.8f); // only a melee Pokémon walks in for it
                    }
                    else if (foeDistance <= BuffRange && !self.HasModifier(move.modStat, move.modValue > 1f))
                        return Use(decision, slot, Vector2.right, 0f);
                    continue;
                }

                // Attacks: projectile, melee, zone.
                if (foe == null || toFoe == Vector2.zero) continue;
                if (move.power <= 0 && !string.IsNullOrEmpty(move.status) && foe.HasStatus(move.status)) continue; // already has it

                if (move.family == "melee")
                {
                    // For a ranged or support Pokémon a melee move is self-defence only.
                    float reach = melee ? move.Reach : Mathf.Min(move.Reach, SelfDefence);
                    bool intoCloud = move.lunge > 0f && Zone.Inside(foe.transform.position, self.team, ZoneMargin);
                    if (foeDistance <= reach && lineOfFire && !intoCloud)
                    {
                        int value = TerrainValue(self, move, foe);
                        if (value > 0) return Use(decision, slot, toFoe, foeDistance);
                        if (value == 0 && fallback < 0) (fallback, fallbackAim, fallbackDistance) = (slot, toFoe, foeDistance);
                        continue;
                    }
                    if (melee) wanted = Mathf.Min(wanted, move.Reach * 0.9f);
                    continue;
                }

                // Too close with room to step back: back off first, shoot afterwards.
                bool usable = foeDistance <= move.Reach && (lineOfFire || move.family == "zone") && retreat == Vector2.zero;
                if (usable)
                {
                    int value = TerrainValue(self, move, foe);
                    if (value > 0) return Use(decision, slot, toFoe, foeDistance);
                    if (value == 0 && fallback < 0) (fallback, fallbackAim, fallbackDistance) = (slot, toFoe, foeDistance);
                    continue;
                }
                wanted = Mathf.Min(wanted, move.Reach * 0.9f);
            }
        }
        if (fallback >= 0) return Use(decision, fallback, fallbackAim, fallbackDistance);

        // Hurt: heads for a terrain that helps it, and stays there.
        if (refuge != null)
        {
            decision.engaged = true;
            Vector2 toRefuge = refuge.Value - position;
            decision.move = toRefuge.magnitude > RefugeReached ? navigator.Direction(position, refuge.Value, self) : Vector2.zero;
            decision.move = Separate(self, grid, position, decision.move);
            return decision;
        }

        // Nothing to use right now: where to stand.
        if (melee)
        {
            if (foe == null) return decision;
            decision.engaged = true;
            if (wanted < float.MaxValue)
            {
                // Behind a wall, being in range is not enough: keep walking until the target is in sight.
                if (foeDistance > wanted || !lineOfFire) decision.move = navigator.Direction(position, foe.transform.position, self);
            }
            else if (foeDistance > MeleeStandOff) decision.move = navigator.Direction(position, foe.transform.position, self);
        }
        else if (retreat != Vector2.zero)
        {
            decision.engaged = true;
            decision.move = retreat;
        }
        else if (support && patient != null)
        {
            decision.engaged = true;
            decision.move = navigator.Direction(position, patient.transform.position, self);
            decision.look = decision.move;
        }
        else
        {
            var friend = support ? Combatant.Nearest(self.team, position, float.MaxValue, self) : null;
            if (friend != null && Vector2.Distance(friend.transform.position, position) > RegroupDistance)
            {
                decision.engaged = true;
                decision.move = navigator.Direction(position, friend.transform.position, self);
            }
            else if (foe != null)
            {
                decision.engaged = true;
                if (!tooClose)
                {
                    // Stays in its band: walks in only as far as needed to have the target in reach and in sight.
                    float far = Mathf.Min(support ? SupportMax : RangedMax, wanted);
                    bool hasShot = wanted < float.MaxValue;
                    if (foeDistance > far && (hasShot || ranged) || (hasShot && !lineOfFire))
                        decision.move = navigator.Direction(position, foe.transform.position, self);
                }
            }
        }

        if (decision.engaged) decision.move = Separate(self, grid, position, decision.move);
        return decision;
    }

    /// Where a hurt Pokémon goes to recover (see the Refuge constants), or null.
    static Vector2? RefugeTarget(Combatant self, float foeDistance, bool hasFoe)
    {
        refuges.TryGetValue(self, out var state);
        float health = self.maxHp > 0f ? self.hp / self.maxHp : 1f;
        bool going = state != null && Time.time < state.until;
        if (going && health >= RefugeLeave)
        {
            state.until = 0f;
            state.cooldownUntil = Time.time + RefugeCooldown;
            return null;
        }
        if (going) return Terrain.FindRefuge(self, RefugeSearch); // looked for again: the terrain may be gone
        if (health >= RefugeEnter || (state != null && Time.time < state.cooldownUntil)) return null;
        if (hasFoe && foeDistance < RefugeSafe) return null; // in the thick of the fight
        var spot = Terrain.FindRefuge(self, RefugeSearch);
        if (spot == null) return null;
        if (state == null) refuges[self] = state = new RefugeState();
        state.until = Time.time + RefugeTime;
        state.cooldownUntil = state.until + RefugeCooldown;
        return spot;
    }

    /// What the ground effect of an attack is worth against "foe": +1 when it reacts with the terrain under the
    /// opponent into something that hurts it (lightning on the puddle it stands in), -1 when it would hurt a
    /// team-mate (in the area, or on the same puddle when the reaction spreads), 0 otherwise.
    static int TerrainValue(Combatant self, MoveDef move, Combatant foe)
    {
        var t = Terrain.Of(move);
        if (t == null) return 0;
        Vector2 from = self.transform.position, at = foe.transform.position;
        bool line = t.shape == "trail" || t.shape == "dash" || t.shape == "strike";
        Vector2 centre = t.shape == "around" ? from : at;
        float radius = Terrain.RadiusOf(t, move) + 0.5f;
        var under = Terrain.At(at);
        string result = Terrain.Predict(under?.key, t.terrain);
        bool reacts = Terrain.Reacts(under?.key, t.terrain, out bool spreads);

        foreach (var ally in Combatant.All)
        {
            if (ally == null || ally == self || ally.team != self.team || !ally.IsAlive) continue;
            Vector2 p = ally.transform.position;
            bool inArea = line ? DistanceToSegment(p, from, at) <= radius : Vector2.Distance(p, centre) <= radius;
            bool onSamePuddle = spreads && Terrain.At(p) == under && Vector2.Distance(p, at) <= SpreadReach;
            if (!inArea && !onSamePuddle) continue;
            var there = Terrain.Def(Terrain.Predict(Terrain.At(p)?.key, t.terrain));
            if (Terrain.Harmful(there, ally)) return -1;
        }
        return reacts && Terrain.Harmful(Terrain.Def(result), foe) ? 1 : 0;
    }

    static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    static Decision Use(Decision decision, int slot, Vector2 aim, float distance)
    {
        decision.engaged = true;
        decision.slot = slot;
        decision.aim = aim;
        decision.distance = distance;
        decision.look = aim;
        return decision;
    }

    /// Direction that takes the Pokémon away from an opponent: straight back, else sideways, else zero (cornered).
    public static Vector2 Away(Combatant self, DungeonGenerator grid, Vector2 position, Vector2 toFoe)
    {
        Vector2 back = -toFoe.normalized;
        if (Passable(self, grid, position, back)) return back;
        var left = new Vector2(-back.y, back.x);
        if (Passable(self, grid, position, left)) return left;
        if (Passable(self, grid, position, -left)) return -left;
        return Vector2.zero;
    }

    /// Bends a walking direction away from team-mates that are too close, so the AIs do not bunch up.
    /// A Pokémon standing still only steps aside when it is really crowded.
    public static Vector2 Separate(Combatant self, DungeonGenerator grid, Vector2 position, Vector2 move)
    {
        Vector2 push = self.Repulsion(PersonalSpace);
        if (push.sqrMagnitude < 0.0001f) return move;
        if (move == Vector2.zero)
        {
            if (push.magnitude < 0.25f) return Vector2.zero; // tolerance, so nobody jitters at the edge of its bubble
            Vector2 aside = push.normalized;
            return Passable(self, grid, position, aside) ? aside * 0.5f : Vector2.zero;
        }
        Vector2 blended = move.normalized + push * 1.5f;
        if (blended.sqrMagnitude < 0.01f) return move;
        blended = blended.normalized * move.magnitude;
        return Passable(self, grid, position, blended.normalized) ? blended : move;
    }

    static readonly float[] EscapeAngles = { 0f, 45f, -45f, 90f, -90f };

    /// Way out of a harmful zone: straight out when the floor allows it, else the closest angle that does. Only walls
    /// are checked, since every step is still inside the zone (checking zones too made the AI circle in it).
    public static Vector2 EscapeStep(DungeonGenerator grid, Vector2 position, Vector2 escape)
    {
        foreach (float angle in EscapeAngles)
        {
            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * escape;
            if (grid == null || grid.WalkableAt(position + direction * 0.7f, 0.3f)) return direction;
        }
        return escape;
    }

    /// True when a short step in that direction stays on the floor and out of harmful zones.
    public static bool Passable(Combatant self, DungeonGenerator grid, Vector2 position, Vector2 direction)
    {
        Vector2 ahead = position + direction * 0.7f;
        return (grid == null || grid.WalkableAt(ahead, 0.3f)) && !Zone.Inside(ahead, self.team, ZoneMargin);
    }
}
