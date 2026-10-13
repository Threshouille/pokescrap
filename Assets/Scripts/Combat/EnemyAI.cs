using UnityEngine;

/// Enemy behaviour: approach the nearest team member, announce the attack (wind-up with a visible danger zone),
/// strike, recover. It picks the first ready move that reaches; ranged enemies keep their distance while recharging.
/// It walks around walls and harmful zones, and only attacks a target it can reach in a straight line.
[RequireComponent(typeof(Rigidbody2D), typeof(PmdAnimator), typeof(Combatant))]
public class EnemyAI : MonoBehaviour
{
    enum State { Chase, Windup, Recover }

    const float DefaultWindup = 0.5f;
    const float MeleeStandOff = 1.5f;
    const float ZoneMargin = 0.4f; // tiles kept between the enemy and the edge of a harmful zone
    const float ShotRadius = 0.2f; // clearance needed along a line of fire
    static readonly Color TelegraphColor = new(1f, 0.15f, 0.15f, 0.35f);

    [Tooltip("Share of the Pokémon's own speed used while moving")]
    public float speedFactor = 0.6f;
    [Tooltip("Pause after each attack, on top of the move's cooldown")]
    public float recoverTime = 1.5f;
    [Tooltip("Distance a ranged enemy keeps from its target while every move is recharging")]
    public float holdDistance = 4f;

    readonly Navigator navigator = new();
    Rigidbody2D body;
    PmdAnimator animator;
    Combatant combatant;
    State state;
    float stateUntil;
    int attackSlot;
    Vector2 attackDirection;
    float attackDistance;
    GameObject telegraph;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<PmdAnimator>();
        combatant = GetComponent<Combatant>();
    }

    void OnDestroy()
    {
        if (telegraph != null) Destroy(telegraph);
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case State.Chase:
                Chase();
                break;

            case State.Windup:
                body.linearVelocity = Vector2.zero;
                if (Time.time < stateUntil) break;
                if (telegraph != null) Destroy(telegraph);
                var move = combatant.Moves[attackSlot];
                combatant.TryUseMove(attackSlot, attackDirection, attackDistance);
                animator.Play(move.family != "melee" && animator.Has("Shoot") ? "Shoot" : animator.Has("Attack") ? "Attack" : "Idle");
                state = State.Recover;
                stateUntil = Time.time + recoverTime;
                break;

            case State.Recover:
                if (combatant.IsLunging) break; // the dash drives the body
                body.linearVelocity = Vector2.zero;
                if (Time.time >= stateUntil) state = State.Chase;
                break;
        }
    }

    void Chase()
    {
        // Always goes for the nearest team member still standing.
        var target = Combatant.Nearest(Team.Player, transform.position, float.MaxValue);
        if (target == null)
        {
            body.linearVelocity = Vector2.zero;
            animator.Play("Idle");
            return;
        }
        Vector2 position = transform.position;
        Vector2 targetPosition = target.transform.position;
        Vector2 toTarget = targetPosition - position;
        float distance = toTarget.magnitude;
        var grid = DungeonGenerator.Current;

        // Getting out of a harmful zone comes before anything else.
        Vector2 escape = Zone.EscapeDirection(position, combatant.team, ZoneMargin);
        if (escape != Vector2.zero)
        {
            Move(Passable(grid, position, escape) ? escape : new Vector2(-escape.y, escape.x), toTarget);
            return;
        }

        bool lineOfFire = grid == null || grid.LineClear(position, targetPosition, ShotRadius);
        float wanted = float.MaxValue;
        bool ranged = false;
        for (int slot = 0; slot < Combatant.MoveSlots; slot++)
        {
            var move = combatant.Moves[slot];
            if (move == null) continue;
            if (move.family != "melee") ranged = true;
            if (combatant.CooldownLeft(slot) > 0f) continue;
            if (distance <= move.Reach && toTarget != Vector2.zero && (lineOfFire || move.family == "zone"))
            {
                StartWindup(slot, move, toTarget.normalized, distance);
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
        else if (ranged)
        {
            if (distance > holdDistance + 0.5f) direction = navigator.Direction(position, targetPosition, combatant.team);
            else if (distance < holdDistance - 1f && Passable(grid, position, -toTarget.normalized))
                direction = -toTarget.normalized; // back off while recharging
        }
        else if (distance > MeleeStandOff) direction = navigator.Direction(position, targetPosition, combatant.team);

        Move(direction, toTarget);
    }

    void Move(Vector2 direction, Vector2 lookAt)
    {
        body.linearVelocity = direction * combatant.moveSpeed * speedFactor;
        animator.direction = PmdAnimator.DirectionIndex(direction != Vector2.zero ? direction : lookAt);
        animator.Play(direction != Vector2.zero ? "Walk" : "Idle");
    }

    /// True when a short step in that direction stays on the floor and out of harmful zones.
    bool Passable(DungeonGenerator grid, Vector2 position, Vector2 direction)
    {
        Vector2 ahead = position + direction * 0.7f;
        return (grid == null || grid.WalkableAt(ahead, 0.3f)) && !Zone.Inside(ahead, combatant.team, ZoneMargin);
    }

    // Direction and landing point are locked when the wind-up starts: leaving the red area dodges the attack.
    void StartWindup(int slot, MoveDef move, Vector2 direction, float distance)
    {
        attackSlot = slot;
        attackDirection = direction;
        attackDistance = distance;
        state = State.Windup;
        stateUntil = Time.time + (move.windup > 0f ? move.windup : DefaultWindup);
        body.linearVelocity = Vector2.zero;
        animator.direction = PmdAnimator.DirectionIndex(direction);
        animator.Play("Idle");

        telegraph = new GameObject("Telegraph");
        var renderer = telegraph.AddComponent<SpriteRenderer>();
        renderer.color = TelegraphColor;
        renderer.sortingOrder = 0; // above the floor, under the Pokémon

        if (move.family == "zone")
        {
            // Disc where the zone will land.
            Circle(renderer, transform.position + (Vector3)(direction * Mathf.Min(distance, move.range)), move.radius);
        }
        else if (move.family == "projectile")
        {
            // Thin strip along the flight path.
            Strip(renderer, direction, move.range, move.radius);
        }
        else if (move.lunge > 0f)
        {
            // Strip covering the whole dash, from the enemy to the far edge of the last hit zone.
            Strip(renderer, direction, move.lunge + move.range + move.radius, move.radius);
        }
        else
        {
            Circle(renderer, transform.position + (Vector3)(direction * move.range), move.radius);
        }
    }

    void Circle(SpriteRenderer renderer, Vector3 centre, float radius)
    {
        renderer.sprite = Shapes.Circle;
        renderer.transform.position = centre;
        renderer.transform.localScale = Vector3.one * radius * 2f;
    }

    void Strip(SpriteRenderer renderer, Vector2 direction, float length, float halfWidth)
    {
        renderer.sprite = Shapes.Square;
        renderer.transform.position = transform.position + (Vector3)(direction * length / 2f);
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        renderer.transform.localScale = new Vector3(length, halfWidth * 2f, 1f);
    }
}
