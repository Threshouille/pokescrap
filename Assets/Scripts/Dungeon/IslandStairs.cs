using UnityEngine;

/// Stairs of the island leading behind a loading screen to a room of their own (rooms.json), whose stairs bring the
/// team back onto them: the secret stairs (SecretStairs), the sealed cave's (CaveStairs), the key dungeon (KeyDungeon,
/// several rooms one after the other) and the boss islet's portal (BossPortal, locked without its key); added by
/// IslandRun. Each room has its content (RoomContent).
public abstract class IslandStairs : MonoBehaviour
{
    const float Reach = 0.8f, Leave = 1.6f; // tiles: walking onto stairs; stepping off them again

    protected IslandRun island;
    PlayerController team;
    DungeonGenerator arena;
    GameObject stairs, back;
    protected bool used;
    bool inRoom, armed;
    int zone, floor;
    GameObject next; // stairs on to the next room, once this one is over
    float refusedAt = -10f;
    readonly System.Random rng = new();

    protected IslandPlan Plan => island != null ? island.Plan : null;
    /// Their island tile, x = -1 for none.
    protected abstract Vector2Int Tile { get; }
    protected abstract string StairsName { get; }
    protected abstract string RoomName { get; }
    protected abstract Color Colour { get; }
    protected abstract SecretRooms.Room Room { get; }
    /// Its kind for room_contents.json: "secret" or "cave".
    protected abstract string Kind { get; }

    /// What waits in the room being played (RoomContent), drawn on the first visit.
    public RoomContent Content { get; private set; }
    /// The room being played (0: the first).
    public int Floor => floor;
    /// Tests: in one of their rooms; where their stairs, the way back and the next room's stairs stand (null: none).
    public bool InRoom => inRoom;
    public Vector2? StairsAt => stairs != null ? stairs.transform.position : null;
    public Vector2? BackAt => back != null ? back.transform.position : null;
    public Vector2? NextAt => next != null ? next.transform.position : null;
    /// How many rooms follow one another behind them.
    protected virtual int Floors => 1;
    /// Shut for now, and why (the team is told when it steps on them).
    protected virtual bool Locked(out string why) { why = null; return false; }
    /// The content of that room (drawn by default; kept per quarter by the stairs that move from one to another).
    protected virtual RoomContent Stored(int room) => room == 0 ? first : null;
    protected virtual void Store(int room, RoomContent content) { if (room == 0) first = content; }
    protected virtual RoomContent Make(int room, string habitat, int decor, int rank, float distance, System.Random random) =>
        RoomContent.Draw(Kind, habitat, decor, rank, distance, random, forceContent);
    /// A room's content is over (its chest opened, its foes beaten).
    protected virtual void Finished(int room, RoomContent content) { }
    RoomContent first;
    bool finishedHere;
    /// Tests: the content its room will have (treasure, ambush, guardian), instead of a draw.
    public string forceContent;
    /// Gone once used (the secret stairs), or there for good.
    protected virtual bool OneUse => false;
    /// The zone their tile belongs to (a bridge over the sky: its quarter's).
    protected virtual int ZoneOf(Vector2Int tile) => Plan.region[Plan.Index(tile.x, tile.y)];
    /// Where the team comes back from their rooms (their own tile by default).
    protected virtual Vector2Int ReturnTile => Tile;
    /// Drawn (stairs), or an unseen place to walk onto (a bridge leading on).
    protected virtual bool ShowMarker => true;
    /// Their icon in their bubble and on the mini-map (EntranceArt).
    protected virtual EntranceArt.Icon Icon => EntranceArt.Icon.Down;
    /// Height of their bubble above their tile, in tiles.
    protected virtual float BubbleHeight => EntranceArt.Data.bubbleHeight;
    /// The sub-biome (or theme) key of their tile: the rim of their stairs.
    protected string BiomeKey => Plan != null && Tile.x >= 0 ? Plan.biome[Plan.Index(Tile.x, Tile.y)]?.key : null;
    EntranceBubble bubble;

