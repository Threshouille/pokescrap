using UnityEngine;

/// The key dungeon of a quarter whose gear comes from a boss (TimeGears): its entrance leads to rooms one after the
/// other (time_gears.json boss.dungeonRooms: an ambush, then a guardian by a chest), in the theme of the boss's types
/// and with Pokémon of those types; the last one over gives the key of the boss's islet. Its rooms are kept per
/// quarter. Added by IslandRun.
public class KeyDungeon : IslandStairs
{
    TimeGears gears;
    // Looked up when first needed: the island is built in the frame these stairs are added, before their Start.
    TimeGears Gears => gears != null ? gears : gears = GetComponent<TimeGears>();
    TimeGears.Quarter Quarter => Gears != null ? Gears.Here : null;

    protected override Vector2Int Tile => Quarter != null && Quarter.mode != TimeGears.Fragments ? Quarter.dungeonTile : new Vector2Int(-1, -1);
    protected override string StairsName => "Donjon de la clé";
    protected override string RoomName => "Donjon de la clé";
    protected override Color Colour => new(0.85f, 0.85f, 1f);
    protected override SecretRooms.Room Room => new() { type = "rest" };
    protected override string Kind => "key";
    protected override int Floors => TimeGears.DungeonRooms.Length;
    // The mountain's cave when it has one: going under its roof leads in, the team comes back in front of its mouth.
    bool InCave => Quarter?.cave != null;
    protected override bool ShowMarker => !InCave;
    protected override Vector2Int ReturnTile => InCave ? Quarter.caveExit : Tile;
    protected override bool Reached(Vector2 at, float d) => InCave ? UnderRoof(at) : base.Reached(at, d);
    protected override bool SteppedOff(Vector2 at, float d) => InCave ? !UnderRoof(at) : base.SteppedOff(at, d);
    bool UnderRoof(Vector2 at)
    {
        var t = island.TileOf(at);
        return t.x >= 0 && t.y >= 0 && t.x < Plan.size && t.y < Plan.size && Quarter.cave.Contains(Plan.Index(t.x, t.y));
    }

    // Otherwise a rift on the ground (story art).
    protected override Sprite[] Frames => RomArt.Frames("GroundObjects/rift_frames", 4);
    protected override float FramesScale => 0.8f;
    protected override float FrameTime => 6f / 60f;

    protected override RoomContent Stored(int room) => Quarter != null && room < Quarter.rooms.Length - 1 ? Quarter.rooms[room] : null;
    protected override void Store(int room, RoomContent content) { if (Quarter != null && room < Quarter.rooms.Length - 1) Quarter.rooms[room] = content; }

    protected override RoomContent Make(int room, string habitat, int decor, int rank, float distance, System.Random random)
    {
        var q = Quarter;
        var content = RoomContent.Draw(Kind, habitat, decor, rank, distance, random, TimeGears.DungeonRooms[Mathf.Clamp(room, 0, Floors - 1)]);
        content.theme = TimeGears.DungeonTheme(q);
        content.types = TimeGears.BossTypes(q);
        content.clearFirst = true; // the chest of the key, and the way on, once the room's foes are beaten
        if (room == Floors - 1) content.spawnGuardian = (spawner, at) => Gears.SpawnGuardian(q, spawner, at);
        return content;
    }

    protected override void Finished(int room, RoomContent content)
    {
        if (room == Floors - 1) Gears.GiveKey(Quarter);
    }
}
