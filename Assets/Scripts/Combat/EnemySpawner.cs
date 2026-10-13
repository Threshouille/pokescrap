using System;
using UnityEngine;

/// Places enemies on free tiles once the arena exists.
[DefaultExecutionOrder(100)] // after DungeonGenerator.Start
public class EnemySpawner : MonoBehaviour
{
    [Serializable]
    public class Entry
    {
        public PmdAnimSet set;
        public int count = 1;
    }

    public DungeonGenerator generator;
    public Entry[] enemies;
    [Tooltip("Minimum distance from the arena centre, where the team starts")]
    public float minDistance = 6f;
    [Tooltip("Enemies deal this share of the normal damage")]
    public float damageFactor = 0.25f;
    [Tooltip("Multiplier on the enemies' base HP")]
    public float healthFactor = 3f;

    void Start()
    {
        var rng = new System.Random();
        foreach (var entry in enemies)
            for (int i = 0; i < entry.count; i++)
                Spawn(entry.set, generator.RandomOpenPosition(rng, minDistance));
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
}
