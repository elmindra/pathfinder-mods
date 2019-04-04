// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Controllers.MapObjects;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.View.MapObjects;

namespace EldritchArcana
{
    static class ArcaneSavantClass
    {
        static LibraryScriptableObject library => Main.library;
        internal static BlueprintCharacterClass savant;
        internal static BlueprintCharacterClass[] savantArray;

        internal static void Load()
        {
            var library = Main.library;
            if (ArcaneSavantClass.savant != null) return;

            // TODO: prestigious spellcaster needs to recognize Arcane Savant.
            var savant = ArcaneSavantClass.savant = Helpers.Create<BlueprintCharacterClass>();
            savantArray = new BlueprintCharacterClass[] { savant };
            savant.name = "ArcaneSavantClass";
            library.AddAsset(savant, "50dfddb6962b4f13a631362766ba4b61");
            savant.LocalizedName = Helpers.CreateString("ArcaneSavant.Name", "ArcaneSavant");
            savant.LocalizedDescription = Helpers.CreateString("ArcaneSavant.Description",
                "Arcane savants are specialists in the theory and practice of magic, illuminating mysteries of the eldritch fabric that permeates existence. " +
                "The path of the arcane savant brings expertise in the lore of glyphs and sigils, knowledge of exotic spells, and the power to unlock the full potential of magical devices. " +
                "This skill also makes savants quite valuable to adventuring parties, both in their mastery over ancient traps that utilize old magic and in their skill at identifying and utilizing magic items found in the field.");
            savant.SkillPoints = 2;
            savant.HitDie = DiceType.D6;
            savant.PrestigeClass = true;

            // TODO: add Magical Aptitude, in place of Spell Focus: UMD?
            var spellFocus = library.Get<BlueprintParametrizedFeature>("16fa59cc9a72a6043b566b49184f53fe");
            var animalClass = library.Get<BlueprintCharacterClass>("4cd1757a0eea7694ba5c933729a53920");
            savant.SetComponents(
                spellFocus.PrerequisiteFeature(),
                Helpers.GetSkillFocus(StatType.SkillUseMagicDevice).PrerequisiteFeature(),
                StatType.SkillUseMagicDevice.PrerequisiteStatValue(5),
                StatType.SkillKnowledgeArcana.PrerequisiteStatValue(5),
                Helpers.Create<PrerequisiteCasterTypeSpellLevel>(p => { p.IsArcane = true; p.RequiredSpellLevel = 2; p.Group = Prerequisite.GroupType.Any; }),
                Helpers.Create<PrerequisiteCasterTypeSpellLevel>(p => { p.IsArcane = false; p.RequiredSpellLevel = 2; p.Group = Prerequisite.GroupType.Any; }),
                Helpers.Create<PrerequisiteNoClassLevel>(p => p.CharacterClass = animalClass));

            var savesPrestigeLow = library.Get<BlueprintStatProgression>("dc5257e1100ad0d48b8f3b9798421c72");
            savant.BaseAttackBonus = library.Get<BlueprintStatProgression>("0538081888b2d8c41893d25d098dee99"); // BAB low
            savant.FortitudeSave = savesPrestigeLow;
            savant.ReflexSave = savesPrestigeLow;
            savant.WillSave = library.Get<BlueprintStatProgression>("1f309006cd2855e4e91a6c3707f3f700"); // savesPrestigeHigh
            savant.IsDivineCaster = true;
            savant.IsArcaneCaster = true;

            savant.ClassSkills = new StatType[] {
                StatType.SkillKnowledgeArcana,
                StatType.SkillKnowledgeWorld,
                StatType.SkillLoreNature,
                StatType.SkillLoreReligion,
                StatType.SkillPerception,
                StatType.SkillUseMagicDevice
            };

            var wizard = library.Get<BlueprintCharacterClass>("ba34257984f4c41408ce1dc2004e342e");
            savant.StartingGold = wizard.StartingGold;
            savant.PrimaryColor = wizard.PrimaryColor;
            savant.SecondaryColor = wizard.SecondaryColor;

            savant.RecommendedAttributes = Array.Empty<StatType>();
            savant.NotRecommendedAttributes = Array.Empty<StatType>();

            savant.EquipmentEntities = wizard.EquipmentEntities;
            savant.MaleEquipmentEntities = wizard.MaleEquipmentEntities;
            savant.FemaleEquipmentEntities = wizard.FemaleEquipmentEntities;

            savant.StartingItems = wizard.StartingItems;

            var progression = Helpers.CreateProgression("ArcaneSavantProgression",
                savant.Name,
                savant.Description,
                "e025086772a443039e1b47bb1c9f3d5c",
                savant.Icon,
                FeatureGroup.None);
            progression.Classes = savantArray;

            // TODO: implement these.
            //
            // note: silence master was removed, as it wouldn't do anything in game.
            var esotericMagic = CreateEsotericMagic();
            var entries = new List<LevelEntry> {
                Helpers.LevelEntry(1, CreateAdeptActivation(), CreateMasterScholar()),
                Helpers.LevelEntry(2, CreateSpellbookChoice(), esotericMagic, CreateGlyphFinding()),
                Helpers.LevelEntry(3, esotericMagic, CreateScrollMaster()),
                Helpers.LevelEntry(4, esotericMagic, CreateQuickIdentification()),
                Helpers.LevelEntry(5, esotericMagic, CreateSigilMaster()),
                Helpers.LevelEntry(6, esotericMagic, CreateAnalyzeDweomer()),
                Helpers.LevelEntry(7, esotericMagic, CreateDispellingMaster()),
                Helpers.LevelEntry(8, esotericMagic, CreateSymbolMaster()),
                Helpers.LevelEntry(9, esotericMagic, CreateSpellcastingMaster()),
                Helpers.LevelEntry(10, esotericMagic, CreateItemMaster()),
            };

            progression.UIDeterminatorsGroup = new BlueprintFeatureBase[] {
                
                // TODO: 1st level stuff
            };
            progression.UIGroups = Helpers.CreateUIGroups(); // TODO
            progression.LevelEntries = entries.ToArray();

            savant.Progression = progression;

            savant.Archetypes = Array.Empty<BlueprintArchetype>();

            savant.RegisterClass();
            Helpers.classes.Add(savant);
        }

