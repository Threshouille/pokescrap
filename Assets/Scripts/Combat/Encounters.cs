using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// What a wave of a stage is made of (Assets/Resources/Data/encounters.json; GDD, "Rencontres"), instead of the
/// hand-written waves: encounters of a few categories drawn until the wave's threat budget is spent.
/// - Solitaire: one Pokémon, more evolved, higher level and much tougher (commun to épique).
/// - Équipe: three Pokémon that complement each other: melee, ranged, support, the second one's type chosen to
///   combine with the first one's (synergies: a puddle the Électrik one charges...) (commun to épique).
/// - Meute: four to six of one Pokémon, low level (commun to rare).
/// - Gang: three solitaires, each carrying an item and its ultimate (rare or épique, later stages).
/// A lot may come with another one in the same group (combos: a pack led by a rare or épique solitaire...).
/// Each member gets a rank (rarities: commun, peu commun, rare, épique): more levels, health and moves, the moves
/// picked among the strongest, an item and its ultimate for the épique ones; a coloured ring under it shows it.
/// Pokémon are taken among those that fit the stage's décor (types), at the evolution stage the stage allows.
public static class Encounters
{
    [Serializable]
    public class Category
    {
        public string key, name;
        public float weight = 1f, cost = 1f, health = 1f, sameSpeciesChance;
        public int[] size = { 1, 1 };
        public int levelBonus, moves, depthBonus, minStage;
        public string[] roles = new string[0];
        public string[] rarities = new string[0]; // ranks allowed (all when empty)
        public bool synergy, items;
    }

    [Serializable]
    public class Rarity
    {
        public string key, name, color;
        public float[] weight = { 1f };
        public int level, moves;
        public float health = 1f, cost = 1f;
        public bool strong, item, repairs;

        public Color Colour => ColorUtility.TryParseHtmlString(color, out var c) ? c : Color.clear;
        public bool Shown => !string.IsNullOrEmpty(color);
    }

    [Serializable] class Budget { public float start = 5f, perStage = 2.5f, perWave = 1.5f; public int maxGroups = 3; }
    [Serializable] class Decor { public int tileset; public int[] types = new int[0]; }
    [Serializable] class Synergy { public int a, b; }

    /// A lot that may come with another one: "lot" drawn, "with" joins it (chance), at these ranks (its own when empty).
    [Serializable]
    public class Combo
    {
        public string lot, with;
        public float chance;
        public string[] rarities = new string[0];
    }

    [Serializable]
    class File
    {
        public Budget budget = new();
        public Category[] categories = new Category[0];
        public Rarity[] rarities = new Rarity[0];
        public int[] maxDepthByStage = { 0 };
        public int minPool = 6;
        public Decor[] decors = new Decor[0];
        public Synergy[] synergies = new Synergy[0];
        public int[] exclude = new int[0];
        public Combo[] combos = new Combo[0];
    }

    /// One Pokémon of an encounter, as it will be spawned.
    public class Member
    {
        public int pokemonId;
        public string role; // imposed role, or null for its natural one
        public Rarity rarity;
        public int levelBonus, moveCount;
        public float health = 1f;
        public bool item; // carries an item (its rank's, or its lot's: a gang)
        public Category lot; // the lot it belongs to (a combo's companion keeps its own)
    }

    public class Encounter
    {
        public Category category;
        public Category with; // the lot that came along (combos), or null
        public readonly List<Member> members = new();
        public float cost;

        /// "Équipe : Racaillou (rare, corps à corps), ..." for the banners.
        public string Describe() =>
            $"{category.name}{(with != null ? " + " + with.name : "")} : " + string.Join(", ", members.Select(m =>
                $"{GameData.Pokemon(m.pokemonId)?.name} ({m.rarity.name.ToLowerInvariant()}{(m.role != null ? ", " + RoleName(m.role) : "")})"));
    }

    static string RoleName(string role) => role switch { "melee" => "corps à corps", "ranged" => "distance", "support" => "soutien", _ => role };

    static File file;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => file = null;

    static File Data
    {
        get
        {
            if (file != null) return file;
            var asset = Resources.Load<TextAsset>("Data/encounters");
            file = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
            return file;
        }
    }

