using System.Collections.Generic;
using UnityEngine;

/// A level (LevelPlan) in play: the whole map is built at once by DungeonGenerator and crossed without any fade.
/// The mini-map (top right) starts in the fog and is revealed around the team as it explores: floor, walls,
/// obstacles in their colours, and the special zones (boss, sub-boss, challenge, rest, monster house) marked once
/// seen. Enemies, challenges and the barriers of the boss come later (GDD, "Niveaux continus", step 4).
public class LevelRun : MonoBehaviour
{
    const float RevealRadius = 11f, RevealEvery = 0.2f, MapWidth = 150f, MapMaxHeight = 110f;

    public LevelPlan Plan { get; private set; }

    /// The zone the controlled Pokémon stands in, or null on a path.
    public LevelPlan.Zone Here { get; private set; }

    DungeonGenerator arena;
    PlayerController team;
    Texture2D map;
    Color32[] pixels;
    bool[] revealed;
    readonly HashSet<LevelPlan.Zone> seenZones = new();
    float nextReveal;
    bool dirty;

    static readonly Color32 Fog = new(0, 0, 0, 0), Floor = new(170, 170, 160, 255), Wall = new(70, 60, 55, 255);

    static readonly Dictionary<string, Color> ZoneColours = new()
    {
        { "start", new Color(0.45f, 0.7f, 1f) }, { "monster_house", new Color(1f, 0.85f, 0.3f) },
        { "challenge", new Color(0.75f, 0.45f, 1f) }, { "rest", new Color(0.45f, 0.9f, 0.45f) },
        { "subboss", new Color(1f, 0.6f, 0.2f) }, { "boss", new Color(1f, 0.25f, 0.2f) },
    };

    /// A new level, the team at its start.
    public void Begin(System.Random rng)
    {
        arena = DungeonGenerator.Current;
        team = FindAnyObjectByType<PlayerController>();
        Plan = LevelPlan.Generate(rng);
        arena.room = null;
        arena.level = Plan;
        arena.Generate();
        if (team != null) team.PrepareForNextArena(arena.SpawnPoint, 0f);
        var inside = arena.Interior;
        if (map != null) Destroy(map);
        map = new Texture2D(inside.width, inside.height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        pixels = new Color32[inside.width * inside.height];
        revealed = new bool[pixels.Length];
        seenZones.Clear();
        dirty = true;
        nextReveal = 0f;
    }

    /// Leaves the level (the arena is rebuilt by whoever comes next).
    public void End()
    {
        if (arena != null) arena.level = null;
        Plan = null;
        Here = null;
        if (map != null) Destroy(map);
        map = null;
    }

    void FixedUpdate()
    {
        if (Plan == null || team == null || team.Controlled == null || Time.time < nextReveal) return;
        nextReveal = Time.time + RevealEvery;
        Vector2 at = team.Controlled.transform.position;
        Reveal(arena.CellOf(at));
        var origin = arena.Interior.min;
        Here = null;
        foreach (var z in Plan.zones)
        {
            var r = z.Rect;
            if (new RectInt(r.xMin + origin.x, r.yMin + origin.y, r.width, r.height).Contains(arena.CellOf(at))) Here = z;
        }
    }

    void Reveal(Vector2Int cell)
    {
        var inside = arena.Interior;
        int r = Mathf.CeilToInt(RevealRadius);
        for (int x = cell.x - r; x <= cell.x + r; x++)
            for (int y = cell.y - r; y <= cell.y + r; y++)
            {
                int mx = x - inside.xMin, my = y - inside.yMin;
                if (mx < 0 || my < 0 || mx >= inside.width || my >= inside.height) continue;
                int i = my * inside.width + mx;
                if (revealed[i] || (x - cell.x) * (x - cell.x) + (y - cell.y) * (y - cell.y) > RevealRadius * RevealRadius) continue;
                revealed[i] = true;
                var o = Obstacles.At(x, y);
                pixels[i] = !arena.Walkable(x, y) ? Wall : o == null ? Floor : Colour(o.key);
                dirty = true;
            }
        var origin = inside.min;
        foreach (var z in Plan.zones)
        {
            var c = origin + Vector2Int.RoundToInt(z.centre);
            if (Vector2Int.Distance(c, cell) <= RevealRadius + Mathf.Max(z.radius.x, z.radius.y)) seenZones.Add(z);
        }
    }

    static Color32 Colour(string key) => key switch
    {
        "water" => new Color32(50, 110, 230, 255),
        "lava" => new Color32(230, 80, 30, 255),
        "void" => new Color32(10, 10, 20, 255),
        "ice_floor" or "frozen" or "ice_wall" => new Color32(170, 225, 255, 255),
        "bridge" or "vine_bridge" or "burnt_bridge" or "burning_bridge" => new Color32(150, 100, 50, 255),
        "quicksand" => new Color32(200, 160, 70, 255),
        "metal" or "magnet" => new Color32(140, 150, 170, 255),
        "pylon" => new Color32(255, 220, 0, 255),
        _ => new Color32(110, 95, 85, 255), // rocks and the rest
    };

    void OnGUI()
    {
        if (Plan == null || map == null) return;
        if (dirty)
        {
            map.SetPixels32(pixels);
            map.Apply();
            dirty = false;
        }
        float s = Mathf.Max(1f, Mathf.Round(Screen.height / 360f));
        var inside = arena.Interior;
        float scale = Mathf.Min(MapWidth / inside.width, MapMaxHeight / inside.height) * s;
        var rect = new Rect(Screen.width - 8f * s - inside.width * scale, 34f * s, inside.width * scale, inside.height * scale);
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, map); // texture rows go up: drawn as is, the map's north is on top
        // Special zones once seen, and the team.
        var origin = inside.min;
        foreach (var z in seenZones)
        {
            if (!ZoneColours.TryGetValue(z.type, out var c)) continue;
            GUI.color = c;
            var p = MapPoint(rect, scale, z.centre);
            GUI.DrawTexture(new Rect(p.x - 3f * s, p.y - 3f * s, 6f * s, 6f * s), Texture2D.whiteTexture);
        }
        if (team != null && team.Controlled != null)
        {
            var cell = arena.CellOf(team.Controlled.transform.position) - origin;
            var p = MapPoint(rect, scale, cell);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(p.x - 2f * s, p.y - 2f * s, 4f * s, 4f * s), Texture2D.whiteTexture);
        }
        GUI.color = previous;
    }

    // A point of the canvas (tiles) on the mini-map: x to the right, y up.
    static Vector2 MapPoint(Rect rect, float scale, Vector2 tiles) => new(rect.x + tiles.x * scale, rect.yMax - tiles.y * scale);
}
