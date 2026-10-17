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
    const float DefaultStatusDuration = 5f;
    const float ConfusionChange = 0.5f; // seconds between two changes of the confusion deviation
    const int IconsPerRow = 4;
    const float IconSpacing = 0.45f; // tiles between two icons
    const float BlinkTime = 1f, BlinkRate = 8f; // an icon blinks during its last second
    const float DefenseScale = 30f; // damage taken = raw * scale / (scale + defense)
    static readonly Vector3 StatusIconOffset = new(0f, 1.1f, 0f);
    static readonly Color HealTint = new(0.6f, 1f, 0.6f);

    public Team team;
    public float maxHp;
    public float hp;
    public float moveSpeed = 4f;
    [Tooltip("Multiplier on the damage this Pokémon deals (enemies hit softer than players)")]
    public float damageFactor = 1f;
    [Tooltip("Bosses resist statuses: less damage over time, shorter disabling statuses (see statuses.json)")]
    public bool isBoss;

    public int level = 1;
    // Current stats: base values plus what each level brought.
    public float atk, spAtk, def, spDef;
    // Multipliers changed by upgrades.
    public float attackMul = 1f, specialAttackMul = 1f, cooldownMul = 1f;
    public float projectileSizeMul = 1f, projectileSpeedMul = 1f, meleeAreaMul = 1f;

    public PokemonStats Stats { get; private set; }
    /// The form this Pokémon started the run as (it may have evolved since).
    public int OriginId { get; private set; }
    public MoveDef[] Moves { get; private set; } = new MoveDef[MoveSlots];
    /// melee, ranged or support: drives the AI.
    public string Role { get; private set; } = "melee";
    public event Action<Combatant> Died;
    /// Raised for every Pokémon that goes down, whatever its team.
    public static event Action<Combatant> AnyDied;
    public bool IsAlive => hp > 0f;
    public bool IsPoisoned => HasStatus("poison");
    public bool IsParalysed => HasStatus("paralysis");
    /// False while a status (sleep, freeze, trap) holds this Pokémon in place.
    public bool CanMove => StatusAllows(def => def.canMove) && Time.time >= stunUntil;
    /// False while a status (sleep, freeze) prevents attacking.
    public bool CanAttack => StatusAllows(def => def.canAttack) && Time.time >= stunUntil;
    /// True while a melee dash is carrying this Pokémon forward: controllers must not steer it.
    public bool IsLunging => Time.time < lungeUntil;
    public bool IsDodging => Time.time < dodgeUntil;
    /// True while a dash (melee lunge or dodge) drives the body: controllers leave its velocity alone.
    public bool IsDashing => IsLunging || IsDodging || Time.time < knockUntil;
    public float DodgeCooldownLeft => Mathf.Max(0f, dodgeReadyAt - Time.time);
    /// Where the player controlling this Pokémon aims right now (zero for an AI). A shot that leaves after a cast
    /// or a delay follows it, rather than the direction it was aimed at when the move started.
    public Vector2 LiveAim { get; set; }
    /// True while a move with a cast time is being prepared: the Pokémon stands still and cannot use another move.
    public bool IsCasting => castMove != null || blinkMove != null;
    /// Movement speed right now, statuses and temporary modifiers included.
    public float CurrentSpeed => CanMove && !IsCasting && Time.time >= recoverUntil ? moveSpeed * Modifier("speed") * StatusProduct(def => def.speedMul) : 0f;

    class TimedModifier
    {
        public string stat; // attack, defense or speed
        public float value; // multiplier: above 1 is a bonus, below 1 a penalty
        public float until;
    }

    class ActiveStatus
    {
        public StatusDef def;
        public float until, nextTick;
        public Combatant source; // who gave it (draining statuses heal it)
    }

    static readonly List<Combatant> all = new();

    readonly float[] readyAt = new float[MoveSlots];
    readonly HashSet<Combatant> lungeHit = new();
    readonly List<TimedModifier> modifiers = new();
    readonly List<ActiveStatus> statuses = new();
    readonly Dictionary<string, GameObject> statusIcons = new();
    readonly List<EffectIcon> shownEffects = new();
    MoveDef lungeMove;
    Vector2 lungeDirection;
    float lungeUntil;
    Vector2 dodgeDirection;
    float dodgeUntil, dodgeReadyAt;
    readonly List<Collider2D> dodgeIgnored = new(); // opponents passed through during the current dodge
    MoveDef castMove; // move being cast, fired at castUntil
    struct PendingStrike { public MoveDef move; public Vector2 direction; public float at; }
    readonly List<PendingStrike> strikes = new(); // melee strikes still to come (combos)
    int chainCount; // strikes landed in a row (chainBonus)
    float chainUntil;
    float lungeSpeed;
    bool lungeBouncing, lungeFinishPending;
    float stunUntil, recoverUntil; // stunned by a melee hit; rooted after one's own move (recovery)
    float knockUntil; // pushed away by a hit: controllers leave the body alone
    Vector2 knockVelocity;
    float hopOffset; // height of the jump during a cast (hop)
    MoveDef blinkMove; // teleport in progress: gone until blinkUntil
    Vector2 blinkDirection;
    float blinkUntil;
    Vector2 castDirection;
    float castDistance, castUntil;
    float confusionOffset, nextConfusionChange;
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

    /// Push away from the standing team-mates closer than "space": zero when nobody is near, stronger when closer.
    public Vector2 Repulsion(float space)
    {
        Vector2 push = Vector2.zero, me = transform.position;
        foreach (var c in all)
        {
            if (c == this || c.team != team || !c.IsAlive) continue;
            Vector2 away = me - (Vector2)c.transform.position;
            float distance = away.magnitude;
            if (distance >= space) continue;
            // Exactly on top of each other: any direction will do.
            push += (distance > 0.001f ? away / distance : UnityEngine.Random.insideUnitCircle.normalized) * (1f - distance / space);
        }
        return push;
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

    /// moveIds: the moves to start with (slot order); the hand-written loadout when null.
    public void Init(int pokemonId, Team newTeam, int[] moveIds = null)
    {
        team = newTeam;
        Stats = GameData.Pokemon(pokemonId);
        OriginId = pokemonId;
        Moves = moveIds == null ? GameData.StartingMoves(pokemonId, MoveSlots) : new MoveDef[MoveSlots];
        for (int i = 0; moveIds != null && i < MoveSlots && i < moveIds.Length; i++) Moves[i] = GameData.Move(moveIds[i]);
        // Bosses keep the role written with their fixed moves; the others get it from the moves they have.
        Role = moveIds == null ? GameData.Role(pokemonId) : GameData.RoleFromMoves(Moves);
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
        castMove = null;
        strikes.Clear();
        blinkMove = null;
        stunUntil = recoverUntil = knockUntil = 0f;
        hopOffset = 0f;
    }

    public float CooldownLeft(int slot) => Mathf.Max(0f, readyAt[slot] - Time.time);

    /// Recharge time of a move for this Pokémon, upgrades included.
    public float CooldownOf(MoveDef move) => move.cooldown * cooldownMul * EnemyBasic(move, SaveData.Settings.enemyBasicCooldownMul);

    // Enemies fire their basic attack less often but harder: the factor for enemies, 1 otherwise.
    float EnemyBasic(MoveDef move, float factor) => move.basic && team == Team.Enemy ? factor : 1f;

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

    /// Turns into another form: its base stats replace the old ones, level gains are kept.
    public void Evolve(int pokemonId)
    {
        var next = GameData.Pokemon(pokemonId);
        if (next == null || Stats == null) return;
        float hpShare = maxHp > 0f ? hp / maxHp : 1f;
        maxHp += next.baseHp - Stats.baseHp;
        if (IsAlive) hp = Mathf.Max(1f, maxHp * hpShare);
        atk += next.baseAtk - Stats.baseAtk;
        spAtk += next.baseSpAtk - Stats.baseSpAtk;
        def += next.baseDef - Stats.baseDef;
        spDef += next.baseSpDef - Stats.baseSpDef;
        moveSpeed *= GameData.MoveSpeed(next.baseSpeed) / GameData.MoveSpeed(Stats.baseSpeed);
        Stats = next;
    }

    /// Moves this Pokémon could learn now: those of its first form, plus those of the forms it evolved into,
    /// with a weight (moves of an evolved current form are more likely). Known moves are left out.
    public List<(MoveDef move, float weight)> LearnableMoves(float evolvedWeight)
    {
        var result = new List<(MoveDef, float)>();
        var forms = new List<int>();
        for (var form = Stats; form != null; form = form.id == OriginId || form.evolvesFrom <= 0 ? null : GameData.Pokemon(form.evolvesFrom))
            forms.Add(form.id);
        if (!forms.Contains(OriginId)) forms.Add(OriginId);
        var current = Stats.id != OriginId ? GameData.MovePool(Stats.id).ConvertAll(m => m.id) : new List<int>();
        var seen = new HashSet<int>();
        foreach (int form in forms)
            foreach (var move in GameData.MovePool(form))
            {
                if (!seen.Add(move.id) || Array.Exists(Moves, m => m != null && m.id == move.id)) continue;
                result.Add((move, current.Contains(move.id) ? evolvedWeight : 1f));
            }
        return result;
    }

    public int KnownMoveCount => Array.FindAll(Moves, m => m != null).Length;

    /// Puts a move in a slot (replacing what was there), ready to use.
    public void LearnMove(int slot, MoveDef move)
    {
        if (slot < 0 || slot >= MoveSlots) return;
        Moves[slot] = move;
        readyAt[slot] = 0f;
        Role = GameData.RoleFromMoves(Moves);
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

    public bool HasStatus(string status)
    {
        if (!IsAlive) return false;
        foreach (var st in statuses)
            if (st.def.key == status && Time.time < st.until) return true;
        return false;
    }

    bool StatusAllows(Predicate<StatusDef> allows)
    {
        if (!IsAlive) return true;
        foreach (var st in statuses)
            if (Time.time < st.until && !allows(st.def)) return false;
        return true;
    }

    float StatusProduct(Func<StatusDef, float> factor)
    {
        float result = 1f;
        foreach (var st in statuses)
            if (Time.time < st.until) result *= factor(st.def);
        return result;
    }

    float ConfuseAngle()
    {
        float angle = 0f;
        foreach (var st in statuses)
            if (Time.time < st.until) angle = Mathf.Max(angle, st.def.confuseAngle);
        return angle;
    }

    /// Movement or aim thrown off while confused.
    public Vector2 Steer(Vector2 direction)
    {
        if (direction == Vector2.zero || ConfuseAngle() <= 0f) return direction;
        return Quaternion.Euler(0f, 0f, confusionOffset) * direction;
    }

    /// Gives a status for a number of seconds (refreshed, not stacked). Unknown statuses are ignored.
    public void AddStatus(string key, float duration, Combatant source = null)
    {
        var def = StatusLibrary.Get(key);
        if (!IsAlive || def == null) return;
        duration *= team == Team.Player ? def.playerDurationFactor : 1f;
        if (isBoss) duration *= def.bossDurationFactor;
        foreach (var st in statuses)
            if (st.def == def)
            {
                if (Time.time >= st.until) st.nextTick = Time.time + st.def.tick;
                st.until = Mathf.Max(st.until, Time.time + duration);
                if (source != null) st.source = source;
                return;
            }
        statuses.Add(new ActiveStatus { def = def, until = Time.time + duration, nextTick = Time.time + def.tick, source = source });
    }

    /// Applies what a move carries besides damage: a status (with its chance) and a stat modifier.
    /// sure = ignore the status chance (lingering zones). source = the user of the move.
    public void ApplyEffects(MoveDef move, bool sure = false, Combatant source = null)
    {
        if (!IsAlive) return;
        statuses.RemoveAll(st => st.def.thawType == move.type); // a fire move thaws a frozen Pokémon
        if (!string.IsNullOrEmpty(move.modStat) && move.family != "modifier"
            && (sure || move.statChance <= 0f || UnityEngine.Random.value <= move.statChance))
            AddModifier(move.modStat, move.modValue, move.modDuration > 0f ? move.modDuration : DefaultStatusDuration);

        if (string.IsNullOrEmpty(move.status)) return;
        float chance = move.statusChance > 0f ? move.statusChance : 1f;
        if (!sure && UnityEngine.Random.value > chance) return;
        AddStatus(move.status, move.statusDuration > 0f ? move.statusDuration : DefaultStatusDuration, source);
    }

    /// Called on the attacker when one of its moves lands: drain, recoil and bonuses it gives itself.
    public void HitLanded(MoveDef move, float dealt)
    {
        if (dealt > 0f && move.drain > 0f)
            MostWounded(team, transform.position, float.MaxValue, 1f)?.Heal(dealt * move.drain);
        if (!string.IsNullOrEmpty(move.selfModStat) && (move.selfStatChance <= 0f || UnityEngine.Random.value <= move.selfStatChance))
            AddModifier(move.selfModStat, move.selfModValue, move.selfModDuration > 0f ? move.selfModDuration : DefaultStatusDuration);
        if (dealt > 0f && move.recoil > 0f && IsAlive) LoseHp(Mathf.Max(1f, dealt * move.recoil));
    }

    // ---- Moves ----

    /// Uses a move towards a direction. distance (tiles) is where a zone move lands; by default its full range.
    public bool TryUseMove(int slot, Vector2 direction, float distance = -1f)
    {
        var move = Moves[slot];
        if (move == null || Time.time < readyAt[slot] || !CanAttack || IsCasting || Time.time < recoverUntil) return false;

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

        if (move.family == "burst")
        {
            // Damage all around the user.
            readyAt[slot] = Time.time + CooldownOf(move);
            foreach (var c in all.ToArray()) // a hit can remove a Pokémon from the list
                if (c.team != team && c.IsAlive && Vector2.Distance(c.transform.position, transform.position) <= move.radius
                    && c.TakeHit(DamageOf(move), move.category, out float dealt))
                {
                    c.ApplyEffects(move, false, this);
                    HitLanded(move, dealt);
                }
            if (EffectLibrary.Get(move.effect) != null) EffectPlayer.Spawn(move.effect, transform.position, Vector2.right);
            else Shapes.Pulse(transform.position, move.radius, GameData.TypeColor(move.type));
            return true;
        }

        if (move.family == "zone" && move.target == "each")
        {
            // One zone under every opponent still standing.
            readyAt[slot] = Time.time + CooldownOf(move);
            foreach (var c in all.ToArray())
                if (c.team != team && c.IsAlive) Zone.Spawn(this, move, c.transform.position);
            return true;
        }

        if (direction == Vector2.zero) return false;
        readyAt[slot] = Time.time + CooldownOf(move);
        direction = Steer(direction.normalized);
        if ((move.family == "projectile" || move.family == "melee") && move.castTime > 0f)
        {
            // The move goes off once the cast is over, toward where the player aims then.
            castMove = move;
            castDirection = direction;
            castDistance = distance;
            castUntil = Time.time + move.castTime;
        }
        else if (move.family == "projectile") FireProjectiles(move, direction, distance);
        else if (move.family == "zone")
        {
            float reach = distance > 0f ? Mathf.Min(distance, move.range) : move.range;
            Zone.Spawn(this, move, (Vector2)transform.position + direction * reach);
        }
        else if (move.family == "melee") Melee(move, direction);
        return true;
    }

    // A melee move: blink forward first if it teleports, then a dash (hits along the way) or one or more strikes.
    void Melee(MoveDef move, Vector2 direction)
    {
        if (move.teleport)
        {
            EffectPlayer.Spawn(move.trailEffect, transform.position, direction, null, 0.25f);
            if (move.teleportDelay > 0f)
            {
                // Gone for a moment (no hit can land), then back further on, striking.
                blinkMove = move;
                blinkDirection = direction;
                blinkUntil = Time.time + move.teleportDelay;
                ShowSelf(false);
                return;
            }
            MoveSafely(direction, move.lunge);
        }
        else if (move.lunge > 0f && body != null)
        {
            StartLunge(move, direction);
            return;
        }
        QueueStrikes(move, direction);
    }

    void ShowSelf(bool visible)
    {
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = visible;
    }

    /// Pushes this Pokémon away for "time" seconds (bosses do not budge).
    public void Knockback(Vector2 velocity, float time)
    {
        if (isBoss || !IsAlive || body == null) return;
        knockVelocity = velocity;
        knockUntil = Time.time + time;
    }

    /// Neither moving nor attacking for a moment (bosses are immune).
    public void Stun(float duration)
    {
        if (isBoss || duration <= 0f) return;
        stunUntil = Mathf.Max(stunUntil, Time.time + duration);
    }

    void QueueStrikes(MoveDef move, Vector2 direction)
    {
        int count = Mathf.Max(1, move.hits);
        for (int i = 0; i < count; i++)
        {
            float angle = move.hitAngles != null && move.hitAngles.Length > 0 ? move.hitAngles[i % move.hitAngles.Length] : 0f;
            strikes.Add(new PendingStrike { move = move, direction = Quaternion.Euler(0f, 0f, angle) * direction, at = Time.time + i * move.hitInterval });
        }
    }

    // Moves the user up to "distance" tiles, through opponents but stopping short of walls.
    void MoveSafely(Vector2 direction, float distance)
    {
        const float Margin = 0.35f;
        foreach (var hit in Physics2D.RaycastAll(transform.position, direction, distance + Margin))
            if (!hit.collider.isTrigger && hit.collider.GetComponentInParent<Combatant>() == null)
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - Margin));
        Vector2 to = (Vector2)transform.position + direction * distance;
        if (body != null) body.position = to;
        transform.position = to;
    }

    /// One strike of a melee move, shaped by move.shape; radius overrides the size (dash finish). Returns the opponents hit.
    int Strike(MoveDef move, Vector2 direction, float radius = -1f)
    {
        float mul = meleeAreaMul;
        string shape = radius > 0f ? "around" : move.shape;
        Vector2 at = (Vector2)transform.position + (shape == "arc" || shape == "thrust" ? direction * move.strikeOffset : Vector2.zero);
        float reach = radius > 0f ? radius : move.radiusMin > 0f ? UnityEngine.Random.Range(move.radiusMin, move.range) : move.range;
        Collider2D[] found = shape switch
        {
            "arc" => Physics2D.OverlapCircleAll(at, reach * mul),
            "thrust" => Physics2D.OverlapBoxAll(at + direction * reach * mul / 2f, new Vector2(reach * mul, move.radius * 2f * mul),
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg),
            "around" => Physics2D.OverlapCircleAll(at, reach * mul),
            _ => Physics2D.OverlapCircleAll(at + direction * move.range, move.radius * mul),
        };
        // Strikes that land in quick succession hit harder (chainBonus).
        if (move.chainBonus > 0f && Time.time > chainUntil) chainCount = 0;
        ShowStrike(move, shape, at, direction, reach * mul, radius > 0f);
        float factor = move.chainBonus > 0f ? Mathf.Min(1f + move.chainBonus * chainCount, move.chainMax > 0f ? move.chainMax : 2f) : 1f;

        var hit = new HashSet<Combatant>();
        foreach (var other in found)
        {
            var target = other.GetComponentInParent<Combatant>();
            if (target == null || target.team == team || !hit.Add(target)) continue;
            Vector2 to = (Vector2)target.transform.position - at;
            if (shape == "arc" && to.magnitude > 0.3f && Vector2.Angle(direction, to) > move.arc / 2f) { hit.Remove(target); continue; }
            EffectPlayer.Spawn(move.impactEffect, target.transform.position, direction);
            if (target.TakeHit(DamageOf(move) * factor, move.category, out float dealt))
            {
                target.Stun(SaveData.Settings.meleeStun);
                target.ApplyEffects(move, false, this);
                HitLanded(move, dealt);
            }
        }
        if (move.chainBonus > 0f && hit.Count > 0)
        {
            chainCount++;
            chainUntil = Time.time + move.chainWindow;
        }
        return hit.Count;
    }

    // The strike's sprite sheet, or a flash of the type colour over the area when there is none.
    // A dash finish uses finishEffect; a chained strike uses the brighter sheets <effect>_2, <effect>_3 when they exist.
    void ShowStrike(MoveDef move, string shape, Vector2 at, Vector2 direction, float reach, bool finish = false)
    {
        string sheet = finish ? move.finishEffect : move.effect;
        if (!finish && move.chainBonus > 0f && chainCount > 0)
        {
            string brighter = $"{move.effect}_{Mathf.Min(chainCount + 1, 3)}";
            if (EffectLibrary.Get(brighter) != null) sheet = brighter;
        }
        Vector2 centre = shape switch
        {
            "arc" or "thrust" => at + direction * reach * 0.55f,
            "around" => at,
            _ => at + direction * move.range,
        };
        if (EffectLibrary.Get(sheet) != null)
        {
            // An area around the user is stretched to its size; the other strikes keep the sheet's own size.
            float size = shape == "around" ? reach * 2f
                : move.effectScale > 0f ? EffectLibrary.Get(sheet).frame_width / (float)EffectLibrary.PixelsPerUnit * move.effectScale : 0f;
            EffectPlayer.Spawn(sheet, centre, direction, null, 0f, size);
            return;
        }
        var colour = GameData.TypeColor(move.type);
        if (shape == "thrust")
            for (float d = 0.25f; d <= reach; d += 0.35f) Shapes.Pulse(at + direction * d, move.radius * meleeAreaMul, colour);
        else Shapes.Pulse(centre, shape == "around" ? reach : shape == "arc" ? reach * 0.5f : move.radius * meleeAreaMul, colour);
    }

    /// Quick dash that goes through opponents (not walls), without protection: hits still land. False while it recharges.
    public bool TryDodge(Vector2 direction)
    {
        if (!IsAlive || Time.time < dodgeReadyAt || !CanMove || IsCasting || IsLunging || direction == Vector2.zero || body == null) return false;
        var settings = SaveData.Settings;
        dodgeDirection = direction.normalized;
        dodgeUntil = Time.time + settings.dodgeTime;
        dodgeReadyAt = Time.time + settings.dodgeCooldown;
        Team opponents = team == Team.Player ? Team.Enemy : Team.Player;
        var mine = GetComponents<Collider2D>();
        foreach (var other in all)
        {
            if (other.team != opponents) continue;
            foreach (var theirs in other.GetComponents<Collider2D>())
            {
                foreach (var own in mine) Physics2D.IgnoreCollision(own, theirs, true);
                dodgeIgnored.Add(theirs);
            }
        }
        return true;
    }

    void EndDodge()
    {
        var mine = GetComponents<Collider2D>();
        foreach (var theirs in dodgeIgnored)
            if (theirs != null) foreach (var own in mine) Physics2D.IgnoreCollision(own, theirs, false);
        dodgeIgnored.Clear();
    }

    static float LungeTime(MoveDef move) => move.lungeTime > 0f ? move.lungeTime : DefaultLungeTime;

    void StartLunge(MoveDef move, Vector2 direction)
    {
        lungeMove = move;
        lungeDirection = direction;
        lungeHit.Clear();
        lungeUntil = Time.time + LungeTime(move);
        lungeSpeed = move.lunge / LungeTime(move);
        lungeBouncing = false;
        lungeFinishPending = move.finishRadius > 0f;
        // Trail just behind the attacker, following it for the length of the dash.
        EffectPlayer.Spawn(move.effect, transform.position - (Vector3)(direction * 0.5f), direction, transform, LungeTime(move));
    }

    // Several shots are fanned out evenly around the aimed direction; a burst fires that volley again and again, in
    // the same direction, from wherever the user stands. Sequential shots of a fan leave one by one, from the
    // middle outwards, left before right.
    void FireProjectiles(MoveDef move, Vector2 direction, float distance)
    {
        int shots = Mathf.Max(1, move.count);
        if (move.path == "arc" && distance <= 0f) AutoAim(move, shots * Mathf.Max(1, move.burst), ref direction, ref distance);
        for (int volley = 0; volley < Mathf.Max(1, move.burst); volley++)
            for (int i = 0; i < shots; i++)
            {
                float angle = shots == 1 ? 0f : Mathf.Lerp(-move.spread / 2f, move.spread / 2f, i / (shots - 1f));
                float delay = volley * move.burstInterval;
                if (move.sequential)
                {
                    float centre = (shots - 1) / 2f;
                    int rank = Mathf.RoundToInt(2f * Mathf.Abs(i - centre)) - (i > centre ? 1 : 0); // i > centre: left side
                    delay += rank * move.burstInterval;
                }
                angle += UnityEngine.Random.Range(-move.jitter, move.jitter);
                Projectile.Spawn(this, move, Quaternion.Euler(0f, 0f, angle) * direction, i, delay, distance,
                    (volley * shots + i) * move.rangeStep);
            }
    }

    // Arc shots fired without a distance (the player) land on the nearest opponent within reach close to the aimed
    // direction; shots landing further and further (rangeStep) are centred on it.
    void AutoAim(MoveDef move, int total, ref Vector2 direction, ref float distance)
    {
        const float Cone = 40f; // degrees either side of the aim
        float reach = move.range + (total - 1) * move.rangeStep, best = float.MaxValue;
        Combatant target = null;
        foreach (var c in all)
        {
            if (c.team == team || !c.IsAlive) continue;
            Vector2 to = c.transform.position - transform.position;
            float d = to.magnitude;
            if (d > reach || d >= best || Vector2.Angle(direction, to) > Cone) continue;
            best = d;
            target = c;
        }
        if (target == null) return;
        direction = ((Vector2)(target.transform.position - transform.position)).normalized;
        distance = Mathf.Max(0.5f, best - (total - 1) / 2f * move.rangeStep);
    }

    void FixedUpdate()
    {
        if (!UpdateStatuses()) { castMove = null; return; }

        // Little jump in place while casting a move with a hop.
        float hop = castMove != null && castMove.hop > 0f
            ? castMove.hop * Mathf.Sin(Mathf.PI * Mathf.Clamp01(1f - (castUntil - Time.time) / castMove.castTime)) : 0f;
        if (body != null && hop != hopOffset)
        {
            body.position += Vector2.up * (hop - hopOffset);
            hopOffset = hop;
        }

        if (blinkMove != null && Time.time >= blinkUntil)
        {
            var move = blinkMove;
            blinkMove = null;
            MoveSafely(blinkDirection, move.lunge);
            ShowSelf(true);
            QueueStrikes(move, blinkDirection);
        }

        if (castMove != null && Time.time >= castUntil)
        {
            var aim = LiveAim != Vector2.zero ? Steer(LiveAim.normalized) : castDirection;
            if (CanAttack && castMove.family == "melee") Melee(castMove, aim);
            else if (CanAttack) FireProjectiles(castMove, aim, castDistance);
            castMove = null;
        }

        for (int i = 0; i < strikes.Count; i++)
        {
            var strike = strikes[i];
            if (Time.time < strike.at) continue;
            strikes.RemoveAt(i--);
            if (!CanAttack) continue;
            if (strike.move.step > 0f) MoveSafely(strike.direction, strike.move.step);
            Strike(strike.move, strike.direction);
            if (strike.move.recovery > 0f && !strikes.Exists(x => x.move == strike.move))
                recoverUntil = Time.time + strike.move.recovery;
        }

        if (Time.time < knockUntil)
        {
            body.linearVelocity = knockVelocity;
            return;
        }

        if (IsDodging)
        {
            var settings = SaveData.Settings;
            body.linearVelocity = dodgeDirection * settings.dodgeDistance / settings.dodgeTime;
            return;
        }
        if (dodgeIgnored.Count > 0) EndDodge();

        // During a dash the attacker is pushed forward and hits each opponent it reaches once; it may end with a
        // strike around it, or bounce back off the first one it hits.
        if (!IsLunging)
        {
            if (lungeFinishPending)
            {
                lungeFinishPending = false;
                Strike(lungeMove, lungeDirection, lungeMove.finishRadius);
            }
            return;
        }
        body.linearVelocity = lungeDirection * lungeSpeed;
        if (lungeBouncing) return;
        if (MeleeHit(lungeMove, lungeDirection, lungeHit) > 0 && lungeMove.bounceBack > 0f)
        {
            // Both are thrown apart, slowly enough to be seen.
            const float BounceTime = 0.3f;
            foreach (var target in lungeHit)
                if (target != null) target.Knockback(lungeDirection * lungeMove.knockback / BounceTime, BounceTime);
            lungeBouncing = true;
            lungeFinishPending = false;
            lungeDirection = -lungeDirection;
            lungeSpeed = lungeMove.bounceBack / BounceTime;
            lungeUntil = Time.time + BounceTime;
        }
    }

    /// Damage over time, statuses that end or turn into another one, confusion. False once knocked out.
    bool UpdateStatuses()
    {
        for (int i = 0; i < statuses.Count; i++)
        {
            var st = statuses[i];
            if (Time.time >= st.until)
            {
                statuses.RemoveAt(i--);
                if (!string.IsNullOrEmpty(st.def.becomes))
                    AddStatus(st.def.becomes, StatusLibrary.Get(st.def.becomes)?.duration ?? DefaultStatusDuration, st.source);
                continue;
            }
            if (st.def.damageShare <= 0f || Time.time < st.nextTick) continue;
            st.nextTick += st.def.tick;
            // Damage over time ignores defence and the post-hit protection.
            float amount = Mathf.Max(1f, maxHp * st.def.damageShare * (isBoss ? st.def.bossDamageFactor : 1f));
            LoseHp(amount);
            if (st.def.drainToSource && st.source != null && st.source.IsAlive) st.source.Heal(amount);
            if (!IsAlive) return false;
        }
        if (Time.time >= nextConfusionChange)
        {
            nextConfusionChange = Time.time + ConfusionChange;
            float angle = ConfuseAngle();
            confusionOffset = UnityEngine.Random.Range(-angle, angle);
        }
        return true;
    }

    /// Hits every opponent inside a circle placed in front of the attacker, skipping those already hit.
    int MeleeHit(MoveDef move, Vector2 direction, HashSet<Combatant> hit)
    {
        int count = 0;
        Vector2 centre = (Vector2)transform.position + direction * move.range;
        foreach (var other in Physics2D.OverlapCircleAll(centre, move.radius * meleeAreaMul))
        {
            var target = other.GetComponentInParent<Combatant>();
            if (target == null || target.team == team || !hit.Add(target)) continue;
            count++;
            EffectPlayer.Spawn(move.impactEffect, target.transform.position, direction);
            if (target.TakeHit(DamageOf(move), move.category, out float dealt))
            {
                target.Stun(SaveData.Settings.meleeStun);
                target.ApplyEffects(move, false, this);
                HitLanded(move, dealt);
            }
        }
        return count;
    }

    /// Raw damage of a move before the target's defence: power plus the matching attack stat (provisional formula).
    public float DamageOf(MoveDef move)
    {
        if (move.power <= 0) return 0f; // pure status moves
        float stat = move.category == "physical" ? atk * attackMul : spAtk * specialAttackMul;
        float burn = move.category == "physical" ? StatusProduct(def => def.physicalAttackMul) : 1f;
        float scale = (move.damageScale > 0f ? move.damageScale : 1f) * EnemyBasic(move, SaveData.Settings.enemyBasicDamageMul);
        return (move.power + stat) * Modifier("attack") * burn * damageFactor * scale;
    }

    /// A hit from a move: the target's defence (physical or special) reduces it. Returns false when the hit was
    /// ignored (already down, or protected just after a previous hit); "dealt" is the health actually lost.
    public bool TakeHit(float raw, string category, out float dealt)
    {
        dealt = 0f;
        if (!IsAlive || blinkMove != null) return false;
        if (team == Team.Player && Time.time < invulnerableUntil) return false;
        if (raw <= 0f) return true; // a status-only hit lands without damage
        if (team == Team.Player) invulnerableUntil = Time.time + PlayerInvulnerability;
        float defence = category == "physical" ? def : spDef;
        dealt = Mathf.Max(1f, raw * DefenseScale / (DefenseScale + defence) / Modifier("defense"));
        statuses.RemoveAll(st => st.def.wakeOnHit);
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
        statuses.Clear();
        modifiers.Clear();
    }

    /// Removes statuses, bonuses and penalties (between two arenas).
    public void Cleanse()
    {
        lungeUntil = dodgeUntil = dodgeReadyAt = 0f;
        ClearEffects();
    }

    /// A knocked-out team member stays on the ground but no longer blocks or gets hit.
    public void KnockOut()
    {
        lungeUntil = dodgeUntil = 0f;
        lungeFinishPending = false;
        strikes.Clear();
        castMove = null;
        if (blinkMove != null) ShowSelf(true);
        blinkMove = null;
        EndDodge();
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
        var tint = StatusTint();
        if (spriteRenderer != null)
            spriteRenderer.color = Time.time < flashUntil ? new Color(1f, 0.4f, 0.4f)
                : Time.time < healFlashUntil ? HealTint
                : Time.time < invulnerableUntil ? new Color(1f, 1f, 1f, 0.5f) // half transparent while protected
                : tint;

        // Icons above the health bar: statuses first, then bonuses and penalties, four per row.
        shownEffects.Clear();
        GetEffects(shownEffects);
        foreach (var icon in statusIcons.Values)
            if (icon != null) icon.SetActive(false);
        for (int i = 0; i < shownEffects.Count; i++)
        {
            var icon = IconFor(shownEffects[i].key);
            icon.SetActive(!Blinking(shownEffects[i].remaining));
            int row = i / IconsPerRow, column = i % IconsPerRow;
            int inRow = Mathf.Min(IconsPerRow, shownEffects.Count - row * IconsPerRow);
            icon.transform.localPosition = StatusIconOffset
                + new Vector3((column - (inRow - 1) / 2f) * IconSpacing, row * IconSpacing, 0f);
        }
    }

    /// Colour of the highest-priority status on, white when there is none.
    Color StatusTint()
    {
        if (!IsAlive) return Color.white;
        foreach (var def in StatusLibrary.All)
            foreach (var st in statuses)
                if (st.def == def && Time.time < st.until) return def.color;
        return Color.white;
    }

    /// One icon to display: a status (poison, paralysis) or a stat change (attack_up, speed_down...).
    public struct EffectIcon
    {
        public string key;
        public float remaining; // seconds left
    }

    /// Statuses, then stat bonuses and penalties, currently active on this Pokémon.
    public void GetEffects(List<EffectIcon> into)
    {
        if (!IsAlive) return;
        foreach (var def in StatusLibrary.All)
            foreach (var st in statuses)
                if (st.def == def && Time.time < st.until)
                    into.Add(new EffectIcon { key = def.key, remaining = st.until - Time.time });
        foreach (var m in modifiers)
            if (Time.time < m.until)
                into.Add(new EffectIcon { key = m.stat + (m.value > 1f ? "_up" : "_down"), remaining = m.until - Time.time });
    }

    /// True on the "off" half of the blink that announces the end of an effect.
    public static bool Blinking(float remaining) =>
        remaining < BlinkTime && Mathf.Repeat(Time.time * BlinkRate, 1f) < 0.5f;

    GameObject IconFor(string key)
    {
        if (statusIcons.TryGetValue(key, out var icon) && icon != null) return icon;
        icon = new GameObject("StatusIcon");
        icon.transform.SetParent(transform, false);
        var renderer = icon.AddComponent<SpriteRenderer>();
        renderer.sprite = Shapes.StatusIcon(key, StatusLibrary.Get(key)?.color);
        renderer.sortingOrder = 12; // above the health bar
        statusIcons[key] = icon;
        return icon;
    }
}
