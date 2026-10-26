using UnityEngine;

/// The way to the islet of a quarter whose gear comes from a boss (TimeGears): the rope bridge over the sky, behind a
/// portal shut until the team has the key (KeyDungeon); walking out on it takes the team straight to the boss's arena
/// (the islet seen at its end is only seen), where the way out stays shut until the boss falls, which gives the gear;
/// back on the coast at the bridge's foot. Without room for a bridge, a portal on the coast. Added by IslandRun.
public class BossPortal : IslandStairs
{
    TimeGears gears;
    // Looked up when first needed: the island is built in the frame these stairs are added, before their Start.
    TimeGears Gears => gears != null ? gears : gears = GetComponent<TimeGears>();
    TimeGears.Quarter Quarter => Gears != null ? Gears.Here : null;

    protected override Vector2Int Tile => Quarter == null || Quarter.mode == TimeGears.Fragments ? new Vector2Int(-1, -1)
        : Quarter.HasBridge ? Quarter.triggerTile : Quarter.portalTile;
    protected override int ZoneOf(Vector2Int tile) => Quarter != null && Quarter.HasBridge ? island.Zone : base.ZoneOf(tile);
    protected override Vector2Int ReturnTile => Quarter != null ? Quarter.portalTile : Tile;
    protected override bool ShowMarker => Quarter == null || !Quarter.HasBridge; // the bridge itself leads on
    protected override string StairsName => "Portail de l'îlot";
    protected override string RoomName => "Îlot du boss";
    protected override Color Colour => new(0.75f, 0.5f, 1f);
    protected override SecretRooms.Room Room => new() { type = TimeGears.BossRoomType };
    protected override string Kind => "boss";

    protected override bool Locked(out string why)
    {
        why = "Il faut la clé de l'îlot : elle est au fond du donjon de la clé";
        return Quarter != null && !Quarter.hasKey;
    }

    // The islet is the quarter's last room slot.
    protected override RoomContent Stored(int room) => Quarter?.rooms[^1];
    protected override void Store(int room, RoomContent content) { if (Quarter != null) Quarter.rooms[^1] = content; }

    protected override RoomContent Make(int room, string habitat, int decor, int rank, float distance, System.Random random)
    {
        var q = Quarter;
        var content = RoomContent.Draw(Kind, habitat, decor, rank, distance, random, RoomContent.Bosses);
        content.bossRoomType = TimeGears.BossRoomType;
        (content.theme, content.variant) = TimeGears.ArenaRoom(q); // an arena made for its types
        content.spawnBoss = (arena, spawner, at) => Gears.SpawnBoss(q, spawner, at);
        return content;
    }

    protected override void Finished(int room, RoomContent content) => Gears.BossBeaten(Quarter, content.EndAt ?? Vector2.zero);
}
