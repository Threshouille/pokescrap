using UnityEngine;

/// Team member played by the computer. Its role (melee, ranged or support, see RoleTactics) decides how it fights;
/// this class adds what is specific to an ally: following the leader, and never straying too far from it. It never
/// follows the leader into danger: it heads for the safe tile nearest to the leader, by a path around the dangers,
/// and waits at the edge when there is none. When the leader falls back (walks away from it beyond allyFollowBreak),
/// it drops the fight and follows; hurt (under allyRetreatHealth), it keeps close to the leader and only fights
/// opponents that come near, until it is back above allyRetreatRecover (meta.json). Cut off from the leader by water,
/// lava or void, it changes them with its moves to make a way (ObstacleTactics.Repair). While a team-mate is knocked
/// out, it either guards it (draws the opponents near it away) or brings it back (TeamRescue).
/// It walks around walls and harmful zones, dodges announced attacks and incoming shots, and a melee ally dodges
/// forward to catch up with its target.
[RequireComponent(typeof(Rigidbody2D), typeof(PmdAnimator), typeof(Combatant))]
public class AllyAI : MonoBehaviour
{
    const float AttackAnimTime = 0.3f;
    const float BodyRadius = 0.35f; // margin added to danger areas and shots
    const float ShotLookahead = 0.5f; // seconds: a shot arriving within this is dodged
    const float PursuitMin = 3f, PursuitMax = 6f; // melee: dodges toward a target this far away...
    const float PursuitHealth = 0.5f; // ...when above this share of its HP (the dodge stays for defence otherwise)
    const int SafeSearch = 12; // tiles around the leader searched for a safe spot (in danger, or out of reach)
    const float EdgeLook = 0.6f; // tiles ahead checked before a step while following

    [Tooltip("The Pokémon the player controls (the team object follows it)")]
    public Transform leader;
    [Tooltip("Distance kept from the leader when there is nothing to fight")]
    public float followDistance = 2f;
    [Tooltip("Beyond this distance from the leader, the ally drops its target and comes back (allyLeash in meta.json)")]
    public float leash = 9f;
    [Tooltip("Enemies farther than this are ignored (allyAggroRange in meta.json)")]
    public float aggroRange = 9f;
    [Tooltip("Share of the Pokémon's own speed used by the AI")]
    public float speedFactor = 0.9f;

