using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Island preview (Unity closed): python3 Tools/setup.py --unity-only IslandCheck.Run
/// Generates 4 islands (IslandPlan) and draws their sub-biome maps, one pixel per tile, in Logs/island_preview.png:
/// sub-biomes in their colour (biomes.json), barriers between quarters grey, the void around the central zone black,
/// the central zone purple, the sky dark blue, the landing point red. ISLAND lines in Logs/setup_unity.log give each
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
                if (plan.biome[i] != null)
                {
                    string key = $"{plan.themes[r].name}/{plan.biome[i].name}";
                    counts[key] = counts.GetValueOrDefault(key) + 1;
                }
            }
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

    static Color32 Hex(string hex) =>
        ColorUtility.TryParseHtmlString(hex ?? "", out var c) ? (Color32)c : new Color32(255, 0, 255, 255);
}