    /// Found on the island: their tile revealed on the mini-map and nothing covering them (their icon shows there).
    public bool Found => Waiting(island.Zone) && island.Revealed(Tile) && arena != null
                         && !Hidden(arena.CellOf(island.Cell(Tile)));
    public EntranceArt.Icon MapIcon => Icon;

    /// Their look on the island at "at" (stairs with the rim of their ground and a glow by default); the object they
    /// stand on, its bubble added after.
    protected virtual GameObject MakeEntrance(Vector2 at) =>
        EntranceArt.Stairs(StairsName, at, "entrance_rim_biomes", EntranceArt.RimOf(BiomeKey));

    /// The team's leader at "at" has walked onto them ("d": tiles from their tile); a whole place (a cave) can be.
    protected virtual bool Reached(Vector2 at, float d) => d <= Reach;
    /// The leader has stepped off them again (they can be taken once more).
    protected virtual bool SteppedOff(Vector2 at, float d) => d > Leave;

    /// Hidden for now (under a cracked rock, behind a shut door).
    protected virtual bool Hidden(Vector2Int cell) => false;

    /// Where they stand on the island and their colour (the mini-map marks them in tests).
    public Vector2Int MapTile => Tile;
    public Color MapColour => Colour;

    /// The stairs still wait in that zone (the mini-map marks them in tests).
    public bool Waiting(int inZone) =>
        Plan != null && !used && !inRoom && Tile.x >= 0 && ZoneOf(Tile) == inZone;

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
        if (next != null) Destroy(next);
        stairs = back = next = null;
        used = inRoom = false;
        Content?.Leave();
        Content = null;
        first = null;
        floor = 0;
    }

    void OnBuilt() => Refresh();

    /// Puts their marker back in the zone (the zone was built, or what they lead to changed: TimeGears' G).
    public void Refresh()
    {
        team = FindAnyObjectByType<PlayerController>();
        arena = DungeonGenerator.Current;
        if (stairs != null) Destroy(stairs);
        stairs = null;
        if (!Waiting(island.Zone)) return;
        zone = island.Zone;
        bubble = null;
        if (ShowMarker)
        {
            stairs = MakeEntrance(island.Cell(Tile));
            PointLights.Attach(stairs, "stairs");
            bubble = EntranceArt.Bubble(stairs.transform, Icon, BubbleHeight);
        }
        else stairs = new GameObject(StairsName) { transform = { position = island.Cell(Tile) } }; // an unseen place
        armed = false;
    }

    // The stairs of a room: down to the next one (it shows in a burst of dust), or up to the island.
    static GameObject RoomStairs(string name, Vector2 at, bool down)
    {
        var go = EntranceArt.Stairs(name, at, down ? "entrance_stairs_down" : "entrance_stairs_up", glow: down);
        PointLights.Attach(go, "stairs");
        if (down) EffectPlayer.Spawn("entrance_reveal", at, Vector2.right);
        return go;
    }

    // The way back of a room: shut by a grate while its ambush lasts.
    void ShutBack(bool shut)
    {
        var look = back != null ? back.GetComponentInChildren<EntranceSprite>() : null;
        string sheet = shut ? "entrance_stairs_locked" : "entrance_stairs_up";
        if (look != null && look.Sheet != sheet) look.Still(sheet);
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
            if (hidden)
            {
                // Away from them while covered: taken as soon as they show (leaves blown away under the team).
                if (Vector2.Distance(at, target.transform.position) > Leave) armed = true;
                return;
            }
            bubble?.Show(island.Revealed(Tile));
        }
        if (inRoom && Content != null)
        {
            // The room's content plays; an ambush shuts the way back until it is over (the stairs go grey).
            Content.Tick(at);
            // Shut: the way back is not there until the room is over.
            ShutBack(Content.Locked);
            if (Content.Done && !finishedHere)
            {
                finishedHere = true;
                Finished(floor, Content);
                if (floor + 1 < Floors) next = RoomStairs("Escalier suivant", NearestOpen(Content.EndAt ?? at), true);
            }
            if (Content.Locked) return;
            if (next != null && armed && Vector2.Distance(at, next.transform.position) <= Reach)
            {
                armed = false;
                int on = floor + 1;
                LoadingScreen.Run($"{RoomName} — salle {on + 1}", () => EnterRoom(on));
                return;
            }
        }
        float d = Vector2.Distance(at, target.transform.position);
        bool there = inRoom ? d <= Reach : Reached(at, d);
        if (!inRoom && there && Locked(out string why))
        {
            if (Time.time - refusedAt > 2f && team.GetComponent<Progression>() is { } progression) progression.Announce(why, Notice.Alert);
            refusedAt = Time.time;
            return;
        }
        if (armed && there)
        {
            armed = false;
            EffectPlayer.Spawn("entrance_enter", target.transform.position, Vector2.right);
            if (inRoom) LoadingScreen.Run(Plan.ZoneName(zone), GoBack);
            else LoadingScreen.Run(RoomName, () => EnterRoom(0));
            return;
        }
        if ((inRoom ? d > Leave : SteppedOff(at, d)) && (next == null || Vector2.Distance(at, next.transform.position) > Leave)) armed = true;
    }

    void EnterRoom(int index)
    {
        Content?.Leave();
        if (next != null) Destroy(next);
        next = null;
        floor = index;
        // Coming from the island: what lies on its ground is picked up (it would show in the room).
        if (!inRoom) FindAnyObjectByType<Loot>()?.CollectAll();
        Content = Stored(index);
        if (Content == null)
        {
            int i = Plan.Index(Tile.x, Tile.y);
            var biome = Plan.biome[i];
            var arrival = Plan.Arrival(zone);
            var bounds = Plan.ZoneBounds(zone);
            float far = Mathf.Max(1f, new Vector2(bounds.width, bounds.height).magnitude);
            Content = Make(index, biome?.key, biome != null && biome.tilesets.Length > 0 ? biome.tilesets[0] : 0,
                Mathf.Max(0, System.Array.IndexOf(Plan.order, zone)), Vector2.Distance(Tile, arrival) / far, rng);
            Store(index, Content);
        }
        finishedHere = Content.Done;
        var r = Room;
        string theme = string.IsNullOrEmpty(r.theme) ? null : r.theme, variant = string.IsNullOrEmpty(r.variant) ? null : r.variant;
        // Played already (stairs that stay): the plain room of the data.
        // The room of its content, the same one each time (played already: empty).
        var room = Content.Room() ?? RoomCatalog.Pick(r.type, rng, theme, variant) ?? RoomCatalog.Pick(null, rng);
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
        back = RoomStairs("Escalier de retour", arena.CentreOf(cell.x, cell.y), false);
        armed = cell != spawn;
        Content.Begin(arena, FindAnyObjectByType<EnemySpawner>(), arena.SpawnPoint);
        // A room already over: straight on to the next one.
        if (Content.Done && floor + 1 < Floors) next = RoomStairs("Escalier suivant", NearestOpen(arena.SpawnPoint + Vector2.right * 3f), true);
    }

    // The free tile nearest to a point (stairs never land in a wall or an obstacle).
    Vector2 NearestOpen(Vector2 point)
    {
        var c = arena.CellOf(point);
        for (int r = 0; r < 12; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                    int x = c.x + dx, y = c.y + dy;
                    if (arena.Walkable(x, y) && Obstacles.At(x, y) == null) return arena.CentreOf(x, y);
                }
        return arena.SpawnPoint;
    }


    void GoBack()
    {
        if (back != null) Destroy(back);
        if (next != null) Destroy(next);
        back = next = null;
        inRoom = false;
        used = OneUse;
        Content?.Leave();
        floor = 0;
        foreach (var zoneArea in FindObjectsByType<global::Zone>()) Destroy(zoneArea.gameObject);
        foreach (var shot in FindObjectsByType<Projectile>()) Destroy(shot.gameObject);
        island.Return(zone, ReturnTile);
    }
}
