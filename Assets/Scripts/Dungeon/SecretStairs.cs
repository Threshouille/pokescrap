using UnityEngine;

/// The secret stairs of the island (IslandPlan.secret; GDD, "Escaliers secrets"), added by IslandRun: shown once
/// nothing covers them any more (a cracked rock is broken), they lead behind a loading screen to a room of their own
/// (secret_rooms.json "room", empty for now), whose stairs bring the team back onto them; then they are gone.
public class SecretStairs : MonoBehaviour
{
    const float Reach = 0.8f, Leave = 1.6f; // tiles: walking onto stairs; stepping off them again

    IslandRun island;
    PlayerController team;
    DungeonGenerator arena;
    GameObject stairs, back;
    bool used, inRoom, armed;
    int zone;
    readonly System.Random rng = new();

    /// The stairs still wait in that zone (the mini-map marks them in tests).
    public bool Waiting(int inZone) =>
        island != null && island.Plan != null && !used && !inRoom && island.Plan.secret.x >= 0
        && island.Plan.region[island.Plan.Index(island.Plan.secret.x, island.Plan.secret.y)] == inZone;

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
        stairs = Marker("Escalier secret", island.Cell(island.Plan.secret), new Color(0.3f, 1f, 0.9f));
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
            // Hidden while the cracked rock stands on them.
            var cell = arena.CellOf(target.transform.position);
            bool covered = Obstacles.At(cell.x, cell.y)?.key == SecretRooms.Data.rock;
            if (target.activeSelf == covered) target.SetActive(!covered);
            if (covered) return;
        }
        float d = Vector2.Distance(at, target.transform.position);
        if (armed && d <= Reach)
        {
            armed = false;
            if (inRoom) LoadingScreen.Run(island.Plan.ZoneName(zone), GoBack);
            else LoadingScreen.Run("Salle secrète", Enter);
            return;
        }
        if (d > Leave) armed = true;
    }

    void Enter()
    {
        var r = SecretRooms.Data.room;
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
        used = true;
        foreach (var zoneArea in FindObjectsByType<global::Zone>()) Destroy(zoneArea.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        island.Return(zone, island.Plan.secret);
    }
}
