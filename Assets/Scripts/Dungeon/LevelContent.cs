using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// What lives in the zones of a dungeon's level (SummitDungeon; GDD « Donjon du volcan »), carried by the level's
/// RoomContent: kept with it, so a zone beaten stays beaten when the team comes back.
/// - combat zones: one or two groups at rest, Pokémon of the zone's sub-theme (SubTheme.Habitat, populations.json),
///   spawned when the team comes near (spawnRange), waking as it comes closer;
/// - arenas (monster house, challenge, sub-boss: Settings.arenas): stone gates rise across every way in as soon as
///   the controlled Pokémon walks in (LevelPlan.Zone entries; teammates left outside join it), waves come one after
///   the other, and once the last falls the gates sink back and a chest stands in the middle;
/// - chests (Settings.chests): in the rest zones and at the end of the branches of a sub-theme of their own (the
///   hidden oasis, the crystal cave).
/// Plain class: SummitDungeon makes it, RoomContent runs it (Begin, Tick each physics step, Leave).
public class LevelContent
{
    [Serializable] public class Weighted { public string key; public float weight = 1f; }

    [Serializable]
    public class Arena
    {
        public string type, announce = "Les herses se referment !", cleared = "La salle est vidée : les herses s'abaissent.";
        public int[] waves = { 2, 3 }, lotsPerWave = { 1, 2 };
        public Weighted[] lots = new Weighted[0];
        public string[] rarities = new string[0]; // ranks allowed (encounters.json), all when empty
        public int stageBonus;
        public float stoneChance, pause = 1.5f;
        public string chestIcon;
    }

    [Serializable]
    public class Chest
    {
        public string type, subTheme; // a zone type (rest...), or a branch end's sub-theme
        public float stoneChance;
        public string chestIcon;
    }

    [Serializable]
    public class Settings
    {
        public int baseLevel = 4, levelPerFloor = 3, stageBonus = 1, stagePerFloor = 1;
        public float spawnRange = 24f, wake = 7f, leash = 16f;
        public int[] groups = { 1, 2 };
        public Weighted[] lots = new Weighted[0];
        public Arena[] arenas = new Arena[0];
        public Chest[] chests = new Chest[0];
        public float gateInside = 0.7f; // how far in (share of its radius) the controlled Pokémon shuts an arena
    }

    enum State { Waiting, Resting, Fighting, Beaten }

    class ZoneLife
    {
        public LevelPlan.Zone zone;
        public State state;
        public readonly List<Combatant> foes = new();
        public int wavesLeft;
        public float nextWaveAt;
        public bool chestPlaced, chestGiven; // a chest stood there; it was opened
        public GameObject chest;
        public readonly List<Vector2Int> gate = new();
        public readonly List<EntranceSprite> gateLooks = new();
    }

    readonly LevelPlan plan;
    readonly Settings settings;
    readonly int floor, rank;
    readonly System.Random rng;
    readonly List<ZoneLife> zones = new();

    DungeonGenerator arena;
    EnemySpawner spawner;
    Loot loot;
    PlayerController team;
    GameObject gates;
    bool started;

    public LevelContent(LevelPlan plan, Settings settings, int floor, int rank, System.Random rng)
    {
        this.plan = plan;
        this.settings = settings ?? new Settings();
        this.floor = floor;
        this.rank = rank;
        this.rng = rng;
        foreach (var z in plan.zones) zones.Add(new ZoneLife { zone = z });
    }

    /// An arena fight going on (its gates up).
    public bool Fighting => zones.Any(z => z.state == State.Fighting && ArenaOf(z.zone) != null);

    /// Tests: the gate tiles up, the chests waiting, the foes alive.
    public int GatesUp => zones.Sum(z => z.gate.Count);
    public int ChestsWaiting => zones.Count(z => z.chest != null);
    public int ChestsPlaced => zones.Count(z => z.chestPlaced);
    public int FoesAlive => zones.Sum(z => z.foes.Count(c => c != null && c.IsAlive));

    int Stage(int bonus) => Populations.Stage(rank, 1f) + settings.stageBonus + settings.stagePerFloor * floor + bonus;
    int Level => settings.baseLevel + Populations.LevelBonus(rank, 1f) + settings.levelPerFloor * floor;

    Arena ArenaOf(LevelPlan.Zone z) => Array.Find(settings.arenas, a => a.type == z.type);

    Chest ChestOf(LevelPlan.Zone z) =>
        Array.Find(settings.chests, c => !string.IsNullOrEmpty(c.subTheme) && c.subTheme == z.subTheme && !z.main && z.links.Count == 1)
        ?? Array.Find(settings.chests, c => !string.IsNullOrEmpty(c.type) && c.type == z.type);

