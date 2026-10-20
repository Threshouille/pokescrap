using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// Mega evolution, a kind of ultimate: a gauge shared by the team fills as enemies are knocked out (a boss gives a
/// big share) and is kept from one stage to the next. Once full, the controlled Pokémon turns into its mega
/// evolution with L3 + R3 (F): it stands still and cannot be hurt while the silhouettes of both forms alternate,
/// then a shock wave. It keeps the mega's stats and types (Combatant.MegaEvolve) for megaTime seconds, then
/// goes back to its form; being knocked out ends it at once. Allies left to the AI never trigger it.
/// Who may: a form with a mega evolution in megas.json (with sprites), and for now only in devMode (mega stones,
/// equipped from the team menu, come next). In the test arena (Run.Sandbox) the gauge is always full.
[RequireComponent(typeof(PlayerController))]
public class MegaEvolution : MonoBehaviour
{
    PlayerController team;
    Progression progression;
    Evolution evolution;

    /// Share of the gauge filled, from 0 to 1.
    public float Gauge { get; private set; }
    public bool Full => Gauge >= 1f;

    class Active
    {
        public float endsAt;
    }

    readonly Dictionary<Combatant, Active> active = new();

    // Transformation in progress.
    Combatant transforming;
    PmdAnimSet before, after;
    float startedAt, nextSwap;
    bool showingAfter;
    Material originalMaterial;

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
        if (Full) progression.Announce("Jauge Méga pleine ! (L3 + R3)");
    }

    /// The mega evolution this member could turn into, gauge aside, or null.
    public MegaDef MegaOf(Combatant member)
    {
        if (member == null || member.Stats == null || !member.IsAlive || member.Mega != null || member.IsEvolving) return null;
        if (!SaveData.Settings.devMode) return null; // mega stones come next
        return GameData.MegasOf(member.Stats.id).FirstOrDefault();
    }

    /// Whether the controlled Pokémon can mega evolve right now.
    public bool Ready => Full && transforming == null && MegaOf(team.Controlled) != null;

    /// Seconds left before this member goes back to its form (0 when it is not a mega).
    public float TimeLeft(Combatant member) =>
        member != null && active.TryGetValue(member, out var a) ? Mathf.Max(0f, a.endsAt - Time.time) : 0f;

    void Update()
    {
        if (Time.timeScale == 0f || team.GameOver || !Ready) return;
        if (evolution != null && evolution.Evolving) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        bool pressed = (pad != null && pad.leftStickButton.isPressed && pad.rightStickButton.isPressed
                        && (pad.leftStickButton.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame))
                       || (kb != null && kb.fKey.wasPressedThisFrame);
        if (pressed) Begin(team.Controlled, MegaOf(team.Controlled));
    }

    // The stats change at once; the silhouettes are only the show.
    void Begin(Combatant member, MegaDef mega)
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
        var aura = EffectPlayer.Spawn("evolution_aura", member.transform.position + Vector3.up * Evolution.AuraHeight,
            Vector2.right, member.transform, settings.megaCastTime);
        if (aura != null) aura.GetComponent<SpriteRenderer>().sortingOrder = Evolution.AuraSortingOrder;
        progression.Announce($"{name} méga-évolue en {mega.name} !");
    }

    void FixedUpdate()
    {
        if (Run.Sandbox) Gauge = 1f;
        if (transforming != null) Animate();
        foreach (var (member, state) in active.ToList())
        {
            if (member == null)
            {
                active.Remove(member);
                continue;
            }
            if (!member.IsAlive || Time.time >= state.endsAt) Revert(member);
        }
    }

    void Animate()
    {
        var member = transforming;
        var animator = member.GetComponent<PmdAnimator>();
        float t = (Time.time - startedAt) / SaveData.Settings.megaCastTime;
        if (t >= 1f || !member.IsEvolving || !member.IsAlive)
        {
            Finish(member, animator);
            return;
        }
        if (Time.time < nextSwap) return;
        showingAfter = !showingAfter;
        animator.SetAnimSet(showingAfter ? after : before);
        nextSwap = Time.time + Mathf.Lerp(0.3f, 0.05f, t);
    }

    void Finish(Combatant member, PmdAnimator animator)
    {
        var settings = SaveData.Settings;
        transforming = null;
        animator.SetAnimSet(after);
        member.GetComponent<SpriteRenderer>().sharedMaterial = originalMaterial;
        member.gameObject.name = member.Stats.name;
        active[member] = new Active { endsAt = Time.time + settings.megaTime };
        if (evolution != null && member.IsAlive) evolution.Shockwave(member);
    }

    void Revert(Combatant member)
    {
        active.Remove(member);
        string name = member.Stats.name;
        member.RevertMega();
        member.GetComponent<PmdAnimator>().SetAnimSet(PokemonRoster.Get(member.Stats.id));
        member.gameObject.name = member.Stats.name;
        EffectPlayer.Spawn("evolution_eclat", member.transform.position, Vector2.right);
        if (member.IsAlive) progression.Announce($"{name} redevient {member.Stats.name}.");
    }
}
