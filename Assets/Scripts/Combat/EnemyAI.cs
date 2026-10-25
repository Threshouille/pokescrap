using UnityEngine;

/// Enemy played by the computer. Its role (melee, ranged or support, see RoleTactics) decides how it fights;
/// this class adds what is specific to an enemy: every move is announced (wind-up with a visible danger area)
/// before it is used; it keeps moving right after, but waits a moment before announcing the next one. It walks
/// around walls and harmful zones. Its dodge lets a ranged or support enemy flee an opponent that gets too close,
/// and a melee enemy rush at its target. An elite cut off from its target by water, lava or void changes them with
/// its moves to make a way (ObstacleTactics.Repair), as allies do; so do the higher ranks (encounters.json, repairs).
[RequireComponent(typeof(Rigidbody2D), typeof(PmdAnimator), typeof(Combatant))]
public class EnemyAI : MonoBehaviour
{
    enum State { Chase, Windup }

    const float DefaultWindup = 0.5f;
    const float AttackAnimTime = 0.3f;
    const float RepairInterval = 0.5f;
    const float FleeDistance = 2.5f; // ranged and support: dodge away from an opponent this close
    const float RushMin = 3f, RushMax = 6f; // melee: dodge toward a target this far away
    static readonly Color TelegraphColor = new(1f, 0.15f, 0.15f, 0.35f);

    [Tooltip("Share of the Pokémon's own speed used while moving")]
    public float speedFactor = 0.6f;
    [Tooltip("Delay after each attack before the next announcement (the enemy keeps moving)")]
    public float recoverTime = 1.5f;

    [System.NonSerialized] public EnemyGroup group; // set by EnemySpawner: the group's members share their target

