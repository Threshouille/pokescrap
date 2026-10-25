using UnityEngine;

/// Dungeon tileset built from an exported tileset.json (see Pokescrap > Build Tilesets).
/// Each table maps (neighbour mask * 3 + variation) to a chunk index in the texture.
public class DungeonTileset : ScriptableObject
{
    public int romId; // ROM tileset number (its folder name), set by Build Tilesets: picks the arena's obstacles
    public Texture2D texture;
    public int chunkSize = 24;
    public int columns = 16;
    public int padding; // px of repeated edge colour around each chunk in the texture
    public int[] wall;
    public int[] floor;
    public int[] water;
    // Palette animation (anim.png, see ChunkAnimation): the chunks that change, their frames, ms per frame.
    public Texture2D anim;
    public int[] animChunks = new int[0];
    public int animFrames;
    public float animFrameMs;

    [System.NonSerialized] ChunkAnimation animation;
    [System.NonSerialized] bool animationRead;

    /// Its palette animation, or null.
    public ChunkAnimation Animation
    {
        get
        {
            if (!animationRead) { animation = ChunkAnimation.Make(anim, animChunks, animFrames, animFrameMs, chunkSize, padding); animationRead = true; }
            return animation;
        }
    }
}
