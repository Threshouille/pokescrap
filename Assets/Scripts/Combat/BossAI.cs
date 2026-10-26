using System.Collections.Generic;
using UnityEngine;

/// Boss behaviour: walks towards the team, then announces one of its moves, picked at random, with a numbered
/// bubble above its head. The number is the only warning the player gets: there is no danger area on the ground,
/// each number always means the same move, and the patterns have to be learned. Announcements get shorter once the
/// boss is under half of its health.
[RequireComponent(typeof(Rigidbody2D), typeof(PmdAnimator), typeof(Combatant))]
public class BossAI : MonoBehaviour
{
    enum State { Approach, Announce, Recover }

    const float DefaultWindup = 1f;
    const float MinApproach = 0.6f, MaxApproach = 3f; // seconds walking before the next announcement
    static readonly Vector3 BubbleOffset = new(0f, 1.25f, 0f);
    const float TauntReach = 12f; // tiles: a guard this close draws the boss

    [Tooltip("Share of the Pokémon's own speed used while moving")]
    public float speedFactor = 0.5f;
    [Tooltip("The boss stops walking this close to its target")]
    public float approachDistance = 4f;
    [Tooltip("Pause after each move")]
    public float recoverTime = 1.2f;
    [Tooltip("Under this share of health, the announcements are shortened")]
    public float enragedHealth = 0.5f;
    [Tooltip("Announcement time multiplier once enraged")]
    public float enragedAnnounce = 0.6f;

    /// An island boss (TimeGears): it picks its move first and walks to that move's reach, circles its target while
    /// waiting, and its pause after a move shrinks with its recharges (its second phase).
    [System.NonSerialized] public bool seeksReach;
    const float StrafeSwap = 2.5f; // seconds before it circles the other way
    int planned = -1;

    public bool Enraged => combatant.maxHp > 0f && combatant.hp / combatant.maxHp < enragedHealth;

    readonly Navigator navigator = new();
    readonly List<int> choices = new();
    Rigidbody2D body;
    PmdAnimator animator;
    Combatant combatant;
    SpriteRenderer bubble;
    State state;
    float stateSince, stateUntil;
    int slot;
    Vector2 aim;
    float aimDistance;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        animator = GetComponent<PmdAnimator>();
        combatant = GetComponent<Combatant>();

