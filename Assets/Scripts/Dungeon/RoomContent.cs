using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// What waits in the room behind stairs of the island (Resources/Data/room_contents.json; GDD « Escaliers secrets »,
/// « Grottes scellées »): a treasure (a chest), an ambush (the way out shut, waves of the sub-biome's Pokémon, then a
/// chest) or a guardian (an elite asleep on a chest, easier to recruit). The room takes the sub-biome's look
/// (RoomFor). One per stairs, drawn on the first visit, played once; IslandStairs drives it.
public class RoomContent
{
    [Serializable] class Weighted { public string key; public float weight = 1f; }
    [Serializable] class Contents { public Weighted[] secret = new Weighted[0], cave = new Weighted[0]; }
    [Serializable] class ThemeOf { public string subBiome, theme; }

    [Serializable]
    class Treasure { public string roomType = "rest", chestIcon; public float stoneChance = 0.15f; }

    [Serializable]
    class Ambush
    {
        public string roomType = "monster_house", chestIcon;
        public int[] waves = { 2, 3 }, lotsPerWave = { 1, 2 };
        public float pause = 2f, minDistance = 5f, stoneChance = 0.1f;
        public int stageBonus = 1;
        public Weighted[] lots = new Weighted[0];
    }

    [Serializable]
    class Guardian
    {
        public string roomType = "subboss", lot = "champion", chestIcon;
        public int stageBonus = 1;
        public float wake = 5f, leash = 10f, recruitBonus = 3f, stoneChance = 0.25f;
    }

    [Serializable]
    class File
    {
        public int baseLevel = 2;
        public Contents contents = new();
        public Treasure treasure = new();
        public Ambush ambush = new();
        public Guardian guardian = new();
        public ThemeOf[] themes = new ThemeOf[0];
    }

    static File data;
    static File Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/room_contents");
            data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return data;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => data = null;

    public const string Treasures = "treasure", Ambushes = "ambush", Guardians = "guardian";

    /// treasure, ambush or guardian.
    public readonly string key;
    readonly string habitat;
    readonly int decor; // the sub-biome's ROM décor, 0 for none
    readonly int rank;
    readonly float distance;
    readonly System.Random rng;

    /// Played to the end (its chest given or the room cleared).
    public bool Done { get; private set; }
    /// The way out is shut (an ambush still going).
    public bool Locked => key == Ambushes && started && !Done;

    bool started;
    int wavesLeft;
    float nextWaveAt;
    readonly List<Combatant> enemies = new();
    Loot loot;
    DungeonGenerator arena;
    EnemySpawner spawner;

    RoomContent(string key, string habitat, int decor, int rank, float distance, System.Random rng)
    {
        this.key = key;
        this.habitat = habitat;
        this.decor = decor;
        this.rank = rank;
        this.distance = distance;
        this.rng = rng;
    }

    /// The content of stairs of that kind ("secret" or "cave") standing in that sub-biome (and its ROM décor), drawn by
    /// weight unless forced. rank: the quarter's place in the run; distance: 0 at its arrival, 1 at its farthest tile.
    public static RoomContent Draw(string stairs, string habitat, int decor, int rank, float distance, System.Random rng, string forced = null)
    {
        var weights = stairs == "cave" ? Data.contents.cave : Data.contents.secret;
        string key = forced;
        if (key == null && weights.Length > 0)
        {
            double roll = rng.NextDouble() * weights.Sum(w => w.weight);
            key = weights[^1].key;
            foreach (var w in weights)
                if ((roll -= w.weight) < 0) { key = w.key; break; }
        }
        return new RoomContent(key ?? Treasures, habitat, decor, rank, distance, rng);
    }

    /// Its room: the content's room type, in the sub-biome's theme and décor when the catalogue has them.
    public RoomInstance Room()
    {
        string type = key == Ambushes ? Data.ambush.roomType : key == Guardians ? Data.guardian.roomType : Data.treasure.roomType;
        string theme = Array.Find(Data.themes, t => t.subBiome == habitat)?.theme;
        var room = (theme != null ? RoomCatalog.Pick(type, rng, theme) : null) ?? RoomCatalog.Pick(type, rng) ?? RoomCatalog.Pick(null, rng);
        // The sub-biome's own décor when its theme draws it.
        if (room != null && decor > 0 && room.theme != null && Array.IndexOf(room.theme.tilesets, decor) >= 0 && TilesetRoster.Get(decor) != null)
            room.tileset = decor;
        return room;
    }

    int Stage(int bonus) => Populations.Stage(rank, distance) + bonus;
    int Level => Data.baseLevel + Populations.LevelBonus(rank, distance);

