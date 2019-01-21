// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Validation;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Properties;
using Kingmaker.Utility;
using Newtonsoft.Json;
using UnityEngine;

namespace EldritchArcana
{
    static class EldritchHeritage
    {
        static LibraryScriptableObject library => Main.library;
        static BlueprintFeatureSelection bloodlineSelection => Helpers.bloodlineSelection;

        static BlueprintFeatureSelection heritageSelection;

        static BlueprintFeatureSelection improvedHeritageSelection;

        internal static void Load()
        {
            if (heritageSelection != null) return;

            var spellSpecialization = library.Get<BlueprintFeatureSelection>("fe67bc3b04f1cd542b4df6e28b6e0ff5");
            //var noFeature = Helpers.Create<PrerequisiteNoFeature>();
            heritageSelection = Helpers.CreateFeatureSelection("EldritchHeritageSelection",
                "Eldritch Heritage",
                "You are descended from a long line of sorcerers, and some portion of their power flows in your veins.\n" +
                "Select one sorcerer bloodline. You must have Skill focus in the class skill that bloodline grants to a sorcerer at 1st level (for example, Heal for the celestial bloodline). This bloodline cannot be a bloodline you already have. You gain the first-level bloodline power for the selected bloodline. For purposes of using that power, treat your sorcerer level as equal to your character level – 2, even if you have levels in sorcerer. You do not gain any of the other bloodline abilities.",
                "733b54b0669b4aeda47953ec0e2b33dd",
                spellSpecialization.Icon,
                FeatureGroup.Feat);

            var components = new List<BlueprintComponent> {
                heritageSelection.PrerequisiteNoFeature(),
                Helpers.PrerequisiteCharacterLevel(3),
                Helpers.PrerequisiteStatValue(StatType.Charisma, 13),
            };
            components.Add(Helpers.PrerequisiteFeaturesFromList(Helpers.skillFocusFeat.AllFeatures));
            heritageSelection.SetComponents(components);

            improvedHeritageSelection = Helpers.CreateFeatureSelection("ImprovedEldritchHeritageSelection",
                "Improved Eldritch Heritage",
                "The power of your discovered bloodline continues to grow.\n" +
                "You gain either the 3rd-level or the 9th-level power (your choice) of the bloodline you selected with the Eldritch Heritage feat. For purposes of using that power, treat your sorcerer level as equal to your character level – 2, even if you have levels in sorcerer. You do not gain any of the other bloodline abilities.",
                "c8bd273034684e6689b105a7d8bc9c3b",
                spellSpecialization.Icon,
                FeatureGroup.Feat,
                Helpers.PrerequisiteCharacterLevel(11),
                Helpers.PrerequisiteStatValue(StatType.Charisma, 15),
                heritageSelection.PrerequisiteFeature());
            improvedHeritageSelection.Mode = SelectionMode.OnlyNew;
            improvedHeritageSelection.Ranks = 2;

            var noFeature = Helpers.Create<PrerequisiteNoFeature>();
            var greaterHeritageSelection = Helpers.CreateFeatureSelection("GreaterEldritchHeritageSelection",
                "Greater Eldritch Heritage",
                "Your discovered bloodline power reaches its zenith.\n" +
                "You gain an additional power from the bloodline you selected with the Eldritch Heritage feat. You gain a 15th-level (or lower) sorcerer bloodline power that you do not already have. For purposes of using that power, treat your character level as your sorcerer level for all your sorcerer bloodline powers granted by this feat, Eldritch Heritage, and Improved Eldritch Heritage.",
                "24aad7af058a49f88d1203b856409023",
                spellSpecialization.Icon,
                FeatureGroup.Feat,
                Helpers.PrerequisiteCharacterLevel(17),
                Helpers.PrerequisiteStatValue(StatType.Charisma, 17),
                improvedHeritageSelection.PrerequisiteFeature(),
                noFeature);
            noFeature.Feature = greaterHeritageSelection;
            EldritchHeritageBloodlineLogic.greaterHeritageSelection = greaterHeritageSelection;

            var undoChoice = UndoSelection.Feature.Value;
            var heritageFeats = new List<BlueprintFeature> { undoChoice };
            var improvedHeritageFeats = new List<BlueprintFeature> { undoChoice };
            var greaterHeritageFeats = new List<BlueprintFeature> { undoChoice };
            var featDescription = new StringBuilder(heritageSelection.Description)
                .Append($"\n{bloodlineSelection.Name} — {Helpers.skillFocusFeat.Name} prerequisites:");

            bool seenDraconic = false;
            bool seenElemental = false;
            foreach (var bloodline in bloodlineSelection.AllFeatures.Cast<BlueprintProgression>())
            {
                // Create Eldritch Heritage (level 1 power, Prereq: level 3+, Cha 13+, skill focus bloodline skill)
                String classSkillName;
                var heritageFeat = CreateHeritage(bloodline, out classSkillName);
                if (heritageFeat == null) continue;
                heritageFeats.Add(heritageFeat);

                var bloodlineName = bloodline.Name;
                if (bloodline.name.StartsWith("BloodlineDraconic"))
                {
                    if (!seenDraconic)
                    {
                        var i = bloodlineName.IndexOf(" — ");
                        if (i >= 0) bloodlineName = bloodlineName.Substring(0, i);
                        featDescription.Append($"\n  {bloodlineName} — {classSkillName}");
                        seenDraconic = true;
                    }
                }
                else if (bloodline.name.StartsWith("BloodlineElemental"))
                {
                    if (!seenElemental)
                    {
                        var i = bloodlineName.IndexOf(" — ");
                        if (i >= 0) bloodlineName = bloodlineName.Substring(0, i);
                        featDescription.Append($"\n  {bloodlineName} — {classSkillName}");
                        seenElemental = true;
                    }
                }
                else
                {
                    featDescription.Append($"\n  {bloodlineName} — {classSkillName}");
                }

                // Create Improved Eldrith Heritage (choice of level 3/9 powers and use level -2, can select twice, Prereq: level 11+, Cha 15+)
                var improvedFeat3 = CreateImprovedHeritage(bloodline, heritageFeat, 3);
                var improvedFeat9 = CreateImprovedHeritage(bloodline, heritageFeat, 9);
                improvedHeritageFeats.Add(improvedFeat3);
                improvedHeritageFeats.Add(improvedFeat9);

                // Create Greater Eldrith Heritage (choice of level 15 or lower powers and use full level, Prereq: level 17+, Cha 17+)
                var improvedfeats = new BlueprintFeature[] { improvedFeat3, improvedFeat9 };
                var greaterFeat3 = CreateGreaterHeritage(bloodline, improvedfeats, 3, improvedFeat3);
                var greaterFeat9 = CreateGreaterHeritage(bloodline, improvedfeats, 9, improvedFeat9);
                var greaterFeat15 = CreateGreaterHeritage(bloodline, improvedfeats);
                greaterHeritageFeats.Add(greaterFeat3);
                greaterHeritageFeats.Add(greaterFeat9);
                greaterHeritageFeats.Add(greaterFeat15);
            }

            heritageSelection.SetDescription(featDescription.ToString());
            heritageSelection.SetFeatures(heritageFeats);
            improvedHeritageSelection.SetFeatures(improvedHeritageFeats);
            greaterHeritageSelection.SetFeatures(greaterHeritageFeats);

            library.AddFeats(heritageSelection, improvedHeritageSelection, greaterHeritageSelection);
        }

