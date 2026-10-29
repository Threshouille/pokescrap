using System.Collections.Generic;
using UnityEngine;

/// The ability of a Pokémon in a fight (Abilities, ability_effects.json): drawn at the start (a slot of its form,
/// kept through evolutions), the mega's own during a mega evolution, chosen in the menu for the dev characters.
public partial class Combatant
{
    const float AbilityNoticeGap = 4f; // seconds between two banners of the same ability on the same Pokémon

    /// Its ability now (AbilityLibrary id, 0: none) and what it does (null: nothing yet).
    public int AbilityId { get; private set; }
    public AbilityEffect Ability { get; private set; }
    int abilitySlot; // 0, 1: regular; 2: hidden; -1: set by id (dev characters)
    int abilityBeforeMega;
    float absorbBoostUntil, nextAbilityTick, nextAbilityCure, abilityNoticeAt = -AbilityNoticeGap;
    readonly HashSet<Combatant> intimidated = new();

    static Progression progression;

    /// The ability in that slot of its current form (0, 1: regular, 2: hidden).
    public void SetAbilitySlot(int slot)
    {
        abilitySlot = slot;
        SetAbilityId(Abilities.Suspended ? 0 : Abilities.InSlot(Stats, slot));
    }

    /// A given ability (dev characters, tests).
    public void SetAbility(int id)
    {
        abilitySlot = -1;
        SetAbilityId(id);
    }

    void SetAbilityId(int id)
    {
        AbilityId = id;
        Ability = Abilities.Effect(id);
        intimidated.Clear();
    }

    /// After an evolution: the same slot in the new form.
    void RefreshAbility()
    {
        if (abilitySlot >= 0 && Mega == null) SetAbilitySlot(abilitySlot);
    }

    void AbilityToMega(MegaDef mega)
    {
        abilityBeforeMega = AbilityId;
        if (mega.ability != 0) SetAbilityId(mega.ability);
    }

    void AbilityFromMega() => SetAbilityId(abilityBeforeMega);

    public string AbilityName => AbilityLibrary.Get(AbilityId)?.name;

    /// A banner naming the ability when it acts (not too often).
    void ShowAbility(string what = null)
    {
        if (Time.time - abilityNoticeAt < AbilityNoticeGap || AbilityName == null || Stats == null) return;
        abilityNoticeAt = Time.time;
        if (progression == null) progression = FindAnyObjectByType<Progression>();
        if (progression == null) return;
        string who = team == Team.Enemy ? $"{Stats.name} ennemi" : Stats.name;
        progression.Announce($"{AbilityName} de {who}{(what != null ? " : " + what : "")}", team == Team.Enemy ? Notice.Alert : Notice.Info);
    }

    string WeatherKey => Climate.Now?.key;

    AbilityEffect.WeatherBonus WeatherBonus => Ability?.Under(WeatherKey);

    bool HasAnyStatus
    {
        get
        {
            foreach (var st in statuses)
                if (Time.time < st.until) return true;
            return false;
        }
    }

    /// No damage from the weather (Sand Veil, Snow Cloak, Ice Body…).
    public bool WeatherProof => WeatherBonus is { chipImmune: true };

    /// Movement multiplier from the ability (weather, statuses).
    float AbilitySpeedMul
    {
        get
        {
            if (Ability == null) return 1f;
            float mul = WeatherBonus?.speedMul ?? 1f;
            if (Ability.statusedSpeedMul != 1f && HasAnyStatus) mul *= Ability.statusedSpeedMul;
            return mul;
        }
    }

    float AbilityDodgeCooldownMul => Ability == null ? 1f : Ability.dodgeCooldownMul * (WeatherBonus?.dodgeCooldownMul ?? 1f);

    /// Range multiplier of its shots (Keen Eye).
    public float AbilityRangeMul => Ability?.rangeMul ?? 1f;

    /// Its moves' damage before the target's defence, from its ability.
    float AbilityDealtMul(MoveDef move)
    {
        if (Ability == null) return 1f;
        float mul = Abilities.DealtMul(Ability, move, maxHp > 0f ? hp / maxHp : 1f, HasAnyStatus, WeatherKey);
        if (Time.time < absorbBoostUntil && move.type == Ability.absorbType) mul *= Ability.absorbBoost; // Flash Fire
        return mul;
    }

