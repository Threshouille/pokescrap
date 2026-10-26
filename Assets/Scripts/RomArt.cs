using System.Collections.Generic;
using UnityEngine;

/// Images taken from the ROM at setup (Resources/GroundObjects, Resources/Bridges...: ignored by Git, made by
/// Tools/setup.py) as sprites of 24 pixels a tile, loaded once. Null when the image is missing (setup not run).
public static class RomArt
{
    static readonly Dictionary<string, Sprite> sprites = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => sprites.Clear();

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
