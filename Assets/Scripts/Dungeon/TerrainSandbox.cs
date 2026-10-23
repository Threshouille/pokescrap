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
    static readonly Vector2 RoomsStairs = new(-12f, -12f); // to the room gallery (rooms.json)
    static readonly Vector2 IslandStairs = new(0f, -12f); // to a test island (IslandPlan, IslandRun)
    static readonly Vector2 QuarterStairs = new(0f, 12f); // to the test quarter of island (quarter_test.json)
    static readonly Vector2 GalleryCentre = new(0f, 5f); // above the arrival point, which stays clear

    int room; // 0: terrain sandbox, 1: obstacle room, 2: mega gallery, 3: room gallery, 4: test island, 5: test quarter
    int quarterSeed, quarterRock; // test quarter: kept to draw the same one again with another rock (C)
    IslandRun island; // test island
    RoomInstance shownRoom; // room gallery: the room on show
    int roomType; // room gallery: 0 any type, else RoomCatalog.Types[roomType - 1]
    DungeonTileset sandboxDecor; // the arena's own décor, put back when leaving the room gallery
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
            AddStairs(arena, RoomsStairs, 3, "Escalier vers la galerie des salles", new Color(0.5f, 0.9f, 0.5f));
            AddStairs(arena, IslandStairs, 4, "Escalier vers une île de test", new Color(1f, 0.6f, 0.2f));
            AddStairs(arena, QuarterStairs, 5, "Escalier vers le quart d'île de test", new Color(0.3f, 0.85f, 0.75f));
        }
        else if (room == 4 || room == 5)
            AddStairs(arena, FreeNear(arena, arena.CellOf(arena.SpawnPoint)) - Centre(arena), 0, "Escalier vers le terrain de test", new Color(0.6f, 0.85f, 1f));
        else if (room == 3) AddStairs(arena, BackStairsNearSpawn(arena), 0, "Escalier vers le terrain de test", new Color(0.6f, 0.85f, 1f));
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
        outer.sortingOrder = -1; // under the light square inside (a tie would hide it)
        var inner = new GameObject("Marches").AddComponent<SpriteRenderer>();
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = Vector3.one * 0.6f;
        inner.sprite = Shapes.Square;
        inner.color = colour;
        inner.sortingOrder = 0;
        stairs.Add((go, target));
    }

    // The first free tile (no wall, no water) about 3 tiles from a cell, for the way back on an island.
    static Vector2 FreeNear(DungeonGenerator arena, Vector2Int cell)
    {
        for (int r = 3; r <= 10; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = cell.x + dx, y = cell.y + dy;
                    if (arena.Walkable(x, y) && Obstacles.At(x, y) == null) return arena.CentreOf(x, y);
                }
        return arena.CentreOf(cell.x, cell.y);
    }

    // A room of the gallery may be small or full of walls: the way back is laid on the first free tile found a few
    // tiles from the spawn point (as an offset from the centre).
    static Vector2 BackStairsNearSpawn(DungeonGenerator arena)
    {
        var centre = arena.CellOf(Centre(arena));
        for (int r = 3; r <= 8; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = centre.x + dx, y = centre.y + dy;
                    if (arena.Walkable(x, y) && Obstacles.At(x, y) == null) return arena.CentreOf(x, y) - Centre(arena);
                }
        return Vector2.zero;
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
        // The room gallery builds a room of the catalogue; the other rooms are the sandbox's own open arena.
        if (sandboxDecor == null) sandboxDecor = arena.tileset;
        if (island != null) island.End();
        if (room == 4 || room == 5)
        {
            // A test island: four quarters of biomes around the sealed central zone; or the test quarter.
            if (island == null)
            {
                island = gameObject.AddComponent<IslandRun>();
                island.Built += PlaceStairs; // the way back, near where the team arrives in each zone
                island.revealAll = true; // test island: the whole mini-map at once
            }
            if (room == 5)
            {
                island.Begin(IslandPlan.QuarterTestPlan(quarterSeed, quarterRock));
                quarterSeed = 0; // R draws a new one
            }
            else island.Begin(rng);
            PlaceStairs();
            return;
        }
        if (room == 3)
        {
            var types = RoomCatalog.Types;
            shownRoom = RoomCatalog.Pick(roomType > 0 && roomType <= types.Count ? types[roomType - 1].key : null, rng);
            arena.room = shownRoom;
        }
        else
        {
            arena.room = null;
            arena.tileset = sandboxDecor;
        }
        arena.Generate(); // also clears terrain and obstacles, and moves the team object to the centre
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
        if ((room != 1 && room != 3 && room != 4 && room != 5) || pendingRoom >= 0) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame)) Switch(room);
        // Test quarter: C or d-pad down draws the same quarter with the other rock for its cliffs.
        if (room == 5 && ((kb != null && kb.cKey.wasPressedThisFrame) || (pad != null && pad.dpad.down.wasPressedThisFrame)))
        {
            quarterSeed = IslandPlan.LastQuarterSeed;
            quarterRock++;
            Switch(5);
        }
        // Room gallery: N or d-pad up picks the type of room shown (any, then each type in turn).
        if (room == 3 && ((kb != null && kb.nKey.wasPressedThisFrame) || (pad != null && pad.dpad.up.wasPressedThisFrame)))
        {
            roomType = (roomType + 1) % (RoomCatalog.Types.Count + 1);
            Switch(3);
        }
    }

    void OnGUI()
    {
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        label.fontSize = Mathf.RoundToInt(8f * s);
        string line = room == 0 ? "Escaliers : jaune (en haut à droite) obstacles, violet (en bas à droite) mégas, vert (en bas à gauche) salles, orange (en bas) île, turquoise (en haut) quart d'île"
            : room == 2 ? "Galerie des mégas : s'approcher d'un Pokémon montre ses animations     Escalier (en haut à gauche) : retour"
            : room == 4 ? $"Île de test : {(island?.Quarter >= 0 ? $"{island.Plan.themes[island.Quarter].name} (quartier {System.Array.IndexOf(island.Plan.order, island.Quarter) + 1}/4) — {island.Biome?.name}" : "hors des quartiers")}     R ou Select : nouvelle île     Escalier (à l'arrivée) : retour"
            : room == 5 ? $"Quart d'île de test : {island?.Biome?.name ?? "—"}     R ou Select : nouveau tirage     C ou croix bas : autre roche     Escalier (à l'arrivée) : retour"
            : room == 3 ? $"{shownRoom?.Describe() ?? "Aucune salle dans rooms.json"}     Type : {(roomType > 0 && roomType <= RoomCatalog.Types.Count ? RoomCatalog.Types[roomType - 1].name : "tous")} (N, croix haut)     R ou Select : autre salle     Escalier : retour"
            : $"Salle des obstacles : {(drawn.Count > 0 ? string.Join(", ", drawn) : "rien")}     R ou Select : nouveau tirage     Escalier (en bas à gauche) : retour";
        var rect = new Rect(0f, 18f * s, Screen.width, 12f * s);
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        float boxWidth = Mathf.Min(Screen.width, label.CalcSize(new GUIContent(line)).x + 16f * s);
        GUI.DrawTexture(new Rect((Screen.width - boxWidth) / 2f, rect.y, boxWidth, rect.height), Texture2D.whiteTexture);
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