    public static IReadOnlyList<Category> Categories => Data.categories;
    public static IReadOnlyList<Rarity> Rarities => Data.rarities;

    /// The threat budget of a wave (stage and wave counted from 0).
    public static float BudgetOf(int stage, int wave) => Data.budget.start + Data.budget.perStage * stage + Data.budget.perWave * wave;

    /// The encounters of a wave: drawn until its budget is spent (the first one always), at most maxGroups.
    public static List<Encounter> Wave(System.Random rng, int stage, int wave, int decor)
    {
        var result = new List<Encounter>();
        float left = BudgetOf(stage, wave);
        for (int tries = 0; result.Count < Data.budget.maxGroups && tries < 12; tries++)
        {
            var category = PickCategory(rng, stage);
            if (category == null) break;
            var e = Make(rng, category, stage, decor);
            if (e == null) continue;
            Accompany(rng, e, stage, decor);
            if (result.Count > 0 && e.cost > left) continue; // too dear for what is left: another one, maybe cheaper
            result.Add(e);
            left -= e.cost;
            if (left <= 0f) break;
        }
        return result;
    }

    static Category PickCategory(System.Random rng, int stage)
    {
        var open = Data.categories.Where(c => c.minStage <= stage && c.weight > 0f).ToList();
        if (open.Count == 0) return null;
        double roll = rng.NextDouble() * open.Sum(c => c.weight);
        foreach (var c in open)
            if ((roll -= c.weight) < 0) return c;
        return open[^1];
    }

    public static Category CategoryOf(string key) => Array.Find(Data.categories, c => c.key == key);

    public static IReadOnlyList<Combo> Combos => Data.combos;

    /// The lot that may come with this one (combos), drawn: its members join the encounter's group.
    public static void Accompany(System.Random rng, Encounter e, int stage, int decor)
    {
        foreach (var combo in Data.combos)
        {
            if (combo.lot != e.category.key || rng.NextDouble() >= combo.chance) continue;
            Join(rng, e, combo, stage, decor);
            return; // one companion lot at most
        }
    }

    /// Adds that combo's companion lot to the encounter (the encounter lab forces one).
    public static bool Join(System.Random rng, Encounter e, Combo combo, int stage, int decor)
    {
        var with = CategoryOf(combo.with);
        var extra = with != null ? Make(rng, with, stage, decor, combo.rarities) : null;
        if (extra == null) return false;
        e.with = with;
        e.members.AddRange(extra.members);
        e.cost += extra.cost;
        return true;
    }

    /// One encounter of that category, or null when no Pokémon fits.
    /// rarities: ranks allowed instead of the category's own (a combo's companion).
    public static Encounter Make(System.Random rng, Category category, int stage, int decor, string[] rarities = null)
    {
        var pool = Pool(stage + category.depthBonus, decor);
        if (pool.Count == 0) return null;
        var e = new Encounter { category = category };
        int count = rng.Next(category.size[0], category.size[Mathf.Min(1, category.size.Length - 1)] + 1);
        var ids = category.roles.Length > 0 ? Team(rng, category, pool, count)
            : category.sameSpeciesChance > 0f ? Pack(rng, category, pool, count)
            : Enumerable.Range(0, count).Select(_ => pool[rng.Next(pool.Count)]).ToList();
        for (int i = 0; i < ids.Count; i++)
        {
            var rarity = PickRarity(rng, stage, rarities != null && rarities.Length > 0 ? rarities : category.rarities);
            string role = i < category.roles.Length && GameData.CanTakeRole(ids[i], category.roles[i]) ? category.roles[i] : null;
            var m = new Member
            {
                pokemonId = ids[i],
                role = role,
                rarity = rarity,
                levelBonus = category.levelBonus + rarity.level,
                moveCount = Mathf.Clamp(SaveData.Settings.enemyMoveCount + category.moves + rarity.moves, 1, Combatant.MoveSlots),
                health = category.health * rarity.health,
                item = rarity.item || category.items,
                lot = category,
            };
            e.members.Add(m);
            e.cost += category.cost * rarity.cost;
        }
        return e;
    }

