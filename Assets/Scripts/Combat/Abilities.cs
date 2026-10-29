using System;
using System.Collections.Generic;
using UnityEngine;

/// What an ability does in real time (Resources/Data/ability_effects.json). Every field is neutral by default: an
/// ability only sets the ones it uses. Types are ROM type ids (0: none).
[Serializable]
public class AbilityEffect
{
    [Serializable] public class TypeMul { public int type; public float mul = 1f; }
    [Serializable] public class StatusMul { public string status; public float mul = 1f; }

    /// Under a weather (Climate key: sun, rain, hail, sand).
    [Serializable]
    public class WeatherBonus
    {
        public string weather;
        public float speedMul = 1f, dodgeCooldownMul = 1f, spAtkMul = 1f;
        public float regenShare; // max HP healed each second
        public float chip; // max HP lost each second (Solar Power)
        public bool chipImmune; // no weather damage
        public bool cure; // statuses cured each second (Hydration)
        public bool statusImmune; // no status at all (Leaf Guard)
        public TypeMul[] types = new TypeMul[0]; // damage of its moves of these types
    }

    public int id;
    public string desc; // replaces the generated description

    // Damage taken and dealt.
    public TypeMul[] takenTypes = new TypeMul[0]; // damage taken from moves of these types
    public float superEffectiveTakenMul = 1f; // damage taken from super-effective moves (Filter)
    public float resistedDealtMul = 1f; // its not-very-effective hits (Tinted Lens)
    public float stabMul; // same-type bonus instead of the usual one (Adaptability); 0: unchanged
    public int[] ignoreImmunityTypes = new int[0]; // its moves of these types ignore type immunities (Scrappy)
    public float lowPowerMax, lowPowerMul = 1f; // moves of at most this power (Technician)
    public float effectMovesMul = 1f; // moves with a side effect hit harder and lose it (Sheer Force)
    public float effectChanceMul = 1f; // chance of its moves' side effects (Serene Grace)
    public float rangeMul = 1f; // range of its shots (Keen Eye)

    // Absorbing a type: no damage, no effect; heals, raises a stat or powers its own moves of that type.
    public int absorbType;
    public float absorbHeal; // share of max HP
    public string absorbStat;
    public float absorbValue = 1f, absorbDuration = 8f;
    public float absorbBoost = 1f; // its moves of absorbType hit this much harder after absorbing (Flash Fire)
    public float attractRadius; // shots of absorbType passing this close are drawn in (Lightning Rod)
    public string[] absorbTerrains = new string[0]; // standing on these terrains: no harm, and what absorbing gives…
    public float terrainHeal; // …max HP healed each second there (absorbStat / absorbBoost: kept up while there)

    // Low health.
    public float lowHpShare; // below this share of max HP
    public int lowHpType; // moves of this type (0: all)
    public float lowHpMul = 1f;

    // Statuses.
    public string[] statusImmune = new string[0];
    public bool stunImmune; // never stunned by a hit (Inner Focus)
    public StatusMul[] statusDurations = new StatusMul[0];
    public float cureEvery, cureChance; // a chance to shed its statuses every few seconds (Shed Skin)
    public bool poisonHeals; // poison heals instead of hurting (Poison Heal)
    public float statusedAttackMul = 1f, statusedSpeedMul = 1f; // while it has a status (Guts, Quick Feet)
    public string[] modImmune = new string[0]; // stats its foes cannot lower ("all": every one)

    // Close-range blows.
    public string[] contactStatuses = new string[0]; // given to whoever strikes it (one at random)
    public float contactChance;
    public float contactShare; // max HP of whoever strikes it lost (Rough Skin)
    public float faintContactShare; // when it falls to a blow (Aftermath)
    public string hitStatus; // its own blows give it (Poison Touch)
    public float hitChance;
    public float hitStunChance, hitStun = 0.4f; // its own hits may stun (Stench)

    // Survival and recoil.
    public bool endure; // a blow at full health leaves it 1 HP (Sturdy)
    public bool noRecoil;
    public float recoilMoveMul = 1f; // moves with recoil (Reckless)

    // Arrival (start of an arena, a zone, a room; an enemy appearing).
    public int arrivalClimate; // climate move cast on arrival (Drought…)
    public string arrivalModStat; // foes around get this modifier (Intimidate)
    public float arrivalModValue = 1f, arrivalModDuration = 6f;

    // Timing.
    public float dodgeCooldownMul = 1f; // its dodge (Run Away)
    public float attackerCooldownMul = 1f; // a foe that hits it waits longer for that move (Pressure)

    public WeatherBonus[] weathers = new WeatherBonus[0];

