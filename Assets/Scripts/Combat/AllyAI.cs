using UnityEngine;

/// Team member played by the computer: stays near the leader, engages the nearest enemy with whatever move is ready.
/// It walks around walls and harmful zones (and leaves a zone it stands in), but does not dodge announced attacks yet.
[RequireComponent(typeof(Rigidbody2D), typeof(PmdAnimator), typeof(Combatant))]
public class AllyAI : MonoBehaviour
{
    const float AttackAnimTime = 0.3f;
    const float ZoneMargin = 0.4f; // tiles kept between the ally and the edge of a harmful zone
    const float ShotRadius = 0.2f; // clearance needed along a line of fire

    [Tooltip("The Pokémon the player controls (the team object follows it)")]
    public Transform leader;
    [Tooltip("Distance kept from the leader when there is nothing to fight")]
    public float followDistance = 2f;
    [Tooltip("Beyond this distance from the leader, the ally drops its target and comes back")]
    public float leash = 7f;
    [Tooltip("Enemies farther than this are ignored")]
    public float aggroRange = 7f;
    [Tooltip("Distance kept from the target while every move is recharging")]
    public float holdDistance = 3.5f;
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
        Vector2 escape = Zone.EscapeDirection(position, combatant.team, ZoneMargin);
        if (escape != Vector2.zero)
        {
            Steer(Passable(grid, position, escape) ? escape : new Vector2(-escape.y, escape.x), escape);
            return;
        }

        Vector2 toLeader = (Vector2)leader.position - position;
        // Without this memory the ally would flip between "come back" and "attack" right at the leash limit.
        if (toLeader.magnitude > leash) returning = true;
        else if (toLeader.magnitude <= followDistance + 1f) returning = false;
        var target = returning ? null : Combatant.Nearest(Team.Enemy, position, aggroRange);
        if (target == null)
        {
            Steer(toLeader.magnitude > followDistance ? navigator.Direction(position, leader.position, combatant.team) : Vector2.zero, toLeader);
            return;
        }

        Vector2 targetPosition = target.transform.position;
        Vector2 toTarget = targetPosition - position;
        float distance = toTarget.magnitude;
        bool lineOfFire = grid == null || grid.LineClear(position, targetPosition, ShotRadius);

        // Use the first ready move that reaches; otherwise close in to the shortest reach among ready moves.
        float wanted = float.MaxValue;
        for (int slot = 0; slot < Combatant.MoveSlots; slot++)
        {
            var move = combatant.Moves[slot];
            if (move == null || combatant.CooldownLeft(slot) > 0f) continue;
            bool usable = distance <= move.Reach && (lineOfFire || move.family == "zone")
                && !(move.lunge > 0f && Zone.Inside(targetPosition, combatant.team, ZoneMargin)); // no dashing into a cloud
            if (usable && combatant.TryUseMove(slot, toTarget, distance))
            {
                attackUntil = Time.time + AttackAnimTime;
                attackAnim = move.family != "melee" && animator.Has("Shoot") ? "Shoot" : "Attack";
                attackDirection = toTarget;
                Steer(Vector2.zero, toTarget);
                return;
            }
            wanted = Mathf.Min(wanted, move.Reach * 0.9f);
        }

        Vector2 direction = Vector2.zero;
        if (wanted < float.MaxValue)
        {
            // Behind a wall, being in range is not enough: keep walking until the target is in sight.
            if (distance > wanted || !lineOfFire) direction = navigator.Direction(position, targetPosition, combatant.team);
        }
        else if (distance > holdDistance + 0.5f) direction = navigator.Direction(position, targetPosition, combatant.team);
        else if (distance < holdDistance - 1f && Passable(grid, position, -toTarget.normalized))
            direction = -toTarget.normalized; // back off while recharging
        Steer(direction, toTarget);
    }

    /// True when a short step in that direction stays on the floor and out of harmful zones.
    bool Passable(DungeonGenerator grid, Vector2 position, Vector2 direction)
    {
        Vector2 ahead = position + direction * 0.7f;
        return (grid == null || grid.WalkableAt(ahead, 0.3f)) && !Zone.Inside(ahead, combatant.team, ZoneMargin);
    }

    void Steer(Vector2 direction, Vector2 lookAt)
    {
        body.linearVelocity = direction * combatant.moveSpeed * speedFactor;
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
