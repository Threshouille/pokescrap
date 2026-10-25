using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// The sealed cave of the island (IslandPlan.sealedCave; GDD, "Grottes scellées"), added by IslandRun: draws the
/// parts of its seal in front of the door (IslandPlan.caveParts; placeholder shapes in the colours of what opens it),
/// shows its hint nearby and opens the door (IslandPlan.OpenCave, DungeonGenerator.RedrawIsland) once its condition
/// is met. element: an attack of one of its types on the mechanism ('stages' hits), one of its weathers, the night;
/// team: a Pokémon of its types (or ids) near the mechanism; braziers: all lit at once by its types, each for
/// 'seconds'; plates: all held by Pokémon that people play (never the AI); sequence: the shells hit from the smallest;
/// time: 'seconds' spent near it. K in devMode opens it at once.
public class CaveSeal : MonoBehaviour
{
    const float HintRange = 3.5f, NearRange = 10f, TeamRange = 2.5f, HitMargin = 0.7f; // tiles
    const float FlashTime = 0.25f, CycleTime = 1f, TeamTime = 1f; // seconds

    class Part
    {
        public GameObject root;
        public SpriteRenderer stone, glow;
        public float size = 1f; // shells: their size, the order to hit them in
        public float litUntil; // braziers
        public bool done; // shells hit, plates held
        public float flashUntil;
        public Color flash;
    }

