using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Room catalogue check (Unity closed): python3 Tools/setup.py --unity-only RoomCheck.Run
/// Builds 12 rooms of every type of rooms.json in the arena scene (fixed seed), checks that at least 90 % of the
/// walkable floor is reached from the spawn point (ROOMCHECK lines in Logs/setup_unity.log) and draws their mini-maps
/// in Logs/rooms_preview.png (walls brown, water blue, lava orange, void black, ice pale blue, bridges, boulders...).
public static class RoomCheck
{
    const int Px = 4, Per = 12;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var gen = Object.FindAnyObjectByType<DungeonGenerator>();
        var rng = new System.Random(7);
        var shots = new List<(Texture2D, string)>();
        int bad = 0;
        foreach (var type in RoomCatalog.Types)
            for (int n = 0; n < Per; n++)
            {
                var room = RoomCatalog.Pick(type.key, rng);
                if (room == null) { Debug.LogError($"ROOMCHECK none for {type.key}"); bad++; break; }
                gen.room = room;
                gen.seed = rng.Next(1, int.MaxValue);
                try { gen.Generate(); }
                catch (System.Exception e) { Debug.LogError($"ROOMCHECK exception {room.Describe()} {e}"); bad++; continue; }
                var inside = gen.Interior;
                int floor = 0, reached = 0, obstacles = 0;
                var c = new Vector2Int(gen.Size / 2, gen.Size / 2);
                var seen = new HashSet<Vector2Int> { c };
                var q = new Queue<Vector2Int>(); q.Enqueue(c);
                while (q.Count > 0)
                {
                    var p = q.Dequeue();
                    foreach (var d in new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down })
                    {
                        var m = p + d;
                        if (seen.Contains(m) || !gen.Walkable(m.x, m.y) || Obstacles.Blocks(m.x, m.y, null)) continue;
                        seen.Add(m); q.Enqueue(m);
                    }
                }
                var tex = new Texture2D(34 * Px, 30 * Px);
                var px = new Color32[tex.width * tex.height];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(20, 20, 20, 255);
                int ox = (34 - inside.width) / 2, oy = (30 - inside.height) / 2;
                for (int x = inside.xMin; x < inside.xMax; x++)
                    for (int y = inside.yMin; y < inside.yMax; y++)
                    {
                        Color32 col;
                        var o = Obstacles.At(x, y);
                        if (!gen.Walkable(x, y)) col = new Color32(90, 70, 60, 255);
                        else
                        {
                            floor++;
                            if (o != null) obstacles++;
                            if (seen.Contains(new Vector2Int(x, y))) reached++;
                            col = o == null ? new Color32(200, 200, 190, 255) : Kind(o.key);
                        }
                        if (x == c.x && y == c.y) col = new Color32(255, 0, 0, 255);
                        for (int a = 0; a < Px; a++)
                            for (int b = 0; b < Px; b++)
                                px[((y - inside.yMin + oy) * Px + b) * tex.width + (x - inside.xMin + ox) * Px + a] = col;
                    }
                tex.SetPixels32(px); tex.Apply();
                int walkable = floor - obstacles;
                float share = walkable > 0 ? reached / (float)walkable : 0f;
                string line = $"#{shots.Count} {room.Describe()} sol={floor} obstacles={obstacles} atteint={share:P0}";
                if (share < 0.9f) { Debug.LogWarning("ROOMCHECK LOW " + line); bad++; }
                Debug.Log("ROOMCHECK " + line);
                shots.Add((tex, line));
            }
        int cols = Per, rows = (shots.Count + cols - 1) / cols;
        var sheet = new Texture2D(cols * (34 * Px + 4), rows * (30 * Px + 4));
        var bg = new Color32[sheet.width * sheet.height];
        sheet.SetPixels32(bg);
        for (int i = 0; i < shots.Count; i++)
            sheet.SetPixels32((i % cols) * (34 * Px + 4), (rows - 1 - i / cols) * (30 * Px + 4), 34 * Px, 30 * Px, shots[i].Item1.GetPixels32());
        File.WriteAllBytes("Logs/rooms_preview.png", sheet.EncodeToPNG());
        Debug.Log($"ROOMCHECK DONE {shots.Count} rooms, {bad} problems");
    }

    static Color32 Kind(string key) => key switch
    {
        "water" => new Color32(40, 90, 220, 255),
        "lava" => new Color32(230, 70, 20, 255),
        "void" => new Color32(0, 0, 0, 255),
        "rock" => new Color32(80, 60, 60, 255),
        "bridge" or "vine_bridge" => new Color32(150, 100, 50, 255),
        "ice_floor" or "frozen" => new Color32(170, 230, 255, 255),
        "ice_wall" => new Color32(110, 190, 255, 255),
        "quicksand" => new Color32(200, 160, 70, 255),
        "metal" or "magnet" => new Color32(130, 140, 160, 255),
        "pylon" => new Color32(255, 220, 0, 255),
        _ => new Color32(60, 110, 60, 255),
    };
}
