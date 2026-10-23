using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// The ultimates (GDD, "Objets spéciaux"): a gauge shared by the team fills as enemies are knocked out (a boss gives a
/// big share) and is kept from one stage to the next. Once full, the controlled Pokémon unleashes the ultimate of its
/// active special item (Combatant.Special) with L3 + R3 (F), and the gauge empties; allies left to the AI never do.
/// - Mega stone: it turns into its mega evolution (the stone's line, once at the right form; Rayquaza needs no stone but
///   a move): still and unharmed while the silhouettes of both forms alternate, then a shock wave; the mega's stats and
///   types (Combatant.MegaEvolve) for megaTime seconds.
/// - Z crystal: a pose (still and unharmed, zPose seconds), then every opponent on screen is struck, one after the
///   other from the nearest: the damage of its strongest move of the crystal's type × ZPower(level), and the crystal's
///   own effect (SpecialItems.ZHit: status, penalty, push, terrain, drain, team bonus). Only when an opponent is in view.
/// - Tera shard: it takes the shard's type for TeraTime(level) seconds (Combatant.Terastallize).
/// Being knocked out ends a mega or a Terastal phase at once. In the test arena (Run.Sandbox) the gauge is always
/// full; in devMode a member without an item may still mega evolve, for tests.
[RequireComponent(typeof(PlayerController))]
public class Ultimates : MonoBehaviour
{
    public const float TeraPose = 0.6f;

    PlayerController team;
    Progression progression;
    Evolution evolution;

    /// Share of the gauge filled, from 0 to 1.
    public float Gauge { get; private set; }
    public bool Full => Gauge >= 1f;

    class Active
    {
        public SpecialItems.Family kind;
        public float endsAt, length;
    }

    readonly Dictionary<Combatant, Active> active = new();

    // Mega transformation in progress.
    Combatant transforming;
    PmdAnimSet before, after;
    float startedAt, nextSwap;
    bool showingAfter;
    Material originalMaterial;

    /// The Z demo room of the test arena: a member that knows no move of the crystal's type borrows the strongest
    /// one the game has.
    public static bool DemoMoves;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => DemoMoves = false;

    // Z move waiting for the end of its pose.
    Combatant posing;
    MoveDef zMove;

    void Awake()
    {
        team = GetComponent<PlayerController>();
        progression = GetComponent<Progression>();
        evolution = GetComponent<Evolution>();
    }

    void OnEnable() => Combatant.AnyDied += OnDied;
    void OnDisable() => Combatant.AnyDied -= OnDied;

    void OnDied(Combatant combatant)
    {
        if (combatant.team != Team.Enemy || Full) return;
        var settings = SaveData.Settings;
        bool boss = combatant.GetComponent<BossAI>() != null;
        Gauge = Mathf.Min(1f, Gauge + (boss ? settings.megaBossFill : 1f / Mathf.Max(1f, settings.megaKillsToFill)));
        if (Full) progression.Announce("Jauge d'ultime pleine ! (L3 + R3)");
    }

    /// The family of the ultimate this member could unleash now, gauge aside (None when it cannot).
    public SpecialItems.Family KindOf(Combatant member)
    {
        if (member == null || member.Stats == null || !member.IsAlive || member.IsEvolving || active.ContainsKey(member)) return SpecialItems.Family.None;
        var e = member.Special;
        if (e == null) return SaveData.Settings.devMode && MegaOf(member) != null ? SpecialItems.Family.Mega : SpecialItems.Family.None;
        return e.family switch
        {
            SpecialItems.Family.Mega => MegaOf(member) != null ? e.family : SpecialItems.Family.None,
            SpecialItems.Family.Z => BaseMoveFor(member, e.type) != null ? e.family : SpecialItems.Family.None,
            SpecialItems.Family.Tera => member.TeraType < 0 ? e.family : SpecialItems.Family.None,
            _ => SpecialItems.Family.None,
        };
    }

    /// The mega evolution this member turns into, or null: its stone's mega when it stands at that mega's base form
    /// (Rayquaza: when it knows the stand-in move); in devMode without an item, its form's first mega.
    public MegaDef MegaOf(Combatant member)
    {
        if (member == null || member.Stats == null || member.Mega != null) return null;
        var e = member.Special;
        if (e == null || e.family != SpecialItems.Family.Mega)
            return SaveData.Settings.devMode && e == null ? GameData.MegasOf(member.Stats.id).FirstOrDefault() : null;
        if (e.stone != null)
        {
            var m = GameData.Mega(e.stone.mega);
            return m != null && m.@base == member.Stats.id && PokemonRoster.Has(m.id) ? m : null;
        }
        var r = GameData.Mega(SpecialItems.RayquazaMega);
        bool knows = member.Moves.Any(mv => mv != null && mv.id == SpecialItems.RayquazaMove);
        return r != null && r.@base == member.Stats.id && knows ? r : null;
    }

