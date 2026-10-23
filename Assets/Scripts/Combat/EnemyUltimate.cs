using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// The ultimate of an elite or a boss (GDD, "Objets spéciaux"; settings in items.json, "enemy"). Like any Pokémon it
/// carries one special item, so one ultimate, used once. The item is picked when it first looks at the fight, from
/// the player's team:
/// - a Tera shard when a type would shrug off their moves (the type that lowers the most their average type
///   multiplier against it; its own attacks' types break ties): it Terastallizes once its health is low, for good;
/// - else the Z crystal of its strongest damaging move: the Z move goes when several opponents are near, or when one
///   would lose most of what it has left. A pose with a banner warns first; the strike reaches opponents in view
///   within zRange and takes at most zMaxShare of each one's health; its terrain never acts on its own team.
[RequireComponent(typeof(Combatant))]
public class EnemyUltimate : MonoBehaviour
{
    /// The family of the item it carries (None until it has looked at the fight), and its type.
    public SpecialItems.Family Item { get; private set; }
    public int ItemType { get; private set; } = -1;

    /// What it unleashed ("tera", "z" or ""), for the tests.
    public string LastUsed { get; private set; } = "";

    Combatant self;
    float nextCheck;
    bool used;
    Progression progression;

    void Awake()
    {
        self = GetComponent<Combatant>();
        nextCheck = Time.time + SpecialItems.Enemy.startDelay;
    }

    void Start()
    {
        var team = FindAnyObjectByType<PlayerController>();
        progression = team != null ? team.GetComponent<Progression>() : null;
    }

    void FixedUpdate()
    {
        if (used || Time.time < nextCheck || self == null || !self.IsAlive || self.IsEvolving || !self.CanAttack) return;
        nextCheck = Time.time + SpecialItems.Enemy.checkEvery;
        Decide();
    }

    /// Gives it that item instead of the one it would pick (tests).
    public void Carry(SpecialItems.Family family, int type)
    {
        Item = family;
        ItemType = type;
    }

    /// Picks the item it carries, from the opponents it faces now.
    public void ChooseItem()
    {
        int tera = BestTeraType(out float gain);
        if (tera > 0 && gain >= SpecialItems.Enemy.teraMinGain)
        {
            Carry(SpecialItems.Family.Tera, tera);
            return;
        }
        var strongest = self.Moves.Where(m => m != null && SpecialItems.KnowsMoveOfType(new[] { m }, m.type))
            .OrderByDescending(m => m.power).FirstOrDefault();
        if (strongest != null) Carry(SpecialItems.Family.Z, strongest.type);
    }

    /// Looks at the situation and unleashes its ultimate if it is worth it now (also called by the tests).
    public bool Decide()
    {
        if (used) return false;
        if (Item == SpecialItems.Family.None) ChooseItem();
        var s = SpecialItems.Enemy;
        if (Item == SpecialItems.Family.Tera && self.TeraType < 0 && self.hp <= self.maxHp * s.teraHealth)
        {
            Tera(ItemType);
            return true;
        }
        if (Item == SpecialItems.Family.Z && ZWorth(out var baseMove))
        {
            StartCoroutine(ZMove(baseMove));
            return true;
        }
        return false;
    }

    // ---- Tera ----

    /// The type that most lowers the average multiplier of the opponents' damaging moves against it; "gain" is how
    /// much it lowers it.
    public int BestTeraType(out float gain)
    {
        gain = 0f;
        var incoming = IncomingTypes();
        if (incoming.Count == 0) return -1;
        float now = Average(incoming, self.Stats.types);
        int best = -1;
        float bestScore = float.MinValue;
        foreach (var shard in SpecialItems.Shards)
        {
            float after = Average(incoming, new[] { shard.type });
            int own = self.Moves.Count(m => m != null && m.power > 0 && m.type == shard.type);
            float score = now - after + 0.05f * own;
            if (score > bestScore) { bestScore = score; best = shard.type; gain = now - after; }
        }
        return best;
    }

    // Types of the damaging moves of the living opponents (each move counts once per Pokémon).
    List<int> IncomingTypes()
    {
        var types = new List<int>();
        foreach (var c in Combatant.All)
            if (c != null && c.IsAlive && c.team != self.team && c.Moves != null)
                foreach (var m in c.Moves)
                    if (m != null && m.power > 0) types.Add(m.type);
        return types;
    }

    static float Average(List<int> incoming, int[] defender)
    {
        float sum = 0f;
        foreach (int t in incoming) sum += TypeChart.Multiplier(t, defender, out _);
        return sum / incoming.Count;
    }

    void Tera(int type)
    {
        used = true;
        LastUsed = "tera";
        self.Terastallize(type);
        self.BeginEvolution(Ultimates.TeraPose);
        Ultimates.Aura(self, Ultimates.TeraPose, GameData.TypeColor(type));
        if (EffectLibrary.Get("tera_eclat") != null) EffectPlayer.Spawn("tera_eclat", transform.position, Vector2.right);
        else EffectPlayer.Spawn("evolution_eclat", transform.position, Vector2.right);
        progression?.Announce($"{self.Stats.name} ennemi se téracristallise : type {GameData.TypeName(type)} !");
    }

    // ---- Z move ----

    bool ZWorth(out MoveDef baseMove)
    {
        var s = SpecialItems.Enemy;
        baseMove = self.Moves.Where(m => m != null && SpecialItems.KnowsMoveOfType(new[] { m }, ItemType))
            .OrderByDescending(m => m.power).FirstOrDefault();
        if (baseMove == null) return false;
        var foes = Ultimates.VisibleFoes(self, s.zRange);
        if (foes.Count >= s.zMinTargets) return true;
        var z = Ultimates.ZVersion(baseMove, ItemType, s.level);
        foreach (var foe in foes)
            if (Mathf.Min(self.EstimateHit(z, foe), foe.maxHp * s.zMaxShare) >= foe.hp * s.zKillShare) return true;
        return false;
    }

    System.Collections.IEnumerator ZMove(MoveDef baseMove)
    {
        var s = SpecialItems.Enemy;
        used = true;
        LastUsed = "z";
        var z = Ultimates.ZVersion(baseMove, ItemType, s.level);
        self.BeginEvolution(SpecialItems.ZPose);
        Ultimates.Aura(self, SpecialItems.ZPose, GameData.TypeColor(ItemType));
        progression?.Announce($"Attention : {self.Stats.name} ennemi prépare {z.name} !");
        yield return new WaitForSeconds(SpecialItems.ZPose);
        if (self == null || !self.IsAlive) yield break;
        yield return Ultimates.StrikeZ(self, z, ItemType, s.level, s.zRange, s.zMaxShare);
    }
}
