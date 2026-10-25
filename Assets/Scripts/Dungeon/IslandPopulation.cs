using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// The enemies living in the island's quarters (Resources/Data/island_population.json, GDD « L'île flottante »,
/// peuplement): camps (mostly gangs) and patrols (mostly packs) placed when the island is made, resting until the
/// team comes (EnemyGroup.Roam); spawn points that bring a lot when one of their conditions holds (night, weather,
/// time spent in the zone, or simply the team near) out of sight. Lots come from the sub-biome's population
/// (Populations), stronger with the quarter and the distance from its arrival. A beaten camp or patrol is remembered
/// for the whole run; another lot may take its place after a while. Only the zone being played has its enemies.
/// Added by IslandRun.
public class IslandPopulation : MonoBehaviour
{
    [Serializable] class LotWeight { public string key; public float weight = 1f; }

    [Serializable]
    class Kind
    {
        public int[] count = { 0, 0 };
        public float spacing = 14f, wake = 6f, leash = 14f, giveUp = 4f;
        public LotWeight[] lots = new LotWeight[0];
        // Patrols.
        public int points = 3;
        public float radius = 12f;
        // Spawn points.
        public float[] near = { 10f, 22f };
        public float cooldown = 90f, timeInZone = 120f;
        public string[] conditions = new string[0];
    }

    [Serializable]
    class File
    {
        public int baseLevel = 2;
        public float arrivalClear = 18f, keepClear = 6f, respawn = 240f, respawnDistance = 20f;
        public Kind camps = new(), patrols = new(), spawns = new();
    }

    public enum Sort { Camp, Patrol, Spawn }

    /// A place of a quarter where enemies live.
    public class Place
    {
        public Sort sort;
        public int quarter, rank; // the quarter, and its place in the run's order (0 to 3)
        public Vector2Int tile;
        public readonly List<Vector2Int> route = new(); // a patrol's round (its tile first)
        public string habitat; // sub-biome key
        public float distance; // 0 at the quarter's arrival, 1 at its farthest tile
        public string condition; // a spawn point's
        public Encounters.Encounter lot; // the lot living there (null: beaten, or a spawn point waiting)
        public float freeAt; // a beaten camp or patrol: when another lot may come; a spawn point: its next try
        public readonly List<Combatant> spawned = new();
        public bool Beaten;
    }

