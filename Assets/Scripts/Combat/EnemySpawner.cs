using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// Runs the arena: sends the enemy waves one after the other, and declares victory once the last one is beaten.
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

    public DungeonGenerator generator;
    public Wave[] waves;
    [Tooltip("Pause between the end of a wave and the next one, in seconds")]
    public float delayBetweenWaves = 2f;
    [Tooltip("Enemies never appear closer than this to the controlled Pokémon")]
    public float minDistance = 6f;
    [Tooltip("Enemies deal this share of the normal damage")]
    public float damageFactor = 0.25f;
    [Tooltip("Multiplier on the enemies' base HP")]
    public float healthFactor = 3f;

    public int CurrentWave { get; private set; } // 1-based, 0 before the first wave
    public bool Victory { get; private set; }

    readonly System.Random rng = new();
    Transform team;
    float nextWaveAt;

    void Start()
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) team = player.transform;
        SpawnNextWave();
    }

    void FixedUpdate()
    {
        if (Victory || Combatant.CountAlive(Team.Enemy) > 0) return;
        if (CurrentWave >= waves.Length)
        {
            Victory = true;
            return;
        }
        if (nextWaveAt <= 0f) nextWaveAt = Time.time + delayBetweenWaves;
        else if (Time.time >= nextWaveAt) SpawnNextWave();
    }

    void SpawnNextWave()
    {
        nextWaveAt = 0f;
        if (CurrentWave >= waves.Length) return;
        Vector2? around = team != null ? (Vector2)team.position : null;
        foreach (var entry in waves[CurrentWave].enemies)
            for (int i = 0; i < entry.count; i++)
                Spawn(entry.set, generator.RandomOpenPosition(rng, minDistance, around));
        CurrentWave++;
    }

    void Spawn(PmdAnimSet set, Vector3 position)
    {
        var go = new GameObject(GameData.Pokemon(set.pokemonId)?.name ?? "Enemy");
        go.transform.SetParent(transform, false);
        go.transform.position = position;

        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        go.AddComponent<CircleCollider2D>().radius = 0.3f;

        go.AddComponent<PmdAnimator>().SetAnimSet(set);
        var combatant = go.AddComponent<Combatant>();
        combatant.Init(set.pokemonId, Team.Enemy);
        combatant.damageFactor = damageFactor;
        combatant.maxHp = combatant.hp = combatant.maxHp * healthFactor;
        go.AddComponent<EnemyAI>();
        go.AddComponent<HealthBar>();
    }

    void Update()
    {
        if (!Victory) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if ((pad != null && pad.startButton.wasPressedThisFrame) || (kb != null && kb.enterKey.wasPressedThisFrame))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Provisional HUD: wave counter, then the victory message.
    void OnGUI()
    {
        if (waves == null || waves.Length == 0) return;
        if (Victory)
        {
            GUI.Box(new Rect(Screen.width / 2 - 170, Screen.height / 2 - 30, 340, 60),
                "Arène terminée !\nOptions (manette) ou Entrée pour recommencer");
            return;
        }
        string label = nextWaveAt > 0f ? $"Vague {CurrentWave}/{waves.Length} terminée" : $"Vague {CurrentWave}/{waves.Length}";
        GUI.Box(new Rect(Screen.width / 2 - 90, 10, 180, 26), label);
    }
}
