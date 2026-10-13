using System;
using System.Collections.Generic;
using UnityEngine;

public enum Team { Player, Enemy }

/// Health, stats, moves, statuses and temporary modifiers of one Pokémon in the arena.
public class Combatant : MonoBehaviour
{
    public const int MoveSlots = 4;
    const float HitFlashTime = 0.1f;
    const float DefaultLungeTime = 0.2f;
    const float PlayerInvulnerability = 0.8f; // seconds without damage after a hit, so simultaneous attacks do not stack
    const float PoisonTick = 1f; // seconds between two poison damages
    const float PoisonShare = 0.04f; // share of max HP lost per tick
    const float ParalysisSlow = 0.5f; // speed multiplier while paralysed
    const float DefaultStatusDuration = 5f;
    const float DefenseScale = 30f; // damage taken = raw * scale / (scale + defense)
    static readonly Vector3 StatusIconOffset = new(0f, 1.1f, 0f);
    static readonly Color PoisonTint = new(0.85f, 0.7f, 1f), ParalysisTint = new(1f, 1f, 0.6f), HealTint = new(0.6f, 1f, 0.6f);

    public Team team;
    public float maxHp;
    public float hp;
    public float moveSpeed = 4f;
    [Tooltip("Multiplier on the damage this Pokémon deals (enemies hit softer than players)")]
    public float damageFactor = 1f;

    public int level = 1;
    // Current stats: base values plus what each level brought.
    public float atk, spAtk, def, spDef;
    // Multipliers changed by upgrades.
    public float attackMul = 1f, specialAttackMul = 1f, cooldownMul = 1f;
    public float projectileSizeMul = 1f, projectileSpeedMul = 1f, meleeAreaMul = 1f;

    public PokemonStats Stats { get; private set; }
    public MoveDef[] Moves { get; private set; } = new MoveDef[MoveSlots];
    /// melee, ranged or support: drives the AI.
    public string Role { get; private set; } = "melee";
    public event Action<Combatant> Died;
    /// Raised for every Pokémon that goes down, whatever its team.
    public static event Action<Combatant> AnyDied;
    public bool IsAlive => hp > 0f;
    public bool IsPoisoned => IsAlive && Time.time < poisonUntil;
    public bool IsParalysed => IsAlive && Time.time < paralysisUntil;
    /// True while a melee dash is carrying this Pokémon forward: controllers must not steer it.
    public bool IsLunging => Time.time < lungeUntil;
    /// Movement speed right now, statuses and temporary modifiers included.
    public float CurrentSpeed => moveSpeed * Modifier("speed") * (IsParalysed ? ParalysisSlow : 1f);

    class TimedModifier
    {
        public string stat; // attack, defense or speed
        public float value; // multiplier: above 1 is a bonus, below 1 a penalty
        public float until;
    }

    static readonly List<Combatant> all = new();

    readonly float[] readyAt = new float[MoveSlots];
    readonly HashSet<Combatant> lungeHit = new();
    readonly List<TimedModifier> modifiers = new();
    readonly Dictionary<string, GameObject> statusIcons = new();
    MoveDef lungeMove;
    Vector2 lungeDirection;
    float lungeUntil;
    float poisonUntil, nextPoisonTick, paralysisUntil;
    Rigidbody2D body;
    SpriteRenderer spriteRenderer;
    float flashUntil, healFlashUntil;
    float invulnerableUntil;