    readonly Navigator navigator = new();
    readonly Navigator repairNavigator = new(); // way to the spot a repair is made from (the main one keeps the leader)
    readonly Navigator rescueNavigator = new(); // way to a knocked-out team-mate or to the guard's spot
    PlayerController team;
    Combatant leaderCombatant;
    Transform leaderSeenAs;
    ObstacleTactics.Plan repair;
    bool hasRepair;
    float nextRepair;
    const float RepairInterval = 0.5f;
    Rigidbody2D body;
    PmdAnimator animator;
    Combatant combatant;
    float attackUntil;
    string attackAnim = "Attack";
    Vector2 attackDirection;
    bool returning; // set when the leash is exceeded, cleared once back next to the leader
    bool cautious; // low on health: stays by the leader
    Vector2 lastLeader, leaderVelocity; // the leader's smoothed velocity, to tell when it falls back
    bool leaderSeen;
    // On a large map the path search stops at its limit and gives a partial path, so a river with no way across
    // still looks "reachable": an ally that stops getting closer for StuckTime seconds looks for a repair anyway.
    const float StuckTime = 1.5f, StuckProgress = 0.3f;
    float stuckSince = -1f, stuckDistance;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<PmdAnimator>();
        combatant = GetComponent<Combatant>();
        leash = SaveData.Settings.allyLeash;
        aggroRange = SaveData.Settings.allyAggroRange;
    }

    void Start() => team = FindAnyObjectByType<PlayerController>();

    void FixedUpdate()
    {
        if (leader == null || !combatant.IsAlive || combatant.IsDashing) return; // a melee dash drives the body

        Vector2 position = transform.position;
        var grid = DungeonGenerator.Current;

        // Getting out of an announced attack or the path of a shot comes first: with a dodge when it is ready,
        // else on foot.
        Vector2 threat = DangerArea.Escape(position, combatant.team, BodyRadius);
        if (threat == Vector2.zero) threat = Projectile.Threat(position, combatant.team, BodyRadius, ShotLookahead);
        if (threat != Vector2.zero)
        {
            Vector2 way = RoleTactics.EscapeStep(grid, position, threat);
            if (combatant.TryDodge(way)) attackUntil = 0f;
            Steer(way, way);
            return;
        }

        // Then getting out of a harmful zone.
        Vector2 escape = Zone.EscapeDirection(position, combatant.team, RoleTactics.ZoneMargin);
        if (escape == Vector2.zero) escape = Terrain.EscapeDirection(position, combatant);
        if (escape == Vector2.zero) escape = Pylons.EscapeDirection(position, combatant);
        if (escape != Vector2.zero)
        {
            Steer(RoleTactics.EscapeStep(grid, position, escape), escape);
            return;
        }

        if (TryRescue(position, grid)) return;

        var settings = SaveData.Settings;
        Vector2 toLeader = (Vector2)leader.position - position;
        Vector2 leaderAt = leader.position;
        if (leaderSeen) leaderVelocity = Vector2.Lerp(leaderVelocity, (leaderAt - lastLeader) / Time.fixedDeltaTime, 0.2f);
        lastLeader = leaderAt;
        leaderSeen = true;
        float health = combatant.maxHp > 0f ? combatant.hp / combatant.maxHp : 1f;
        if (health < settings.allyRetreatHealth) cautious = true;
        else if (health >= settings.allyRetreatRecover) cautious = false;
        float leashNow = cautious ? followDistance + 2f : leash;
        // The leader walking away from us, already a few tiles off: it falls back, the team follows.
        bool leaderLeaving = toLeader.magnitude > settings.allyFollowBreak
            && Vector2.Dot(leaderVelocity, toLeader.normalized) > settings.allyLeaderRetreatSpeed;
        // Without this memory the ally would flip between "come back" and "attack" right at the leash limit.
        if (toLeader.magnitude > leashNow || leaderLeaving) returning = true;
        else if (toLeader.magnitude <= followDistance + 1f) returning = false;

        var decision = returning ? default : RoleTactics.Decide(combatant, navigator, cautious ? settings.allyCautiousAggro : aggroRange);
        if (!returning && decision.slot >= 0 && Act(decision.slot, decision.aim, decision.distance)) return;
        if (returning || !decision.engaged)
        {
            Vector2 goal = SafeSpotNear(leader.position, grid);
            Vector2 toGoal = goal - position;
            float near = goal == (Vector2)leader.position ? followDistance : 0.3f;
            Vector2 follow = toGoal.magnitude > near ? navigator.Direction(position, goal, combatant, avoid: true) : Vector2.zero;
            bool cutOff = follow == Vector2.zero && !navigator.Reachable;
            if (toGoal.magnitude > near && (cutOff || Stuck(toGoal.magnitude, near)) && TryRepair(position)) return;
            follow = RoleTactics.Separate(combatant, grid, position, follow); // spread out around the leader
            // No safe way: waits at the edge rather than stepping into danger.
            if (follow != Vector2.zero && !navigator.SafePath && Dangerous(position + follow.normalized * EdgeLook)) follow = Vector2.zero;
            Steer(follow, toLeader);
            return;
        }
        if (TryPursue(decision, position, grid)) return;
        Steer(decision.move, decision.look);
    }

    bool Act(int slot, Vector2 aim, float distance)
    {
        if (!combatant.TryUseMove(slot, aim, distance)) return false;
        attackUntil = Time.time + AttackAnimTime;
        attackAnim = GameData.AttackAnim(combatant.Moves[slot], animator);
        attackDirection = aim;
        Steer(Vector2.zero, aim);
        return true;
    }

    // No way to the leader: walks to where a move makes one (freezes the river...), then uses it.
    bool TryRepair(Vector2 position)
    {
        if (Time.time >= nextRepair)
        {
            hasRepair = ObstacleTactics.Repair(combatant, leader.position, out repair);
            nextRepair = Time.time + RepairInterval;
        }
        if (!hasRepair) return false;
        if (ObstacleTactics.AtStand(combatant, repair))
        {
            if (combatant.CooldownLeft(repair.slot) <= 0f && Act(repair.slot, repair.aim, repair.distance))
            {
                nextRepair = 0f; // look again once the way changed
                return true;
            }
            Steer(Vector2.zero, repair.aim); // waits for the move to come back
            return true;
        }
        Steer(repairNavigator.Direction(position, repair.stand, combatant, avoid: true), repair.stand - position);
        return true;
    }

    // A team-mate is knocked out: guards it or brings it back (TeamRescue).
    bool TryRescue(Vector2 position, DungeonGenerator grid)
    {
        if (leaderSeenAs != leader)
        {
            leaderSeenAs = leader;
            leaderCombatant = leader != null ? leader.GetComponent<Combatant>() : null;
        }
        float radius = team != null ? team.reviveRadius : 1f;
        var job = TeamRescue.JobOf(combatant, leaderCombatant, radius, out var downed, out var threat);
        if (job == TeamRescue.Job.None) return false;

        if (job == TeamRescue.Job.Revive)
        {
            Vector2 toBody = (Vector2)downed.transform.position - position;
            // On it: stays, and only hits what is in reach from there.
            if (toBody.magnitude <= radius * 0.6f)
            {
                var hold = RoleTactics.Decide(combatant, navigator, aggroRange);
                if (hold.slot >= 0 && hold.foe != null && combatant.Moves[hold.slot].lunge <= 0f
                    && Act(hold.slot, hold.aim, hold.distance)) return true;
                Steer(Vector2.zero, hold.foe != null ? (Vector2)hold.foe.transform.position - position : toBody);
                return true;
            }
            Vector2 way = rescueNavigator.Direction(position, downed.transform.position, combatant, avoid: true);
            if (way != Vector2.zero && !rescueNavigator.SafePath && Dangerous(position + way.normalized * EdgeLook)) way = Vector2.zero;
            Steer(way, toBody);
            return true;
        }

        // Guard: fights the opponent nearest to the knocked-out one and draws it (and its group) away.
        TargetChoice.Taunt(combatant);
        var decision = RoleTactics.Decide(combatant, navigator, float.MaxValue, threat);
        if (decision.slot >= 0 && Act(decision.slot, decision.aim, decision.distance)) return true;
        bool ready = false;
        for (int i = 0; i < Combatant.MoveSlots; i++)
            if (combatant.Moves[i] != null && combatant.Moves[i].power > 0 && combatant.CooldownLeft(i) <= 0f) ready = true;
        Vector2 spot = TeamRescue.GuardSpot(combatant, downed, threat, ready);
        Vector2 toSpot = spot - position;
        Vector2 step = toSpot.magnitude > 0.4f ? rescueNavigator.Direction(position, spot, combatant, avoid: true) : Vector2.zero;
        Steer(step, (Vector2)threat.transform.position - position);
        return true;
    }

    // Following without getting closer for StuckTime seconds (distance to the goal down by less than StuckProgress).
    bool Stuck(float distance, float near)
    {
        if (distance <= near + 0.5f)
        {
            stuckSince = -1f;
            return false;
        }
        if (stuckSince < 0f || distance < stuckDistance - StuckProgress)
        {
            stuckSince = Time.time;
            stuckDistance = distance;
            return false;
        }
        return Time.time - stuckSince >= StuckTime;
    }

    /// True when standing there would hurt this ally: a harmful zone, terrain or electric arc.
    bool Dangerous(Vector2 point) =>
        Zone.Inside(point, combatant.team, RoleTactics.ZoneMargin) || Terrain.Hazard(point, combatant) || Pylons.Hazard(point, combatant);

    // The leader's position when it is safe, else the centre of the nearest safe tile around it (the leader itself
    // when there is none within reach).
    Vector2 SafeSpotNear(Vector2 target, DungeonGenerator grid)
    {
        // Also when the leader stands where this ally cannot (a flyer over water, lava or void).
        if (grid == null || (!Dangerous(target) && !Obstacles.BlocksAt(target, combatant))) return target;
        var cell = grid.CellOf(target);
        for (int r = 1; r <= SafeSearch; r++)
        {
            Vector2 best = target;
            float bestDistance = float.MaxValue;
            for (int x = cell.x - r; x <= cell.x + r; x++)
                for (int y = cell.y - r; y <= cell.y + r; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x - cell.x), Mathf.Abs(y - cell.y)) != r) continue;
                    if (!grid.Walkable(x, y) || Obstacles.Blocks(x, y, combatant)) continue;
                    Vector2 centre = grid.CentreOf(x, y);
                    if (Dangerous(centre)) continue;
                    float d = Vector2.Distance(centre, target) + 0.5f * Vector2.Distance(centre, transform.position);
                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        best = centre;
                    }
                }
            if (bestDistance < float.MaxValue) return best;
        }
        return target;
    }

    // A melee ally with nothing to use yet dodges straight at a target that keeps out of reach.
    bool TryPursue(RoleTactics.Decision decision, Vector2 position, DungeonGenerator grid)
    {
        if (combatant.Role != "melee" || decision.foe == null || decision.move == Vector2.zero) return false;
        if (combatant.hp < combatant.maxHp * PursuitHealth || combatant.DodgeCooldownLeft > 0f) return false;
        Vector2 toFoe = (Vector2)decision.foe.transform.position - position;
        float distance = toFoe.magnitude;
        if (distance < PursuitMin || distance > PursuitMax) return false;
        if (grid != null && !grid.LineClear(position, decision.foe.transform.position, 0.3f)) return false;
        Vector2 landing = position + toFoe.normalized * Mathf.Min(SaveData.Settings.dodgeDistance, distance - 1f);
        if (Zone.Inside(landing, combatant.team, RoleTactics.ZoneMargin)) return false; // not into a cloud
        if (!combatant.TryDodge(toFoe)) return false;
        Steer(toFoe, toFoe);
        return true;
    }

    void Steer(Vector2 direction, Vector2 lookAt)
    {
        combatant.Drive(combatant.Steer(direction) * combatant.CurrentSpeed * speedFactor);
        if (Time.time < attackUntil)
        {
            animator.direction = PmdAnimator.DirectionIndex(attackDirection);
            animator.Play(attackAnim);
        }
        else if (direction != Vector2.zero)
        {
            animator.direction = PmdAnimator.DirectionIndex(direction);
            animator.Play("Walk");
        }
        else
        {
            if (lookAt != Vector2.zero) animator.direction = PmdAnimator.DirectionIndex(lookAt);
            animator.Play("Idle");
        }
    }

    /// Forgets where the leader was (it was moved by hand: the jump is not the leader falling back).
    public void ForgetLeaderMotion()
    {
        leaderSeen = false;
        leaderVelocity = Vector2.zero;
        returning = false;
    }

    void OnDisable()
    {
        if (body != null && combatant != null && combatant.IsAlive) body.linearVelocity = Vector2.zero;
    }
}