    IslandRun island;
    PlayerController team;
    DungeonGenerator arena;
    CaveSeals.Seal seal;
    readonly List<Part> parts = new();
    GameObject root;
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
        var sizes = new List<float>();
        for (int k = 0; k < plan.caveParts.Count; k++) sizes.Add(0.55f + 0.25f * k);
        var rng = new System.Random(plan.sealedCave.x * 7919 + plan.sealedCave.y);
        for (int k = sizes.Count - 1; k > 0; k--) { int j = rng.Next(k + 1); (sizes[k], sizes[j]) = (sizes[j], sizes[k]); }
        for (int k = 0; k < plan.caveParts.Count; k++)
        {
            var part = new Part { size = sizes[k] };
            part.root = new GameObject(seal.kind);
            part.root.transform.SetParent(root.transform, false);
            part.root.transform.position = island.Cell(plan.caveParts[k]);
            BuildPart(part);
            parts.Add(part);
        }
    }

    void BuildPart(Part p)
    {
        if (seal.kind == "plates")
        {
            // Flat on the ground, under the Pokémon.
            Piece(p, "Dalle", Shapes.Square, new Color(0.45f, 0.42f, 0.38f), Vector2.zero, Vector2.one * 0.85f, -2, false);
            p.stone = Piece(p, "Gravure", Shapes.Square, new Color(0.3f, 0.28f, 0.25f), Vector2.zero, Vector2.one * 0.5f, -1, false);
            return;
        }
        p.root.AddComponent<SortingGroup>().sortingOrder = 1; // the Pokémon's, sorted by its foot
        if (seal.kind == "sequence")
        {
            float s = p.size;
            Piece(p, "Coquille", Shapes.Circle, new Color(0.95f, 0.85f, 0.8f), new Vector2(0f, 0.25f * s), new Vector2(0.8f, 0.6f) * s, 0, true);
            p.glow = Piece(p, "Lueur", Shapes.Circle, Color.clear, new Vector2(0f, 0.25f * s), Vector2.one * 0.9f * s, 1, false);
            p.stone = Piece(p, "Nacre", Shapes.Circle, new Color(0.9f, 0.6f, 0.65f), new Vector2(0f, 0.25f * s), new Vector2(0.35f, 0.25f) * s, 2, false);
            return;
        }
        if (seal.kind == "braziers")
        {
            Piece(p, "Pied", Shapes.Square, new Color(0.3f, 0.28f, 0.27f), new Vector2(0f, 0f), new Vector2(0.3f, 0.7f), 0, true);
            Piece(p, "Vasque", Shapes.Square, new Color(0.4f, 0.37f, 0.35f), new Vector2(0f, 0.38f), new Vector2(0.8f, 0.22f), 1, false);
            p.glow = Piece(p, "Lueur", Shapes.Circle, Color.clear, new Vector2(0f, 0.65f), Vector2.one * 0.9f, 2, false);
            p.stone = Piece(p, "Flamme", Shapes.Circle, Color.clear, new Vector2(0f, 0.62f), new Vector2(0.4f, 0.55f), 3, false);
            return;
        }
        Piece(p, "Socle", Shapes.Square, new Color(0.35f, 0.3f, 0.27f), new Vector2(0f, -0.1f), new Vector2(0.8f, 0.55f), 0, true);
        Piece(p, "Bord", Shapes.Square, new Color(0.5f, 0.45f, 0.4f), new Vector2(0f, 0.18f), new Vector2(0.9f, 0.12f), 1, false);
        p.glow = Piece(p, "Lueur", Shapes.Circle, Color.clear, new Vector2(0f, 0.42f), Vector2.one * 0.75f, 2, false);
        p.stone = Piece(p, "Pierre", Shapes.Circle, Color.gray, new Vector2(0f, 0.42f), Vector2.one * 0.4f, 3, false);
    }

    static SpriteRenderer Piece(Part p, string name, Sprite sprite, Color colour, Vector2 offset, Vector2 scale, int order, bool foot)
    {
        var r = new GameObject(name).AddComponent<SpriteRenderer>();
        r.transform.SetParent(p.root.transform, false);
        r.transform.localPosition = offset;
        r.transform.localScale = scale;
        r.sprite = sprite;
        r.color = colour;
        r.sortingOrder = order;
        if (foot) r.spriteSortPoint = SpriteSortPoint.Pivot;
        return r;
    }

    // The colours of what opens it (types, weathers, night), in turn.
    List<Color> Colours()
    {
        var list = new List<Color>();
        if (seal == null) return list;
        foreach (int type in seal.types) list.Add(GameData.TypeColor(type));
        foreach (var w in seal.weathers)
            list.Add(w switch
            {
                "sun" => new Color(1f, 0.8f, 0.25f),
                "rain" => new Color(0.3f, 0.5f, 1f),
                "sand" => new Color(0.85f, 0.7f, 0.4f),
                "hail" => new Color(0.8f, 0.92f, 1f),
                _ => Color.white,
            });
        if (seal.night) list.Add(new Color(0.4f, 0.4f, 0.8f));
        return list;
    }

    void Update()
    {
        if (root == null || seal == null) return;
        if (root.activeSelf == island.Away) root.SetActive(!island.Away); // not in another room
        bool open = Plan != null && Plan.caveOpen;
        var colours = Colours();
        var colour = colours.Count > 0 ? colours[(int)(Time.time / CycleTime) % colours.Count] : new Color(0.7f, 0.65f, 0.55f);
        foreach (var p in parts)
        {
            if (p.stone == null) continue;
            bool flashing = Time.time < p.flashUntil;
            switch (seal.kind)
            {
                case "braziers":
                {
                    bool lit = open || Time.time < p.litUntil;
                    float flicker = 0.85f + 0.15f * Mathf.Sin(Time.time * 17f + p.root.transform.position.x);
                    p.stone.color = flashing ? p.flash : lit ? new Color(1f, 0.55f + 0.2f * flicker, 0.15f, 1f) : Color.clear;
                    p.stone.transform.localScale = new Vector3(0.4f, 0.55f * flicker, 1f);
                    p.glow.color = lit ? new Color(1f, 0.5f, 0.1f, 0.35f * flicker) : new Color(colour.r, colour.g, colour.b, 0.15f);
                    break;
                }
                case "plates":
                    p.stone.color = open || p.done ? new Color(0.95f, 0.85f, 0.4f) : flashing ? p.flash : new Color(0.3f, 0.28f, 0.25f);
                    break;
                case "sequence":
                    p.stone.color = flashing ? p.flash : new Color(0.9f, 0.6f, 0.65f);
                    p.glow.color = open || p.done ? new Color(1f, 0.9f, 0.95f, 0.5f) : Color.clear;
                    break;
                default:
                {
                    p.stone.color = flashing ? p.flash : open ? Color.Lerp(colour, Color.white, 0.3f) : Color.Lerp(colour, Color.black, 0.25f);
                    // A glow that grows as it nears opening, steady once open.
                    float share = seal.kind == "time" ? Plan.caveProgress / Mathf.Max(1f, seal.seconds)
                        : seal.kind == "team" ? teamNear / TeamTime
                        : seal.stages > 1 ? (float)hits / seal.stages : 0f;
                    float pulse = open ? 0.55f : Mathf.Clamp01(share) * (0.35f + 0.15f * Mathf.Sin(Time.time * 6f));
                    p.glow.color = new Color(colour.r, colour.g, colour.b, pulse);
                    break;
                }
            }
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
        if (hit == null) return;
        bool typed = System.Array.IndexOf(seal.types, move.type) >= 0;
        switch (seal.kind)
        {
            case "element":
                if (!typed) { Flash(hit, Grey); return; }
                hits++;
                Flash(hit, Color.white);
                if (hits >= Mathf.Max(1, seal.stages)) Open();
                break;
            case "braziers":
                if (!typed) { Flash(hit, Grey); return; }
                hit.litUntil = Time.time + Mathf.Max(1f, seal.seconds);
                break;
            case "sequence":
            {
                if (hit.done) return;
                // The smallest shell not hit yet comes next; any other starts again.
                Part next = null;
                foreach (var p in parts) if (!p.done && (next == null || p.size < next.size)) next = p;
                if (hit != next)
                {
                    foreach (var p in parts) { p.done = false; Flash(p, Grey); }
                    return;
                }
                hit.done = true;
                Flash(hit, Color.white);
                if (parts.TrueForAll(p => p.done)) Open();
                break;
            }
            default:
                Flash(hit, Grey); // that seal does not open by hitting it
                break;
        }
    }

    static readonly Color Grey = new(0.45f, 0.45f, 0.45f);

    static void Flash(Part p, Color colour)
    {
        p.flash = colour;
        p.flashUntil = Time.time + FlashTime;
    }

    /// Opens the door: the wall goes, the mouth shows, the camera shakes and dust flies.
    public void Open()
    {
        if (Plan == null || Plan.caveOpen) return;
        var changed = Plan.OpenCave();
        if (arena != null) arena.RedrawIsland(changed);
        CameraFollow.Shake(0.15f, 0.6f);
        StartCoroutine(Dust(island.Cell(Plan.sealedCaveFront + Vector2Int.up * 2)));
    }

    IEnumerator Dust(Vector2 at)
    {
        var puffs = new List<(SpriteRenderer r, Vector2 velocity)>();
        for (int k = 0; k < 14; k++)
        {
            var r = new GameObject("Poussière").AddComponent<SpriteRenderer>();
            r.sprite = Shapes.Circle;
            r.sortingOrder = 2;
            r.transform.position = at + new Vector2(Random.Range(-1.4f, 1.4f), Random.Range(-1f, 0.6f));
            r.transform.localScale = Vector3.one * Random.Range(0.4f, 0.8f);
            puffs.Add((r, new Vector2(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.4f))));
        }
        const float time = 0.8f;
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            foreach (var (r, velocity) in puffs)
            {
                if (r == null) continue;
                r.transform.position += (Vector3)(velocity * Time.deltaTime);
                r.transform.localScale += Vector3.one * Time.deltaTime * 0.6f;
                r.color = new Color(0.75f, 0.68f, 0.58f, 0.7f * (1f - t / time));
            }
            yield return null;
        }
        foreach (var (r, _) in puffs) if (r != null) Destroy(r.gameObject);
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
