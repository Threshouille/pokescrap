using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Room of the test arena (TerrainSandbox.EncounterRoom, white stairs) to watch how the encounters (Encounters)
/// fight in an obstacle room: one lot at a time (solitaire, meute, équipe, gang in turn), a combination (a lot and
/// the one that comes with it, combos), the elites (champion, squad), then a populated zone (a
/// whole wave of several lots spread over the room), drawn for a stage that goes up every few rounds, against the team: played by the player, or on autopilot to watch (PlayerController.AutoPilot; V or d-pad up switches, "watch" in the settings for the start). A round ends when
/// the encounter is down, the controlled Pokémon is low, or after roundTime seconds; the room is then drawn again with
/// other obstacles. The team is much tougher while here; R / Select skips to the next round. The solitaire is a
/// Pokémon whose line has a mega evolution, carrying its stone; the gang's three members carry an item each.
public class EncounterLab : MonoBehaviour
{
    [Serializable]
    class Settings
    {
        public float roundTime = 45f, teamHealth = 4f, distance = 8f;
        public int stageEvery = 3;
        public bool watch; // starts in watch mode (autopilot) rather than played
    }

    [Serializable] class SandboxFile { public Settings encounterLab = new(); }

    Settings settings;
    bool watching; // the team on autopilot; otherwise the player plays (V / d-pad up switches)
    PlayerController team;
    EnemySpawner spawner;
    TerrainSandbox sandbox;
    readonly System.Random rng = new();
    readonly List<Combatant> spawned = new();
    readonly Dictionary<Combatant, float> keptHealth = new();
    // The lots in turn, then a populated zone (a whole wave of the stage).
    static readonly string[] Modes = { "solo", "pack", "team", "gang", "combo", "champion", "squad", "zone" };
    const int ZoneWave = 2; // the wave whose budget a populated zone uses
    readonly List<Encounters.Encounter> lots = new();
    int round, stage;
    float endsAt;
    bool ending;

    void Awake()
    {
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        settings = asset != null ? JsonUtility.FromJson<SandboxFile>(asset.text).encounterLab ?? new Settings() : new Settings();
        team = FindAnyObjectByType<PlayerController>();
        spawner = GetComponent<EnemySpawner>();
        sandbox = GetComponent<TerrainSandbox>();
        if (sandbox != null) sandbox.Rebuilt += Round;
    }

    void Start()
    {
        watching = settings.watch;
        if (team != null) team.AutoPilot = watching;
        if (round == 0) Round(); // unless the room's Rebuilt event already started the first one
    }

    void OnDestroy()
    {
        if (sandbox != null) sandbox.Rebuilt -= Round;
        Clear();
        if (team != null) team.AutoPilot = false;
        foreach (var (c, maxHp) in keptHealth)
            if (c != null)
            {
                c.maxHp = maxHp;
                c.hp = Mathf.Min(c.hp, maxHp);
            }
        keptHealth.Clear();
    }

    void Clear()
    {
        foreach (var c in spawned)
            if (c != null) Destroy(c.gameObject);
        spawned.Clear();
    }

