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
/// The obstacle room also holds training dummies ("dummies"): enemies that neither move nor attack, at random free
/// tiles, which come back a few seconds after being knocked out, to try moves, Z moves and obstacles on.
/// A fourth room shows the Z moves: sturdy dummies in a ring, the controlled Pokémon carries the Z crystal picked with
/// ↑/↓ (1/2) and borrows the strongest move of its type when it knows none (Ultimates.DemoMoves).
/// Two more rooms are real fights, with the opponent back a few seconds after a knock-out: an elite beyond a river
/// (with a bridge or not, R / Select: it freezes the river to come, burns the bridge under a close fighter, uses
/// its ultimate), and a legendary boss whose ultimate (Terastal phase or Z move) depends on the fight. The green stairs
/// lead to the rescue and heal demos (RescueDemos).
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

    [Serializable] class SandboxFile { public bool enemyWaves = true; public Emitter[] emitters; public int galleryColumns = 9; public float gallerySpacing = 3f;
        public int[] dummies = new int[0]; public int dummyCount = 6, dummyLevel = 5; public float dummyRespawn = 4f, dummyHealth = 3f, dummyMinDistance = 4f;
        public int zDemoCount = 8; public float zDemoRadius = 4.5f, zDemoHealth = 30f, zDemoRespawn = 2f;
        public Fighter eliteRoom = new(), bossRoom = new(); }

    /// The opponent of the elite or boss room: a real fight (its AI, its moves, its ultimate).
    [Serializable]
    class Fighter
    {
        public int pokemon;
        public int[] moves = new int[0];
        public int level = 8;
        public float health = 3f, respawn = 6f;
        public float[] offset = new float[2];
    }

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
    static readonly Vector2 ZDemoStairs = new(-12f, 12f), ZDemoBackStairs = new(12f, 12f);
    static readonly Vector2 EliteStairs = new(-12f, -12f), EliteBackStairs = new(-12f, 12f);
    static readonly Vector2 BossStairs = new(0f, -12f), BossBackStairs = new(0f, 12f);
    static readonly Vector2 RescueStairs = new(12f, 0f), RescueBackStairs = new(-12f, 0f);
    const int RiverFrom = 3, RiverTo = 5; // elite room: columns of the river, from the arrival point
    static readonly Vector2 GalleryCentre = new(0f, 5f); // above the arrival point, which stays clear

    int room; // 0: terrain sandbox, 1: obstacle room, 2: mega gallery, 3: Z demo, 4: elite room, 5: boss room, 6: rescue demos
    Combatant fighter; // the elite or the boss of rooms 4 and 5
    float fighterBack; // when it comes back once knocked out
    bool bridge; // elite room: the river has a bridge (R / Select switches)
    class Dummy
    {
        public Combatant combatant;
        public int pokemon;
        public Vector2? at; // where it stands again (Z demo room), else a random free tile
        public float health, back; // health ×; when a knocked-out dummy comes back
    }

    readonly List<Dummy> dummies = new();
    readonly Dictionary<Combatant, SpecialItems.Equipped> keptItems = new(); // items before the Z demo room
    int zDemoIndex;
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

    /// Removes the emitters and dummies (the AI test bench needs a quiet arena).
    public void Quiet()
    {
        ClearEmitters();
        ClearDummies();
    }

    // ---- Training dummies (obstacle room) ----

    static SandboxFile Settings()
    {
        var asset = Resources.Load<TextAsset>("Data/terrain_sandbox");
        return asset != null ? JsonUtility.FromJson<SandboxFile>(asset.text) : new SandboxFile();
    }

    void SpawnDummies()
    {
        var file = Settings();
        for (int i = 0; i < file.dummyCount && file.dummies.Length > 0; i++)
            AddDummy(file, file.dummies[i % file.dummies.Length], null, file.dummyHealth);
    }

    // Z demo room: dummies in a ring around the arrival point, sturdy enough to show every effect, back where they stood.
    void SpawnZDemo()
    {
        var file = Settings();
        var arena = DungeonGenerator.Current;
        if (arena == null || file.dummies.Length == 0) return;
        for (int i = 0; i < file.zDemoCount; i++)
        {
            float angle = (90f + 360f * i / file.zDemoCount) * Mathf.Deg2Rad;
            AddDummy(file, file.dummies[i % file.dummies.Length], Centre(arena) + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * file.zDemoRadius, file.zDemoHealth);
        }
    }

    void AddDummy(SandboxFile file, int pokemonId, Vector2? at, float health)
    {
        var d = new Dummy { pokemon = pokemonId, at = at, health = health };
        d.combatant = SpawnDummy(file, d);
        dummies.Add(d);
    }

    // An enemy without AI: it stands still and never attacks, but takes hits, statuses and pushes.
    Combatant SpawnDummy(SandboxFile file, Dummy d)
    {
        var arena = DungeonGenerator.Current;
        if (arena == null || !PokemonRoster.Has(d.pokemon)) return null;
        var go = new GameObject($"Cible : {GameData.Pokemon(d.pokemon)?.name}");
        go.transform.SetParent(transform, false);
        go.transform.position = d.at ?? (Vector2)arena.RandomOpenPosition(rng, file.dummyMinDistance);
        go.AddComponent<SpriteRenderer>().sortingOrder = 1;
        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.mass = 5f; // hard to shove by walking into it
        body.linearDamping = 8f; // stops where a push leaves it
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var animator = go.AddComponent<PmdAnimator>();
        animator.SetAnimSet(PokemonRoster.Get(d.pokemon));
        animator.direction = PmdAnimator.DirectionIndex(Vector2.down);
        var c = go.AddComponent<Combatant>();
        c.Init(d.pokemon, Team.Enemy, new int[0]);
        for (int level = 2; level <= file.dummyLevel; level++) c.ApplyGrowth(LevelData.Growth(d.pokemon, level), level);
        c.maxHp = c.hp = c.maxHp * d.health;
        go.AddComponent<HealthBar>();
        return c;
    }

    void UpdateDummies()
    {
        if (dummies.Count == 0) return;
        SandboxFile file = null;
        foreach (var d in dummies)
        {
            if (d.combatant != null && d.combatant.IsAlive) continue;
            file ??= Settings();
            if (d.back <= 0f)
            {
                d.back = Time.time + (room == 3 ? file.zDemoRespawn : file.dummyRespawn);
                continue;
            }
            if (Time.time < d.back) continue;
            if (d.combatant != null) Destroy(d.combatant.gameObject);
            if (d.at == null) d.pokemon = file.dummies[rng.Next(file.dummies.Length)];
            d.combatant = SpawnDummy(file, d);
            d.back = 0f;
        }
    }

    // ---- Elite and boss rooms: a real fight ----

    // A river from wall to wall between the arrival point and the elite, with a bridge in the middle or not.
    void BuildRiver(DungeonGenerator arena)
    {
        if (Obstacles.Current == null) return;
        var centre = arena.CellOf(Centre(arena));
        var inside = arena.Interior;
        for (int x = centre.x + RiverFrom; x <= centre.x + RiverTo; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                Obstacles.Place(x, y, bridge && Mathf.Abs(y - centre.y) <= 0 ? "bridge" : "water");
    }

    void SpawnFighter()
    {
        var spawner = GetComponent<EnemySpawner>();
        var arena = DungeonGenerator.Current;
        var f = room == 4 ? Settings().eliteRoom : Settings().bossRoom;
        if (spawner == null || arena == null || f.pokemon <= 0) return;
        fighter = spawner.SpawnFighter(f.pokemon, f.moves.Length > 0 ? f.moves : null, Centre(arena) + Vec(f.offset),
            isBoss: room == 5, elite: room == 4, f.level, f.health);
        if (fighter != null && room == 5) fighter.gameObject.AddComponent<HealthBar>(); // no boss bar in the test arena
        fighterBack = 0f;
    }

    void UpdateFighter()
    {
        if ((room != 4 && room != 5) || pendingRoom >= 0 || (fighter != null && fighter.IsAlive)) return;
        var f = room == 4 ? Settings().eliteRoom : Settings().bossRoom;
        if (fighterBack <= 0f)
        {
            fighterBack = Time.time + f.respawn;
            return;
        }
        if (Time.time < fighterBack) return;
        if (fighter != null) Destroy(fighter.gameObject);
        SpawnFighter();
    }

    // ---- Z demo room: the controlled Pokémon carries the chosen Z crystal ----

    void UpdateZDemo()
    {
        var team = FindAnyObjectByType<PlayerController>();
        var member = team != null ? team.Controlled : null;
        var crystals = SpecialItems.Crystals;
        if (member == null || crystals.Count == 0) return;
        if (!keptItems.ContainsKey(member)) keptItems[member] = member.Special;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        int step = 0;
        if ((pad != null && pad.dpad.up.wasPressedThisFrame) || (kb != null && kb.digit1Key.wasPressedThisFrame)) step = -1;
        if ((pad != null && pad.dpad.down.wasPressedThisFrame) || (kb != null && kb.digit2Key.wasPressedThisFrame)) step = 1;
        zDemoIndex = (zDemoIndex + step + crystals.Count) % crystals.Count;
        int type = crystals[zDemoIndex].type;
        if (member.Special == null || member.Special.family != SpecialItems.Family.Z || member.Special.type != type)
            member.Special = new SpecialItems.Equipped { family = SpecialItems.Family.Z, type = type, level = 1 };
    }

    void RestoreItems()
    {
        foreach (var (member, item) in keptItems)
            if (member != null) member.Special = item;
        keptItems.Clear();
        Ultimates.DemoMoves = false;
    }

    void ClearDummies()
    {
        foreach (var d in dummies)
            if (d.combatant != null) Destroy(d.combatant.gameObject);
        dummies.Clear();
    }

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
            AddStairs(arena, ZDemoStairs, 3, "Escalier vers la démo des capacités Z", new Color(1f, 0.55f, 0.2f));
            AddStairs(arena, EliteStairs, 4, "Escalier vers la salle de l'élite", new Color(1f, 0.85f, 0.25f));
            AddStairs(arena, BossStairs, 5, "Escalier vers la salle du boss", new Color(0.9f, 0.2f, 0.2f));
            AddStairs(arena, RescueStairs, 6, "Escalier vers les démos de secours", new Color(0.3f, 0.9f, 0.5f));
        }
        else AddStairs(arena, room switch { 1 => ObstacleStairs, 3 => ZDemoBackStairs, 4 => EliteBackStairs, 5 => BossBackStairs, 6 => RescueBackStairs, _ => GalleryBackStairs }, 0,
            "Escalier vers le terrain de test", new Color(0.6f, 0.85f, 1f));
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
        ClearDummies();
        if (fighter != null) Destroy(fighter.gameObject);
        fighter = null;
        fighterBack = 0f;
        arena.Generate(); // also clears terrain and obstacles, and moves the team object to the centre
        drawn = new List<string>();
        if (room == 1 && Obstacles.Current != null)
        {
            var asset = Resources.Load<TextAsset>("Data/obstacle_room");
            if (asset != null) drawn = Obstacles.Current.Generate(JsonUtility.FromJson<Obstacles.Room>(asset.text), rng, KeptClear(arena));
        }
        if (room == 0) SpawnEmitters();
        if (room == 1) SpawnDummies();
        RestoreItems();
        if (room == 3)
        {
            SpawnZDemo();
            Ultimates.DemoMoves = true;
        }
        if (room == 4) BuildRiver(arena);
        if (room == 4 || room == 5) SpawnFighter();
        var demos = GetComponent<RescueDemos>();
        if (room == 6 && demos == null) gameObject.AddComponent<RescueDemos>();
        if (room != 6 && demos != null) Destroy(demos);
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
        if (room == 3 && pendingRoom < 0) UpdateZDemo(); // input: read each frame
        if ((room != 1 && room != 4) || pendingRoom >= 0) return;
        var pad = Gamepad.current;
        var kb = Keyboard.current;
        if ((kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.selectButton.wasPressedThisFrame))
        {
            if (room == 4) bridge = !bridge;
            Switch(room);
        }
    }

    string ZDemoLine()
    {
        var crystals = SpecialItems.Crystals;
        if (crystals.Count == 0) return "Pas de cristal Z dans items.json";
        var c = crystals[zDemoIndex % crystals.Count];
        return $"Démo Z ({zDemoIndex % crystals.Count + 1}/{crystals.Count}) : {c.name} → {c.move} : {SpecialItems.ZDescription(c.type)}"
               + "     ↑/↓ ou 1/2 : cristal     F ou L3 + R3 : lancer     Escalier (en haut à droite) : retour";
    }

    void OnGUI()
    {
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        label ??= new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        label.fontSize = Mathf.RoundToInt(8f * s);
        string line = room == 0 ? "Escalier jaune (en haut à droite) : salle des obstacles     Escalier violet (en bas à droite) : galerie des mégas     Escalier orange (en haut à gauche) : démo des capacités Z     Escalier vert (à droite) : démos de secours"
            : room == 3 ? ZDemoLine()
            : room == 6 ? (GetComponent<RescueDemos>() is { } rescue ? rescue.Line() : "Démos de secours")
            : room == 4 ? $"Salle de l'élite : rivière {(bridge ? "avec un pont (en mêlée sur le pont, l'élite le brûle)" : "sans pont (l'élite la gèle pour venir)")}     R ou Select : {(bridge ? "sans" : "avec")} pont     Escalier (en haut à gauche) : retour"
            : room == 5 ? $"Salle du boss : {fighter?.Stats?.name ?? "boss"} {(fighter != null ? $"{Mathf.CeilToInt(fighter.hp)}/{Mathf.CeilToInt(fighter.maxHp)} PV" : "(revient bientôt)")} : téracristal ou capacité Z selon la situation     Escalier (en haut) : retour"

            : room == 2 ? "Galerie des mégas : s'approcher d'un Pokémon montre ses animations     Escalier (en haut à gauche) : retour"
            : $"Salle des obstacles : {(drawn.Count > 0 ? string.Join(", ", drawn) : "rien")}     Cibles immobiles (reviennent après un K.O.)     R ou Select : nouveau tirage     Escalier (en bas à gauche) : retour";
        var rect = new Rect(0f, 18f * s, Screen.width, 12f * s);
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        float width = Mathf.Min(Screen.width, Mathf.Max(400f * s, label.CalcSize(new GUIContent(line)).x + 16f * s));
        GUI.DrawTexture(new Rect(Screen.width / 2f - width / 2f, rect.y, width, rect.height), Texture2D.whiteTexture);
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
        UpdateDummies();
        UpdateFighter();
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
