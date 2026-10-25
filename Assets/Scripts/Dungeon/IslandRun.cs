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
    public bool Away { get; set; }

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
        foreach (var p in Plan.passages)
        {
            if (p.from == zone) AddExit(p.a, p.to, p.b);
            if (p.to == zone) AddExit(p.b, p.from, p.a);
        }
        foreach (var m in Plan.markers)
            if (Plan.region[Plan.Index(m.x, m.y)] == zone) AddMarker(m, "Portail (test)", new Color(1f, 0.85f, 0.3f));
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

    // A passage end: a dark square with a light one inside (like the test stairs), in the colour of where it leads.
    void AddExit(Vector2Int tile, int zone, Vector2Int arrival)
    {
        var go = new GameObject($"Passage vers {Plan.ZoneName(zone)}");
        go.transform.position = Cell(tile);
        var outer = go.AddComponent<SpriteRenderer>();
        outer.sprite = Shapes.Square;
        outer.color = new Color(0.1f, 0.1f, 0.12f);
        outer.sortingOrder = -1; // under the light square inside (a tie would hide it)
        var inner = new GameObject("Marches").AddComponent<SpriteRenderer>();
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = Vector3.one * 0.6f;
        inner.sprite = Shapes.Square;
        inner.color = zone == IslandPlan.Centre ? new Color(0.75f, 0.5f, 1f) : new Color(1f, 0.85f, 0.3f);
        exits.Add((go, zone, arrival));
    }

    static readonly Dictionary<string, Sprite> propSprites = new();
    Transform propRoot;

    // The zone's decor elements, standing on their foot, sorted with the Pokémon (lower on screen in front).
    void PlaceProps(int zone)
    {
        if (propRoot != null) Destroy(propRoot.gameObject);
        propRoot = new GameObject("Éléments de décor").transform;
        foreach (var (key, at) in Plan.decorProps)
        {
            var tile = Vector2Int.FloorToInt(at);
            if (Plan.region[Plan.Index(tile.x, tile.y)] != zone) continue;
            var sprite = PropSprite(key);
            if (sprite == null) continue;
            var go = new GameObject(key);
            go.transform.SetParent(propRoot, false);
            go.transform.position = Cell(tile) + (at - tile - new Vector2(0.5f, 0.5f));
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            Wind.Sway(r, key); // trees, bushes and flowers sway
            r.sortingOrder = 1; // the Pokémon's
            r.spriteSortPoint = SpriteSortPoint.Pivot;
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
        var go = new GameObject(name);
        go.transform.position = Cell(tile);
        var outer = go.AddComponent<SpriteRenderer>();
        outer.sprite = Shapes.Square;
        outer.color = new Color(0.1f, 0.1f, 0.12f);
        outer.sortingOrder = -1; // under the light square inside (a tie would hide it)
        var inner = new GameObject("Marches").AddComponent<SpriteRenderer>();
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = Vector3.one * 0.6f;
        inner.sprite = Shapes.Square;
        inner.color = colour;
        markers.Add(go);
    }

    // World position of an island tile of the current zone.
    public Vector2 Cell(Vector2Int tile)
    {
        var c = tile - arena.IslandOrigin + arena.Interior.min;
        return arena.CentreOf(c.x, c.y);
    }

    // Island tile under a world position.
    Vector2Int TileOf(Vector2 position) => arena.CellOf(position) - arena.Interior.min + arena.IslandOrigin;

    public void End()
    {
        if (GetComponent<SecretStairs>() is { } secret) secret.Clear();
        if (GetComponent<CaveSeal>() is { } cave) cave.Clear();
        if (GetComponent<CaveStairs>() is { } caveStairs) caveStairs.Clear();
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
        // Passage ends: armed once the controlled Pokémon stands clear of them all.
        Vector2 at = team.Controlled.transform.position;
        float closest = float.MaxValue;
        foreach (var e in exits)
        {
            float d = Vector2.Distance(at, e.marker.transform.position);
            closest = Mathf.Min(closest, d);
            if (armed && d <= PassageReach)
            {
                int zone = e.zone;
                var arrival = e.arrival;
                armed = false;
                LoadingScreen.Run(Plan.ZoneName(zone), () => Build(zone, arrival));
                return;
            }
        }
        if (closest > PassageLeave) armed = true;
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
        var rect = new Rect(Screen.width - 8f * s - bounds.width * scale, 34f * s, bounds.width * scale, bounds.height * scale);
        var uv = new Rect(bounds.x / (float)Plan.size, bounds.y / (float)Plan.size, bounds.width / (float)Plan.size, bounds.height / (float)Plan.size);
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(rect, map, uv);
        // Passage ends, once seen; the team.
        foreach (var e in exits)
        {
            var tile = TileOf(e.marker.transform.position);
            if (!revealed[Plan.Index(tile.x, tile.y)]) continue;
            GUI.color = e.zone == IslandPlan.Centre ? new Color(0.75f, 0.5f, 1f) : new Color(1f, 0.85f, 0.3f);
            Dot(rect, scale, tile - bounds.min, 3f * s);
        }
        GUI.color = new Color(1f, 0.85f, 0.3f);
        foreach (var m in Plan.markers)
            if (revealed[Plan.Index(m.x, m.y)]) Dot(rect, scale, m - bounds.min, 3f * s);
        // Tests: the secret stairs.
        if ((revealAll || SaveData.Settings.devMode) && GetComponent<SecretStairs>() is { } secret && secret.Waiting(Zone))
        {
            GUI.color = new Color(0.3f, 1f, 0.9f);
            Dot(rect, scale, Plan.secret - bounds.min, 3.5f * s);
        }
        if ((revealAll || SaveData.Settings.devMode) && Plan.sealedCave.x >= 0 && Plan.region[Plan.Index(Plan.sealedCave.x, Plan.sealedCave.y)] == Zone)
        {
            GUI.color = new Color(1f, 0.5f, 0f);
            Dot(rect, scale, Plan.sealedCave - bounds.min, 3.5f * s);
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

    static void Dot(Rect rect, float scale, Vector2 tiles, float half) =>
        GUI.DrawTexture(new Rect(rect.x + tiles.x * scale - half, rect.yMax - tiles.y * scale - half, 2f * half, 2f * half), Texture2D.whiteTexture);
}