    /// Guts: a status that weakens physical moves (burn) does not.
    bool IgnoresStatusAttackLoss => Ability != null && Ability.statusedAttackMul > 1f;

    /// The same-type bonus with its ability (Adaptability), "usual" otherwise.
    float AbilityStab(float usual) => Ability != null && Ability.stabMul > 0f && usual > 1f ? Mathf.Max(usual, Ability.stabMul) : usual;

    /// A hit of "move" from "attacker" on this Pokémon, after the type chart: the multiplier both abilities give.
    float AbilityHitMul(MoveDef move, Combatant attacker, ref float typeMul, ref Efficacy efficacy)
    {
        var their = attacker != null ? attacker.Ability : null;
        if (their != null && efficacy == Efficacy.None && System.Array.IndexOf(their.ignoreImmunityTypes, move.type) >= 0)
        {
            typeMul = 1f; // Scrappy
            efficacy = Efficacy.Normal;
        }
        float mul = Abilities.TakenMul(Ability, move.type, efficacy);
        if (their != null && efficacy == Efficacy.NotVery) mul *= their.resistedDealtMul;
        return mul;
    }

    /// True when its ability takes this move in (no damage, no effect): it heals, raises a stat or powers itself.
    bool TryAbsorb(MoveDef move)
    {
        if (!Abilities.Absorbs(Ability, move.type)) return false;
        Absorb(move);
        return true;
    }

    /// Takes in a move of its absorbed type (a hit, or a shot drawn to it by Lightning Rod).
    public void Absorb(MoveDef move)
    {
        if (Ability == null || !IsAlive) return;
        if (Ability.absorbHeal > 0f) Heal(maxHp * Ability.absorbHeal);
        if (!string.IsNullOrEmpty(Ability.absorbStat)) AddModifier(Ability.absorbStat, Ability.absorbValue, Ability.absorbDuration);
        if (Ability.absorbBoost != 1f) absorbBoostUntil = Time.time + Ability.absorbDuration;
        ShowAbility("absorbe " + GameData.TypeName(move.type));
    }

    /// The Pokémon of another team whose ability draws in a shot of this move passing at "at" (Lightning Rod), or null.
    public static Combatant Attractor(MoveDef move, Vector2 at, Team shooter)
    {
        if (move == null) return null;
        foreach (var c in all)
        {
            var a = c.Ability;
            if (a == null || a.attractRadius <= 0f || c.team == shooter || !c.IsAlive || a.absorbType != move.type) continue;
            if (Vector2.Distance(c.transform.position, at) <= a.attractRadius) return c;
        }
        return null;
    }

    /// Sturdy: what a blow really takes off.
    float AbilityEndure(float amount)
    {
        float taken = Abilities.Endure(Ability, hp, maxHp, amount);
        if (taken < amount) ShowAbility("tient bon");
        return taken;
    }

    /// Pressure: the move that hit it takes longer to come back for its user.
    void AbilityPressure(MoveDef move, Combatant attacker)
    {
        if (Ability == null || Ability.attackerCooldownMul == 1f || attacker == null || attacker == this) return;
        for (int i = 0; i < MoveSlots; i++)
            if (attacker.Moves[i] == move && attacker.readyAt[i] > Time.time)
                attacker.readyAt[i] += attacker.CooldownOf(move) * (Ability.attackerCooldownMul - 1f);
    }

    /// Status chance of a move from "source", with the abilities of both (Serene Grace; Sheer Force drops it).
    static bool SideEffectsDropped(Combatant source, MoveDef move) =>
        source != null && source.Ability != null && source.Ability.effectMovesMul != 1f && Abilities.HasSideEffect(move);

    static float EffectChanceMul(Combatant source) => source != null && source.Ability != null ? source.Ability.effectChanceMul : 1f;

    /// A penalty its ability refuses (Hyper Cutter, Clear Body…).
    bool KeepsStat(string stat, float value) => value < 1f && Ability != null && Ability.KeepsStat(stat);

