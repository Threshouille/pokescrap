using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Room of the test arena (TerrainSandbox, AbilityRoom) that shows every ability with an effect (Abilities), one
/// after the other on a loop: on the left a Pokémon with the ability, on the right the same without, in the same
/// situation, and the banner compares what happened (health, statuses, distance walked, dodges…). The situation is
/// worked out from the ability's data (Plan), so a new ability gets its demo without code. ←/→ another ability,
/// ↑/↓ another family, R / Select again. The team stands aside and watches (its AIs off while here).
public class AbilityDemos : MonoBehaviour
{
    const float CaseTime = 7f, ActEvery = 1.2f, SideOffset = 4f;
    const float Floor = 0.05f; // the cast never falls below this share of its HP (LoseHp), and is refilled at half
    const int Fallback = 19; // Rattata: species of the cast when nobody has the ability

    enum Act { Hit, Count, Walk, Dodge, Idle, Heal, Arrive, Explain }

    /// How an ability is shown: who holds it, what is done, what to watch.
    class Plan
    {
        public string watch = "";
        public Act act = Act.Hit;
        public bool holderAttacks; // the ability is the attacker's (else the target's)
        public MoveDef move;
        public int attackerSpecies, targetSpecies;
        public float distance = 3f;
        public float targetHp; // 0: its real HP (refilled, never knocked out); else this much, and it may fall
        public float attackerHpShare = 1f, targetHpShare = 1f;
        public string preStatus; // given before the act (to the holder's role)
        public string weather;
        public bool holderBeside; // the holder stands by the target, on its team (Friend Guard)
        public int allyType; // an ally of this type stands by the attacker (Plus, Minus)
        public bool balloon; // the target holds an Air Balloon (Unburden)
        public bool respawn; // knocked-out targets come back (Moxie)
        public bool attackerItem; // the attacker holds an item (Pickpocket)
    }

    class Side
    {
        public Combatant attacker, target, extra;
        public Vector2 attackerAt, targetAt;
        public float attackerHp0, targetHp0;
        public float targetLost, attackerLost; // HP lost before a refill
        public int acts, dodges;
        public float walked;
    }

    readonly List<AbilityEffect> list = new();
    readonly List<Combatant> spawned = new();
    readonly List<AllyAI> paused = new();
    PlayerController team;
    EnemySpawner spawner;
    int index, family = -1;
    float endsAt;
    Plan plan;
    Side with, without;
    string error;

    /// Set by the AI bench: cases last this long (0: the normal time).
    public float caseTime;

    public int Count => list.Count;
    public int Index => index;
    public string Error => error;

    void Awake() => Build();

    void Build()
    {
        list.Clear();
        foreach (var e in Abilities.All)
            if (family < 0 || Abilities.FamilyOf(e) == Abilities.Families[family].key) list.Add(e);
        list.Sort((a, b) => string.CompareOrdinal(Abilities.FamilyOf(a) + NameOf(a), Abilities.FamilyOf(b) + NameOf(b)));
    }

    static string NameOf(AbilityEffect e) => AbilityLibrary.Get(e.id)?.name ?? e.id.ToString();

    IEnumerator Start()
    {
        team = FindAnyObjectByType<PlayerController>();
        spawner = GetComponent<EnemySpawner>();
        yield return null;
        if (team != null)
            foreach (var m in team.Members)
                if (m.GetComponent<AllyAI>() is { enabled: true } ai)
                {
                    ai.enabled = false;
                    paused.Add(ai);
                }
        Play(0);
    }

    void OnDestroy()
    {
        StopAllCoroutines();
        Clear();
        foreach (var ai in paused) if (ai != null) ai.enabled = true;
        paused.Clear();
        if (team != null) team.ScriptedMove = null;
        Climate.Clear();
    }

    void Update()
    {
        if (team == null || Time.timeScale == 0f || list.Count == 0) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        int step = 0, familyStep = 0;
        if ((pad != null && pad.dpad.left.wasPressedThisFrame) || (kb != null && kb.leftArrowKey.wasPressedThisFrame)) step = -1;
        if ((pad != null && pad.dpad.right.wasPressedThisFrame) || (kb != null && kb.rightArrowKey.wasPressedThisFrame)) step = 1;
        if ((pad != null && pad.dpad.up.wasPressedThisFrame) || (kb != null && kb.upArrowKey.wasPressedThisFrame)) familyStep = -1;
        if ((pad != null && pad.dpad.down.wasPressedThisFrame) || (kb != null && kb.downArrowKey.wasPressedThisFrame)) familyStep = 1;
        bool again = (pad != null && pad.selectButton.wasPressedThisFrame) || (kb != null && kb.rKey.wasPressedThisFrame);
        if (familyStep != 0)
        {
            int count = Abilities.Families.Count + 1;
            family = (family + 1 + familyStep + count) % count - 1;
            Build();
            Play(0);
        }
        else if (step != 0) Play((index + step + list.Count) % list.Count);
        else if (again) Play(index);
        else if (endsAt > 0f && Time.time >= endsAt) Play((index + 1) % list.Count);
    }