    // ---- Lot 2 ----
    public float takenAllMul = 1f; // every hit taken (No Guard)
    public float meleeTakenMul = 1f; // close-range hits taken (Shell Armor)
    public bool noChainBonus; // chained strikes get no bonus against it
    public float fullHpTakenMul = 1f; // hits taken at full health (Multiscale)
    public bool shieldDust; // the side effects of the moves that hit it do not act
    public float physicalMul = 1f; // its physical moves (Hustle)
    public float shotJitter; // degrees its shots may stray (Hustle)
    public float shotSizeMul = 1f, shotSpeedMul = 1f, meleeAreaMul = 1f; // its shots and blows (Super Luck, Sniper)
    public float farShotMul = 1f; // a shot hits this much harder at the end of its range (Sniper)
    public float rageShare; // a single hit taking off this share of its HP…
    public float rageValue = 1.5f, rageDuration = 10f; // …raises its attack (Anger Point)
    public int[] hitByTypes = new int[0]; // hit by a move of these types…
    public string hitByStat; // …this modifier (Justified, Rattled)
    public float hitByValue = 1f, hitByDuration = 8f;
    public float loweredAttack = 1f, loweredDuration = 8f; // a foe lowers one of its stats: attack bonus (Defiant)
    public bool synchronize; // poison, burn and paralysis go back to whoever gave them
    public float naturalCure; // seconds without being hit (not played by a person) before its statuses are cured
    public bool oblivious; // neither Intimidate nor a taunting foe acts on it
    public float curseChance, curseTime = 3f; // the move that hit it is blocked for its user (Cursed Body)
    public bool liquidOoze; // draining it hurts instead of healing
    public float koAttackStep = 1f, koAttackMax = 1f; // each foe it knocks out (Moxie), up to the max, until the next arena
    public float speedBoostEvery, speedBoostStep = 1f, speedBoostMax = 1f; // in a fight, every few seconds (Speed Boost)
    public bool magicGuard; // no damage but from hits (statuses, terrain, weather, recoil, contact)
    public bool noKnockback; // Suction Cups
    public float knockbackMul = 1f, dodgeTimeMul = 1f; // Heavy Metal, Light Metal
    public float confusedDodgeMul = 1f; // dodge recharge while confused (Tangled Feet)
    public float stunSpeedMul = 1f, stunSpeedTime = 4f; // stunned: speed bonus (Steadfast)
    public float trapRadius; // foes this close cannot dodge…
    public int[] trapTypes = new int[0]; // …only of these types (empty: all)…
    public bool trapSparesFliers; // …but those that fly or float (Arena Trap)
    public float dampRadius; // these moves cannot be used this close to it (Damp)
    public int[] dampMoves = new int[0];
    public float nightmareRadius, nightmareShare; // foes asleep this close lose this share of their HP each second (Bad Dreams)
    public float allyGuardRadius, allyGuardMul = 1f; // allies this close take less (Friend Guard)
    public int allyTypeBoost; // an ally of this type within allyTypeRadius…
    public float allyTypeRadius = 4f, allyTypeMul = 1f; // …powers its moves (Plus, Minus)
    public float pickupChance; // a foe knocked out by its team: chance of a bag item…
    public string[] pickupItems = new string[0]; // …among these (Pickup, Honey Gather)
    public float bagHealMul = 1f; // healing items of the bag (Gluttony)
    public float unburdenSpeed = 1f, unburdenTime = 8f; // its held item used up: speed bonus (Unburden)

    // ---- Lot 3 ----
    [Serializable] public class WeatherType { public string weather; public int type; }
    [Serializable] public class StatusBoost { public string status, category; public float mul = 1f; }
    public float rivalTypeMul = 1f; // hitting a foe that shares one of its types (Rivalry)
    public float glueSlow = 1f, glueTime = 3f; // its close-range blows slow the target (Sticky Hold)
    public bool soundImmune, powderImmune; // sound moves, spores and powders do nothing to it
    public float ignoreHitChance; // chance to take nothing from a hit (Anticipation)
    public bool superEffectiveDodge; // a super-effective hit recharges its dodge (Anticipation)
    public bool copyAbility; // takes the ability of the first foe it meets (Trace)
    public float xpMul = 1f; // the team's experience (Illuminate)
    public WeatherType[] weatherTypes = new WeatherType[0]; // its type under a weather (Forecast)
    public bool typeFromHit, typeFromMove; // takes the type of the last move that hit it (Color Change) / of its own (Protean)
    public float firstHitMul = 1f; // the first hit of each foe (Forewarn)
    public bool simple, contrary; // its stat changes count double (Simple) / go the other way (Contrary)
    public bool unaware; // ignores its target's defence bonus and its attacker's attack bonus
    public bool moldBreaker; // its moves ignore the target's defensive abilities
    public bool frisk; // a foe it knocks out always drops its held item, consumables more often
    public float downloadMul = 1f; // its hits aimed at the target's weaker defence
    public float allDealtMul = 1f, cooldownMul = 1f; // every move it uses (Stall, Truant)
    public float afterAttackPause; // seconds without attacking after each move (Truant)
    public float slowStartTime, slowStartMul = 1f; // its attack this much for that long in each arena (Slow Start)
    public bool normalize; // its moves are Normal
    public float normalizeMul = 1f;
    public int extraHits; // one more shot in a burst, one more strike in a multi-hit move (Skill Link)
    public float statusedTakenMul = 1f; // hits taken while it has a status (Marvel Scale)
    public bool onlySuperEffective; // only super-effective hits hurt it (Wonder Guard)
    public float punchMul = 1f; // punching moves (Iron Fist)
    public float foesHealMul = 1f, foesHealRadius = 4f; // foes this close heal less (Unnerve)
    public float zoneTakenMul = 1f; // zone moves taken (Telepathy)
    public bool weatherProof; // no weather damage at all (Overcoat)
    public bool infiltrator; // its moves ignore defence bonuses and Friend Guard
    public float moodyEvery, moodyUp = 1f, moodyDown = 1f; // a random stat up, another down, every few seconds (Moody)
    public float analyticMul = 1f; // on a target that attacked less than a second ago (Analytic)
    public string hitByCategory; // hit by a move of this category… (Weak Armor)
    public string hitByCategoryStat, hitByCategoryStat2;
    public float hitByCategoryValue = 1f, hitByCategoryValue2 = 1f, hitByCategoryDuration = 8f;
    public float offControlRegen; // HP each second out of the player's hands (Regenerator)
    public bool prankster; // status moves: no cast time…
    public float statusCooldownMul = 1f; // …and a shorter recharge
    public bool pickpocket; // struck at close range with nothing held: takes the attacker's item
    public bool magicBounce; // status moves aimed at it go back to their user
    public float statusChanceMul = 1f; // chance of the statuses given to it (Wonder Skin)
    public float allyCureEvery, allyCureChance, allyCureRadius = 4f; // cures a team-mate's status now and then (Healer)
    public float berryKeepChance; // a berry of the bag it eats may not be used up (Harvest)
    public StatusBoost[] statusBoosts = new StatusBoost[0]; // with that status, moves of that category (Flare Boost)
    public bool clearWeather; // ends the weather on arrival (Cloud Nine)
    public float allyWeatherRadius; // under its weather bonus, team-mates this close get…
    public string allyWeatherStat; // …this stat bonus (Flower Gift)
    public float allyWeatherValue = 1f;

