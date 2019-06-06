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
using Kingmaker.UI.LevelUp;
using Kingmaker.UI.ServiceWindow.CharacterScreen;
using Kingmaker.UI.Tooltip;
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
    static class OracleArchetypes
    {
        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass oracle => OracleClass.oracle;
        static BlueprintCharacterClass[] oracleArray => OracleClass.oracleArray;

        internal static List<BlueprintArchetype> Create(BlueprintFeatureSelection mystery, BlueprintFeatureSelection revelation, BlueprintFeature mysteryClassSkills)
        {
            var archetypes = new List<BlueprintArchetype>();

            archetypes.Add(CreateSeeker(revelation, mysteryClassSkills));
            archetypes.Add(CreateAncientLorekeeper(mystery, mysteryClassSkills));
            //archetypes.Add(CreateDivineHerbalist(revelation, mysteryClassSkills));

            // TODO: oracle archetypes? Ideas
            // - Divine Herbalist (paladin feel -- lay on hands & chance to cure conditions, popular choice with Life)
            // - Dual Cursed (very popular choice, not too hard to implement other than the spells, see below.)
            // - Elementalist Oracle (not too bad if Admixture subschool is implemented.)
            // - ...
            //
            // Dual Cursed:
            // - Fortune: is basically the same as Rewind Time, so it's already implemented.
            // - Misfortune: forcing enemies to reroll saving throws is generally the best use
            //   of this ability; a toggle could be used to enable this for all enemies. Similar
            //   to Persistent Spell.
            // - Ill Omen spell: kind of like Rewind Time but reversed, must take worse rolls.
            // - Oracle's burden: a bit tricky because the curses are not really designed to work
            //   on 

            return archetypes;
        }

        static BlueprintArchetype CreateAncientLorekeeper(BlueprintFeatureSelection mysteries, BlueprintFeature mysteryClassSkills)
        {
            var lorekeeper = Helpers.Create<BlueprintArchetype>(a =>
            {
                a.name = "AncientLorekeeperArchetype";
                a.LocalizedName = Helpers.CreateString($"{a.name}.Name", "Ancient Lorekeeper");
                a.LocalizedDescription = Helpers.CreateString($"{a.name}.Description",
                    "The ancient lorekeeper is a repository for all the beliefs and vast knowledge of an elven people. They show a strong interest in and understanding of histories and creation legends at a young age, and as they mature their calling to serve as the memory of their long-lived people becomes clear to all who know them.\n" +
                    "An ancient lorekeeper adds Knowledge (arcana) to their list of class skills. This replaces the bonus skills the ancient lorekeeper gains from their mystery.");
            });
            Helpers.SetField(lorekeeper, "m_ParentClass", oracle);
            library.AddAsset(lorekeeper, "df571bf056a941babe0903e94430dc9d");

            lorekeeper.RemoveFeatures = new LevelEntry[] {
                Helpers.LevelEntry(1, mysteryClassSkills),
            };

            var classSkill = Helpers.CreateFeature("AncientLorekeeperClassSkills",
                UIUtility.GetStatText(StatType.SkillKnowledgeArcana),
                "An ancient lorekeeper adds Knowledge (arcana) to their class skills.",
                "a6edf10077d24a95b6d8a701b8fb51d5", null, FeatureGroup.None,
                Helpers.Create<AddClassSkill>(a => a.Skill = StatType.SkillKnowledgeArcana));

            var entries = new List<LevelEntry> { Helpers.LevelEntry(1, classSkill), };
            var elvenArcana = CreateElvenArcana();
            for (int level = 2; level <= 18; level += 2)
            {
                entries.Add(Helpers.LevelEntry(level, elvenArcana));
            }
            lorekeeper.AddFeatures = entries.ToArray();

            // Enable archetype prerequisites
            var patchDescription = "Archetype prerequisites (such as Ancient Lorekeeper)";
            Main.ApplyPatch(typeof(CharBSelectorLayer_FillData_Patch), patchDescription);
            Main.ApplyPatch(typeof(DescriptionTemplatesLevelup_LevelUpClassPrerequisites_Patch), patchDescription);
            Main.ApplyPatch(typeof(CharacterBuildController_SetRace_Patch), patchDescription);

            lorekeeper.SetComponents(
                Helpers.PrerequisiteFeaturesFromList(Helpers.elf, Helpers.halfElf));

            // Adjust prerequisites for mysteries
            foreach (var mystery in mysteries.AllFeatures)
            {
                if (mystery == TimeMystery.mystery)
                {
                    mystery.AddComponent(Helpers.Create<PrerequisiteArchetypeLevel>(p => { p.Archetype = lorekeeper; p.CharacterClass = oracle; }));
                }
                else
                {
                    mystery.AddComponent(Helpers.Create<PrerequisiteNoArchetype>(p => { p.Archetype = lorekeeper; p.CharacterClass = oracle; }));
                }
            }

            return lorekeeper;
        }

        static BlueprintFeature CreateElvenArcana()
        {
            var elvenMagic = library.Get<BlueprintFeature>("55edf82380a1c8540af6c6037d34f322");

            var chooseSpell = Helpers.CreateParamSelection<SelectAnySpellAtComputedLevel>(
                "AncientLorekeeperElvenArcana",
                "Elven Arcana",
                "At 2nd level, an ancient lorekeeper’s mastery of elven legends and philosophy has allowed them to master one spell used by elven wizards. They select one spell from the sorcerer/ wizard spell list that is at least one level lower than the highest-level oracle spell they can cast. The ancient lorekeeper gains this as a bonus spell known. The spell is treated as one level higher than its true level for all purposes. The ancient lorekeeper may choose an additional spell at 4th, 6th, 8th, 10th, 12th, 14th, 16th, and 18th levels.\n" +
                "This ability replaces the bonus spells they would normally gain at these levels from their chosen mystery.",
                "aee6d141ddd545c287f64e553ab0bf04",
                elvenMagic.Icon,
                FeatureGroup.None,
                Helpers.wizardSpellList.CreateLearnSpell(oracle, penalty: 1));
            chooseSpell.Ranks = 9;
            chooseSpell.SpellList = Helpers.wizardSpellList;
            chooseSpell.SpellcasterClass = oracle;
            chooseSpell.SpellLevelPenalty = 1;

            var selection = Helpers.CreateFeatureSelection($"{chooseSpell.name}Selection", chooseSpell.Name, chooseSpell.Description,
                "51ed5bf78de74e0cb6e55024bb948f7e", chooseSpell.Icon, FeatureGroup.None);
            selection.SetFeatures(chooseSpell);
            return selection;
        }

        static BlueprintArchetype CreateSeeker(BlueprintFeatureSelection revelation, BlueprintFeature mysteryClassSkills)
        {
            var seeker = Helpers.Create<BlueprintArchetype>(a =>
            {
                a.name = "SeekerOracleArchetype";
                a.LocalizedName = Helpers.CreateString($"{a.name}.Name", "Seeker");
                a.LocalizedDescription = Helpers.CreateString($"{a.name}.Description",
                    "Oracles gain their magical powers through strange and mysterious ways, be they chosen by fate or blood. While most might be content with their strange powers, some oracles join the Pathfinders specifically to find out more about their mysteries and determine the genesis and history of their eldritch talents. These spellcasters are known among the Spells as seekers, after their obsession with researching ancient texts and obscure ruins for any clues they can find about their heritage and histories.");
            });
            Helpers.SetField(seeker, "m_ParentClass", oracle);
            library.AddAsset(seeker, "15c95e56e3414c089b624b50c18127a0");
            seeker.RemoveFeatures = new LevelEntry[] {
                Helpers.LevelEntry(1, mysteryClassSkills),
                Helpers.LevelEntry(3, revelation),
                Helpers.LevelEntry(15, revelation),
            };
            seeker.AddFeatures = new LevelEntry[] {
                Helpers.LevelEntry(1, CreateSeekerTinkering()),
                Helpers.LevelEntry(3, CreateSeekerLore()),
                Helpers.LevelEntry(15, CreateSeekerMagic()),
            };
            return seeker;
        }

        static BlueprintFeature CreateSeekerTinkering()
        {
            var trapfinding = library.Get<BlueprintFeature>("dbb6b3bffe6db3547b31c3711653838e");
            var tinkering = Helpers.CreateFeature("SeekerTinkering", "Tinkering",
                "Seekers often look to ancient devices, old tomes, and strange magical items in order to learn more about their oracle mysteries. As a result of this curiosity and thanks to an innate knack at deciphering the strange and weird, a seeker adds half their oracle level on Perception checks made to locate traps (minimum +1). If the seeker also possesses levels in rogue or another class that provides the trapfinding ability, those levels stack with their oracle levels for determining their overall bonus on these skill checks.\nThis ability replaces all of the bonus class skills they would otherwise normally gain from their mystery.",
                "fe8c5a9648414b35ae86176b7d77ea2b",
                trapfinding.Icon,
                FeatureGroup.None,
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.Div2,
                    min: 1, classes: oracleArray),
                Helpers.CreateAddContextStatBonus(StatType.SkillPerception, ModifierDescriptor.UntypedStackable));
            return tinkering;
        }

        static BlueprintFeature CreateSeekerLore()
        {
            var arcaneCombatCastingAdept = library.Get<BlueprintFeature>("7aa83ee3526a946419561d8d1aa09e75");
            var feat = Helpers.CreateFeature("SeekerLore", "Seeker Lore",
                "By 3rd level, a seeker has already learned much about their mystery, and is more comfortable using the bonus spells gained by that mystery. They gain a +4 bonus on all concentration checks, on caster level checks made to overcome spell resistance with their bonus spells.\nThis ability replaces the revelation gained at 3rd level.",
                "64270d88d68f4aaca5dd986fd6b60f1c",
                arcaneCombatCastingAdept.Icon,
                FeatureGroup.None,
                Helpers.Create<ConcentrationBonusForGrantedSpells>(c => c.Class = oracle),
                Helpers.Create<SpellPenetrationBonusForGrantedSpells>(s => s.Class = oracle));
            return feat;
        }

        static BlueprintFeature CreateSeekerMagic()
        {
            var arcaneSchoolPower = library.Get<BlueprintFeatureSelection>("3524a71d57d99bb4b835ad20582cf613");
            var feat = Helpers.CreateFeature("SeekerMagic", "Seeker Magic",
                "At 15th level, a seeker becomes skilled at modifying their mystery spells with metamagic. When a seeker applies a metamagic feat to any bonus spells granted by their mystery, they reduce the metamagic feat’s spell level adjustment by 1. Thus, applying a Metamagic feat like Extend Spell to a spell does not change its effective spell level at all, while applying Quicken Spell only increases the spell’s effective spell level by 3 instead of by 4. This reduction to the spell level adjustment for Metamagic feats does not stack with similar reductions from other abilities.\nThis ability replaces the revelation gained at 15th level.",
                "201005185def4d7687a94c81b9b9394d",
                arcaneSchoolPower.Icon,
                FeatureGroup.None,
                Helpers.Create<ReduceMetamagicCostForGrantedSpells>(r => r.Class = oracle));
            return feat;
        }

        internal static bool IsGrantedSpell(UnitDescriptor unit, BlueprintAbility spell)
        {
            return unit.Progression.Features.Enumerable
                .SelectMany(f => f.Blueprint.GetComponents<AddKnownSpell>())
                .Any(a => a.Spell == spell);
        }

        internal static bool MeetsPrerequisites(BlueprintArchetype archetype, UnitDescriptor unit, LevelUpState state)
        {
            bool? all = null;
            bool? any = null;
            foreach (var prerequisite in archetype.GetComponents<Prerequisite>())
            {
                var passed = prerequisite.Check(null, unit, state);
                if (prerequisite.Group == Prerequisite.GroupType.All)
                {
                    all = (!all.HasValue) ? passed : (all.Value && passed);
                }
                else
                {
                    any = (!any.HasValue) ? passed : (any.Value || passed);
                }
            }
            var result = (!all.HasValue || all.Value) && (!any.HasValue || any.Value);
            return result;
        }
    }

    public class SpellPenetrationBonusForGrantedSpells : RuleInitiatorLogicComponent<RuleSpellResistanceCheck>
    {
        public BlueprintCharacterClass Class;
        public int Bonus = 4;

        public override void OnEventAboutToTrigger(RuleSpellResistanceCheck evt)
        {
            if (evt.Ability.Type != AbilityType.Spell) return;
            // TODO: ideally we could check the spellbook used to cast the spell.
            var spellbook = Owner.GetSpellbook(Class);
            if (spellbook == null || !spellbook.IsKnown(evt.Ability)) return;
            if (OracleArchetypes.IsGrantedSpell(Owner, evt.Ability))
            {
                evt.AdditionalSpellPenetration += Bonus;
            }
        }

        public override void OnEventDidTrigger(RuleSpellResistanceCheck evt) { }
    }

    public class ConcentrationBonusForGrantedSpells : RuleInitiatorLogicComponent<RuleCalculateAbilityParams>
    {
        public BlueprintCharacterClass Class;
        public int Bonus = 4;

        public override void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            if (evt.Spellbook != Owner.GetSpellbook(Class)) return;
            if (OracleArchetypes.IsGrantedSpell(Owner, evt.Spell))
            {
                evt.AddBonusConcentration(Bonus);
            }
        }

        public override void OnEventDidTrigger(RuleCalculateAbilityParams evt) { }
    }

    public class ReduceMetamagicCostForGrantedSpells : RuleInitiatorLogicComponent<RuleApplyMetamagic>
    {
        public BlueprintCharacterClass Class;

        public int Reduction = 1;

        // True if this doesn't stack with other metamagic reductions
        public bool DoesNotStack = true;

        public override void OnEventAboutToTrigger(RuleApplyMetamagic evt)
        {
            if (evt.Spellbook != Owner.GetSpellbook(Class)) return;
            if (OracleArchetypes.IsGrantedSpell(Owner, evt.Spell))
            {
                Log.Write($"Reduce cost of spell: {evt.Spell.name} by {Reduction}");
                evt.ReduceCost(Reduction);
            }
        }

        public override void OnEventDidTrigger(RuleApplyMetamagic evt)
        {
            // Make sure this doesn't stack with other bonuses.
            var result = evt.Result;
            var totalReduction = (int)get_CostReduction(evt);
            if (DoesNotStack && totalReduction > Reduction)
            {
                int withoutThisReduction = totalReduction - Reduction;
                set_CostReduction(evt, Math.Max(withoutThisReduction, Reduction));
                evt.Result.Clear();
                evt.OnTrigger(Rulebook.CurrentContext);
            }
        }

        static FastGetter get_CostReduction = Helpers.CreateFieldGetter<RuleApplyMetamagic>("m_CostReduction");
        static FastSetter set_CostReduction = Helpers.CreateFieldSetter<RuleApplyMetamagic>("m_CostReduction");
    }

    [Harmony12.HarmonyPatch(typeof(DescriptionTemplatesLevelup), "LevelUpClassPrerequisites", typeof(DescriptionBricksBox), typeof(TooltipData), typeof(bool))]
    static class DescriptionTemplatesLevelup_LevelUpClassPrerequisites_Patch
    {
        static void Postfix(DescriptionTemplatesLevelup __instance, DescriptionBricksBox box, TooltipData data, bool b)
        {
            try
            {
                if(data?.Archetype == null || Main.settings?.RelaxAncientLorekeeper == true) return;
                Prerequisites(__instance, box, data.Archetype.GetComponents<Prerequisite>());
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        static readonly FastInvoke Prerequisites = Helpers.CreateInvoker<DescriptionTemplatesLevelup>("Prerequisites", new Type[] { typeof(DescriptionBricksBox), typeof(IEnumerable<Prerequisite>), typeof(FeatureSelectionState) });
    }

    [Harmony12.HarmonyPatch(typeof(CharBSelectorLayer), "FillData", typeof(BlueprintCharacterClass), typeof(BlueprintArchetype[]), typeof(CharBFeatureSelector))]
    static class CharBSelectorLayer_FillData_Patch
    {
        static void Postfix(CharBSelectorLayer __instance, BlueprintCharacterClass charClass, BlueprintArchetype[] archetypesList)
        {
            try
            {
                var self = __instance;
                var items = self.SelectorItems;
                if (items == null || archetypesList == null || items.Count == 0 || Main.settings?.RelaxAncientLorekeeper == true)
                {
                    return;
                }

                // Note: conceptually this is the same as `CharBSelectorLayer.FillDataLightClass()`,
                // but for archetypes.

                // TODO: changing race won't refresh the prereq, although it does update if you change class.
                var state = Game.Instance.UI.CharacterBuildController.LevelUpController.State;
                foreach (var item in items)
                {
                    var archetype = item?.Archetype;
                    if (archetype == null || !archetypesList.Contains(archetype)) continue;

                    item.Show(state: true);
                    item.Toggle.interactable = item.enabled = OracleArchetypes.MeetsPrerequisites(archetype, state.Unit, state);
                    var classData = state.Unit.Progression.GetClassData(state.SelectedClass);
                    self.SilentSwitch(classData.Archetypes.Contains(archetype), item);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    [Harmony12.HarmonyPatch(typeof(CharacterBuildController), "SetRace", typeof(BlueprintRace))]
    static class CharacterBuildController_SetRace_Patch
    {
        static bool Prefix(CharacterBuildController __instance, BlueprintRace race)
        {
            try
            {
                if (race == null || Main.settings?.RelaxAncientLorekeeper == true) return true;
                var self = __instance;
                var levelUp = self.LevelUpController;
                var @class = levelUp.State.SelectedClass;
                if (@class == null) return true;

                if (@class.Archetypes.Any(a => a.GetComponents<Prerequisite>() != null))
                {
                    self.SetArchetype(null);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return true;
        }
    }
}
