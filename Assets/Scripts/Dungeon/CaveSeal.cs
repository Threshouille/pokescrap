using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// The sealed cave of the island (IslandPlan.sealedCave; GDD, "Grottes scellées"), added by IslandRun: draws the
/// parts of its seal in front of the door (IslandPlan.caveParts; sheets seal_* of Resources/Effects: a stele with the
/// icons of what opens it, braziers, plates, shells) and the mark of the sealed caves on its door, shows its hint nearby and opens the door (IslandPlan.OpenCave, DungeonGenerator.RedrawIsland) once its condition
/// is met. element: an attack of one of its types on the mechanism ('stages' hits), one of its weathers, the night;
/// team: a Pokémon of its types (or ids) near the mechanism; braziers: all lit at once by its types, each for
/// 'seconds'; plates: all held by Pokémon that people play (never the AI); sequence: the shells hit from the smallest;
/// time: 'seconds' spent near it. K in devMode opens it at once.
public class CaveSeal : MonoBehaviour
{
    const float HintRange = 3.5f, NearRange = 10f, TeamRange = 2.5f, HitMargin = 0.7f; // tiles
    const float CycleTime = 1f, TeamTime = 1f; // seconds

    class Part
    {
        public GameObject root;
        public SpriteRenderer body, icon;
        public string sheet; // the sheet playing on the body
        public float since; // when it started
        public string then; // the sheet after a one-shot one
        public int frame; // the frame of a still sheet (plates, shells)
        public int rank; // shells: 0 the smallest
        public float litUntil; // braziers
        public bool done; // shells hit, plates held
    }

    IslandRun island;
    PlayerController team;
    DungeonGenerator arena;
    CaveSeals.Seal seal;
    readonly List<Part> parts = new();
    GameObject root;
    SpriteRenderer door, mark;
    float markSince;
    int hits;
    float teamNear;

    IslandPlan Plan => island != null ? island.Plan : null;
    bool Shut => root != null && seal != null && Plan != null && !Plan.caveOpen && !island.Away;

    /// The cave is in that zone and still shut.
    public bool Waiting(int inZone) =>
        Plan != null && Plan.sealedCave.x >= 0 && !Plan.caveOpen
        && Plan.region[Plan.Index(Plan.sealedCave.x, Plan.sealedCave.y)] == inZone;

    void Awake()
    {
        island = GetComponent<IslandRun>();
        island.Built += OnBuilt;
        Obstacles.Hit += OnHit;
    }

    void OnDestroy()
    {
        if (island != null) island.Built -= OnBuilt;
        Obstacles.Hit -= OnHit;
        Clear();
    }

    /// Forgets the seal's parts (the island is left, or rebuilt).
    public void Clear()
    {
        if (root != null) Destroy(root);
        root = null;
        parts.Clear();
        seal = null;
    }

    void OnBuilt()
    {
        Clear();
        team = FindAnyObjectByType<PlayerController>();
        arena = DungeonGenerator.Current;
        var plan = Plan;
        if (plan == null || plan.sealedCave.x < 0 || plan.region[plan.Index(plan.caveMechanism.x, plan.caveMechanism.y)] != island.Zone) return;
        seal = CaveSeals.Get(plan.caveSeal);
        if (seal == null) return;
        hits = 0;
        teamNear = 0f;
        root = new GameObject("Sceau de la grotte");
        // Shells: sizes shuffled along the row (always the same for this cave).
        var ranks = new List<int>();
        for (int k = 0; k < plan.caveParts.Count; k++) ranks.Add(k);
        var rng = new System.Random(plan.sealedCave.x * 7919 + plan.sealedCave.y);
        for (int k = ranks.Count - 1; k > 0; k--) { int j = rng.Next(k + 1); (ranks[k], ranks[j]) = (ranks[j], ranks[k]); }
        for (int k = 0; k < plan.caveParts.Count; k++)
        {
            var part = new Part { rank = Mathf.Min(ranks[k], 2) };
            part.root = new GameObject(seal.kind);
            part.root.transform.SetParent(root.transform, false);
            part.root.transform.position = island.Cell(plan.caveParts[k]);
            BuildPart(part);
            parts.Add(part);
        }
        // The door's sheet over its 3 × 2 tiles, the mark of every sealed cave at its top: over the walls (-1), under
        // the Pokémon.
        var front = new GameObject("Porte");
        front.transform.SetParent(root.transform, false);
        front.transform.position = island.Cell(plan.sealedCaveFront + Vector2Int.up) + Vector2.up * 0.5f;
        front.AddComponent<SortingGroup>().sortingOrder = 0;
        door = new GameObject("Planche").AddComponent<SpriteRenderer>();
        door.transform.SetParent(front.transform, false);
        mark = new GameObject("Repère").AddComponent<SpriteRenderer>();
        mark.transform.SetParent(front.transform, false);
        mark.transform.localPosition = Vector2.up * 0.75f;
        mark.sortingOrder = 1;
        markSince = Time.time;
    }