        static BlueprintFeature CreateHeritage(BlueprintProgression bloodline, out String classSkillName)
        {
            var levelLogic = EldritchHeritageBloodlineLogic.TryCreate(bloodline);
            if (levelLogic == null)
            {
                classSkillName = null;
                return null;
            }

            const int powerLevel = 1;
            var levelEntry = bloodline.LevelEntries.First(l => l.Level == powerLevel);
            // There should always be 3 first level items in a bloodline:
            // - add a class skill
            // - add the Arcana
            // - add the 1st level power
            var components = new List<BlueprintComponent> {
                bloodline.PrerequisiteNoFeature(),
                CrossbloodedSorcerer.crossbloodForBloodline[bloodline].PrerequisiteNoFeature()
            };
            if (bloodline.AssetGuid == arcaneBloodlineId)
            {
                // Arcane can select from one of several skills
                components.Add(Helpers.PrerequisiteFeaturesFromList(new StatType[] {
                    StatType.SkillKnowledgeArcana,
                    StatType.SkillKnowledgeWorld,
                    StatType.SkillLoreNature,
                    StatType.SkillLoreReligion
                }.Select(Helpers.GetSkillFocus)));
                classSkillName = "Any Lore/Knowledge Skill";
            }
            else
            {
                // Find the class skill
                AddClassSkill addClassSkill = null;
                foreach (var f in levelEntry.Features)
                {
                    addClassSkill = f.ComponentsArray.Select(c => c as AddClassSkill).FirstOrDefault(c => c != null);
                    if (addClassSkill != null) break;
                }

                var skillFocus = Helpers.GetSkillFocus(addClassSkill.Skill);
                classSkillName = UIUtility.GetStatText(addClassSkill.Skill);
                if (bloodline.AssetGuid == abyssalBloodlineId)
                {
                    // Abyssal's class skill in PnP is Knowledge (planes), so it should be Lore (Religion).
                    // So offer that choice, along with Athletics (which is what the game gives them).
                    components.Add(skillFocus.PrerequisiteFeature(true));
                    components.Add(Helpers.GetSkillFocus(StatType.SkillLoreReligion).PrerequisiteFeature(true));
                    classSkillName += $" or {UIUtility.GetStatText(StatType.SkillLoreReligion)}";
                }
                else
                {
                    components.Add(skillFocus.PrerequisiteFeature());
                }
            }
            components.Add(levelLogic);

            var power = GetBloodlinePower(bloodline, powerLevel);
            return CreateHeritageFeat(bloodline, power, powerLevel,
                $"{bloodline.name.Replace("Progression", "")}EldritchHeritage",
                $"Eldritch Heritage — {bloodline.Name}",
                bloodline.Icon,
                Helpers.MergeIds(bloodline.AssetGuid, "7114742a530d4946ba36888247422abe"),
                components);
        }

