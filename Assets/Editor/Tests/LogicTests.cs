using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// Fast checks of the pure game logic and of the data files, in edit mode (Window > General > Test Runner, EditMode, or
/// python3 Tools/setup.py --edit-tests): seconds instead of the play-mode bench's minutes, with the editor open.
public class LogicTests
{
    static int TypeNamed(string name)
    {
        for (int t = 0; t < 32; t++)
            if (GameData.TypeName(t) == name) return t;
        Assert.Fail($"type « {name} » introuvable");
        return -1;
    }

    // ---- Items ----

    [Test]
    public void EveryItemSaysWhatItDoes()
    {
        foreach (var i in BagItems.All)
            Assert.IsNotEmpty(BagItems.Describe(i), $"consommable {i.key}");
        foreach (var i in HeldItems.All)
            for (int tier = 1; tier <= HeldItems.MaxTier; tier++)
                Assert.IsNotEmpty(HeldItems.Describe(HeldItems.AtTier(i, tier)), $"objet tenu {i.key}, tier {tier}");
    }

    [Test]
    public void HeldTiersScaleTheirValues()
    {
        var band = HeldItems.Get("choice_band");
        Assert.NotNull(band);
        float one = HeldItems.AtTier(band, 1).attackMul, two = HeldItems.AtTier(band, 2).attackMul, three = HeldItems.AtTier(band, 3).attackMul;
        Assert.Less(one, two);
        Assert.Less(two, three);
        Assert.AreEqual(band.attackMul, two, 1e-4f, "les valeurs écrites sont celles du tier II");
    }

    [Test]
    public void SameHeldItemsMergeIntoTheNextTier()
    {
        var band = HeldItems.Get("choice_band");
        var merged = HeldItems.Merge(HeldItems.AtTier(band, 1), HeldItems.AtTier(band, 1), out var dropped);
        Assert.IsNull(dropped);
        Assert.AreEqual(2, merged.tier);
        merged = HeldItems.Merge(HeldItems.AtTier(band, 3), HeldItems.AtTier(band, 3), out _);
        Assert.AreEqual(HeldItems.MaxTier, merged.tier, "pas au-delà du dernier tier");
        var other = HeldItems.All.First(i => i.key != band.key);
        var kept = HeldItems.Merge(HeldItems.AtTier(band, 2), other, out dropped);
        Assert.AreEqual(other.key, kept.key);
        Assert.AreEqual(band.key, dropped.key, "l'ancien objet tombe");
    }

    [Test]
    public void ItemRollsGiveKnownItems()
    {
        var rng = new System.Random(1);
        for (int k = 0; k < 200; k++)
        {
            var held = HeldItems.Roll(rng);
            if (held != null) Assert.NotNull(HeldItems.Get(held.key));
            var bag = BagItems.Roll(rng);
            if (bag != null) Assert.NotNull(BagItems.Get(bag.key));
        }
    }

    // ---- Types ----

    [Test]
    public void TypeChartFollowsTheSoftenedRules()
    {
        int fire = TypeNamed("Feu"), water = TypeNamed("Eau"), grass = TypeNamed("Plante");
        Assert.Greater(TypeChart.Multiplier(water, new[] { fire }, out var e), 1f);
        Assert.AreEqual(Efficacy.Super, e);
        Assert.Less(TypeChart.Multiplier(fire, new[] { water }, out e), 1f);
        Assert.AreEqual(Efficacy.NotVery, e);
        // A weakness and a resistance cancel out (Eau against Feu / Eau).
        Assert.AreEqual(1f, TypeChart.Multiplier(water, new[] { fire, water }, out _), 1e-4f);
        for (int a = 1; a <= 17; a++) // ROM type ids (0: none)
            for (int d = 1; d <= 17; d++)
            {
                float m = TypeChart.Multiplier(a, new[] { d }, out _);
                Assert.Greater(m, 0f, "pas d'immunité totale");
                Assert.LessOrEqual(m, 2f + 1e-4f);
            }
        Assert.Greater(TypeChart.Multiplier(fire, new[] { grass }, out _), 1f);
    }

    // ---- Encounters ----

