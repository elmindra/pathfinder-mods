// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Kingmaker;
using Kingmaker.AreaLogic;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Combat;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI.Common;
using Kingmaker.UI.ServiceWindow.CharacterScreen;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Actions;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.Visual.Animation.Kingmaker.Actions;
using Kingmaker.Visual.Sound;
using Newtonsoft.Json;
using UnityEngine;
using static Kingmaker.RuleSystem.RulebookEvent;

namespace EldritchArcana
{
    static class HeavensMystery
    {
        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass oracle => OracleClass.oracle;
        static BlueprintCharacterClass[] oracleArray => OracleClass.oracleArray;

        internal static (BlueprintFeature, BlueprintFeature) Create(String mysteryDescription, BlueprintFeature classSkillFeat)
        {
            var skill1 = StatType.SkillKnowledgeArcana;
            var skill2 = StatType.SkillPerception;

            var mystery = Helpers.CreateProgression("MysteryHeavensProgression", "Heavens Mystery", $"{mysteryDescription}\n" +
                "Oracles who drawn to the night sky learn spells and revelations that draw power from the heavens: the many colors of starlight, the moon, or the empty void of space.\n" +
                $"Class skills: {UIUtility.GetStatText(skill1)}, {UIUtility.GetStatText(skill2)}",
                "dabcaefe63bc471dac44e8e23c1c330f",
                Helpers.GetIcon("91da41b9793a4624797921f221db653c"), // color spray
                UpdateLevelUpDeterminatorText.Group,
                AddClassSkillIfHasFeature.Create(skill1, classSkillFeat),
                AddClassSkillIfHasFeature.Create(skill2, classSkillFeat));
            mystery.Classes = oracleArray;

            var spells = Bloodlines.CreateSpellProgression(mystery, new String[] {
                "91da41b9793a4624797921f221db653c", // color spray
                Spells.hypnoticPatternId,
                "bf0accce250381a44b857d4af6c8e10d", // searing light (should be: daylight)
                "4b8265132f9c8174f87ce7fa6d0fe47b", // rainbow pattern
                FlySpells.overlandFlight.AssetGuid,
                "645558d63604747428d55f0dd3a4cb58", // chain lightning
                "b22fd434bdb60fb4ba1068206402c4cf", // prismatic spray
                "e96424f70ff884947b06f41a765b7658", // sunburst
                FireSpells.meteorSwarm.AssetGuid,
            });

            var revelations = new List<BlueprintFeature>()
            {
                // TODO
            };
            var description = new StringBuilder(mystery.Description).AppendLine();
            description.AppendLine("An oracle with the flame mystery can choose from any of the following revelations:");
            foreach (var r in revelations)
            {
                description.AppendLine($"• {r.Name}");
                r.InsertComponent(0, Helpers.PrerequisiteFeature(mystery));
            }
            mystery.SetDescription(description.ToString());

            var entries = new List<LevelEntry>();
            for (int level = 1; level <= 9; level++)
            {
                entries.Add(Helpers.LevelEntry(level * 2, spells[level - 1]));
            }
            // TODO:
            //var finalRevelation = CreateFinalRevelation();
            //entries.Add(Helpers.LevelEntry(20, finalRevelation));

            mystery.LevelEntries = entries.ToArray();
            mystery.UIGroups = Helpers.CreateUIGroups(new List<BlueprintFeatureBase>(spells) { /*TODO:finalRevelation*/ });

            var revelation = Helpers.CreateFeatureSelection("MysteryFlameRevelation", "Flame Revelation",
                mystery.Description, "40db1e0f9b3a4f5fb9fde0801b158216", null, FeatureGroup.None,
                Helpers.PrerequisiteFeature(mystery));
            revelation.Mode = SelectionMode.OnlyNew;
            revelation.SetFeatures(revelations);
            return (mystery, revelation);
        }
    }
}