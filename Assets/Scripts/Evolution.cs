using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// Evolutions during a run, all triggered by hand: once a team member meets the conditions of a form (level, IQ,
/// item, trade, Pokémon in the team, plus day or night, beauty, gender, chance, a known move, Attack against
/// Defence), the player presses Triangle (E) while controlling it. White silhouettes of the two forms alternate
/// while it stands still and cannot be hurt, then a shock wave pushes enemies back and hurts them.
/// IQ grows while the player controls the Pokémon, beauty while nothing hits it. Gender and chance are drawn at the
/// start of the run. Items and trades come from cards of the upgrade screen (Progression asks CardFor).
[RequireComponent(typeof(PlayerController))]
public class Evolution : MonoBehaviour
{
    const int Shedinja = 320, Nincada = 318; // Munja joins when Ningale evolves, if there is room
    const float AuraHeight = 0.65f; // lifts the aura sheet so its glowing oval sits under the Pokémon's feet
    const int AuraSortingOrder = 0; // behind the Pokémon (aura and shock wave)

    /// Run state of a team member that its evolution may depend on.
    class State
    {
        public bool male, randomA;
        public float iq, beauty;
        public int itemForm; // form chosen with an item card, 0 when none
        public bool traded;
        public bool announced; // the "can evolve" banner was shown for the current form
    }

    /// A card of the upgrade screen that gives an evolution item or a trade.
    public class Card
    {
        public int form;
        public bool trade;
        public string name, description;
    }

    readonly Dictionary<Combatant, State> states = new();
    PlayerController team;
    Progression progression;
    EnemySpawner spawner;
    static Material silhouette;

    // Evolution in progress.
    Combatant evolving;
    public bool Evolving => evolving != null;
    PmdAnimSet before, after;
    int into;
    float startedAt, nextSwap;
    bool showingAfter;
    Material originalMaterial;

    public bool Night => spawner != null && spawner.Night;

    void Awake()
    {
        team = GetComponent<PlayerController>();
        progression = GetComponent<Progression>();
    }

    void Start() => spawner = FindAnyObjectByType<EnemySpawner>();

    State StateOf(Combatant member)
    {
        if (states.TryGetValue(member, out var state)) return state;
        state = new State { male = Random.value < 0.5f, randomA = Random.value < 0.5f };
        member.Hurt += _ => state.beauty = Mathf.Max(0f, state.beauty - SaveData.Settings.beautyPerHit);
        return states[member] = state;
    }

    /// The form this member can turn into right now, or null.
    public PokemonStats ReadyForm(Combatant member)
    {
        if (member == null || member.Stats == null || !member.IsAlive || member == evolving) return null;
        foreach (var form in GameData.Evolutions(member.Stats.id))
            if (Meets(member, form)) return form;
        return null;
    }

    bool Meets(Combatant member, PokemonStats form)
    {
        var state = StateOf(member);
        var ev = form.evolution;
        bool method = ev.method switch
        {
            "level" => progression.Level >= ev.level,
            "iq" => state.iq >= ev.iq,
            "item" => state.itemForm == form.id,
            "party" => team.Members.Any(m => m != member && m.Stats != null && m.Stats.baseForm == (GameData.Pokemon(ev.party)?.baseForm ?? ev.party)),
            "trade" => state.traded,
            _ => false,
        };
        if (!method) return false;
        return ev.condition switch
        {
            "move" => member.Moves.Any(m => m != null && m.id == ev.move),
            "day" => !Night,
            "night" => Night,
            "beauty" => state.beauty >= SaveData.Settings.beautyThreshold,
            "male" => state.male,
            "female" => !state.male,
            "randomA" => state.randomA,
            "randomB" => !state.randomA,
            "atkAboveDef" => member.atk > member.def * (1f + SaveData.Settings.statTieMargin),
            "atkBelowDef" => member.atk * (1f + SaveData.Settings.statTieMargin) < member.def,
            "atkEqualsDef" => Mathf.Abs(member.atk - member.def) <= Mathf.Max(member.atk, member.def) * SaveData.Settings.statTieMargin,
            "trade" => state.traded,
            _ => true,
        };
    }

    /// An evolution card for this member's upgrade screen, or null: only when it can use one, and then only by
    /// chance. An item card picks the branch (Pierre Eau: Aquali); a trade card follows the item when the
    /// evolution also needs a trade, or stands alone for evolutions by trade.
    public Card CardFor(Combatant member, System.Random rng)
    {
        if (member.Stats == null) return null;
        var state = StateOf(member);
        var cards = new List<Card>();
        foreach (var form in GameData.Evolutions(member.Stats.id))
        {
            var ev = form.evolution;
            if (ev.method == "item" && state.itemForm == 0 && GenderFits(state, ev))
                cards.Add(new Card { form = form.id, name = ev.itemName, description = $"Objet d'évolution\n{member.Stats.name} pourra évoluer en {form.name}" });
            bool needsTrade = ev.method == "trade" || (ev.method == "item" && ev.condition == "trade" && state.itemForm == form.id);
            if (needsTrade && !state.traded)
                cards.Add(new Card { form = form.id, trade = true, name = "Échange", description = $"{member.Stats.name} pourra évoluer en {form.name}" });
        }
        if (cards.Count == 0 || rng.NextDouble() >= SaveData.Settings.evolutionCardChance) return null;
        return cards[rng.Next(cards.Count)];
    }