        static BlueprintFeature CreateItemMaster()
        {
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateSpellcastingMaster()
        {
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateSymbolMaster()
        {
            // TODO: rework Symbol Master, maybe increase save bonus of sigil master?
            // Or remove this?
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateDispellingMaster()
        {
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateAnalyzeDweomer()
        {
            // TODO: Analyze Dweomer.. presumably: this could print all the target's buff info to the combat log?
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateSigilMaster()
        {
            // TODO: Sigil Master as saving throw bonus against all traps? maybe reduce the bonus.
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateQuickIdentification()
        {
            // TODO: Quick Identification as bonus to identifying items?
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateScrollMaster()
        {
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateEsotericMagic()
        {
            // TODO: choice of any class spell; spells not on your list will be learned at +1 level.
            //
            // Note: I don't see anything about restricting the spell to a level you can actually cast.
            //
            // Also this shouldn't give you a known spell, it should only put it on your list.
            // But that's difficult to accomplish in game, so it will award a spell known.
            // (This prestige class works best for prepared casters anyway; sorcerers/oracles give up
            // their bloodline/mystery advancement when they choose it.)
            //
            // There is also some rules debate over whether Esoteric Magic can be used to gain
            // "early access" to spells that are on your list, but also on other lists. For example,
            // taking "Greater Angelic Aspect" from the Paladin list, gaining it at as a level 4 spell
            // instead of 8. But the rules seem to go out of their way to allow this.
            //
            // TODO: Ranks = 9
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateSpellbookChoice()
        {
            // TODO: replace spellbook choice for all arcane/divine spellbooks.
            throw new NotImplementedException();
        }

        static BlueprintFeature CreateGlyphFinding()
        {
            return Helpers.CreateFeature("ArcaneSavantGlyphFinding", "Glyph Finding",
                $"At 2nd level, an arcane savant can use {UIUtility.GetStatText(StatType.SkillKnowledgeArcana)} to find magical " +
                $"traps in the same way a rogue can use {UIUtility.GetStatText(StatType.SkillPerception)} to search for traps.",
                "f64aa29727344ed9b7fa7918943d3038",
                Helpers.GetIcon("dbb6b3bffe6db3547b31c3711653838e"), // trapfinding
                FeatureGroup.None,
                Helpers.Create<UseSkillForTrapFinding>(u => u.Skill = StatType.SkillKnowledgeArcana));
        }

        static BlueprintFeature CreateMasterScholar()
        {
            return Helpers.CreateFeature("ArcaneSavantMasterScholar", "Master Scholar",
                "An arcane savant adds half their class level (minimum 1) as a bonus on Knowledge (arcana), and Use Magic Device checks, and can always take 10 on Knowledge (arcana) checks, even if distracted or endangered.",
                "0f8e9b62eb1b46e194955b1a0592e848",
                Helpers.GetSkillFocus(StatType.SkillKnowledgeArcana).Icon,
                FeatureGroup.None,
                Helpers.Create<Take10ForSuccessLogic>(t => t.Skill = StatType.SkillKnowledgeArcana),
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.Div2, min: 1, classes: savantArray),
                Helpers.CreateAddContextStatBonus(StatType.SkillKnowledgeArcana, ModifierDescriptor.None),
                Helpers.CreateAddContextStatBonus(StatType.SkillUseMagicDevice, ModifierDescriptor.None));
        }

        static BlueprintFeatureBase CreateAdeptActivation()
        {
            return Helpers.CreateFeature("ArcaneSavantAdeptActivation", "Adept Activation",
                "An arcane savant can always take 10 on Use Magic Device checks, except when activating an item blindly.",
                "0d0f6d7e0326444ea519ef9c2cb7c8a4",
                Helpers.GetSkillFocus(StatType.SkillUseMagicDevice).Icon,
                FeatureGroup.None,
                Helpers.Create<Take10ForSuccessLogic>(t => t.Skill = StatType.SkillUseMagicDevice));
        }
    }


    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowMultipleComponents]
    public class Take10ForSuccessLogic : RuleInitiatorLogicComponent<RuleSkillCheck>
    {
        public StatType Skill;

        public override void OnEventAboutToTrigger(RuleSkillCheck evt)
        {
            if (evt.StatType == Skill) evt.Take10ForSuccess = true;
        }

        public override void OnEventDidTrigger(RuleSkillCheck evt) { }
    }

    public class UseSkillForTrapFinding : RuleInitiatorLogicComponent<RuleSkillCheck>
    {
        public StatType Skill;

        public override void OnEventAboutToTrigger(RuleSkillCheck evt)
        {
            if (evt.StatType == StatType.SkillPerception && evt.Reason.SourceEntity is TrapObjectView.TrapObjectData)
            {
                Log.Write($"{GetType().Name} use {Skill} for finding trap {evt.Reason.SourceEntity}");
                Helpers.SetField(evt, "StatType", Skill);
            }
        }

        public override void OnEventDidTrigger(RuleSkillCheck evt) { }
    }
}