    // ---- Megas (the mega's ability, and the effect of its own on top: Abilities.ForMega) ----
    public float contactMul = 1f; // its close-range moves (Tough Claws)
    public int normalTo; // its Normal moves take this type… (Pixilate, Refrigerate)
    public float normalToMul = 1f; // …and hit this much harder
    public TypeMul[] dealtTypes = new TypeMul[0]; // its moves of these types
    public float echoShare, echoStun; // each hit strikes again this much later, may stun (Parental Bond)
    public bool ignoreRedirect; // its shots go past Lightning Rod and magnets (Stalwart)
    public string hitTerrain; // terrain laid where its hits land…
    public float hitTerrainRadius = 0.6f, hitTerrainDuration = 4f;
    public bool hitTerrainMeleeOnly; // …close-range ones only
    public float trappedDrain; // foes it keeps from dodging lose this share of HP each second
    public float shotSlowRadius, shotSlowMul = 1f; // foes' shots this close fly slower
    public float statusGivenMul = 1f; // statuses it gives last this much longer
    public float reflectShotChance; // a shot hitting it may go back to its shooter
    public float meleeDrain; // its close-range hits heal it this share of the damage
    public string trailTerrain; // terrain it leaves where it walks
    public float allyHealShare, allyHealRadius = 4f; // it and its team-mates this close heal each second
    public float allyStatusShieldRadius; // team-mates this close get no status
    public int[] piercingTypes = new int[0]; // its shots of these types go through foes
    public float homingTurn; // degrees a second its shots turn toward their target
    public float speedMul = 1f; // its movement
    public float meleeKnockback; // tiles its close-range hits push the target
    public string anyHitStatus; // any of its hits may give it…
    public float anyHitChance;

    public WeatherBonus Under(string weather)
    {
        if (string.IsNullOrEmpty(weather)) return null;
        foreach (var w in weathers)
            if (w.weather == weather) return w;
        return null;
    }

    public float TakenMul(int type)
    {
        float result = 1f;
        foreach (var t in takenTypes)
            if (t.type == type) result *= t.mul;
        return result;
    }

    public float StatusDurationMul(string status)
    {
        foreach (var s in statusDurations)
            if (s.status == status) return s.mul;
        return 1f;
    }

    public bool ImmuneTo(string status) => Array.IndexOf(statusImmune, status) >= 0;

    public bool KeepsStat(string stat) => Array.IndexOf(modImmune, "all") >= 0 || Array.IndexOf(modImmune, stat) >= 0;
}

/// Abilities in real time: which ones have an effect (ability_effects.json), who gets which, and the pure rules the
/// fight uses (Combatant.Ability.cs). The catalogue of names and descriptions is AbilityLibrary.
public static class Abilities
{
    [Serializable]
    public class Settings
    {
        public float hiddenChance = 0.05f; // a team member drawing its hidden ability at the start of a run
        public float eliteHiddenChance = 0.5f; // an elite enemy
        public bool bossHidden = true; // a boss takes its hidden ability when it has one
        public float arrivalRadius = 5f; // tiles: foes Intimidate reaches
        public bool enemyClimate; // ordinary enemies cast their arrival weather too (bosses and elites always do)
    }

    /// A family of abilities (dev picker filter, demo room): key as FamilyOf gives it, name shown.
    [Serializable] public class Family { public string key, name; }

    /// The effect of a mega's own on top of its ability (megas of ability_effects.json; mega: its megas.json id).
    [Serializable] public class MegaEffect : AbilityEffect { public int mega; }

    [Serializable]
    class File
    {
        public Settings settings = new();
        public Family[] families = new Family[0];
        public AbilityEffect[] abilities = new AbilityEffect[0];
        public MegaEffect[] megas = new MegaEffect[0];
    }

    static File data;
    static Dictionary<int, AbilityEffect> byId, byMega;

    /// While true (the AI bench), nobody draws an ability: only SetAbility gives one.
    public static bool Suspended;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { data = null; byId = null; byMega = null; Suspended = false; }

    static void Load()
    {
        if (data != null) return;
        var asset = Resources.Load<TextAsset>("Data/ability_effects");
        data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
        byId = new Dictionary<int, AbilityEffect>();
        foreach (var a in data.abilities) byId[a.id] = a;
        byMega = new Dictionary<int, AbilityEffect>();
    }

    public static Settings Config { get { Load(); return data.settings; } }

    public static IReadOnlyList<Family> Families { get { Load(); return data.families; } }

    /// Every mega's own effect, in file order.
    public static IReadOnlyList<MegaEffect> Megas { get { Load(); return data.megas; } }

    public static MegaEffect MegaOwn(int megaId)
    {
        Load();
        foreach (var m in data.megas)
            if (m.mega == megaId) return m;
        return null;
    }

    /// What a mega does: its ability and its own effect together (null when neither does anything).
    public static AbilityEffect ForMega(MegaDef mega)
    {
        if (mega == null) return null;
        Load();
        if (byMega.TryGetValue(mega.id, out var cached)) return cached;
        var merged = Merge(Effect(mega.ability), MegaOwn(mega.id));
        if (merged != null) merged.id = mega.ability;
        byMega[mega.id] = merged;
        return merged;
    }

    static readonly AbilityEffect Neutral = new();

    /// Two effects in one: a field left at its default takes the other's; multipliers (default 1) multiply, other
    /// numbers keep the larger, flags add up, lists join, a text or a type set on "b" wins.
    public static AbilityEffect Merge(AbilityEffect a, AbilityEffect b)
    {
        if (a == null && b == null) return null;
        var result = new AbilityEffect();
        foreach (var f in typeof(AbilityEffect).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            object va = a != null ? f.GetValue(a) : f.GetValue(Neutral), vb = b != null ? f.GetValue(b) : f.GetValue(Neutral), vd = f.GetValue(Neutral);
            object value;
            if (f.FieldType == typeof(float))
            {
                float fa = (float)va, fb = (float)vb, fd = (float)vd;
                value = fb == fd ? fa : fa == fd ? fb : fd == 1f ? fa * fb : Mathf.Max(fa, fb);
            }
            else if (f.FieldType == typeof(bool)) value = (bool)va || (bool)vb;
            else if (f.FieldType == typeof(int)) value = (int)vb != (int)vd ? vb : va;
            else if (f.FieldType == typeof(string)) value = !string.IsNullOrEmpty((string)vb) ? vb : va;
            else if (f.FieldType.IsArray)
            {
                var xa = (Array)va ?? Array.CreateInstance(f.FieldType.GetElementType(), 0);
                var xb = (Array)vb ?? Array.CreateInstance(f.FieldType.GetElementType(), 0);
                var joined = Array.CreateInstance(f.FieldType.GetElementType(), xa.Length + xb.Length);
                Array.Copy(xa, joined, xa.Length);
                Array.Copy(xb, 0, joined, xa.Length, xb.Length);
                value = joined;
            }
            else value = vb ?? va;
            f.SetValue(result, value);
        }
        return result;
    }

