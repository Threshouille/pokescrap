using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// The ultimate of an elite or a boss (GDD, "Objets spéciaux"; settings in items.json, "enemy"). Like any Pokémon it
/// carries one special item, so one ultimate, used once. The item is picked when it first looks at the fight, from
/// the player's team:
/// - a mega stone when its line has a mega with sprites (megaChance): it mega evolves once an opponent is near;
/// - a Tera shard when a type would shrug off their moves (the type that lowers the most their average type
///   multiplier against it; its own attacks' types break ties) or would carry it across the obstacle between it and
///   its target, or around it (water, lava, void, rocks: Eau or Vol on a lake, Spectre among rocks...): it
///   Terastallizes once its health is low, at once when that obstacle stops it, or, picked for the ground, as soon as
///   the fight starts; for good;
/// - else the Z crystal of its strongest damaging move: the Z move goes when several opponents are near, or when one
///   would lose most of what it has left. A pose with a banner warns first; the strike reaches opponents in view
///   within zRange and takes at most zMaxShare of each one's health; its terrain never acts on its own team.
[RequireComponent(typeof(Combatant))]
public class EnemyUltimate : MonoBehaviour
{
    /// The family of the item it carries (None until it has looked at the fight), its type, its mega.
    public SpecialItems.Family Item { get; private set; }
    public int ItemType { get; private set; } = -1;
    public MegaDef ItemMega { get; private set; }

    /// The Tera shard was picked mostly to get about the obstacles around it: used as soon as the fight starts.
    public bool TeraForTerrain { get; private set; }

    /// Carries the mega stone of its line whenever it can (the encounter lab shows mega evolutions).
    public bool preferMega;