    static File data;
    static File Data
    {
        get
        {
            if (data != null) return data;
            var asset = Resources.Load<TextAsset>("Data/island_population");
            data = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return data;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => data = null;

    /// Every place of the island (tests read them).
    public readonly List<Place> places = new();

    IslandRun island;
    IslandPlan madeFor;
    EnemySpawner spawner;
    PlayerController team;
    readonly System.Random rng = new();
    float zoneSince;
    bool despawned;

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

    /// Forgets the island (it is left).
    public void Clear()
    {
        Despawn();
        places.Clear();
        madeFor = null;
    }

    void OnBuilt()
    {
        spawner = FindAnyObjectByType<EnemySpawner>();
        team = FindAnyObjectByType<PlayerController>();
        if (island.Plan != madeFor) Make(island.Plan);
        Despawn();
        despawned = false;
        zoneSince = Time.time;
        foreach (var p in places)
            if (p.quarter == island.Zone && p.lot != null) Spawn(p, resting: true);
    }

    // ---- Placing ----

    void Make(IslandPlan plan)
    {
        places.Clear();
        madeFor = plan;
        if (plan == null) return;
        for (int q = 0; q < 4; q++)
        {
            int rank = Array.IndexOf(plan.order, q);
            var land = Land(plan, q, out var arrival);
            if (land.Count == 0) continue;
            float far = land.Max(t => Vector2.Distance(t, arrival));
            var taken = new List<Vector2Int>();
            // Camps: the forests' clearings first, then the heights, then anywhere.
            var clearings = new HashSet<Vector2Int>();
            foreach (var c in plan.clearings)
                foreach (var t in land)
                    if (Vector2.Distance(t, c) <= 3f) clearings.Add(t);
            var byPreference = Shuffled(land).OrderBy(t => clearings.Contains(t) ? 0 : Height(plan, t) > 0 ? 1 : 2).ToList();
            Pick(plan, Sort.Camp, Data.camps, byPreference, taken, q, rank, arrival, far);
            Pick(plan, Sort.Patrol, Data.patrols, Shuffled(land), taken, q, rank, arrival, far);
            Pick(plan, Sort.Spawn, Data.spawns, Shuffled(land), taken, q, rank, arrival, far);
        }
    }

    // The quarter's open land: walkable, no obstacle, out of caves, away from its arrival and its landmarks.
    static List<Vector2Int> Land(IslandPlan plan, int q, out Vector2Int arrival)
    {
        arrival = plan.Arrival(q);
        var keep = new List<Vector2Int>(plan.markers.Concat(plan.caves));
        foreach (var p in plan.passages) { if (p.from == q) keep.Add(p.a); if (p.to == q) keep.Add(p.b); }
        if (plan.secret.x >= 0) keep.Add(plan.secret);
        if (plan.sealedCave.x >= 0) keep.Add(plan.sealedCave);
        var land = new List<Vector2Int>();
        var bounds = plan.ZoneBounds(q);
        for (int y = bounds.yMin; y < bounds.yMax; y++)
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                int i = plan.Index(x, y);
                if (plan.region[i] != q || plan.wall[i] || plan.obstacle[i] != null || plan.biome[i] == null) continue;
                if ((plan.wallLook != null && plan.wallLook[i]) || (plan.roof != null && plan.roof[i])) continue;
                var t = new Vector2Int(x, y);
                if (Vector2.Distance(t, arrival) < Data.arrivalClear || keep.Any(k => Vector2.Distance(t, k) < Data.keepClear)) continue;
                land.Add(t);
            }
        return land;
    }

    void Pick(IslandPlan plan, Sort sort, Kind kind, List<Vector2Int> candidates, List<Vector2Int> taken, int q, int rank,
        Vector2Int arrival, float far)
    {
        int wanted = rng.Next(kind.count[0], kind.count[Mathf.Min(1, kind.count.Length - 1)] + 1);
        var landSet = sort == Sort.Patrol ? new HashSet<Vector2Int>(candidates) : null;
        foreach (var t in candidates)
        {
            if (wanted <= 0) break;
            if (taken.Any(o => Vector2.Distance(o, t) < kind.spacing)) continue;
            var place = new Place
            {
                sort = sort, quarter = q, rank = rank, tile = t, habitat = plan.biome[plan.Index(t.x, t.y)].key,
                distance = far > 0f ? Vector2.Distance(t, arrival) / far : 0f,
            };
            if (sort == Sort.Patrol && !Round(plan, place, kind, landSet)) continue;
            if (sort == Sort.Spawn) place.condition = kind.conditions.Length > 0 ? kind.conditions[rng.Next(kind.conditions.Length)] : "near";
            else place.lot = Draw(place);
            if (sort != Sort.Spawn && place.lot == null) continue;
            places.Add(place);
            taken.Add(t);
            wanted--;
        }
    }

    // A patrol's round: points within its radius, on its own level, spread out.
    bool Round(IslandPlan plan, Place place, Kind kind, HashSet<Vector2Int> land)
    {
        place.route.Add(place.tile);
        int level = Height(plan, place.tile);
        for (int tries = 0; tries < 60 && place.route.Count < Mathf.Max(2, kind.points); tries++)
        {
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f, reach = kind.radius * (0.5f + 0.5f * (float)rng.NextDouble());
            var t = place.tile + Vector2Int.RoundToInt(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * reach);
            if (!land.Contains(t) || Height(plan, t) != level || place.route.Any(r => Vector2.Distance(r, t) < kind.radius * 0.5f)) continue;
            place.route.Add(t);
        }
        return place.route.Count >= 2;
    }

    static int Height(IslandPlan plan, Vector2Int t) => plan.height != null ? plan.height[plan.Index(t.x, t.y)] : 0;

    List<Vector2Int> Shuffled(List<Vector2Int> list)
    {
        var copy = new List<Vector2Int>(list);
        for (int i = copy.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (copy[i], copy[j]) = (copy[j], copy[i]);
        }
        return copy;
    }

    Kind KindOf(Sort sort) => sort == Sort.Camp ? Data.camps : sort == Sort.Patrol ? Data.patrols : Data.spawns;

    /// A lot for that place: a category by the place's weights (not above its stage), from its sub-biome.
    public Encounters.Encounter Draw(Place place)
    {
        int stage = Populations.Stage(place.rank, place.distance);
        var open = KindOf(place.sort).lots.Where(l => l.weight > 0f && Encounters.CategoryOf(l.key) is { } c && c.minStage <= stage).ToList();
        if (open.Count == 0) return null;
        double roll = rng.NextDouble() * open.Sum(l => l.weight);
        var pick = open[^1];
        foreach (var l in open)
            if ((roll -= l.weight) < 0) { pick = l; break; }
        var where = new Populations.Where(place.habitat, spawner != null && spawner.Night);
        var e = Encounters.Make(rng, Encounters.CategoryOf(pick.key), stage, -1, where: where);
        if (e != null && place.sort == Sort.Camp) Encounters.Accompany(rng, e, stage, -1, where);
        return e;
    }

    // ---- Living ----

    void Spawn(Place place, bool resting)
    {
        if (spawner == null || place.lot == null) return;
        Vector2 at = island.Cell(place.tile);
        place.spawned.Clear();
        place.spawned.AddRange(spawner.SpawnEncounter(place.lot, at, Data.baseLevel + Populations.LevelBonus(place.rank, place.distance)));
        var group = place.spawned.Count > 0 ? place.spawned[0].GetComponent<EnemyAI>()?.group : null;
        if (group == null) return;
        var kind = KindOf(place.sort);
        var roam = new EnemyGroup.Roam { home = at, wake = kind.wake, leash = kind.leash, giveUp = kind.giveUp };
        foreach (var t in place.route) roam.route.Add(island.Cell(t));
        group.Rest(roam);
        if (!resting) group.Wake(); // a spawn point's lot comes for the team
    }

    // The zone's enemies go (the team leaves it); what lives there is kept.
    void Despawn()
    {
        foreach (var p in places)
        {
            foreach (var c in p.spawned)
                if (c != null) Destroy(c.gameObject);
            p.spawned.Clear();
            if (p.sort == Sort.Spawn) p.lot = null; // a spawn point's lot does not stay
        }
    }

    void FixedUpdate()
    {
        if (island == null || island.Plan == null || madeFor != island.Plan) return;
        if (island.Away || LoadingScreen.Busy)
        {
            if (!despawned) Despawn(); // into a secret room: back when the zone is built again
            despawned = true;
            return;
        }
        Vector2? at = team != null && team.Controlled != null ? team.Controlled.transform.position : null;
        foreach (var p in places)
        {
            if (p.quarter != island.Zone) continue;
            if (p.spawned.Count > 0 && p.spawned.All(c => c == null || !c.IsAlive))
            {
                // Beaten: remembered; another lot later (a spawn point simply waits for its next try).
                p.spawned.Clear();
                p.lot = null;
                p.Beaten = p.sort != Sort.Spawn;
                p.freeAt = Time.time + (p.sort == Sort.Spawn ? Data.spawns.cooldown : Data.respawn);
            }
            if (p.lot != null || Time.time < p.freeAt || at == null) continue;
            Vector2 where = island.Cell(p.tile);
            float d = Vector2.Distance(at.Value, where);
            if (p.sort == Sort.Spawn)
            {
                if (d < Data.spawns.near[0] || d > Data.spawns.near[1] || OnScreen(where) || !Met(p.condition)) continue;
                p.lot = Draw(p);
                p.freeAt = Time.time + Data.spawns.cooldown;
                Spawn(p, resting: false);
            }
            else if (p.Beaten && d >= Data.respawnDistance && !OnScreen(where))
            {
                p.lot = Draw(p);
                Spawn(p, resting: true);
            }
        }
    }

    bool Met(string condition) => condition switch
    {
        "night" => spawner != null && spawner.Night,
        "weather" => Climate.Now != null,
        "time" => Time.time - zoneSince >= Data.spawns.timeInZone,
        _ => true,
    };

    static bool OnScreen(Vector2 at)
    {
        var cam = Camera.main;
        if (cam == null) return false;
        var v = cam.WorldToViewportPoint(at);
        return v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }
}