    /// The family of an ability, from what it does: weather, team (and items), contact, status, defense, attack,
    /// else combat.
    public static string FamilyOf(AbilityEffect e)
    {
        if (e == null) return "combat";
        if (e.weathers.Length > 0 || e.arrivalClimate != 0 || e.weatherTypes.Length > 0 || e.clearWeather || e.weatherProof) return "weather";
        if (e.allyGuardRadius > 0f || e.allyTypeBoost != 0 || e.pickupChance > 0f || e.bagHealMul != 1f || e.unburdenSpeed != 1f
            || e.xpMul != 1f || e.frisk || e.berryKeepChance > 0f || e.pickpocket || e.allyCureEvery > 0f || e.foesHealMul != 1f) return "team";
        if (e.contactStatuses.Length > 0 || e.contactShare > 0f || e.faintContactShare > 0f || !string.IsNullOrEmpty(e.hitStatus)
            || e.hitStunChance > 0f) return "contact";
        if (e.statusImmune.Length > 0 || e.statusDurations.Length > 0 || e.cureEvery > 0f || e.poisonHeals || e.statusedAttackMul != 1f
            || e.statusedSpeedMul != 1f || e.synchronize || e.naturalCure > 0f || e.shieldDust || e.magicGuard || e.modImmune.Length > 0
            || e.stunImmune || e.statusChanceMul != 1f || e.statusBoosts.Length > 0 || e.magicBounce) return "status";
        if (e.takenTypes.Length > 0 || e.superEffectiveTakenMul != 1f || e.absorbType != 0 || e.endure || e.meleeTakenMul != 1f
            || e.fullHpTakenMul != 1f || e.liquidOoze || e.soundImmune || e.powderImmune || e.onlySuperEffective
            || e.statusedTakenMul != 1f || e.zoneTakenMul != 1f || e.firstHitMul != 1f || e.ignoreHitChance > 0f) return "defense";
        if (e.stabMul > 0f || e.lowHpShare > 0f || e.lowPowerMax > 0f || e.effectMovesMul != 1f || e.effectChanceMul != 1f || e.rangeMul != 1f
            || e.resistedDealtMul != 1f || e.ignoreImmunityTypes.Length > 0 || e.physicalMul != 1f || e.shotSizeMul != 1f
            || e.shotSpeedMul != 1f || e.farShotMul != 1f || e.recoilMoveMul != 1f || e.noRecoil || e.koAttackStep > 1f
            || e.rivalTypeMul != 1f || e.downloadMul != 1f || e.analyticMul != 1f || e.punchMul != 1f || e.normalize
            || e.allDealtMul != 1f || e.extraHits > 0 || e.slowStartTime > 0f || e.moldBreaker || e.infiltrator || e.unaware) return "attack";
        return "combat";
    }

    public static string FamilyName(string key)
    {
        foreach (var f in Families)
            if (f.key == key) return f.name;
        return key;
    }

    /// Every ability with an effect, in file order.
    public static IReadOnlyList<AbilityEffect> All { get { Load(); return data.abilities; } }

    /// Its effect, or null when the ability does nothing yet.
    public static AbilityEffect Effect(int id)
    {
        Load();
        return byId.TryGetValue(id, out var e) ? e : null;
    }

    // ---- Who gets which ----

    /// The abilities a form may have: slot 0 and 1 regular, slot 2 hidden (0 where it has none).
    public static int InSlot(PokemonStats stats, int slot)
    {
        if (stats == null) return 0;
        if (slot >= 2) return stats.hiddenAbility;
        var regular = stats.abilities;
        if (regular == null || regular.Length == 0) return 0;
        return regular[Mathf.Min(slot, regular.Length - 1)];
    }

    /// Draws a slot: hidden with that chance when the form has one, otherwise one of its regular abilities.
    public static int DrawSlot(PokemonStats stats, float hiddenChance, System.Random rng)
    {
        if (stats == null) return 0;
        // Never an ability that does nothing when another one of the form does something (Imposter, Klutz…).
        bool hidden = stats.hiddenAbility != 0 && (Effect(stats.hiddenAbility) != null || !AnyActive(stats));
        if (hidden && rng.NextDouble() < hiddenChance) return 2;
        int count = stats.abilities != null ? stats.abilities.Length : 0;
        var active = new List<int>();
        for (int i = 0; i < count; i++)
            if (Effect(stats.abilities[i]) != null) active.Add(i);
        if (active.Count > 0) return active[rng.Next(active.Count)];
        if (stats.hiddenAbility != 0 && Effect(stats.hiddenAbility) != null) return 2;
        return count > 1 ? rng.Next(count) : 0;
    }

    static bool AnyActive(PokemonStats stats)
    {
        if (stats.abilities != null)
            foreach (int a in stats.abilities)
                if (Effect(a) != null) return true;
        return false;
    }

    /// The modifier a stat change becomes with the ability of who gets it (Simple: twice as far from 1; Contrary: the
    /// other way).
    public static float ChangedModifier(AbilityEffect holder, float value)
    {
        if (holder == null || value <= 0f) return value;
        if (holder.contrary) value = 1f / value;
        if (holder.simple) value = Mathf.Max(0.1f, 1f + 2f * (value - 1f));
        return value;
    }

    /// The type of a move as its user throws it (Normalize: Normal).
    public static int TypeOf(MoveDef move, AbilityEffect user) =>
        user is { normalize: true } ? 1 : move.type == 1 && user is { normalTo: > 0 } ? user.normalTo : move.type; // Pixilate

    // ---- Pure rules (also used by the tests) ----

    /// Multiplier on the damage a hit of moveType deals to "holder", from the holder's ability.
    public static float TakenMul(AbilityEffect holder, int moveType, Efficacy efficacy)
    {
        if (holder == null) return 1f;
        float mul = holder.TakenMul(moveType);
        if (efficacy == Efficacy.Super) mul *= holder.superEffectiveTakenMul;
        return mul;
    }

