using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// The volcano's summit dungeon (Resources/Data/summit_dungeon.json; GDD « Donjon du volcan »): optional, its entrance
/// on the crater's rim (IslandPlan.summitDungeon, the stairs of the cone lead up to it). Behind it, levels crossed on
/// foot one after the other (LevelPlan, GDD « Niveaux continus »: « Galeries de magma », then « Cœur du volcan »), each
/// with its mini-map in the fog and its way on in its last zone, then the boss's chamber; the way back leads to the
/// rim. Its zones' foes, arenas and chests: LevelContent; the boss comes next.
/// Added by IslandRun.
public class SummitDungeon : IslandStairs
{
    [Serializable]
    public class Chamber
    {
        public string name = "Chambre du boss", type = "subboss", theme = "lava", variant = "";
        public int[] pokemon = { 527, 415 }; // one drawn per island (Heatran or Groudon)
        public float recruitChance = 0.15f, stoneChance = 0.5f;
        public string chestIcon = "pmd_Chest_Gold", announce = "Le gardien du volcan : {0} !";
        public int heldTier = 3; // a held item of that tier beside the chest
    }

    [Serializable]
    public class Settings
    {
        public string name = "Donjon du volcan";
        public LevelPlan.Settings[] levels = new LevelPlan.Settings[0];
        public LevelPlan.SubTheme[] subThemes = new LevelPlan.SubTheme[0];
        public Chamber boss = new();
        public LevelContent.Settings zones = new();
    }

    static Settings data;

    public static Settings Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/summit_dungeon");
            data = asset != null ? JsonUtility.FromJson<Settings>(asset.text) : new Settings();
            return data;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => data = null;

    // Its floors, kept for the island: the same levels and the same chamber when the team comes back.
    IslandPlan madeFor;
    LevelPlan[] levels = new LevelPlan[0];
    RoomContent[] contents = new RoomContent[0];
    int seed;

    /// The levels behind it as they were drawn (tests).
    public LevelPlan LevelAt(int floor) => floor >= 0 && floor < levels.Length ? levels[floor] : null;

    /// What lives in a level's zones (tests).
    public LevelContent ContentAt(int floor) => floor >= 0 && floor < contents.Length ? contents[floor]?.levelContent : null;

    /// The boss of this island's summit dungeon (Chamber.pokemon, one drawn per island; 0 without sprites).
    public int BossId
    {
        get
        {
            Prepare();
            var ids = Data.boss.pokemon.Where(PokemonRoster.Has).ToArray();
            return ids.Length == 0 ? 0 : ids[(int)((uint)(seed * 2654435761u) % (uint)ids.Length)];
        }
    }

    TimeGears gears;
    TimeGears Gears => gears != null ? gears : gears = GetComponent<TimeGears>();

    void Prepare()
    {
        if (madeFor == Plan) return;
        madeFor = Plan;
        levels = new LevelPlan[Data.levels.Length];
        contents = new RoomContent[Data.levels.Length + 1];
        seed = Plan != null ? Plan.size * 7919 + Plan.summitDungeon.x * 31 + Plan.summitDungeon.y : 1;
    }

    protected override Vector2Int Tile => Plan != null ? Plan.summitDungeon : new Vector2Int(-1, -1);
    protected override string StairsName => Data.name;
    protected override string RoomName => Data.name;
    protected override Color Colour => new(1f, 0.45f, 0.2f);
    protected override SecretRooms.Room Room => new() { type = Data.boss.type, theme = Data.boss.theme, variant = Data.boss.variant };
    protected override string Kind => "summit";
    protected override int Floors => Data.levels.Length + 1;
    protected override EntranceArt.Icon Icon => EntranceArt.Icon.Boss;

    protected override string FloorTitle(int room) =>
        room < Data.levels.Length && !string.IsNullOrEmpty(Data.levels[room].name) ? $"{Data.name} — {Data.levels[room].name}"
        : room >= Data.levels.Length ? $"{Data.name} — {Data.boss.name}" : base.FloorTitle(room);

    protected override LevelPlan LevelFor(int room)
    {
        Prepare();
        if (room < 0 || room >= levels.Length) return null;
        return levels[room] ??= LevelPlan.Generate(new System.Random(seed + room * 104729), Data.levels[room], Data.subThemes);
    }

    // The boss fallen: a big chest (a mega stone more often) and a held item of a high tier beside it.
    protected override void Finished(int room, RoomContent content)
    {
        if (room < Data.levels.Length || content.key != RoomContent.Bosses) return;
        var loot = FindAnyObjectByType<Loot>();
        if (loot == null) return;
        Vector2 at = content.EndAt ?? Vector2.zero;
        loot.SpawnChest(at + Vector2.right * 1.5f, anywhere: true, Data.boss.stoneChance, Data.boss.chestIcon);
        if (HeldItems.Roll(new System.Random(seed + 17)) is { } held)
            loot.PlaceHeld(held.key, at + new Vector2(-1.5f, 0f), held.tiered ? Mathf.Min(Data.boss.heldTier, HeldItems.MaxTier) : 0);
        if (FindAnyObjectByType<Progression>() is { } progression)
            progression.Announce($"{Data.name} : le gardien est tombé !", Notice.Loot);
    }

    protected override RoomContent Stored(int room)
    {
        Prepare();
        return room >= 0 && room < contents.Length ? contents[room] : null;
    }

    protected override void Store(int room, RoomContent content)
    {
        Prepare();
        if (room >= 0 && room < contents.Length) contents[room] = content;
    }

    protected override RoomContent Make(int room, string habitat, int decor, int rank, float distance, System.Random random)
    {
        if (room < Data.levels.Length)
        {
            var passage = RoomContent.Draw(Kind, habitat, decor, rank, distance, random, RoomContent.Passages);
            passage.levelContent = new LevelContent(LevelFor(room), Data.zones, room, rank, new System.Random(seed + room * 7 + 1));
            return passage;
        }
        // The boss's chamber: Heatran or Groudon, the way out shut until it falls.
        var content = RoomContent.Draw(Kind, habitat, decor, rank, distance, random, RoomContent.Bosses);
        content.bossRoomType = Data.boss.type;
        content.theme = Data.boss.theme;
        content.variant = string.IsNullOrEmpty(Data.boss.variant) ? null : Data.boss.variant;
        int id = BossId;
        content.spawnBoss = (arena, spawner, at) =>
        {
            if (id <= 0 || Gears == null) return new List<Combatant>();
            var all = Gears.SpawnSummitBoss(id, rank, spawner, at, Data.boss.announce);
            if (all.Count > 0) all[0].gameObject.AddComponent<RecruitBonus>().chance = Data.boss.recruitChance;
            return all;
        };
        return content;
    }
}