        var go = new GameObject("Bubble");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = BubbleOffset;
        bubble = go.AddComponent<SpriteRenderer>();
        bubble.sortingOrder = 13; // above everything attached to a Pokémon
        go.SetActive(false);
        stateSince = Time.time;
    }

    /// Asleep where it was put (a quarter's boss on the island, TimeGears): until a member of the team comes within
    /// that many tiles, or it is hurt.
    public void Sleep(float wakeRadius)
    {
        asleep = true;
        sleepRadius = wakeRadius;
        sleepHealth = combatant != null ? combatant.hp : float.MaxValue;
    }

    public bool Asleep => asleep;

    bool asleep;
    float sleepRadius, sleepHealth;

    void FixedUpdate()
    {
        if (!combatant.IsAlive) return;
        if (asleep)
        {
            var near = Combatant.Nearest(Team.Player, transform.position, sleepRadius);
            if (near == null && combatant.hp >= sleepHealth - 0.5f)
            {
                combatant.Drive(Vector2.zero);
                animator.Play(animator.Has("Sleep") ? "Sleep" : "Idle");
                return;
            }
            asleep = false;
        }
        // The nearest member of the team, unless one guards a knocked-out team-mate (TeamRescue): it draws the boss.
        var target = TargetChoice.Taunter(Team.Player, transform.position, TauntReach);
        if (target == null) target = Combatant.Nearest(Team.Player, transform.position, float.MaxValue);

        switch (state)
        {
            case State.Approach:
                if (target == null)
                {
                    combatant.Drive(Vector2.zero);
                    animator.Play("Idle");
                    break;
                }
                Vector2 toTarget = target.transform.position - transform.position;
                float reach = approachDistance;
                if (seeksReach)
                {
                    if (planned < 0 || combatant.Moves[planned] == null) planned = PickSlot();
                    reach = planned >= 0 ? Reach(combatant.Moves[planned]) : approachDistance;
                }
                bool close = toTarget.magnitude <= reach;
                Vector2 step = close ? Vector2.zero : navigator.Direction(transform.position, target.transform.position, combatant);
                // Within reach but still waiting: it circles its target instead of standing still.
                if (seeksReach && close && toTarget != Vector2.zero)
                    step = new Vector2(-toTarget.y, toTarget.x).normalized * (Mathf.Repeat(Time.time, StrafeSwap * 2f) < StrafeSwap ? 1f : -1f) * 0.6f;
                combatant.Drive(combatant.Steer(step) * combatant.CurrentSpeed * speedFactor);
                animator.direction = PmdAnimator.DirectionIndex(step != Vector2.zero ? step : toTarget);
                animator.Play(step != Vector2.zero ? "Walk" : "Idle");

                float walked = Time.time - stateSince;
                if (walked >= MinApproach && (close || walked >= MaxApproach * (seeksReach ? 2f : 1f)) && toTarget != Vector2.zero) Announce(toTarget);
                break;

            case State.Announce:
                combatant.Drive(Vector2.zero);
                if (Time.time < stateUntil) break;
                bubble.gameObject.SetActive(false);
                var move = combatant.Moves[slot];
                combatant.TryUseMove(slot, aim, aimDistance);
                animator.Play(GameData.AttackAnim(move, animator));
                Enter(State.Recover, recoverTime * (seeksReach ? combatant.cooldownMul : 1f));
                break;

            case State.Recover:
                if (combatant.IsDashing) break; // the dash drives the body
                combatant.Drive(Vector2.zero);
                if (Time.time >= stateUntil) Enter(State.Approach, 0f);
                break;
        }
    }

    void Enter(State next, float duration)
    {
        state = next;
        stateSince = Time.time;
        stateUntil = Time.time + duration;
    }

    // The move is drawn at random among those the boss has; the aim is locked when the bubble appears.
    int PickSlot()
    {
        choices.Clear();
        for (int i = 0; i < Combatant.MoveSlots; i++)
            if (combatant.Moves[i] != null) choices.Add(i);
        return choices.Count > 0 ? choices[Random.Range(0, choices.Count)] : -1;
    }

    // How close it wants to be to use that move: in contact for a strike, within its flight for a shot; anywhere for
    // a move on itself (heal, bonus, weather).
    static float Reach(MoveDef move) => move.family switch
    {
        "melee" => move.lunge > 0f ? Mathf.Max(1.5f, move.lunge * 0.8f) : Mathf.Max(1.2f, move.radius + 0.6f),
        "projectile" => Mathf.Clamp(move.range * 0.7f, 2f, 6f),
        "zone" => Mathf.Clamp(move.range > 0f ? move.range : 5f, 2f, 6f),
        "burst" => Mathf.Max(1.5f, move.radius * 0.8f),
        _ => float.MaxValue,
    };

    void Announce(Vector2 toTarget)
    {
        choices.Clear();
        for (int i = 0; i < Combatant.MoveSlots; i++)
            if (combatant.Moves[i] != null) choices.Add(i);
        if (choices.Count == 0) return;

        slot = planned >= 0 && combatant.Moves[planned] != null ? planned : choices[Random.Range(0, choices.Count)];
        planned = -1;
        aim = toTarget.normalized;
        aimDistance = toTarget.magnitude;
        var move = combatant.Moves[slot];
        float windup = (move.windup > 0f ? move.windup : DefaultWindup) * (Enraged ? enragedAnnounce : 1f);

        combatant.Drive(Vector2.zero);
        animator.direction = PmdAnimator.DirectionIndex(aim);
        animator.Play("Idle");
        bubble.sprite = Shapes.NumberBubble(slot + 1);
        bubble.gameObject.SetActive(true);
        Enter(State.Announce, windup);
    }
}