        static BlueprintFeature CreateImprovedHeritage(BlueprintProgression bloodline, BlueprintFeature heritageFeat, int powerLevel)
        {
            var power = GetBloodlinePower(bloodline, powerLevel);
            return CreateHeritageFeat(bloodline, power, powerLevel,
                $"{GetPowerName(power)}ImprovedHeritage",
                $"Improved Eldritch Heritage — {power.Name}",
                power.Icon,
                Helpers.MergeIds(power.AssetGuid, "6a4ec4f556ff4f0d9581722972cb6600"),
                new List<BlueprintComponent> { heritageFeat.PrerequisiteFeature() });
        }

        internal static string GetPowerName(BlueprintFeature power)
        {
            var name = power.name;
            return name.EndsWith("1") ? name.Substring(0, name.Length - 1) : name;
        }

        static BlueprintFeature CreateGreaterHeritage(BlueprintProgression bloodline, BlueprintFeature[] allImprovedFeats, int powerLevel = 15, BlueprintFeature improvedFeat = null)
        {
            var power = GetBloodlinePower(bloodline, powerLevel);
            var name = $"{GetPowerName(power)}GreaterHeritage";
            var displayName = $"Greater Eldritch Heritage — {power.Name}";
            var assetId = Helpers.MergeIds(power.AssetGuid, "f2f2797315644c32a949182d79ae151e");
            if (improvedFeat != null)
            {
                // Copy the improved feat so we can update it.
                var greaterFeat = library.CopyAndAdd(improvedFeat, name, assetId);
                greaterFeat.SetName(displayName);
                improvedFeat.AddComponent(greaterFeat.PrerequisiteNoFeature());

                var otherImprovedFeat = allImprovedFeats.First(f => f != improvedFeat);
                greaterFeat.AddComponents(new BlueprintComponent[] {
                    improvedFeat.PrerequisiteNoFeature(),
                    otherImprovedFeat.PrerequisiteFeature()
                });
                return greaterFeat;
            }
            return CreateHeritageFeat(bloodline, power, powerLevel,
                name, displayName, power.Icon, assetId,
                new List<BlueprintComponent> { Helpers.PrerequisiteFeaturesFromList(allImprovedFeats) });
        }

