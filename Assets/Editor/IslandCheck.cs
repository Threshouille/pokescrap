using System.Collections.Generic;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Island preview (Unity closed): python3 Tools/setup.py --unity-only IslandCheck.Run
/// Generates 4 islands (IslandPlan) and draws their sub-biome maps, one pixel per tile, in Logs/island_preview.png:
/// sub-biomes in their colour (biomes.json), barriers between quarters grey, the void around the central zone black,
/// the central zone purple, the sky dark blue, the landing point red; walls darker, water blue, lava orange, void
/// black, bridges brown, cooled rock dark, ice pale, quicksand ochre; the passage ends between zones white (to the centre:
/// pink). ISLAND lines in Logs/setup_unity.log give each island's themes in opening order and the share of each sub-biome;
/// ZONES lines check each zone (a map of its own): all its walkable tiles reached from its arrival tile, its passage ends
/// walkable, its size.
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
            foreach (var p in plan.passages)
                foreach (var end in new[] { p.a, p.b })
                    for (int dx = -2; dx <= 2; dx++)
                        for (int dy = -2; dy <= 2; dy++)
                            px[plan.Index(end.x + dx, end.y + dy)] = p.to == IslandPlan.Centre ? new Color32(255, 150, 220, 255) : new Color32(255, 255, 255, 255);
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
            Debug.Log($"ZONES #{seed} : {CheckZones(plan)}");
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

    // Each zone: walkable tiles left unreached from its arrival tile, passage ends that are not walkable, size.
    static string CheckZones(IslandPlan plan)
    {
        var parts = new List<string>();
        int problems = 0;
        for (int zone = 0; zone < IslandPlan.ZoneCount; zone++)
        {
            var box = plan.ZoneBounds(zone);
            var arrival = plan.Arrival(zone);
            string name = zone == IslandPlan.Centre ? "centre" : plan.themes[zone].name;
            if (zone == IslandPlan.Centre)
            {
                parts.Add($"{name} {box.width}×{box.height}");
                continue;
            }
            var reached = plan.Reach(plan.Index(arrival.x, arrival.y));
            int lost = 0;
            for (int i = 0; i < reached.Length; i++)
                if (plan.region[i] == zone && plan.Walkable(i) && !reached[i]) lost++;
            int badEnds = 0, ends = 0;
            foreach (var p in plan.passages)
                foreach (var (end, z) in new[] { (p.a, p.from), (p.b, p.to) })
                    if (z == zone)
                    {
                        ends++;
                        if (!reached[plan.Index(end.x, end.y)]) badEnds++;
                    }
            problems += lost + badEnds + (reached[plan.Index(arrival.x, arrival.y)] ? 0 : 1);
            parts.Add($"{name} {box.width}×{box.height}, {ends} passage(s){(badEnds > 0 ? $" dont {badEnds} INACCESSIBLE(S)" : "")}{(lost > 0 ? $", {lost} cases isolées" : "")}");
        }
        return $"{plan.passages.Count} passages, {(problems == 0 ? "OK" : $"{problems} PROBLÈME(S)")} ; " + string.Join(" ; ", parts);
    }

    static Color32 Lighter(Color32 c, int level) => new((byte)Mathf.Min(255, c.r + 22 * level), (byte)Mathf.Min(255, c.g + 22 * level), (byte)Mathf.Min(255, c.b + 14 * level), 255);

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
        "column" => new Color32(255, 255, 255, 255),
        "ledge" => new Color32(230, 140, 60, 255),
        _ => new Color32(255, 0, 255, 255),
    };

    static Color32 Hex(string hex) =>
        ColorUtility.TryParseHtmlString(hex ?? "", out var c) ? (Color32)c : new Color32(255, 0, 255, 255);

    /// Unity -batchmode -projectPath . -executeMethod IslandCheck.Shots -quit (graphics, not -nographics): builds an
    /// island's first quarter (a map of its own) in the arena scene, logs how long it takes, and draws it as the game
    /// does in Logs/island_shots.png: the whole quarter, then close-ups of the landing point, a bridge and the passage
    /// to the next quarter.
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
        Debug.Log($"ISLANDSHOT construction du premier quartier {build} ms, obstacles {Obstacles.Current.Count}, zone {gen.Interior.width}×{gen.Interior.height} (île {plan.size}×{plan.size})");
        var origin = gen.Interior.min - gen.IslandOrigin;
        Vector2 Tile(Vector2Int p) => gen.CentreOf(p.x + origin.x, p.y + origin.y);
        int zone = plan.order[0];
        Vector2Int bridge = plan.start;
        for (int i = 0; i < plan.obstacle.Length; i++)
            if (plan.region[i] == zone && (plan.obstacle[i] == "bridge" || plan.obstacle[i] == "rock" || plan.obstacle[i] == "vine_bridge")) { bridge = new Vector2Int(i % plan.size, i / plan.size); break; }
        var exit = plan.passages.Find(p => p.from == zone);
        var views = new List<(Vector2 centre, float half)>
        {
            (gen.CentreOf(gen.Size / 2, gen.Size / 2), gen.Interior.width / 2f + 2f),
            (Tile(plan.start), 12f),
            (Tile(bridge), 12f),
            (Tile(exit != null ? exit.a : plan.start), 14f),
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

    /// Test quarter (quarter_test.json), Unity closed: python3 Tools/setup.py --unity-only IslandCheck.Quarter
    /// Four draws side by side in Logs/quarter_preview.png (sub-biomes in their colour, water blue, bridges brown, the
    /// landing point red); QUARTER lines: size, share of each side, water, bridges, all land reached from the landing.
    public static void Quarter()
    {
        var shots = new List<Texture2D>();
        for (int k = 0; k < 4; k++)
        {
            var plan = IslandPlan.QuarterTestPlan();
            var px = new Color32[plan.size * plan.size];
            int left = 0, right = 0, water = 0, bridge = 0, walkable = 0;
            var reached = plan.Reach(plan.Index(plan.start.x, plan.start.y));
            int lost = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (plan.region[i] != 0) { px[i] = new Color32(12, 20, 48, 255); continue; }
                px[i] = Hex(plan.biome[i]?.color);
                if (plan.biome[i] == plan.themes[0].subbiomes[0]) left++; else right++;
                if (plan.decor != null && plan.decor[i] > 0) px[i] = plan.decor[i] == 30 ? new Color32(120, 120, 120, 255) : new Color32(130, 85, 50, 255);
                if (plan.height != null && plan.height[i] > 0 && !plan.wall[i] && (plan.decor == null || plan.decor[i] == 0))
                    px[i] = Lighter(px[i], plan.height[i]);
                if (plan.roof != null && plan.roof[i]) px[i] = new Color32(90, 50, 110, 255);
                if (plan.wall[i]) px[i] = new Color32(110, 70, 60, 255);
                if (plan.obstacle[i] != null) px[i] = Obstacle(plan.obstacle[i]);
                float shade = plan.Shade(i);
                if (shade < 0.9f) px[i] = new Color32((byte)(px[i].r * shade), (byte)(px[i].g * shade), (byte)(px[i].b * shade), 255);
                if (plan.obstacle[i] == "water") water++;
                else if (plan.obstacle[i] != null) bridge++;
                if (plan.Walkable(i)) { walkable++; if (!reached[i]) lost++; }
            }
            foreach (var c in plan.caves)
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        px[plan.Index(c.x + dx, c.y + dy)] = new Color32(20, 10, 30, 255);
            foreach (var m in plan.markers)
                for (int dx = -2; dx <= 2; dx++)
                    for (int dy = -2; dy <= 2; dy++)
                        px[plan.Index(m.x + dx, m.y + dy)] = new Color32(255, 210, 60, 255);
            if (plan.sealedCave.x >= 0)
            {
                foreach (var p in plan.sealedCaveRect.allPositionsWithin) px[plan.Index(p.x, p.y)] = new Color32(255, 120, 0, 255);
                foreach (var p in plan.caveParts) px[plan.Index(p.x, p.y)] = new Color32(255, 255, 0, 255);
                px[plan.Index(plan.sealedCaveFront.x, plan.sealedCaveFront.y)] = new Color32(255, 255, 255, 255);
                var front = plan.biome[plan.Index(plan.sealedCaveFront.x, plan.sealedCaveFront.y)];
                Debug.Log($"QUARTER #{k + 1} grotte scellée : {plan.caveSeal} en {plan.sealedCave}, sous-biome {front?.key}, éléments {string.Join(" ", plan.caveParts)}");
            }
            else Debug.Log($"QUARTER #{k + 1} grotte scellée : aucune");
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    px[plan.Index(plan.start.x + dx, plan.start.y + dy)] = new Color32(255, 0, 0, 255);
            int roadTiles = 0, stones = 0, columns = 0;
            for (int i = 0; i < px.Length; i++)
            {
                if (plan.decor != null && plan.decor[i] > 0) { roadTiles++; if (plan.decor[i] == 30) stones++; }
                if (plan.obstacle[i] == "column") columns++;
            }
            Debug.Log($"QUARTER #{k + 1} route : {roadTiles} cases dont {stones} de pierre, {columns} colonnes, portails {string.Join(" ", plan.markers)}");
            var tex = new Texture2D(plan.size, plan.size, TextureFormat.RGB24, false);
            tex.SetPixels32(px);
            tex.Apply();
            shots.Add(tex);
            int land = left + right;
            Debug.Log($"QUARTER #{k + 1} {plan.size}×{plan.size} : terre {land} cases, {plan.themes[0].subbiomes[0].name} {left * 100f / land:0}% / {plan.themes[0].subbiomes[1].name} {right * 100f / land:0}%, eau {water} cases, ponts {bridge} cases, praticable {walkable}, hors d'atteinte depuis l'arrivée {lost} (l'autre rive sans pont)");
        }
        int s = shots[0].width;
        var sheet = new Texture2D(2 * s + 6, 2 * s + 6, TextureFormat.RGB24, false);
        sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
        for (int k = 0; k < shots.Count; k++)
            sheet.SetPixels32((k % 2) * (s + 6), (1 - k / 2) * (s + 6), shots[k].width, shots[k].height, shots[k].GetPixels32());
        sheet.Apply();
        File.WriteAllBytes("Logs/quarter_preview.png", sheet.EncodeToPNG());
        Debug.Log("QUARTER DONE");
    }

    /// Unity -batchmode -projectPath . -executeMethod IslandCheck.QuarterShots -quit (graphics): builds a test quarter
    /// in the arena scene and draws it as the game does in Logs/quarter_shots.png: the whole quarter, then close-ups of
    /// the spring, a bridge and the mouth.
    public static void QuarterShots()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var gen = Object.FindAnyObjectByType<DungeonGenerator>();
        var cam = Camera.main;
        var plan = IslandPlan.QuarterTestPlan();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        gen.island = plan;
        gen.Generate();
        long build = watch.ElapsedMilliseconds;
        typeof(Obstacles).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(Obstacles.Current, null);
        Debug.Log($"QUARTERSHOT construction {build} ms, obstacles {Obstacles.Current.Count}, zone {gen.Interior.width}×{gen.Interior.height}");
        var origin = gen.Interior.min - gen.IslandOrigin;
        Vector2 Tile(Vector2Int p) => gen.CentreOf(p.x + origin.x, p.y + origin.y);
        Vector2Int bridge = plan.start, mouth = plan.start;
        float far = -1f;
        for (int i = 0; i < plan.obstacle.Length; i++)
        {
            var p = new Vector2Int(i % plan.size, i / plan.size);
            if (plan.obstacle[i] == plan.themes[0].bridgeKind && bridge == plan.start) bridge = p;
            if (plan.obstacle[i] == plan.themes[0].riverKind && p.x + p.y > far) { far = p.x + p.y; mouth = p; }
        }
        var views = new List<(Vector2 centre, float half)>
        {
            (gen.CentreOf(gen.Size / 2, gen.Size / 2), gen.Interior.width / 2f + 2f),
            (Tile(plan.start), 14f),
            (Tile(bridge), 12f),
            (Tile(mouth), 14f),
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
        File.WriteAllBytes("Logs/quarter_shots.png", sheet.EncodeToPNG());
        gen.island = null;
        Debug.Log("QUARTERSHOT DONE");
    }
}
