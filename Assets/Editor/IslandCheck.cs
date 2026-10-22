using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Island preview (Unity closed): python3 Tools/setup.py --unity-only IslandCheck.Run
/// Generates 4 islands (IslandPlan) and draws their sub-biome maps, one pixel per tile, in Logs/island_preview.png:
/// sub-biomes in their colour (biomes.json), barriers between quarters grey, the void around the central zone black,
/// the central zone purple, the sky dark blue, the landing point red; walls darker, water blue, lava orange, void
/// black, bridges brown, cooled rock dark, ice pale, quicksand ochre; the passages between quarters white. ISLAND lines in Logs/setup_unity.log give each
/// island's themes in opening order and the share of each sub-biome.
public static class IslandCheck
{
    public static void Run()
    {
        var shots = new List<Texture2D>();
        for (int seed = 1; seed <= 4; seed++)
        {
            var plan = IslandPlan.Generate(new System.Random(seed * 101));
            var tex = new Texture2D(plan.size, plan.size, TextureFormat.RGB24, false);
            var px = new Color32[plan.size * plan.size];
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < px.Length; i++)
            {
                int r = plan.region[i];
                px[i] = r == IslandPlan.Void ? new Color32(12, 20, 48, 255)
                    : r == IslandPlan.Border ? new Color32(70, 70, 70, 255)
                    : r == IslandPlan.Moat ? new Color32(0, 0, 0, 255)
                    : r == IslandPlan.Centre ? new Color32(140, 90, 185, 255)
                    : Hex(plan.biome[i]?.color);
                // Big features over the sub-biomes: walls darker, obstacles in their colour.
                if (r >= 0 && r < 4)
                {
                    if (plan.wall[i]) px[i] = Darker(px[i]);
                    else if (plan.obstacle[i] != null) px[i] = Obstacle(plan.obstacle[i]);
                }
                if (plan.biome[i] != null)
                {
                    string key = $"{plan.themes[r].name}/{plan.biome[i].name}";
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
            }
            foreach (var g in plan.gates)
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        px[plan.Index(g.x + dx, g.y + dy)] = new Color32(255, 255, 255, 255);
            for (int dx = -3; dx <= 3; dx++)
                for (int dy = -3; dy <= 3; dy++)
                {
                    int x = plan.start.x + dx, y = plan.start.y + dy;
                    if (x >= 0 && y >= 0 && x < plan.size && y < plan.size) px[plan.Index(x, y)] = new Color32(255, 0, 0, 255);
                }
            tex.SetPixels32(px);
            tex.Apply();
            shots.Add(tex);
            var themes = new List<string>();
            foreach (int q in plan.order) themes.Add(plan.themes[q].name);
            var shares = new List<string>();
            foreach (var kv in counts) shares.Add($"{kv.Key} {kv.Value * 100f / px.Length:0.#}%");
            Debug.Log($"ISLAND #{seed} {plan.size}×{plan.size}, ordre : {string.Join(" → ", themes)} ; {string.Join(", ", shares)}");
        }
        int s = shots[0].width;
        var sheet = new Texture2D(2 * s + 6, 2 * s + 6, TextureFormat.RGB24, false);
        sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
        for (int k = 0; k < shots.Count; k++)
        {
            var src = shots[k];
            sheet.SetPixels32((k % 2) * (s + 6), (1 - k / 2) * (s + 6), src.width, src.height, src.GetPixels32());
        }
        sheet.Apply();
        File.WriteAllBytes("Logs/island_preview.png", sheet.EncodeToPNG());
        Debug.Log("ISLAND DONE");
    }

    static Color32 Darker(Color32 c) => new((byte)(c.r * 0.35f), (byte)(c.g * 0.35f), (byte)(c.b * 0.35f), 255);

    static Color32 Obstacle(string key) => key switch
    {
        "water" => new Color32(40, 100, 230, 255),
        "lava" => new Color32(255, 90, 20, 255),
        "void" => new Color32(0, 0, 0, 255),
        "ice_floor" => new Color32(200, 240, 255, 255),
        "quicksand" => new Color32(150, 110, 40, 255),
        "bridge" or "vine_bridge" => new Color32(150, 95, 40, 255),
        "rock" => new Color32(60, 45, 45, 255),
        _ => new Color32(255, 0, 255, 255),
    };

    static Color32 Hex(string hex) =>
        ColorUtility.TryParseHtmlString(hex ?? "", out var c) ? (Color32)c : new Color32(255, 0, 255, 255);

    /// Unity -batchmode -projectPath . -executeMethod IslandCheck.Shots -quit (graphics, not -nographics): builds an
    /// island in the arena scene, logs how long it takes, and draws it as the game does in Logs/island_shots.png: the
    /// whole island, then close-ups of the landing point, a bridge and a passage to the next quarter.
    public static void Shots()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var gen = Object.FindAnyObjectByType<DungeonGenerator>();
        var cam = Camera.main;
        var plan = IslandPlan.Generate(new System.Random(303));
        var watch = System.Diagnostics.Stopwatch.StartNew();
        gen.island = plan;
        gen.Generate();
        long build = watch.ElapsedMilliseconds;
        typeof(Obstacles).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(Obstacles.Current, null);
        Debug.Log($"ISLANDSHOT construction {build} ms, obstacles {Obstacles.Current.Count}, carte {plan.size}×{plan.size}");
        var origin = gen.Interior.min;
        Vector2 Tile(Vector2Int p) => gen.CentreOf(p.x + origin.x, p.y + origin.y);
        Vector2Int bridge = plan.start;
        for (int i = 0; i < plan.obstacle.Length; i++)
            if (plan.obstacle[i] == "bridge" || plan.obstacle[i] == "rock" || plan.obstacle[i] == "vine_bridge") { bridge = new Vector2Int(i % plan.size, i / plan.size); break; }
        var views = new List<(Vector2 centre, float half)>
        {
            (gen.CentreOf(gen.Size / 2, gen.Size / 2), plan.size / 2f + 2f),
            (Tile(plan.start), 12f),
            (Tile(bridge), 12f),
            (Tile(plan.gates.Count > 0 ? plan.gates[0] : plan.start), 14f),
        };
        cam.backgroundColor = new Color(0.42f, 0.62f, 0.86f);
        const int W = 800;
        var rt = new RenderTexture(W, W, 24);
        var sheet = new Texture2D(2 * W + 8, 2 * W + 8, TextureFormat.RGB24, false);
        for (int k = 0; k < views.Count; k++)
        {
            cam.transform.position = new Vector3(views[k].centre.x, views[k].centre.y, -10f);
            cam.orthographicSize = views[k].half;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, W, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, W), 0, 0);
            tex.Apply();
            sheet.SetPixels((k % 2) * (W + 8), (1 - k / 2) * (W + 8), W, W, tex.GetPixels());
        }
        cam.targetTexture = null;
        RenderTexture.active = null;
        sheet.Apply();
        File.WriteAllBytes("Logs/island_shots.png", sheet.EncodeToPNG());
        gen.island = null;
        Debug.Log("ISLANDSHOT DONE");
    }
}