    void BuildPart(Part p)
    {
        if (seal.kind == "plates")
        {
            // Flat on the ground, under the Pokémon.
            p.body = Piece(p, "Dalle", Vector2.zero, 0);
            p.sheet = "seal_plate";
            return;
        }
        p.root.AddComponent<SortingGroup>().sortingOrder = 1; // the Pokémon's, sorted by its foot
        // Standing things: their foot a little below the middle of the tile.
        float up = seal.kind == "sequence" ? 0.15f : 0.4f;
        p.body = Piece(p, "Corps", new Vector2(0f, up), 0);
        p.sheet = seal.kind switch { "braziers" => "seal_brazier", "sequence" => "seal_shells", _ => "seal_stele" };
        if (seal.kind == "sequence") p.frame = p.rank;
        if (seal.kind is "braziers" or "sequence") return;
        // The stele's hollow holds the icon of what opens it.
        p.icon = Piece(p, "Icône", new Vector2(0f, up + 0.21f), 1);
        p.icon.transform.localScale = Vector3.one * 0.7f;
    }

    static SpriteRenderer Piece(Part p, string name, Vector2 offset, int order)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(p.root.transform, false);
        r.transform.localPosition = offset;
        r.sortingOrder = order;
        return r;
    }

    // A sheet's frame at that time since it started (its last one once a one-shot sheet is over), or null.
    static Sprite FrameOf(string name, float elapsed, int still, out bool over)
    {
        over = false;
        var sheet = EffectLibrary.Get(name);
        if (sheet == null) return null;
        int total = 0;
        foreach (int d in sheet.frame_duration_ms) total += d;
        if (total <= 0) return sheet.Frame(Mathf.Clamp(still, 0, sheet.frame_count - 1));
        int ms = (int)(elapsed * 1000f);
        if (sheet.loop) ms %= total;
        else if (ms >= total) { over = true; return sheet.Frame(sheet.frame_count - 1); }
        for (int k = 0; k < sheet.frame_count; k++)
        {
            ms -= sheet.frame_duration_ms[Mathf.Min(k, sheet.frame_duration_ms.Length - 1)];
            if (ms < 0) return sheet.Frame(k);
        }
        return sheet.Frame(sheet.frame_count - 1);
    }

    static void Play(Part p, string sheet, string then = null)
    {
        p.sheet = sheet;
        p.then = then;
        p.since = Time.time;
    }

    // The icons of what opens it (types, weathers, night...), shown in turn: seal_type_icons (by type) or seal_icons.
    List<Sprite> Icons()
    {
        var list = new List<Sprite>();
        if (seal == null) return list;
        var types = EffectLibrary.Get("seal_type_icons");
        var icons = EffectLibrary.Get("seal_icons");
        void Icon(int k) { if (icons != null && k < icons.frame_count) list.Add(icons.Frame(k)); }
        if (types != null)
            foreach (int t in seal.types)
                if (t >= 1 && t <= types.frame_count) list.Add(types.Frame(t - 1));
        foreach (var w in seal.weathers) Icon(w switch { "sun" => 0, "rain" => 1, "sand" => 2, "hail" => 3, _ => 6 });
        if (seal.night) Icon(4);
        switch (seal.kind)
        {
            case "team": Icon(6); break;
            case "time": Icon(5); break;
            case "key": Icon(7); break;
            case "guardian": case "clear": Icon(8); break;
        }
        if (list.Count == 0) Icon(6);
        return list;
    }

    void Update()
    {
        if (root == null || seal == null) return;
        if (root.activeSelf == island.Away) root.SetActive(!island.Away); // not in another room
        bool open = Plan != null && Plan.caveOpen;
        if (mark != null) mark.sprite = open ? null : FrameOf("seal_mark", Time.time - markSince, 0, out _);
        if (door != null)
        {
            // A looping door plays; the others show how worn they are (hits), the runes lit while a reader is near.
            var sheet = EffectLibrary.Get(seal.door);
            int stage = seal.kind == "team" ? (teamNear > 0f ? 1 : 0) : hits;
            door.sprite = open || sheet == null ? null
                : sheet.loop ? FrameOf(seal.door, Time.time - markSince, 0, out _) : sheet.Frame(Mathf.Clamp(stage, 0, sheet.frame_count - 1));
        }
        var icons = Icons();
        var icon = icons.Count > 0 ? icons[(int)(Time.time / CycleTime) % icons.Count] : null;
        foreach (var p in parts)
        {
            if (p.body == null) continue;
            switch (seal.kind)
            {
                case "braziers":
                {
                    string want = open || Time.time < p.litUntil ? "seal_brazier_lit" : "seal_brazier";
                    if (p.sheet != want) Play(p, want);
                    break;
                }
                case "plates":
                    p.frame = open || p.done ? 1 : 0;
                    break;
                case "sequence":
                    p.frame = p.rank + (open || p.done ? 3 : 0);
                    break;
                default:
                    if (open && p.sheet != "seal_stele_open" && p.then == null) Play(p, "seal_stele_open");
                    if (p.icon != null)
                    {
                        p.icon.sprite = open ? null : icon;
                        // Brighter as it nears opening.
                        float share = seal.kind == "time" ? Plan.caveProgress / Mathf.Max(1f, seal.seconds)
                            : seal.kind == "team" ? teamNear / TeamTime
                            : seal.stages > 1 ? (float)hits / seal.stages : 0f;
                        float light = 0.6f + 0.4f * Mathf.Clamp01(share);
                        p.icon.color = new Color(light, light, light, 1f);
                    }
                    break;
            }
            p.body.sprite = FrameOf(p.sheet, Time.time - p.since, p.frame, out bool over);
            if (over && p.then != null) Play(p, p.then);
        }
    }

    void FixedUpdate()
    {
        if (!Shut) return;
        var leader = team != null ? team.Controlled : null;
        if (leader == null) return;
        Vector2 at = island.Cell(Plan.caveMechanism);
        bool near = Vector2.Distance(leader.transform.position, at) <= NearRange;
        switch (seal.kind)
        {
            case "element":
                if (!near) break;
                if (Climate.Now is { } weather && System.Array.IndexOf(seal.weathers, weather.key) >= 0) Open();
                else if (seal.night && FindAnyObjectByType<EnemySpawner>() is { Night: true }) Open();
                break;
            case "team":
            {
                bool reader = false;
                foreach (var m in team.Members)
                    if (m != null && m.IsAlive && Vector2.Distance(m.transform.position, at) <= TeamRange && Fits(m.Stats)) reader = true;
                teamNear = reader ? teamNear + Time.fixedDeltaTime : 0f;
                if (teamNear >= TeamTime) Open();
                break;
            }
            case "time":
                if (near) Plan.caveProgress += Time.fixedDeltaTime;
                if (Plan.caveProgress >= seal.seconds) Open();
                break;
            case "braziers":
            {
                bool all = parts.Count > 0;
                foreach (var p in parts) all &= Time.time < p.litUntil;
                if (all) Open();
                break;
            }
            case "plates":
            {
                // Only the Pokémon that people play hold a plate: the AI never helps.
                int held = 0;
                foreach (var p in parts)
                {
                    var cell = arena.CellOf(p.root.transform.position);
                    p.done = arena.CellOf(leader.transform.position) == cell;
                    if (p.done) held++;
                }
                if (held >= parts.Count) Open();
                break;
            }
        }
    }

    bool Fits(PokemonStats stats)
    {
        if (stats == null) return false;
        if (System.Array.IndexOf(seal.pokemon, stats.id) >= 0) return true;
        foreach (int t in stats.types) if (System.Array.IndexOf(seal.types, t) >= 0) return true;
        return false;
    }

    void LateUpdate()
    {
        // Tests: K opens it at once.
        if (SaveData.Settings.devMode && Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame && Shut) Open();
    }

    void OnHit(MoveDef move, Vector2 at, float radius, Combatant caster)
    {
        if (!Shut || caster == null || caster.team != Team.Player) return;
        // The part hit: the nearest within reach.
        Part hit = null;
        float best = Mathf.Max(radius, 0.4f) + HitMargin;
        foreach (var p in parts)
        {
            float d = Vector2.Distance(at, p.root.transform.position + Vector3.up * 0.3f);
            if (d <= best) { best = d; hit = p; }
        }
        // An element seal also takes the blows aimed at its door (they count on the stele).
        if (hit == null && seal.kind == "element" && door != null && parts.Count > 0
            && Mathf.Abs(at.x - door.transform.position.x) <= 1.5f + Mathf.Max(radius, 0f)
            && Mathf.Abs(at.y - door.transform.position.y) <= 1f + Mathf.Max(radius, 0f)) hit = parts[0];
        if (hit == null) return;
        bool typed = System.Array.IndexOf(seal.types, move.type) >= 0;
        switch (seal.kind)
        {
            case "element":
                if (!typed) { NoEffect(hit); return; }
                hits++;
                Play(hit, "seal_stele_hit", "seal_stele");
                if (hits >= Mathf.Max(1, seal.stages)) Open();
                break;
            case "braziers":
                if (!typed) { NoEffect(hit); return; }
                hit.litUntil = Time.time + Mathf.Max(1f, seal.seconds);
                break;
            case "sequence":
            {
                if (hit.done) return;
                // The smallest shell not hit yet comes next; any other starts again.
                Part next = null;
                foreach (var p in parts) if (!p.done && (next == null || p.rank < next.rank)) next = p;
                if (hit != next)
                {
                    foreach (var p in parts) { p.done = false; NoEffect(p); }
                    return;
                }
                hit.done = true;
                if (parts.TrueForAll(p => p.done)) Open();
                break;
            }
            default:
                NoEffect(hit); // that seal does not open by hitting it
                break;
        }
    }

    static void NoEffect(Part p) => EffectPlayer.Spawn("seal_no_effect", p.root.transform.position + Vector3.up * 0.5f, Vector2.right);

    /// Opens the door: the wall goes, the mouth shows, the camera shakes and dust flies.
    public void Open()
    {
        if (Plan == null || Plan.caveOpen) return;
        var changed = Plan.OpenCave();
        if (arena != null) arena.RedrawIsland(changed);
        CameraFollow.Shake(0.15f, 0.6f);
        // The door crumbles: dust over its 3 × 2 tiles.
        var dust = EffectPlayer.Spawn("seal_dust", island.Cell(Plan.sealedCaveFront + Vector2Int.up) + Vector2.up * 0.5f, Vector2.right);
        if (dust != null) dust.GetComponent<SpriteRenderer>().sortingOrder = 2;
    }

    void OnGUI()
    {
        if (!Shut || string.IsNullOrEmpty(seal.hint)) return;
        var leader = team != null ? team.Controlled : null;
        var cam = Camera.main;
        if (leader == null || cam == null) return;
        Vector3 at = island.Cell(Plan.caveMechanism);
        if (Vector2.Distance(leader.transform.position, at) > HintRange) return;
        var screen = cam.WorldToScreenPoint(at + Vector3.up * 1.2f);
        float scale = Screen.height / 720f;
        var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(16 * scale), wordWrap = true, alignment = TextAnchor.MiddleCenter };
        string text = $"{seal.name}\n{seal.hint}";
        float width = 280f * scale, height = style.CalcHeight(new GUIContent(text), width);
        GUI.Box(new Rect(screen.x - width / 2f, Screen.height - screen.y - height, width, height), text, style);
    }
}
