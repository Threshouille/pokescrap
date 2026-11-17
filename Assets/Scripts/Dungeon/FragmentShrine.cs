using UnityEngine;

/// The shrine of a Time Gear fragment's guardian (TimeGears, fragment guard "shrine"; GDD « Quart du volcan »,
/// gardiens des fragments): visible from afar (a stone dolmen, a bubble and an icon on the mini-map, the fragment's
/// exact hint), a little hard to reach yet reached on foot; it leads to the guardian's arena (its Tera type's, else its
/// own types'), where the way out stays shut until the guardian falls and hands the fragment over; back in front of the
/// shrine, which is then gone. One component per fragment ("index"); added by IslandRun.
public class FragmentShrine : IslandStairs
{
    public int index;

    TimeGears gears;
    // Looked up when first needed: the island is built in the frame these stairs are added, before their Start.
    TimeGears Gears => gears != null ? gears : gears = GetComponent<TimeGears>();
    TimeGears.Quarter Quarter => Gears != null ? Gears.Here : null;
    TimeGears.Fragment Fragment => Quarter != null && Quarter.mode == TimeGears.Fragments && index < Quarter.fragments.Count
        && Quarter.fragments[index].guard == TimeGears.Shrine ? Quarter.fragments[index] : null;

    protected override Vector2Int Tile => Fragment != null && !Fragment.taken ? Fragment.tile : new Vector2Int(-1, -1);
    protected override Vector2Int ReturnTile => Fragment != null ? Fragment.tile : Tile;
    protected override string StairsName => "Sanctuaire du gardien";
    protected override string RoomName => "Sanctuaire du gardien";
    protected override Color Colour => new(1f, 0.75f, 0.3f);
    protected override SecretRooms.Room Room => new() { type = TimeGears.BossRoomType };
    protected override string Kind => "shrine";
    protected override bool OneUse => true;
    protected override EntranceArt.Icon Icon => EntranceArt.Icon.Boss;
    protected override float BubbleHeight => EntranceArt.Data.gateBubbleHeight;

    // A stone dolmen over stairs going down, of the stone of its ground, its runes glowing (the key dungeon's, for now).
    protected override GameObject MakeEntrance(Vector2 at)
    {
        var go = new GameObject(StairsName);
        go.transform.position = at;
        var foot = new Vector2(0f, -0.5f);
        EntranceSprite.Make(go.transform, foot, EntranceArt.Foot, EntranceArt.StandOrder).Still("key_dungeon", EntranceArt.KeyMaterialOf(BiomeKey));
        EntranceSprite.Make(go.transform, foot - new Vector2(0f, 0.01f), EntranceArt.Foot, EntranceArt.StandOrder, unlit: true).Play("key_dungeon_glow");
        return go;
    }

    protected override RoomContent Stored(int room) => Fragment?.room;
    protected override void Store(int room, RoomContent content) { if (Fragment != null) Fragment.room = content; }

    protected override RoomContent Make(int room, string habitat, int decor, int rank, float distance, System.Random random)
    {
        var q = Quarter;
        var f = Fragment;
        var content = RoomContent.Draw(Kind, habitat, decor, rank, distance, random, RoomContent.Bosses);
        content.bossRoomType = TimeGears.BossRoomType;
        (content.theme, content.variant) = TimeGears.FragmentArena(f);
        content.spawnBoss = (arena, spawner, at) => Gears.SpawnFragmentGuardian(q, f, spawner, at);
        return content;
    }

    protected override void Finished(int room, RoomContent content) => Gears.TakeFragment(Quarter, Fragment);
}
