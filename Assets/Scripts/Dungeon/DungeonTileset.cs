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
}
