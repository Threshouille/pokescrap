using UnityEngine;

/// Enemy played by the computer. Its role (melee, ranged or support, see RoleTactics) decides how it fights;
/// this class adds what is specific to an enemy: every move is announced (wind-up with a visible danger area)
/// before it is used, then the enemy pauses. It walks around walls and harmful zones.
[RequireComponent(typeof(Rigidbody2D), typeof(PmdAnimator), typeof(Combatant))]
public class EnemyAI : MonoBehaviour
{
    enum State { Chase, Windup, Recover }

    const float DefaultWindup = 0.5f;
    static readonly Color TelegraphColor = new(1f, 0.15f, 0.15f, 0.35f);

    [Tooltip("Share of the Pokémon's own speed used while moving")]
    public float speedFactor = 0.6f;
    [Tooltip("Pause after each attack, on top of the move's cooldown")]
    public float recoverTime = 1.5f;

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
        Vector2 position = transform.position;
        var grid = DungeonGenerator.Current;

        // Getting out of a harmful zone comes before anything else.
        Vector2 escape = Zone.EscapeDirection(position, combatant.team, RoleTactics.ZoneMargin);
        if (escape != Vector2.zero)
        {
            Move(RoleTactics.Passable(combatant, grid, position, escape) ? escape : new Vector2(-escape.y, escape.x), escape);
            return;
        }

        var decision = RoleTactics.Decide(combatant, navigator, float.MaxValue);
        if (decision.slot >= 0)
        {
            Vector2 aim = decision.aim != Vector2.zero ? decision.aim.normalized : Vector2.right;
            StartWindup(decision.slot, combatant.Moves[decision.slot], aim, decision.distance);
            return;
        }
        Move(decision.engaged ? decision.move : Vector2.zero, decision.look);
    }

    void Move(Vector2 direction, Vector2 lookAt)
    {
        body.linearVelocity = direction * combatant.CurrentSpeed * speedFactor;
        Vector2 facing = direction != Vector2.zero ? direction : lookAt;
        if (facing != Vector2.zero) animator.direction = PmdAnimator.DirectionIndex(facing);
        animator.Play(direction != Vector2.zero ? "Walk" : "Idle");
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

        // Heals and bonuses threaten nobody: no danger area.
        if (move.family == "heal" || (move.family == "modifier" && move.target != "enemies")) return;

        telegraph = new GameObject("Telegraph");
        var renderer = telegraph.AddComponent<SpriteRenderer>();
        renderer.color = TelegraphColor;
        renderer.sortingOrder = 0; // above the floor, under the Pokémon

        if (move.family == "modifier")
        {
            // Penalty cast around the enemy itself.
            Circle(renderer, transform.position, move.radius);
        }
        else if (move.family == "zone")
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
