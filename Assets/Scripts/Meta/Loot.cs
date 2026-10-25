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
        public string consumable; // a consumable of the bag (BagItems key) instead of a special item
        public bool inBag; // the consumable already went into the bag when picked up
        public string held; // a held item (HeldItems key): the controlled Pokémon takes it
        public int heldTier; // its tier (1 to 3), 0 for an untiered one
    }

    class Drop
    {
        public Item item;
        public GameObject go;
        public float size;
        public Combatant notFor; // the Pokémon that just let it go: not picked up again until it steps off
    }

    const float ChestSize = 1.1f; // tiles
    const float IconSize = 0.8f, SquareSize = 0.45f; // tiles: an item's icon, or the coloured square without one

    readonly List<Drop> drops = new();
    GameObject chest;
    float chestStone; // the waiting chest's chance of a mega stone
    PlayerController team;
    Progression progression;
    readonly System.Random rng = new();

    /// Called when the stage's chest is opened.
    public event System.Action ChestOpened;
    public bool ChestWaiting => chest != null;

    /// Where the waiting chest stands (tests, the island's rooms), null without one.
    public Vector2? ChestAt => chest != null ? chest.transform.position : null;

    /// Takes the waiting chest away unopened (the team leaves the room it stands in).
    public void RemoveChest()
    {
        if (chest != null) Destroy(chest);
        chest = null;
    }

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
        DropConsumable(c);
        if (c.team == Team.Enemy && c.Held != null)
            Place(new Item { held = c.Held.key, heldTier = c.Held.tier }, (Vector2)c.transform.position + Random.insideUnitCircle * 0.3f);
        var mark = c.GetComponent<EliteMark>();
        if ((Run.Sandbox && !DropsInSandbox) || c.team != Team.Enemy || mark == null) return;
        if (mark.squad != null && mark.squad.members.Exists(m => m != null && m != c && m.IsAlive)) return; // others still up
        if (mark.sureLoot || rng.NextDouble() < SpecialItems.Loot.eliteDrop) Place(RollTyped(rng, TeamTypes()), c.transform.position);
    }

    // A knocked-out enemy may leave a consumable of the bag, the more likely the higher its rank (always for an
    // elite); in the test arena too, to try the bag.
    void DropConsumable(Combatant c)
    {
        if (c.team != Team.Enemy || c.isBoss) return;
        string rank = c.GetComponent<EliteMark>() != null ? "elite" : c.GetComponent<RarityMark>()?.Rarity?.key ?? "common";
        if (rng.NextDouble() >= BagItems.DropChance(rank)) return;
        var item = BagItems.Roll(rng);
        if (item != null) Place(new Item { consumable = item.key }, (Vector2)c.transform.position + Random.insideUnitCircle * 0.3f);
    }

    /// Lays a special item on the ground (the test arena's showcase): a mega stone (key), or a Z crystal / Tera shard
    /// of that type. Picked up like any drop.
    public void PlaceSpecial(SpecialItems.Family family, string stone, int type, Vector2 at) =>
        Place(new Item { family = family, stone = stone, type = type }, at);

    /// Lays a held item on the ground (the test arena's showcase).
    public void PlaceHeld(string key, Vector2 at, int tier = 0) =>
        Place(new Item { held = key, heldTier = HeldItems.Get(key) is { tiered: true } ? Mathf.Max(1, tier) : 0 }, at);

    /// Removes what lies on the ground (the test arena, when a room is drawn again).
    public void ClearGround()
    {
        foreach (var d in drops)
            if (d.go != null) Destroy(d.go);
        drops.Clear();
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
    /// anywhere: also in the test arena (the island's rooms); stoneChance: a mega stone instead of the Z crystal or
    /// Tera shard, that often.
    public void SpawnChest(Vector2 at, bool anywhere = false, float stoneChance = 0f, string icon = null)
    {
        if ((Run.Sandbox && !anywhere) || chest != null) return;
        chestStone = stoneChance;
        chest = new GameObject("Coffre");
        chest.transform.position = at;
        // Mystery Dungeon's chest (PMDCollab), else a drawn box with its lid.
        var sprite = SpecialItems.Icon(string.IsNullOrEmpty(icon) ? SpecialItems.Loot.chestIcon : icon);
        if (sprite != null)
        {
            var r = chest.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = 1;
            r.spriteSortPoint = SpriteSortPoint.Pivot;
            chest.transform.localScale = Vector3.one * ChestSize;
            return;
        }
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
        var go = new GameObject($"{(item.consumable != null ? "Consommable" : item.held != null ? "Objet tenu" : "Objet")} : {Describe(item, 0)}");
        go.transform.position = at;
        var r = go.AddComponent<SpriteRenderer>();
        r.sortingOrder = 2;
        var bagItem = item.consumable != null ? BagItems.Get(item.consumable) : null;
        var heldItem = item.held != null ? HeldItems.Get(item.held) : null;
        var icon = bagItem != null ? SpecialItems.Icon(bagItem.icon) : heldItem != null ? SpecialItems.Icon(heldItem.icon)
            : SpecialItems.Icon(SpecialItems.IconName(item.family, item.stone, item.type));
        float size = icon != null ? IconSize : SquareSize;
        if (icon != null) r.sprite = icon;
        else if (bagItem != null)
        {
            r.sprite = Shapes.Circle; // a coloured ball until the ROM's item icons are in
            r.color = bagItem.Colour;
        }
        else if (heldItem != null)
        {
            r.sprite = Shapes.Square; // a coloured square, upright, for a held item
            r.color = heldItem.Colour;
        }
        else
        {
            go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            r.sprite = Shapes.Square;
            r.color = item.family == SpecialItems.Family.Mega ? new Color(0.85f, 0.45f, 1f) : GameData.TypeColor(item.type);
        }
        if (heldItem != null && HeldItems.TierOf(item.heldTier) is { } tier)
        {
            // Its tier: a thin ring of the tier's colour under it (bronze, silver, gold), like the enemies' ranks.
            var ring = new GameObject("Tier").AddComponent<SpriteRenderer>();
            ring.transform.SetParent(go.transform, false);
            ring.transform.localPosition = new Vector3(0f, -0.35f, 0f);
            ring.transform.localScale = new Vector3(1.3f, 0.55f, 1f);
            ring.sprite = Shapes.Ring;
            ring.color = tier.Colour;
            ring.sortingOrder = 0; // ground marks: under the item, never the Pokémon's order
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
            bool near = walker != null && Vector2.Distance(walker.transform.position, d.go.transform.position) <= PickReach;
            if (d.notFor != null && (d.notFor != walker || !near)) d.notFor = null; // it stepped off
            if (near && d.notFor == null && Fits(d.item)) Take(i);
        }
        if (chest != null && walker != null && Vector2.Distance(walker.transform.position, chest.transform.position) <= ChestReach)
            OpenChest();
    }

    void OpenChest()
    {
        var at = (Vector2)chest.transform.position;
        Destroy(chest);
        chest = null;
        var special = rng.NextDouble() < chestStone ? RollStone(rng, TeamIds(), SaveData.HasStone) : null;
        Place(special ?? RollTyped(rng, TeamTypes()), at + Vector2.up * 0.8f);
        if (rng.NextDouble() < HeldItems.ChestChance && HeldItems.Roll(rng) is { } held)
            Place(new Item { held = held.key, heldTier = held.tiered ? HeldItems.ChestTier(rng) : 0 }, at + new Vector2(-0.8f, 0.3f));
        for (int k = 0; k < BagItems.ChestCount; k++)
            if (BagItems.Roll(rng) is { } item) Place(new Item { consumable = item.key }, at + new Vector2(0.8f * (k + 1), 0.3f));
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

    // A consumable is picked up only when the bag has room for it (it stays on the ground otherwise).
    bool Fits(Item item)
    {
        if (item.consumable == null) return true;
        var bag = team != null ? team.GetComponent<Bag>() : null;
        if (bag == null) return false;
        if (bag.Add(item.consumable))
        {
            item.inBag = true; // Grant only says so
            return true;
        }
        if (Time.time >= fullSaidAt) { progression?.Announce("Sac plein", Notice.Alert); fullSaidAt = Time.time + 3f; }
        return false;
    }

    float fullSaidAt;

    // The HUD's card: what was picked up and what it does.
    void Card(string title, string body, Sprite icon)
    {
        var hud = team != null ? team.GetComponent<TeamHud>() : null;
        if (hud != null) hud.ShowCard(title, body, icon);
    }

    void Grant(Item item)
    {
        if (item.held != null)
        {
            // The controlled Pokémon takes it; what it held drops at its feet (to give it to another member).
            var taker = team != null ? team.Controlled : null;
            var found = HeldItems.AtTier(HeldItems.Get(item.held), item.heldTier);
            if (taker == null || found == null) return;
            // The same item gains a tier (or keeps the higher one); another one replaces it, the old one drops.
            var now = HeldItems.Merge(taker.Held, found, out var dropped);
            taker.Hold(now);
            if (dropped != null)
            {
                Place(new Item { held = dropped.key, heldTier = dropped.tier }, (Vector2)taker.transform.position + Vector2.down * 0.6f);
                drops[^1].notFor = taker;
            }
            progression?.Announce($"{taker.Stats.name} tient {now.FullName}{(dropped != null ? $" (lâche {dropped.FullName})" : "")}", Notice.Loot);
            Card($"{taker.Stats.name} tient {now.FullName}", HeldItems.Describe(now), SpecialItems.Icon(now.icon));
            return;
        }
        if (item.consumable != null)
        {
            var bagItem = BagItems.Get(item.consumable);
            if (bagItem == null) return;
            var bag = team != null ? team.GetComponent<Bag>() : null;
            if (!item.inBag && (bag == null || !bag.Add(bagItem.key))) return; // picked up at the end of a stage: no room, lost
            progression?.Announce($"{bagItem.name} ramassée", Notice.Loot);
            Card($"{bagItem.name} (sac)", BagItems.Describe(bagItem), SpecialItems.Icon(bagItem.icon));
            return;
        }
        if (Run.Sandbox) // the test arena's showcase: shown, not written into the save
        {
            progression?.Announce($"{Describe(item, 0)} (terrain de test : non enregistré)", Notice.Loot);
            return;
        }
        int level = 0;
        switch (item.family)
        {
            case SpecialItems.Family.Mega: if (!SaveData.AddStone(item.stone)) return; break;
            case SpecialItems.Family.Z: level = SaveData.AddZ(item.type); break;
            case SpecialItems.Family.Tera: level = SaveData.AddTera(item.type); break;
        }
        progression?.Announce($"{Describe(item, level)} obtenu !", Notice.Loot);
    }

    static string Describe(Item item, int level) => item.consumable != null ? BagItems.Get(item.consumable)?.name ?? "Objet"
        : item.held != null ? HeldItems.AtTier(HeldItems.Get(item.held), item.heldTier)?.FullName ?? "Objet tenu" : item.family switch
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
