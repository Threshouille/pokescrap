using System.Collections.Generic;
using UnityEngine;

/// The island (IslandPlan) in play, one zone at a time (a quarter or the centre, each a map of its own built by
/// DungeonGenerator, walled off from the others), the sky around it (camera background), the team landing on the first
/// quarter's coast. Walking onto a passage end loads the zone at its other end behind a loading screen, both ways. The
/// mini-map (top right) shows the current zone, in the fog at first and revealed around the team (kept for the whole
/// island): walls, floor in its sub-biome's colour, water, lava and void; the passage ends marked once seen. Seals,
/// Time Gears and encounters come later (GDD, "L'île flottante").
public class IslandRun : MonoBehaviour
{
    const float RevealRadius = 12f, RevealEvery = 0.2f, MapSize = 140f;
    const float PassageReach = 0.8f, PassageLeave = 1.6f; // tiles: walking onto a passage end; stepping off it again
    static readonly Color Sky = new(0.42f, 0.62f, 0.86f);

    public IslandPlan Plan { get; private set; }

    /// Tests: each zone's mini-map comes fully revealed, its passage ends shown (also with devMode in meta.json); the
    /// secret stairs marked too.
    public bool revealAll;

    /// The team is away from the island (a secret room, SecretStairs): no passage ends, no mini-map.
    public bool Away
    {
        get => away;
        set
        {
            away = value;
            // In a room, what belongs to the island's zone is not drawn: its decor, passage ends and landmarks.
            if (propRoot != null) propRoot.gameObject.SetActive(!away);
            foreach (var e in exits) if (e.marker != null) e.marker.SetActive(!away);
            foreach (var m in markers) if (m != null) m.SetActive(!away);
        }
    }
    bool away;

    /// Marks on the mini-map whatever was revealed (a gear fragment's hint, TimeGears), in island tiles.
    public readonly List<(Vector2Int tile, Color colour)> hints = new();

    /// Called once a zone is built (the team in place).
    public event System.Action Built;

    /// The zone being played: a quarter (0 to 3) or the centre (IslandPlan.Centre).
    public int Zone { get; private set; } = -1;

    /// The quarter the controlled Pokémon stands in (0 to 3), or -1 elsewhere.
    public int Quarter { get; private set; } = -1;

    /// Sub-biome under the controlled Pokémon, or null.
    public IslandPlan.SubBiome Biome { get; private set; }

    DungeonGenerator arena;
    PlayerController team;
    TimeGears gears;
    Texture2D map;
    Color32[] pixels;
    bool[] revealed;
    float nextReveal;
    bool dirty;
    Color previousBackground;
    RectInt bounds; // the current zone on the island
    readonly List<(GameObject marker, int zone, Vector2Int arrival)> exits = new(); // the passage ends of the zone
    readonly List<GameObject> markers = new(); // landmarks of the zone (IslandPlan.markers)
    bool armed; // the passage ends only work once the controlled Pokémon has stepped off them

    public void Begin(System.Random rng) => Begin(IslandPlan.Generate(rng));

