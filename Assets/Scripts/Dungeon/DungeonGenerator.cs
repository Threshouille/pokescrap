using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Generates a square arena (walls around, a few obstacles inside) and draws it with a DungeonTileset.
public class DungeonGenerator : MonoBehaviour
{
    // Neighbour offsets in tileset mask bit order: S, SE, E, NE, N, NW, W, SW.
    static readonly Vector2Int[] Neighbours =
    {
        new(0, -1), new(1, -1), new(1, 0), new(1, 1), new(0, 1), new(-1, 1), new(-1, 0), new(-1, -1),
    };

    public DungeonTileset tileset;
    public Tilemap floorMap;
    public Tilemap wallMap;
    public Transform player;
    [Tooltip("Side of the walkable square, in tiles")]
    public int arenaSize = 30;
    [Tooltip("Wall thickness around the arena, so the camera never shows the void")]
    public int margin = 14;
    public int obstacles = 8;
    public int obstacleMinSize = 2;
    public int obstacleMaxSize = 4;
    [Tooltip("Tiles kept free around the centre, where the player starts")]
    public int spawnClearance = 3;
    [Tooltip("0 = different arena every time")]
    public int seed;

    /// The arena currently in play, used by the AIs to find their way.
    public static DungeonGenerator Current { get; private set; }

    public int Size => size;

    /// Tiles of the arena inside its outer walls.
    public RectInt Interior => new(margin, margin, arenaSize, arenaSize);

    bool[,] open;
    int size;
    readonly Dictionary<(int, bool), Tile> tiles = new();

    void Start()
    {
        Generate();
    }

    public void Generate()
    {
        Current = this;
        tiles.Clear(); // the tileset may have changed since the previous arena
        var rng = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
        size = arenaSize + 2 * margin;
        open = new bool[size, size];
        for (int x = margin; x < margin + arenaSize; x++)
            for (int y = margin; y < margin + arenaSize; y++)
                open[x, y] = true;

        // Obstacles are only placed where a free ring remains around them, so nothing gets sealed off.
        int centre = size / 2, placed = 0;
        int wanted = Run.Sandbox ? 0 : obstacles; // the dev sandbox is an open field
        for (int attempt = 0; attempt < wanted * 20 && placed < wanted; attempt++)
        {
            int w = rng.Next(obstacleMinSize, obstacleMaxSize + 1), h = rng.Next(obstacleMinSize, obstacleMaxSize + 1);
            if (w + 2 > arenaSize || h + 2 > arenaSize) continue;
            var block = new RectInt(rng.Next(margin + 1, margin + arenaSize - w), rng.Next(margin + 1, margin + arenaSize - h), w, h);
            if (!IsFree(block, centre)) continue;
            for (int x = block.xMin; x < block.xMax; x++)
                for (int y = block.yMin; y < block.yMax; y++)
                    open[x, y] = false;
            placed++;
        }

        Draw(rng);
        Obstacles.Rebuild(this);
        Terrain.Rebuild(this);

        if (player != null)
            player.position = floorMap.transform.TransformPoint(new Vector3(centre + 0.5f, centre + 0.5f, 0f));
    }

    /// World position of a random free tile at least minDistance tiles away from a point (the centre by default).
    public Vector3 RandomOpenPosition(System.Random rng, float minDistance, Vector2? around = null)
    {
        int centre = size / 2;
        Vector2 origin = around.HasValue ? (Vector2)CellOf(around.Value) : new Vector2(centre, centre);
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int x = rng.Next(margin, margin + arenaSize), y = rng.Next(margin, margin + arenaSize);
            if (open[x, y] && Vector2.Distance(new Vector2(x, y), origin) >= minDistance)
                return floorMap.transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, 0f));
        }
        return floorMap.transform.TransformPoint(new Vector3(centre + 0.5f, centre + 0.5f, 0f));
    }

    bool IsFree(RectInt block, int centre)
    {
        for (int x = block.xMin - 1; x <= block.xMax; x++)
            for (int y = block.yMin - 1; y <= block.yMax; y++)
                if (!open[x, y]) return false;
        return block.xMin - centre > spawnClearance || centre - (block.xMax - 1) > spawnClearance
            || block.yMin - centre > spawnClearance || centre - (block.yMax - 1) > spawnClearance;
    }

    void Draw(System.Random rng)
    {
        floorMap.ClearAllTiles();
        wallMap.ClearAllTiles();
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                bool isOpen = open[x, y];
                int mask = 0;
                for (int i = 0; i < Neighbours.Length; i++)
                    if (IsOpen(x + Neighbours[i].x, y + Neighbours[i].y) == isOpen)
                        mask |= 1 << i;
                int variation = rng.Next(8) == 0 ? rng.Next(1, 3) : 0;
                int chunk = (isOpen ? tileset.floor : tileset.wall)[mask * 3 + variation];
                (isOpen ? floorMap : wallMap).SetTile(new Vector3Int(x, y, 0), GetTile(chunk, !isOpen));
            }
    }

    // Everything outside the map counts as wall.
    bool IsOpen(int x, int y) => open != null && x >= 0 && y >= 0 && x < size && y < size && open[x, y];

    public bool Walkable(int x, int y) => IsOpen(x, y);

    public Vector2Int CellOf(Vector2 world)
    {
        var cell = floorMap.WorldToCell(world);
        return new Vector2Int(cell.x, cell.y);
    }

    public Vector2 CentreOf(int x, int y) => floorMap.transform.TransformPoint(new Vector3(x + 0.5f, y + 0.5f, 0f));

    /// True when a disc of that radius centred on the point touches no wall tile.
    public bool WalkableAt(Vector2 world, float radius)
    {
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            {
                var cell = CellOf(world + new Vector2(sx * radius, sy * radius));
                if (!IsOpen(cell.x, cell.y)) return false;
            }
        return true;
    }

    /// True when a disc of that radius can travel in a straight line between the two points without touching a wall.
    public bool LineClear(Vector2 from, Vector2 to, float radius)
    {
        float length = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.25f));
        for (int i = 0; i <= steps; i++)
            if (!WalkableAt(Vector2.Lerp(from, to, (float)i / steps), radius)) return false;
        return true;
    }

    Tile GetTile(int chunk, bool solid)
    {
        if (tiles.TryGetValue((chunk, solid), out var tile) && tile != null) return tile;
        int chunkSize = tileset.chunkSize, cell = chunkSize + 2 * tileset.padding;
        var rect = new Rect(chunk % tileset.columns * cell + tileset.padding,
            tileset.texture.height - (chunk / tileset.columns + 1) * cell + tileset.padding, chunkSize, chunkSize);
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Sprite.Create(tileset.texture, rect, new Vector2(0.5f, 0.5f), chunkSize, 0, SpriteMeshType.FullRect);
        tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        tiles[(chunk, solid)] = tile;
        return tile;
    }
}
