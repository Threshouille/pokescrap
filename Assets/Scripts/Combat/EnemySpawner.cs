using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    readonly System.Random rng = new();
    PlayerController team;
    Combatant boss;
    Phase phase;
    float phaseUntil, nextWaveAt;
    float runTime;
    int defeated;
    GUIStyle label, title, text;

    Stage Current => stages[CurrentStage];

    void OnEnable() => Combatant.AnyDied += OnDied;
    void OnDisable() => Combatant.AnyDied -= OnDied;

    void OnDied(Combatant combatant)
    {
        if (combatant.team == Team.Enemy) defeated++;
    }

    void Start()
    {
        team = FindFirstObjectByType<PlayerController>();
        BeginStage();
    }

    void BeginStage()
    {
        CurrentWave = 0;
        nextWaveAt = 0f;
        boss = null;
        if (Current.boss != null) boss = Spawn(Current.boss, SpawnPoint(), true);
        SpawnNextWave();
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
                if (CurrentStage >= stages.Length - 1) phase = Phase.Won;
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
        bool bossStage = Current.boss != null;
        if (bossStage && (boss == null || !boss.IsAlive))
        {
            // The boss is down: its escort goes with it.
            foreach (var escort in GetComponentsInChildren<Combatant>())
                if (escort.IsAlive) escort.TakeDamage(float.MaxValue);
            Enter(Phase.Cleared, stagePause);
            return;
        }

        int escortAlive = Combatant.CountAlive(Team.Enemy) - (bossStage ? 1 : 0);
        if (escortAlive > 0) return;
        if (CurrentWave >= Current.waves.Length)
        {
            if (!bossStage) Enter(Phase.Cleared, stagePause);
            return;
        }
        if (nextWaveAt <= 0f) nextWaveAt = Time.time + delayBetweenWaves;
        else if (Time.time >= nextWaveAt) SpawnNextWave();
    }

    void NextStage()
    {
        CurrentStage++;
        // Leftovers of the previous fight.
        foreach (var zone in FindObjectsByType<Zone>(FindObjectsSortMode.None)) Destroy(zone.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(shot.gameObject);

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
        foreach (var entry in Current.waves[CurrentWave].enemies)
            for (int i = 0; i < entry.count; i++)
                Spawn(entry.set, SpawnPoint(), false);
        CurrentWave++;
    }

    Combatant Spawn(PmdAnimSet set, Vector3 position, bool isBoss)
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
        combatant.Init(set.pokemonId, Team.Enemy);
        for (int level = 2; level <= Current.enemyLevel; level++)
            combatant.ApplyGrowth(LevelData.Growth(set.pokemonId, level), level);
        combatant.damageFactor = damageFactor;
        combatant.isBoss = isBoss;
        combatant.maxHp = combatant.hp = combatant.maxHp * (isBoss ? bossHealthFactor : healthFactor);
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
        if (!Victory) return; // after a defeat, the team object handles the restart
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if ((pad != null && pad.startButton.wasPressedThisFrame) || (kb != null && kb.enterKey.wasPressedThisFrame))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
            bool bossStage = Current.boss != null;
            string where = bossStage ? Current.name
                : nextWaveAt > 0f ? $"{Current.name} — Vague {CurrentWave}/{Current.waves.Length} terminée"
                : $"{Current.name} — Vague {CurrentWave}/{Current.waves.Length}";
            Fill(new Rect(cx - 75f * s, 4f * s, 150f * s, 12f * s), new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(cx - 75f * s, 4f * s, 150f * s, 12f * s), where, label);

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
        GUI.Label(new Rect(0, y + 70f * s, Screen.width, 14f * s), "Options (manette) ou Entrée pour recommencer", text);
    }

    static void Fill(Rect rect, Color color)
    {
        var previous = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = previous;
    }
}
