using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// The palette animation of a ROM décor (DPLA, extracted by Tools/pmd/extract_tileset.py into anim.png: one column per
/// chunk that changes, one row per step of frameMs): the shimmer of water and lava, crystals. Every chunk of every
/// décor runs on the same clock (Time.time), so they stay in step.
public class ChunkAnimation
{
    readonly Texture2D texture;
    readonly Dictionary<int, int> column = new(); // chunk → its column in the texture
    readonly Dictionary<int, Sprite[]> frames = new();
    readonly int chunkSize, padding;
    public readonly int frameCount;
    public readonly float frameMs;

    ChunkAnimation(Texture2D texture, int[] chunks, int frameCount, float frameMs, int chunkSize, int padding)
    {
        this.texture = texture;
        for (int k = 0; k < chunks.Length; k++) column[chunks[k]] = k;
        this.frameCount = frameCount;
        this.frameMs = frameMs;
        this.chunkSize = chunkSize;
        this.padding = padding;
    }

    /// Null when the décor has no animation.
    public static ChunkAnimation Make(Texture2D texture, int[] chunks, int frameCount, float frameMs, int chunkSize, int padding) =>
        texture != null && chunks != null && chunks.Length > 0 && frameCount > 1 && frameMs > 0f
            ? new ChunkAnimation(texture, chunks, frameCount, frameMs, chunkSize, padding) : null;

    public bool Has(int chunk) => column.ContainsKey(chunk);

    /// The frame shown now.
    public int FrameNow => (int)(Time.time * 1000f / frameMs) % frameCount;

    /// Every frame of that chunk (null if it does not move).
    public Sprite[] Frames(int chunk)
    {
        if (!column.TryGetValue(chunk, out int c)) return null;
        // Runtime sprites are destroyed when play mode ends while this cache can survive.
        if (frames.TryGetValue(chunk, out var found) && found[0] != null) return found;
        int cell = chunkSize + 2 * padding;
        found = new Sprite[frameCount];
        for (int f = 0; f < frameCount; f++)
        {
            var rect = new Rect(c * cell + padding, texture.height - (f + 1) * cell + padding, chunkSize, chunkSize);
            found[f] = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), chunkSize, 0, SpriteMeshType.FullRect);
        }
        frames[chunk] = found;
        return found;
    }

    /// A tilemap tile playing that chunk's frames (Unity animates it on its own).
    public TileBase Tile(int chunk, bool solid)
    {
        var tile = ScriptableObject.CreateInstance<AnimatedChunkTile>();
        tile.frames = Frames(chunk);
        tile.speed = 1000f / frameMs;
        tile.solid = solid;
        return tile;
    }
}

/// A tilemap tile cycling through sprites (ChunkAnimation), with or without a collider.
public class AnimatedChunkTile : TileBase
{
    public Sprite[] frames;
    public float speed; // frames per second
    public bool solid;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData data)
    {
        data.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        data.color = Color.white;
        data.flags = TileFlags.LockTransform;
        data.colliderType = solid ? UnityEngine.Tilemaps.Tile.ColliderType.Grid : UnityEngine.Tilemaps.Tile.ColliderType.None;
    }

    public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData data)
    {
        if (frames == null || frames.Length < 2) return false;
        data.animatedSprites = frames;
        data.animationSpeed = speed;
        data.animationStartTime = 0f;
        return true;
    }
}
