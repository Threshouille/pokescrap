using System;
using System.Collections.Generic;
using UnityEngine;

public enum Team { Player, Enemy }

/// Health, stats, moves and status of one Pokémon in the arena.
public class Combatant : MonoBehaviour
{
    public const int MoveSlots = 4;
    const float HitFlashTime = 0.1f;
    const float DefaultLungeTime = 0.2f;
    const float PlayerInvulnerability = 0.8f; // seconds without damage after a hit, so simultaneous attacks do not stack
    const float PoisonTick = 1f; // seconds between two poison damages
    const float PoisonShare = 0.04f; // share of max HP lost per tick
    const float DefaultStatusDuration = 5f;
    static readonly Vector3 StatusIconOffset = new(0f, 1.1f, 0f);
    static readonly Color PoisonTint = new(0.85f, 0.7f, 1f);

    public Team team;
    public float maxHp;
    public float hp;
    public float moveSpeed = 4f;
    [Tooltip("Multiplier on the damage this Pokémon deals (enemies hit softer than players)")]
    public float damageFactor = 1f;

    public PokemonStats Stats { get; private set; }
    public MoveDef[] Moves { get; private set; } = new MoveDef[MoveSlots];
    public event Action<Combatant> Died;
    public bool IsAlive => hp > 0f;
    public bool IsPoisoned => IsAlive && Time.time < poisonUntil;
    /// True while a melee dash is carrying this Pokémon forward: controllers must not steer it.
    public bool IsLunging => Time.time < lungeUntil;

    static readonly List<Combatant> all = new();