    /// The move a Z crystal of that type powers up: the strongest damaging one of the type the member knows.
    /// A dev character, which knows few of its moves, takes the strongest of all those it could learn.
    public static MoveDef BaseMoveFor(Combatant member, int type)
    {
        IEnumerable<MoveDef> moves = GameData.IsDev(member.OriginId) ? GameData.MovePool(member.OriginId) : member.Moves;
        var best = moves.Where(m => m != null && SpecialItems.KnowsMoveOfType(new[] { m }, type)).OrderByDescending(m => m.power).FirstOrDefault();
        if (best != null || !DemoMoves) return best;
        return GameData.AllMoves.Where(m => SpecialItems.KnowsMoveOfType(new[] { m }, type)).OrderByDescending(m => m.power).FirstOrDefault();
    }

    /// A Z move waits for an opponent in view (not in the test arena, often empty: it strikes the air there).
    static bool FoesInView(Combatant member) => Run.Sandbox || VisibleFoes(member).Count > 0;

    /// The opponents a Z move strikes: alive and within the camera's view (and within "range" tiles when > 0),
    /// nearest first.
    public static List<Combatant> VisibleFoes(Combatant member, float range = 0f)
    {
        var cam = Camera.main;
        var result = new List<Combatant>();
        if (cam == null || member == null) return result;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        foreach (var other in Combatant.All)
        {
            if (other == null || !other.IsAlive || other.team == member.team) continue;
            Vector2 d = (Vector2)other.transform.position - c;
            if (Mathf.Abs(d.x) > w || Mathf.Abs(d.y) > h) continue;
            if (range > 0f && Vector2.Distance(other.transform.position, member.transform.position) > range) continue;
            result.Add(other);
        }
        Vector2 from = member.transform.position;
        result.Sort((a, b) => Vector2.Distance(from, a.transform.position).CompareTo(Vector2.Distance(from, b.transform.position)));
        return result;
    }

    /// Whether the controlled Pokémon can unleash its ultimate right now.
    public bool Ready => Full && transforming == null && posing == null && KindOf(team.Controlled) switch
    {
        SpecialItems.Family.None => false,
        SpecialItems.Family.Z => FoesInView(team.Controlled),
        _ => true,
    };

    /// Seconds left of this member's mega or Terastal phase (0 when none).
    public float TimeLeft(Combatant member) =>
        member != null && active.TryGetValue(member, out var a) ? Mathf.Max(0f, a.endsAt - Time.time) : 0f;

    /// Length of this member's current mega or Terastal phase (for the HUD's gauge).
    public float Length(Combatant member) => member != null && active.TryGetValue(member, out var a) ? a.length : 1f;

    /// Short name of the ultimate this member carries, for the HUD ("" when none).
    public static string Label(Combatant member)
    {
        var e = member?.Special;
        if (e == null) return SaveData.Settings.devMode && member != null && member.Stats != null && GameData.MegasOf(member.Stats.id).Count > 0 ? "Méga" : "";
        return e.family switch { SpecialItems.Family.Mega => "Méga", SpecialItems.Family.Z => "Z", SpecialItems.Family.Tera => "Téra", _ => "" };
    }

    void Update()
    {
        if (Time.timeScale == 0f || team.GameOver) return;
        if (evolution != null && evolution.Evolving) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        bool pressed = (pad != null && pad.leftStickButton.isPressed && pad.rightStickButton.isPressed
                        && (pad.leftStickButton.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame))
                       || (kb != null && kb.fKey.wasPressedThisFrame);
        if (!pressed) return;
        if (Ready) Trigger(team.Controlled);
        else if (Full && transforming == null && posing == null && KindOf(team.Controlled) == SpecialItems.Family.Z)
            progression.Announce("Capacité Z : aucun ennemi en vue.");
    }

    /// Unleashes the member's ultimate (input, tests). False when it cannot.
    public bool Trigger(Combatant member)
    {
        var kind = KindOf(member);
        if (kind == SpecialItems.Family.Z && !FoesInView(member)) return false;
        switch (kind)
        {
            case SpecialItems.Family.Mega: BeginMega(member, MegaOf(member)); return true;
            case SpecialItems.Family.Z: BeginZ(member); return true;
            case SpecialItems.Family.Tera: BeginTera(member); return true;
            default: return false;
        }
    }

