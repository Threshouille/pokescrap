using UnityEngine;

/// Team member played by the computer. Its role (melee, ranged or support, see RoleTactics) decides how it fights;
/// this class adds what is specific to an ally: following the leader, and never straying too far from it.
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

    [Tooltip("The Pokémon the player controls (the team object follows it)")]
    public Transform leader;
    [Tooltip("Distance kept from the leader when there is nothing to fight")]
    public float followDistance = 2f;
    [Tooltip("Beyond this distance from the leader, the ally drops its target and comes back")]
    public float leash = 7f;
    [Tooltip("Enemies farther than this are ignored")]
    public float aggroRange = 7f;
    [Tooltip("Share of the Pokémon's own speed used by the AI")]
    public float speedFactor = 0.9f;

    readonly Navigator navigator = new();
    Rigidbody2D body;
    PmdAnimator animator;
    Combatant combatant;
    float attackUntil;
    string attackAnim = "Attack";
    Vector2 attackDirection;
    bool returning; // set when the leash is exceeded, cleared once back next to the leader

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<PmdAnimator>();
        combatant = GetComponent<Combatant>();
    }

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
        if (escape != Vector2.zero)
        {
            Steer(RoleTactics.EscapeStep(grid, position, escape), escape);
            return;
        }

        Vector2 toLeader = (Vector2)leader.position - position;
        // Without this memory the ally would flip between "come back" and "attack" right at the leash limit.
        if (toLeader.magnitude > leash) returning = true;
        else if (toLeader.magnitude <= followDistance + 1f) returning = false;

        var decision = returning ? default : RoleTactics.Decide(combatant, navigator, aggroRange);
        if (!returning && decision.slot >= 0 && combatant.TryUseMove(decision.slot, decision.aim, decision.distance))
        {
            var move = combatant.Moves[decision.slot];
            attackUntil = Time.time + AttackAnimTime;
            attackAnim = GameData.AttackAnim(move, animator);
            attackDirection = decision.aim;
            Steer(Vector2.zero, decision.aim);
            return;
        }
        if (returning || !decision.engaged)
        {
            Vector2 follow = toLeader.magnitude > followDistance ? navigator.Direction(position, leader.position, combatant) : Vector2.zero;
            Steer(RoleTactics.Separate(combatant, grid, position, follow), toLeader); // spread out around the leader
            return;
        }
        if (TryPursue(decision, position, grid)) return;
        Steer(decision.move, decision.look);
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

    void OnDisable()
    {
        if (body != null && combatant != null && combatant.IsAlive) body.linearVelocity = Vector2.zero;
    }
}
