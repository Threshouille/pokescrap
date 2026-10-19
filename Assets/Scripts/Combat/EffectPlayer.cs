using System;
using System.Collections.Generic;
using UnityEngine;

/// Sprite sheets of attack effects, described by Resources/Effects/effects.json (one row of frames per sheet).
public static class EffectLibrary
{
    public const int PixelsPerUnit = 24; // same scale as the Pokémon and the tiles

    [Serializable]
    public class Sheet
    {
        public string file;
        public int frame_width, frame_height, frame_count;
        public int[] frame_duration_ms;
        public bool loop;

        [NonSerialized] public Texture2D texture;
        [NonSerialized] Sprite[] sprites;

        public Sprite Frame(int index)
        {
            sprites ??= new Sprite[frame_count];
            // Runtime sprites are destroyed when play mode ends while this cache can survive.
            if (sprites[index] == null)
                sprites[index] = Sprite.Create(texture, new Rect(index * frame_width, 0, frame_width, frame_height),
                    new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            return sprites[index];
        }
    }

    [Serializable] class SheetFile { public Sheet[] sheets; }

    static Dictionary<string, Sheet> sheets;

    // Statics can outlive a play session in the editor: reload the files each time a game starts.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => sheets = null;

    /// Sheet by name (file name without extension), or null when it does not exist.
    public static Sheet Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (sheets == null)
        {
            sheets = new Dictionary<string, Sheet>();
            var json = Resources.Load<TextAsset>("Effects/effects");
            if (json != null)
                foreach (var sheet in JsonUtility.FromJson<SheetFile>(json.text).sheets)
                {
                    string key = System.IO.Path.GetFileNameWithoutExtension(sheet.file);
                    sheet.texture = Resources.Load<Texture2D>("Effects/" + key);
                    if (sheet.texture != null && sheet.frame_count > 0) sheets[key] = sheet;
                }
        }
        return sheets.TryGetValue(name, out var found) ? found : null;
    }
}

/// Plays one effect sheet on a SpriteRenderer. Sheets are drawn pointing right and rotated towards a direction.
public class EffectPlayer : MonoBehaviour
{
    const int SortingOrder = 3; // above the Pokémon

    EffectLibrary.Sheet sheet;
    SpriteRenderer spriteRenderer;
    int frame;
    float timer; // ms spent on the current frame
    float dieAt = -1f;

    /// Starts an effect on an existing object. Returns false when the sheet does not exist.
    public static bool Attach(GameObject go, string name, Vector2 direction)
    {
        var sheet = EffectLibrary.Get(name);
        if (sheet == null) return false;
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        var player = go.AddComponent<EffectPlayer>();
        player.sheet = sheet;
        player.spriteRenderer = go.GetComponent<SpriteRenderer>();
        if (player.spriteRenderer == null) player.spriteRenderer = go.AddComponent<SpriteRenderer>();
        player.spriteRenderer.sortingOrder = SortingOrder;
        player.spriteRenderer.color = Color.white;
        player.spriteRenderer.sprite = sheet.Frame(0);
        return true;
    }

    /// Creates a standalone effect. One-shot sheets remove themselves at the end; looping ones need a lifetime.
    /// size: width in tiles the frame is stretched to (its own size when ≤ 0). Returns the effect, or null.
    public static GameObject Spawn(string name, Vector3 position, Vector2 direction, Transform parent = null, float lifetime = 0f, float size = 0f)
    {
        var sheet = EffectLibrary.Get(name);
        if (sheet == null) return null;
        var go = new GameObject(name);
        go.transform.position = position;
        if (size > 0f) go.transform.localScale = Vector3.one * size * EffectLibrary.PixelsPerUnit / sheet.frame_width;
        if (parent != null) go.transform.SetParent(parent, true);
        Attach(go, name, direction);
        if (lifetime > 0f) go.GetComponent<EffectPlayer>().dieAt = Time.time + lifetime;
        return go;
    }

    /// An effect turning around "pivot" from fromAngle to toAngle (degrees, relative to direction) while it plays,
    /// "distance" tiles away from it (a whip sweeping an arc). size as in Spawn.
    public static void SpawnSweep(string name, Vector2 pivot, Vector2 direction, float distance, float fromAngle, float toAngle, float size = 0f)
    {
        var sheet = EffectLibrary.Get(name);
        if (sheet == null) return;
        var holder = new GameObject(name + " sweep");
        holder.transform.position = pivot;
        var go = Spawn(name, pivot, Vector2.right, holder.transform, 0f, size);
        go.transform.localPosition = new Vector3(distance, 0f, 0f);
        float duration = 0f;
        for (int i = 0; i < sheet.frame_count; i++) duration += go.GetComponent<EffectPlayer>().Duration(i) / 1000f;
        float facing = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        holder.AddComponent<EffectSweep>().Begin(facing + fromAngle, facing + toAngle, duration);
    }

    void Update()
    {
        if (dieAt > 0f && Time.time >= dieAt)
        {
            Destroy(gameObject);
            return;
        }

        timer += Time.deltaTime * 1000f;
        for (int guard = 0; guard < sheet.frame_count && timer >= Duration(frame); guard++)
        {
            timer -= Duration(frame);
            frame++;
            if (frame < sheet.frame_count) continue;
            if (!sheet.loop)
            {
                Destroy(gameObject);
                return;
            }
            frame = 0;
        }
        spriteRenderer.sprite = sheet.Frame(frame);
    }

    float Duration(int index)
    {
        var durations = sheet.frame_duration_ms;
        return durations != null && index < durations.Length && durations[index] > 0 ? durations[index] : 80f;
    }
}

/// Turns its object (holding an effect) from one angle to another, then goes away with the effect.
public class EffectSweep : MonoBehaviour
{
    float from, to, duration, elapsed;

    public void Begin(float fromAngle, float toAngle, float seconds)
    {
        from = fromAngle;
        to = toAngle;
        duration = Mathf.Max(0.01f, seconds);
        transform.rotation = Quaternion.Euler(0f, 0f, from);
    }

    void Update()
    {
        if (transform.childCount == 0) { Destroy(gameObject); return; }
        elapsed += Time.deltaTime;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(from, to, elapsed / duration));
    }
}
