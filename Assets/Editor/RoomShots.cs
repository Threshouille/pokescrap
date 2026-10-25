using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Pictures of every variant of rooms.json as the game draws them (décor, obstacles), Unity closed and with
/// graphics (not -nographics): Unity -batchmode -projectPath . -executeMethod RoomShots.Run -quit
/// Writes Logs/rooms_shots.png (one picture per variant, in the order of the ROOMSHOT lines of the log).
public static class RoomShots
{
    const int W = 360, H = 300, Cols = 6;

    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var gen = Object.FindAnyObjectByType<DungeonGenerator>();
        var cam = Camera.main;
        var rng = new System.Random(3);
        var shots = new List<Texture2D>();
        var lateUpdate = typeof(Obstacles).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        var rt = new RenderTexture(W, H, 24);
        var file = JsonUtility.FromJson<Catalog>(File.ReadAllText("Assets/Resources/Data/rooms.json"));
        foreach (var theme in file.themes)
            foreach (var variant in theme.variants)
            {
                var room = RoomCatalog.Pick("combat", rng, theme.key, variant.key);
                if (room == null) continue;
                gen.room = room;
                gen.seed = rng.Next(1, int.MaxValue);
                gen.Generate();
                lateUpdate?.Invoke(Obstacles.Current, null);
                Vector2 centre = gen.CentreOf(gen.Size / 2, gen.Size / 2);
                cam.transform.position = new Vector3(centre.x, centre.y, -10f);
                cam.orthographicSize = Mathf.Max(room.size.y / 2f, room.size.x / 2f * H / W) + 1f;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                shots.Add(tex);
                Debug.Log($"ROOMSHOT #{shots.Count - 1} {room.Describe()} obstacles={Obstacles.Current.Count}");
            }
        cam.targetTexture = null;
        RenderTexture.active = null;
        int rows = (shots.Count + Cols - 1) / Cols;
        var sheet = new Texture2D(Cols * (W + 4), rows * (H + 4), TextureFormat.RGB24, false);
        sheet.SetPixels32(new Color32[sheet.width * sheet.height]);
        for (int i = 0; i < shots.Count; i++)
            sheet.SetPixels((i % Cols) * (W + 4), (rows - 1 - i / Cols) * (H + 4), W, H, shots[i].GetPixels());
        File.WriteAllBytes("Logs/rooms_shots.png", sheet.EncodeToPNG());
        Debug.Log($"ROOMSHOT DONE {shots.Count}");
    }

    [System.Serializable] class Catalog { public RoomCatalog.Theme[] themes; }
}
