using UnityEngine;

/// What the AI members of the team do while one of them is knocked out (it gets back up when a living team member
/// stands on it long enough: PlayerController.UpdateRevives). Of the living members, the fittest to take hits and
/// finish opponents off is the guard: it draws the opponents near the knocked-out one away from it (TargetChoice
/// taunt) while the other brings it back. The controlled Pokémon counts: when the player stands on the knocked-out
/// member, the AI ally guards; when the player is the guard, the AI ally revives. With no opponent near the
/// knocked-out member, the AI ally simply goes and revives it.
public static class TeamRescue
{
    public enum Job { None, Guard, Revive }

    /// The job of this AI ally now; "downed" is the knocked-out team-mate, "threat" the opponent nearest to it.
    public static Job JobOf(Combatant self, Combatant leader, float reviveRadius, out Combatant downed, out Combatant threat)
    {
        threat = null;
        downed = Downed(self);
        if (downed == null) return Job.None;
        threat = ThreatNear(downed, self.team);
        bool leaderReviving = leader != null && leader.IsAlive && leader != self
            && Vector2.Distance(leader.transform.position, downed.transform.position) <= reviveRadius;
        if (threat == null) return leaderReviving ? Job.None : Job.Revive;
        if (leaderReviving) return Job.Guard;
        // Two living members (self and the controlled one): the fitter guards, the other revives.
        return leader == null || !leader.IsAlive || Aptitude(self, threat) >= Aptitude(leader, threat) ? Job.Guard : Job.Revive;
    }

    /// The knocked-out team-mate nearest to "self", or null.
    public static Combatant Downed(Combatant self)
    {
        Combatant best = null;
        float bestDistance = float.MaxValue;
        foreach (var c in Combatant.All)
        {
            if (c == null || c == self || c.team != self.team || c.IsAlive || c.Stats == null) continue;
            float d = Vector2.Distance(self.transform.position, c.transform.position);
            if (d < bestDistance) { best = c; bestDistance = d; }
        }
        return best;
    }

    /// The opponent nearest to the knocked-out member within allyGuardRadius, or null.
    public static Combatant ThreatNear(Combatant downed, Team team) =>
        Combatant.Nearest(team == Team.Player ? Team.Enemy : Team.Player, downed.transform.position, SaveData.Settings.allyGuardRadius);

    /// How fit a member is to guard against that opponent: its health and defences (it takes the hits), how much of
    /// the opponent's health its best move takes (it finishes it), a bonus for a melee fighter.
    public static float Aptitude(Combatant c, Combatant threat)
    {
        if (c == null || !c.IsAlive || c.maxHp <= 0f) return float.MinValue;
        float tank = c.hp / c.maxHp * (1f + (c.def + c.spDef) / 20f);
        float finish = 0f;
        if (threat != null)
            foreach (var m in c.Moves)
                if (m != null && m.power > 0) finish = Mathf.Max(finish, Mathf.Min(1f, c.EstimateHit(m, threat) / Mathf.Max(1f, threat.hp)));
        return tank + finish + (c.Role == "melee" ? 0.2f : 0f);
    }

    /// Where a guard stands: between the knocked-out member and the opponent when its attack is ready (a melee blow
    /// then pushes the opponent away), beyond the opponent otherwise, so that the opponent turns its back on it.
    public static Vector2 GuardSpot(Combatant guard, Combatant downed, Combatant threat, bool attackReady)
    {
        Vector2 body = downed.transform.position, foe = threat.transform.position;
        Vector2 away = foe - body;
        away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.right;
        if (guard.Role != "melee") return foe + away * SaveData.Settings.allyLureDistance;
        return attackReady ? foe - away * 1f : foe + away * 1.5f;
    }
}
