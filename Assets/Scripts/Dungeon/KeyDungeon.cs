using UnityEngine;

/// The key dungeon of a quarter whose gear comes from a boss (TimeGears): its entrance leads to rooms one after the
/// other (time_gears.json boss.dungeonRooms: an ambush, then a guardian by a chest); the last one over gives the key
/// of the boss's islet. Its rooms are kept per quarter. Added by IslandRun.
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

    protected override RoomContent Stored(int room) => Quarter != null && room < Quarter.rooms.Length - 1 ? Quarter.rooms[room] : null;
    protected override void Store(int room, RoomContent content) { if (Quarter != null && room < Quarter.rooms.Length - 1) Quarter.rooms[room] = content; }

    protected override RoomContent Make(int room, string habitat, int decor, int rank, float distance, System.Random random) =>
        RoomContent.Draw(Kind, habitat, decor, rank, distance, random, TimeGears.DungeonRooms[Mathf.Clamp(room, 0, Floors - 1)]);

    protected override void Finished(int room, RoomContent content)
    {
        if (room == Floors - 1) Gears.GiveKey(Quarter);
    }
}
