using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Special items found during a run (GDD, "Objets spéciaux"; rates in items.json, "loot"), added by EnemySpawner:
/// - a boss drops a mega stone not owned yet (one for a line of the team first), else maybe a Z crystal or Tera shard;
/// - a chest appears once a stage is cleared: one Z crystal or Tera shard;
/// - elites (a few enemies, tougher, a golden ring under them: MakeElite) may drop one.
/// Items of the team's types are likelier. Each drop lies on the ground and is picked up by walking onto it (the
/// controlled Pokémon), or picked up anyway when the stage ends. A banner says what it was; the save keeps it at once
/// (mega stones are unique, a duplicate Z crystal or Tera shard goes up a level). Nothing in the test arena.
public class Loot : MonoBehaviour
{
    const float PickReach = 0.8f, ChestReach = 0.9f;

    /// What a drop gives.
    public class Item
    {
        public SpecialItems.Family family;
        public string stone; // mega stone key
        public int type = -1; // Z crystal or Tera shard
    }

    class Drop
    {
        public Item item;
        public GameObject go;
        public float size;
    }

    const float IconSize = 0.8f, SquareSize = 0.45f; // tiles: an item's icon, or the coloured square without one

    readonly List<Drop> drops = new();
    GameObject chest;
    PlayerController team;
    Progression progression;
    readonly System.Random rng = new();

    /// Called when the stage's chest is opened.
    public event System.Action ChestOpened;
    public bool ChestWaiting => chest != null;

    void Start()
    {
        team = FindAnyObjectByType<PlayerController>();
        progression = team != null ? team.GetComponent<Progression>() : null;
    }

    void OnEnable() => Combatant.AnyDied += OnDied;
    void OnDisable() => Combatant.AnyDied -= OnDied;

    /// The test bench lets elites drop their loot in the test arena (nothing drops there otherwise).
    public static bool DropsInSandbox;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => DropsInSandbox = false;

    void OnDied(Combatant c)
    {
        var mark = c.GetComponent<EliteMark>();
        if ((Run.Sandbox && !DropsInSandbox) || c.team != Team.Enemy || mark == null) return;
        if (mark.squad != null && mark.squad.members.Exists(m => m != null && m != c && m.IsAlive)) return; // others still up
        if (mark.sureLoot || rng.NextDouble() < SpecialItems.Loot.eliteDrop) Place(RollTyped(rng, TeamTypes()), c.transform.position);
    }

    // ---- Rolls (pure: tests call them) ----

    /// A mega stone not owned yet, one for a line of the team first; null when every stone is owned.
    public static Item RollStone(System.Random rng, IEnumerable<int> teamIds, System.Func<string, bool> owned)
    {
        var free = SpecialItems.Stones.Where(s => !owned(s.key) && GameData.Mega(s.mega) != null).ToList();
        if (free.Count == 0) return null;
        var lines = new HashSet<int>(teamIds.Select(id => GameData.Pokemon(id)?.baseForm ?? id));
        var mine = free.Where(s => lines.Contains(GameData.Pokemon(GameData.Mega(s.mega).@base)?.baseForm ?? -1)).ToList();
        var pool = mine.Count > 0 ? mine : free;
        return new Item { family = SpecialItems.Family.Mega, stone = pool[rng.Next(pool.Count)].key };
    }

    /// A Z crystal or a Tera shard, of a type of the team teamTypeWeight times likelier.
    public static Item RollTyped(System.Random rng, ICollection<int> teamTypes)
    {
        var types = SpecialItems.Crystals.Select(c => c.type).ToList();
        float weight = SpecialItems.Loot.teamTypeWeight, total = 0f;
        foreach (int t in types) total += teamTypes.Contains(t) ? weight : 1f;
        double roll = rng.NextDouble() * total;
        int type = types[^1];
        foreach (int t in types)
        {
            roll -= teamTypes.Contains(t) ? weight : 1f;
            if (roll < 0) { type = t; break; }
        }
        return new Item { family = rng.Next(2) == 0 ? SpecialItems.Family.Z : SpecialItems.Family.Tera, type = type };
    }

    List<int> TeamIds() => team != null ? team.Members.Where(m => m != null && m.Stats != null).Select(m => m.OriginId).ToList() : new List<int>();

    HashSet<int> TeamTypes()
    {
        var types = new HashSet<int>();
        if (team == null) return types;
        foreach (var m in team.Members)
            if (m?.Stats?.types != null) types.UnionWith(m.Stats.types);
        return types;
    }

    // ---- Drops ----

    /// A boss is down: its drop at its feet.
    public void BossDrop(Vector2 at)
    {
        if (Run.Sandbox) return;
        var l = SpecialItems.Loot;
        Item item = rng.NextDouble() < l.bossMega ? RollStone(rng, TeamIds(), SaveData.HasStone) : null;
        if (item == null && rng.NextDouble() < l.bossOther) item = RollTyped(rng, TeamTypes());
        if (item != null) Place(item, at);
    }

