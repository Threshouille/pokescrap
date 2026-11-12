using UnityEngine;
using UnityEngine.Tilemaps;

/// The rampart that hides the island's other zones (GDD « Entrées des donjons », lot 4b): its tiles, cut from the
/// sheets of Resources/Effects made by Design/effects_v22_remparts/export.py. rampart_tiles: the top of a tile by its
/// 8 neighbours (bit set where rampart: 1 N, 2 NE, 4 E, 8 SE, 16 S, 32 SW, 64 W, 128 NW; tile n at column n % 16,
/// row n / 16); rampart_faces: a face of one tile (left end, middle, right end, alone); rampart_fill: the darker
/// stone further than FillFrom tiles from the zone (2 × 2 tiles).
public static class RampartTiles
{
    public enum Face { Left, Middle, Right, Alone }

    public const int FillFrom = 3; // tiles from the nearest tile of the zone (a face counts as rampart)

    static Tile[] tops, faces, fills;
    static bool missing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset()
    {
        tops = faces = fills = null;
        missing = false;
    }

    /// The sheets are there (else the walls of the décors stay).
    public static bool Ready => Load();

    public static Tile Top(int mask) => Load() ? tops[mask & 255] : null;
    public static Tile FaceTile(Face face) => Load() ? faces[(int)face] : null;
    public static Tile Fill(int x, int y) => Load() ? fills[(y & 1) * 2 + (x & 1)] : null;

    /// The bit of a neighbour in a mask (dx, dy: Unity's axes, y up).
    public static int Bit(int dx, int dy) => (dx, dy) switch
    {
        (0, 1) => 1, (1, 1) => 2, (1, 0) => 4, (1, -1) => 8, (0, -1) => 16, (-1, -1) => 32, (-1, 0) => 64, _ => 128,
    };

    static bool Load()
    {
        if (tops != null && tops[0] != null) return true;
        if (missing) return false;
        var top = Resources.Load<Texture2D>("Effects/rampart_tiles");
        var face = Resources.Load<Texture2D>("Effects/rampart_faces");
        var fill = Resources.Load<Texture2D>("Effects/rampart_fill");
        if (top == null || face == null || fill == null) { missing = true; return false; }
        tops = new Tile[256];
        for (int n = 0; n < 256; n++) tops[n] = Make(top, n % 16, n / 16);
        faces = new Tile[4];
        for (int n = 0; n < 4; n++) faces[n] = Make(face, n, 0);
        // The fill's top row is the tiles with an odd y (Unity's rows go up).
        fills = new[] { Make(fill, 0, 1), Make(fill, 1, 1), Make(fill, 0, 0), Make(fill, 1, 0) };
        return true;
    }

    // A solid tile of 24 px at column, row (from the top) of a sheet.
    static Tile Make(Texture2D sheet, int column, int row)
    {
        var rect = new Rect(column * 24, sheet.height - (row + 1) * 24, 24, 24);
        var tile = ScriptableObject.CreateInstance<Tile>();
        tile.sprite = Sprite.Create(sheet, rect, new Vector2(0.5f, 0.5f), 24f, 0, SpriteMeshType.FullRect);
        tile.colliderType = Tile.ColliderType.Grid;
        return tile;
    }
}
