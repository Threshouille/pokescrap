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
    float koAttackMul = 1f, speedBoostMul = 1f, nextSpeedBoost; // Moxie, Speed Boost: until the next arena
    float shotSizeApplied = 1f, shotSpeedApplied = 1f, meleeAreaApplied = 1f; // what the ability added to the upgrades
    static bool synchronizing; // a status sent back is not sent back again
    readonly HashSet<Combatant> intimidated = new();

    static Progression progression;

    /// The ability in that slot of its current form (0, 1: regular, 2: hidden).
    public void SetAbilitySlot(int slot)
    {
        abilitySlot = slot;
        SetAbilityId(Abilities.Suspended ? 0 : Abilities.InSlot(Stats, slot));
    }

    /// A given ability (dev characters, tests); "effect": what it does instead of the ability's own (a mega's).
    public void SetAbility(int id, AbilityEffect effect = null)
    {
        abilitySlot = -1;
        SetAbilityId(id, effect);
    }

    void SetAbilityId(int id, AbilityEffect effect = null)
    {
        AbilityId = id;
        Ability = effect ?? Abilities.Effect(id);
        intimidated.Clear();
        // Bigger or faster shots, a wider blow (Super Luck, Sniper): on top of the upgrades, taken back on a change.
        projectileSizeMul /= shotSizeApplied;
        projectileSpeedMul /= shotSpeedApplied;
        meleeAreaMul /= meleeAreaApplied;
        shotSizeApplied = Ability?.shotSizeMul ?? 1f;
        shotSpeedApplied = Ability?.shotSpeedMul ?? 1f;
        meleeAreaApplied = Ability?.meleeAreaMul ?? 1f;
        projectileSizeMul *= shotSizeApplied;
        projectileSpeedMul *= shotSpeedApplied;
        meleeAreaMul *= meleeAreaApplied;
    }

    /// After an evolution: the same slot in the new form.
    void RefreshAbility()
    {
        if (abilitySlot >= 0 && Mega == null) SetAbilitySlot(abilitySlot);
    }

    void AbilityToMega(MegaDef mega)
    {
        abilityBeforeMega = AbilityId;
        if (mega.ability == 0) return;
        SetAbilityId(mega.ability, Abilities.ForMega(mega)); // its ability and the mega's own effect
        wards.Clear();
        RefreshWards(); // its protégés, chosen now
        // Steelix, Houndoom: their weather as they mega evolve.
        if (Ability != null && Ability.arrivalClimate != 0 && GameData.Move(Ability.arrivalClimate) is { } climate) Climate.Cast(climate, this);
    }

    void AbilityFromMega() => SetAbilityId(abilityBeforeMega);

    readonly List<Combatant> wards = new(); // the team-mates its mega's effects go to (AbilityEffect.wards)

    /// The mega whose ward this Pokémon is (Gardevoir, Latias, Altaria), or null.
    public static Combatant ProtectorOf(Combatant ward)
    {
        foreach (var c in all)
            if (c != ward && c.IsAlive && c.wards.Contains(ward)) return c;
        return null;
    }

    /// Its team-mate effect reaches "c": one of its wards within the leash, or close enough when it has none.
    bool Covers(Combatant c, float radius) => c != null && c != this && c.team == team && c.IsAlive
        && Abilities.Covers(Ability, radius, wards.Contains(c), Vector2.Distance(c.transform.position, transform.position));

    /// Keeps its wards: the fallen and the ones gone to the other side dropped, filled up again from its group first
    /// (enemies), then team-mates within the leash, the supports first, the nearest first.
    void RefreshWards()
    {
        int want = Ability?.wards ?? 0;
        wards.RemoveAll(w => w == null || !w.IsAlive || w.team != team);
        if (want <= 0) { wards.Clear(); return; }
        if (wards.Count >= want) return;
        var group = TryGetComponent<EnemyAI>(out var ai) ? ai.group : null;
        float leash = Ability.wardLeash;
        var pool = new List<Combatant>();
        foreach (var c in all)
            if (c != this && c.team == team && c.IsAlive && !wards.Contains(c)) pool.Add(c);
        int Rank(Combatant c) =>
            (group != null && group.members.Contains(c) ? 0 : 4)
            + (Vector2.Distance(c.transform.position, transform.position) <= leash ? 0 : 2)
            + (c.Role == "support" ? 0 : 1);
        Vector2 me = transform.position;
        pool.Sort((x, y) => Rank(x) != Rank(y) ? Rank(x).CompareTo(Rank(y))
            : Vector2.Distance(x.transform.position, me).CompareTo(Vector2.Distance(y.transform.position, me)));
        for (int i = 0; i < pool.Count && wards.Count < want; i++) wards.Add(pool[i]);
    }

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
    public bool WeatherProof => WeatherBonus is { chipImmune: true } || Ability is { magicGuard: true } || Ability is { weatherProof: true };

    /// Movement multiplier from the ability (weather, statuses).
    float AbilitySpeedMul
    {
        get
        {
            if (Ability == null) return 1f;
            float mul = (WeatherBonus?.speedMul ?? 1f) * speedBoostMul * Ability.speedMul;
            if (Ability.statusedSpeedMul != 1f && HasAnyStatus) mul *= Ability.statusedSpeedMul;
            return mul;
        }
    }

    float AbilityDodgeCooldownMul => Ability == null ? 1f
        : Ability.dodgeCooldownMul * (WeatherBonus?.dodgeCooldownMul ?? 1f) * (HasStatus("confusion") ? Ability.confusedDodgeMul : 1f);

    /// Range multiplier of its shots (Keen Eye).
    public float AbilityRangeMul => Ability?.rangeMul ?? 1f;

    /// Its moves' damage before the target's defence, from its ability.
    float AbilityDealtMul(MoveDef move)
    {
        if (Ability == null) return 1f;
        float mul = Abilities.DealtMul(Ability, move, maxHp > 0f ? hp / maxHp : 1f, HasAnyStatus, WeatherKey);
        if (Time.time < absorbBoostUntil && move.type == Ability.absorbType) mul *= Ability.absorbBoost; // Flash Fire
        mul *= koAttackMul; // Moxie
        if (Ability.slowStartTime > 0f && Time.time - arrivedAt < Ability.slowStartTime) mul *= Ability.slowStartMul; // Slow Start
        foreach (var b in Ability.statusBoosts)
            if (move.category == b.category && HasStatus(b.status)) mul *= b.mul; // Flare Boost, Toxic Boost
        if (Ability.allyTypeBoost != 0 && AllyOfTypeNear(Ability.allyTypeBoost, Ability.allyTypeRadius)) mul *= Ability.allyTypeMul; // Plus, Minus
        return mul;
    }

    /// Guts: a status that weakens physical moves (burn) does not.
    bool IgnoresStatusAttackLoss => Ability != null && Ability.statusedAttackMul > 1f;

    /// The same-type bonus with its ability (Adaptability), "usual" otherwise.
    float AbilityStab(float usual) => Ability != null && Ability.stabMul > 0f && usual > 1f ? Mathf.Max(usual, Ability.stabMul) : usual;

    /// A hit of "move" from "attacker" on this Pokémon, after the type chart: the multiplier both abilities give.
    float AbilityHitMul(MoveDef move, int type, Combatant attacker, ref float typeMul, ref Efficacy efficacy)
    {
        var their = attacker != null ? attacker.Ability : null;
        var mine = their is { moldBreaker: true } ? null : Ability; // Mold Breaker: its own does not count
        if (their != null && efficacy == Efficacy.None && System.Array.IndexOf(their.ignoreImmunityTypes, type) >= 0)
        {
            typeMul = 1f; // Scrappy
            efficacy = Efficacy.Normal;
        }
        float mul = Abilities.TakenMul(mine, type, efficacy) * Abilities.TakenFlatMul(mine, move, hp >= maxHp - 0.01f);
        if (mine != null && mine.statusedTakenMul != 1f && HasAnyStatus) mul *= mine.statusedTakenMul; // Marvel Scale
        if (mine != null && mine.firstHitMul != 1f && attacker != null && seenFoes.Add(attacker)) mul *= mine.firstHitMul; // Forewarn
        if (their != null)
        {
            if (efficacy == Efficacy.NotVery) mul *= their.resistedDealtMul;
            if (their.rivalTypeMul != 1f && SharesType(attacker)) mul *= their.rivalTypeMul; // Rivalry
            if (their.downloadMul != 1f && (move.category == "physical" ? def < spDef : spDef < def)) mul *= their.downloadMul; // Download
            if (their.analyticMul != 1f && Time.time - lastAttackAt < 1f) mul *= their.analyticMul; // Analytic
        }
        if (their is not { infiltrator: true }) mul *= AllyGuardMul(); // Friend Guard
        return mul;
    }

    /// True when its ability takes this move in (no damage, no effect): it heals, raises a stat or powers itself.
    bool TryAbsorb(MoveDef move, Combatant attacker)
    {
        if (attacker?.Ability is { moldBreaker: true }) return false; // Mold Breaker
        if (!Abilities.Absorbs(Ability, Abilities.TypeOf(move, attacker?.Ability))) return false;
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
        AbilityFx();
    }

    /// The Pokémon of another team whose ability draws in a shot of this move passing at "at" (Lightning Rod), or null.
    public static Combatant Attractor(MoveDef move, Vector2 at, Team shooter, Combatant owner = null)
    {
        if (move == null || owner?.Ability is { ignoreRedirect: true }) return null; // Stalwart
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
        if (taken < amount)
        {
            ShowAbility("tient bon");
            AbilityFx();
        }
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
    bool SideEffectsDropped(Combatant source, MoveDef move) =>
        (source != null && source.Ability != null && source.Ability.effectMovesMul != 1f && Abilities.HasSideEffect(move))
        || (Ability is { shieldDust: true } && source != null && source != this); // Shield Dust

    static float EffectChanceMul(Combatant source) => source != null && source.Ability != null ? source.Ability.effectChanceMul : 1f;

    /// A penalty its ability refuses (Hyper Cutter, Clear Body…).
    bool KeepsStat(string stat, float value) => value < 1f && Ability != null && Ability.KeepsStat(stat);

    // ---- Lot 3 ----

    readonly HashSet<Combatant> seenFoes = new(); // Forewarn: foes whose first hit it took
    float lastAttackAt = -10f, arrivedAt, nextMoody, nextAllyCure;
    PokemonStats typeShiftFrom; // its form before Forecast, Color Change or Protean changed its type
    bool copied; // Trace has taken a foe's ability

    /// The Pokémon that dealt it its last hit (Frisk: Loot).
    public Combatant LastAttacker { get; private set; }

    int ExtraHits => Ability?.extraHits ?? 0;

    bool SharesType(Combatant other)
    {
        var mine = Stats?.types;
        var theirs = other?.Stats?.types;
        if (mine == null || theirs == null) return false;
        foreach (int t in theirs)
            if (System.Array.IndexOf(mine, t) >= 0) return true;
        return false;
    }

    float AbilityCooldownMul(MoveDef move)
    {
        if (Ability == null) return 1f;
        return Ability.cooldownMul * (move.power <= 0 ? Ability.statusCooldownMul : 1f);
    }

    /// The defence bonus a hit from "attacker" meets (Unaware, Infiltrator ignore bonuses; Unaware on it ignores the
    /// attacker's attack bonus, taken back here).
    float DefenseAgainst(Combatant attacker)
    {
        float defence = Modifier("defense");
        if (attacker?.Ability is { unaware: true } || (attacker?.Ability is { infiltrator: true } && defence > 1f)) defence = 1f;
        if (Ability is { unaware: true } && attacker != null) defence *= attacker.Modifier("attack");
        return defence;
    }

    /// Soundproof, Overcoat, Magic Bounce, Anticipation: the hit does nothing.
    bool AbilityIgnoresHit(MoveDef move, Combatant attacker, float raw)
    {
        var mine = attacker?.Ability is { moldBreaker: true } ? null : Ability;
        if (mine == null) return false;
        if ((mine.soundImmune && move.sound) || (mine.powderImmune && move.powder))
        {
            ShowAbility("sans effet");
            return true;
        }
        if (mine.magicBounce && raw <= 0f && attacker != null && attacker != this && attacker.team != team)
        {
            attacker.ApplyEffects(move, false, this); // back to its user
            ShowAbility("renvoyé");
            AbilityFx();
            return true;
        }
        if (mine.ignoreHitChance > 0f && raw > 0f && Random.value < mine.ignoreHitChance)
        {
            ShowAbility("évité");
            AbilityFx();
            return true;
        }
        return false;
    }

    /// Wonder Guard: anything but a super-effective hit does nothing; Anticipation: a super-effective one recharges
    /// the dodge.
    bool AbilityShrugs(Efficacy efficacy, Combatant attacker)
    {
        var mine = attacker?.Ability is { moldBreaker: true } ? null : Ability;
        if (mine == null) return false;
        if (mine.superEffectiveDodge && efficacy == Efficacy.Super) dodgeReadyAt = 0f; // no fx: only a dodged hit shows
        if (mine.onlySuperEffective && efficacy != Efficacy.Super)
        {
            ShowAbility("sans effet");
            return true;
        }
        return false;
    }

    /// After one of its moves goes: Truant pauses it, Protean takes its type, Analytic notes it.
    void AbilityAttacked(MoveDef move)
    {
        lastAttackAt = Time.time;
        if (Ability == null) return;
        if (Ability.afterAttackPause > 0f) recoverUntil = Mathf.Max(recoverUntil, Time.time + Ability.afterAttackPause);
        if (Ability.typeFromMove && move.power > 0) ShiftType(Abilities.TypeOf(move, Ability));
    }

    /// Unnerve of a foe close by: what it heals.
    float UnnerveMul()
    {
        float mul = 1f;
        foreach (var c in all)
            if (c.team != team && c.IsAlive && c.Ability is { foesHealMul: < 1f } a
                && Vector2.Distance(c.transform.position, transform.position) <= a.foesHealRadius) mul = Mathf.Min(mul, a.foesHealMul);
        return mul;
    }

    /// Forecast, Color Change, Protean: a single type for now (never while Terastallized or mega evolved).
    void ShiftType(int type)
    {
        if (Stats == null || TeraType >= 0 || Mega != null || type <= 0) return;
        if (Stats.types != null && Stats.types.Length == 1 && Stats.types[0] == type) return;
        typeShiftFrom ??= Stats;
        var form = typeShiftFrom.Clone();
        form.types = new[] { type };
        Stats = form;
        ShowAbility("type " + GameData.TypeName(type));
    }

    void RevertTypeShift()
    {
        if (typeShiftFrom == null) return;
        Stats = typeShiftFrom;
        typeShiftFrom = null;
    }

    /// The team's experience bonus (Illuminate of a member still up).
    public static float TeamXpMul()
    {
        float mul = 1f;
        foreach (var c in all)
            if (c.team == Team.Player && c.IsAlive && c.Ability != null) mul = Mathf.Max(mul, c.Ability.xpMul);
        return mul;
    }

    /// Harvest: a berry of the bag it eats is kept.
    public bool KeepsBerry(BagItems.Item item) =>
        item != null && (item.effect == "heal" || item.effect == "cure") && Ability is { berryKeepChance: > 0f } a && Random.value < a.berryKeepChance;

    /// Once a second, the lot 3 abilities that act on their own.
    void UpdateLotThree()
    {
        var a = Ability;
        if (a == null) return;
        // Trace: the first foe near enough lends its ability.
        if (a.copyAbility && !copied)
        {
            var foe = NearestOpponent(6f);
            if (foe != null && foe.Ability != null && !foe.Ability.copyAbility)
            {
                copied = true;
                string from = foe.AbilityName, fx = a.fx;
                SetAbilityId(foe.AbilityId);
                ShowAbility($"copie {from}");
                AbilityFx(fx);
                return;
            }
        }
        // Forecast: the type of the weather on, its own without one.
        if (a.weatherTypes.Length > 0)
        {
            int type = 0;
            foreach (var w in a.weatherTypes)
                if (w.weather == WeatherKey) type = w.type;
            if (type > 0) ShiftType(type);
            else RevertTypeShift();
        }
        if (a.moodyEvery > 0f && Time.time >= nextMoody)
        {
            nextMoody = Time.time + a.moodyEvery;
            string[] stats = { "attack", "defense", "speed" };
            int up = Random.Range(0, stats.Length), down = (up + Random.Range(1, stats.Length)) % stats.Length;
            AddModifier(stats[up], a.moodyUp, a.moodyEvery);
            AddModifier(stats[down], a.moodyDown, a.moodyEvery);
        }
        if (a.offControlRegen > 0f && !PlayerDriven && hp < maxHp && (team == Team.Player || NearestOpponent(8f) == null))
            Heal(maxHp * a.offControlRegen); // Regenerator
        if (a.allyCureEvery > 0f && Time.time >= nextAllyCure)
        {
            nextAllyCure = Time.time + a.allyCureEvery;
            foreach (var c in all)
                if (c != this && c.team == team && c.IsAlive && c.HasAnyStatus && Random.value < a.allyCureChance
                    && Vector2.Distance(c.transform.position, transform.position) <= a.allyCureRadius)
                {
                    c.statuses.Clear();
                    ShowAbility();
                    break;
                }
        }
        // Flower Gift: under its weather, it and its team-mates nearby get the bonus.
        if (a.allyWeatherRadius > 0f && !string.IsNullOrEmpty(a.allyWeatherStat) && WeatherBonus != null)
            foreach (var c in all)
                if (c.team == team && c.IsAlive && Vector2.Distance(c.transform.position, transform.position) <= a.allyWeatherRadius)
                    c.AddModifier(a.allyWeatherStat, a.allyWeatherValue, 1.5f);
    }

    bool AbilityBlocksStatus(string key) => (Ability != null && (Ability.ImmuneTo(key) || WeatherBonus is { statusImmune: true }))
        || ShieldedFromStatus(); // Latias's shield

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
            if (their.contactShare > 0f) attacker.TakeIndirectDamage(attacker.maxHp * their.contactShare);
            if (their.pickpocket && target.IsAlive && target.Held == null && attacker.Held != null) // Pickpocket
            {
                target.Hold(attacker.Hold(null));
                target.ShowAbility("vole l'objet");
            }
            if (!target.IsAlive && their.faintContactShare > 0f)
            {
                attacker.TakeIndirectDamage(attacker.maxHp * their.faintContactShare);
                target.ShowAbility();
            }
        }
        var own = attacker.Ability;
        if (own != null && target.IsAlive)
        {
            if (!string.IsNullOrEmpty(own.hitStatus) && Random.value < own.hitChance) target.AddStatus(own.hitStatus, DefaultStatusDuration, attacker);
            if (own.hitStunChance > 0f && Random.value < own.hitStunChance) target.Stun(own.hitStun);
            if (own.glueSlow != 1f && !target.KeepsStat("speed", own.glueSlow)) target.AddModifier("speed", own.glueSlow, own.glueTime); // Sticky Hold
            if (own.meleeKnockback > 0f) // Lopunny
            {
                Vector2 away = target.transform.position - attacker.transform.position;
                if (away != Vector2.zero) target.Knockback(away.normalized * own.meleeKnockback / 0.25f, 0.25f);
            }
        }
    }

    /// Once a second: weather healing or damage, curing (Rain Dish, Solar Power, Hydration, Shed Skin);
    /// Intimidate on the foes that come near.
    void UpdateAbility()
    {
        if (Ability == null || !IsAlive || IsEvolving) return;
        UpdateMegaAuras();
        if (!string.IsNullOrEmpty(Ability.arrivalModStat)) Intimidate();
        // Natural Cure: out of the player's hands and left alone a while.
        if (Ability.naturalCure > 0f && !PlayerDriven && HasAnyStatus && Time.time - lastHurtAt >= Ability.naturalCure)
        {
            statuses.Clear();
            ShowAbility();
        }
        // Speed Boost: faster and faster while a foe is near.
        if (Ability.speedBoostEvery > 0f && Time.time >= nextSpeedBoost)
        {
            nextSpeedBoost = Time.time + Ability.speedBoostEvery;
            if (NearestOpponent(8f) != null && speedBoostMul < Ability.speedBoostMax)
                speedBoostMul = Mathf.Min(Ability.speedBoostMax, speedBoostMul * Ability.speedBoostStep);
        }
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
        AbsorbTerrain();
        UpdateLotThree();
        MegaAurasEachSecond();
        if (Ability.nightmareRadius > 0f) // Bad Dreams
            foreach (var c in new List<Combatant>(all))
                if (c.team != team && c.IsAlive && c.HasStatus("sleep") && Vector2.Distance(c.transform.position, transform.position) <= Ability.nightmareRadius)
                    c.TakeIndirectDamage(c.maxHp * Ability.nightmareShare);
        var w = WeatherBonus;
        if (w == null) return;
        if (w.regenShare > 0f && hp < maxHp) Heal(maxHp * w.regenShare);
        if (w.chip > 0f) TakeIndirectDamage(maxHp * w.chip);
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
            if (c.KeepsStat(Ability.arrivalModStat, Ability.arrivalModValue) || c.Ability is { oblivious: true }) continue;
            c.AddModifier(Ability.arrivalModStat, Ability.arrivalModValue, Ability.arrivalModDuration);
            c.Lowered();
            c.TargetFx(Ability.targetFx);
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
        seenFoes.Clear();
        arrivedAt = Time.time;
        koAttackMul = speedBoostMul = 1f; // Moxie and Speed Boost last one arena
        if (Ability is { clearWeather: true } && IsAlive && Climate.Now != null) // Cloud Nine
        {
            Climate.Clear();
            ShowAbility("plus de météo");
        }
        if (Ability == null || Ability.arrivalClimate == 0 || !IsAlive) return;
        if (team == Team.Enemy && !isBoss && !IsElite && !Abilities.Config.enemyClimate) return;
        var move = GameData.Move(Ability.arrivalClimate);
        if (move == null) return;
        Climate.Cast(move, this);
        ShowAbility();
    }

    // ---- Lot 2 ----

    /// Damage that does not come from a hit (statuses, terrain, weather, recoil, contact): none with Magic Guard.
    public void TakeIndirectDamage(float amount)
    {
        if (Ability is { magicGuard: true }) return;
        TakeFixedDamage(amount);
    }

    bool MagicGuard => Ability is { magicGuard: true };

    /// Friend Guard of a team-mate nearby.
    float AllyGuardMul()
    {
        float mul = 1f;
        foreach (var c in all)
        {
            var a = c.Ability;
            if (c == this || c.team != team || !c.IsAlive || a == null || a.allyGuardRadius <= 0f) continue;
            if (c.Covers(this, a.allyGuardRadius)) mul = Mathf.Min(mul, a.allyGuardMul);
        }
        return mul;
    }

    bool AllyOfTypeNear(int type, float radius)
    {
        foreach (var c in all)
            if (c != this && c.team == team && c.IsAlive && c.Stats?.types != null && System.Array.IndexOf(c.Stats.types, type) >= 0
                && Vector2.Distance(c.transform.position, transform.position) <= radius) return true;
        return false;
    }

    /// What a hit did to it, for the abilities that answer (Anger Point, Justified, Rattled, Cursed Body, Moxie).
    void AbilityAfterHit(MoveDef move, Combatant attacker, float dealt, int type)
    {
        var a = Ability;
        if (a != null && IsAlive)
        {
            if (a.typeFromHit) ShiftType(type); // Color Change
            if (!string.IsNullOrEmpty(a.hitByCategory) && move.category == a.hitByCategory) // Weak Armor
            {
                if (!string.IsNullOrEmpty(a.hitByCategoryStat)) AddModifier(a.hitByCategoryStat, a.hitByCategoryValue, a.hitByCategoryDuration);
                if (!string.IsNullOrEmpty(a.hitByCategoryStat2)) AddModifier(a.hitByCategoryStat2, a.hitByCategoryValue2, a.hitByCategoryDuration);
            }
            if (a.rageShare > 0f && dealt >= maxHp * a.rageShare)
            {
                AddModifier("attack", a.rageValue, a.rageDuration);
                ShowAbility();
            }
            if (!string.IsNullOrEmpty(a.hitByStat) && System.Array.IndexOf(a.hitByTypes, move.type) >= 0)
            {
                AddModifier(a.hitByStat, a.hitByValue, a.hitByDuration);
                ShowAbility();
            }
            if (a.curseChance > 0f && attacker != null && attacker != this && Random.value < a.curseChance)
                for (int i = 0; i < MoveSlots; i++)
                    if (attacker.Moves[i] == move)
                    {
                        attacker.readyAt[i] = Mathf.Max(attacker.readyAt[i], Time.time + a.curseTime);
                        ShowAbility("attaque bloquée");
                        attacker.Cursed(a.targetFx, a.curseTime);
                    }
        }
        if (!IsAlive && attacker != null && attacker != this && attacker.IsAlive && attacker.Ability is { koAttackStep: > 1f } m
            && attacker.koAttackMul < m.koAttackMax)
        {
            attacker.koAttackMul = Mathf.Min(m.koAttackMax, attacker.koAttackMul * m.koAttackStep);
            attacker.ShowAbility();
        }
        if (!IsAlive && team == Team.Enemy) PickUp();
        // The attacker's mega effects on what it hit.
        var their = attacker != null && attacker != this ? attacker.Ability : null;
        if (their == null) return;
        if (!string.IsNullOrEmpty(their.hitTerrain) && (!their.hitTerrainMeleeOnly || move.family == "melee"))
            Terrain.PaintArea(their.hitTerrain, transform.position, their.hitTerrainRadius, their.hitTerrainDuration, attacker, spareTeam: true); // its own ice never trips it
        if (IsAlive && !string.IsNullOrEmpty(their.anyHitStatus) && Random.value < their.anyHitChance)
        {
            AddStatus(their.anyHitStatus, DefaultStatusDuration, attacker);
            TargetFx(their.targetFx); // Glalie, Darkrai
        }
        if (their.echoShare > 0f && !echoing && IsAlive) attacker.StartCoroutine(attacker.Echo(this, move, dealt));
    }

    static bool echoing; // a second strike does not strike again

    /// Parental Bond: the hit lands a second time, weaker, a moment later.
    System.Collections.IEnumerator Echo(Combatant target, MoveDef move, float dealt)
    {
        yield return new WaitForSeconds(0.2f);
        if (target == null || !target.IsAlive || !IsAlive || Ability == null) yield break;
        echoing = true;
        var previous = HitSource;
        HitSource = this;
        if (target.TakeHit(DamageOf(move) * Ability.echoShare, move, out _, hitCap))
        {
            target.TargetFx(Ability.targetFx); // the little one's punch
            if (Ability.echoStun > 0f) target.Stun(Ability.echoStun);
        }
        HitSource = previous;
        echoing = false;
    }

    /// Mewtwo Y: a foe's aura slows a shot of "team" flying by.
    public static float ShotSlow(Team shooter, Vector2 at)
    {
        float mul = 1f;
        foreach (var c in all)
            if (c.team != shooter && c.IsAlive && c.Ability is { shotSlowRadius: > 0f } a
                && Vector2.Distance(c.transform.position, at) <= a.shotSlowRadius) mul = Mathf.Min(mul, a.shotSlowMul);
        return mul;
    }

    /// Sableye: this shot goes back to its shooter.
    public bool ReflectsShot()
    {
        if (Ability is not { reflectShotChance: > 0f } a || !IsAlive || Random.value >= a.reflectShotChance) return false;
        ShowAbility("tir renvoyé");
        AbilityFx(); // its gem
        return true;
    }

    /// Latios, Lucario: its shots of this move pierce / turn toward their target (degrees a second, 0: no).
    public bool PiercesWith(MoveDef move) => Ability != null && System.Array.IndexOf(Ability.piercingTypes, Abilities.TypeOf(move, Ability)) >= 0;
    public float HomingTurn => Ability?.homingTurn ?? 0f;

    /// Latias: a team-mate's shield keeps statuses off it.
    bool ShieldedFromStatus()
    {
        foreach (var c in all)
            if (c != this && c.team == team && c.IsAlive && c.Ability is { allyStatusShieldRadius: > 0f } a
                && c.Covers(this, a.allyStatusShieldRadius)) return true;
        return false;
    }

    float nextTrail;

    /// Manectric: a trail of its terrain where it walks; Gengar: the foes it traps lose HP; Altaria: heals around.
    void UpdateMegaAuras()
    {
        var a = Ability;
        if (a == null || !IsAlive) return;
        if (!string.IsNullOrEmpty(a.trailTerrain) && Time.time >= nextTrail && body != null && body.linearVelocity.sqrMagnitude > 0.5f)
        {
            nextTrail = Time.time + 0.25f;
            Vector2 behind = (Vector2)transform.position - body.linearVelocity.normalized * 0.6f; // a trail, not a carpet ahead
            Terrain.PaintArea(a.trailTerrain, behind, 0.4f, 3f, this, spareTeam: true);
        }
    }

    void MegaAurasEachSecond()
    {
        var a = Ability;
        if (a == null) return;
        if (a.trappedDrain > 0f && a.trapRadius > 0f)
            foreach (var c in new List<Combatant>(all))
                if (c.team != team && c.IsAlive && c.Trapped())
                {
                    c.TakeIndirectDamage(c.maxHp * a.trappedDrain);
                    c.TargetFx(a.targetFx);
                }
        RefreshWards();
        if (a.allyHealShare > 0f)
            foreach (var c in all)
                if (c.IsAlive && c.hp < c.maxHp && (c == this || Covers(c, a.allyHealRadius)))
                    c.Heal(c.maxHp * a.allyHealShare);
    }

    /// Defiant: a foe lowered one of its stats.
    void Lowered()
    {
        if (Ability == null || Ability.loweredAttack == 1f || !IsAlive) return;
        AddModifier("attack", Ability.loweredAttack, Ability.loweredDuration);
        ShowAbility();
    }

    /// Synchronize: a status given by a foe goes back to it.
    void Synchronize(string key, float duration, Combatant source)
    {
        if (synchronizing || Ability is not { synchronize: true } || source == null || source == this || source.team == team) return;
        if (key != "poison" && key != "burn" && key != "paralysis") return;
        synchronizing = true;
        source.AddStatus(key, duration, this);
        synchronizing = false;
        ShowAbility();
        AbilityFx();
    }

    /// Steadfast: stunned, it gets faster.
    void AbilityStunned()
    {
        if (Ability == null || Ability.stunSpeedMul == 1f) return;
        AddModifier("speed", Ability.stunSpeedMul, Ability.stunSpeedTime);
    }

    /// Suction Cups, Heavy Metal: the push on it.
    float AbilityKnockbackMul => Ability == null ? 1f : Ability.noKnockback ? 0f : Ability.knockbackMul;

    float AbilityDodgeTimeMul => Ability?.dodgeTimeMul ?? 1f;

    /// Liquid Ooze: what draining "target" gives back (negative: it hurts the drainer).
    public static float DrainSign(Combatant target) => target != null && target.Ability is { liquidOoze: true } ? -1f : 1f;

    /// The last Pokémon a hit landed on (Liquid Ooze for drains: HitLanded has no target).
    public static Combatant LastHit;

    /// Arena Trap, Magnet Pull, Shadow Tag: a foe close by keeps it from dodging.
    bool Trapped()
    {
        foreach (var c in all)
            if (c.team != team && c.IsAlive && c.Ability is { trapRadius: > 0f } a
                && Abilities.Traps(a, Vector2.Distance(c.transform.position, transform.position), Stats?.types, Flies)) return true;
        return false;
    }

    /// Damp: a foe (or anyone) close by keeps this move from being used.
    bool Damped(MoveDef move)
    {
        foreach (var c in all)
            if (c != this && c.IsAlive && c.Ability is { dampRadius: > 0f } a && System.Array.IndexOf(a.dampMoves, move.id) >= 0
                && Vector2.Distance(c.transform.position, transform.position) <= a.dampRadius) return true;
        return false;
    }

    /// Unburden: its held item used up.
    void ItemUsedUp()
    {
        if (Ability == null || Ability.unburdenSpeed == 1f) return;
        AddModifier("speed", Ability.unburdenSpeed, Ability.unburdenTime);
        ShowAbility();
    }

    /// Healing of the bag's items on it (Gluttony).
    public float BagHealMul => Ability?.bagHealMul ?? 1f;

    static Bag teamBag;

    /// Pickup, Honey Gather: a foe down, each team-mate with the ability may find an item for the bag.
    void PickUp()
    {
        foreach (var c in new List<Combatant>(all))
        {
            if (c.team == team || !c.IsAlive || c.team != Team.Player || c.Ability is not { pickupChance: > 0f } a || a.pickupItems.Length == 0) continue;
            if (Random.value >= a.pickupChance) continue;
            if (teamBag == null) teamBag = FindAnyObjectByType<Bag>();
            if (teamBag != null && teamBag.Add(a.pickupItems[Random.Range(0, a.pickupItems.Length)])) c.ShowAbility("objet trouvé");
        }
    }

    /// Water Absorb on a puddle, Volt Absorb on charged ground…: once a second, a little of what absorbing gives.
    void AbsorbTerrain()
    {
        if (Ability == null || Ability.absorbTerrains.Length == 0) return;
        var def = Terrain.At(transform.position);
        if (def == null || System.Array.IndexOf(Ability.absorbTerrains, def.key) < 0) return;
        if (def.grounded && Stats != null && (Stats.movementType == 2 || Stats.movementType == 3)) return; // above it
        bool acts = false;
        if (Ability.terrainHeal > 0f && hp < maxHp)
        {
            Heal(maxHp * Ability.terrainHeal);
            acts = true;
        }
        if (!string.IsNullOrEmpty(Ability.absorbStat)) { AddModifier(Ability.absorbStat, Ability.absorbValue, 1.5f); acts = true; }
        if (Ability.absorbBoost != 1f) { absorbBoostUntil = Mathf.Max(absorbBoostUntil, Time.time + 1.5f); acts = true; }
        if (acts)
        {
            ShowAbility(def.name);
            AbilityFx(null, 3f);
        }
    }

    /// Its ability takes this terrain in: it does it no harm (Terrain.Immune).
    public static bool AbsorbsTerrain(Combatant c, string terrain) =>
        c != null && c.Ability != null && System.Array.IndexOf(c.Ability.absorbTerrains, terrain) >= 0;

    /// No taunt draws it (Oblivious).
    public bool IgnoresTaunts => Ability is { oblivious: true };
}
