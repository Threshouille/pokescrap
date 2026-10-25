using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Level checks (Unity closed):
/// - python3 Tools/setup.py --unity-only LevelCheck.Run: builds 20 levels (LevelPlan) in the arena scene and checks
///   that every zone (boss included) is reached on foot from the start; logs zone counts, map size and the walk to the
///   boss in tiles and seconds (LEVELCHECK lines in Logs/setup_unity.log).
/// - Unity -batchmode -projectPath . -executeMethod LevelCheck.Shots -quit (graphics, not -nographics): pictures of
///   3 whole levels as the game draws them in Logs/levels_shots.png.
public static class LevelCheck
{
    const float WalkSpeed = 4f; // tiles per second, about a Pokémon's walking speed

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var gen = Object.FindAnyObjectByType<DungeonGenerator>();
        var rng = new System.Random(5);
        int problems = 0;
        for (int n = 0; n < 20; n++)
        {
            var plan = LevelPlan.Generate(rng);
            gen.level = plan;
            gen.Generate();
            var origin = gen.Interior.min;
            var dist = Distances(gen, gen.CellOf(gen.SpawnPoint));
            int unreached = 0;
            foreach (var z in plan.zones)
                if (!dist.ContainsKey(origin + Vector2Int.RoundToInt(z.centre))) unreached++;
            var bossCell = origin + Vector2Int.RoundToInt(plan.boss.centre);
            int walk = dist.TryGetValue(bossCell, out int d) ? d : -1;
            if (unreached > 0 || walk < 0) problems++;
            // Full exploration: the walk to the boss plus a return trip into every branch.
            int detours = 0;
            foreach (var z in plan.zones)
            {
                if (z.main || z.links.Count != 1) continue;
                var root = z;
                while (root != null && !root.main) root = root.links.Find(l => l.main || dist.GetValueOrDefault(origin + Vector2Int.RoundToInt(l.centre)) < dist.GetValueOrDefault(origin + Vector2Int.RoundToInt(root.centre)));
                if (root == null) continue;
                detours += 2 * (dist.GetValueOrDefault(origin + Vector2Int.RoundToInt(z.centre)) - dist.GetValueOrDefault(origin + Vector2Int.RoundToInt(root.centre)));
            }
            Debug.Log($"LEVELCHECK #{n} {plan.theme} décor {plan.tileset}: {plan.zones.Count} zones ({plan.zones.FindAll(z => z.main).Count} sur le chemin), "
                + $"carte {plan.size.x}×{plan.size.y}, vers le boss {walk} cases ≈ {walk / WalkSpeed:0} s, exploration complète ≈ {(walk + detours) / WalkSpeed / 60f:0.0} min, obstacles {Obstacles.Current.Count}, zones non atteintes {unreached}");
        }
        gen.level = null;
        Debug.Log($"LEVELCHECK DONE 20 levels, {problems} problems");
    }

    static Dictionary<Vector2Int, int> Distances(DungeonGenerator gen, Vector2Int from)
    {
        var dist = new Dictionary<Vector2Int, int> { [from] = 0 };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            foreach (var step in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
            {
                var n = c + step;
                if (dist.ContainsKey(n) || !gen.Walkable(n.x, n.y) || Obstacles.Blocks(n.x, n.y, null)) continue;
                dist[n] = dist[c] + 1;
                queue.Enqueue(n);
            }
        }
        return dist;
    }

    public static void Shots()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var gen = Object.FindAnyObjectByType<DungeonGenerator>();
        var cam = Camera.main;
        var lateUpdate = typeof(Obstacles).GetMethod("LateUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var rng = new System.Random(9);
        const int W = 1000, H = 800;
        var rt = new RenderTexture(W, H, 24);
        var shots = new List<Texture2D>();
        for (int n = 0; n < 3; n++)
        {
            var plan = LevelPlan.Generate(rng);
            gen.level = plan;
            gen.Generate();
            lateUpdate?.Invoke(Obstacles.Current, null);
            var inside = gen.Interior;
            Vector2 centre = gen.CentreOf(inside.xMin + inside.width / 2, inside.yMin + inside.height / 2);
            cam.transform.position = new Vector3(centre.x, centre.y, -10f);
            cam.orthographicSize = Mathf.Max(inside.height / 2f, inside.width / 2f * H / W) + 1f;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            shots.Add(tex);
            Debug.Log($"LEVELSHOT #{n} {plan.theme} décor {plan.tileset}, {plan.zones.Count} zones, carte {plan.size.x}×{plan.size.y}");
        }
        cam.targetTexture = null;
        RenderTexture.active = null;
        var sheet = new Texture2D(W, H * shots.Count + 8 * (shots.Count - 1), TextureFormat.RGB24, false);
        for (int i = 0; i < shots.Count; i++) sheet.SetPixels(0, (shots.Count - 1 - i) * (H + 8), W, H, shots[i].GetPixels());
        File.WriteAllBytes("Logs/levels_shots.png", sheet.EncodeToPNG());
        gen.level = null;
        Debug.Log("LEVELSHOT DONE");
    }
}
