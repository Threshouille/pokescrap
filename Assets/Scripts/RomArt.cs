using System.Collections.Generic;
using UnityEngine;

/// Images taken from the ROM at setup (Resources/GroundObjects, Resources/Bridges...: ignored by Git, made by
/// Tools/setup.py) as sprites of 24 pixels a tile, loaded once. Null when the image is missing (setup not run).
public static class RomArt
{
    static readonly Dictionary<string, Sprite> sprites = new();
    static readonly Dictionary<string, Sprite[]> strips = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        sprites.Clear();
        strips.Clear();
    }

    /// The frames of a strip made by setup (<key>_frames.png: "count" equal cells on one row, centred on the object's
    /// foot). Null when it is missing.
    public static Sprite[] Frames(string path, int count)
    {
        if (strips.TryGetValue(path, out var frames) && frames != null && frames.Length > 0 && frames[0] != null) return frames;
        var texture = Resources.Load<Texture2D>(path);
        frames = null;
        if (texture != null && count > 0)
        {
            int w = texture.width / count;
            frames = new Sprite[count];
            for (int k = 0; k < count; k++)
                frames[k] = Sprite.Create(texture, new Rect(k * w, 0, w, texture.height), new Vector2(0.5f, 0.5f), 24f);
        }
        strips[path] = frames;
        return frames;
    }

    /// "path": under Resources, without extension; "pivot": (0.5, 0.5) for the centre.
    public static Sprite Get(string path, Vector2? pivot = null)
    {
        if (sprites.TryGetValue(path, out var sprite) && sprite != null) return sprite;
        var texture = Resources.Load<Texture2D>(path);
        sprite = texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot ?? new Vector2(0.5f, 0.5f), 24f) : null;
        sprites[path] = sprite;
        return sprite;
    }
}