    readonly Navigator navigator = new();
    readonly Navigator repairNavigator = new();
    ObstacleTactics.Plan repair;
    readonly Progress towardFoe = new(); // stops getting closer to its target: maybe something to repair
    // Walking direction, eased toward the wanted one: in a tight group the pull to the target and the neighbours' push
    // swapped from one frame to the next and the enemy turned back several times a second.
    const float SteerEase = 0.25f;
    Vector2 walking;
    bool hasRepair;
    float nextRepair;
    Rigidbody2D body;
    PmdAnimator animator;
    Combatant combatant;
    State state;
    float stateUntil;
    float nextAttackAt, attackAnimUntil;
    string attackAnim;
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
                if (!combatant.IsDashing) combatant.Drive(Vector2.zero); // a push still carries it away
                if (Time.time < stateUntil) break;
                if (telegraph != null) Destroy(telegraph);
                var move = combatant.Moves[attackSlot];
                combatant.TryUseMove(attackSlot, attackDirection, attackDistance);
                attackAnim = GameData.AttackAnim(move, animator);
                attackAnimUntil = Time.time + AttackAnimTime;
                animator.Play(attackAnim);
                state = State.Chase;
                nextAttackAt = Time.time + recoverTime;
                break;
        }
    }

    void Chase()
    {
        if (combatant.IsDashing) return; // the dash drives the body
        Vector2 position = transform.position;
        var grid = DungeonGenerator.Current;

        // Getting out of a harmful zone comes before anything else.
        Vector2 escape = Zone.EscapeDirection(position, combatant.team, RoleTactics.ZoneMargin);
        if (escape == Vector2.zero) escape = Terrain.EscapeDirection(position, combatant);
        if (escape == Vector2.zero) escape = Pylons.EscapeDirection(position, combatant);
        if (escape != Vector2.zero)
        {
            Move(RoleTactics.EscapeStep(grid, position, escape), escape);
            return;
        }

        bool canAttack = Time.time >= nextAttackAt;
        var decision = RoleTactics.Decide(combatant, navigator, float.MaxValue, group?.Target(), canAttack);
        if (TryDodge(decision, position, grid)) return;
        // Elites and the higher ranks (RarityMark.Repairs) make a way to a target they cannot reach.
        if (decision.slot < 0 && decision.foe != null && (combatant.IsElite || RarityMark.Repairs(combatant)))
        {
            Vector2 foeAt = decision.foe.transform.position;
            if ((!navigator.Reachable || towardFoe.Stuck(Vector2.Distance(position, foeAt))) && TryRepair(position, foeAt, canAttack)) return;
        }
        else towardFoe.Reset();
        if (decision.slot >= 0)
        {
            Vector2 aim = decision.aim != Vector2.zero ? decision.aim.normalized : Vector2.right;
            StartWindup(decision.slot, combatant.Moves[decision.slot], aim, decision.distance);
            return;
        }
        Move(decision.engaged ? decision.move : Vector2.zero, decision.look);
    }

    // No way to its target: walks to where a move makes one (freezes the river...), then announces it.
    bool TryRepair(Vector2 position, Vector2 goal, bool canAttack)
    {
        if (Time.time >= nextRepair)
        {
            hasRepair = ObstacleTactics.Repair(combatant, goal, out repair);
            nextRepair = Time.time + RepairInterval;
        }
        if (!hasRepair) return false;
        if (ObstacleTactics.AtStand(combatant, repair))
        {
            if (canAttack && combatant.CooldownLeft(repair.slot) <= 0f)
            {
                StartWindup(repair.slot, combatant.Moves[repair.slot], repair.aim.normalized, repair.distance);
                nextRepair = 0f; // look again once the way changed
                return true;
            }
            Move(Vector2.zero, repair.aim); // waits for the move to come back
            return true;
        }
        Move(repairNavigator.Direction(position, repair.stand, combatant, avoid: true), repair.stand - position);
        return true;
    }

    bool TryDodge(RoleTactics.Decision decision, Vector2 position, DungeonGenerator grid)
    {
        if (combatant.DodgeCooldownLeft > 0f) return false;
        Vector2 direction = Vector2.zero;
        if (combatant.Role != "melee")
        {
            // Flees the nearest opponent when it gets too close; cornered, it stays and fights.
            Team opponents = combatant.team == Team.Player ? Team.Enemy : Team.Player;
            var near = Combatant.Nearest(opponents, position, FleeDistance);
            if (near != null) direction = RoleTactics.Away(combatant, grid, position, (Vector2)near.transform.position - position);
        }
        else if (decision.slot < 0 && decision.foe != null)
        {
            // Rushes at a target out of reach, unless the way is blocked or ends in a harmful zone.
            Vector2 toFoe = (Vector2)decision.foe.transform.position - position;
            float distance = toFoe.magnitude;
            Vector2 landing = position + toFoe.normalized * Mathf.Min(SaveData.Settings.dodgeDistance, distance - 1f);
            if (distance >= RushMin && distance <= RushMax
                && (grid == null || grid.LineClear(position, decision.foe.transform.position, 0.3f))
                && !Zone.Inside(landing, combatant.team, RoleTactics.ZoneMargin))
                direction = toFoe;
        }
        if (direction == Vector2.zero || !combatant.TryDodge(direction)) return false;
        animator.direction = PmdAnimator.DirectionIndex(direction);
        animator.Play("Walk");
        return true;
    }

    void Move(Vector2 direction, Vector2 lookAt)
    {
        walking = Vector2.Lerp(walking, direction, SteerEase);
        if (walking.sqrMagnitude < 0.0025f) walking = Vector2.zero;
        direction = walking;
        combatant.Drive(combatant.Steer(direction) * combatant.CurrentSpeed * speedFactor);
        if (Time.time < attackAnimUntil)
        {
            animator.Play(attackAnim);
            return;
        }
        Vector2 facing = direction != Vector2.zero ? direction : lookAt;
        animator.Face(facing);
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
        combatant.Drive(Vector2.zero);
        animator.direction = PmdAnimator.DirectionIndex(direction);
        animator.Play("Idle");

        // Heals and bonuses threaten nobody: no danger area.
        if (move.family == "heal" || (move.family == "modifier" && move.target != "enemies")) return;

        telegraph = new GameObject("Telegraph");
        var renderer = telegraph.AddComponent<SpriteRenderer>();
        Unlit.Apply(renderer); // keeps its colours at night (DayNight)
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
        else if (move.family == "projectile" && move.path == "arc")
        {
            // Disc where the shot will land.
            Circle(renderer, transform.position + (Vector3)(direction * Mathf.Min(distance, move.range)), Mathf.Max(move.radius, move.splash));
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
        DangerArea.AddCircle(renderer.gameObject, combatant.team, centre, radius);
    }

    void Strip(SpriteRenderer renderer, Vector2 direction, float length, float halfWidth)
    {
        renderer.sprite = Shapes.Square;
        renderer.transform.position = transform.position + (Vector3)(direction * length / 2f);
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        renderer.transform.localScale = new Vector3(length, halfWidth * 2f, 1f);
        DangerArea.AddStrip(renderer.gameObject, combatant.team, transform.position, direction, length, halfWidth);
    }
}
