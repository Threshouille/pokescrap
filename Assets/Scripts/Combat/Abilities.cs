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

    [Serializable] class File { public Settings settings = new(); public AbilityEffect[] abilities = new AbilityEffect[0]; }

    static File data;
    static Dictionary<int, AbilityEffect> byId;

    /// While true (the AI bench), nobody draws an ability: only SetAbility gives one.
    public static bool Suspended;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() { data = null; byId = null; Suspended = false; }

    static void Load()
    {
        if (data != null) return;
        var asset = Resources.Load<TextAsset>("Data/ability_effects");
        data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
        byId = new Dictionary<int, AbilityEffect>();
        foreach (var a in data.abilities) byId[a.id] = a;
    }

    public static Settings Config { get { Load(); return data.settings; } }

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
        if (stats.hiddenAbility != 0 && rng.NextDouble() < hiddenChance) return 2;
        int count = stats.abilities != null ? stats.abilities.Length : 0;
        return count > 1 ? rng.Next(count) : 0;
    }

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
            if (under.Count > 0) parts.Add($"{WeatherName(w.weather)} : {string.Join(", ", under)}");
        }
        if (parts.Count == 0) return "Sans effet pour l'instant.";
        string text = string.Join(" ; ", parts) + ".";
        return char.ToUpper(text[0]) + text.Substring(1);
    }
}