        static BlueprintFeature CreateHeritageFeat(
            BlueprintProgression bloodline, BlueprintFeature power, int powerLevel,
            String name, String displayName, Sprite icon, String assetId,
            List<BlueprintComponent> components)
        {
            var entries = CollectLevelEntries(powerLevel, power, bloodline);
            var description = bloodline.Description + $"\n{power.Description}";

            if (entries.Count == 1)
            {
                var feat = library.CopyAndAdd(power, name, assetId);
                feat.Groups = new FeatureGroup[] { FeatureGroup.Feat };
                feat.SetNameDescriptionIcon(displayName, description, icon);
                if (feat is BlueprintFeatureSelection)
                {
                    feat.AddComponents(components);
                }
                else
                {
                    components.Add(feat.CreateAddFact());
                    feat.SetComponents(components);
                }
                return feat;
            }
            else
            {
                var feat = Helpers.CreateFeature(name, displayName, description, assetId, icon, FeatureGroup.Feat);
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    components.Add(AddFactOnBloodlineLevel.Create(e.Item2, $"${i}", e.Item1));
                }
                feat.SetComponents(components);
                return feat;
            }
        }

        internal static BlueprintFeature GetBloodlinePower(BlueprintProgression bloodline, int level)
        {
            if (level == 1)
            {
                return (BlueprintFeature)bloodline.GetLevelEntry(1).Features.First(f => !IsArcanaOrClassSkill(f));
            }
            var features = bloodline.GetLevelEntry(level).Features;
            var power = features.First(f => (!Char.IsDigit(f.name.Last()) || f.name.Last() == '1') && !f.name.EndsWith("ExtraUse") &&
                f.GetComponent<AddKnownSpell>() == null);
            return (BlueprintFeature)power;
        }

        internal static bool IsArcanaOrClassSkill(BlueprintFeatureBase f) => f.name.EndsWith("Arcana") || f.name.Contains("ClassSkill");

        // Find all entries associated with `ability` in `progression` and return the
        // resulting level entries (containing only features associated with this ability).
        //
        // Some abilities use a progression to allow them to "rank up" over time, for example,
        // replacing claws with stronger versions. These use a naming scheme ending in a
        // number, for example `BloodlineAbyssalClawsFeatureAddLevel1`.
        //
        // We need to find all of the other abilities so we can reproduce the progression.
        //
        // NOTE: we don't adjust levels here. That is handleCollectLevelEntriesd by `TransformComponents`
        // which will use `AddFeatureOnEldritchHeritageLevel` to compute the units's
        // "eldritch heritage level", either character level - 2, or character level if
        // the unit has "greater eldritch heritage".
        internal static List<(int, BlueprintFeature)> CollectLevelEntries(int abilityLevel, BlueprintFeature ability, BlueprintProgression progression)
        {
            var entries = new List<(int, BlueprintFeature)>();
            entries.Add((abilityLevel, ability));
            if (ability.AssetGuid == arcaneNewArcanaId)
            {
                // New Arcana is actually 3 selections: one at 9, one at 13, and one at 17.
                foreach (var level in new int[] { 13, 17 })
                {
                    entries.Add((level, ability));
                }
                return entries;
            }

            var name = ability.name;
            if (name.EndsWith("BaseFeature"))
            {
                // Handle breath weapon extra uses.
                name = name.Substring(0, name.Length - "BaseFeature".Length);
                foreach (var entry in progression.LevelEntries)
                {
                    if (entry.Level <= abilityLevel) continue;
                    var match = entry.Features.FirstOrDefault(f => f.name == name + "ExtraUse") as BlueprintFeature;
                    if (match != null) entries.Add((entry.Level, match));
                }
                if (entries.Count < 3)
                {
                    foreach (var e in entries) Log.Append($"  level {e.Item1} {e.Item2.name}");
                    throw Main.Error($"Failed to find the entries for {ability.name} (guid {ability.AssetGuid})");
                }
                return entries;
            }

            if (!name.EndsWith("1")) return entries;

            name = name.Substring(0, name.Length - 1);
            int abilityRank = 2; // the rank we're searching for
            foreach (var entry in progression.LevelEntries)
            {
                if (entry.Level <= abilityLevel) continue;
                var match = entry.Features.FirstOrDefault(f => f.name == name + abilityRank) as BlueprintFeature;
                if (match != null)
                {
                    entries.Add((entry.Level, match));
                    abilityRank++;
                }
            }
            if (entries.Count < 2)
            {
                foreach (var e in entries) Log.Append($"  level {e.Item1} {e.Item2.name}");
                throw Main.Error($"Failed to find the entries for {ability.name} (guid {ability.AssetGuid})");
            }
            return entries;
        }

