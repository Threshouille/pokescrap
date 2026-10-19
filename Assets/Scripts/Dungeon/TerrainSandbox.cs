using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Dev sandbox (Run.Sandbox, from the team menu in devMode): Pokémon set around the arena keep terrain alive,
/// so terrains and their affinities can be tried at leisure. Read from terrain_sandbox.json. The emitters are
/// scenery: no collider, nothing can hit them, and the terrain they paint has no owner (it acts on everyone).
/// An emitter casts its zone, then only keeps alive the tiles that still hold it (every "every" seconds): a tile
/// neutralised by another terrain stays so until the next cast ("recast" seconds later). Strikes on another patch
/// (a "target") are casts each time.
/// Stairs lead to a second room, the obstacle room (Obstacles.Generate from obstacle_room.json: 2 or 3 obstacles
/// drawn at random, R or Select to draw again), and back.
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

    const float FadeTime = 0.25f, StairsReach = 0.6f;
    static readonly Vector2 BaseStairs = new(12f, 12f), ObstacleStairs = new(-12f, -12f); // from the arena centre, in tiles

    int room; // 0: terrain sandbox, 1: obstacle room
    int pendingRoom = -1;
    float fadeOutAt, switchAt, fadeInUntil;
    bool stairsArmed; // the stairs only work once the Pokémon has stepped off them
    GameObject stairs;
    Transform emitterRoot; // the emitters' parent (this component sits on the enemy spawner, whose children are enemies)
    readonly System.Random rng = new();
    List<string> drawn = new();
    GUIStyle label;

    /// Whether enemies keep coming in the sandbox (EnemySpawner asks).
    public static bool EnemyWaves()
    {
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        return asset == null || JsonUtility.FromJson<SandboxFile>(asset.text).enemyWaves;
    }

    void Start()
    {
        SpawnEmitters();
        PlaceStairs();
    }

    static Vector2 Centre(DungeonGenerator arena) => arena.CentreOf(arena.Size / 2, arena.Size / 2);

    void SpawnEmitters()
    {
        var arena = DungeonGenerator.Current;
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        if (arena == null || asset == null) return;
        Vector2 centre = Centre(arena);
        foreach (var e in JsonUtility.FromJson<SandboxFile>(asset.text).emitters ?? new Emitter[0])
        {
            var set = PokemonRoster.Get(e.pokemon);
            Vector2 at = centre + Vec(e.offset);
            var go = new GameObject($"Émetteur {GameData.Pokemon(e.pokemon)?.name} ({e.terrain})");
            if (emitterRoot == null) emitterRoot = new GameObject("Émetteurs").transform;
            go.transform.SetParent(emitterRoot, false);
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

    void ClearEmitters()
    {
        emitters.Clear();
        if (emitterRoot != null) Destroy(emitterRoot.gameObject);
        emitterRoot = null;
    }

    // Provisional stairs: a dark square with a light one inside, to walk onto.
    void PlaceStairs()
    {
        var arena = DungeonGenerator.Current;
        if (arena == null) return;
        if (stairs != null) Destroy(stairs);
        var cell = arena.CellOf(Centre(arena) + (room == 0 ? BaseStairs : ObstacleStairs));
        stairs = new GameObject(room == 0 ? "Escalier vers la salle des obstacles" : "Escalier vers le terrain de test");
        stairs.transform.position = arena.CentreOf(cell.x, cell.y);
        var outer = stairs.AddComponent<SpriteRenderer>();
        outer.sprite = Shapes.Square;
        outer.color = new Color(0.1f, 0.1f, 0.12f);
        outer.sortingOrder = 0;
        var inner = new GameObject("Marches").AddComponent<SpriteRenderer>();
        inner.transform.SetParent(stairs.transform, false);
        inner.transform.localScale = Vector3.one * 0.6f;
        inner.sprite = Shapes.Square;
        inner.color = room == 0 ? new Color(0.95f, 0.8f, 0.3f) : new Color(0.6f, 0.85f, 1f);
        inner.sortingOrder = 0;
        stairsArmed = false;
    }

    List<Vector2Int> KeptClear(DungeonGenerator arena)
    {
        var c = Centre(arena);
        return new List<Vector2Int> { arena.CellOf(c), arena.CellOf(c + ObstacleStairs), arena.CellOf(c + BaseStairs) };
    }

    // Fades out, rebuilds the arena as the other room (or draws the obstacle room again), fades back in.
    void Switch(int target)
    {
        if (pendingRoom >= 0) return;
        pendingRoom = target;
        fadeOutAt = Time.unscaledTime;
        switchAt = Time.unscaledTime + FadeTime;
    }

    void Rebuild()
    {
        var arena = DungeonGenerator.Current;
        var team = FindAnyObjectByType<PlayerController>();
        if (arena == null) return;
        room = pendingRoom;
        foreach (var zone in FindObjectsByType<Zone>()) Destroy(zone.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        ClearEmitters();
        arena.Generate(); // also clears terrain and obstacles, and moves the team object to the centre
        drawn = new List<string>();
        if (room == 1 && Obstacles.Current != null)
        {
            var asset = Resources.Load<TextAsset>("Data/obstacle_room");
            if (asset != null) drawn = Obstacles.Current.Generate(JsonUtility.FromJson<Obstacles.Room>(asset.text), rng, KeptClear(arena));
        }
        if (room == 0) SpawnEmitters();
        PlaceStairs();
        if (team != null) team.PrepareForNextArena(team.transform.position, 0f);
    }

    void Update()
    {
        if (pendingRoom >= 0 && Time.unscaledTime >= switchAt)
        {
            Rebuild();
            pendingRoom = -1;
            fadeInUntil = Time.unscaledTime + FadeTime;
        }
        if (room != 1 || pendingRoom >= 0) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame)) Switch(1);
    }

    void OnGUI()
    {
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        label.fontSize = Mathf.RoundToInt(8f * s);
        string line = room == 0 ? "Escalier (en haut à droite) : salle des obstacles"
            : $"Salle des obstacles : {(drawn.Count > 0 ? string.Join(", ", drawn) : "rien")}     R ou Select : nouveau tirage     Escalier (en bas à gauche) : retour";
        var rect = new Rect(0f, 18f * s, Screen.width, 12f * s);
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - 200f * s, rect.y, 400f * s, rect.height), Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(rect, line, label);
        // Fade between the rooms.
        float alpha = pendingRoom >= 0 ? Mathf.Clamp01((Time.unscaledTime - fadeOutAt) / FadeTime)
            : Time.unscaledTime < fadeInUntil ? (fadeInUntil - Time.unscaledTime) / FadeTime : 0f;
        if (alpha <= 0f) return;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    void FixedUpdate()
    {
        var team = FindAnyObjectByType<PlayerController>();
        var walker = team != null ? team.Controlled : null;
        if (stairs != null && walker != null && pendingRoom < 0)
        {
            bool on = Vector2.Distance(walker.transform.position, stairs.transform.position) <= StairsReach;
            if (on && stairsArmed) Switch(1 - room);
            else if (!on) stairsArmed = true;
        }
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