    /// Shows the ability at this place of the list.
    public void Play(int which)
    {
        if (list.Count == 0) return;
        index = which;
        endsAt = Time.time + (caseTime > 0f ? caseTime : CaseTime);
        StopAllCoroutines();
        if (team != null) team.ScriptedMove = Vector2.zero; // the player only watches
        StartCoroutine(Run(list[index]));
    }

    // ---- The banner ----

    public string Line()
    {
        if (list.Count == 0) return "Démo des talents : aucun talent";
        var e = list[index];
        string fam = family < 0 ? "toutes familles" : Abilities.Families[family].name;
        string head = $"Talents ({index + 1}/{list.Count}, {fam}) : {NameOf(e)} [{Abilities.FamilyName(Abilities.FamilyOf(e))}] — {Abilities.Describe(e)}";
        string body = plan == null ? "" : plan.act == Act.Explain ? $"     {plan.watch}"
            : $"     {plan.watch}     Avec : {Measure(with)}     Sans : {Measure(without)}";
        float left = Mathf.Max(0f, endsAt - Time.time);
        return $"{head}{body}     suivant dans {left:0} s     ←/→ : talent     ↑/↓ : famille     R ou Select : relancer     Escalier (à gauche) : retour";
    }

    string Measure(Side s)
    {
        if (s == null || s.target == null) return "—";
        var parts = new List<string>();
        switch (plan.act)
        {
            case Act.Walk: parts.Add($"{s.walked:0.0} cases parcourues"); break;
            case Act.Dodge: parts.Add($"{s.dodges} esquive{(s.dodges > 1 ? "s" : "")}"); break;
            case Act.Count: parts.Add($"{s.acts} attaque{(s.acts > 1 ? "s" : "")} lancée{(s.acts > 1 ? "s" : "")}"); break;
        }
        float lost = s.targetLost + s.targetHp0 - (s.target != null ? s.target.hp : 0f);
        if (s.target == null || !s.target.IsAlive) parts.Add("cible K.O.");
        else if (Mathf.Abs(lost) >= 0.5f) parts.Add(lost > 0f ? $"cible −{lost:0} PV" : $"cible +{-lost:0} PV");
        if (s.attacker != null)
        {
            float own = s.attackerLost + s.attackerHp0 - s.attacker.hp;
            if (Mathf.Abs(own) >= 0.5f) parts.Add(own > 0f ? $"attaquant −{own:0} PV" : $"attaquant +{-own:0} PV");
        }
        if (s.target != null && s.target.IsAlive && Vector2.Distance(s.target.transform.position, s.targetAt) > 0.5f && plan.act == Act.Hit)
            parts.Add($"cible poussée de {Vector2.Distance(s.target.transform.position, s.targetAt):0.0}");
        string effects = Effects(s.target);
        if (effects != "") parts.Add($"cible : {effects}");
        effects = Effects(s.attacker);
        if (effects != "") parts.Add($"attaquant : {effects}");
        return parts.Count > 0 ? string.Join(", ", parts) : "rien";
    }

    static readonly List<Combatant.EffectIcon> icons = new();

    static string Effects(Combatant c)
    {
        if (c == null || !c.IsAlive) return "";
        icons.Clear();
        c.GetEffects(icons);
        var names = new List<string>();
        foreach (var i in icons) names.Add(StatusLibrary.Get(i.key)?.name ?? EffectName(i.key));
        if (c.IsHurt) names.Add("étourdi");
        return string.Join(", ", names);
    }

    static string EffectName(string key) => key switch
    {
        "attack_up" => "attaque +", "attack_down" => "attaque −", "defense_up" => "défense +",
        "defense_down" => "défense −", "speed_up" => "vitesse +", "speed_down" => "vitesse −", _ => key,
    };

    // ---- Running a case ----

    IEnumerator Run(AbilityEffect e)
    {
        Clear();
        error = null;
        var arena = DungeonGenerator.Current;
        if (arena == null || team == null || spawner == null) yield break;
        // The team aside, healed, watching from below.
        team.PrepareForNextArena(Centre + new Vector2(0f, -9f), 1f);
        Climate.Clear();
        plan = null;
        try { plan = PlanFor(e); }
        catch (Exception ex) { error = ex.Message; Debug.LogException(ex); }
        if (plan == null || plan.act == Act.Explain) yield break;
        CameraFollow.Focus(Centre + new Vector2(0f, 1f), (caseTime > 0f ? caseTime : CaseTime) + 0.5f, 0.2f); // the cast, not the team
        if (!string.IsNullOrEmpty(plan.weather) && Climate.MoveOf(plan.weather) is var climate and > 0)
            Climate.Cast(GameData.Move(climate), null);
        with = Stage(-SideOffset, e.id);
        without = Stage(SideOffset, 0);
        yield return new WaitForFixedUpdate();
        foreach (var s in new[] { with, without }) Ready(s);
        float next = 0f;
        while (true)
        {
            yield return new WaitForFixedUpdate();
            foreach (var s in new[] { with, without })
                if (s != null)
                {
                    Tick(s, ref next);
                    Refill(s);
                }
            if (Time.time >= next) next = Time.time + (plan.act == Act.Count ? 0.3f : plan.act == Act.Dodge ? 0.35f : ActEvery);
        }
    }

