using UnityEngine;

/// The island (IslandPlan) in play: built whole by DungeonGenerator, the sky around it (camera background), the team
/// landing on the first quarter's coast. The mini-map (top right) starts in the fog and is revealed around the team:
/// walls, floor in its sub-biome's colour, water, lava and void; the passages to the next quarters and the central
/// zone marked once seen. Seals, Time Gears and encounters come later (GDD, "L'île flottante").
public class IslandRun : MonoBehaviour
{
    const float RevealRadius = 12f, RevealEvery = 0.2f, MapSize = 140f;
    static readonly Color Sky = new(0.42f, 0.62f, 0.86f);

    public IslandPlan Plan { get; private set; }

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

    public void Begin(System.Random rng)
    {
        arena = DungeonGenerator.Current;
        team = FindAnyObjectByType<PlayerController>();
        Plan = IslandPlan.Generate(rng);
        arena.room = null;
        arena.level = null;
        arena.island = Plan;
        arena.Generate();
        if (team != null) team.PrepareForNextArena(arena.SpawnPoint, 0f);
        if (Camera.main != null)
        {
            previousBackground = Camera.main.backgroundColor;
            Camera.main.backgroundColor = Sky;
        }
        if (map != null) Destroy(map);
        map = new Texture2D(Plan.size, Plan.size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        pixels = new Color32[Plan.size * Plan.size];
        revealed = new bool[pixels.Length];
        dirty = true;
        nextReveal = 0f;
    }

    public void End()
    {
        if (arena != null) arena.island = null;
        if (Camera.main != null && Plan != null) Camera.main.backgroundColor = previousBackground;
        Plan = null;
        if (map != null) Destroy(map);
        map = null;
    }

    void FixedUpdate()
    {
        if (Plan == null || team == null || team.Controlled == null || Time.time < nextReveal) return;
        nextReveal = Time.time + RevealEvery;
        var cell = arena.CellOf(team.Controlled.transform.position) - arena.Interior.min;
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
                if (revealed[i]) continue;
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
        return ColorUtility.TryParseHtmlString(Plan.biome[i]?.color ?? "", out var c) ? (Color32)c : new Color32(170, 170, 160, 255);
    }

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
        float scale = MapSize / Plan.size * s;
        var rect = new Rect(Screen.width - 8f * s - Plan.size * scale, 34f * s, Plan.size * scale, Plan.size * scale);
        var previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.DrawTexture(rect, map);
        // Passages to the next quarters, once seen; the team.
        GUI.color = new Color(1f, 0.85f, 0.3f);
        foreach (var g in Plan.gates)
            if (revealed[Plan.Index(g.x, g.y)]) Dot(rect, scale, g, 3f * s);
        if (team != null && team.Controlled != null)
        {
            GUI.color = Color.white;
            Dot(rect, scale, arena.CellOf(team.Controlled.transform.position) - arena.Interior.min, 2.5f * s);
        }
        GUI.color = previous;
    }

    static void Dot(Rect rect, float scale, Vector2 tiles, float half) =>
        GUI.DrawTexture(new Rect(rect.x + tiles.x * scale - half, rect.yMax - tiles.y * scale - half, 2f * half, 2f * half), Texture2D.whiteTexture);
}