    Populations.Where Habitat(LevelPlan.Zone z) =>
        new(plan.SubOf(z.subTheme)?.Habitat, spawner != null && spawner.Night);

    Vector2 Centre(LevelPlan.Zone z) => arena.LevelPoint(z.centre);

    /// The level is built and the team at its start.
    public void Begin(DungeonGenerator arena, EnemySpawner spawner)
    {
        this.arena = arena;
        this.spawner = spawner;
        loot = UnityEngine.Object.FindAnyObjectByType<Loot>();
        team = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        started = true;
        gates = new GameObject("Herses");
        foreach (var z in zones)
        {
            if (z.state != State.Beaten) z.state = State.Waiting;
            z.foes.Clear();
            // A chest not opened yet waits where it was.
            if (!z.chestGiven && ChestOf(z.zone) is { } c) GiveChest(z, c.stoneChance, c.chestIcon);
            else if (z.state == State.Beaten && !z.chestGiven && ArenaOf(z.zone) is { } a) GiveChest(z, a.stoneChance, a.chestIcon);
        }
    }

    public void Tick(Vector2 teamAt)
    {
        if (!started || arena == null) return;
        foreach (var z in zones)
        {
            if (z.chestPlaced && !z.chestGiven && z.chest == null) z.chestGiven = true; // opened
            if (z.zone.type == "start" || z.state == State.Beaten) continue;
            var centre = Centre(z.zone);
            float radius = Mathf.Max(z.zone.radius.x, z.zone.radius.y);
            float d = Vector2.Distance(teamAt, centre);
            var arenaRule = ArenaOf(z.zone);
            if (arenaRule != null) TickArena(z, arenaRule, teamAt, centre);
            else if (z.state == State.Waiting && d < radius + settings.spawnRange && IsCombat(z.zone)) Settle(z, centre);
            else if (z.state == State.Resting)
            {
                z.foes.RemoveAll(c => c == null || !c.IsAlive);
                if (z.foes.Count == 0) z.state = State.Beaten;
            }
        }
    }

    static bool IsCombat(LevelPlan.Zone z) => z.type == "combat" || z.type == "boss";

    // Groups at rest in a combat zone.
    void Settle(ZoneLife z, Vector2 centre)
    {
        z.state = State.Resting;
        if (spawner == null) return;
        int groups = rng.Next(settings.groups[0], settings.groups[Mathf.Min(1, settings.groups.Length - 1)] + 1);
        for (int g = 0; g < groups; g++)
        {
            var e = MakeEncounter(settings.lots, Stage(0), null, z.zone);
            if (e == null) continue;
            Vector2 at = SpotIn(z.zone, centre, 0f, null);
            var spawned = spawner.SpawnEncounter(e, at, Level);
            z.foes.AddRange(spawned);
            if (spawned.Count > 0 && spawned[0].GetComponent<EnemyAI>()?.group is { } group)
                group.Rest(new EnemyGroup.Roam { home = at, wake = settings.wake, leash = settings.leash });
        }
        if (z.foes.Count == 0) z.state = State.Beaten;
    }

    Encounters.Encounter MakeEncounter(Weighted[] lots, int stage, string[] rarities, LevelPlan.Zone zone)
    {
        var open = lots.Where(l => l.weight > 0f && Encounters.CategoryOf(l.key) is { } c && c.minStage <= stage).ToList();
        if (open.Count == 0) open = lots.Where(l => Encounters.CategoryOf(l.key) != null).ToList();
        if (open.Count == 0) return null;
        double roll = rng.NextDouble() * open.Sum(l => Mathf.Max(0.01f, l.weight));
        var pick = open[^1];
        foreach (var l in open)
            if ((roll -= Mathf.Max(0.01f, l.weight)) < 0) { pick = l; break; }
        return Encounters.Make(rng, Encounters.CategoryOf(pick.key), stage, -1, rarities != null && rarities.Length > 0 ? rarities : null, Habitat(zone));
    }

    // A free tile in the zone, at least "away" tiles from a point (the team), else any free one.
    Vector2 SpotIn(LevelPlan.Zone zone, Vector2 centre, float away, Vector2? from)
    {
        Vector2 best = centre;
        for (int k = 0; k < 40; k++)
        {
            var p = centre + new Vector2(((float)rng.NextDouble() * 2f - 1f) * zone.radius.x * 0.7f, ((float)rng.NextDouble() * 2f - 1f) * zone.radius.y * 0.7f);
            var cell = arena.CellOf(p);
            if (!arena.Walkable(cell.x, cell.y) || Obstacles.At(cell.x, cell.y) != null) continue;
            best = arena.CentreOf(cell.x, cell.y);
            if (from == null || Vector2.Distance(best, from.Value) >= away) return best;
        }
        return best;
    }

