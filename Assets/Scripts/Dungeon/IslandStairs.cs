using UnityEngine;

/// Stairs of the island leading behind a loading screen to a room of their own (rooms.json), whose stairs bring the
/// team back onto them: the secret stairs (SecretStairs) and the sealed cave's (CaveStairs), added by IslandRun.
public abstract class IslandStairs : MonoBehaviour
{
    const float Reach = 0.8f, Leave = 1.6f; // tiles: walking onto stairs; stepping off them again

    protected IslandRun island;
    PlayerController team;
    DungeonGenerator arena;
    GameObject stairs, back;
    protected bool used;
    bool inRoom, armed;
    int zone;
    readonly System.Random rng = new();

    protected IslandPlan Plan => island != null ? island.Plan : null;
    /// Their island tile, x = -1 for none.
    protected abstract Vector2Int Tile { get; }
    protected abstract string StairsName { get; }
    protected abstract string RoomName { get; }
    protected abstract Color Colour { get; }
    protected abstract SecretRooms.Room Room { get; }
    /// Gone once used (the secret stairs), or there for good.
    protected virtual bool OneUse => false;
    /// Hidden for now (under a cracked rock, behind a shut door).
    protected virtual bool Hidden(Vector2Int cell) => false;

    /// The stairs still wait in that zone (the mini-map marks them in tests).
    public bool Waiting(int inZone) =>
        Plan != null && !used && !inRoom && Tile.x >= 0 && Plan.region[Plan.Index(Tile.x, Tile.y)] == inZone;

    void Awake()
    {
        island = GetComponent<IslandRun>();
        island.Built += OnBuilt;
    }

    void OnDestroy()
    {
        if (island != null) island.Built -= OnBuilt;
        Clear();
    }

    /// Forgets the stairs (the island is left).
    public void Clear()
    {
        if (stairs != null) Destroy(stairs);
        if (back != null) Destroy(back);
        stairs = back = null;
        used = inRoom = false;
    }

    void OnBuilt()
    {
        team = FindAnyObjectByType<PlayerController>();
        arena = DungeonGenerator.Current;
        if (stairs != null) Destroy(stairs);
        stairs = null;
        if (!Waiting(island.Zone)) return;
        zone = island.Zone;
        stairs = Marker(StairsName, island.Cell(Tile), Colour);
        armed = false;
    }

    static GameObject Marker(string name, Vector2 at, Color colour)
    {
        var go = new GameObject(name);
        go.transform.position = at;
        var outer = go.AddComponent<SpriteRenderer>();
        outer.sprite = Shapes.Square;
        outer.color = new Color(0.1f, 0.1f, 0.12f);
        outer.sortingOrder = -1; // under the light square inside (a tie would hide it)
        var inner = new GameObject("Marches").AddComponent<SpriteRenderer>();
        inner.transform.SetParent(go.transform, false);
        inner.transform.localScale = Vector3.one * 0.6f;
        inner.sprite = Shapes.Square;
        inner.color = colour;
        return go;
    }

    void FixedUpdate()
    {
        if (team == null || team.Controlled == null || LoadingScreen.Busy) return;
        Vector2 at = team.Controlled.transform.position;
        var target = inRoom ? back : stairs;
        if (target == null) return;
        if (!inRoom)
        {
            // Hidden while something covers them, or while the team is in another stairs' room.
            var cell = arena.CellOf(target.transform.position);
            bool hidden = island.Away || Hidden(cell);
            if (target.activeSelf == hidden) target.SetActive(!hidden);
            if (hidden) return;
        }
        float d = Vector2.Distance(at, target.transform.position);
        if (armed && d <= Reach)
        {
            armed = false;
            if (inRoom) LoadingScreen.Run(Plan.ZoneName(zone), GoBack);
            else LoadingScreen.Run(RoomName, Enter);
            return;
        }
        if (d > Leave) armed = true;
    }

    void Enter()
    {
        var r = Room;
        string theme = string.IsNullOrEmpty(r.theme) ? null : r.theme, variant = string.IsNullOrEmpty(r.variant) ? null : r.variant;
        var room = RoomCatalog.Pick(r.type, rng, theme, variant) ?? RoomCatalog.Pick(null, rng);
        if (stairs != null) Destroy(stairs);
        stairs = null;
        inRoom = true;
        island.Away = true;
        foreach (var zoneArea in FindObjectsByType<global::Zone>()) Destroy(zoneArea.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        arena.island = null;
        arena.level = null;
        arena.room = room;
        arena.Generate();
        if (team != null) team.PrepareForNextArena(arena.SpawnPoint, 0f);
        // The way back: beside the arrival.
        var spawn = arena.CellOf(arena.SpawnPoint);
        var cell = spawn;
        foreach (var d in new[] { new Vector2Int(0, -2), new Vector2Int(2, 0), new Vector2Int(-2, 0), new Vector2Int(0, 2) })
            if (arena.Walkable(spawn.x + d.x, spawn.y + d.y) && Obstacles.At(spawn.x + d.x, spawn.y + d.y) == null) { cell = spawn + d; break; }
        back = Marker("Escalier de retour", arena.CentreOf(cell.x, cell.y), new Color(1f, 0.85f, 0.3f));
        armed = cell != spawn;
    }

    void GoBack()
    {
        if (back != null) Destroy(back);
        back = null;
        inRoom = false;
        used = OneUse;
        foreach (var zoneArea in FindObjectsByType<global::Zone>()) Destroy(zoneArea.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        island.Return(zone, Tile);
    }
}
