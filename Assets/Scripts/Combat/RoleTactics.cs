using UnityEngine;

/// What an AI-driven Pokémon should do right now, according to its role (melee, ranged or support).
/// Shared by the player's allies and by the enemies.
public static class RoleTactics
{
    public const float ZoneMargin = 0.4f; // tiles kept from the edge of a harmful zone
    const float ShotRadius = 0.2f; // clearance needed along a line of fire
    const float MeleeStandOff = 1.5f; // melee: distance kept while every move recharges
    const float HoldDistance = 4f; // ranged: distance kept while every move recharges
    const float FleeDistance = 2.5f; // support: runs away from opponents closer than this
    const float RegroupDistance = 3f; // support: comes back when its nearest ally is farther than this
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
    }

    public static Decision Decide(Combatant self, Navigator navigator, float aggroRange)
    {
        var decision = new Decision { slot = -1 };
        Vector2 position = self.transform.position;
        var grid = DungeonGenerator.Current;
        Team opponents = self.team == Team.Player ? Team.Enemy : Team.Player;
        bool support = self.Role == "support";

        var foe = Combatant.Nearest(opponents, position, aggroRange);
        Vector2 toFoe = foe != null ? (Vector2)foe.transform.position - position : Vector2.zero;
        float foeDistance = toFoe.magnitude;
        bool lineOfFire = foe != null && (grid == null || grid.LineClear(position, foe.transform.position, ShotRadius));
        decision.look = toFoe;

        // Care first: heals and bonuses. A support looks at these before anything else; the other roles take their
        // moves in slot order, so a tank can still raise its defence between two blows.
        Combatant patient = null; // wounded ally that is out of reach
        float wanted = float.MaxValue; // shortest reach among the ready attacks
        bool anyRanged = false;

        for (int pass = 0; pass < 2; pass++)
        {
            for (int slot = 0; slot < Combatant.MoveSlots; slot++)
            {
                var move = self.Moves[slot];
                if (move == null) continue;
                bool care = move.family == "heal" || move.family == "modifier";
                if (support && care != (pass == 0)) continue; // support: care on the first pass, attacks on the second
                if (!support && pass == 1) continue; // other roles: a single pass, in slot order
                if (!care && move.family != "melee") anyRanged = true;
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
                        if (foeDistance <= move.radius * 0.9f && !foe.HasModifier(move.modStat, false)) return Use(decision, slot, toFoe, foeDistance);
                        if (!foe.HasModifier(move.modStat, false)) wanted = Mathf.Min(wanted, move.radius * 0.8f);
                    }
                    else if (foeDistance <= BuffRange && !self.HasModifier(move.modStat, move.modValue > 1f))
                        return Use(decision, slot, Vector2.right, 0f);
                    continue;
                }

                // Attacks: projectile, melee, zone.
                if (foe == null) continue;
                if (move.power <= 0 && !string.IsNullOrEmpty(move.status) && foe.HasStatus(move.status)) continue; // already has it
                bool usable = foeDistance <= move.Reach && toFoe != Vector2.zero && (lineOfFire || move.family == "zone")
                    && !(move.lunge > 0f && Zone.Inside(foe.transform.position, self.team, ZoneMargin)); // no dashing into a cloud
                if (usable) return Use(decision, slot, toFoe, foeDistance);
                wanted = Mathf.Min(wanted, move.Reach * 0.9f);
            }
        }

        // Nothing to use right now: where to stand.
        if (support)
        {
            if (foe != null && foeDistance < FleeDistance)
            {
                decision.engaged = true;
                Vector2 away = -toFoe.normalized;
                decision.move = Passable(self, grid, position, away) ? away
                    : Passable(self, grid, position, new Vector2(-away.y, away.x)) ? new Vector2(-away.y, away.x)
                    : Vector2.zero;
                return decision;
            }
            if (patient != null)
            {
                decision.engaged = true;
                decision.move = navigator.Direction(position, patient.transform.position, self.team);
                decision.look = decision.move;
                return decision;
            }
            var friend = Combatant.Nearest(self.team, position, float.MaxValue, self);
            if (friend != null && Vector2.Distance(friend.transform.position, position) > RegroupDistance)
            {
                decision.engaged = true;
                decision.move = navigator.Direction(position, friend.transform.position, self.team);
                return decision;
            }
            if (foe == null) return decision;
            decision.engaged = true;
            // Steps forward for a ready move, but never into the distance it would then run away from.
            if (wanted < float.MaxValue && foeDistance > Mathf.Max(wanted, FleeDistance + 0.3f))
                decision.move = navigator.Direction(position, foe.transform.position, self.team);
            return decision;
        }

        if (foe == null) return decision;
        decision.engaged = true;
        if (wanted < float.MaxValue)
        {
            // Behind a wall, being in range is not enough: keep walking until the target is in sight.
            if (foeDistance > wanted || !lineOfFire) decision.move = navigator.Direction(position, foe.transform.position, self.team);
        }
        else if (self.Role == "ranged" || anyRanged && self.Role != "melee")
        {
            if (foeDistance > HoldDistance + 0.5f) decision.move = navigator.Direction(position, foe.transform.position, self.team);
            else if (foeDistance < HoldDistance - 1f && Passable(self, grid, position, -toFoe.normalized))
                decision.move = -toFoe.normalized; // back off while recharging
        }
        else if (foeDistance > MeleeStandOff) decision.move = navigator.Direction(position, foe.transform.position, self.team);
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

    /// True when a short step in that direction stays on the floor and out of harmful zones.
    public static bool Passable(Combatant self, DungeonGenerator grid, Vector2 position, Vector2 direction)
    {
        Vector2 ahead = position + direction * 0.7f;
        return (grid == null || grid.WalkableAt(ahead, 0.3f)) && !Zone.Inside(ahead, self.team, ZoneMargin);
    }
}
