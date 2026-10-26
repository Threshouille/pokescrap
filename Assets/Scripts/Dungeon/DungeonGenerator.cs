using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// Generates a square arena (walls around, a few obstacles inside) and draws it with a DungeonTileset; or, when
/// "room" is set (RoomCatalog.Pick), a room of the catalogue: its size, wall plan, décor and obstacles.
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

    /// The room to build at the next Generate (null: the classic square arena).
    [System.NonSerialized] public RoomInstance room;

    /// The level to build at the next Generate (LevelPlan: zones and paths on one continuous map); before "room".
    [System.NonSerialized] public LevelPlan level;

    /// The island to build at the next Generate (IslandPlan); before "level" and "room". Only one zone of it is built
    /// (islandZone: a quarter or the centre; the first quarter when negative), the team at islandArrival (island tile;
    /// by default where the team arrives in that zone).
    [System.NonSerialized] public IslandPlan island;
    [System.NonSerialized] public int islandZone = -1;
    [System.NonSerialized] public Vector2Int? islandArrival;

    /// Island tile of the arena's Interior.min when an island zone is built: island tile = cell - Interior.min + this.
    public Vector2Int IslandOrigin { get; private set; }

    /// ROM décor of the central zone of the island (its final dungeon, sealed for now).
    const int CentreDecor = 45;

    public int Size => size;

    /// Tiles of the arena inside its outer walls (a rectangle centred in the square grid).
    public RectInt Interior => new((size - width) / 2, (size - height) / 2, width, height);

    int width, height; // of the interior
    int retries;
    Vector2Int spawn;
    // A whole level: its canvas as the interior, zones and paths carved by the plan, each zone's obstacles laid
    // within it, the team at the start zone's centre.
    void GenerateLevel(System.Random rng)
    {
        if (TilesetRoster.Get(level.tileset) is { } decor) tileset = decor;
        tiles.Clear();
        width = level.size.x;
        height = level.size.y;
        size = Mathf.Max(width, height) + 2 * margin;
        open = new bool[size, size];
        var origin = Interior.min;
        level.Carve(open, origin, rng, spawnClearance);
        spawn = origin + Vector2Int.RoundToInt(level.start.centre);
        Draw(rng);
        Obstacles.Rebuild(this);
        Terrain.Rebuild(this);
        Climate.Clear();
        foreach (var z in level.zones)
        {
            var keep = new List<Vector2Int> { origin + Vector2Int.RoundToInt(z.centre) };
            keep.AddRange(z.entries);
            var rect = z.Rect;
            Obstacles.Current.GenerateZone(z.room.obstacles, rng, keep, new RectInt(rect.xMin + origin.x, rect.yMin + origin.y, rect.width, rect.height));
        }
        Obstacles.Current.FinishLevel(spawn);
        if (player != null) player.position = CentreOf(spawn.x, spawn.y);
    }

    // One zone of the island, as a map of its own: the zone's land and its sub-biomes' décors tile by tile (the centre
    // in its own décor), its walls (cliffs, thickets) drawn with the décor next to them; everything else around it
    // (the other zones, the barriers, the void around the centre) a thick wall; the sky beyond the coast (nothing
    // drawn, an invisible wall along it); then the zone's obstacles (lakes, rivers, bridges, crater) tile by tile.
    void GenerateIsland(System.Random rng)
    {
        var plan = island;
        int zone = islandZone >= 0 ? islandZone : plan.order[0];
        var box = plan.ZoneBounds(zone);
        int n = Mathf.Max(box.width, box.height); // the zone in a square of n × n island tiles
        IslandOrigin = box.min;
        width = height = n;
        size = n + 2 * margin;
        open = new bool[size, size];
        cellDecor = new DungeonTileset[size * size];
        skyWall = new bool[size * size];
        wallLook = plan.wallLook != null ? new bool[size * size] : null;
        cellShade = plan.height != null ? new float[size * size] : null;
        if (plan.height != null)
        {
            cellHeight = new int[size * size];
            cliffHeight = new int[size * size];
            for (int k = 0; k < cliffHeight.Length; k++) cliffHeight[k] = -1;
        }
        var origin = Interior.min;
        var centreDecor = TilesetRoster.Get(CentreDecor);
        int PlanIndex(int x, int y) => x + box.x < plan.size && y + box.y < plan.size ? plan.Index(x + box.x, y + box.y) : -1;
        int Region(int x, int y) => PlanIndex(x, y) is var i && i >= 0 ? plan.region[i] : IslandPlan.Void;
        // The décor of the nearest tile of the zone, for the tiles that have none of their own (walls, sky).
        var nearest = new DungeonTileset[n * n];
        var dist = new int[n * n];
        var queue = new Queue<int>();
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int k = y * n + x;
                dist[k] = int.MaxValue;
                if (Region(x, y) != zone) continue;
                int i = PlanIndex(x, y);
                var b = plan.biome[i];
                var own = zone == IslandPlan.Centre ? centreDecor
                    : plan.decor != null && plan.decor[i] > 0 ? TilesetRoster.Get(plan.decor[i])
                    : b != null && b.tilesets.Length > 0 ? TilesetRoster.Get(b.tilesets[(i * 7919) % b.tilesets.Length]) : null;
                if (own == null) continue;
                nearest[k] = own;
                dist[k] = 0;
                queue.Enqueue(k);
            }
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            int cx = c % n, cy = c / n;
            foreach (var d in Neighbours)
            {
                int x = cx + d.x, y = cy + d.y;
                if (x < 0 || y < 0 || x >= n || y >= n) continue;
                int k = y * n + x;
                if (dist[k] <= dist[c] + 1) continue;
                dist[k] = dist[c] + 1;
                nearest[k] = nearest[c];
                queue.Enqueue(k);
            }
        }
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int k = y * n + x, gx = x + origin.x, gy = y + origin.y, g = gy * size + gx;
                int r = Region(x, y);
                if (r == IslandPlan.Void)
                {
                    // A rope bridge over the sky (TimeGears): walked on, nothing drawn under it.
                    if (PlanIndex(x, y) is var b && b >= 0 && plan.skyBridge.TryGetValue(b, out int bridgeZone) && bridgeZone == zone)
                    {
                        open[gx, gy] = true;
                        continue;
                    }
                    skyWall[g] = dist[k] <= 2; // the edge of the island
                    continue;
                }
                cellDecor[g] = nearest[k];
                open[gx, gy] = r == zone && (zone == IslandPlan.Centre || !plan.wall[PlanIndex(x, y)]);
                if (r == zone && wallLook != null) wallLook[g] = plan.wallLook[PlanIndex(x, y)];
                if (r == zone && plan.cliffStyle != null) LayCliff(plan, PlanIndex(x, y), x, y, gx, gy);
                if (r == zone && cellHeight != null) cellHeight[g] = plan.height[PlanIndex(x, y)];
                if (r == zone && cellShade != null) cellShade[g] = plan.Shade(PlanIndex(x, y));
            }
        // The sky along a bridge is walled too (nobody walks off its sides).
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int gx = x + origin.x, gy = y + origin.y;
                if (!open[gx, gy] || Region(x, y) != IslandPlan.Void) continue;
                foreach (var d in Neighbours)
                {
                    int nx = gx + d.x, ny = gy + d.y;
                    if (nx >= 0 && ny >= 0 && nx < size && ny < size && !open[nx, ny] && cellDecor[ny * size + nx] == null) skyWall[ny * size + nx] = true;
                }
            }
        var arrival = (islandArrival ?? plan.Arrival(zone)) - box.min;
        tileset = nearest[Mathf.Clamp(arrival.y, 0, n - 1) * n + Mathf.Clamp(arrival.x, 0, n - 1)] ?? tileset;
        tiles.Clear();
        spawn = origin + arrival;
        Draw(rng);
        BuildEdges();
        if (plan.roof != null) DrawRoof(plan, box, n);
        Obstacles.Rebuild(this);
        Terrain.Rebuild(this);
        Climate.Clear();
        if (zone != IslandPlan.Centre)
        {
            var theme = plan.themes[zone];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = PlanIndex(x, y);
                    if (i < 0 || plan.region[i] != zone || plan.wall[i] || plan.obstacle[i] == null) continue;
                    string key = plan.obstacle[i];
                    // A bridge spans its river: the river first, the bridge laid over it.
                    if (key == theme.bridgeKind) Obstacles.Place(x + origin.x, y + origin.y, theme.riverKind);
                    Obstacles.Place(x + origin.x, y + origin.y, key);
                }
        }
        Obstacles.Current.FinishLevel(spawn);
        if (player != null) player.position = CentreOf(spawn.x, spawn.y);
    }

    // The cliff piece of an island tile (x, y in the zone; gx, gy in the grid), if any: its tile, its edge collider
    // and the level it blocks shots at.
    void LayCliff(IslandPlan plan, int i, int x, int y, int gx, int gy)
    {
        int g = gy * size + gx;
        if (cliffTiles != null) cliffTiles[g] = null;
        if (plan.CliffRole(i) is not { } role) return;
        cliffTiles ??= new Tile[size * size];
        var sprites = CliffSprites(plan.cliffStyles[plan.cliffStyle[i]], role);
        if (sprites.Length == 0) sprites = CliffSprites(plan.cliffStyles[1], role); // borrowed (the rock's stairs)
        // A face's rows: consecutive columns of the map, in turn along the face; other pieces at random.
        bool row = role is "top" or "middle" or "bottom";
        // The edges block as thick as they are drawn (BuildEdges), not the whole tile; to the navigation they stay walls.
        bool edge = !open[gx, gy] && role is "back" or "left" or "right" or "backLeft" or "backRight" or "top" or "leftTop" or "rightTop";
        if (sprites.Length > 0) cliffTiles[g] = CliffTile(sprites[(row ? x : x * 7 + y * 13) % sprites.Length], !open[gx, gy] && !edge);
        if (edge) { edgeCells.Add(new Vector2Int(gx, gy)); edgeRoles.Add(role); }
        if (!open[gx, gy] && cliffHeight != null) cliffHeight[g] = plan.height[i];
    }

    /// Island tiles changed in play (a cave's door opened, IslandPlan.OpenCave): drawn again, with their collisions,
    /// edges and what the navigation sees.
    public void RedrawIsland(IEnumerable<int> planTiles)
    {
        var plan = island;
        if (plan == null || open == null) return;
        var origin = Interior.min;
        int edges = edgeCells.Count;
        foreach (int i in planTiles)
        {
            int x = i % plan.size - IslandOrigin.x, y = i / plan.size - IslandOrigin.y, gx = x + origin.x, gy = y + origin.y;
            if (gx < 0 || gy < 0 || gx >= size || gy >= size || cellDecor == null) continue;
            int g = gy * size + gx;
            open[gx, gy] = !plan.wall[i];
            if (cliffHeight != null) cliffHeight[g] = -1;
            for (int k = edgeCells.Count - 1; k >= 0; k--)
                if (edgeCells[k] == new Vector2Int(gx, gy)) { edgeCells.RemoveAt(k); edgeRoles.RemoveAt(k); edges = -1; }
            if (plan.decor != null && plan.decor[i] > 0 && TilesetRoster.Get(plan.decor[i]) is { } own) cellDecor[g] = own;
            if (cellShade != null) cellShade[g] = plan.Shade(i);
            if (plan.cliffStyle != null) LayCliff(plan, i, x, y, gx, gy);
            var decor = cellDecor[g];
            if (decor == null) continue;
            var cell = new Vector3Int(gx, gy, 0);
            var piece = cliffTiles != null ? cliffTiles[g] : null;
            floorMap.SetTile(cell, GetTile(decor, decor.floor[255 * 3], false));
            wallMap.SetTile(cell, piece != null ? piece : open[gx, gy] ? null : GetTile(decor, decor.wall[255 * 3], true));
            float shade = cellShade != null && cellShade[g] > 0f ? cellShade[g] : 1f;
            foreach (var map in new[] { floorMap, wallMap })
            {
                map.SetTileFlags(cell, TileFlags.None);
                map.SetColor(cell, new Color(shade, shade, shade, 1f));
            }
        }
        if (edges != edgeCells.Count)
        {
            if (edgeRoot != null)
            {
                if (Application.isPlaying) Destroy(edgeRoot);
                else DestroyImmediate(edgeRoot);
            }
            BuildEdges();
        }
    }

    /// Where the team appeared in the arena.
    public Vector2 SpawnPoint => CentreOf(spawn.x, spawn.y);


    bool[,] open;
    int size;
    readonly Dictionary<(DungeonTileset, int, bool), Tile> tiles = new();
    DungeonTileset[] cellDecor; // per tile when the map mixes décors (the island); null: "tileset" everywhere
    bool[] wallLook; // per tile: walkable but drawn as a wall, without collider (a ledge); may be null
    // A roof over some tiles (a cave in a mountain), drawn above the Pokémon; it fades while the controlled Pokémon is
    // under it, so that the inside shows.
    const int RoofOrder = 9; // over the Pokémon (1), under the health bars (10)
    Tilemap roofMap;
    bool[] roofed;
    float roofAlpha = 1f, roofSeeThrough = 0.15f, roofFade = 0.25f;
    PlayerController team;
    float[] cellShade; // per tile: tint of its ground (relief), 1 = none; may be null
    Tile[] cliffTiles; // per tile: a cliff piece drawn over its ground (cliff_styles.json), may be null
    int[] cellHeight; // per tile: its level of ground (relief), may be null (all 0)
    int[] cliffHeight; // per tile: the level of the cliff blocking it (shots from that level or higher fly over), -1 none; may be null
    readonly List<Vector2Int> edgeCells = new(); // edges of the levels: a collider as thick as the edge drawn
    readonly List<string> edgeRoles = new();
    const float EdgeThickness = 0.25f; // of a tile: the collider along the drawn edge of a level seen from above
    GameObject edgeRoot;
    static readonly Dictionary<(string style, string role), Sprite[]> cliffSprites = new();
    readonly Dictionary<(Sprite, bool), Tile> cliffTileCache = new();
    bool[] skyWall; // island: sky tiles next to land, an invisible wall (nothing drawn, a collider)
    Tile invisibleWall;

    void Start()
    {
        Generate();
    }

    // Drawing order of the floor. Obstacles (+1, +2) and terrain (+3) are drawn over it, and must stay under the
    // walls (-1), the marks on the ground (0) and the Pokémon (1): a tie with them makes the sprites flicker.
    const int FloorOrder = -6;

    public void Generate()
    {
        Current = this;
        if (floorMap.GetComponent<TilemapRenderer>() is { } floor) floor.sortingOrder = FloorOrder;
        int s = seed != 0 ? seed : room != null ? room.seed : 0; // a room of a floor is laid out the same way each time
        var rng = new System.Random(s != 0 ? s : System.Environment.TickCount);
        cellDecor = null;
        skyWall = null;
        wallLook = null;
        cellShade = null;
        cliffTiles = null;
        cellHeight = null;
        cliffHeight = null;
        edgeCells.Clear();
        edgeRoles.Clear();
        if (edgeRoot != null)
        {
            if (Application.isPlaying) Destroy(edgeRoot);
            else DestroyImmediate(edgeRoot);
        }
        roofed = null;
        if (roofMap != null) roofMap.ClearAllTiles();
        if (island != null)
        {
            GenerateIsland(rng);
            return;
        }
        if (level != null)
        {
            GenerateLevel(rng);
            return;
        }
        if (room != null && TilesetRoster.Get(room.tileset) is { } decor) tileset = decor;
        tiles.Clear(); // the tileset may have changed since the previous arena
        width = room != null ? room.size.x : arenaSize;
        height = room != null ? room.size.y : arenaSize;
        size = Mathf.Max(width, height) + 2 * margin;
        open = new bool[size, size];
        var inside = Interior;
        for (int x = inside.xMin; x < inside.xMax; x++)
            for (int y = inside.yMin; y < inside.yMax; y++)
                open[x, y] = true;
        int centre = size / 2;
        spawn = new Vector2Int(centre, centre);
        var keep = new List<Vector2Int> { spawn };

        Obstacles.Room profile;
        if (room != null)
        {
            RoomLayouts.Apply(open, inside, keep, room.layout, rng, spawnClearance);
            profile = room.obstacles;
        }
        else
        {
            // Obstacles are only placed where a free ring remains around them, so nothing gets sealed off.
            // The dev sandbox is an open field; a real arena keeps the wall blocks its décor's obstacle profile asks for.
            profile = Run.Sandbox || tileset == null ? null : Obstacles.ArenaProfile(tileset.romId);
            int wanted = Run.Sandbox ? 0 : profile != null && profile.walls >= 0 ? profile.walls : obstacles, placed = 0;
            for (int attempt = 0; attempt < wanted * 20 && placed < wanted; attempt++)
            {
                int w = rng.Next(obstacleMinSize, obstacleMaxSize + 1), h = rng.Next(obstacleMinSize, obstacleMaxSize + 1);
                if (w + 2 > width || h + 2 > height) continue;
                var block = new RectInt(rng.Next(inside.xMin + 1, inside.xMax - w), rng.Next(inside.yMin + 1, inside.yMax - h), w, h);
                if (!IsFree(block, centre)) continue;
                for (int x = block.xMin; x < block.xMax; x++)
                    for (int y = block.yMin; y < block.yMax; y++)
                        open[x, y] = false;
                placed++;
            }
        }

        Draw(rng);
        Obstacles.Rebuild(this);
        Terrain.Rebuild(this);
        Climate.Clear(); // a new arena starts with clear skies
        if (room != null || !Run.Sandbox) Obstacles.Current.GenerateArena(profile, rng, keep);
        // A room whose obstacles found no room to fit (a lake in a small room) is laid out again, a few times.
        if (room != null && profile?.pool != null && profile.pool.Length > 0 && Obstacles.Current.Count == 0 && retries < 6)
        {
            retries++;
            int kept = seed;
            seed = rng.Next(1, int.MaxValue);
            Generate();
            seed = kept;
            return;
        }
        retries = 0;

        if (player != null) player.position = CentreOf(spawn.x, spawn.y);
    }

    /// World position of a random free tile at least minDistance tiles away from a point (the centre by default).
    public Vector3 RandomOpenPosition(System.Random rng, float minDistance, Vector2? around = null)
    {
        int centre = size / 2;
        Vector2 origin = around.HasValue ? (Vector2)CellOf(around.Value) : new Vector2(centre, centre);
        var inside = Interior;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            int x = rng.Next(inside.xMin, inside.xMax), y = rng.Next(inside.yMin, inside.yMax);
            if (open[x, y] && Obstacles.Standable(x, y) && Vector2.Distance(new Vector2(x, y), origin) >= minDistance)
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
        // Laid in batches: a whole island is hundreds of thousands of tiles.
        var floorCells = new List<Vector3Int>();
        var floorTiles = new List<TileBase>();
        var wallCells = new List<Vector3Int>();
        var wallTiles = new List<TileBase>();
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                bool isOpen = Looks(x, y);
                bool solid = !open[x, y];
                var decor = cellDecor != null ? cellDecor[y * size + x] : tileset;
                if (decor == null)
                {
                    // The sky around the island: nothing drawn, an invisible wall along the coast.
                    if (skyWall != null && skyWall[y * size + x])
                    {
                        wallCells.Add(new Vector3Int(x, y, 0));
                        wallTiles.Add(InvisibleWall());
                    }
                    continue;
                }
                int variation = rng.Next(8) == 0 ? rng.Next(1, 3) : 0;
                // A cliff piece: the ground under it (whole floor), the piece over it (blocking unless walkable).
                if (cliffTiles != null && cliffTiles[y * size + x] != null)
                {
                    floorCells.Add(new Vector3Int(x, y, 0));
                    floorTiles.Add(GetTile(decor, decor.floor[255 * 3 + variation], false));
                    wallCells.Add(new Vector3Int(x, y, 0));
                    wallTiles.Add(cliffTiles[y * size + x]);
                    continue;
                }
                int mask = 0;
                for (int i = 0; i < Neighbours.Length; i++)
                    if (LooksOpen(x + Neighbours[i].x, y + Neighbours[i].y) == isOpen)
                        mask |= 1 << i;
                int chunk = (isOpen ? decor.floor : decor.wall)[mask * 3 + variation];
                (isOpen ? floorCells : wallCells).Add(new Vector3Int(x, y, 0));
                (isOpen ? floorTiles : wallTiles).Add(GetTile(decor, chunk, solid));
            }
        floorMap.SetTiles(floorCells.ToArray(), floorTiles.ToArray());
        wallMap.SetTiles(wallCells.ToArray(), wallTiles.ToArray());
        // Relief: each level's ground tinted, darker at the foot of the cliffs; the cliff faces darker towards their foot.
        if (cellShade != null)
            foreach (var (map, cells) in new[] { (floorMap, floorCells), (wallMap, wallCells) })
                foreach (var cell in cells)
                {
                    float shade = cellShade[cell.y * size + cell.x];
                    if (shade <= 0f || Mathf.Approximately(shade, 1f)) continue;
                    map.SetTileFlags(cell, TileFlags.None);
                    map.SetColor(cell, new Color(shade, shade, shade, 1f));
                }
    }

    // The roof: the walls of its décor over every roofed tile (its own edges where the roof ends), one tile layer.
    void DrawRoof(IslandPlan plan, RectInt box, int n)
    {
        var decor = TilesetRoster.Get(plan.roofDecor);
        if (decor == null) return;
        if (roofMap == null)
        {
            var go = new GameObject("Toit");
            go.transform.SetParent(floorMap.transform.parent, false);
            go.transform.localPosition = floorMap.transform.localPosition;
            roofMap = go.AddComponent<Tilemap>();
            roofMap.tileAnchor = floorMap.tileAnchor;
            go.AddComponent<TilemapRenderer>().sortingOrder = RoofOrder;
        }
        roofed = new bool[size * size];
        var origin = Interior.min;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int px = x + box.x, py = y + box.y;
                if (px < plan.size && py < plan.size && plan.roof[plan.Index(px, py)]) roofed[(y + origin.y) * size + x + origin.x] = true;
            }
        var cells = new List<Vector3Int>();
        var roofTiles = new List<TileBase>();
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                if (!roofed[y * size + x]) continue;
                int mask = 0;
                for (int i = 0; i < Neighbours.Length; i++)
                    if (Roofed(x + Neighbours[i].x, y + Neighbours[i].y)) mask |= 1 << i;
                cells.Add(new Vector3Int(x, y, 0));
                roofTiles.Add(GetTile(decor, decor.wall[mask * 3], false));
            }
        roofMap.SetTiles(cells.ToArray(), roofTiles.ToArray());
        var cave = plan.relief?.cave;
        if (cave != null) { roofSeeThrough = cave.roofAlpha; roofFade = Mathf.Max(0.01f, cave.fadeTime); }
        roofAlpha = 1f;
        roofMap.color = Color.white;
    }

    // The pieces of a cliff role (Resources/Cliffs/<style>/<role>_<n>, extracted by Tools/setup.py), empty if none.
    static Sprite[] CliffSprites(string style, string role)
    {
        if (cliffSprites.TryGetValue((style, role), out var found) && (found.Length == 0 || found[0] != null)) return found;
        var list = new List<Sprite>();
        for (int n = 0; ; n++)
        {
            var texture = Resources.Load<Texture2D>($"Cliffs/{style}/{role}_{n}");
            if (texture == null) break;
            list.Add(Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width, 0, SpriteMeshType.FullRect));
        }
        found = list.ToArray();
        cliffSprites[(style, role)] = found;
        return found;
    }

    Tile CliffTile(Sprite sprite, bool solid)
    {
        if (cliffTileCache.TryGetValue((sprite, solid), out var tile) && tile != null) return tile;
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = sprite;
        tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        cliffTileCache[(sprite, solid)] = tile;
        return tile;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCliffSprites() => cliffSprites.Clear();

    bool Roofed(int x, int y) => roofed != null && x >= 0 && y >= 0 && x < size && y < size && roofed[y * size + x];

    void Update()
    {
        if (roofed == null || roofMap == null) return;
        if (team == null) team = FindAnyObjectByType<PlayerController>();
        var leader = team != null ? team.Controlled : null;
        bool under = false;
        if (leader != null)
        {
            var cell = CellOf(leader.transform.position);
            under = Roofed(cell.x, cell.y);
        }
        roofAlpha = Mathf.MoveTowards(roofAlpha, under ? roofSeeThrough : 1f, Time.deltaTime / roofFade);
        roofMap.color = new Color(1f, 1f, 1f, roofAlpha);
    }

    // How a tile is drawn: open ground, or a wall (also a ledge, walkable but drawn as its cliff).
    bool Looks(int x, int y) => IsOpen(x, y) && (wallLook == null || !wallLook[y * size + x]);

    // The same for its neighbours' borders: a cliff piece stands on whole floor, it gives them none.
    bool LooksOpen(int x, int y) => Looks(x, y)
        || cliffTiles != null && x >= 0 && y >= 0 && x < size && y < size && cliffTiles[y * size + x] != null;

    Tile InvisibleWall()
    {
        if (invisibleWall != null) return invisibleWall;
        invisibleWall = ScriptableObject.CreateInstance<Tile>();
        invisibleWall.colliderType = Tile.ColliderType.Grid;
        return invisibleWall;
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

    // The colliders of the edges, as thick as drawn so that one walks up to them: seen from above, a strip along the
    // side where the ground drops; a face's top row, its lower half (the upper half is the ground above).
    void BuildEdges()
    {
        if (edgeCells.Count == 0) return;
        edgeRoot = new GameObject("Bords des falaises");
        edgeRoot.transform.SetParent(wallMap.transform, false);
        edgeRoot.layer = wallMap.gameObject.layer;
        edgeRoot.transform.position = Vector3.zero;
        Vector2 step = CentreOf(1, 1) - CentreOf(0, 0);
        void Box(Vector2 centre, Vector2 tiles)
        {
            var box = edgeRoot.AddComponent<BoxCollider2D>();
            box.offset = centre;
            box.size = Vector2.Scale(tiles, step);
        }
        float t = EdgeThickness, shift = (1f - t) / 2f;
        for (int k = 0; k < edgeCells.Count; k++)
        {
            var c = CentreOf(edgeCells[k].x, edgeCells[k].y);
            string role = edgeRoles[k];
            if (role is "top" or "leftTop" or "rightTop")
            {
                Box(c - new Vector2(0f, step.y / 4f), new Vector2(1f, 0.5f));
                continue;
            }
            if (role.StartsWith("back")) Box(c + new Vector2(0f, shift * step.y), new Vector2(1f, t));
            if (role.EndsWith("eft")) Box(c - new Vector2(shift * step.x, 0f), new Vector2(t, 1f));
            if (role.EndsWith("ight")) Box(c + new Vector2(shift * step.x, 0f), new Vector2(t, 1f));
        }
    }

    /// The level of ground at that point (relief of the island), 0 without relief.
    public int HeightAt(Vector2 world)
    {
        var c = CellOf(world);
        return HeightOf(c.x, c.y);
    }

    public int HeightOf(int x, int y) =>
        cellHeight != null && x >= 0 && y >= 0 && x < size && y < size ? cellHeight[y * size + x] : 0;

    /// True when the wall at that tile is a cliff no higher than that level: a shot from there flies over it.
    public bool ShotPasses(int x, int y, int level)
    {
        if (cliffHeight == null || x < 0 || y < 0 || x >= size || y >= size) return false;
        int h = cliffHeight[y * size + x];
        return h >= 0 && h <= level;
    }

    /// LineClear for a shot from the first point: the cliffs no higher than its level do not stop it.
    public bool ShotClear(Vector2 from, Vector2 to, float radius)
    {
        int level = HeightAt(from);
        float length = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.25f));
        for (int i = 0; i <= steps; i++)
        {
            var p = Vector2.Lerp(from, to, (float)i / steps);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var cell = CellOf(p + new Vector2(sx * radius, sy * radius));
                    if (!IsOpen(cell.x, cell.y) && !ShotPasses(cell.x, cell.y, level)) return false;
                }
        }
        return true;
    }

    /// Test bench (AiScenarios): a cliff of level 1 along row y, from wall to wall, the ground above it on level 1.
    public void TestCliff(int row)
    {
        cellHeight = new int[size * size];
        cliffHeight = new int[size * size];
        for (int k = 0; k < cliffHeight.Length; k++) cliffHeight[k] = -1;
        var inside = Interior;
        for (int x = inside.xMin; x < inside.xMax; x++)
        {
            open[x, row] = false;
            cliffHeight[row * size + x] = 1;
            wallMap.SetTile(new Vector3Int(x, row, 0), InvisibleWall());
            for (int y = row + 1; y < inside.yMax; y++) cellHeight[y * size + x] = 1;
        }
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

    Tile GetTile(DungeonTileset decor, int chunk, bool solid)
    {
        if (tiles.TryGetValue((decor, chunk, solid), out var tile) && tile != null) return tile;
        int chunkSize = decor.chunkSize, cell = chunkSize + 2 * decor.padding;
        var rect = new Rect(chunk % decor.columns * cell + decor.padding,
            decor.texture.height - (chunk / decor.columns + 1) * cell + decor.padding, chunkSize, chunkSize);
        tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Sprite.Create(decor.texture, rect, new Vector2(0.5f, 0.5f), chunkSize, 0, SpriteMeshType.FullRect);
        tile.colliderType = solid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
        tiles[(decor, chunk, solid)] = tile;
        return tile;
    }
}