    /// Multiplier on the damage the attacker's move deals before the target's defence, from the attacker's ability
    /// (hp share: its health now).
    public static float DealtMul(AbilityEffect attacker, MoveDef move, float hpShare, bool statused, string weather)
    {
        if (attacker == null || move == null) return 1f;
        float mul = 1f;
        if (attacker.lowHpShare > 0f && hpShare <= attacker.lowHpShare && (attacker.lowHpType == 0 || attacker.lowHpType == move.type))
            mul *= attacker.lowHpMul;
        if (attacker.lowPowerMax > 0f && move.power > 0 && move.power <= attacker.lowPowerMax) mul *= attacker.lowPowerMul;
        if (attacker.effectMovesMul != 1f && HasSideEffect(move)) mul *= attacker.effectMovesMul;
        if (statused && move.category == "physical") mul *= attacker.statusedAttackMul;
        if (move.category == "physical") mul *= attacker.physicalMul;
        mul *= attacker.allDealtMul;
        if (move.family == "melee") mul *= attacker.contactMul; // Tough Claws
        if (move.type == 1 && attacker.normalTo > 0) mul *= attacker.normalToMul; // Pixilate, Refrigerate
        foreach (var t in attacker.dealtTypes)
            if (t.type == move.type) mul *= t.mul;
        if (move.punch) mul *= attacker.punchMul;
        if (attacker.normalize) mul *= attacker.normalizeMul;
        if (move.recoil > 0f) mul *= attacker.recoilMoveMul;
        var w = attacker.Under(weather);
        if (w != null)
        {
            foreach (var t in w.types)
                if (t.type == move.type) mul *= t.mul;
            if (move.category == "special") mul *= w.spAtkMul;
        }
        return mul;
    }

    /// Multiplier on a hit taken by "holder" from its own ability: every hit, close range, at full health.
    public static float TakenFlatMul(AbilityEffect holder, MoveDef move, bool fullHealth)
    {
        if (holder == null || move == null) return 1f;
        float mul = holder.takenAllMul;
        if (move.family == "melee") mul *= holder.meleeTakenMul;
        if (move.family == "zone") mul *= holder.zoneTakenMul;
        if (fullHealth) mul *= holder.fullHpTakenMul;
        return mul;
    }

    /// Sniper: a shot that has covered "share" of its range hits this much harder.
    public static float FarShotMul(AbilityEffect owner, float share) =>
        owner == null || owner.farShotMul == 1f ? 1f : Mathf.Lerp(1f, owner.farShotMul, Mathf.Clamp01(share));

    /// True when a foe with a trapping ability at "distance" keeps "victim" (its types, its movement) from dodging.
    public static bool Traps(AbilityEffect trapper, float distance, int[] victimTypes, bool victimFlies)
    {
        if (trapper == null || trapper.trapRadius <= 0f || distance > trapper.trapRadius) return false;
        if (trapper.trapSparesFliers && victimFlies) return false;
        if (trapper.trapTypes.Length == 0) return true;
        if (victimTypes == null) return false;
        foreach (int t in victimTypes)
            if (Array.IndexOf(trapper.trapTypes, t) >= 0) return true;
        return false;
    }

    public static bool HasSideEffect(MoveDef move) => !string.IsNullOrEmpty(move.status) || !string.IsNullOrEmpty(move.modStat);

    /// True when the attacker's move would be absorbed by this ability.
    public static bool Absorbs(AbilityEffect holder, int moveType) => holder != null && holder.absorbType != 0 && holder.absorbType == moveType;

    /// Sturdy: the damage a blow of "amount" really deals (it stops at 1 HP when it would fell it from full health).
    public static float Endure(AbilityEffect holder, float hp, float maxHp, float amount) =>
        holder != null && holder.endure && hp >= maxHp - 0.01f && hp > 1f && hp - amount <= 0f ? hp - 1f : amount;

    // ---- Text ----

    static string TypeName(int type) => GameData.TypeName(type);

    static string Pct(float share) => $"{Mathf.RoundToInt(share * 100f)} %";

    static string Times(float mul) => $"× {mul:0.##}";

    static string StatName(string stat) => stat switch
    {
        "attack" => "l'attaque",
        "defense" => "la défense",
        "speed" => "la vitesse",
        "all" => "les stats",
        _ => stat,
    };

    static string StatusName(string key) => StatusLibrary.Get(key)?.name ?? key;

    static string WeatherName(string key) => Climate.WeatherName(key);