        internal const String arcaneNewArcanaId = "20a2435574bdd7f4e947f405df2b25ce";
        const String arcaneBloodlineId = "4d491cf9631f7e9429444f4aed629791";
        const String abyssalBloodlineId = "d3a4cb7be97a6694290f0dcfbd147113";
    }


    // Adds a feature based on bloodline level.
    [AllowMultipleComponents]
    [AllowedOn(typeof(BlueprintUnitFact))]
    public class AddFactOnBloodlineLevel : AddFactOnLevelUpCondtion
    {
        public static AddFactOnBloodlineLevel Create(BlueprintUnitFact feat, string name, int min = 1, int max = 20)
        {
            var a = Helpers.Create<AddFactOnBloodlineLevel>();
            a.name = $"AddFactOnBloodlineLevel${name}";
            a.Feature = feat;
            a.MinLevel = min;
            a.MaxLevelInclusive = max;
            return a;
        }

        protected override int CalcLevel()
        {
            var part = Owner.Get<UnitPartBloodline>();
            var level = part?.CalcLevel(Feature);
            Log.Write($"got level {level} for {Feature.name}, part: {part}");
            return level ?? Owner.Progression.CharacterLevel;
        }
    }

    public abstract class BloodlineLevelLogic : OwnedGameLogicComponent<UnitDescriptor>
    {
        public string BloodlineName;

        public abstract int CalcLevel();

        public override void OnFactActivate() => Owner.Ensure<UnitPartBloodline>().Add(Fact);
        public override void OnFactDeactivate() => Owner.Ensure<UnitPartBloodline>().Remove(Fact);

        protected bool SetBloodlineName(BlueprintProgression bloodline)
        {
            const string prefix = "Bloodline", suffix = "Progression";
            var name = bloodline.name;
            if (!name.StartsWith(prefix) || !name.EndsWith(suffix))
            {
                Log.Write($"Error: bloodline '{name}' should start with 'Bloodline' and end with 'Progression'.");
                BloodlineName = name;
                return false;
            }
            BloodlineName = name.Substring(prefix.Length, name.Length - suffix.Length - prefix.Length);
            return true;
        }

        static BloodlineLevelLogic()
        {
            var description = "Eldritch Heritage/Crossblooded advancement";
            Main.ApplyPatch(typeof(BindAbilitiesToClass_GetLevel_Patch), description);
            Main.ApplyPatch(typeof(AddFeatureOnClassLevel_GetLevel_Patch), description);
            Main.ApplyPatch(typeof(ReplaceCasterLevelOfAbility_CalculateClassLevel_Patch), description);
            Main.ApplyPatch(typeof(ReplaceCasterLevelOfAbility_OnEventAboutToTrigger_Patch), description);
            Main.ApplyPatch(typeof(BlueprintAbilityResource_GetMaxAmount_Patch), description);
            ContextRankConfig_GetValue_Patch.Apply();
        }
    }

    public class EldritchHeritageBloodlineLogic : BloodlineLevelLogic, IUnitGainFactHandler
    {
        public static EldritchHeritageBloodlineLogic TryCreate(BlueprintProgression bloodline)
        {
            var h = Helpers.Create<EldritchHeritageBloodlineLogic>();
            return h.SetBloodlineName(bloodline) ? h : null;
        }

        // Returns the effective level of the unit's bloodline powers, equal to
        // character level - 2, or character level if the unit has "greater eldritch heritage".
        public override int CalcLevel()
        {
            var level = Owner.Progression.CharacterLevel;
            var isGreater = Owner.HasFact(greaterHeritageSelection) || Owner.Progression.Features.HasFact(greaterHeritageSelection);
            return isGreater ? level : level - 2;
        }

        void IUnitGainFactHandler.HandleUnitGainFact(Fact fact)
        {
            if (greaterHeritageSelection.Features.Contains(fact.Blueprint))
            {
                var name = $"Bloodline{BloodlineName}";
                foreach (var f in Owner.Progression.Features.Enumerable)
                {
                    if (!f.Blueprint.name.StartsWith(name)) continue;
                    foreach (var c in f.SelectComponents<AddFeatureOnClassLevel>())
                    {
                        c.OnFactActivate();
                    }
                }
            }
        }

        internal static BlueprintFeatureSelection greaterHeritageSelection;
    }


    // Provides support for calculating "bloodline level" for a given sorcerer bloodline.
    //
    // Both Crossblooded and Eldritch Heritage change the rules for how bloodlines are
    // computed:
    // - Eldritch Heritage is character level - 2, or character level with the "greater" feat
    // - Crossblooded advances with Dragon Disciple, as long as the caster has some draconic
    //   blood. It does not advance with Eldritch Scion.
    public class UnitPartBloodline : UnitPart
    {
        [JsonProperty]
        List<Fact> bloodlines = new List<Fact>();

        internal void Add(Fact fact) => bloodlines.Add(fact);

        internal void Remove(Fact fact) => bloodlines.Remove(fact);

        public int? CalcLevel(BlueprintScriptableObject fact)
        {
            var name = fact.name;
            if (!name.StartsWith("Bloodline")) return null;

            name = name.Substring("Bloodline".Length);
            int? result = null;
            foreach (var bloodline in bloodlines)
            {
                foreach (var logic in bloodline.SelectComponents<BloodlineLevelLogic>())
                {
                    Log.Write($"match name {name} to bloodline {logic.BloodlineName}");
                    if (name.StartsWith(logic.BloodlineName))
                    {
                        var level = logic.CalcLevel();
                        if (result == null || level > result.Value) result = level;
                    }
                }
            }
            return result;
        }
    }

    [Harmony12.HarmonyPatch(typeof(BindAbilitiesToClass), "GetLevel", typeof(UnitDescriptor))]
    static class BindAbilitiesToClass_GetLevel_Patch
    {
        static bool Prefix(BindAbilitiesToClass __instance)
        {
            ReplaceCasterLevelOfAbility_CalculateClassLevel_Patch.currentFact = __instance.Fact;
            return true;
        }
        static void Postfix() => ReplaceCasterLevelOfAbility_CalculateClassLevel_Patch.currentFact = null;
    }


    [Harmony12.HarmonyPatch(typeof(AddFeatureOnClassLevel), "IsFeatureShouldBeApplied")]
    static class AddFeatureOnClassLevel_GetLevel_Patch
    {
        static bool Prefix(AddFeatureOnClassLevel __instance)
        {
            ReplaceCasterLevelOfAbility_CalculateClassLevel_Patch.currentFact = __instance.Fact;
            return true;
        }
        static void Postfix() => ReplaceCasterLevelOfAbility_CalculateClassLevel_Patch.currentFact = null;
    }

    [Harmony12.HarmonyPatch(typeof(ReplaceCasterLevelOfAbility), "OnEventAboutToTrigger", typeof(RuleCalculateAbilityParams))]
    static class ReplaceCasterLevelOfAbility_OnEventAboutToTrigger_Patch
    {
        static bool Prefix(ReplaceCasterLevelOfAbility __instance)
        {
            ReplaceCasterLevelOfAbility_CalculateClassLevel_Patch.currentFact = __instance.Fact;
            return true;
        }
        static void Postfix() => ReplaceCasterLevelOfAbility_CalculateClassLevel_Patch.currentFact = null;
    }

    [Harmony12.HarmonyPatch(typeof(ReplaceCasterLevelOfAbility), "CalculateClassLevel", typeof(BlueprintCharacterClass), typeof(BlueprintCharacterClass[]), typeof(UnitDescriptor), typeof(BlueprintArchetype[]))]
    static class ReplaceCasterLevelOfAbility_CalculateClassLevel_Patch
    {
        internal static Fact currentFact;

        public static bool Prefix(BlueprintCharacterClass characterClass, BlueprintCharacterClass[] additionalClasses, UnitDescriptor unit, BlueprintArchetype[] archetypeList, ref int __result)
        {
            try
            {
                if (currentFact == null || characterClass != Helpers.sorcererClass) return true;
                var level = unit.Get<UnitPartBloodline>()?.CalcLevel(currentFact.Blueprint);
                if (level.HasValue)
                {
                    Log.Write($"ReplaceCasterLevelOfAbility: modify level of {currentFact.Name}: {level}");
                    __result = level.Value;
                    return false;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return true;
        }
    }

    [Harmony12.HarmonyPatch(typeof(BlueprintAbilityResource), "GetMaxAmount", typeof(UnitDescriptor))]
    static class BlueprintAbilityResource_GetMaxAmount_Patch
    {
        static bool Prefix(BlueprintAbilityResource __instance, UnitDescriptor unit, ref int __result)
        {
            try
            {
                var self = __instance;
                var amount = ExtensionMethods.getMaxAmount(self);
                if ((bool)getIncreasedByLevel(amount))
                {
                    var classes = (BlueprintCharacterClass[])getClass(amount);
                    if (classes?.Contains(Helpers.sorcererClass) == true)
                    {
                        var level = unit.Get<UnitPartBloodline>()?.CalcLevel(self);
                        if (level.HasValue)
                        {
                            int num = (int)getBaseValue(amount) + level.Value * (int)getLevelIncrease(amount);
                            int bonus = 0;
                            EventBus.RaiseEvent(unit.Unit, (IResourceAmountBonusHandler h) => h.CalculateMaxResourceAmount(self, ref bonus));
                            __result = (int)applyMinMax(self, num) + bonus;
                            Log.Write($"BlueprintAbilityResource: modify amount of {self.name}: {__result}");
                            return false;
                        }
                    }
                }
                else if ((bool)getIncreasedByLevelStartPlusDivStep(amount))
                {
                    var classes = (BlueprintCharacterClass[])getClassDiv(amount);
                    if (classes?.Contains(Helpers.sorcererClass) == true)
                    {
                        var level = unit.Get<UnitPartBloodline>()?.CalcLevel(self);
                        if (level.HasValue)
                        {
                            int num = (int)getBaseValue(amount);
                            int totalLevel = level.Value;
                            totalLevel += (int)((unit.Progression.CharacterLevel - totalLevel) * (float)getOtherClassesModifier(amount));
                            if ((int)getStartingLevel(amount) <= totalLevel)
                            {
                                num += Math.Max((int)getStartingIncrease(amount) + (int)getPerStepIncrease(amount) * (totalLevel - (int)getStartingLevel(amount)) / (int)getLevelStep(amount), (int)getMinClassLevelIncrease(amount));
                            }

                            int bonus = 0;
                            EventBus.RaiseEvent(unit.Unit, (IResourceAmountBonusHandler h) => h.CalculateMaxResourceAmount(self, ref bonus));
                            __result = (int)applyMinMax(self, num) + bonus;
                            Log.Write($"BlueprintAbilityResource: modify amount of {self.name} (start plus div step): {__result}");
                            return false;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return true;
        }

        static readonly Type amountType = Harmony12.AccessTools.Inner(typeof(BlueprintAbilityResource), "Amount");
        static readonly FastGetter getIncreasedByLevel = Helpers.CreateFieldGetter(amountType, "IncreasedByLevel");
        static readonly FastGetter getIncreasedByLevelStartPlusDivStep = Helpers.CreateFieldGetter(amountType, "IncreasedByLevelStartPlusDivStep");
        static readonly FastGetter getClass = Helpers.CreateFieldGetter(amountType, "Class");
        static readonly FastGetter getClassDiv = Helpers.CreateFieldGetter(amountType, "ClassDiv");
        static readonly FastGetter getBaseValue = Helpers.CreateFieldGetter(amountType, "BaseValue");
        static readonly FastGetter getLevelIncrease = Helpers.CreateFieldGetter(amountType, "LevelIncrease");
        static readonly FastGetter getOtherClassesModifier = Helpers.CreateFieldGetter(amountType, "OtherClassesModifier");
        static readonly FastGetter getStartingLevel = Helpers.CreateFieldGetter(amountType, "StartingLevel");
        static readonly FastGetter getStartingIncrease = Helpers.CreateFieldGetter(amountType, "StartingIncrease");
        static readonly FastGetter getPerStepIncrease = Helpers.CreateFieldGetter(amountType, "PerStepIncrease");
        static readonly FastGetter getLevelStep = Helpers.CreateFieldGetter(amountType, "LevelStep");
        static readonly FastGetter getMinClassLevelIncrease = Helpers.CreateFieldGetter(amountType, "MinClassLevelIncrease");
        static readonly FastInvoke applyMinMax = Helpers.CreateInvoker<BlueprintAbilityResource>("ApplyMinMax");
    }
}
