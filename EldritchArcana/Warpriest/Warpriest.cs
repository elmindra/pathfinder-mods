// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Root;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.Utility;
using Newtonsoft.Json;

namespace EldritchArcana
{
    static class WarpriestClass
    {
        static LibraryScriptableObject library => Main.library;
        internal static BlueprintCharacterClass warpriest;
        internal static BlueprintCharacterClass[] warpriestArray;

        internal static void Load()
        {
            if (WarpriestClass.warpriest != null) return;

            var cleric = Helpers.GetClass("67819271767a9dd4fbfd4ae700befea0");

            var warpriest = WarpriestClass.warpriest = Helpers.Create<BlueprintCharacterClass>();
            warpriestArray = new BlueprintCharacterClass[] { warpriest };
            warpriest.name = "WarpriestClass";
            library.AddAsset(warpriest, "38856a7c382e42caa5b605bc17bcc787");
            warpriest.LocalizedName = Helpers.CreateString("Warpriest.Name", "Warpriest");
            warpriest.LocalizedDescription = Helpers.CreateString("Warpriest.Description", "Capable of calling upon the power of the gods in the form of blessings and spells, warpriests blend divine magic with martial skill. They are unflinching bastions of their faith, shouting gospel as they pummel foes into submission, and never shy away from a challenge to their beliefs. While clerics might be subtle and use diplomacy to accomplish their aims, warpriests aren’t above using violence whenever the situation warrants it. In many faiths, warpriests form the core of the church’s martial forces—reclaiming lost relics, rescuing captured clergy, and defending the church’s tenets from all challenges.\n" +
                "Role: Warpriests can serve as capable healers or spellcasters, calling upon their divine powers from the center of the fight, where their armor and martial skills are put to the test.\n" +
                "Alignment: A warpriest’s alignment must be within one step of their deity’s, along either the law/chaos axis or the good/evil axis.");
            warpriest.m_Icon = cleric.Icon;
            warpriest.SkillPoints = 2;
            warpriest.HitDie = DiceType.D8;
            warpriest.BaseAttackBonus = cleric.BaseAttackBonus;
            warpriest.FortitudeSave = cleric.FortitudeSave;
            warpriest.ReflexSave = cleric.ReflexSave;
            warpriest.WillSave = cleric.WillSave;

            var spellbook = Helpers.Create<BlueprintSpellbook>();
            spellbook.name = "WarpriestSpellbook";
            library.AddAsset(spellbook, "440161ffb24541148f787f0d4561ed6c");
            spellbook.Name = warpriest.LocalizedName;
            var magusSpellLevels = library.Get<BlueprintSpellsTable>("6326b540f7c6a604f9d6f82cc0e2293c");
            spellbook.SpellsPerDay = magusSpellLevels;
            spellbook.SpellList = cleric.Spellbook.SpellList;
            spellbook.Spontaneous = false;
            spellbook.IsArcane = false;
            spellbook.AllSpellsKnown = true;
            spellbook.CanCopyScrolls = false;
            spellbook.CastingAttribute = StatType.Wisdom;
            spellbook.CharacterClass = warpriest;
            spellbook.CantripsType = CantripsType.Orisions;
            warpriest.Spellbook = spellbook;

            // Consolidated skills make this a bit of a judgement call. Explanation below.
            warpriest.ClassSkills = new StatType[] {
                // Warpriests have Diplomacy, Intimidate and Sense Motive (which in PF:K is like Persuasion).
                StatType.SkillPersuasion,
                // Warpriests have Climb/Swim
                StatType.SkillAthletics,
                // Warpriests have Knowledge (religion) which is the main part of the consolidated skill.
                StatType.SkillLoreReligion,
                // Warpriests have Survial and Handle Animal
                StatType.SkillLoreNature
            };

            warpriest.IsDivineCaster = true;
            warpriest.IsArcaneCaster = false;

            var paladin = library.Get<BlueprintCharacterClass>("bfa11238e7ae3544bbeb4d0b92e897ec");
            warpriest.StartingGold = paladin.StartingGold; // all classes start with 411.
            warpriest.PrimaryColor = paladin.PrimaryColor;
            warpriest.SecondaryColor = paladin.SecondaryColor;

            warpriest.RecommendedAttributes = new StatType[] { StatType.Wisdom };
            warpriest.NotRecommendedAttributes = new StatType[] { StatType.Intelligence };

            warpriest.EquipmentEntities = paladin.EquipmentEntities;
            warpriest.MaleEquipmentEntities = paladin.MaleEquipmentEntities;
            warpriest.FemaleEquipmentEntities = paladin.FemaleEquipmentEntities;

            // Both of the restrictions here are relevant (no atheism feature, no animal class).
            warpriest.ComponentsArray = cleric.ComponentsArray;
            warpriest.StartingItems = paladin.StartingItems;

            var progression = Helpers.CreateProgression("WarpriestProgression",
                warpriest.Name,
                warpriest.Description,
                "944abc5e118f43a798fb536cdd4e5ca3",
                warpriest.Icon,
                FeatureGroup.None);
            progression.Classes = warpriestArray;
            var entries = new List<LevelEntry>();

            var orisons = library.CopyAndAdd<BlueprintFeature>(
                "e62f392949c24eb4b8fb2bc9db4345e3", // cleric orisions
                "WarpriestOrisonsFeature",
                "904f17a28c1d4fde8a2afb036c4a9b41");
            orisons.SetDescription("Warpriests learn a number of orisons, or 0-level spells. These spells are cast like any other spell, but they do not consume any slots and may be used again.");
            orisons.SetComponents(orisons.ComponentsArray.Select(c =>
            {
                var bind = c as BindAbilitiesToClass;
                if (bind == null) return c;
                bind = UnityEngine.Object.Instantiate(bind);
                bind.CharacterClass = warpriest;
                bind.Stat = StatType.Charisma;
                return bind;
            }));
            var proficiencies = library.CopyAndAdd<BlueprintFeature>(
                "b10ff88c03308b649b50c31611c2fefb", // paladin proficiencies
                "WarpriestProficiencies",
                "7fa029dd97a744be84ac5ee53d921ab6");
            proficiencies.SetName("Warpriest Proficiencies");
            proficiencies.SetDescription("A warpriest is proficient with all simple and martial weapons, as well as the favored weapon of their deity, and with all armor (heavy, light, and medium) and shields (except tower shields). If the warpriest worships a deity with unarmed strike as its favored weapon, the warpriest gains Improved Unarmed Strike as a bonus feat.");

            var detectMagic = library.Get<BlueprintFeature>("ee0b69e90bac14446a4cf9a050f87f2e");
            var deitySelection = library.Get<BlueprintFeatureSelection>("59e7a76987fe3b547b9cce045f4db3e4");

            // TODO: blessings. There are 37, so 74 abilities total (PF:K only implements 33 domains).
            // Perhaps we can look at the popular ones, and the deities and ensure each diety has 2 options.
            //
            // Absolute minimum (12+4 alignment options, which are all similar):
            // - Good (restrict to PC & diety alignment)
            // - Evil
            // - Law
            // - Chaos
            // - Protection
            // - Destruction
            // - Death
            // - Healing
            // - Air
            // - Plant
            // - Trickery
            // - Strength
            // - War (how to deal with nonlethal damage? it can be tracked, but what about UI?)
            // - Sun
            // - Travel
            // - Luck (will need some adaptation, but doable)
            //
            // Others: Earth, Fire/Water (these are similar), Weather. Tricky but interesting: Rune, Charm
            //
            // These blessings are generally useful, and should give each character at least 2,
            // regardless of deity/alignment. (Note: they may only get 2 choices in some instances.)
            // True neutral characters/deities will get the fewest choices. Otherwise the PC should
            // get 1-2 alignment options, in addition to at least 1 other option.
            entries.Add(Helpers.LevelEntry(1,
                proficiencies,
                deitySelection,
                // TODO: Blessing selection x2, focus weapon, sacred weapon
                orisons,
                library.Get<BlueprintFeature>("d3e6275cfa6e7a04b9213b7b292a011c"), // ray calculate feature
                library.Get<BlueprintFeature>("62ef1cdb90f1d654d996556669caf7fa"), // touch calculate feature
                detectMagic
            ));
            // TODO: BAB/Fighter prereq fix. Either patch, or scan & adjust?
            var fighterFeat = library.Get<BlueprintFeatureSelection>("41c8486641f7d6d4283ca9dae4147a9f");

            // TODO: create separate features to show fervor bumps, sacred weapon, sacred armor.
            // (that's how the game usually handles things like that, and it looks nicer.)
            entries.Add(Helpers.LevelEntry(2)); // TODO: fervor
            entries.Add(Helpers.LevelEntry(3, fighterFeat));
            entries.Add(Helpers.LevelEntry(4)); // TODO: sacred weapon enhance, channel energy
            entries.Add(Helpers.LevelEntry(6, fighterFeat));
            entries.Add(Helpers.LevelEntry(7)); // TODO: sacred armor
            entries.Add(Helpers.LevelEntry(9, fighterFeat));
            entries.Add(Helpers.LevelEntry(12, fighterFeat));
            entries.Add(Helpers.LevelEntry(15, fighterFeat));
            entries.Add(Helpers.LevelEntry(18, fighterFeat));
            entries.Add(Helpers.LevelEntry(20)); // TODO: aspect of war
            progression.UIDeterminatorsGroup = new BlueprintFeatureBase[] {
                // TODO: 1st level stuff
            };
            progression.UIGroups = Helpers.CreateUIGroups(); // TODO
            progression.LevelEntries = entries.ToArray();

            warpriest.Progression = progression;

            warpriest.Archetypes = Array.Empty<BlueprintArchetype>();

            warpriest.RegisterClass();
            Helpers.classes.Add(warpriest);
        }
    }
}