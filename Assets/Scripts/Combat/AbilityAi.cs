using System.Collections.Generic;
using UnityEngine;

/// What the AIs make of abilities (lot 1: not hurting themselves). An AI only knows some abilities: a share drawn
/// from its side and rank (Abilities settings, aiKnowledge of encounters.json rarities), and for each ability a stable
/// draw says whether it is one of them, so it keeps the same blind spots for the whole fight. An ability weighs on a
/// choice, it never forbids every move: what it would rather not do stays possible when nothing else is.
public static class AbilityAi
{
    /// Free: as usual. Reluctant: only when nothing else is ready. Sparing: same, and not more than once every
    /// aiReluctantEvery seconds (shots into a reflection). Avoid: never (a status move bounced back, a hit absorbed).
    public enum Stance { Free, Reluctant, Sparing, Avoid }

    /// The bench sets it to make every AI know all abilities (1) or none (0); below 0: the usual draw.
    public static float ForcedKnowledge = -1f;

    static readonly Dictionary<Combatant, float> nextSparing = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        nextSparing.Clear();
        ForcedKnowledge = -1f;
    }

    /// How a move of type "type" fares against an opponent with this ability (pure; null: an ability it does not know).
    /// slowField: the opponent stands in a foe's shot-slowing aura (Mewtwo Y); moldBreaker: the user ignores it.
    public static Stance Against(AbilityEffect foe, MoveDef move, int type, bool slowField, bool moldBreaker)
    {
        if (move == null) return Stance.Free;
        var a = moldBreaker ? null : foe;
        if (a != null)
        {
            bool statusOnly = move.power <= 0;
            if (Abilities.Absorbs(a, type)) return Stance.Avoid;
            if (a.magicBounce && statusOnly && (!string.IsNullOrEmpty(move.status) || (!string.IsNullOrEmpty(move.modStat) && move.modValue < 1f)))
                return Stance.Avoid;
            if (a.synchronize && (move.status is "burn" or "poison" or "paralysis")) return statusOnly ? Stance.Avoid : Stance.Reluctant;
            if (a.reflectShotChance > 0f && move.family == "projectile") return Stance.Sparing;
            if ((a.contactStatuses.Length > 0 || a.contactShare > 0f || a.faintContactShare > 0f) && move.family == "melee") return Stance.Reluctant;
        }
        if (slowField && move.family == "projectile") return Stance.Sparing;
        return Stance.Free;
    }

    /// Whether an AI with this knowledge takes this ability into account: the same answer for a seed and an ability.
    public static bool Knows(float knowledge, int seed, int abilityId)
    {
        if (knowledge <= 0f || abilityId == 0) return false;
        if (knowledge >= 1f) return true;
        unchecked
        {
            uint h = (uint)(seed * 73856093) ^ (uint)(abilityId * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return h % 10000 / 10000f < knowledge;
        }
    }

    /// The share of abilities this Pokémon's AI knows: its side, its rank.
    public static float KnowledgeOf(Combatant c)
    {
        if (ForcedKnowledge >= 0f) return ForcedKnowledge;
        var s = Abilities.Config;
        if (c.team == Team.Player) return s.aiKnowledgeAllies;
        if (c.isBoss) return s.aiKnowledgeBosses;
        if (c.IsElite) return s.aiKnowledgeElites;
        var mark = c.GetComponent<RarityMark>();
        if (mark != null && mark.Rarity != null && mark.Rarity.aiKnowledge >= 0f) return mark.Rarity.aiKnowledge;
        return s.aiKnowledgeEnemies;
    }

    /// How "self" sees using this move on "foe", with the abilities it knows.
    public static Stance Judge(Combatant self, MoveDef move, Combatant foe)
    {
        if (self == null || foe == null || move == null) return Stance.Free;
        float knowledge = KnowledgeOf(self);
        if (knowledge <= 0f) return Stance.Free;
        int seed = self.GetHashCode(); // stable for this Pokémon
        var theirs = Knows(knowledge, seed, foe.AbilityId) ? foe.Ability : null;
        bool slow = false;
        if (move.family == "projectile")
            foreach (var c in Combatant.All)
                if (c.team != self.team && c.IsAlive && c.Ability is { shotSlowRadius: > 0f } a && Knows(knowledge, seed, c.AbilityId)
                    && Vector2.Distance(c.transform.position, foe.transform.position) <= a.shotSlowRadius)
                {
                    slow = true;
                    break;
                }
        return Against(theirs, move, Abilities.TypeOf(move, self.Ability), slow, self.Ability is { moldBreaker: true });
    }

    /// Whether "self" knows this Pokémon's ability (its own, a team-mate's): what it does with it, within its knowledge.
    public static bool KnowsAbilityOf(Combatant self, Combatant holder) =>
        self != null && holder != null && Knows(KnowledgeOf(self), self.GetHashCode(), holder.AbilityId);

    /// Lot 2 rule: how far it wants to stand from a foe it can trap (pure): within the trap's reach, never closer than
    /// it already would. band, retreat: its usual distances.
    public static (float band, float retreat) TrapperBand(float trapRadius, float band, float retreat) =>
        trapRadius <= 0f ? (band, retreat) : (Mathf.Min(band, trapRadius), Mathf.Min(retreat, trapRadius * 0.7f));

    /// Lot 3 rule: how far it stands from a foe that can trap it (pure): out of the trap's reach, never closer than it
    /// already would. band, retreat: its usual distances.
    public static (float band, float retreat) TrappedBand(float trapRadius, float band, float retreat) =>
        trapRadius <= 0f ? (band, retreat) : (Mathf.Max(band, trapRadius + 2f), Mathf.Max(retreat, trapRadius + 1f));

    /// A heal on "patient" would do nothing: a foe of its with an Unnerve that stops every heal is close by, and "self"
    /// knows it (an ally keeps its berries, a support its heal).
    public static bool HealStopped(Combatant self, Combatant patient)
    {
        if (self == null || patient == null) return false;
        foreach (var c in Combatant.All)
            if (c.team != patient.team && c.IsAlive && c.Ability is { foesHealMul: <= 0.01f } a
                && Vector2.Distance(c.transform.position, patient.transform.position) <= a.foesHealRadius && KnowsAbilityOf(self, c))
                return true;
        return false;
    }

    /// "foe" is under a mega's protection and "self" knows it (it looks for another target first).
    public static bool Warded(Combatant self, Combatant foe)
    {
        var protector = Combatant.ProtectorOf(foe);
        return protector != null && protector.Protects(foe) && KnowsAbilityOf(self, protector);
    }

    /// A move it uses sparingly may go now (then not again for aiReluctantEvery seconds).
    public static bool MaySpare(Combatant self)
    {
        if (nextSparing.TryGetValue(self, out float next) && Time.time < next) return false;
        nextSparing[self] = Time.time + Abilities.Config.aiReluctantEvery;
        return true;
    }
}