    /// Plays a plan made elsewhere (the test quarter).
    public void Begin(IslandPlan plan)
    {
        arena = DungeonGenerator.Current;
        team = FindAnyObjectByType<PlayerController>();
        Plan = plan;
        arena.room = null;
        arena.level = null;
        arena.island = Plan;
        if (Camera.main != null)
        {
            previousBackground = Camera.main.backgroundColor;
            Camera.main.backgroundColor = Sky;
        }
        if (map != null) Destroy(map);
        map = new Texture2D(Plan.size, Plan.size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        pixels = new Color32[Plan.size * Plan.size];
        revealed = new bool[pixels.Length];
        Away = false;
        if (GetComponent<SecretStairs>() == null) gameObject.AddComponent<SecretStairs>();
        if (GetComponent<CaveSeal>() == null) gameObject.AddComponent<CaveSeal>();
        if (GetComponent<CaveStairs>() == null) gameObject.AddComponent<CaveStairs>();
        if (GetComponent<IslandPopulation>() == null) gameObject.AddComponent<IslandPopulation>();
        gears = GetComponent<TimeGears>();
        if (gears == null) gears = gameObject.AddComponent<TimeGears>(); // after the population: its places are kept clear
        gears.Bind(this);
        gears.Prepare(plan); // its bridges over the sky before the first zone is built
        // After the gears (their places are made first when a zone is built).
        if (GetComponent<KeyDungeon>() == null) gameObject.AddComponent<KeyDungeon>();
        if (GetComponent<BossPortal>() == null) gameObject.AddComponent<BossPortal>();
        Build(Plan.order[0], null);
    }

    /// Builds a zone again, the team at that island tile (back from a secret room).
    public void Return(int zone, Vector2Int arrival)
    {
        arena.room = null;
        arena.level = null;
        arena.island = Plan;
        Away = false;
        Build(zone, arrival);
    }

    // Builds a zone and places the team at its arrival tile (island tile; the zone's default when null).
    void Build(int zone, Vector2Int? arrival)
    {
        foreach (var e in exits) if (e.marker != null) Destroy(e.marker);
        exits.Clear();
        foreach (var m in markers) if (m != null) Destroy(m);
        markers.Clear();
        foreach (var zoneArea in FindObjectsByType<global::Zone>()) Destroy(zoneArea.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        Zone = zone;
        bounds = Plan.ZoneBounds(zone);
        arena.islandZone = zone;
        arena.islandArrival = arrival;
        arena.Generate();
        if (team != null) team.PrepareForNextArena(arena.SpawnPoint, 0f);
        AddExits(zone);
        PlaceProps(zone);

        if (revealAll || SaveData.Settings.devMode)
            for (int y = bounds.yMin; y < bounds.yMax; y++)
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    int i = Plan.Index(x, y);
                    if (Plan.region[i] != zone || revealed[i]) continue;
                    revealed[i] = true;
                    pixels[i] = Colour(i);
                }
        armed = false;
        dirty = true;
        nextReveal = 0f;
        Built?.Invoke();
    }

    // The zone's passage ends (its gates), and the test quarter's portals.
    void AddExits(int zone)
    {
        foreach (var p in Plan.passages)
        {
            if (!Plan.Active(p)) continue; // the centre: only from the last quarter
            if (p.from == zone) AddExit(p.a, p.to, p.b);
            if (p.to == zone) AddExit(p.b, p.from, p.a);
        }
        if (testRedraw != null && Plan.markers.Count >= 2)
        {
            // The test quarter's portals as real passages: to a new draw of the test quarter.
            AddExit(Plan.markers[0], TestPrevious, Plan.markers[0]);
            AddExit(Plan.markers[1], TestNext, Plan.markers[1]);
        }
        else
            foreach (var m in Plan.markers)
                if (Plan.region[Plan.Index(m.x, m.y)] == zone) AddMarker(m, "Portail (test)", new Color(1f, 0.85f, 0.3f));
    }

    /// Dev (Ctrl+G): the zone's gates shut again, as if never opened, so that their opening plays once more.
    public void ReplayGates()
    {
        foreach (var e in exits)
        {
            if (gears != null) gears.Forget(Zone, e.zone);
            if (e.marker != null) Destroy(e.marker);
        }
        exits.Clear();
        foreach (var m in markers) if (m != null) Destroy(m);
        markers.Clear();
        AddExits(Zone);
    }

    /// The test quarter (sandbox room 5): a new draw of it, behind its portals (null elsewhere: portals inert).
    public System.Func<IslandPlan> testRedraw;
    const int TestPrevious = -11, TestNext = -10; // the test quarter's portals, as zones

    string Towards(int zone) => zone == TestNext ? "le quartier suivant (nouveau tirage)" : zone == TestPrevious ? "le quartier précédent (nouveau tirage)" : Plan.ZoneName(zone);

    // Whether the way to a zone asks for a gear: all of them start shut (TimeGears), the test quarter's portals too.
    bool GatedTo(int zone) => gears != null && gears.Gated(Zone, zone);

    // Whether the way to a zone is open (TimeGears: opened by a gear, the player's choice, both ways).
    bool MayPass(int zone) => gears == null || gears.CanPass(Zone, zone);

    // A passage end: a quarter's gate (EntranceArt), open once its Time Gear is found.
    void AddExit(Vector2Int tile, int zone, Vector2Int arrival)
    {
        var go = QuarterGate.Make($"Passage vers {Towards(zone)}", Cell(tile), GatedTo(zone), zone == IslandPlan.Centre).gameObject;
        exits.Add((go, zone, arrival));
    }

    static readonly Dictionary<string, Sprite> propSprites = new();
    Transform propRoot;

    // The zone's decor elements, standing on their foot, sorted with the Pokémon (lower on screen in front).
    void PlaceProps(int zone)
    {
        if (propRoot != null) Destroy(propRoot.gameObject);
        propRoot = new GameObject("Éléments de décor").transform;
        var fade = propRoot.gameObject.AddComponent<PropFade>(); // trees see-through over the team
        foreach (var (key, at) in Plan.decorProps)
        {
            var tile = Vector2Int.FloorToInt(at);
            if (Plan.region[Plan.Index(tile.x, tile.y)] != zone) continue;
            if (arena.IsRampart(arena.CellOf(Cell(tile)))) continue; // under the rampart made straight at a gate
            var sprite = PropSprite(key);
            if (sprite == null) continue;
            var go = new GameObject(key);
            go.transform.SetParent(propRoot, false);
            go.transform.position = Cell(tile) + (at - tile - new Vector2(0.5f, 0.5f));
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            Wind.Sway(r, key); // trees, bushes and flowers sway
            // A trunk stops the Pokémon where it is drawn (its tiles only steer the AI's paths: obstacle "trunk").
            if (IslandPlan.TrunkOf(key) is { } trunk)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(trunk[0], trunk[1]) / 24f;
                box.offset = new Vector2(trunk[2], trunk[1] / 2f) / 24f;
            }
            r.sortingOrder = 1; // the Pokémon's
            r.spriteSortPoint = SpriteSortPoint.Pivot;
            fade.Add(r);
        }
    }