    readonly float[] readyAt = new float[MoveSlots];
    readonly HashSet<Combatant> lungeHit = new();
    MoveDef lungeMove;
    Vector2 lungeDirection;
    float lungeUntil;
    float poisonUntil, nextPoisonTick;
    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    GameObject statusIcon;
    float flashUntil;
    float invulnerableUntil;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
    }

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public static int CountAlive(Team team)
    {
        int count = 0;
        foreach (var c in all)
            if (c.team == team && c.IsAlive) count++;
        return count;
    }

    /// Nearest standing Pokémon of a team within maxDistance, or null.
    public static Combatant Nearest(Team team, Vector2 from, float maxDistance)
    {
        Combatant best = null;
        float bestDistance = maxDistance;
        foreach (var c in all)
        {
            if (c.team != team || !c.IsAlive) continue;
            float distance = Vector2.Distance(from, c.transform.position);
            if (distance <= bestDistance)
            {
                best = c;
                bestDistance = distance;
            }
        }
        return best;
    }

    public void Init(int pokemonId, Team newTeam)
    {
        team = newTeam;
        Stats = GameData.Pokemon(pokemonId);
        Moves = GameData.StartingMoves(pokemonId, MoveSlots);
        if (Stats == null) return;
        maxHp = hp = Stats.baseHp;
        moveSpeed = GameData.MoveSpeed(Stats.baseSpeed);
        Array.Clear(readyAt, 0, readyAt.Length);
    }

    public float CooldownLeft(int slot) => Mathf.Max(0f, readyAt[slot] - Time.time);

    /// Uses a move towards a direction. distance (tiles) is where a zone move lands; by default its full range.
    public bool TryUseMove(int slot, Vector2 direction, float distance = -1f)
    {
        var move = Moves[slot];
        if (move == null || Time.time < readyAt[slot] || direction == Vector2.zero) return false;
        readyAt[slot] = Time.time + move.cooldown;
        direction = direction.normalized;
        if (move.family == "projectile") Projectile.Spawn(this, move, direction);
        else if (move.family == "zone")
        {
            float reach = distance > 0f ? Mathf.Min(distance, move.range) : move.range;
            Zone.Spawn(this, move, (Vector2)transform.position + direction * reach);
        }
        else if (move.family == "melee")
        {
            if (move.lunge > 0f && body != null) StartLunge(move, direction);
            else
            {
                EffectPlayer.Spawn(move.effect, transform.position + (Vector3)(direction * move.range), direction);
                MeleeHit(move, direction, new HashSet<Combatant>());
            }
        }
        return true;
    }

    static float LungeTime(MoveDef move) => move.lungeTime > 0f ? move.lungeTime : DefaultLungeTime;

    void StartLunge(MoveDef move, Vector2 direction)
    {
        lungeMove = move;
        lungeDirection = direction;
        lungeHit.Clear();
        lungeUntil = Time.time + LungeTime(move);
        // Trail just behind the attacker, following it for the length of the dash.
        EffectPlayer.Spawn(move.effect, transform.position - (Vector3)(direction * 0.5f), direction, transform, LungeTime(move));
    }

    void FixedUpdate()
    {
        if (IsPoisoned && Time.time >= nextPoisonTick)
        {
            nextPoisonTick += PoisonTick;
            LoseHp(Mathf.Max(1f, maxHp * PoisonShare)); // poison ignores the post-hit protection
            if (!IsAlive) return;
        }

        // During a dash the attacker is pushed forward and hits each opponent it reaches once.
        if (!IsLunging) return;
        body.linearVelocity = lungeDirection * lungeMove.lunge / LungeTime(lungeMove);
        MeleeHit(lungeMove, lungeDirection, lungeHit);
    }

    /// Hits every opponent inside a circle placed in front of the attacker, skipping those already hit.
    void MeleeHit(MoveDef move, Vector2 direction, HashSet<Combatant> hit)
    {
        Vector2 centre = (Vector2)transform.position + direction * move.range;
        foreach (var other in Physics2D.OverlapCircleAll(centre, move.radius))
        {
            var target = other.GetComponentInParent<Combatant>();
            if (target == null || target.team == team || !hit.Add(target)) continue;
            EffectPlayer.Spawn(move.impactEffect, target.transform.position, direction);
            if (target.TakeDamage(DamageOf(move))) target.ApplyStatus(move);
        }
    }

    /// Provisional formula: move power plus the matching attack stat. Types and defence come later.
    public float DamageOf(MoveDef move)
    {
        int stat = Stats == null ? 0 : move.category == "physical" ? Stats.baseAtk : Stats.baseSpAtk;
        return (move.power + stat) * damageFactor;
    }

    /// Returns false when the hit was ignored (already down, or protected just after a previous hit).
    public bool TakeDamage(float amount)
    {
        if (!IsAlive) return false;
        if (team == Team.Player)
        {
            if (Time.time < invulnerableUntil) return false;
            invulnerableUntil = Time.time + PlayerInvulnerability;
        }
        LoseHp(amount);
        return true;
    }

    void LoseHp(float amount)
    {
        hp = Mathf.Max(0f, hp - amount);
        flashUntil = Time.time + HitFlashTime;
        if (IsAlive) return;
        poisonUntil = 0f;
        Died?.Invoke(this);
        if (team == Team.Enemy) Destroy(gameObject);
    }

    /// Applies the status carried by a move, if any. sure = ignore the move's chance (lingering zones).
    public void ApplyStatus(MoveDef move, bool sure = false)
    {
        if (move.status != "poison" || !IsAlive) return;
        float chance = move.statusChance > 0f ? move.statusChance : 1f;
        if (!sure && UnityEngine.Random.value > chance) return;
        if (!IsPoisoned) nextPoisonTick = Time.time + PoisonTick;
        float duration = move.statusDuration > 0f ? move.statusDuration : DefaultStatusDuration;
        poisonUntil = Mathf.Max(poisonUntil, Time.time + duration);
    }

    /// A knocked-out team member stays on the ground but no longer blocks or gets hit.
    public void KnockOut()
    {
        lungeUntil = 0f;
        poisonUntil = 0f;
        foreach (var collider in GetComponents<Collider2D>()) collider.enabled = false;
        if (body != null) body.simulated = false;
    }

    /// Brings a knocked-out team member back with a share of its max HP, briefly protected.
    public void Revive(float healthShare)
    {
        hp = Mathf.Max(1f, maxHp * healthShare);
        invulnerableUntil = Time.time + 2f * PlayerInvulnerability;
        foreach (var collider in GetComponents<Collider2D>()) collider.enabled = true;
        if (body != null) body.simulated = true;
    }

    void LateUpdate()
    {
        bool poisoned = IsPoisoned;
        if (spriteRenderer != null)
            spriteRenderer.color = Time.time < flashUntil ? new Color(1f, 0.4f, 0.4f)
                : Time.time < invulnerableUntil ? new Color(1f, 1f, 1f, 0.5f) // half transparent while protected
                : poisoned ? PoisonTint
                : Color.white;

        if (poisoned && statusIcon == null)
        {
            statusIcon = new GameObject("StatusIcon");
            statusIcon.transform.SetParent(transform, false);
            statusIcon.transform.localPosition = StatusIconOffset;
            var icon = statusIcon.AddComponent<SpriteRenderer>();
            icon.sprite = Shapes.StatusIcon("poison");
            icon.sortingOrder = 12; // above the health bar
        }
        if (statusIcon != null) statusIcon.SetActive(poisoned);
    }
}