    // ---- Mega ----

    // The stats change at once; the silhouettes are only the show.
    void BeginMega(Combatant member, MegaDef mega)
    {
        var settings = SaveData.Settings;
        Gauge = 0f;
        transforming = member;
        before = PokemonRoster.Get(member.Stats.id);
        after = PokemonRoster.Get(mega.id);
        string name = member.Stats.name;
        member.MegaEvolve(mega);
        member.BeginEvolution(settings.megaCastTime);
        startedAt = Time.time;
        nextSwap = Time.time;
        showingAfter = true; // the first swap shows the old form
        var renderer = member.GetComponent<SpriteRenderer>();
        originalMaterial = renderer.sharedMaterial;
        renderer.sharedMaterial = Evolution.Silhouette;
        Aura(member, settings.megaCastTime, Color.white);
        progression.Announce($"{name} méga-évolue en {mega.name} !");
    }

    void AnimateMega()
    {
        var member = transforming;
        var animator = member.GetComponent<PmdAnimator>();
        float t = (Time.time - startedAt) / SaveData.Settings.megaCastTime;
        if (t >= 1f || !member.IsEvolving || !member.IsAlive)
        {
            transforming = null;
            animator.SetAnimSet(after);
            member.GetComponent<SpriteRenderer>().sharedMaterial = originalMaterial;
            member.gameObject.name = member.Stats.name;
            float length = SaveData.Settings.megaTime;
            active[member] = new Active { kind = SpecialItems.Family.Mega, endsAt = Time.time + length, length = length };
            if (evolution != null && member.IsAlive) evolution.Shockwave(member);
            return;
        }
        if (Time.time < nextSwap) return;
        showingAfter = !showingAfter;
        animator.SetAnimSet(showingAfter ? after : before);
        nextSwap = Time.time + Mathf.Lerp(0.3f, 0.05f, t);
    }

    // ---- Z move ----

    void BeginZ(Combatant member)
    {
        var e = member.Special;
        var baseMove = BaseMoveFor(member, e.type);
        Gauge = 0f;
        zMove = ZVersion(baseMove, e.type, e.level);
        posing = member;
        member.BeginEvolution(SpecialItems.ZPose);
        Aura(member, SpecialItems.ZPose, GameData.TypeColor(e.type));
        progression.Announce($"{member.Stats.name} : {zMove.name} !");
    }

    /// The Z version of a move: power × ZPower(level), the crystal's name, and its effect on each opponent struck.
    public static MoveDef ZVersion(MoveDef move, int type, int level)
    {
        var z = JsonUtility.FromJson<MoveDef>(JsonUtility.ToJson(move));
        var crystal = SpecialItems.Crystal(type);
        var hit = crystal?.hit ?? new SpecialItems.ZHit();
        z.name = crystal?.move ?? $"{move.name} Z";
        z.power = Mathf.RoundToInt(move.power * SpecialItems.ZPower(level) * hit.bonus);
        z.basic = false;
        z.damageScale = 1f;
        z.status = hit.status;
        z.statusChance = 1f;
        z.statusDuration = hit.statusDuration;
        z.modStat = hit.modStat;
        z.modValue = hit.modValue;
        z.modDuration = hit.modDuration;
        z.statChance = 1f;
        z.drain = hit.drain;
        z.recoil = 0f;
        z.selfModStat = null;
        return z;
    }

    // At the end of the pose: the strike, from the nearest opponent on screen to the farthest.
    void FireZ()
    {
        var member = posing;
        if (member.IsEvolving && member.IsAlive) return;
        posing = null;
        if (!member.IsAlive) return;
        StartCoroutine(StrikeZ(member, zMove, member.Special.type, member.Special.level));
    }

    const float ZCascade = 0.06f; // seconds between two opponents struck
    const float ZPushTime = 0.5f; // seconds a Z push lasts (ZHit.knockback: tiles)