    Combatant Holder(Side s) => plan.holderBeside ? s.extra : plan.holderAttacks ? s.attacker : s.target;

    Side Stage(float x, int ability)
    {
        var s = new Side();
        Vector2 middle = Centre + new Vector2(x, 1f);
        s.attackerAt = middle + Vector2.down * plan.distance / 2f;
        s.targetAt = middle + Vector2.up * plan.distance / 2f;
        bool attacks = plan.act is Act.Hit or Act.Count or Act.Arrive || plan.holderAttacks || plan.allyType != 0;
        if (attacks || plan.act == Act.Idle)
        {
            s.attacker = Cast(plan.attackerSpecies, s.attackerAt, Team.Player);
            if (plan.move != null) s.attacker.LearnMove(0, plan.move);
            s.attacker.hp = s.attacker.maxHp * plan.attackerHpShare;
        }
        s.target = Cast(plan.targetSpecies, s.targetAt, Team.Enemy);
        if (plan.targetHp > 0f)
        {
            s.target.maxHp = plan.targetHp;
            s.target.phaseFloorShare = 0f; // this case shows a knock-out
        }
        s.target.hp = s.target.maxHp * plan.targetHpShare;
        if (plan.holderBeside) s.extra = Cast(plan.targetSpecies, s.targetAt + Vector2.right * 1.2f, Team.Enemy);
        if (plan.allyType != 0 && FindSpecies(t => Array.IndexOf(t, plan.allyType) >= 0) is var mate and > 0)
            Cast(mate, s.attackerAt + Vector2.left * 1.2f, Team.Player);
        if (plan.balloon) s.target.Hold(HeldItems.Get("air_balloon"));
        if (plan.attackerItem && s.attacker != null) s.attacker.Hold(HeldItems.Get("leftovers"));
        var holder = Holder(s);
        if (holder != null) holder.SetAbility(ability);
        if (!string.IsNullOrEmpty(plan.preStatus) && holder != null) holder.AddStatus(plan.preStatus, 30f);
        return s;
    }

    void Ready(Side s)
    {
        if (s == null) return;
        s.targetHp0 = s.target != null ? s.target.hp : 0f;
        s.attackerHp0 = s.attacker != null ? s.attacker.hp : 0f;
        if (plan.act == Act.Arrive && s.attacker != null) s.attacker.Arrive();
    }

    void Tick(Side s, ref float next)
    {
        bool now = Time.time >= next;
        var dir = (s.targetAt - s.attackerAt).normalized;
        switch (plan.act)
        {
            case Act.Hit:
                if (!now || s.attacker == null || !s.attacker.IsAlive) break;
                if (plan.respawn && (s.target == null || !s.target.IsAlive)) Respawn(s);
                s.attacker.LearnMove(0, plan.move); // no recharge between two demo hits
                if (s.attacker.TryUseMove(0, dir)) s.acts++;
                break;
            case Act.Count:
                if (now && s.attacker != null && s.attacker.TryUseMove(0, dir)) s.acts++;
                break;
            case Act.Walk:
            {
                var walker = s.target;
                if (walker == null) break;
                walker.Drive(Vector2.up * walker.CurrentSpeed);
                s.walked = Vector2.Distance(walker.transform.position, s.targetAt);
                if (s.walked > 7f) // back to the start: the walk goes on
                {
                    Teleport(walker, s.targetAt);
                    s.walked = 0f;
                }
                break;
            }
            case Act.Dodge:
                if (now && s.target != null && s.target.TryDodge(s.dodges % 2 == 0 ? Vector2.right : Vector2.left)) s.dodges++;
                if (s.target != null) s.target.Drive(Vector2.zero);
                break;
            case Act.Heal:
                if (now && s.target != null && BagItems.Get("oran") is { } oran)
                {
                    s.target.hp = s.target.maxHp * 0.2f;
                    s.targetHp0 = s.target.hp;
                    s.target.Heal(s.target.maxHp * oran.value * s.target.BagHealMul);
                }
                break;
        }
    }

    // Back to its starting HP once below half of it, what it lost kept for the banner (real HP, real numbers).
    void Refill(Side s)
    {
        if (plan.act == Act.Heal) return;
        if (plan.targetHp <= 0f && s.target != null && s.target.IsAlive && s.target.hp < s.targetHp0 * 0.5f)
        {
            s.targetLost += s.targetHp0 - s.target.hp;
            s.target.hp = s.targetHp0;
        }
        if (s.attacker != null && s.attacker.IsAlive && s.attacker.hp < s.attackerHp0 * 0.5f)
        {
            s.attackerLost += s.attackerHp0 - s.attacker.hp;
            s.attacker.hp = s.attackerHp0;
        }
    }