    // ------------------------------------------------------------------------------------------------ arenas

    void TickArena(ZoneLife z, Arena rule, Vector2 teamAt, Vector2 centre)
    {
        if (z.state == State.Waiting)
        {
            var off = teamAt - centre;
            float inside = Mathf.Sqrt(off.x * off.x / (z.zone.radius.x * z.zone.radius.x) + off.y * off.y / (z.zone.radius.y * z.zone.radius.y));
            if (inside > settings.gateInside) return;
            Shut(z, rule, teamAt, centre);
            return;
        }
        if (z.state != State.Fighting) return;
        z.foes.RemoveAll(c => c == null || !c.IsAlive);
        if (z.foes.Count > 0 || Time.time < z.nextWaveAt) return;
        if (z.wavesLeft <= 0)
        {
            Open(z);
            z.state = State.Beaten;
            Announce(rule.cleared, Notice.Loot);
            GiveChest(z, rule.stoneChance, rule.chestIcon);
            return;
        }
        Wave(z, rule, teamAt, centre);
    }

    void Shut(ZoneLife z, Arena rule, Vector2 teamAt, Vector2 centre)
    {
        z.state = State.Fighting;
        z.wavesLeft = rng.Next(rule.waves[0], rule.waves[Mathf.Min(1, rule.waves.Length - 1)] + 1);
        z.nextWaveAt = Time.time + rule.pause;
        // Teammates left outside come in with the controlled Pokémon.
        if (team != null)
            foreach (var m in team.Members)
            {
                if (m == null || !m.IsAlive || m == team.Controlled) continue;
                var off = (Vector2)m.transform.position - centre;
                float inside = Mathf.Sqrt(off.x * off.x / (z.zone.radius.x * z.zone.radius.x) + off.y * off.y / (z.zone.radius.y * z.zone.radius.y));
                if (inside > 0.85f) m.Warp(teamAt + UnityEngine.Random.insideUnitCircle * 0.8f);
            }
        foreach (var cell in GateCells(z.zone))
        {
            Obstacles.Place(cell.x, cell.y, "zone_gate");
            z.gate.Add(cell);
            var look = EntranceSprite.Make(gates.transform, arena.CentreOf(cell.x, cell.y) + new Vector2(0f, -0.5f), EntranceArt.Foot, EntranceArt.StandOrder);
            look.Play("level_gate_rise", false, "level_gate");
            z.gateLooks.Add(look);
        }
        CameraFollow.Shake(0.25f, 0.15f);
        Announce(rule.announce, Notice.Alert);
    }

    void Wave(ZoneLife z, Arena rule, Vector2 teamAt, Vector2 centre)
    {
        z.wavesLeft--;
        z.nextWaveAt = Time.time + rule.pause;
        if (spawner == null) return;
        int lots = rng.Next(rule.lotsPerWave[0], rule.lotsPerWave[Mathf.Min(1, rule.lotsPerWave.Length - 1)] + 1);
        for (int k = 0; k < lots; k++)
        {
            var e = MakeEncounter(rule.lots.Length > 0 ? rule.lots : settings.lots, Stage(rule.stageBonus), rule.rarities, z.zone);
            if (e == null) continue;
            z.foes.AddRange(spawner.SpawnEncounter(e, SpotIn(z.zone, centre, 4f, teamAt), Level)); // they come at once
        }
    }

    void Open(ZoneLife z)
    {
        foreach (var cell in z.gate) if (Obstacles.At(cell.x, cell.y)?.key == "zone_gate") Obstacles.Place(cell.x, cell.y, null);
        z.gate.Clear();
        foreach (var look in z.gateLooks)
            if (look != null)
            {
                var go = look.gameObject;
                look.Play("level_gate_drop", false, finished: () => { if (go != null) UnityEngine.Object.Destroy(go); });
            }
        z.gateLooks.Clear();
    }