    /// The Z strike itself (the player's, or an enemy's: EnemyUltimate, with a range, and no more than maxShare of each
    /// target's maximum health when > 0).
    public static System.Collections.IEnumerator StrikeZ(Combatant member, MoveDef move, int type, int level, float range = 0f, float maxShare = 0f)
    {
        var hit = SpecialItems.Crystal(type)?.hit ?? new SpecialItems.ZHit();
        float scale = SpecialItems.ZScale(level);
        Color colour = GameData.TypeColor(type);
        var flash = EffectPlayer.Spawn(SpecialItems.ZFlash, member.transform.position, Vector2.right, null, 0f, 8f * scale);
        if (flash != null) flash.GetComponent<SpriteRenderer>().color = colour;
        foreach (var foe in VisibleFoes(member, range))
        {
            if (foe == null || !foe.IsAlive) continue;
            Vector2 at = foe.transform.position;
            var sheet = EffectLibrary.Get(hit.effect) != null ? hit.effect : move.impactEffect;
            EffectPlayer.Spawn(sheet, at, Vector2.right, null, 0f, 1.5f * scale);
            float raw = member != null ? member.DamageOf(move) : 0f;
            if (maxShare > 0f && member != null)
            {
                float estimate = member.EstimateHit(move, foe);
                if (estimate > foe.maxHp * maxShare) raw *= foe.maxHp * maxShare / estimate;
            }
            if (member != null && foe.TakeHit(raw, move, out float dealt))
            {
                foe.ApplyEffects(move, true, member);
                member.HitLanded(move, dealt);
                Vector2 away = at - (Vector2)member.transform.position;
                if (hit.knockback != 0f && away != Vector2.zero) foe.Knockback(away.normalized * hit.knockback / ZPushTime, ZPushTime);
                foe.Stun(hit.stun);
            }
            if (!string.IsNullOrEmpty(hit.terrain)) Terrain.PaintArea(hit.terrain, at, hit.terrainRadius * scale, hit.terrainDuration, member, spareTeam: true);
            yield return new WaitForSeconds(ZCascade);
        }
        if (string.IsNullOrEmpty(hit.teamModStat) || member == null) yield break;
        foreach (var ally in Combatant.All)
            if (ally != null && ally.IsAlive && ally.team == member.team)
                ally.AddModifier(hit.teamModStat, hit.teamModValue, hit.teamModDuration);
    }

    // ---- Tera ----

    void BeginTera(Combatant member)
    {
        var e = member.Special;
        Gauge = 0f;
        member.Terastallize(e.type);
        member.BeginEvolution(TeraPose);
        Aura(member, TeraPose, GameData.TypeColor(e.type));
        float length = SpecialItems.TeraTime(e.level);
        active[member] = new Active { kind = SpecialItems.Family.Tera, endsAt = Time.time + length, length = length };
        if (EffectLibrary.Get("tera_eclat") != null) EffectPlayer.Spawn("tera_eclat", member.transform.position, Vector2.right);
        else EffectPlayer.Spawn("evolution_eclat", member.transform.position, Vector2.right);
        progression.Announce($"{member.Stats.name} se téracristallise : type {GameData.TypeName(e.type)} !");
    }

    // ---- Shared ----

    public static void Aura(Combatant member, float time, Color colour)
    {
        var aura = EffectPlayer.Spawn("evolution_aura", member.transform.position + Vector3.up * Evolution.AuraHeight,
            Vector2.right, member.transform, time);
        if (aura == null) return;
        var renderer = aura.GetComponent<SpriteRenderer>();
        renderer.sortingOrder = Evolution.AuraSortingOrder;
        renderer.color = colour;
    }

    void FixedUpdate()
    {
        if (Run.Sandbox) Gauge = 1f;
        if (transforming != null) AnimateMega();
        if (posing != null) FireZ();
        foreach (var (member, state) in active.ToList())
        {
            if (member == null)
            {
                active.Remove(member);
                continue;
            }
            if (!member.IsAlive || Time.time >= state.endsAt) End(member, state.kind);
        }
    }

    void End(Combatant member, SpecialItems.Family kind)
    {
        active.Remove(member);
        if (kind == SpecialItems.Family.Tera)
        {
            member.RevertTera();
            EffectPlayer.Spawn("evolution_eclat", member.transform.position, Vector2.right);
            if (member.IsAlive) progression.Announce($"{member.Stats.name} perd son Téracristal.");
            return;
        }
        string name = member.Stats.name;
        member.RevertMega();
        member.GetComponent<PmdAnimator>().SetAnimSet(PokemonRoster.Get(member.Stats.id));
        member.gameObject.name = member.Stats.name;
        EffectPlayer.Spawn("evolution_eclat", member.transform.position, Vector2.right);
        if (member.IsAlive) progression.Announce($"{name} redevient {member.Stats.name}.");
    }
}