    // Statics can outlive a play session in the editor.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        all.Clear();
        AnyDied = null;
    }

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

    /// Nearest standing Pokémon of a team within maxDistance, or null. "except" is never returned.
    public static Combatant Nearest(Team team, Vector2 from, float maxDistance, Combatant except = null)
    {
        Combatant best = null;
        float bestDistance = maxDistance;
        foreach (var c in all)
        {
            if (c.team != team || !c.IsAlive || c == except) continue;
            float distance = Vector2.Distance(from, c.transform.position);
            if (distance <= bestDistance)
            {
                best = c;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// Standing Pokémon of a team with the lowest share of health, among those under maxRatio and within range.
    public static Combatant MostWounded(Team team, Vector2 from, float range, float maxRatio)
    {
        Combatant best = null;
        float bestRatio = maxRatio;
        foreach (var c in all)
        {
            if (c.team != team || !c.IsAlive || c.maxHp <= 0f) continue;
            if (Vector2.Distance(from, c.transform.position) > range) continue;
            float ratio = c.hp / c.maxHp;
            if (ratio < bestRatio)
            {
                best = c;
                bestRatio = ratio;
            }
        }
        return best;
    }

    public void Init(int pokemonId, Team newTeam)
    {
        team = newTeam;
        Stats = GameData.Pokemon(pokemonId);
        Moves = GameData.StartingMoves(pokemonId, MoveSlots);
        Role = GameData.Role(pokemonId);
        if (Stats == null) return;
        maxHp = hp = Stats.baseHp;
        level = 1;
        atk = Stats.baseAtk;
        spAtk = Stats.baseSpAtk;
        def = Stats.baseDef;
        spDef = Stats.baseSpDef;
        attackMul = specialAttackMul = cooldownMul = projectileSizeMul = projectileSpeedMul = meleeAreaMul = 1f;
        moveSpeed = GameData.MoveSpeed(Stats.baseSpeed);
        Array.Clear(readyAt, 0, readyAt.Length);
        modifiers.Clear();
    }

    public float CooldownLeft(int slot) => Mathf.Max(0f, readyAt[slot] - Time.time);

    /// Recharge time of a move for this Pokémon, upgrades included.
    public float CooldownOf(MoveDef move) => move.cooldown * cooldownMul;

    /// Stats gained on reaching a new level. A knocked-out Pokémon gets the stats but stays down.
    public void ApplyGrowth(LevelGrowth growth, int newLevel)
    {
        level = newLevel;
        maxHp += growth.hp;
        if (IsAlive) hp += growth.hp;
        atk += growth.atk;
        spAtk += growth.spAtk;
        def += growth.def;
        spDef += growth.spDef;
    }

    public void ApplyUpgrade(UpgradeDef upgrade)
    {
        switch (upgrade.stat)
        {
            case "maxHp":
                float gain = maxHp * upgrade.value;
                maxHp += gain;
                if (IsAlive) hp += gain;
                break;
            case "attack": attackMul += upgrade.value; break;
            case "specialAttack": specialAttackMul += upgrade.value; break;
            case "moveSpeed": moveSpeed *= 1f + upgrade.value; break;
            case "cooldown": cooldownMul *= 1f - upgrade.value; break;
            case "projectileSize": projectileSizeMul += upgrade.value; break;
            case "projectileSpeed": projectileSpeedMul += upgrade.value; break;
            case "meleeArea": meleeAreaMul += upgrade.value; break;
            default: Debug.LogWarning($"Unknown upgrade stat '{upgrade.stat}'"); break;
        }
    }

    // ---- Temporary modifiers and statuses ----

    /// Product of the active modifiers on a stat (1 when there is none).
    public float Modifier(string stat)
    {
        float result = 1f;
        foreach (var m in modifiers)
            if (m.stat == stat && Time.time < m.until) result *= m.value;
        return result;
    }

    /// True when a bonus (or a penalty) is currently active on that stat.
    public bool HasModifier(string stat, bool bonus)
    {
        foreach (var m in modifiers)
            if (m.stat == stat && Time.time < m.until && m.value > 1f == bonus) return true;
        return false;
    }

    /// Adds a bonus or penalty. The same kind on the same stat is refreshed rather than stacked.
    public void AddModifier(string stat, float value, float duration)
    {
        if (!IsAlive || string.IsNullOrEmpty(stat) || value <= 0f) return;
        modifiers.RemoveAll(m => Time.time >= m.until || (m.stat == stat && m.value > 1f == value > 1f));
        modifiers.Add(new TimedModifier { stat = stat, value = value, until = Time.time + duration });
    }

    public bool HasStatus(string status) => status == "poison" ? IsPoisoned : status == "paralysis" && IsParalysed;

    /// Applies what a move carries besides damage: a status (with its chance) and a stat modifier.
    /// sure = ignore the status chance (lingering zones).
    public void ApplyEffects(MoveDef move, bool sure = false)
    {
        if (!IsAlive) return;
        if (!string.IsNullOrEmpty(move.modStat) && move.family != "modifier")
            AddModifier(move.modStat, move.modValue, move.modDuration > 0f ? move.modDuration : DefaultStatusDuration);

        if (string.IsNullOrEmpty(move.status)) return;
        float chance = move.statusChance > 0f ? move.statusChance : 1f;
        if (!sure && UnityEngine.Random.value > chance) return;
        float duration = move.statusDuration > 0f ? move.statusDuration : DefaultStatusDuration;
        if (move.status == "poison")
        {
            if (!IsPoisoned) nextPoisonTick = Time.time + PoisonTick;
            poisonUntil = Mathf.Max(poisonUntil, Time.time + duration);
        }
        else if (move.status == "paralysis")
        {
            paralysisUntil = Mathf.Max(paralysisUntil, Time.time + duration);
        }
    }

    // ---- Moves ----

    /// Uses a move towards a direction. distance (tiles) is where a zone move lands; by default its full range.
    public bool TryUseMove(int slot, Vector2 direction, float distance = -1f)
    {
        var move = Moves[slot];
        if (move == null || Time.time < readyAt[slot]) return false;

        if (move.family == "heal")
        {
            var patient = MostWounded(team, transform.position, move.range, 1f);
            if (patient == null) return false; // nobody to heal: the move is not spent
            readyAt[slot] = Time.time + CooldownOf(move);
            patient.Heal(patient.maxHp * move.healShare);
            if (EffectLibrary.Get(move.effect) != null) EffectPlayer.Spawn(move.effect, patient.transform.position, Vector2.right);
            else Shapes.Pulse(patient.transform.position, 0.6f, HealTint);
            return true;
        }

        if (move.family == "modifier")
        {
            readyAt[slot] = Time.time + CooldownOf(move);
            float duration = move.modDuration > 0f ? move.modDuration : DefaultStatusDuration;
            if (move.target == "self") AddModifier(move.modStat, move.modValue, duration);
            else
            {
                Team side = move.target == "enemies" ? (team == Team.Player ? Team.Enemy : Team.Player) : team;
                foreach (var c in all)
                    if (c.team == side && c.IsAlive && Vector2.Distance(c.transform.position, transform.position) <= move.radius)
                        c.AddModifier(move.modStat, move.modValue, duration);
            }
            if (EffectLibrary.Get(move.effect) != null) EffectPlayer.Spawn(move.effect, transform.position, Vector2.right);
            else Shapes.Pulse(transform.position, move.target == "self" ? 0.6f : move.radius, GameData.TypeColor(move.type));
            return true;
        }

        if (direction == Vector2.zero) return false;
        readyAt[slot] = Time.time + CooldownOf(move);
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
            LoseHp(Mathf.Max(1f, maxHp * PoisonShare)); // poison ignores defence and the post-hit protection
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
        foreach (var other in Physics2D.OverlapCircleAll(centre, move.radius * meleeAreaMul))
        {
            var target = other.GetComponentInParent<Combatant>();
            if (target == null || target.team == team || !hit.Add(target)) continue;
            EffectPlayer.Spawn(move.impactEffect, target.transform.position, direction);
            if (target.TakeHit(DamageOf(move), move.category, out _)) target.ApplyEffects(move);
        }
    }

    /// Raw damage of a move before the target's defence: power plus the matching attack stat (provisional formula).
    public float DamageOf(MoveDef move)
    {
        if (move.power <= 0) return 0f; // pure status moves
        float stat = move.category == "physical" ? atk * attackMul : spAtk * specialAttackMul;
        return (move.power + stat) * Modifier("attack") * damageFactor;
    }

    /// A hit from a move: the target's defence (physical or special) reduces it. Returns false when the hit was
    /// ignored (already down, or protected just after a previous hit); "dealt" is the health actually lost.
    public bool TakeHit(float raw, string category, out float dealt)
    {
        dealt = 0f;
        if (!IsAlive) return false;
        if (team == Team.Player && Time.time < invulnerableUntil) return false;
        if (raw <= 0f) return true; // a status-only hit lands without damage
        if (team == Team.Player) invulnerableUntil = Time.time + PlayerInvulnerability;
        float defence = category == "physical" ? def : spDef;
        dealt = Mathf.Max(1f, raw * DefenseScale / (DefenseScale + defence) / Modifier("defense"));
        LoseHp(dealt);
        return true;
    }

    /// Direct damage that ignores defence (used by tests and scripted effects).
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

    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        hp = Mathf.Min(maxHp, hp + amount);
        healFlashUntil = Time.time + 2f * HitFlashTime;
    }

    void LoseHp(float amount)
    {
        hp = Mathf.Max(0f, hp - amount);
        flashUntil = Time.time + HitFlashTime;
        if (IsAlive) return;
        ClearEffects();
        Died?.Invoke(this);
        AnyDied?.Invoke(this);
        if (team == Team.Enemy) Destroy(gameObject);
    }

    void ClearEffects()
    {
        poisonUntil = paralysisUntil = 0f;
        modifiers.Clear();
    }

    /// A knocked-out team member stays on the ground but no longer blocks or gets hit.
    public void KnockOut()
    {
        lungeUntil = 0f;
        ClearEffects();
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
        bool poisoned = IsPoisoned, paralysed = IsParalysed;
        if (spriteRenderer != null)
            spriteRenderer.color = Time.time < flashUntil ? new Color(1f, 0.4f, 0.4f)
                : Time.time < healFlashUntil ? HealTint
                : Time.time < invulnerableUntil ? new Color(1f, 1f, 1f, 0.5f) // half transparent while protected
                : poisoned ? PoisonTint
                : paralysed ? ParalysisTint
                : Color.white;

        // Status icons side by side above the health bar.
        int shown = (poisoned ? 1 : 0) + (paralysed ? 1 : 0), index = 0;
        ShowIcon("poison", poisoned, ref index, shown);
        ShowIcon("paralysis", paralysed, ref index, shown);
    }

    void ShowIcon(string status, bool visible, ref int index, int shown)
    {
        statusIcons.TryGetValue(status, out var icon);
        if (visible && icon == null)
        {
            icon = new GameObject("StatusIcon");
            icon.transform.SetParent(transform, false);
            var renderer = icon.AddComponent<SpriteRenderer>();
            renderer.sprite = Shapes.StatusIcon(status);
            renderer.sortingOrder = 12; // above the health bar
            statusIcons[status] = icon;
        }
        if (icon == null) return;
        icon.SetActive(visible);
        if (!visible) return;
        icon.transform.localPosition = StatusIconOffset + new Vector3((index - (shown - 1) / 2f) * 0.45f, 0f, 0f);
        index++;
    }
}
