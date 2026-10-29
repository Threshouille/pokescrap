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
    public bool WeatherProof => WeatherBonus is { chipImmune: true } || Ability is { magicGuard: true };

    /// Movement multiplier from the ability (weather, statuses).
    float AbilitySpeedMul
    {
        get
        {
            if (Ability == null) return 1f;
            float mul = (WeatherBonus?.speedMul ?? 1f) * speedBoostMul;
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
        if (Ability.allyTypeBoost != 0 && AllyOfTypeNear(Ability.allyTypeBoost, Ability.allyTypeRadius)) mul *= Ability.allyTypeMul; // Plus, Minus
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
        float mul = Abilities.TakenMul(Ability, move.type, efficacy) * Abilities.TakenFlatMul(Ability, move, hp >= maxHp - 0.01f);
        if (their != null && efficacy == Efficacy.NotVery) mul *= their.resistedDealtMul;
        mul *= AllyGuardMul(); // Friend Guard
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
    bool SideEffectsDropped(Combatant source, MoveDef move) =>
        (source != null && source.Ability != null && source.Ability.effectMovesMul != 1f && Abilities.HasSideEffect(move))
        || (Ability is { shieldDust: true } && source != null && source != this); // Shield Dust

    static float EffectChanceMul(Combatant source) => source != null && source.Ability != null ? source.Ability.effectChanceMul : 1f;

    /// A penalty its ability refuses (Hyper Cutter, Clear Body…).
    bool KeepsStat(string stat, float value) => value < 1f && Ability != null && Ability.KeepsStat(stat);

    bool AbilityBlocksStatus(string key) => Ability != null && (Ability.ImmuneTo(key) || WeatherBonus is { statusImmune: true });

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
        }
    }

    /// Once a second: weather healing or damage, curing (Rain Dish, Solar Power, Hydration, Shed Skin);
    /// Intimidate on the foes that come near.
    void UpdateAbility()
    {
        if (Ability == null || !IsAlive || IsEvolving) return;
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
        koAttackMul = speedBoostMul = 1f; // Moxie and Speed Boost last one arena
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
            if (Vector2.Distance(c.transform.position, transform.position) <= a.allyGuardRadius) mul = Mathf.Min(mul, a.allyGuardMul);
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
    void AbilityAfterHit(MoveDef move, Combatant attacker, float dealt)
    {
        var a = Ability;
        if (a != null && IsAlive)
        {
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
                        ShowAbility();
                    }
        }
        if (!IsAlive && attacker != null && attacker != this && attacker.IsAlive && attacker.Ability is { koAttackStep: > 1f } m
            && attacker.koAttackMul < m.koAttackMax)
        {
            attacker.koAttackMul = Mathf.Min(m.koAttackMax, attacker.koAttackMul * m.koAttackStep);
            attacker.ShowAbility();
        }
        if (!IsAlive && team == Team.Enemy) PickUp();
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
        bool flies = Stats != null && (Stats.movementType == 2 || Stats.movementType == 3
            || (Stats.types != null && System.Array.IndexOf(Stats.types, 10) >= 0));
        foreach (var c in all)
            if (c.team != team && c.IsAlive && c.Ability is { trapRadius: > 0f } a
                && Abilities.Traps(a, Vector2.Distance(c.transform.position, transform.position), Stats?.types, flies)) return true;
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
        if (acts) ShowAbility(def.name);
    }

    /// Its ability takes this terrain in: it does it no harm (Terrain.Immune).
    public static bool AbsorbsTerrain(Combatant c, string terrain) =>
        c != null && c.Ability != null && System.Array.IndexOf(c.Ability.absorbTerrains, terrain) >= 0;

    /// No taunt draws it (Oblivious).
    public bool IgnoresTaunts => Ability is { oblivious: true };
}