    // A new round: the team healed and tougher, the next encounter some way off.
    void Round()
    {
        Clear();
        if (team == null || spawner == null || DungeonGenerator.Current == null) return;
        foreach (var m in team.Members)
        {
            if (m == null) continue;
            if (!keptHealth.ContainsKey(m)) keptHealth[m] = m.maxHp;
            m.maxHp = keptHealth[m] * settings.teamHealth;
            m.hp = m.IsAlive ? m.maxHp : m.hp;
        }
        stage = Mathf.Min(3, round / Mathf.Max(1, settings.stageEvery));
        var arena = DungeonGenerator.Current;
        int decor = arena.tileset != null ? arena.tileset.romId : -1;
        int level = team.Controlled != null ? team.Controlled.level : 1;
        string mode = Modes[round % Modes.Length];
        round++;
        ending = false;
        endsAt = Time.time + settings.roundTime * (mode == "zone" ? 2f : 1f);
        lots.Clear();
        if (mode == "zone")
        {
            // A populated zone: a whole wave of the stage (several lots), each lot somewhere else in the room.
            lots.AddRange(Encounters.Wave(rng, stage, ZoneWave, decor));
            var taken = new List<Vector2> { team.transform.position };
            foreach (var lot in lots)
            {
                Vector3 at = arena.RandomOpenPosition(rng, settings.distance, team.transform.position);
                for (int k = 0; k < 10 && taken.Exists(t => Vector2.Distance(t, at) < settings.distance * 0.6f); k++)
                    at = arena.RandomOpenPosition(rng, settings.distance, team.transform.position);
                taken.Add(at);
                spawned.AddRange(spawner.SpawnEncounter(lot, at, level));
            }
            return;
        }
        Encounters.Encounter e = null;
        if (mode == "combo")
        {
            // A lot and the one that comes with it (combos of encounters.json), drawn among the combinations.
            var combos = Encounters.Combos;
            var combo = combos.Count > 0 ? combos[rng.Next(combos.Count)] : null;
            var lot = combo != null ? Encounters.CategoryOf(combo.lot) : null;
            e = lot != null ? Encounters.Make(rng, lot, stage, decor) : null;
            if (e != null) Encounters.Join(rng, e, combo, stage, decor);
        }
        else
        {
            var category = Encounters.CategoryOf(mode);
            e = category != null ? Encounters.Make(rng, category, stage, decor) : null;
        }
        if (e == null) return;
        // The solitaire: a Pokémon that can mega evolve (when any has sprites), with its stone.
        bool mega = false;
        if (mode == "solo")
        {
            var bases = SpecialItems.Stones.Select(st => GameData.Mega(st.mega))
                .Where(m => m != null && PokemonRoster.Has(m.id) && PokemonRoster.Has(m.@base)).Select(m => m.@base).Distinct().ToList();
            if (bases.Count > 0)
            {
                e.members[0].pokemonId = bases[rng.Next(bases.Count)];
                e.members[0].item = mega = true;
            }
        }
        lots.Add(e);
        Vector3 spot = arena.RandomOpenPosition(rng, settings.distance, team.transform.position);
        spawned.AddRange(spawner.SpawnEncounter(e, spot, level));
        if (e.category.elite) spawner.AnnounceElite(e);
        if (mega && spawned.Count > 0 && spawned[0].GetComponent<EnemyUltimate>() is { } ultimate) ultimate.preferMega = true;
    }

    void Update()
    {
        if (team == null || Time.timeScale == 0f) return;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if ((kb != null && kb.vKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame))
        {
            watching = !watching;
            team.AutoPilot = watching;
        }
    }

    void FixedUpdate()
    {
        if (ending || sandbox == null || team == null) return;
        var leader = team.Controlled;
        bool down = spawned.Count > 0 && spawned.TrueForAll(c => c == null || !c.IsAlive);
        bool low = leader != null && leader.hp < leader.maxHp * 0.25f;
        if (!down && !low && Time.time < endsAt) return;
        ending = true;
        sandbox.Redraw();
    }

    /// The banner's line: the encounter, the room's obstacles, the time left.
    public string Line(List<string> obstacles)
    {
        string what = lots.Count == 0 ? "aucun Pokémon pour cette rencontre"
            : lots.Count == 1 ? lots[0].Describe() : $"Zone peuplée ({lots.Count} lots) : " + string.Join("  |  ", lots.Select(l => l.Describe()));
        string room = obstacles != null && obstacles.Count > 0 ? string.Join(", ", obstacles) : "sans obstacle";
        return $"Observation {round} (étape {stage + 1}) : {what}     Salle : {room}     fin dans {Mathf.Max(0f, endsAt - Time.time):0} s"
               + $"     {(watching ? "Tu regardes (pilote automatique)" : "Tu joues")} — V ou croix haut : {(watching ? "jouer" : "regarder")}"
               + "     R ou Select : suivante     Escalier : retour";
    }
}
