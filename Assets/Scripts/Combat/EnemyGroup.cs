using System.Collections.Generic;
using UnityEngine;

/// Enemies spawned together (a support and two fighters, see EnemySpawner): they all go after the same member of
/// the team, the best one for the group (TargetChoice, seen from its centre), chosen again every few seconds or as
/// soon as it falls. On the island (IslandPopulation) a group may roam: it waits at its camp or walks its patrol
/// route until the team comes near or hurts one of it, and goes back home once the team has got away.
public class EnemyGroup
{
    public readonly List<Combatant> members = new();
    Combatant target;
    float retargetAt;

    /// How a group of the island lives when it is not fighting.
    public class Roam
    {
        public Vector2 home;
        public List<Vector2> route = new(); // a patrol's waypoints (home first), empty for a camp
        public float wake = 6f, leash = 14f, giveUp = 4f; // tiles; seconds with the team beyond the leash
    }

    /// Null: it fights from the start (the waves of an arena).
    public Roam roam;

    /// Fighting (always, without roam); otherwise at its camp or on its round.
    public bool Engaged { get; private set; } = true;

    const float WaypointReach = 1.5f; // tiles
    int leg;
    float lostSince = -1f;
    readonly Dictionary<Combatant, float> restHealth = new(); // each member's health when it went to rest
    int checkedFrame = -1;

    /// Puts it to rest at its camp or on its round (IslandPopulation), until the team comes.
    public void Rest(Roam how)
    {
        roam = how;
        Engaged = false;
        leg = 0;
        lostSince = -1f;
        KeepHealth();
    }

    // A member is hurt when it has less than this (not less than its maximum: an elite does not start full, a camp
    // comes back to rest hurt).
    void KeepHealth()
    {
        restHealth.Clear();
        foreach (var m in members)
            if (m != null) restHealth[m] = m.hp;
    }

    /// Wakes it up (the team came near, one of it was hurt, a test).
    public void Wake()
    {
        Engaged = true;
        lostSince = -1f;
    }

    /// Updates its state once a frame (any member may ask): woken by an opponent within roam.wake tiles of one of
    /// it or by a member that lost health since it went to rest; back to rest when every opponent has stayed beyond roam.leash of its home
    /// for roam.giveUp seconds.
    public bool Fighting()
    {
        if (roam == null) return true;
        if (checkedFrame == Time.frameCount) return Engaged;
        checkedFrame = Time.frameCount;
        if (!Engaged)
        {
            foreach (var m in members)
            {
                if (m == null || !m.IsAlive) continue;
                if (m.hp < restHealth.GetValueOrDefault(m, m.maxHp) - 0.5f || Nearest(m.transform.position) <= roam.wake) { Wake(); break; }
            }
            return Engaged;
        }
        if (Nearest(roam.home) <= roam.leash) lostSince = -1f;
        else if (lostSince < 0f) lostSince = Time.time;
        else if (Time.time - lostSince >= roam.giveUp)
        {
            Engaged = false;
            target = null;
            leg = 0;
            KeepHealth();
        }
        return Engaged;
    }

    static float Nearest(Vector2 at)
    {
        float best = float.MaxValue;
        foreach (var c in Combatant.All)
            if (c != null && c.IsAlive && c.team == Team.Player) best = Mathf.Min(best, Vector2.Distance(at, c.transform.position));
        return best;
    }

    /// Where a resting member walks: its place around the camp, or the patrol's next waypoint.
    public Vector2 RestPoint(Combatant member)
    {
        var leader = members.Find(m => m != null && m.IsAlive);
        int index = Mathf.Max(0, members.IndexOf(member));
        Vector2 spot = roam.route.Count > 1 ? roam.route[leg % roam.route.Count] : roam.home;
        if (roam.route.Count > 1 && member == leader && Vector2.Distance(member.transform.position, spot) < WaypointReach)
        {
            leg = (leg + 1) % roam.route.Count; // the first one standing leads the round
            spot = roam.route[leg];
        }
        if (member == leader) return spot;
        float angle = index * 2.4f; // the others around it, as they were spawned
        return spot + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 1.2f;
    }

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