    // The gates of a zone: across each path that leaves it, a band of tiles a little beyond its edge following its
    // outline (the same distance from the edge, about two tiles thick so that nothing slips through diagonally), on
    // the path's open tiles near where the path leaves.
    List<Vector2Int> GateCells(LevelPlan.Zone zone)
    {
        var cells = new List<Vector2Int>();
        var origin = arena.Interior.min;
        float Beyond(Vector2 p) // tiles beyond the zone's ellipse along the ray from its centre (negative inside)
        {
            var off = p - zone.centre;
            if (off.sqrMagnitude < 0.01f) return -1f;
            var d = off.normalized;
            float extent = 1f / Mathf.Sqrt(d.x * d.x / (zone.radius.x * zone.radius.x) + d.y * d.y / (zone.radius.y * zone.radius.y));
            return off.magnitude - extent;
        }
        foreach (var other in zone.links)
        {
            var lane = plan.lanes.Find(l => (l.a == zone && l.b == other) || (l.b == zone && l.a == other));
            var d = (other.centre - zone.centre).normalized;
            float extentTo = 1f / Mathf.Sqrt(d.x * d.x / (zone.radius.x * zone.radius.x) + d.y * d.y / (zone.radius.y * zone.radius.y));
            Vector2 at = zone.centre + d * (extentTo + 3f);
            // Where the path really runs, about three tiles beyond the edge (its wavering outline reaches 15 % out).
            float want = 2f + 0.15f * extentTo;
            if (lane != null)
            {
                float bestErr = float.MaxValue;
                foreach (var p in lane.points)
                {
                    float err = Mathf.Abs(Beyond(p) - want);
                    if (err < bestErr) { bestErr = err; at = p; }
                }
            }
            float band = Beyond(at);
            int x0 = Mathf.FloorToInt(at.x) + origin.x, y0 = Mathf.FloorToInt(at.y) + origin.y;
            for (int x = x0 - 7; x <= x0 + 7; x++)
                for (int y = y0 - 7; y <= y0 + 7; y++)
                {
                    var p = new Vector2(x - origin.x + 0.5f, y - origin.y + 0.5f);
                    if (Vector2.Distance(p, at) > 7f || Mathf.Abs(Beyond(p) - band) > 0.85f) continue;
                    var cell = new Vector2Int(x, y);
                    if (cells.Contains(cell) || !arena.Walkable(x, y)) continue;
                    var o = Obstacles.At(x, y);
                    if (o != null && !o.solid && o.key != "bridge") continue; // liquids hold by themselves
                    if (o != null && o.solid) continue;
                    cells.Add(cell);
                }
        }
        return cells;
    }

    /// Tests: the zone shut in by its gates (no walk from its centre to a linked zone's).
    public bool Sealed(LevelPlan.Zone zone)
    {
        var origin = arena.Interior.min;
        var from = origin + Vector2Int.RoundToInt(zone.centre);
        var goals = new HashSet<Vector2Int>(zone.links.Select(l => origin + Vector2Int.RoundToInt(l.centre)));
        var seen = new HashSet<Vector2Int> { from };
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (goals.Contains(c)) return false;
            foreach (var n in new[] { c + Vector2Int.right, c + Vector2Int.left, c + Vector2Int.up, c + Vector2Int.down })
            {
                if (seen.Contains(n) || !arena.Walkable(n.x, n.y) || Obstacles.At(n.x, n.y) is { } o && (o.solid || o.fall)) continue;
                seen.Add(n);
                queue.Enqueue(n);
            }
        }
        return true;
    }

    // ------------------------------------------------------------------------------------------------ chests

    void GiveChest(ZoneLife z, float stoneChance, string icon)
    {
        if (loot == null || z.chest != null) return;
        var centre = Centre(z.zone);
        var cell = arena.CellOf(centre);
        for (int r = 0; r < 6 && (!arena.Walkable(cell.x, cell.y) || Obstacles.At(cell.x, cell.y) != null); r++)
            cell = arena.CellOf(SpotIn(z.zone, centre, 0f, null));
        z.chest = loot.PlaceChest(arena.CentreOf(cell.x, cell.y), stoneChance, icon);
        z.chestPlaced = true;
    }

    void Announce(string text, Notice kind)
    {
        if (!string.IsNullOrEmpty(text) && team != null && team.GetComponent<Progression>() is { } p) p.Announce(text, kind);
    }

    /// The team leaves the level: its foes and gates go; an opened chest counts as given, the others wait.
    public void Leave()
    {
        foreach (var z in zones)
        {
            if (z.state == State.Fighting) { Open(z); z.state = State.Waiting; }
            else if (z.state == State.Resting) z.state = State.Waiting;
            foreach (var c in z.foes) if (c != null) UnityEngine.Object.Destroy(c.gameObject);
            z.foes.Clear();
            z.gate.Clear();
            z.gateLooks.Clear();
            if (z.chestPlaced && z.chest == null) z.chestGiven = true;
            else if (z.chest != null && loot != null) loot.RemoveChests(new[] { z.chest });
            z.chest = null;
        }
        if (gates != null) UnityEngine.Object.Destroy(gates);
        started = false;
    }
}
