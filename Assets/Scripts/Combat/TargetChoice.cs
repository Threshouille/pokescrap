using System.Collections.Generic;
using UnityEngine;

/// Which opponent an AI goes after (allies and enemy groups alike), rather than simply the nearest one. Each
/// opponent in range gets a score (weights aiTarget* in meta.json):
/// - finish: one that the AI's best move would knock out in few hits (worth a lot: a knockout removes a threat);
/// - efficacy: the AI's best type multiplier against it (a Grass Pokémon prefers the Water one);
/// - threat: it is next to a team-mate its own moves hit hard (the Water Pokémon facing our Fire one);
/// - focus: team-mates already after it (the team closes in on the same opponent);
/// - distance: farther costs a little; keep: the current target is kept unless another is clearly better.
public static class TargetChoice
{
    const float ThreatReach = 5f; // tiles: an opponent this close to a team-mate threatens it

    // Opponent each AI went after last (for the focus score).
    static readonly Dictionary<Combatant, Combatant> chosen = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => chosen.Clear();

    /// Remembers what "self" is fighting, for its team-mates' focus.
    public static void Note(Combatant self, Combatant foe)
    {
        if (self == null) return;
        if (foe == null) chosen.Remove(self);
        else chosen[self] = foe;
    }

    /// The opponent "self" went after last, if it still stands.
    public static Combatant Current(Combatant self) =>
        self != null && chosen.TryGetValue(self, out var foe) && foe != null && foe.IsAlive ? foe : null;

    /// The best opponent for "self" standing at "from" within "range", or null. "current": the target it has now.
    public static Combatant Best(Combatant self, Vector2 from, float range, Combatant current = null)
    {
        var s = SaveData.Settings;
        Team opponents = self.team == Team.Player ? Team.Enemy : Team.Player;
        Combatant best = null;
        float bestScore = float.MinValue;
        foreach (var foe in Combatant.All)
        {
            if (foe == null || foe.team != opponents || !foe.IsAlive) continue;
            float distance = Vector2.Distance(from, foe.transform.position);
            if (distance > range) continue;

            float hit = 0f, efficacy = 1f;
            foreach (var move in self.Moves)
            {
                if (move == null || move.power <= 0) continue;
                hit = Mathf.Max(hit, self.EstimateHit(move, foe));
                efficacy = Mathf.Max(efficacy, TypeChart.Multiplier(move.type, foe.Stats?.types, out _));
            }
            float score = -s.aiTargetDistance * distance;
            if (hit > 0f) score += s.aiTargetFinish / Mathf.Max(1f, foe.hp / hit);
            score += s.aiTargetEfficacy * Mathf.Log(Mathf.Max(0.25f, efficacy), 2f);
            score += s.aiTargetThreat * Threat(foe, self.team);
            score += s.aiTargetFocus * Focus(foe, self);
            if (foe == current) score += s.aiTargetKeep;
            if (score > bestScore)
            {
                best = foe;
                bestScore = score;
            }
        }
        return best;
    }

    // How hard this opponent's moves would hit a team-mate of "team" standing near it: 0 when none is weak to it,
    // up to about 1 (2 for a team-mate low on health).
    static float Threat(Combatant foe, Team team)
    {
        float threat = 0f;
        foreach (var mate in Combatant.All)
        {
            if (mate == null || mate.team != team || !mate.IsAlive) continue;
            if (Vector2.Distance(foe.transform.position, mate.transform.position) > ThreatReach) continue;
            float mul = 1f;
            foreach (var move in foe.Moves)
                if (move != null && move.power > 0) mul = Mathf.Max(mul, TypeChart.Multiplier(move.type, mate.Stats?.types, out _));
            if (mul <= 1f) continue;
            float frail = mate.maxHp > 0f ? 1f - mate.hp / mate.maxHp : 0f;
            threat = Mathf.Max(threat, Mathf.Log(mul, 2f) * (1f + frail));
        }
        return threat;
    }

    static int Focus(Combatant foe, Combatant self)
    {
        int count = 0;
        foreach (var (who, target) in chosen)
            if (who != null && who != self && who.IsAlive && who.team == self.team && target == foe) count++;
        return count;
    }
}
