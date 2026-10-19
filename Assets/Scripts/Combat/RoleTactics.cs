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
                    if (foeDistance <= reach && lineOfFire && !intoCloud) return Use(decision, slot, toFoe, foeDistance);
                    if (melee) wanted = Mathf.Min(wanted, move.Reach * 0.9f);
                    continue;
                }

                // Too close with room to step back: back off first, shoot afterwards.
                bool usable = foeDistance <= move.Reach && (lineOfFire || move.family == "zone") && retreat == Vector2.zero;
                if (usable) return Use(decision, slot, toFoe, foeDistance);
                wanted = Mathf.Min(wanted, move.Reach * 0.9f);
            }
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