    static Sprite PropSprite(string key)
    {
        if (propSprites.TryGetValue(key, out var sprite) && sprite != null) return sprite;
        var texture = Resources.Load<Texture2D>("MapProps/" + key);
        sprite = texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0f), 24f, 0, SpriteMeshType.FullRect) : null;
        propSprites[key] = sprite;
        return sprite;
    }

    // A landmark that looks like a passage end but leads nowhere (yet): the test quarter's portals.
    void AddMarker(Vector2Int tile, string name, Color colour)
    {
        var gate = QuarterGate.Make(name, Cell(tile));
        gate.Set(false, true); // leads nowhere yet: always shut
        markers.Add(gate.gameObject);
    }

    // World position of an island tile of the current zone.
    public Vector2 Cell(Vector2Int tile)
    {
        var c = tile - arena.IslandOrigin + arena.Interior.min;
        return arena.CentreOf(c.x, c.y);
    }

    // Island tile under a world position.
    /// That island tile is revealed on the mini-map (seen by the team).
    public bool Revealed(Vector2Int tile) =>
        Plan != null && revealed != null && tile.x >= 0 && tile.y >= 0 && tile.x < Plan.size && tile.y < Plan.size && revealed[Plan.Index(tile.x, tile.y)];

    /// The island tile under a world position.
    public Vector2Int TileOf(Vector2 position) => arena.CellOf(position) - arena.Interior.min + arena.IslandOrigin;

    public void End()
    {
        if (GetComponent<SecretStairs>() is { } secret) secret.Clear();
        if (GetComponent<CaveSeal>() is { } cave) cave.Clear();
        if (GetComponent<CaveStairs>() is { } caveStairs) caveStairs.Clear();
        if (GetComponent<IslandPopulation>() is { } population) population.Clear();
        if (GetComponent<TimeGears>() is { } timeGears) timeGears.Clear();
        if (GetComponent<KeyDungeon>() is { } dungeon) dungeon.Clear();
        if (GetComponent<BossPortal>() is { } portal) portal.Clear();
        hints.Clear();
        Away = false;
        if (propRoot != null) Destroy(propRoot.gameObject);
        foreach (var e in exits) if (e.marker != null) Destroy(e.marker);
        exits.Clear();
        foreach (var m in markers) if (m != null) Destroy(m);
        markers.Clear();
        Zone = -1;
        if (arena != null)
        {
            arena.island = null;
            arena.islandZone = -1;
            arena.islandArrival = null;
        }
        if (Camera.main != null && Plan != null) Camera.main.backgroundColor = previousBackground;
        Plan = null;
        if (map != null) Destroy(map);
        map = null;
    }

    void FixedUpdate()
    {
        if (Plan == null || Away || team == null || team.Controlled == null || LoadingScreen.Busy) return;
        // Passage ends: armed once the controlled Pokémon stands clear of them all (out of their gateways).
        Vector2 at = team.Controlled.transform.position;
        bool near = false;
        foreach (var e in exits)
        {
            float d = Vector2.Distance(at, e.marker.transform.position);
            // Locked until the quarter's Time Gear is found (TimeGears): grey, and a word when stepped on.
            bool open = MayPass(e.zone);
            var gate = e.marker.GetComponent<QuarterGate>();
            // Every gate starts shut; the team brings the quarter's gear to the dolmen of the one it chooses: it opens
            // before its eyes (QuarterGate.Unlock), for good and both ways, and that gear opens no other; without a gear
            // to spend, the dolmen refuses.
            bool onEnd = gate != null ? gate.InGateway(at, PassageReach) : d <= PassageReach;
            near |= gate != null ? gate.InGateway(at, PassageLeave) : d <= PassageLeave;
            bool atDolmen = gate != null && (Vector2.Distance(at, gate.DolmenAt) <= EntranceArt.Data.gateReach || onEnd);
            if (!open && gate != null && atDolmen && !Combatant.InCutScene)
            {
                if (gears.Open(Zone, e.zone))
                {
                    open = true;
                    gate.Unlock(team.Controlled, Towards(e.zone)); // the team goes through once it has played, if still there
                }
                else
                {
                    gears.Refuse(Zone, e.zone);
                    gate.Deny();
                }
            }
            gate?.Set(open, Revealed(TileOf(e.marker.transform.position)), gears != null ? gears.Found : 0);
            if (gate != null && gate.Unlocking)
            {
                armed = true; // once it has played, the team at the gate goes through (even if it was refused before)
                continue;
            }
            if (armed && onEnd && !open)
            {
                armed = false;
                gears.Refuse(Zone, e.zone);
                gate?.Refuse();
                continue;
            }
            if (armed && onEnd && (e.zone == TestNext || e.zone == TestPrevious) && testRedraw != null)
            {
                armed = false;
                var next = testRedraw;
                LoadingScreen.Run("Quart d'île de test", () => Begin(next()));
                return;
            }
            if (armed && onEnd)
            {
                int zone = e.zone;
                var arrival = e.arrival;
                armed = false;
                LoadingScreen.Run(Plan.ZoneName(zone), () => Build(zone, arrival));
                return;
            }
        }
        if (!near) armed = true;
        if (Time.time < nextReveal) return;
        nextReveal = Time.time + RevealEvery;
        var cell = TileOf(at);
        if (cell.x < 0 || cell.y < 0 || cell.x >= Plan.size || cell.y >= Plan.size) return;
        int here = Plan.Index(cell.x, cell.y);
        Quarter = Plan.region[here] >= 0 && Plan.region[here] < 4 ? Plan.region[here] : -1;
        Biome = Plan.biome[here];
        int r = Mathf.CeilToInt(RevealRadius);
        for (int x = cell.x - r; x <= cell.x + r; x++)
            for (int y = cell.y - r; y <= cell.y + r; y++)
            {
                if (x < 0 || y < 0 || x >= Plan.size || y >= Plan.size) continue;
                if ((x - cell.x) * (x - cell.x) + (y - cell.y) * (y - cell.y) > RevealRadius * RevealRadius) continue;
                int i = Plan.Index(x, y);
                if (revealed[i] || Plan.region[i] != Zone) continue; // the other zones are behind the walls
                revealed[i] = true;
                pixels[i] = Colour(i);
                dirty = true;
            }
    }

    Color32 Colour(int i)
    {
        int region = Plan.region[i];
        if (region == IslandPlan.Void) return new Color32(0, 0, 0, 0);
        if (region == IslandPlan.Moat) return new Color32(10, 10, 20, 255);
        if (region == IslandPlan.Centre) return new Color32(140, 90, 185, 255);
        if (region == IslandPlan.Border || Plan.wall[i]) return new Color32(55, 50, 50, 255);
        switch (Plan.obstacle[i])
        {
            case "water": return new Color32(50, 110, 230, 255);
            case "lava": return new Color32(240, 90, 30, 255);
            case "void": return new Color32(10, 10, 20, 255);
            case null: break;
            default: return new Color32(150, 100, 50, 255); // bridges, ice, quicksand, rock
        }
        if (Plan.roof != null && Plan.roof[i]) return new Color32(70, 40, 90, 255); // a cave
        if (Plan.decor != null && Plan.decor[i] > 0) return new Color32(150, 110, 70, 255); // a road
        return ColorUtility.TryParseHtmlString(Plan.biome[i]?.color ?? "", out var c) ? (Color32)c : new Color32(170, 170, 160, 255);
    }

    void OnGUI()
    {
        if (Plan == null || map == null || Away) return;
        if (dirty)
        {
            map.SetPixels32(pixels);
            map.Apply();
            dirty = false;
        }
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        // The current zone only.
        float scale = MapSize / Mathf.Max(bounds.width, bounds.height) * s;
        // Top right, below the lots panel (HudLayout): never over it.
        var rect = new Rect(Screen.width - 8f * s - bounds.width * scale, Mathf.Max(HudLayout.RightTop(s), HudLayout.BelowRightColumn(s)),
            bounds.width * scale, bounds.height * scale);
        var uv = new Rect(bounds.x / (float)Plan.size, bounds.y / (float)Plan.size, bounds.width / (float)Plan.size, bounds.height / (float)Plan.size);
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(rect, map, uv);
        // Passage ends and entrances, once seen (their icons, EntranceArt); the team.
        GUI.color = Color.white;
        foreach (var e in exits)
        {
            var tile = TileOf(e.marker.transform.position);
            if (!revealed[Plan.Index(tile.x, tile.y)]) continue;
            bool open = e.marker.GetComponent<QuarterGate>() is { Open: true };
            IconAt(rect, scale, tile - bounds.min, open ? EntranceArt.Icon.GateOpen : EntranceArt.Icon.GateShut, s);
        }
        if (testRedraw == null)
            foreach (var m in Plan.markers)
                if (revealed[Plan.Index(m.x, m.y)]) IconAt(rect, scale, m - bounds.min, EntranceArt.Icon.GateShut, s);
        foreach (var stairs in GetComponents<IslandStairs>())
            if (stairs.Found) IconAt(rect, scale, stairs.MapTile - bounds.min, stairs.MapIcon, s);
        if (Plan.sealedCave.x >= 0 && Plan.region[Plan.Index(Plan.sealedCave.x, Plan.sealedCave.y)] == Zone && revealed[Plan.Index(Plan.sealedCave.x, Plan.sealedCave.y)])
            IconAt(rect, scale, Plan.sealedCave - bounds.min, EntranceArt.Icon.Sealed, s);
        // Tests: the secret stairs.
        if ((revealAll || SaveData.Settings.devMode) && GetComponent<SecretStairs>() is { } secret && secret.Waiting(Zone))
        {
            GUI.color = new Color(0.3f, 1f, 0.9f);
            Dot(rect, scale, Plan.secret - bounds.min, 3.5f * s);
        }
        // Tests: the key dungeon and the boss islet's portal (TimeGears).
        if (revealAll || SaveData.Settings.devMode)
            foreach (var stairs in new IslandStairs[] { GetComponent<KeyDungeon>(), GetComponent<BossPortal>() })
                if (stairs != null && stairs.Waiting(Zone))
                {
                    GUI.color = stairs.MapColour;
                    Dot(rect, scale, stairs.MapTile - bounds.min, 3.5f * s);
                }
        if ((revealAll || SaveData.Settings.devMode) && Plan.sealedCave.x >= 0 && Plan.region[Plan.Index(Plan.sealedCave.x, Plan.sealedCave.y)] == Zone)
        {
            GUI.color = new Color(1f, 0.5f, 0f);
            Dot(rect, scale, Plan.sealedCave - bounds.min, 3.5f * s);
        }
        foreach (var (tile, colour) in hints)
        {
            if (Plan.region[Plan.Index(tile.x, tile.y)] != Zone) continue;
            GUI.color = colour;
            Dot(rect, scale, tile - bounds.min, 3f * s);
        }
        GUI.color = new Color(0.75f, 0.5f, 1f);
        foreach (var c in Plan.caves)
            if (revealed[Plan.Index(c.x, c.y)]) Dot(rect, scale, c - bounds.min, 3f * s);
        if (team != null && team.Controlled != null)
        {
            GUI.color = Color.white;
            Dot(rect, scale, TileOf(team.Controlled.transform.position) - bounds.min, 2.5f * s);
        }
        GUI.color = previous;
    }

    // An entrance's icon on the mini-map (entrance_icons), centred on its tile.
    static void IconAt(Rect rect, float scale, Vector2 tiles, EntranceArt.Icon icon, float s)
    {
        var sprite = EntranceArt.IconSprite(icon);
        float half = EntranceArt.Data.mapIcon * s / 2f;
        var at = new Rect(rect.x + tiles.x * scale - half, rect.yMax - tiles.y * scale - half, 2f * half, 2f * half);
        if (sprite == null) { GUI.DrawTexture(at, Texture2D.whiteTexture); return; }
        var t = sprite.texture;
        var r = sprite.textureRect;
        GUI.DrawTextureWithTexCoords(at, t, new Rect(r.x / t.width, r.y / t.height, r.width / t.width, r.height / t.height));
    }

    static void Dot(Rect rect, float scale, Vector2 tiles, float half) =>
        GUI.DrawTexture(new Rect(rect.x + tiles.x * scale - half, rect.yMax - tiles.y * scale - half, 2f * half, 2f * half), Texture2D.whiteTexture);
}
