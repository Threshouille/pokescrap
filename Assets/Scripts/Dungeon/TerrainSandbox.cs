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
/// drawn at random, R or Select to draw again), and back. Other stairs lead to the mega gallery: every mega evolution
/// of megas.json standing still in a grid, scenery like the emitters, unnamed; the one the Pokémon stands next to faces it and
/// plays its animations one after the other.
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

    [Serializable] class SandboxFile { public bool enemyWaves = true; public Emitter[] emitters; public int galleryColumns = 9; public float gallerySpacing = 3f; }

    [Serializable] class Mega { public int id; public string name; }
    [Serializable] class MegaFile { public Mega[] megas; }

    class Shown
    {
        public PmdAnimator animator;
        public List<string> anims;
        public int anim = -1; // the animation shown while inspected, -1 when idle
        public float nextAnim;
    }

    readonly List<Shown> gallery = new();
    const float InspectReach = 1.5f, AnimShowTime = 1.2f;

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
    static readonly Vector2 GalleryStairs = new(12f, -12f), GalleryBackStairs = new(-12f, 12f);
    static readonly Vector2 ShowcaseStairs = new(-12f, 12f); // to the showcase of the new island art
    static readonly Vector2 GalleryCentre = new(0f, 5f); // above the arrival point, which stays clear

    int room; // 0: terrain sandbox, 1: obstacle room, 2: mega gallery, 3: showcase of the new island art
    ShowcaseView showcase;
    int pendingRoom = -1;
    float fadeOutAt, switchAt, fadeInUntil;
    bool stairsArmed; // the stairs only work once the Pokémon has stepped off them
    readonly List<(GameObject stairs, int room)> stairs = new();
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

    // Every mega evolution with sprites, in rows facing down around the arena centre.
    void SpawnGallery()
    {
        var arena = DungeonGenerator.Current;
        var asset = Resources.Load<TextAsset>("Data/megas");
        var settings = Resources.Load<TextAsset>("Data/terrain_sandbox");
        if (arena == null || asset == null) return;
        var file = settings != null ? JsonUtility.FromJson<SandboxFile>(settings.text) : new SandboxFile();
        var megas = new List<Mega>();
        foreach (var m in JsonUtility.FromJson<MegaFile>(asset.text).megas ?? new Mega[0])
            if (PokemonRoster.Has(m.id)) megas.Add(m);
        int columns = Mathf.Max(1, file.galleryColumns);
        int rows = (megas.Count + columns - 1) / columns;
        if (emitterRoot == null) emitterRoot = new GameObject("Galerie des mégas").transform;
        for (int i = 0; i < megas.Count; i++)
        {
            int row = i / columns, column = i % columns;
            int inRow = Mathf.Min(columns, megas.Count - row * columns);
            var at = Centre(arena) + GalleryCentre + new Vector2((column - (inRow - 1) / 2f) * file.gallerySpacing, ((rows - 1) / 2f - row) * file.gallerySpacing);
            var go = new GameObject(megas[i].name);
            go.transform.SetParent(emitterRoot, false);
            go.transform.position = at;
            go.AddComponent<SpriteRenderer>().sortingOrder = 1;
            var set = PokemonRoster.Get(megas[i].id);
            var animator = go.AddComponent<PmdAnimator>();
            animator.SetAnimSet(set);
            animator.direction = PmdAnimator.DirectionIndex(Vector2.down);
            var anims = new List<string>();
            foreach (var a in set.anims) anims.Add(a.name);
            gallery.Add(new Shown { animator = animator, anims = anims });
        }
    }

    /// Removes the emitters (the AI test bench needs a quiet arena).
    public void Quiet() => ClearEmitters();

    void ClearEmitters()
    {
        emitters.Clear();
        gallery.Clear();
        if (emitterRoot != null) Destroy(emitterRoot.gameObject);
        emitterRoot = null;
    }

    // Provisional stairs: a dark square with a light one inside, to walk onto.
    void PlaceStairs()
    {
        var arena = DungeonGenerator.Current;
        if (arena == null) return;
        foreach (var (old, _) in stairs) Destroy(old);
        stairs.Clear();
        if (room == 0)
        {
            AddStairs(arena, BaseStairs, 1, "Escalier vers la salle des obstacles", new Color(0.95f, 0.8f, 0.3f));
            AddStairs(arena, GalleryStairs, 2, "Escalier vers la galerie des mégas", new Color(0.85f, 0.5f, 1f));
            AddStairs(arena, ShowcaseStairs, 3, "Escalier vers la vitrine de l'île", new Color(0.55f, 0.9f, 0.45f));
        }
        else if (room == 3 && arena.showcase != null)
        {
            // Two tiles left of where the team arrives.
            var spawn = Vector2Int.FloorToInt(arena.showcase.spawn) + arena.Interior.min;
            AddStairs(arena, arena.CentreOf(spawn.x - 2, spawn.y) - Centre(arena), 0, "Escalier vers le terrain de test", new Color(0.6f, 0.85f, 1f));
        }
        else AddStairs(arena, room == 1 ? ObstacleStairs : GalleryBackStairs, 0, "Escalier vers le terrain de test", new Color(0.6f, 0.85f, 1f));
        stairsArmed = false;
    }

    void AddStairs(DungeonGenerator arena, Vector2 offset, int target, string name, Color colour)
    {
        var cell = arena.CellOf(Centre(arena) + offset);
        var go = new GameObject(name);
        go.transform.position = arena.CentreOf(cell.x, cell.y);
        var outer = go.AddComponent<SpriteRenderer>();
        outer.sprite = Shapes.Square;
        outer.color = new Color(0.1f, 0.1f, 0.12f);
        outer.sortingOrder = 0;
        var inner = new GameObject("Marches").AddComponent<SpriteRenderer>();
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = Vector3.one * 0.6f;
        inner.sprite = Shapes.Square;
        inner.color = colour;
        inner.sortingOrder = 0;
        stairs.Add((go, target));
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
        // The showcase draws its own ground, water and bridge.
        arena.showcase = room == 3 ? ShowcaseMap.Load() : null;
        Obstacles.Hidden = room == 3;
        arena.Generate(); // also clears terrain and obstacles, and moves the team object to the centre
        if (showcase == null) showcase = gameObject.AddComponent<ShowcaseView>();
        if (room == 3) showcase.Build(arena, arena.showcase);
        else showcase.Clear();
        drawn = new List<string>();
        if (room == 1 && Obstacles.Current != null)
        {
            var asset = Resources.Load<TextAsset>("Data/obstacle_room");
            if (asset != null) drawn = Obstacles.Current.Generate(JsonUtility.FromJson<Obstacles.Room>(asset.text), rng, KeptClear(arena));
        }
        if (room == 0) SpawnEmitters();
        if (room == 2) SpawnGallery();
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
        string line = room == 0 ? "Escalier jaune (en haut à droite) : salle des obstacles     Escalier violet (en bas à droite) : galerie des mégas     Escalier vert (en haut à gauche) : vitrine de l'île"
            : room == 3 ? "Vitrine de l'île (nouvelle direction artistique)     Escalier (à gauche de l'arrivée) : retour"
            : room == 2 ? "Galerie des mégas : s'approcher d'un Pokémon montre ses animations     Escalier (en haut à gauche) : retour"
            : $"Salle des obstacles : {(drawn.Count > 0 ? string.Join(", ", drawn) : "rien")}     R ou Select : nouveau tirage     Escalier (en bas à gauche) : retour";
        var rect = new Rect(0f, 18f * s, Screen.width, 12f * s);
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - 200f * s, rect.y, 400f * s, rect.height), Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(rect, line, label);
        GalleryLabels(s);
        // Fade between the rooms.
        float alpha = pendingRoom >= 0 ? Mathf.Clamp01((Time.unscaledTime - fadeOutAt) / FadeTime)
            : Time.unscaledTime < fadeInUntil ? (fadeInUntil - Time.unscaledTime) / FadeTime : 0f;
        if (alpha <= 0f) return;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;
    }

    // The animation the inspected gallery Pokémon plays, under it.
    void GalleryLabels(float s)
    {
        var cam = Camera.main;
        if (gallery.Count == 0 || cam == null) return;
        int size = label.fontSize;
        label.fontSize = Mathf.RoundToInt(6f * s);
        foreach (var g in gallery)
        {
            if (g.animator == null || g.anim < 0) continue;
            Vector3 p = cam.WorldToScreenPoint(g.animator.transform.position + Vector3.down * 0.9f);
            if (p.z < 0f) continue;
            string text = g.anims[g.anim];
            var rect = new Rect(p.x - 60f * s, Screen.height - p.y, 120f * s, 16f * s);
            var previous = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + s, rect.y + s, rect.width, rect.height), text, label);
            GUI.color = new Color(1f, 0.9f, 0.5f);
            GUI.Label(rect, text, label);
            GUI.color = previous;
        }
        label.fontSize = size;
    }

    // The gallery Pokémon next to the controlled one faces it and plays its animations in turn; the others idle.
    void UpdateGallery(Combatant walker)
    {
        foreach (var g in gallery)
        {
            if (g.animator == null || g.anims.Count == 0) continue;
            Vector2 at = g.animator.transform.position;
            bool near = walker != null && Vector2.Distance(walker.transform.position, at) <= InspectReach;
            if (!near)
            {
                if (g.anim >= 0)
                {
                    g.anim = -1;
                    g.animator.direction = PmdAnimator.DirectionIndex(Vector2.down);
                    g.animator.Play("Idle");
                }
                continue;
            }
            g.animator.direction = PmdAnimator.DirectionIndex((Vector2)walker.transform.position - at);
            if (g.anim >= 0 && Time.time < g.nextAnim) continue;
            g.anim = (g.anim + 1) % g.anims.Count;
            g.nextAnim = Time.time + AnimShowTime;
            g.animator.Play(g.anims[g.anim]);
        }
    }

    void FixedUpdate()
    {
        var team = FindAnyObjectByType<PlayerController>();
        var walker = team != null ? team.Controlled : null;
        if (walker != null && pendingRoom < 0)
        {
            bool onAny = false;
            foreach (var (go, target) in stairs)
            {
                if (go == null || Vector2.Distance(walker.transform.position, go.transform.position) > StairsReach) continue;
                onAny = true;
                if (stairsArmed) Switch(target);
            }
            if (!onAny) stairsArmed = true;
        }
        UpdateGallery(walker);
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
