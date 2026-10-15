using System.Collections.Generic;
using UnityEngine;

/// Enemies spawned together (a support and two fighters, see EnemySpawner): they all go after the same member of
/// the team, the one nearest to the group, chosen again every few seconds or as soon as it falls.
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
        target = Combatant.Nearest(Team.Player, centre / alive, float.MaxValue);
        retargetAt = Time.time + SaveData.Settings.groupRetargetTime;
        return target;
    }
}