    void Respawn(Side s)
    {
        if (s.target != null) Destroy(s.target.gameObject);
        s.target = Cast(plan.targetSpecies, s.targetAt, Team.Enemy);
        if (plan.targetHp > 0f)
        {
            s.target.maxHp = s.target.hp = plan.targetHp;
            s.target.phaseFloorShare = 0f;
        }
        s.targetHp0 = s.target.hp;
    }

    // A still Pokémon of the cast (no AI), on a team.
    Combatant Cast(int species, Vector2 at, Team side)
    {
        var c = spawner.SpawnFighter(species, new int[0], at, false, false, 20, 1f);
        if (c == null) throw new Exception($"pas de sprites pour {species}");
        if (c.GetComponent<EnemyAI>() is { } ai) ai.enabled = false;
        c.team = side;
        c.SetAbility(0);
        c.phaseFloorShare = Floor; // real HP, but no knock-out: refilled at half (Refill)
        spawned.Add(c);
        return c;
    }

    void Clear()
    {
        foreach (var c in spawned)
            if (c != null) Destroy(c.gameObject);
        spawned.Clear();
        foreach (var zone in FindObjectsByType<Zone>()) Destroy(zone.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        with = without = null;
    }

    static Vector2 Centre => DungeonGenerator.Current.CentreOf(DungeonGenerator.Current.Size / 2, DungeonGenerator.Current.Size / 2);

    static void Teleport(Combatant c, Vector2 at)
    {
        var body = c.GetComponent<Rigidbody2D>();
        if (body != null)
        {
            body.position = at;
            body.linearVelocity = Vector2.zero;
        }
        c.transform.position = at;
    }

    // ---- Working out the demo of an ability ----

    Plan PlanFor(AbilityEffect e)
    {
        int holder = SpeciesWith(e.id);
        var p = new Plan { attackerSpecies = Fallback, targetSpecies = holder };
        var any = FindMove(m => m.family == "projectile");
        var melee = FindMove(m => m.family == "melee" && m.lunge <= 0f);

        void Attacker() { p.holderAttacks = true; p.attackerSpecies = holder; p.targetSpecies = Fallback; }

        if (e.weathers.Length > 0)
        {
            var w = e.weathers[0];
            p.weather = w.weather;
            string name = Climate.WeatherName(w.weather);
            if (w.speedMul != 1f) { p.act = Act.Walk; p.watch = $"{name} : les deux marchent, comparer la distance."; }
            else if (w.types.Length > 0 || w.spAtkMul != 1f)
            {
                Attacker();
                p.move = w.types.Length > 0 ? TypedMove(w.types[0].type) : FindMove(m => m.family == "projectile" && m.category == "special");
                p.watch = $"{name} : les deux tirent, comparer les dégâts.";
            }
            else if (w.cure) { p.act = Act.Idle; p.preStatus = "burn"; p.watch = $"{name} : les deux sont brûlés, qui guérit ?"; }
            else if (w.statusImmune) { p.move = StatusMove("burn") ?? any; p.watch = $"{name} : brûlure tentée sur les deux."; }
            else { p.act = Act.Idle; p.targetHpShare = 0.5f; p.watch = $"{name} : comparer les PV (soin ou dégâts de la météo)."; }
            return p;
        }
        if (e.arrivalClimate != 0) { Attacker(); p.act = Act.Arrive; p.watch = "à son arrivée, la météo démarre (en haut à droite)."; return p; }
        if (!string.IsNullOrEmpty(e.arrivalModStat))
        {
            Attacker();
            p.act = Act.Idle;
            p.watch = "l'adversaire en face reçoit un malus en approchant.";
            return p;
        }
        if (e.absorbType != 0) { p.move = TypedMove(e.absorbType); p.watch = $"tirs {GameData.TypeName(e.absorbType)} sur les deux : absorbés à gauche."; return p; }
        if (e.takenTypes.Length > 0) { p.move = TypedMove(e.takenTypes[0].type); p.watch = $"tirs {GameData.TypeName(e.takenTypes[0].type)} : comparer les dégâts."; return p; }
        if (e.superEffectiveTakenMul != 1f) { p.move = SuperEffective(holder) ?? any; p.watch = "coups super efficaces : comparer les dégâts."; return p; }
        if (e.noChainBonus || e.meleeTakenMul != 1f)
        {
            p.move = (e.noChainBonus ? FindMove(m => m.family == "melee" && m.chainBonus > 0f) : null) ?? melee;
            p.distance = 1.2f;
            p.watch = "coups au corps à corps : comparer les dégâts.";
            return p;
        }
        if (e.fullHpTakenMul != 1f || e.takenAllMul != 1f && e.shotSizeMul == 1f) { p.move = any; p.watch = "comparer les dégâts reçus (le premier coup à PV pleins)."; return p; }
        if (e.endure) { p.move = StrongMove(); p.targetHp = 30f; p.watch = "un coup fatal à PV pleins : la gauche tient à 1 PV."; return p; }
        if (e.liquidOoze) { p.move = FindMove(m => m.drain > 0f && m.family != "zone") ?? any; p.attackerHpShare = 0.5f; p.watch = "l'attaquant draine : comparer ses PV."; return p; }
        if (e.resistedDealtMul != 1f)
        {
            Attacker();
            p.move = FindMove(m => m.family == "projectile" && TypeChart.Multiplier(m.type, Types(Fallback), out var eff) < 1f && eff == Efficacy.NotVery) ?? any;
            p.watch = "tirs peu efficaces : comparer les dégâts.";
            return p;
        }
        if (e.ignoreImmunityTypes.Length > 0)
        {
            Attacker();
            p.targetSpecies = FindSpecies(t => Array.IndexOf(t, 14) >= 0);
            p.move = TypedMove(e.ignoreImmunityTypes[0]);
            p.watch = "sur un Spectre : comparer les dégâts.";
            return p;
        }
        if (e.lowHpShare > 0f) { Attacker(); p.attackerHpShare = e.lowHpShare * 0.8f; p.move = e.lowHpType != 0 ? TypedMove(e.lowHpType) : any; p.watch = "les deux attaquants sont bas en PV : comparer les dégâts."; return p; }
        if (e.lowPowerMax > 0f) { Attacker(); p.move = FindMove(m => m.family == "projectile" && m.power <= e.lowPowerMax) ?? any; p.watch = "une attaque faible : comparer les dégâts."; return p; }
        if (e.stabMul > 0f) { Attacker(); p.move = TypedMove(Types(holder)[0]); p.watch = "une attaque de son type : comparer les dégâts."; return p; }
        if (e.effectMovesMul != 1f || e.effectChanceMul != 1f)
        {
            Attacker();
            p.move = FindMove(m => m.family == "projectile" && !string.IsNullOrEmpty(m.status) && m.statusChance > 0f && m.statusChance < 1f) ?? any;
            p.watch = "une attaque à effet : comparer dégâts et statuts.";
            return p;
        }
        if (e.rangeMul != 1f || e.shotSpeedMul != 1f || e.farShotMul != 1f || e.shotSizeMul != 1f)
        {
            Attacker();
            p.move = FindMove(m => m.family == "projectile" && m.range >= 5f && m.path != "arc") ?? any;
            p.distance = Mathf.Min(p.move.range * 0.95f, 8f);
            p.watch = "tirs de loin : regarder la taille, la vitesse, la portée et les dégâts.";
            return p;
        }
        if (e.meleeAreaMul != 1f || e.physicalMul != 1f)
        {
            Attacker();
            p.move = e.physicalMul != 1f ? FindMove(m => m.family == "projectile" && m.category == "physical") ?? melee : melee;
            p.distance = p.move.family == "melee" ? 1.2f : 3f;
            p.watch = "comparer les dégâts et la zone des coups.";
            return p;
        }
        if (e.noRecoil || e.recoilMoveMul != 1f) { Attacker(); p.move = FindMove(m => m.recoil > 0f) ?? any; p.distance = p.move.family == "melee" ? 1.2f : 3f; p.watch = "une attaque à recul : comparer les PV de l'attaquant et les dégâts."; return p; }
        if (e.koAttackStep > 1f) { Attacker(); p.move = any; p.targetHp = 20f; p.respawn = true; p.watch = "chaque K.O. renforce la gauche : les chiffres de dégâts montent."; return p; }
        if (e.statusImmune.Length > 0 || e.statusDurations.Length > 0)
        {
            string status = e.statusImmune.Length > 0 ? e.statusImmune[0] : e.statusDurations[0].status;
            p.move = StatusMove(status) ?? any;
            p.distance = p.move.family == "melee" ? 1.2f : 3f;
            p.watch = $"{StatusLibrary.Get(status)?.name} tenté sur les deux.";
            return p;
        }
        if (e.poisonHeals || e.magicGuard) { p.act = Act.Idle; p.preStatus = "poison"; p.targetHpShare = 0.5f; p.watch = "les deux sont empoisonnés : comparer les PV."; return p; }
        if (e.synchronize) { p.move = StatusMove("burn") ?? StatusMove("poison") ?? any; p.watch = "l'attaquant donne un statut : à gauche il le reçoit en retour."; return p; }
        if (e.shieldDust) { p.move = FindMove(m => m.family == "projectile" && !string.IsNullOrEmpty(m.status)) ?? any; p.watch = "une attaque à effet sur les deux : pas d'effet à gauche."; return p; }
        if (e.cureEvery > 0f || e.naturalCure > 0f) { p.act = Act.Idle; p.preStatus = "poison"; p.watch = "les deux sont empoisonnés : qui guérit ?"; return p; }
        if (e.statusedAttackMul != 1f) { Attacker(); p.preStatus = "burn"; p.move = FindMove(m => m.family == "projectile" && m.category == "physical") ?? melee; p.watch = "les deux attaquants sont brûlés : comparer les dégâts."; return p; }
        if (e.statusedSpeedMul != 1f) { p.act = Act.Walk; p.preStatus = "poison"; p.watch = "les deux sont empoisonnés et marchent : comparer la distance."; return p; }
        if (e.modImmune.Length > 0)
        {
            string stat = e.modImmune[0];
            p.move = FindMove(m => !string.IsNullOrEmpty(m.modStat) && m.modValue < 1f && (stat == "all" || m.modStat == stat) && m.family != "modifier") ?? any;
            p.distance = p.move.family == "melee" ? 1.2f : 3f;
            p.watch = "une attaque qui baisse une stat : pas de malus à gauche.";
            return p;
        }
        if (e.stunImmune) { p.move = melee; p.distance = 1.2f; p.watch = "coups au corps à corps : la droite est étourdie, pas la gauche."; return p; }
        if (e.contactStatuses.Length > 0 || e.contactShare > 0f || e.faintContactShare > 0f)
        {
            p.move = melee;
            p.distance = 1.2f;
            if (e.faintContactShare > 0f) { p.targetHp = 15f; p.respawn = true; }
            p.watch = "l'attaquant frappe au corps à corps : comparer ses PV et ses statuts.";
            return p;
        }
        if (!string.IsNullOrEmpty(e.hitStatus) || e.hitStunChance > 0f) { Attacker(); p.move = melee; p.distance = 1.2f; p.watch = "coups au corps à corps : statuts ou étourdissements de la cible."; return p; }
        if (e.allyGuardRadius > 0f) { p.holderBeside = true; p.move = any; p.watch = "la cible a un coéquipier à côté (à gauche, avec le talent) : comparer les dégâts."; return p; }
        if (e.allyTypeBoost != 0) { Attacker(); p.allyType = e.allyTypeBoost; p.move = any; p.watch = $"un allié {GameData.TypeName(e.allyTypeBoost)} à côté de l'attaquant : comparer les dégâts."; return p; }
        if (e.bagHealMul != 1f) { p.act = Act.Heal; p.watch = "une Baie Oran sur les deux : comparer le soin."; return p; }
        if (e.unburdenSpeed != 1f) { p.balloon = true; p.move = any; p.watch = "le Ballon de la cible éclate au premier coup : vitesse + à gauche."; return p; }
        if (e.dodgeCooldownMul != 1f || e.dodgeTimeMul != 1f || e.confusedDodgeMul != 1f)
        {
            p.act = Act.Dodge;
            if (e.confusedDodgeMul != 1f) p.preStatus = "confusion";
            p.watch = "les deux esquivent dès qu'ils peuvent : comparer le nombre et la longueur des esquives.";
            return p;
        }
        if (e.trapRadius > 0f)
        {
            Attacker();
            p.act = Act.Dodge;
            p.distance = Mathf.Min(2f, e.trapRadius * 0.7f);
            if (e.trapTypes.Length > 0) p.targetSpecies = FindSpecies(t => Array.IndexOf(t, e.trapTypes[0]) >= 0);
            else p.targetSpecies = FindSpecies(t => Array.IndexOf(t, 10) < 0);
            p.watch = "la cible essaie d'esquiver près du porteur.";
            return p;
        }
        if (e.attackerCooldownMul != 1f || e.curseChance > 0f) { p.act = Act.Count; p.move = any; p.watch = "l'attaquant tire dès qu'il peut : comparer le nombre d'attaques."; return p; }
        if (e.dampRadius > 0f && e.dampMoves.Length > 0) { p.act = Act.Count; p.move = GameData.Move(e.dampMoves[0]) ?? any; p.distance = 2f; p.watch = $"l'attaquant essaie {p.move.name} près de la cible."; return p; }
        if (e.nightmareRadius > 0f) { Attacker(); p.act = Act.Idle; p.distance = 2f; p.watch = "la cible dort près du porteur : comparer ses PV."; p.preStatus = null; return Sleepy(p); }
        if (e.noKnockback || e.knockbackMul != 1f) { p.move = FindMove(m => m.family == "melee" && m.knockback > 0f) ?? melee; p.distance = 1.2f; p.watch = "coups qui repoussent : comparer la poussée."; return p; }
        if (e.rageShare > 0f) { p.move = StrongMove(); p.targetHp = 60f; p.watch = "un gros coup : attaque + à gauche."; return p; }
        if (!string.IsNullOrEmpty(e.hitByStat) && e.hitByTypes.Length > 0) { p.move = TypedMove(e.hitByTypes[0]); p.watch = $"tirs {GameData.TypeName(e.hitByTypes[0])} : bonus à gauche."; return p; }
        if (e.loweredAttack != 1f)
        {
            p.move = FindMove(m => !string.IsNullOrEmpty(m.modStat) && m.modValue < 1f && m.family != "modifier") ?? any;
            p.distance = p.move.family == "melee" ? 1.2f : 3f;
            p.watch = "une attaque qui baisse une stat : attaque + à gauche.";
            return p;
        }
        if (e.stunSpeedMul != 1f) { p.move = melee; p.distance = 1.2f; p.watch = "étourdie, la gauche gagne de la vitesse."; return p; }
        if (e.speedBoostEvery > 0f) { Attacker(); p.act = Act.Idle; p.distance = 2f; p.watch = $"près d'un adversaire, la vitesse monte toutes les {e.speedBoostEvery:0} s (icône vitesse non affichée : voir le texte)."; return p; }
        // Lot 3.
        if (e.soundImmune) { p.move = FindMove(m => m.sound) ?? any; p.distance = p.move.family == "melee" ? 1.2f : 3f; p.watch = "une attaque sonore sur les deux."; return p; }
        if (e.powderImmune) { p.move = FindPowder() ?? any; p.weather = "sand"; p.watch = "une poudre sur les deux, sous une tempête de sable."; return p; }
        if (e.onlySuperEffective) { p.move = FindMove(m => m.family == "projectile" && TypeChart.Multiplier(m.type, Types(holder), out var eff) <= 1f && eff != Efficacy.Super) ?? any; p.watch = "un tir qui n'est pas super efficace : rien à gauche."; return p; }
        if (e.statusedTakenMul != 1f) { p.preStatus = "poison"; p.move = any; p.watch = "les deux cibles sont empoisonnées : comparer les dégâts."; return p; }
        if (e.zoneTakenMul != 1f) { p.move = FindZone() ?? any; p.watch = "une attaque de zone : comparer les dégâts."; return p; }
        if (e.firstHitMul != 1f || e.ignoreHitChance > 0f || e.superEffectiveDodge) { p.move = SuperEffective(holder) ?? any; p.watch = "tirs répétés : comparer les dégâts (premier coup, coups évités)."; return p; }
        if (e.typeFromHit) { p.move = TypedMove(2); p.watch = "touchée par du Feu, la gauche devient Feu (voir le bandeau)."; return p; }
        if (e.typeFromMove) { Attacker(); p.move = TypedMove(3); p.watch = "en tirant de l'Eau, la gauche devient Eau (voir le bandeau)."; return p; }
        if (e.weatherTypes.Length > 0) { p.act = Act.Idle; p.weather = e.weatherTypes[0].weather; p.watch = $"{Climate.WeatherName(p.weather)} : la gauche change de type (voir le bandeau)."; return p; }
        if (e.clearWeather) { Attacker(); p.act = Act.Arrive; p.weather = "rain"; p.watch = "il pleut ; à son arrivée, la météo s'arrête."; return p; }
        if (e.weatherProof) { p.act = Act.Idle; p.weather = "hail"; p.watch = "sous la grêle : comparer les PV."; return p; }
        if (e.rivalTypeMul != 1f) { Attacker(); p.targetSpecies = holder; p.move = any; p.watch = "contre un Pokémon de même type : comparer les dégâts."; return p; }
        if (e.punchMul != 1f) { Attacker(); p.move = FindMove(m => m.punch) ?? melee; p.distance = p.move.family == "melee" ? 1.2f : 3f; p.watch = "une attaque de poing : comparer les dégâts."; return p; }
        if (e.normalize || e.downloadMul != 1f || e.analyticMul != 1f || e.allDealtMul != 1f || e.slowStartTime > 0f
            || e.unaware || e.infiltrator || e.moldBreaker)
        { Attacker(); p.move = any; p.watch = "les deux tirent : comparer les dégâts."; return p; }
        if (e.extraHits > 0) { Attacker(); p.move = FindMove(m => m.burst > 1 || m.hits > 1) ?? any; p.distance = p.move.family == "melee" ? 1.2f : 3f; p.watch = "une attaque à plusieurs coups : compter les chiffres."; return p; }
        if (e.cooldownMul != 1f || e.afterAttackPause > 0f || e.prankster)
        {
            Attacker();
            p.act = Act.Count;
            p.move = e.prankster ? FindStatusOnly() ?? any : any;
            p.watch = "l'attaquant attaque dès qu'il peut : comparer le nombre d'attaques.";
            return p;
        }
        if (e.simple || e.contrary) { p.move = FindMove(m => !string.IsNullOrEmpty(m.modStat) && m.modValue < 1f && m.family != "modifier") ?? any; p.distance = p.move.family == "melee" ? 1.2f : 3f; p.watch = "une attaque qui baisse une stat : comparer les icônes."; return p; }
        if (e.magicBounce || e.statusChanceMul != 1f) { p.move = StatusMove("paralysis") ?? StatusMove("poison") ?? any; p.distance = p.move.family == "melee" ? 1.2f : 3f; p.watch = "une attaque à statut : comparer les statuts (cible et attaquant)."; return p; }
        if (e.statusBoosts.Length > 0) { Attacker(); p.preStatus = e.statusBoosts[0].status; p.move = FindMove(m => m.family == "projectile" && m.category == e.statusBoosts[0].category) ?? any; p.watch = "les deux attaquants ont le statut : comparer les dégâts."; return p; }
        if (!string.IsNullOrEmpty(e.hitByCategory)) { p.move = FindMove(m => m.family == "projectile" && m.category == e.hitByCategory) ?? any; p.watch = "touchée, la gauche change de stats (icônes)."; return p; }
        if (e.glueSlow != 1f) { Attacker(); p.move = melee; p.distance = 1.2f; p.watch = "coups au corps à corps : la cible de gauche ralentit (icône)."; return p; }
        if (e.pickpocket) { p.move = melee; p.distance = 1.2f; p.attackerItem = true; p.watch = "l'attaquant tient un objet : la cible de gauche le lui prend."; return p; }
        if (e.moodyEvery > 0f) { p.act = Act.Idle; p.watch = $"toutes les {e.moodyEvery:0} s, les stats de la gauche changent (icônes)."; return p; }
        if (e.offControlRegen > 0f) { p.act = Act.Idle; p.targetHpShare = 0.5f; p.watch = "seule et loin des adversaires, la gauche se soigne."; return p; }
        if (e.foesHealMul != 1f) { Attacker(); p.act = Act.Heal; p.distance = 2f; p.watch = "une Baie Oran sur les cibles, près du porteur à gauche : comparer le soin."; return p; }
        if (e.copyAbility) { p.act = Act.Idle; p.distance = 2f; Attacker(); p.watch = "la gauche copie le talent de l'adversaire en face (bandeau)."; return p; }
        p.act = Act.Explain;
        p.watch = "pas de démo pour ce talent : description seulement.";
        return p;
    }

    // Bad Dreams: the targets sleep (the holder attacks nothing).
    Plan Sleepy(Plan p)
    {
        StartCoroutine(PutToSleep());
        return p;
    }

    IEnumerator PutToSleep()
    {
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        foreach (var s in new[] { with, without })
            if (s?.target != null)
            {
                s.target.AddStatus("sleep", 30f);
                s.targetHp0 = s.target.hp;
            }
    }

    // ---- Picking the cast and the moves ----

    static int SpeciesWith(int ability)
    {
        foreach (var p in GameData.AllPokemon)
        {
            if (p.id >= 9000 || !PokemonRoster.Has(p.id)) continue;
            if ((p.abilities != null && Array.IndexOf(p.abilities, ability) >= 0) || p.hiddenAbility == ability) return p.id;
        }
        return Fallback;
    }

    static int FindSpecies(Func<int[], bool> fits)
    {
        foreach (var p in GameData.AllPokemon)
            if (p.id < 9000 && p.types != null && p.types.Length > 0 && PokemonRoster.Has(p.id) && fits(p.types)) return p.id;
        return Fallback;
    }

    static int[] Types(int species) => GameData.Pokemon(species)?.types ?? new[] { 1 };

    static MoveDef FindMove(Func<MoveDef, bool> fits)
    {
        MoveDef best = null;
        foreach (var m in GameData.AllMoves)
            if (m.power > 0 && !m.basic && m.family is "projectile" or "melee" && fits(m) && (best == null || m.power < best.power)) best = m;
        return best;
    }

    static MoveDef TypedMove(int type) =>
        FindMove(m => m.type == type && m.family == "projectile") ?? FindMove(m => m.type == type) ?? FindMove(m => m.family == "projectile");

    static MoveDef StrongMove()
    {
        MoveDef best = null;
        foreach (var m in GameData.AllMoves)
            if (m.family == "projectile" && m.power > 0 && (best == null || m.power > best.power)) best = m;
        return best;
    }

    static MoveDef FindPowder()
    {
        foreach (var m in GameData.AllMoves)
            if (m.powder) return m;
        return null;
    }

    static MoveDef FindZone()
    {
        foreach (var m in GameData.AllMoves)
            if (m.family == "zone" && m.power > 0) return m;
        return null;
    }

    static MoveDef FindStatusOnly()
    {
        foreach (var m in GameData.AllMoves)
            if (m.power <= 0 && m.family is "projectile" or "melee" && !string.IsNullOrEmpty(m.status)) return m;
        return null;
    }

    static MoveDef StatusMove(string status)
    {
        MoveDef best = null;
        foreach (var m in GameData.AllMoves)
            if (m.status == status && m.family is "projectile" or "melee" && (best == null || m.statusChance <= 0f || m.statusChance > best.statusChance)) best = m;
        return best;
    }

    static MoveDef SuperEffective(int species) =>
        FindMove(m => m.family == "projectile" && TypeChart.Multiplier(m.type, Types(species), out var eff) > 1f && eff == Efficacy.Super);
}