    [Test]
    public void WavesAreNeverEmptyAndUseRealPokemon()
    {
        var rng = new System.Random(7);
        for (int stage = 0; stage < 6; stage++)
            for (int wave = 0; wave < 3; wave++)
            {
                var encounters = Encounters.Wave(rng, stage, wave, 1);
                Assert.IsNotEmpty(encounters, $"étape {stage}, vague {wave}");
                foreach (var e in encounters)
                {
                    Assert.IsNotEmpty(e.members);
                    foreach (var m in e.members)
                    {
                        Assert.NotNull(GameData.Pokemon(m.pokemonId), $"Pokémon {m.pokemonId}");
                        Assert.IsTrue(PokemonRoster.Has(m.pokemonId), $"sprites de {m.pokemonId}");
                    }
                }
            }
    }

    [Test]
    public void PacksKeepTheirSizeAndElitesAreElite()
    {
        var rng = new System.Random(3);
        var pack = Encounters.CategoryOf("pack");
        Assert.NotNull(pack);
        for (int k = 0; k < 20; k++)
        {
            var e = Encounters.Make(rng, pack, 2, 1);
            Assert.GreaterOrEqual(e.members.Count, pack.size[0]);
            Assert.LessOrEqual(e.members.Count, pack.size[1]);
        }
        for (int stage = 0; stage < 6; stage++)
        {
            var elite = Encounters.Elite(rng, stage, 1);
            if (elite != null) Assert.IsTrue(elite.category.elite, $"étape {stage}");
        }
    }

    // ---- Data ----

    [Test]
    public void KarateChopSweepsAndThrowsSideways()
    {
        var m = GameData.AllMoves.First(x => x.id == 68);
        Assert.AreEqual("arc", m.shape);
        Assert.IsTrue(m.sweep && m.knockbackSide);
        Assert.AreEqual("random", m.sweepSide);
        Assert.Greater(m.knockback, 0f);
    }

    [Test]
    public void MachPunchStunsThenThrows()
    {
        var m = GameData.AllMoves.First(x => x.id == 300);
        Assert.AreEqual(1, m.hits);
        Assert.Greater(m.knockback, 0f);
        Assert.AreEqual(m.stun, m.knockbackDelay, 0.001f);
    }

    [Test]
    public void MovesUseKnownFamiliesAndStatuses()
    {
        var families = new HashSet<string> { "projectile", "melee", "zone", "burst", "heal", "modifier", "climate" };
        foreach (var m in GameData.AllMoves)
        {
            Assert.IsTrue(string.IsNullOrEmpty(m.family) || families.Contains(m.family), $"{m.name} : famille « {m.family} »");
            if (!string.IsNullOrEmpty(m.status)) Assert.NotNull(StatusLibrary.Get(m.status), $"{m.name} : statut « {m.status} »");
        }
    }

    [Test]
    public void DataFilesParse()
    {
        foreach (var asset in Resources.LoadAll<TextAsset>("Data"))
        {
            if (!asset.text.TrimStart().StartsWith("{")) continue; // levels_rt.bytes and the like
            Assert.DoesNotThrow(() => JsonUtility.FromJson<Placeholder>(asset.text), asset.name);
        }
    }

    [Serializable] class Placeholder { }

    // The game's own settings stay those of a release: the dev profile (meta_dev.json) adds the test switches on top.
    [Test]
    public void ReleaseSettingsHaveNoDevSwitch()
    {
        var file = JsonUtility.FromJson<SaveData.Meta>(Resources.Load<TextAsset>("Data/meta").text);
        Assert.IsFalse(file.devMode, "devMode");
        Assert.IsFalse(file.unlockAll, "unlockAll");
        Assert.IsFalse(file.allItems, "allItems");
    }

    [Test]
    public void AbilitiesPointToTheCatalogue()
    {
        Assert.Greater(AbilityLibrary.All.Count, 120, "abilities_rt.json");
        foreach (var p in GameData.AllPokemon)
        {
            if (p.id >= 9000) continue; // dev characters have no ability
            Assert.IsNotEmpty(p.abilities, p.name);
            foreach (int a in p.abilities) Assert.NotNull(AbilityLibrary.Get(a), $"{p.name} : talent {a}");
            if (p.hiddenAbility != 0) Assert.NotNull(AbilityLibrary.Get(p.hiddenAbility), $"{p.name} : talent caché {p.hiddenAbility}");
        }
        foreach (var m in GameData.AllMegas) Assert.NotNull(AbilityLibrary.Get(m.ability), $"{m.name} : talent {m.ability}");
    }

