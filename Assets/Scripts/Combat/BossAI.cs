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

    public bool Enraged => combatant.maxHp > 0f && combatant.hp / combatant.maxHp < enragedHealth;
    /// Number shown in the bubble (1 to 4), or 0 when the boss is not announcing anything.
    public int Announcing => state == State.Announce ? slot + 1 : 0;

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

    void FixedUpdate()
    {
        if (!combatant.IsAlive) return;
        var target = Combatant.Nearest(Team.Player, transform.position, float.MaxValue);

        switch (state)
        {
            case State.Approach:
                if (target == null)
                {
                    body.linearVelocity = Vector2.zero;
                    animator.Play("Idle");
                    break;
                }
                Vector2 toTarget = target.transform.position - transform.position;
                bool close = toTarget.magnitude <= approachDistance;
                Vector2 step = close ? Vector2.zero : navigator.Direction(transform.position, target.transform.position, combatant.team);
                body.linearVelocity = combatant.Steer(step) * combatant.CurrentSpeed * speedFactor;
                animator.direction = PmdAnimator.DirectionIndex(step != Vector2.zero ? step : toTarget);
                animator.Play(step != Vector2.zero ? "Walk" : "Idle");

                float walked = Time.time - stateSince;
                if (walked >= MinApproach && (close || walked >= MaxApproach) && toTarget != Vector2.zero) Announce(toTarget);
                break;

            case State.Announce:
                body.linearVelocity = Vector2.zero;
                if (Time.time < stateUntil) break;
                bubble.gameObject.SetActive(false);
                var move = combatant.Moves[slot];
                combatant.TryUseMove(slot, aim, aimDistance);
                animator.Play(GameData.AttackAnim(move, animator));
                Enter(State.Recover, recoverTime);
                break;

            case State.Recover:
                if (combatant.IsDashing) break; // the dash drives the body
                body.linearVelocity = Vector2.zero;
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
    void Announce(Vector2 toTarget)
    {
        choices.Clear();
        for (int i = 0; i < Combatant.MoveSlots; i++)
            if (combatant.Moves[i] != null) choices.Add(i);
        if (choices.Count == 0) return;

        slot = choices[Random.Range(0, choices.Count)];
        aim = toTarget.normalized;
        aimDistance = toTarget.magnitude;
        var move = combatant.Moves[slot];
        float windup = (move.windup > 0f ? move.windup : DefaultWindup) * (Enraged ? enragedAnnounce : 1f);

        body.linearVelocity = Vector2.zero;
        animator.direction = PmdAnimator.DirectionIndex(aim);
        animator.Play("Idle");
        bubble.sprite = Shapes.NumberBubble(slot + 1);
        bubble.gameObject.SetActive(true);
        Enter(State.Announce, windup);
    }
}
