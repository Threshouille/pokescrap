using System;
using System.Collections.Generic;
using UnityEngine;

/// Dev sandbox (Run.Sandbox, from the team menu in devMode): Pokémon set around the arena keep terrain alive,
/// so terrains and their affinities can be tried at leisure. Read from terrain_sandbox.json. The emitters are
/// scenery: no collider, nothing can hit them, and the terrain they paint has no owner (it acts on everyone).
/// An emitter casts its zone, then only keeps alive the tiles that still hold it (every "every" seconds): a tile
/// neutralised by another terrain stays so until the next cast ("recast" seconds later). Strikes on another patch
/// (a "target") are casts each time.
public class TerrainSandbox : MonoBehaviour
{
    const float AttackAnimTime = 0.4f;

    [Serializable]
    class Emitter
    {
        public int pokemon;
        public float[] offset = new float[2];
        public string terrain;
        public float[] target = new float[2];
        public float radius = 2f;
        public float every = 2f;
        public float duration = 3f;
        public float delay;
        public float recast = 15f; // seconds between two full casts of the zone
    }

    [Serializable] class SandboxFile { public bool enemyWaves = true; public Emitter[] emitters; }

    class Live
    {
        public Emitter def;
        public Vector2 position, target;
        public PmdAnimator animator;
        public float next, idleAt, nextCast;
    }

    readonly List<Live> emitters = new();

    /// Whether enemies keep coming in the sandbox (EnemySpawner asks).
    public static bool EnemyWaves()
    {
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        return asset == null || JsonUtility.FromJson<SandboxFile>(asset.text).enemyWaves;
    }

    void Start()
    {
        var arena = DungeonGenerator.Current;
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        if (arena == null || asset == null) return;
        int c = arena.Size / 2;
        Vector2 centre = arena.CentreOf(c, c);
        foreach (var e in JsonUtility.FromJson<SandboxFile>(asset.text).emitters ?? new Emitter[0])
        {
            var set = PokemonRoster.Get(e.pokemon);
            Vector2 at = centre + Vec(e.offset);
            var go = new GameObject($"Émetteur {GameData.Pokemon(e.pokemon)?.name} ({e.terrain})");
            go.transform.SetParent(transform, false);
            go.transform.position = at;
            go.AddComponent<SpriteRenderer>().sortingOrder = 1;
            PmdAnimator animator = null;
            if (set != null)
            {
                animator = go.AddComponent<PmdAnimator>();
                animator.SetAnimSet(set);
                animator.direction = PmdAnimator.DirectionIndex(centre - at); // facing the middle
            }
            emitters.Add(new Live { def = e, position = at, target = at + Vec(e.target), animator = animator, next = Time.time + e.delay, nextCast = Time.time + e.delay });
        }
    }

    static Vector2 Vec(float[] v) => v != null && v.Length >= 2 ? new Vector2(v[0], v[1]) : Vector2.zero;

    void FixedUpdate()
    {
        foreach (var e in emitters)
        {
            if (e.animator != null && e.idleAt > 0f && Time.time >= e.idleAt)
            {
                e.animator.Play("Idle");
                e.idleAt = 0f;
            }
            if (Time.time < e.next) continue;
            e.next = Time.time + Mathf.Max(0.2f, e.def.every);
            bool strike = Vec(e.def.target) != Vector2.zero;
            if (!strike && Time.time < e.nextCast)
            {
                Terrain.RefreshArea(e.def.terrain, e.target, e.def.radius, e.def.duration);
                continue;
            }
            e.nextCast = Time.time + Mathf.Max(e.def.every, e.def.recast);
            Terrain.PaintArea(e.def.terrain, e.target, e.def.radius, e.def.duration);
            if (e.animator != null)
            {
                // A cast shows an attack, toward the patch it strikes.
                if (strike) e.animator.direction = PmdAnimator.DirectionIndex(e.target - e.position);
                e.animator.Play(e.animator.Has("Shoot") ? "Shoot" : "Attack");
                e.idleAt = Time.time + AttackAnimTime;
            }
        }
    }
}