    /// What the ability does, from its values (its desc when written).
    public static string Describe(AbilityEffect e)
    {
        if (e == null) return "Sans effet pour l'instant.";
        if (!string.IsNullOrEmpty(e.desc)) return e.desc;
        var parts = new List<string>();
        foreach (var t in e.takenTypes)
            parts.Add($"dégâts {TypeName(t.type)} reçus {Times(t.mul)}");
        if (e.superEffectiveTakenMul != 1f) parts.Add($"dégâts super efficaces reçus {Times(e.superEffectiveTakenMul)}");
        if (e.resistedDealtMul != 1f) parts.Add($"coups peu efficaces {Times(e.resistedDealtMul)}");
        if (e.stabMul > 0f) parts.Add($"bonus de type {Times(e.stabMul)}");
        foreach (int t in e.ignoreImmunityTypes) parts.Add($"attaques {TypeName(t)} sans immunité");
        if (e.lowPowerMax > 0f) parts.Add($"attaques de puissance ≤ {e.lowPowerMax:0} {Times(e.lowPowerMul)}");
        if (e.effectMovesMul != 1f) parts.Add($"attaques à effet {Times(e.effectMovesMul)}, sans l'effet");
        if (e.effectChanceMul != 1f) parts.Add($"chances d'effet {Times(e.effectChanceMul)}");
        if (e.rangeMul != 1f) parts.Add($"portée des tirs {Times(e.rangeMul)}");
        if (e.absorbType != 0)
        {
            var gain = new List<string>();
            if (e.absorbHeal > 0f) gain.Add($"soigne {Pct(e.absorbHeal)} des PV");
            if (!string.IsNullOrEmpty(e.absorbStat)) gain.Add($"{StatName(e.absorbStat)} {Times(e.absorbValue)} {e.absorbDuration:0} s");
            if (e.absorbBoost != 1f) gain.Add($"ses attaques {TypeName(e.absorbType)} {Times(e.absorbBoost)}");
            parts.Add($"absorbe les attaques {TypeName(e.absorbType)}" + (gain.Count > 0 ? " : " + string.Join(", ", gain) : ""));
            if (e.attractRadius > 0f) parts.Add($"attire les tirs {TypeName(e.absorbType)} à {e.attractRadius:0.#} cases");
            if (e.absorbTerrains.Length > 0)
                parts.Add($"sur {string.Join(", ", Array.ConvertAll(e.absorbTerrains, k => Terrain.Def(k)?.name ?? k))} : aucun mal"
                    + (e.terrainHeal > 0f ? $", +{Pct(e.terrainHeal)} des PV par seconde" : ", même effet tant qu'il y reste"));
        }
        if (e.lowHpShare > 0f)
            parts.Add($"sous {Pct(e.lowHpShare)} des PV, attaques {(e.lowHpType != 0 ? TypeName(e.lowHpType) + " " : "")}{Times(e.lowHpMul)}");
        foreach (var s in e.statusImmune) parts.Add($"immunisé : {StatusName(s)}");
        if (e.stunImmune) parts.Add("jamais étourdi");
        foreach (var s in e.statusDurations) parts.Add($"{StatusName(s.status)} {Times(s.mul)} de durée");
        if (e.cureEvery > 0f) parts.Add($"{Pct(e.cureChance)} de guérir toutes les {e.cureEvery:0.#} s");
        if (e.poisonHeals) parts.Add("le poison le soigne");
        if (e.statusedAttackMul != 1f) parts.Add($"sous statut, attaque physique {Times(e.statusedAttackMul)}");
        if (e.statusedSpeedMul != 1f) parts.Add($"sous statut, vitesse {Times(e.statusedSpeedMul)}");
        foreach (var s in e.modImmune) parts.Add($"on ne peut pas baisser {StatName(s)}");
        if (e.contactStatuses.Length > 0)
            parts.Add($"{Pct(e.contactChance)} de donner {string.Join(" ou ", Array.ConvertAll(e.contactStatuses, StatusName))} à qui le frappe au corps à corps");
        if (e.contactShare > 0f) parts.Add($"qui le frappe au corps à corps perd {Pct(e.contactShare)} de ses PV");
        if (e.faintContactShare > 0f) parts.Add($"K.O. au corps à corps : l'attaquant perd {Pct(e.faintContactShare)} de ses PV");
        if (!string.IsNullOrEmpty(e.hitStatus)) parts.Add($"ses coups au corps à corps : {Pct(e.hitChance)} de {StatusName(e.hitStatus)}");
        if (e.hitStunChance > 0f) parts.Add($"{Pct(e.hitStunChance)} d'étourdir à chaque coup");
        if (e.endure) parts.Add("survit à 1 PV à un coup reçu PV pleins");
        if (e.noRecoil) parts.Add("aucun recul");
        if (e.recoilMoveMul != 1f) parts.Add($"attaques à recul {Times(e.recoilMoveMul)}");
        if (e.arrivalClimate != 0) parts.Add($"à son arrivée : {GameData.Move(e.arrivalClimate)?.name}");
        if (!string.IsNullOrEmpty(e.arrivalModStat))
            parts.Add($"à son arrivée : {StatName(e.arrivalModStat)} des adversaires proches {Times(e.arrivalModValue)} {e.arrivalModDuration:0} s");
        if (e.dodgeCooldownMul != 1f) parts.Add($"recharge de l'esquive {Times(e.dodgeCooldownMul)}");
        if (e.attackerCooldownMul != 1f) parts.Add($"qui le frappe recharge son attaque {Times(e.attackerCooldownMul)}");
        foreach (var w in e.weathers)
        {
            var under = new List<string>();
            if (w.speedMul != 1f) under.Add($"vitesse {Times(w.speedMul)}");
            if (w.dodgeCooldownMul != 1f) under.Add($"recharge de l'esquive {Times(w.dodgeCooldownMul)}");
            if (w.spAtkMul != 1f) under.Add($"attaque spéciale {Times(w.spAtkMul)}");
            foreach (var t in w.types) under.Add($"attaques {TypeName(t.type)} {Times(t.mul)}");
            if (w.regenShare > 0f) under.Add($"+{Pct(w.regenShare)} des PV par seconde");
            if (w.chip > 0f) under.Add($"perd {Pct(w.chip)} des PV par seconde");
            if (w.chipImmune) under.Add("aucun dégât de la météo");
            if (w.cure) under.Add("guérit de ses statuts");
            if (w.statusImmune) under.Add("aucun statut");
            if (under.Count > 0) parts.Add($"{WeatherName(w.weather)} : {string.Join(", ", under)}");
        }
        if (e.takenAllMul != 1f) parts.Add($"dégâts reçus {Times(e.takenAllMul)}");
        if (e.meleeTakenMul != 1f) parts.Add($"dégâts reçus au corps à corps {Times(e.meleeTakenMul)}");
        if (e.noChainBonus) parts.Add("pas de bonus de série contre lui");
        if (e.fullHpTakenMul != 1f) parts.Add($"à PV pleins, dégâts reçus {Times(e.fullHpTakenMul)}");
        if (e.shieldDust) parts.Add("insensible aux effets secondaires des attaques");
        if (e.physicalMul != 1f) parts.Add($"attaques physiques {Times(e.physicalMul)}");
        if (e.shotJitter > 0f) parts.Add($"tirs déviés jusqu'à {e.shotJitter:0}°");
        if (e.shotSizeMul != 1f) parts.Add($"taille des tirs {Times(e.shotSizeMul)}");
        if (e.shotSpeedMul != 1f) parts.Add($"vitesse des tirs {Times(e.shotSpeedMul)}");
        if (e.meleeAreaMul != 1f) parts.Add($"zone des coups {Times(e.meleeAreaMul)}");
        if (e.farShotMul != 1f) parts.Add($"tirs jusqu'à {Times(e.farShotMul)} en bout de course");
        if (e.rageShare > 0f) parts.Add($"un coup de plus de {Pct(e.rageShare)} des PV : attaque {Times(e.rageValue)} {e.rageDuration:0} s");
        if (e.hitByTypes.Length > 0 && !string.IsNullOrEmpty(e.hitByStat))
            parts.Add($"touché par {string.Join(", ", Array.ConvertAll(e.hitByTypes, TypeName))} : {StatName(e.hitByStat)} {Times(e.hitByValue)} {e.hitByDuration:0} s");
        if (e.loweredAttack != 1f) parts.Add($"une stat baissée : attaque {Times(e.loweredAttack)} {e.loweredDuration:0} s");
        if (e.synchronize) parts.Add("renvoie poison, brûlure et paralysie");
        if (e.naturalCure > 0f) parts.Add($"guérit après {e.naturalCure:0} s sans être touché (hors contrôle)");
        if (e.oblivious) parts.Add("insensible à Intimidation et aux provocations");
        if (e.curseChance > 0f) parts.Add($"{Pct(e.curseChance)} de bloquer l'attaque qui le touche {e.curseTime:0} s");
        if (e.liquidOoze) parts.Add("qui le draine perd des PV");
        if (e.koAttackStep != 1f) parts.Add($"chaque K.O. : attaque {Times(e.koAttackStep)} (jusqu'à {Times(e.koAttackMax)})");
        if (e.speedBoostEvery > 0f) parts.Add($"en combat, vitesse {Times(e.speedBoostStep)} toutes les {e.speedBoostEvery:0} s (jusqu'à {Times(e.speedBoostMax)})");
        if (e.magicGuard) parts.Add("ne perd des PV que par les coups");
        if (e.noKnockback) parts.Add("jamais repoussé");
        if (e.knockbackMul != 1f) parts.Add($"poussée {Times(e.knockbackMul)}");
        if (e.dodgeTimeMul != 1f) parts.Add($"esquive {Times(e.dodgeTimeMul)} plus longue");
        if (e.confusedDodgeMul != 1f) parts.Add($"confus, recharge de l'esquive {Times(e.confusedDodgeMul)}");
        if (e.stunSpeedMul != 1f) parts.Add($"étourdi, vitesse {Times(e.stunSpeedMul)} {e.stunSpeedTime:0} s");
        if (e.trapRadius > 0f)
            parts.Add($"les adversaires {(e.trapTypes.Length > 0 ? string.Join(", ", Array.ConvertAll(e.trapTypes, TypeName)) + " " : "")}à {e.trapRadius:0.#} cases ne peuvent plus esquiver{(e.trapSparesFliers ? " (sauf ceux qui volent)" : "")}");
        if (e.dampRadius > 0f) parts.Add($"empêche {string.Join(", ", Array.ConvertAll(e.dampMoves, m => GameData.Move(m)?.name ?? m.ToString()))} à {e.dampRadius:0.#} cases");
        if (e.nightmareRadius > 0f) parts.Add($"adversaires endormis à {e.nightmareRadius:0.#} cases : −{Pct(e.nightmareShare)} des PV par seconde");
        if (e.allyGuardRadius > 0f) parts.Add($"alliés à {e.allyGuardRadius:0.#} cases : dégâts reçus {Times(e.allyGuardMul)}");
        if (e.allyTypeBoost != 0) parts.Add($"un allié {TypeName(e.allyTypeBoost)} à {e.allyTypeRadius:0.#} cases : attaques {Times(e.allyTypeMul)}");
        if (e.pickupChance > 0f) parts.Add($"{Pct(e.pickupChance)} de trouver un objet à chaque K.O. ennemi");
        if (e.bagHealMul != 1f) parts.Add($"soins du sac {Times(e.bagHealMul)}");
        if (e.unburdenSpeed != 1f) parts.Add($"objet tenu épuisé : vitesse {Times(e.unburdenSpeed)} {e.unburdenTime:0} s");
        if (e.rivalTypeMul != 1f) parts.Add($"contre un adversaire qui partage un de ses types : dégâts {Times(e.rivalTypeMul)}");
        if (e.glueSlow != 1f) parts.Add($"ses coups au corps à corps : vitesse de la cible {Times(e.glueSlow)} {e.glueTime:0} s");
        if (e.soundImmune) parts.Add("immunisé contre les attaques sonores");
        if (e.powderImmune) parts.Add("immunisé contre les spores et poudres");
        if (e.ignoreHitChance > 0f) parts.Add($"{Pct(e.ignoreHitChance)} de chances d'ignorer un coup");
        if (e.superEffectiveDodge) parts.Add("un coup super efficace recharge son esquive");
        if (e.copyAbility) parts.Add("copie le talent du premier adversaire qu'il croise");
        if (e.xpMul != 1f) parts.Add($"expérience de l'équipe {Times(e.xpMul)}");
        foreach (var w in e.weatherTypes) parts.Add($"{WeatherName(w.weather)} : type {TypeName(w.type)}");
        if (e.typeFromHit) parts.Add("prend le type de la dernière attaque qui le touche");
        if (e.typeFromMove) parts.Add("prend le type de l'attaque qu'il lance");
        if (e.firstHitMul != 1f) parts.Add($"premier coup de chaque adversaire {Times(e.firstHitMul)}");
        if (e.simple) parts.Add("ses bonus et malus comptent double");
        if (e.contrary) parts.Add("ses bonus et malus sont inversés");
        if (e.unaware) parts.Add("ignore les bonus de défense de ses cibles et d'attaque de ceux qui le frappent");
        if (e.moldBreaker) parts.Add("ses attaques ignorent les talents défensifs");
        if (e.frisk) parts.Add("les ennemis qu'il met K.O. lâchent plus souvent un objet du sac (× 1,5)");
        if (e.downloadMul != 1f) parts.Add($"coups visant la défense la plus faible {Times(e.downloadMul)}");
        if (e.allDealtMul != 1f) parts.Add($"dégâts {Times(e.allDealtMul)}");
        if (e.cooldownMul != 1f) parts.Add($"recharge {Times(e.cooldownMul)}");
        if (e.afterAttackPause > 0f) parts.Add($"{e.afterAttackPause:0.#} s sans attaquer après chaque attaque");
        if (e.slowStartTime > 0f) parts.Add($"attaque {Times(e.slowStartMul)} les {e.slowStartTime:0} premières secondes");
        if (e.normalize) parts.Add($"ses attaques deviennent Normal, {Times(e.normalizeMul)}");
        if (e.extraHits > 0) parts.Add($"+{e.extraHits} tir ou coup aux attaques qui en ont plusieurs");
        if (e.statusedTakenMul != 1f) parts.Add($"sous statut, dégâts reçus {Times(e.statusedTakenMul)}");
        if (e.onlySuperEffective) parts.Add("seuls les coups super efficaces le blessent");
        if (e.punchMul != 1f) parts.Add($"attaques de poing {Times(e.punchMul)}");
        if (e.foesHealMul != 1f) parts.Add($"adversaires à {e.foesHealRadius:0} cases : soins {Times(e.foesHealMul)}");
        if (e.zoneTakenMul != 1f) parts.Add($"dégâts des zones reçus {Times(e.zoneTakenMul)}");
        if (e.weatherProof) parts.Add("aucun dégât de météo");
        if (e.infiltrator) parts.Add("ignore les bonus de défense et Garde-Ami");
        if (e.moodyEvery > 0f) parts.Add($"toutes les {e.moodyEvery:0} s : une stat {Times(e.moodyUp)}, une autre {Times(e.moodyDown)}");
        if (e.analyticMul != 1f) parts.Add($"sur une cible qui vient d'attaquer {Times(e.analyticMul)}");
        if (!string.IsNullOrEmpty(e.hitByCategory))
            parts.Add($"touché par une attaque {(e.hitByCategory == "physical" ? "physique" : "spéciale")} : {StatName(e.hitByCategoryStat)} {Times(e.hitByCategoryValue)}"
                + (string.IsNullOrEmpty(e.hitByCategoryStat2) ? "" : $", {StatName(e.hitByCategoryStat2)} {Times(e.hitByCategoryValue2)}"));
        if (e.offControlRegen > 0f) parts.Add($"+{Pct(e.offControlRegen)} des PV par seconde hors contrôle ou hors combat");
        if (e.prankster) parts.Add($"attaques de statut sans lancement, recharge {Times(e.statusCooldownMul)}");
        if (e.pickpocket) parts.Add("frappé au corps à corps sans objet : prend celui de l'attaquant");
        if (e.magicBounce) parts.Add("renvoie les attaques de statut");
        if (e.statusChanceMul != 1f) parts.Add($"chances de subir un statut {Times(e.statusChanceMul)}");
        if (e.allyCureEvery > 0f) parts.Add($"{Pct(e.allyCureChance)} de guérir un allié proche toutes les {e.allyCureEvery:0} s");
        if (e.berryKeepChance > 0f) parts.Add($"{Pct(e.berryKeepChance)} de garder la baie du sac qu'il mange");
        foreach (var b in e.statusBoosts)
            parts.Add($"{StatusName(b.status)} : attaques {(b.category == "special" ? "spéciales" : "physiques")} {Times(b.mul)}");
        if (e.clearWeather) parts.Add("à son arrivée, met fin à la météo");
        if (e.contactMul != 1f) parts.Add($"corps à corps {Times(e.contactMul)}");
        if (e.normalTo > 0) parts.Add($"attaques Normal → {TypeName(e.normalTo)} {Times(e.normalToMul)}");
        foreach (var t in e.dealtTypes) parts.Add($"attaques {TypeName(t.type)} {Times(t.mul)}");
        if (e.echoShare > 0f) parts.Add($"chaque coup frappe une seconde fois à {Pct(e.echoShare)}{(e.echoStun > 0f ? ", qui étourdit" : "")}");
        if (e.ignoreRedirect) parts.Add("ses tirs ignorent Paratonnerre et les aimants");
        if (!string.IsNullOrEmpty(e.hitTerrain)) parts.Add($"{(e.hitTerrainMeleeOnly ? "ses coups au corps à corps" : "ses coups")} laissent : {Terrain.Def(e.hitTerrain)?.name ?? e.hitTerrain}");
        if (e.trappedDrain > 0f) parts.Add($"adversaires piégés : −{Pct(e.trappedDrain)} des PV par seconde");
        if (e.shotSlowRadius > 0f) parts.Add($"tirs ennemis à {e.shotSlowRadius:0.#} cases : vitesse {Times(e.shotSlowMul)}");
        if (e.statusGivenMul != 1f) parts.Add($"statuts donnés {Times(e.statusGivenMul)} plus longs");
        if (e.reflectShotChance > 0f) parts.Add($"{Pct(e.reflectShotChance)} de renvoyer un tir");
        if (e.meleeDrain > 0f) parts.Add($"corps à corps : draine {Pct(e.meleeDrain)}");
        if (!string.IsNullOrEmpty(e.trailTerrain)) parts.Add($"laisse en marchant : {Terrain.Def(e.trailTerrain)?.name ?? e.trailTerrain}");
        if (e.allyHealShare > 0f) parts.Add($"lui et ses alliés à {e.allyHealRadius:0} cases : +{Pct(e.allyHealShare)} des PV par seconde");
        if (e.allyStatusShieldRadius > 0f) parts.Add($"alliés à {e.allyStatusShieldRadius:0} cases : aucun statut");
        if (e.piercingTypes.Length > 0) parts.Add($"tirs {string.Join(", ", Array.ConvertAll(e.piercingTypes, TypeName))} perçants");
        if (e.homingTurn > 0f) parts.Add("ses tirs suivent la cible");
        if (e.speedMul != 1f) parts.Add($"vitesse {Times(e.speedMul)}");
        if (e.meleeKnockback > 0f) parts.Add("ses coups au corps à corps repoussent");
        if (!string.IsNullOrEmpty(e.anyHitStatus)) parts.Add($"{Pct(e.anyHitChance)} de {StatusName(e.anyHitStatus)} à chaque coup");
        if (e.allyWeatherRadius > 0f && e.weathers.Length > 0)
            parts.Add($"{WeatherName(e.weathers[0].weather)} : lui et ses alliés à {e.allyWeatherRadius:0} cases, {StatName(e.allyWeatherStat)} {Times(e.allyWeatherValue)}");
        if (parts.Count == 0) return "Sans effet pour l'instant.";
        string text = string.Join(" ; ", parts) + ".";
        return char.ToUpper(text[0]) + text.Substring(1);
    }
}