    /// Its second phase gets a short cut-scene like a boss's (a key dungeon's guardian, TimeGears).
    [System.NonSerialized] public bool cutScene;

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
        if (used || self == null || !self.IsAlive || self.IsEvolving) return;
        if (Item == SpecialItems.Family.None) ChooseItem(); // early, so that the HP floor of its second phase holds
        // Down to its second phase's threshold: right away, even stunned (the floor keeps it there until then).
        bool phase = PhaseReady && (Item == SpecialItems.Family.Mega || Item == SpecialItems.Family.Tera);
        if (!phase && (Time.time < nextCheck || !self.CanAttack)) return;
        nextCheck = Time.time + SpecialItems.Enemy.checkEvery;
        Decide();
    }

    /// Down to the share of its HP where a mega or a Tera opens its second phase.
    public bool PhaseReady => self.hp <= self.maxHp * SpecialItems.Enemy.phaseHealth + 0.5f;

    bool Major => self.IsElite || GetComponent<BossAI>() != null;

    /// Gives it that item instead of the one it would pick (tests).
    public void Carry(SpecialItems.Family family, int type, MegaDef mega = null)
    {
        Item = family;
        ItemType = type;
        ItemMega = mega;
        // Elites and bosses can't be taken past their second phase in one blow.
        bool phased = family == SpecialItems.Family.Mega || family == SpecialItems.Family.Tera;
        if (self != null) self.phaseFloorShare = phased && Major && !used ? SpecialItems.Enemy.phaseHealth : 0f;
    }

    /// The mega evolutions of its current form that have sprites.
    public List<MegaDef> Megas() =>
        self.Stats == null || self.Mega != null ? new List<MegaDef>() : GameData.MegasOf(self.Stats.id).Where(m => PokemonRoster.Has(m.id)).ToList();

    /// Picks the item it carries, from the opponents it faces now.
    public void ChooseItem()
    {
        var megas = Megas();
        if (megas.Count > 0 && (preferMega || Random.value < SpecialItems.Enemy.megaChance))
        {
            Carry(SpecialItems.Family.Mega, -1, megas[Random.Range(0, megas.Count)]);
            return;
        }
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
        if (Item == SpecialItems.Family.Mega && ItemMega != null && self.Mega == null
            && PhaseReady)
        {
            MegaUp(ItemMega);
            return true;
        }
        if (Item == SpecialItems.Family.Tera && self.TeraType < 0
            && (PhaseReady || Crosses(ItemType) || (TeraForTerrain && Nearest(s.zRange) != null)))
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
            float cross = Crosses(shard.type) ? SpecialItems.Enemy.teraCrossBonus : 0f; // the way to its target opens
            float ground = SpecialItems.Enemy.teraTerrainBonus * TerrainShare(shard.type); // it gets about the obstacles
            float score = now - after + cross + ground + 0.05f * own;
            if (score > bestScore)
            {
                bestScore = score;
                best = shard.type;
                gain = now - after + cross + ground;
                TeraForTerrain = cross + ground > now - after;
            }
        }
        return best;
    }

    Combatant Nearest(float range) =>
        Combatant.Nearest(self.team == Team.Player ? Team.Enemy : Team.Player, self.transform.position, range);

    /// Whether an obstacle on the straight way to its nearest opponent stops it now but would not with that type.
    public bool Crosses(int type)
    {
        var grid = DungeonGenerator.Current;
        var target = Nearest(float.MaxValue);
        if (grid == null || target == null || Obstacles.Current == null || self.Stats == null) return false;
        var tera = self.Stats.Clone();
        tera.types = new[] { type };
        Vector2 from = self.transform.position, to = target.transform.position;
        bool stopped = false;
        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(from, to) / 0.5f));
        for (int i = 1; i < steps; i++)
        {
            var cell = grid.CellOf(Vector2.Lerp(from, to, (float)i / steps));
            if (!Obstacles.BlocksStats(cell.x, cell.y, self.Stats)) continue;
            if (Obstacles.BlocksStats(cell.x, cell.y, tera)) return false; // still stopped there with that type
            stopped = true;
        }
        return stopped;
    }

    /// Share, among the obstacle tiles that stop it within terrainRadius (at least terrainMinTiles of them), of
    /// those that type would let it cross: 0 to 1.
    public float TerrainShare(int type)
    {
        var grid = DungeonGenerator.Current;
        var s = SpecialItems.Enemy;
        if (grid == null || Obstacles.Current == null || self.Stats == null) return 0f;
        var tera = self.Stats.Clone();
        tera.types = new[] { type };
        var at = grid.CellOf(self.transform.position);
        int r = Mathf.CeilToInt(s.terrainRadius), stopped = 0, crossed = 0;
        for (int x = at.x - r; x <= at.x + r; x++)
            for (int y = at.y - r; y <= at.y + r; y++)
            {
                if ((x - at.x) * (x - at.x) + (y - at.y) * (y - at.y) > s.terrainRadius * s.terrainRadius) continue;
                if (!Obstacles.BlocksStats(x, y, self.Stats)) continue;
                stopped++;
                if (!Obstacles.BlocksStats(x, y, tera)) crossed++;
            }
        return stopped >= s.terrainMinTiles ? (float)crossed / stopped : 0f;
    }

    // ---- Mega ----

    void MegaUp(MegaDef mega)
    {
        used = true;
        LastUsed = "mega";
        string name = self.Stats.name;
        var animator = GetComponent<PmdAnimator>();
        var before = animator != null ? animator.animSet : null; // its old form, for a boss's cut-scene
        self.MegaEvolve(mega);
        var set = PokemonRoster.Get(mega.id);
        if (animator != null && set != null) animator.SetAnimSet(set);
        gameObject.name = mega.name;
        float pose = SaveData.Settings.megaCastTime;
        self.BeginEvolution(pose);
        Ultimates.Aura(self, pose, Color.white);
        EffectPlayer.Spawn("evolution_eclat", transform.position, Vector2.right);
        progression?.Announce($"{name} ennemi méga-évolue en {mega.name} !", Notice.Elite);
        SecondPhase(before, mega.name.ToUpperInvariant(), Color.white);
    }

    /// Its second phase: heals (elites and bosses more), recharges faster, pushes the opponents away from the change.
    void SecondPhase(PmdAnimSet before, string title, Color colour)
    {
        var s = SpecialItems.Enemy;
        // A boss: the fight stops for its change on screen (BossCutScene), then goes on.
        if (GetComponent<BossAI>() != null) BossCutScene.Play(self, before, title, colour);
        else if (cutScene) BossCutScene.Play(self, before, title, colour, s.guardianCutScene);
        self.phaseFloorShare = 0f;
        float share = GetComponent<BossAI>() != null ? s.phaseHealBoss : self.IsElite ? s.phaseHeal : s.phaseHealRanked;
        self.Heal(self.maxHp * share);
        self.cooldownMul *= s.phaseCooldownMul;
        float radius = SaveData.Settings.shockwaveRadius;
        Vector2 centre = transform.position;
        var wave = EffectPlayer.Spawn("evolution_onde", centre, Vector2.right, size: 2f * radius);
        if (wave == null) Shapes.Pulse(centre, radius, Color.white);
        foreach (var c in Combatant.All.ToArray())
        {
            if (c == null || !c.IsAlive || c.team == self.team) continue;
            Vector2 away = (Vector2)c.transform.position - centre;
            if (away.magnitude > radius) continue;
            if (away == Vector2.zero) away = Vector2.right;
            c.Knockback(away.normalized * s.phaseKnockback / Combatant.KnockbackTime, Combatant.KnockbackTime);
        }
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
        progression?.Announce($"{self.Stats.name} ennemi se téracristallise : type {GameData.TypeName(type)} !", Notice.Elite);
        SecondPhase(null, $"{self.Stats.name.ToUpperInvariant()} SE TÉRACRISTALLISE · {GameData.TypeName(type).ToUpperInvariant()}", GameData.TypeColor(type));
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
        progression?.Announce($"Attention : {self.Stats.name} ennemi prépare {z.name} !", Notice.Alert);
        yield return new WaitForSeconds(SpecialItems.ZPose);
        if (self == null || !self.IsAlive) yield break;
        yield return Ultimates.StrikeZ(self, z, ItemType, s.level, s.zRange, s.zMaxShare);
    }
}
