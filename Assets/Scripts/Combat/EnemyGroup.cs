using System.Collections.Generic;
using UnityEngine;

/// Enemies spawned together (a support and two fighters, see EnemySpawner): they all go after the same member of
/// the team, the best one for the group (TargetChoice, seen from its centre), chosen again every few seconds or as
/// soon as it falls.
public class EnemyGroup
{
    public readonly List<Combatant> members = new();
    Combatant target;
    float retargetAt;

    public Combatant Target()
    {
        if (target != null && target.IsAlive && Time.time < retargetAt) return target;
        Vector2 centre = Vector2.zero;
        int alive = 0;
        foreach (var member in members)
            if (member != null && member.IsAlive) { centre += (Vector2)member.transform.position; alive++; }
        if (alive == 0) return null;
        Combatant judge = members.Find(m => m != null && m.IsAlive);
        target = TargetChoice.Best(judge, centre / alive, float.MaxValue, target != null && target.IsAlive ? target : null);
        retargetAt = Time.time + SaveData.Settings.groupRetargetTime;
        return target;
    }
}
