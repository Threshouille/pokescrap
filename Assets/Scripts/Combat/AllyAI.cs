using UnityEngine;

/// Team member played by the computer. Its role (melee, ranged or support, see RoleTactics) decides how it fights;
/// this class adds what is specific to an ally: following the leader, and never straying too far from it.
/// It walks around walls and harmful zones, but does not dodge announced attacks yet.
[RequireComponent(typeof(Rigidbody2D), typeof(PmdAnimator), typeof(Combatant))]
public class AllyAI : MonoBehaviour
{
    const float AttackAnimTime = 0.3f;

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
        if (leader == null || !combatant.IsAlive || combatant.IsLunging) return; // a melee dash drives the body

        Vector2 position = transform.position;
        var grid = DungeonGenerator.Current;

        // Getting out of a harmful zone comes before anything else.
        Vector2 escape = Zone.EscapeDirection(position, combatant.team, RoleTactics.ZoneMargin);
        if (escape != Vector2.zero)
        {
            Steer(RoleTactics.Passable(combatant, grid, position, escape) ? escape : new Vector2(-escape.y, escape.x), escape);
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
            attackAnim = move.family != "melee" && animator.Has("Shoot") ? "Shoot" : "Attack";
            attackDirection = decision.aim;
            Steer(Vector2.zero, decision.aim);
            return;
        }
        if (returning || !decision.engaged)
        {
            Vector2 follow = toLeader.magnitude > followDistance ? navigator.Direction(position, leader.position, combatant.team) : Vector2.zero;
            Steer(RoleTactics.Separate(combatant, grid, position, follow), toLeader); // spread out around the leader
            return;
        }
        Steer(decision.move, decision.look);
    }

    void Steer(Vector2 direction, Vector2 lookAt)
    {
        body.linearVelocity = direction * combatant.CurrentSpeed * speedFactor;
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