    static bool GenderFits(State state, EvolutionDef ev) =>
        ev.condition switch { "male" => state.male, "female" => !state.male, _ => true };

    public void Take(Combatant member, Card card)
    {
        var state = StateOf(member);
        if (card.trade) state.traded = true;
        else state.itemForm = card.form;
    }

    void Update()
    {
        if (Time.timeScale == 0f || team.GameOver || evolving != null) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        bool pressed = (pad != null && pad.buttonNorth.wasPressedThisFrame) || (kb != null && kb.eKey.wasPressedThisFrame);
        if (!pressed) return;
        var member = team.Controlled;
        if (ReadyForm(member) is { } form) Begin(member, form.id);
    }

    void FixedUpdate()
    {
        var settings = SaveData.Settings;
        foreach (var member in team.Members.ToList())
        {
            var state = StateOf(member);
            if (!member.IsAlive) continue;
            if (member == team.Controlled) state.iq += settings.iqPerSecond * Time.fixedDeltaTime;
            state.beauty += settings.beautyPerSecond * Time.fixedDeltaTime;
            var form = ReadyForm(member);
            if (form != null && !state.announced)
            {
                state.announced = true;
                progression.Announce($"{member.Stats.name} peut évoluer en {form.name} ! (Triangle)");
            }
        }
        if (evolving != null) Animate();
    }

    void Begin(Combatant member, int form)
    {
        evolving = member;
        into = form;
        before = PokemonRoster.Get(member.Stats.id);
        after = PokemonRoster.Get(form);
        startedAt = Time.time;
        nextSwap = Time.time;
        showingAfter = true; // the first swap shows the old form
        var renderer = member.GetComponent<SpriteRenderer>();
        originalMaterial = renderer.sharedMaterial;
        // Unity null check: the material is destroyed when a new run loads its scene, the static survives.
        if (silhouette == null) silhouette = new Material(Shader.Find("Pokescrap/Silhouette"));
        renderer.sharedMaterial = silhouette;
        member.BeginEvolution(SaveData.Settings.evolveTime);
        var aura = EffectPlayer.Spawn("evolution_aura", member.transform.position + Vector3.up * AuraHeight, Vector2.right,
            member.transform, SaveData.Settings.evolveTime);
        if (aura != null) aura.GetComponent<SpriteRenderer>().sortingOrder = AuraSortingOrder;
    }

    // Old and new silhouettes alternate faster and faster, then the new form appears with a shock wave.
    void Animate()
    {
        var settings = SaveData.Settings;
        var animator = evolving.GetComponent<PmdAnimator>();
        float t = (Time.time - startedAt) / settings.evolveTime;
        if (t >= 1f || !evolving.IsEvolving)
        {
            Finish(animator);
            return;
        }
        if (Time.time < nextSwap) return;
        showingAfter = !showingAfter;
        animator.SetAnimSet(showingAfter ? after : before);
        nextSwap = Time.time + Mathf.Lerp(0.4f, 0.06f, t);
    }

    void Finish(PmdAnimator animator)
    {
        var settings = SaveData.Settings;
        var member = evolving;
        evolving = null;
        string name = member.Stats.name;
        bool nincada = member.Stats.id == Nincada;
        member.Evolve(into);
        animator.SetAnimSet(after);
        member.GetComponent<SpriteRenderer>().sharedMaterial = originalMaterial;
        member.gameObject.name = member.Stats.name;
        var state = StateOf(member);
        state.itemForm = 0;
        state.traded = false;
        state.announced = false;
        progression.Announce($"{name} évolue en {member.Stats.name} !");

        Vector2 centre = member.transform.position;
        EffectPlayer.Spawn("evolution_eclat", centre, Vector2.right);
        var wave = EffectPlayer.Spawn("evolution_onde", centre, Vector2.right, size: 2f * settings.shockwaveRadius);
        if (wave != null) wave.GetComponent<SpriteRenderer>().sortingOrder = AuraSortingOrder; // on the ground
        else Shapes.Pulse(centre, settings.shockwaveRadius, Color.white);
        float damage = settings.shockwaveDamage + settings.shockwaveDamagePerLevel * progression.Level;
        foreach (var enemy in Object.FindObjectsByType<Combatant>())
        {
            if (enemy.team == member.team || !enemy.IsAlive) continue;
            Vector2 away = (Vector2)enemy.transform.position - centre;
            if (away.magnitude > settings.shockwaveRadius) continue;
            if (away == Vector2.zero) away = Random.insideUnitCircle;
            enemy.Knockback(away.normalized * settings.shockwaveKnockback / Combatant.KnockbackTime, Combatant.KnockbackTime);
            EffectPlayer.Spawn("evolution_impact", enemy.transform.position, Vector2.right);
            enemy.TakeFixedDamage(damage);
        }

        if (nincada && team.AddMember(Shedinja, member.transform.position + Vector3.right) is { } shedinja)
        {
            for (int level = 2; level <= progression.Level; level++)
                shedinja.ApplyGrowth(LevelData.Growth(Shedinja, level), level);
            progression.Announce($"{shedinja.Stats.name} rejoint l'équipe !");
        }
    }
}