    /// The stage is cleared: a chest near the team.
    public void SpawnChest(Vector2 at)
    {
        if (Run.Sandbox || chest != null) return;
        chest = new GameObject("Coffre");
        chest.transform.position = at;
        var box = chest.AddComponent<SpriteRenderer>();
        box.sprite = Shapes.Square;
        box.color = new Color(0.55f, 0.33f, 0.12f);
        box.sortingOrder = 1;
        chest.transform.localScale = new Vector3(0.9f, 0.7f, 1f);
        var lid = new GameObject("Couvercle").AddComponent<SpriteRenderer>();
        lid.transform.SetParent(chest.transform, false);
        lid.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        lid.transform.localScale = new Vector3(1f, 0.3f, 1f);
        lid.sprite = Shapes.Square;
        lid.color = new Color(0.95f, 0.75f, 0.2f);
        lid.sortingOrder = 2;
    }

    void Place(Item item, Vector2 at)
    {
        var go = new GameObject($"Objet : {Describe(item, 0)}");
        go.transform.position = at;
        var r = go.AddComponent<SpriteRenderer>();
        r.sortingOrder = 2;
        var icon = SpecialItems.Icon(SpecialItems.IconName(item.family, item.stone, item.type));
        float size = icon != null ? IconSize : SquareSize;
        if (icon != null) r.sprite = icon;
        else
        {
            go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            r.sprite = Shapes.Square;
            r.color = item.family == SpecialItems.Family.Mega ? new Color(0.85f, 0.45f, 1f) : GameData.TypeColor(item.type);
        }
        go.transform.localScale = Vector3.one * size;
        drops.Add(new Drop { item = item, go = go, size = size });
    }

    void FixedUpdate()
    {
        var walker = team != null ? team.Controlled : null;
        // Drops bob; the controlled Pokémon picks up what it walks onto, and opens the chest.
        for (int i = drops.Count - 1; i >= 0; i--)
        {
            var d = drops[i];
            if (d.go == null) { drops.RemoveAt(i); continue; }
            d.go.transform.localScale = Vector3.one * d.size * (1f + 0.1f * Mathf.Sin(Time.time * 4f));
            if (walker != null && Vector2.Distance(walker.transform.position, d.go.transform.position) <= PickReach) Take(i);
        }
        if (chest != null && walker != null && Vector2.Distance(walker.transform.position, chest.transform.position) <= ChestReach)
            OpenChest();
    }

    void OpenChest()
    {
        var at = (Vector2)chest.transform.position;
        Destroy(chest);
        chest = null;
        Place(RollTyped(rng, TeamTypes()), at + Vector2.up * 0.8f);
        ChestOpened?.Invoke();
    }

    void Take(int index)
    {
        var d = drops[index];
        drops.RemoveAt(index);
        if (d.go != null)
        {
            EffectPlayer.Spawn("evolution_eclat", d.go.transform.position, Vector2.right);
            Destroy(d.go);
        }
        Grant(d.item);
    }

    /// The stage ends: the chest left closed is opened and everything on the ground picked up.
    public void CollectAll()
    {
        if (chest != null) OpenChest();
        while (drops.Count > 0) Take(drops.Count - 1);
    }

    void Grant(Item item)
    {
        int level = 0;
        switch (item.family)
        {
            case SpecialItems.Family.Mega: if (!SaveData.AddStone(item.stone)) return; break;
            case SpecialItems.Family.Z: level = SaveData.AddZ(item.type); break;
            case SpecialItems.Family.Tera: level = SaveData.AddTera(item.type); break;
        }
        progression?.Announce($"{Describe(item, level)} obtenu !");
    }

    static string Describe(Item item, int level) => item.family switch
    {
        SpecialItems.Family.Mega => SpecialItems.Stone(item.stone)?.name ?? "Méga-gemme",
        SpecialItems.Family.Z => SpecialItems.Crystal(item.type)?.name + (level > 0 ? $" (niv. {level})" : ""),
        _ => SpecialItems.Shard(item.type)?.name + (level > 0 ? $" (niv. {level})" : ""),
    };

    /// Turns a freshly spawned enemy into an elite: tougher, a golden ring under it, an ultimate (EnemyUltimate), it
    /// changes obstacles like the allies do (cuts walkways, makes a way: EnemyAI), a chance of a drop.
    public static void MakeElite(Combatant c)
    {
        Mark(c);
        c.gameObject.AddComponent<EnemyUltimate>();
        c.maxHp *= SpecialItems.Loot.eliteHealth;
        c.hp = c.maxHp;
    }

    /// The elite mark and its golden ring alone (an elite lot sets its own health and item).
    public static EliteMark Mark(Combatant c)
    {
        var mark = c.gameObject.AddComponent<EliteMark>();
        var ring = new GameObject("Aura d'élite").AddComponent<SpriteRenderer>();
        ring.transform.SetParent(c.transform, false);
        ring.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        ring.transform.localScale = new Vector3(1.1f, 0.5f, 1f);
        ring.sprite = Shapes.Ring;
        ring.color = new Color(1f, 0.82f, 0.25f, 0.9f);
        ring.sortingOrder = 0;
        return mark;
    }
}