    [Test]
    public void AbilityEffectsUseKnownData()
    {
        Assert.Greater(Abilities.All.Count, 0, "ability_effects.json");
        foreach (var e in Abilities.All)
        {
            string name = AbilityLibrary.Get(e.id)?.name;
            Assert.NotNull(name, $"talent {e.id}");
            foreach (var s in e.statusImmune) Assert.NotNull(StatusLibrary.Get(s), $"{name} : {s}");
            foreach (var s in e.contactStatuses) Assert.NotNull(StatusLibrary.Get(s), $"{name} : {s}");
            foreach (var s in e.statusDurations) Assert.NotNull(StatusLibrary.Get(s.status), $"{name} : {s.status}");
            if (!string.IsNullOrEmpty(e.hitStatus)) Assert.NotNull(StatusLibrary.Get(e.hitStatus), $"{name} : {e.hitStatus}");
            if (e.arrivalClimate != 0) Assert.AreEqual("climate", GameData.Move(e.arrivalClimate)?.family, $"{name} : météo");
            foreach (var w in e.weathers) Assert.AreNotEqual(w.weather, Climate.WeatherName(w.weather), $"{name} : {w.weather}");
            Assert.AreNotEqual("Sans effet pour l'instant.", Abilities.Describe(e), name);
        }
    }

    [Test]
    public void AbilityRulesChangeDamage()
    {
        var thickFat = Abilities.Effect(2);
        Assert.AreEqual(0.6f, Abilities.TakenMul(thickFat, 2, Efficacy.Normal), 1e-4f, "Isograisse, Feu");
        Assert.AreEqual(1f, Abilities.TakenMul(thickFat, 3, Efficacy.Normal), 1e-4f, "Isograisse, Eau");
        Assert.AreEqual(0.8f, Abilities.TakenMul(Abilities.Effect(110), 3, Efficacy.Super), 1e-4f, "Filtre");

        var blaze = Abilities.Effect(70);
        var ember = new MoveDef { type = 2, power = 6, category = "special" };
        Assert.AreEqual(1.5f, Abilities.DealtMul(blaze, ember, 0.3f, false, null), 1e-4f, "Brasier sous 1/3");
        Assert.AreEqual(1f, Abilities.DealtMul(blaze, ember, 0.5f, false, null), 1e-4f, "Brasier PV hauts");
        Assert.AreEqual(1.5f, Abilities.DealtMul(Abilities.Effect(100), ember, 1f, false, null), 1e-4f, "Technicien");

        var sturdy = Abilities.Effect(13);
        Assert.AreEqual(99f, Abilities.Endure(sturdy, 100f, 100f, 500f), 1e-4f, "Fermeté à PV pleins");
        Assert.AreEqual(500f, Abilities.Endure(sturdy, 90f, 100f, 500f), 1e-4f, "Fermeté entamée");
        Assert.IsTrue(Abilities.Absorbs(Abilities.Effect(36), 3), "Absorbe-Eau");
    }

    [Test]
    public void AbilityDrawRespectsTheHiddenChance()
    {
        var stats = new PokemonStats { abilities = new[] { 65 }, hiddenAbility = 34 };
        var rng = new System.Random(1);
        Assert.AreEqual(2, Abilities.DrawSlot(stats, 1f, rng));
        Assert.AreEqual(0, Abilities.DrawSlot(stats, 0f, rng));
        Assert.AreEqual(34, Abilities.InSlot(stats, 2));
        Assert.AreEqual(65, Abilities.InSlot(stats, 1), "un seul talent : le second emplacement reprend le premier");
    }

    [Test]
    public void RolloutGrowsEachStep()
    {
        Assert.AreEqual(1f, Combatant.RolloutMul(0.3f, 0), 1e-5f);
        Assert.AreEqual(2.5f, Combatant.RolloutMul(0.3f, 5), 1e-5f);
        var roulade = GameData.Move(105);
        Assert.Greater(roulade.rolloutMax, 0, "Roulade en série");
    }
}