    /// The room is built and the team in it: lays out the content (once).
    public void Begin(DungeonGenerator arena, EnemySpawner spawner, Vector2 arrival)
    {
        if (Done || started) return;
        started = true;
        loot = UnityEngine.Object.FindAnyObjectByType<Loot>();
        Vector2 far = arena.RandomOpenPosition(rng, Mathf.Min(8f, arena.Interior.width / 3f), arrival);
        switch (key)
        {
            case Ambushes:
                wavesLeft = rng.Next(Data.ambush.waves[0], Data.ambush.waves[Mathf.Min(1, Data.ambush.waves.Length - 1)] + 1);
                nextWaveAt = Time.time + Data.ambush.pause;
                break;
            case Guardians:
                far = arena.RandomOpenPosition(rng, Data.guardian.wake + 3f, arrival); // asleep when the team comes in
                loot?.SpawnChest(far, anywhere: true, Data.guardian.stoneChance, Data.guardian.chestIcon);
                var e = Encounters.CategoryOf(Data.guardian.lot) is { } lot
                    ? Encounters.Make(rng, lot, Stage(Data.guardian.stageBonus), -1, where: new Populations.Where(habitat, spawner != null && spawner.Night))
                    : null;
                if (e == null || spawner == null) { Done = loot == null || loot.ChestAt == null; break; }
                enemies.AddRange(spawner.SpawnEncounter(e, far, Level));
                foreach (var c in enemies) c.gameObject.AddComponent<RecruitBonus>().multiplier = Data.guardian.recruitBonus;
                if (enemies.Count > 0 && enemies[0].GetComponent<EnemyAI>()?.group is { } group)
                    group.Rest(new EnemyGroup.Roam { home = far, wake = Data.guardian.wake, leash = Data.guardian.leash });
                break;
            default:
                loot?.SpawnChest(far, anywhere: true, Data.treasure.stoneChance, Data.treasure.chestIcon);
                break;
        }
        this.arena = arena;
        this.spawner = spawner;
    }

    /// Each physics step while the team is in the room.
    public void Tick(Vector2 teamAt)
    {
        if (!started || Done) return;
        enemies.RemoveAll(c => c == null || !c.IsAlive);
        if (key == Ambushes)
        {
            if (enemies.Count > 0) return;
            if (wavesLeft <= 0)
            {
                // Cleared: the chest, and the way out opens.
                loot?.SpawnChest(teamAt + Vector2.up * 1.5f, anywhere: true, Data.ambush.stoneChance, Data.ambush.chestIcon);
                Done = true;
                return;
            }
            if (Time.time < nextWaveAt) return;
            SpawnWave(teamAt);
            wavesLeft--;
            nextWaveAt = Time.time + Data.ambush.pause;
            return;
        }
        // Treasure and guardian: done once the chest is opened.
        if (loot == null || loot.ChestAt == null) Done = true;
    }

    void SpawnWave(Vector2 teamAt)
    {
        if (spawner == null) { wavesLeft = 0; return; }
        var a = Data.ambush;
        int lots = rng.Next(a.lotsPerWave[0], a.lotsPerWave[Mathf.Min(1, a.lotsPerWave.Length - 1)] + 1);
        int stage = Stage(a.stageBonus);
        var open = a.lots.Where(l => l.weight > 0f && Encounters.CategoryOf(l.key) is { } c && c.minStage <= stage).ToList();
        if (open.Count == 0) { wavesLeft = 0; return; }
        var where = new Populations.Where(habitat, spawner.Night);
        for (int k = 0; k < lots; k++)
        {
            double roll = rng.NextDouble() * open.Sum(l => l.weight);
            var pick = open[^1];
            foreach (var l in open)
                if ((roll -= l.weight) < 0) { pick = l; break; }
            var e = Encounters.Make(rng, Encounters.CategoryOf(pick.key), stage, -1, where: where);
            if (e == null) continue;
            Vector2 at = arena.RandomOpenPosition(rng, a.minDistance, teamAt);
            enemies.AddRange(spawner.SpawnEncounter(e, at, Level)); // they come for the team at once
        }
    }

    /// The team leaves the room: its enemies and an unopened chest go (a guardian or a chest left behind waits for
    /// the next visit of stairs that stay).
    public void Leave()
    {
        foreach (var c in enemies)
            if (c != null) UnityEngine.Object.Destroy(c.gameObject);
        enemies.Clear();
        if (!Done) loot?.RemoveChest();
        started = false;
    }
}