    bool AbilityBlocksStatus(string key) => Ability != null && Ability.ImmuneTo(key);

    float AbilityStatusDuration(string key) => Ability?.StatusDurationMul(key) ?? 1f;

    /// A blow at close range from "attacker" on "target": the target's ability may hurt or afflict the attacker
    /// (Static, Rough Skin, Aftermath), the attacker's may afflict the target (Poison Touch, Stench).
    public static void AbilityContact(Combatant attacker, Combatant target)
    {
        if (attacker == null || target == null) return;
        var their = target.Ability;
        if (their != null && attacker.IsAlive)
        {
            if (their.contactStatuses.Length > 0 && Random.value < their.contactChance)
            {
                attacker.AddStatus(their.contactStatuses[Random.Range(0, their.contactStatuses.Length)], DefaultStatusDuration, target);
                target.ShowAbility();
            }
            if (their.contactShare > 0f) attacker.TakeFixedDamage(attacker.maxHp * their.contactShare);
            if (!target.IsAlive && their.faintContactShare > 0f)
            {
                attacker.TakeFixedDamage(attacker.maxHp * their.faintContactShare);
                target.ShowAbility();
            }
        }
        var own = attacker.Ability;
        if (own != null && target.IsAlive)
        {
            if (!string.IsNullOrEmpty(own.hitStatus) && Random.value < own.hitChance) target.AddStatus(own.hitStatus, DefaultStatusDuration, attacker);
            if (own.hitStunChance > 0f && Random.value < own.hitStunChance) target.Stun(own.hitStun);
        }
    }

    /// Once a second: weather healing or damage, curing (Rain Dish, Solar Power, Hydration, Shed Skin);
    /// Intimidate on the foes that come near.
    void UpdateAbility()
    {
        if (Ability == null || !IsAlive || IsEvolving) return;
        if (!string.IsNullOrEmpty(Ability.arrivalModStat)) Intimidate();
        if (Ability.cureEvery > 0f && Time.time >= nextAbilityCure)
        {
            nextAbilityCure = Time.time + Ability.cureEvery;
            if (HasAnyStatus && Random.value < Ability.cureChance)
            {
                statuses.Clear();
                ShowAbility();
            }
        }
        if (Time.time < nextAbilityTick) return;
        nextAbilityTick = Time.time + 1f;
        var w = WeatherBonus;
        if (w == null) return;
        if (w.regenShare > 0f && hp < maxHp) Heal(maxHp * w.regenShare);
        if (w.chip > 0f) TakeFixedDamage(maxHp * w.chip);
        if (w.cure && HasAnyStatus) statuses.Clear();
    }

    /// Intimidate: each foe that comes within reach for the first time gets the penalty.
    void Intimidate()
    {
        float reach = Abilities.Config.arrivalRadius;
        bool any = false;
        foreach (var c in all)
        {
            if (c.team == team || !c.IsAlive || intimidated.Contains(c)) continue;
            if (Vector2.Distance(c.transform.position, transform.position) > reach) continue;
            intimidated.Add(c);
            if (c.KeepsStat(Ability.arrivalModStat, Ability.arrivalModValue)) continue;
            c.AddModifier(Ability.arrivalModStat, Ability.arrivalModValue, Ability.arrivalModDuration);
            any = true;
        }
        if (any) ShowAbility();
    }

    // Half a second after appearing: the arena is drawn (it clears the weather) and an elite has its mark.
    void Start() => Invoke(nameof(Arrive), 0.5f);

    /// Entering a fight (start of an arena or a zone, an enemy appearing): its weather starts (Drought…). Enemies only
    /// when bosses or elites, unless the settings say otherwise.
    public void Arrive()
    {
        intimidated.Clear();
        if (Ability == null || Ability.arrivalClimate == 0 || !IsAlive) return;
        if (team == Team.Enemy && !isBoss && !IsElite && !Abilities.Config.enemyClimate) return;
        var move = GameData.Move(Ability.arrivalClimate);
        if (move == null) return;
        Climate.Cast(move, this);
        ShowAbility();
    }
}
