using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

/// Runs a whole run: a series of stages (arenas, then a boss), each made of enemy waves. Between two stages the
/// arena is rebuilt with the next décor and the team is put back on its feet. Also draws the stage indicator,
/// the boss health bar and the end-of-run screens.
[DefaultExecutionOrder(100)] // after DungeonGenerator.Start and the team's creation
public class EnemySpawner : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public PmdAnimSet set;
        public int count = 1;
    }

    [Serializable]
    public class Wave
    {
        public Entry[] enemies;
    }

    [Serializable]
    public class Stage
    {
        public string name = "Arène";
        public DungeonTileset tileset;
        [Tooltip("Enemies of this stage get the stats of this level")]
        public int enemyLevel = 1;
        public Wave[] waves;
        [Tooltip("Optional. The stage ends when the boss falls; the waves are its escort")]
        public PmdAnimSet boss;
    }

    enum Phase { Fighting, Cleared, FadingOut, FadingIn, Won }

    public DungeonGenerator generator;
    public Stage[] stages;
    [Tooltip("Pause between the end of a wave and the next one, in seconds")]
    public float delayBetweenWaves = 2f;
    [Tooltip("Enemies never appear closer than this to the controlled Pokémon")]
    public float minDistance = 6f;
    [Tooltip("Enemies deal this share of the normal damage")]
    public float damageFactor = 0.25f;
    [Tooltip("Multiplier on the enemies' HP")]
    public float healthFactor = 3f;
    public float bossHealthFactor = 8f;
    public float bossScale = 1.5f;
    [Tooltip("Seconds the \"stage cleared\" message stays before the fade")]
    public float stagePause = 2f;
    public float fadeTime = 0.5f;
    [Tooltip("Share of max HP given back to the Pokémon still standing between two stages")]
    public float healBetweenStages = 0.3f;

    public int CurrentStage { get; private set; } // 0-based
    public int CurrentWave { get; private set; } // 1-based, 0 before the first wave
    public bool Victory => phase == Phase.Won;
    public bool Fading => phase == Phase.FadingOut || phase == Phase.FadingIn;

    readonly System.Random rng = new();
    PlayerController team;
    Combatant boss;
    Phase phase;
    float phaseUntil, nextWaveAt;
    float runTime;
    float overAt; // unscaled time the end screen appeared
    const float EndInputDelay = 1f; // seconds before the end screen accepts a button (a dodge pressed as the run ends)
    int defeated;
    bool recorded;
    Recruitment recruitment;
    Loot loot;
    [Tooltip("Seconds the end of a stage waits for its chest to be opened (it is picked up anyway after)")]
    public float chestWait = 15f;
    GUIStyle label, title, text;

    Stage Current => stages[CurrentStage];
    /// Day and night alternate from one stage to the next (some evolutions need one or the other).
    public bool Night => CurrentStage % 2 == 1;
    SpriteRenderer nightShade;

    void Awake()
    {
        recruitment = gameObject.AddComponent<Recruitment>();
        loot = gameObject.AddComponent<Loot>();
        // An opened chest lets the stage end soon after.
        loot.ChestOpened += () => { if (phase == Phase.Cleared) phaseUntil = Mathf.Min(phaseUntil, Time.time + stagePause); };
    }

    void OnEnable() => Combatant.AnyDied += OnDied;
    void OnDisable() => Combatant.AnyDied -= OnDied;

    void OnDied(Combatant combatant)
    {
        if (combatant.team == Team.Enemy) defeated++;
    }

    void Start()
    {
        team = FindAnyObjectByType<PlayerController>();
        if (Run.Sandbox)
        {
            // Dev sandbox: terrain emitters, nothing recruited or recorded, the first stage's waves on a loop.
            gameObject.AddComponent<TerrainSandbox>();
            recruitment.enabled = false;
            sandboxWaves = TerrainSandbox.EnemyWaves();
        }
        BeginStage();
    }

    bool sandboxWaves;

    void BeginStage()
    {
        CurrentWave = 0;
        nextWaveAt = 0f;
        boss = null;
        ShowNight();
        if (Run.Sandbox && !sandboxWaves) return;
        if (Current.boss != null && !Run.Sandbox) boss = Spawn(Current.boss, SpawnPoint(), true);
        SpawnNextWave();
    }

    // A dark veil over the arena at night, under health bars and numbers.
    void ShowNight()
    {
        var cam = Camera.main;
        if (nightShade == null && cam != null)
        {
            var go = new GameObject("NightShade");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 1f);
            go.transform.localScale = new Vector3(200f, 200f, 1f);
            nightShade = go.AddComponent<SpriteRenderer>();
            nightShade.sprite = Shapes.Square;
            nightShade.sortingOrder = 2; // above the floor and the Pokémon
        }
        if (nightShade == null) return;
        var settings = SaveData.Settings;
        ColorUtility.TryParseHtmlString(settings.nightColor, out var color);
        color.a = settings.nightAlpha;
        nightShade.color = color;
        nightShade.gameObject.SetActive(Night);
    }

    void FixedUpdate()
    {
        switch (phase)
        {
            case Phase.Fighting:
                Fight();
                break;

            case Phase.Cleared:
                if (Time.time < phaseUntil) break;
                if (CurrentStage >= stages.Length - 1)
                {
                    loot.CollectAll();
                    phase = Phase.Won;
                }
                else Enter(Phase.FadingOut, fadeTime);
                break;

            case Phase.FadingOut:
                if (Time.time < phaseUntil) break;
                NextStage();
                Enter(Phase.FadingIn, fadeTime);
                break;

            case Phase.FadingIn:
                if (Time.time >= phaseUntil) phase = Phase.Fighting;
                break;
        }
    }

    void Enter(Phase next, float duration)
    {
        phase = next;
        phaseUntil = Time.time + duration;
    }

    void Fight()
    {
        if (team != null && team.GameOver) return;
        if (Run.Sandbox)
        {
            if (!sandboxWaves || Combatant.CountAlive(Team.Enemy) > 0) return;
            if (CurrentWave >= Current.waves.Length) CurrentWave = 0; // waves on a loop
            if (nextWaveAt <= 0f) nextWaveAt = Time.time + delayBetweenWaves;
            else if (Time.time >= nextWaveAt) SpawnNextWave();
            return;
        }
        bool bossStage = Current.boss != null;
        if (bossStage && (boss == null || !boss.IsAlive))
        {
            // The boss is down: its escort leaves with it (no experience, no recruit, not counted as defeated).
            foreach (var escort in GetComponentsInChildren<Combatant>())
                if (escort.IsAlive) Destroy(escort.gameObject);
            if (boss != null) loot.BossDrop(boss.transform.position);
            Clear();
            return;
        }

        int escortAlive = Combatant.CountAlive(Team.Enemy) - (bossStage ? 1 : 0);
        if (escortAlive > 0) return;
        if (CurrentWave >= Current.waves.Length)
        {
            if (!bossStage) Clear();
            return;
        }
        if (nextWaveAt <= 0f) nextWaveAt = Time.time + delayBetweenWaves;
        else if (Time.time >= nextWaveAt) SpawnNextWave();
    }

    // The stage is won: a chest near the team, and a wait for it to be opened.
    void Clear()
    {
        var leader = team != null ? team.Controlled : null;
        if (leader != null) loot.SpawnChest((Vector2)leader.transform.position + Vector2.right * 2f);
        Enter(Phase.Cleared, loot.ChestWaiting ? stagePause + chestWait : stagePause);
    }

    void NextStage()
    {
        loot.CollectAll(); // what was left on the ground
        CurrentStage++;
        // Leftovers of the previous fight.
        foreach (var zone in FindObjectsByType<Zone>()) Destroy(zone.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);

        if (Current.tileset != null) generator.tileset = Current.tileset;
        generator.Generate(); // also moves the team object to the centre of the new arena
        if (team != null) team.PrepareForNextArena(team.transform.position, healBetweenStages);
        BeginStage();
    }

    Vector3 SpawnPoint()
    {
        Vector2? around = team != null ? (Vector2)team.transform.position : null;
        return generator.RandomOpenPosition(rng, minDistance, around);
    }

    void SpawnNextWave()
    {
        nextWaveAt = 0f;
        if (Current.waves == null || CurrentWave >= Current.waves.Length) return;
        var sets = new List<PmdAnimSet>();
        foreach (var entry in Current.waves[CurrentWave].enemies)
            for (int i = 0; i < entry.count; i++)
                sets.Add(entry.set);
        for (int i = sets.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (sets[i], sets[j]) = (sets[j], sets[i]);
        }
        int size = Mathf.Max(1, SaveData.Settings.enemyGroupSize);
        for (int start = 0; start < sets.Count; start += size)
            SpawnGroup(sets.GetRange(start, Mathf.Min(size, sets.Count - start)));
        CurrentWave++;
    }

    // A group appears in one spot: one support (the first member whose moves allow it; when none can, a member is
    // swapped for a Pokémon of the stage, or else of the run, that can), the others melee or ranged at random,
    // each keeping its natural role when its moves do not allow the one it was given.
    void SpawnGroup(List<PmdAnimSet> sets)
    {
        var group = new EnemyGroup();
        if (sets.Count < 2) // a lone enemy keeps its natural role: nobody to support
        {
            var alone = Spawn(sets[0], SpawnPoint(), false);
            alone.GetComponent<EnemyAI>().group = group;
            group.members.Add(alone);
            return;
        }
        int support = sets.FindIndex(set => GameData.CanTakeRole(set.pokemonId, "support"));
        if (support < 0)
        {
            var healer = SupportCandidate();
            if (healer != null) sets[support = 0] = healer;
        }
        Vector3 centre = SpawnPoint();
        for (int i = 0; i < sets.Count; i++)
        {
            string role = i == support ? "support" : rng.Next(2) == 0 ? "melee" : "ranged";
            if (!GameData.CanTakeRole(sets[i].pokemonId, role)) role = null;
            Vector3 spot = centre + (Vector3)(GroupOffsets[i % GroupOffsets.Length]);
            if (!generator.WalkableAt(spot, 0.3f) || Obstacles.BlocksAt(spot, null)) spot = centre;
            var combatant = Spawn(sets[i], spot, false, role);
            combatant.GetComponent<EnemyAI>().group = group;
            group.members.Add(combatant);
        }
    }

    PmdAnimSet SupportCandidate()
    {
        var candidates = new List<PmdAnimSet>();
        foreach (var stage in new[] { Current }.Concat(stages))
        {
            foreach (var wave in stage.waves ?? new Wave[0])
                foreach (var entry in wave.enemies)
                    if (entry.set != null && GameData.CanTakeRole(entry.set.pokemonId, "support")) candidates.Add(entry.set);
            if (candidates.Count > 0) break;
        }
        return candidates.Count > 0 ? candidates[rng.Next(candidates.Count)] : null;
    }

    static readonly Vector2[] GroupOffsets = { Vector2.zero, new(1.2f, 0f), new(-0.6f, 1f), new(-0.6f, -1f), new(1.2f, 1.2f) };

    Combatant Spawn(PmdAnimSet set, Vector3 position, bool isBoss, string role = null)
    {
        var go = new GameObject(GameData.Pokemon(set.pokemonId)?.name ?? "Enemy");
        go.transform.SetParent(transform, false);
        go.transform.position = position;
        if (isBoss) go.transform.localScale = Vector3.one * bossScale;

        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        if (isBoss) body.mass = 20f; // not shoved around by the team
        go.AddComponent<CircleCollider2D>().radius = 0.3f;

        go.AddComponent<PmdAnimator>().SetAnimSet(set);
        var combatant = go.AddComponent<Combatant>();
        // Bosses keep their fixed moves; other enemies draw theirs from what they learn by level.
        combatant.Init(set.pokemonId, Team.Enemy, isBoss ? null : GameData.RandomMoves(set.pokemonId, SaveData.Settings.enemyMoveCount, rng, role));
        for (int level = 2; level <= Current.enemyLevel; level++)
            combatant.ApplyGrowth(LevelData.Growth(set.pokemonId, level), level);
        combatant.damageFactor = damageFactor;
        combatant.isBoss = isBoss;
        combatant.maxHp = combatant.hp = combatant.maxHp * (isBoss ? bossHealthFactor : healthFactor);
        if (!isBoss && !Run.Sandbox && rng.NextDouble() < SpecialItems.Loot.eliteChance) Loot.MakeElite(combatant);
        if (isBoss) go.AddComponent<BossAI>(); // numbered announcements instead of danger areas
        else
        {
            go.AddComponent<EnemyAI>();
            go.AddComponent<HealthBar>(); // the boss has its own bar at the top of the screen
        }
        return combatant;
    }

    void Update()
    {
        bool over = Victory || (team != null && team.GameOver);
        if (!over) runTime += Time.deltaTime; // stops during the upgrade screen, which freezes time
        if (!over) return;
        if (!recorded)
        {
            recorded = true;
            overAt = Time.unscaledTime;
            if (!Run.Sandbox) SaveData.RecordRun(Victory);
        }
        if (Time.unscaledTime < overAt + EndInputDelay) return;
        // Cross (or Enter / Options) plays again with the same team, Circle (or Escape) goes back to the menu.
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if ((pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame))
            || (kb != null && kb.enterKey.wasPressedThisFrame))
            Run.Restart();
        else if ((pad != null && pad.buttonEast.wasPressedThisFrame) || (kb != null && kb.escapeKey.wasPressedThisFrame))
            Run.ToMenu();
    }

    // Provisional display: stage and wave, boss health, fades and end-of-run screens.
    void OnGUI()
    {
        if (stages == null || stages.Length == 0) return;
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        title ??= new GUIStyle(label);
        text ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        label.fontSize = Mathf.RoundToInt(8f * s);
        title.fontSize = Mathf.RoundToInt(16f * s);
        text.fontSize = Mathf.RoundToInt(8f * s);
        float cx = Screen.width / 2f;

        bool defeat = team != null && team.GameOver;
        if (!Victory && !defeat)
        {
            bool bossStage = Current.boss != null && !Run.Sandbox;
            string where = Run.Sandbox ? "Terrain de test (dev) — Options : menu pause" : Night ? "Nuit — " : "Jour — ";
            if (!Run.Sandbox) where += bossStage ? Current.name
                : nextWaveAt > 0f ? $"{Current.name} — Vague {CurrentWave}/{Current.waves.Length} terminée"
                : $"{Current.name} — Vague {CurrentWave}/{Current.waves.Length}";
            Fill(new Rect(cx - 110f * s, 4f * s, 220f * s, 12f * s), new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(cx - 110f * s, 4f * s, 220f * s, 12f * s), where, label);

            if (bossStage && boss != null && boss.IsAlive)
            {
                var bar = new Rect(cx - 110f * s, 20f * s, 220f * s, 9f * s);
                Fill(new Rect(bar.x - s, bar.y - s, bar.width + 2f * s, bar.height + 2f * s), new Color(0f, 0f, 0f, 0.75f));
                Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(boss.hp / boss.maxHp), bar.height), new Color(0.85f, 0.2f, 0.2f));
                GUI.Label(bar, boss.Stats?.name, label);
            }

            if (phase == Phase.Cleared && CurrentStage < stages.Length - 1)
                GUI.Label(new Rect(0, Screen.height * 0.3f, Screen.width, 24f * s), $"{Current.name} terminée !", title);
        }

        // Fade to black around the change of arena.
        float alpha = phase == Phase.FadingOut ? 1f - Mathf.Clamp01((phaseUntil - Time.time) / fadeTime)
            : phase == Phase.FadingIn ? Mathf.Clamp01((phaseUntil - Time.time) / fadeTime) : 0f;
        if (alpha > 0f) Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, alpha));

        if (!Victory && !defeat) return;
        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.65f));
        var progression = team != null ? team.GetComponent<Progression>() : null;
        float y = Screen.height / 2f - 50f * s;
        GUI.Label(new Rect(0, y, Screen.width, 24f * s), Victory ? "Victoire !" : "Défaite", title);
        string reached = Victory ? "Boss vaincu" : $"Étape atteinte : {Current.name}";
        string summary = $"{reached}\nTemps : {Mathf.FloorToInt(runTime / 60f)}:{Mathf.FloorToInt(runTime % 60f):00}"
            + $"     Niveau : {(progression != null ? progression.Level : 1)}     Ennemis battus : {defeated}";
        GUI.Label(new Rect(0, y + 30f * s, Screen.width, 30f * s), summary, text);
        var recruits = recruitment != null ? recruitment.recruits : null;
        string joined = recruits != null && recruits.Count > 0
            ? "Recrutés : " + string.Join(", ", recruits.ConvertAll(id => GameData.Pokemon(id)?.name ?? "?"))
            : "Aucune recrue";
        GUI.Label(new Rect(0, y + 62f * s, Screen.width, 14f * s), joined, text);
        GUI.Label(new Rect(0, y + 84f * s, Screen.width, 14f * s), "Croix ou Entrée : rejouer     Rond ou Échap : menu", text);
    }

    static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