    /// A rank drawn with the stage's weights, among "allowed" (every rank when empty). When none of them has weight
    /// at that stage, the lowest allowed one.
    public static Rarity PickRarity(System.Random rng, int stage, string[] allowed = null)
    {
        float Weight(Rarity r) => r.weight.Length == 0 ? 0f : r.weight[Mathf.Clamp(stage, 0, r.weight.Length - 1)];
        var open = Data.rarities.Where(r => allowed == null || allowed.Length == 0 || Array.IndexOf(allowed, r.key) >= 0).ToList();
        if (open.Count == 0) open = Data.rarities.ToList();
        float total = open.Sum(Weight);
        double roll = rng.NextDouble() * total;
        if (total > 0f)
            foreach (var r in open)
                if ((roll -= Weight(r)) < 0) return r;
        return open.Count > 0 ? open[0] : new Rarity { key = "common", name = "Commun" };
    }

    // A pack: one species, or one type, several times.
    static List<int> Pack(System.Random rng, Category category, List<int> pool, int count)
    {
        int first = pool[rng.Next(pool.Count)];
        if (rng.NextDouble() < category.sameSpeciesChance) return Enumerable.Repeat(first, count).ToList();
        var types = GameData.Pokemon(first).types;
        var kin = pool.Where(id => GameData.Pokemon(id).types.Intersect(types).Any()).ToList();
        var result = new List<int> { first };
        while (result.Count < count) result.Add(kin[rng.Next(kin.Count)]);
        return result;
    }

    // A team: one Pokémon per role (one that can take it when there is any); the second one's type combines with the
    // first one's when the pool allows it.
    static List<int> Team(System.Random rng, Category category, List<int> pool, int count)
    {
        var result = new List<int>();
        for (int i = 0; i < count; i++)
        {
            string role = i < category.roles.Length ? category.roles[i] : null;
            var fit = role != null ? pool.Where(id => GameData.CanTakeRole(id, role)).ToList() : pool;
            if (fit.Count == 0) fit = pool;
            if (category.synergy && i == 1 && result.Count > 0)
            {
                var partners = fit.Where(id => Combines(result[0], id)).ToList();
                if (partners.Count > 0) fit = partners;
            }
            result.Add(fit[rng.Next(fit.Count)]);
        }
        return result;
    }

    /// Whether two Pokémon have types that combine (synergies: in either order).
    public static bool Combines(int a, int b)
    {
        var ta = GameData.Pokemon(a)?.types ?? new int[0];
        var tb = GameData.Pokemon(b)?.types ?? new int[0];
        foreach (var s in Data.synergies)
            if ((ta.Contains(s.a) && tb.Contains(s.b)) || (ta.Contains(s.b) && tb.Contains(s.a))) return true;
        return false;
    }

    static readonly Dictionary<(int depth, int decor), List<int>> pools = new();

    /// The Pokémon that may appear: sprites, at least two moves, recruitable, not excluded, evolution stage allowed
    /// at that stage, of a type of the décor (all types when the décor has too few).
    public static List<int> Pool(int stage, int decor)
    {
        int depth = Data.maxDepthByStage.Length > 0 ? Data.maxDepthByStage[Mathf.Clamp(stage, 0, Data.maxDepthByStage.Length - 1)] : 9;
        if (pools.TryGetValue((depth, decor), out var cached)) return cached;
        var all = GameData.AllPokemon.Where(p => p.recruitRate > 0f && !GameData.IsDev(p.id) && Array.IndexOf(Data.exclude, p.id) < 0
                                               && Depth(p) <= depth && PokemonRoster.Has(p.id) && GameData.MovePool(p.id).Count >= 2)
            .Select(p => p.id).ToList();
        var d = Array.Find(Data.decors, x => x.tileset == decor);
        var fitting = d == null ? all : all.Where(id => GameData.Pokemon(id).types.Intersect(d.types).Any()).ToList();
        var result = fitting.Count >= Data.minPool ? fitting : all;
        pools[(depth, decor)] = result;
        return result;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetPools() => pools.Clear();

    /// Evolution stage: 0 for a first form, 1 for its evolution...
    public static int Depth(PokemonStats p)
    {
        int depth = 0;
        for (var form = p; form != null && form.evolvesFrom > 0 && depth < 5; form = GameData.Pokemon(form.evolvesFrom)) depth++;
        return depth;
    }
}
