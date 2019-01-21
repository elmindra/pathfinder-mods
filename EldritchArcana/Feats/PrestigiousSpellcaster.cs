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
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;

namespace EldritchArcana
{
    class PrestigiousSpellcaster
    {
        internal const String eldritchKnightId = "de52b73972f0ed74c87f8f6a8e20b542";

        internal const String dragonDiscipleId = "72051275b1dbb2d42ba9118237794f7c";

        static BlueprintFeatureSelection prestigiousSpellcaster;

        internal static BlueprintCharacterClass[] prestigiousSpellcasterClasses;

        static LibraryScriptableObject library => Main.library;

        static BlueprintCharacterClass eldritchKnightClass;

        static BlueprintCharacterClass dragonDiscipleClass;

        // Add the Prestigious Spellcaster feat.
        internal static void Load()
        {
            if (prestigiousSpellcasterClasses != null) return;

            dragonDiscipleClass = Helpers.GetClass(dragonDiscipleId);
            eldritchKnightClass = Helpers.GetClass(eldritchKnightId);
            // NOTE: this order must match the order used in the feats.
            prestigiousSpellcasterClasses = new BlueprintCharacterClass[] { eldritchKnightClass, dragonDiscipleClass };

            FixEldritchKnightPrereq();

            // TODO: it would be nice to find the list of spellcasting prestige classes with skipped levels automatically.
            var spellSpecialization = library.Get<BlueprintFeatureSelection>("fe67bc3b04f1cd542b4df6e28b6e0ff5");
            var prestigiousSpell = Helpers.CreateFeatureSelection("PrestigiousSpellcasterSelection", "Prestigious Spellcaster",
                "The transition into a spellcasting prestige class is less difficult for you, and because of this, you gain 1 additional effective spellcaster level from your prestige class levels.",
                "30e9a3fcdb0446aa87f45d0f50b3b3fc",
                spellSpecialization.Icon, FeatureGroup.Feat);
            prestigiousSpell.SetFeatures(
                CreatePrestigiousSpellcaster(eldritchKnightClass, "dc3ab8d0484467a4787979d93114ebc3" /*EldritchKnightSpellbookSelection*/ ),
                CreatePrestigiousSpellcaster(dragonDiscipleClass, "8c1ba14c0b6dcdb439c56341385ee474" /*DragonDiscipleSpellbookSelection*/ ));

            var components = new List<BlueprintComponent>(prestigiousSpellcasterClasses.Select(
                c => Helpers.PrerequisiteClassLevel(c, 1, any: true)));
            components.Add(Helpers.PrerequisiteFeature(FavoredClassBonus.favoredPrestigeClass));
            components.Add(Helpers.Create<PrestigiousSpellcasterRecommendation>());
            prestigiousSpell.SetComponents(components);
            prestigiousSpell.AllFeatures = prestigiousSpell.Features;
            // Log.Write(prestigiousSpell, "", true);

            library.AddFeats(prestigiousSpell);

            ApplySpellbook_Apply_Patch.onApplySpellbook.Add((state, unit, previousCasterLevel) =>
            {
                if (ShouldGainCasterLevelOnLevelUp(state, unit)) IncreaseCasterLevel(state, unit);
            });

            // Set this last, so it won't be set if we failed to initialize.
            prestigiousSpellcaster = prestigiousSpell;
        }

        static void FixEldritchKnightPrereq()
        {
            if (Main.settings?.EldritchKnightFix != true) return;

            // Since Prestigious Spellcaster allows full casting from Eldritch Knight,
            // the class should be properly restricted to require a martial class
            // (e.g. Barbarian, Fighter, Paladin, Ranger). In PnP this is the only way to
            // gain proficiency with all martial weapons.
            //
            // Note: also allow Eldritch Knight itself to qualify, so we don't break
            // existing characters created before this fix.
            var martialClasses = Helpers.classes.Where(
                    c => c.Progression.GetLevelEntry(1).HasFeatureWithId(martialWeaponProfId))
                .ToList();
            martialClasses.Add(eldritchKnightClass);
            var components = eldritchKnightClass.ComponentsArray.ToList();
            foreach (var martialClass in martialClasses)
            {
                // Log.Write($"Adding Eldrtich Knight prereq for martial class {martialClass.name}");
                components.Add(Helpers.PrerequisiteClassLevel(martialClass, 1, any: true));
            }
            eldritchKnightClass.SetComponents(components);
        }

        static BlueprintFeature CreatePrestigiousSpellcaster(BlueprintCharacterClass prestigeClass, String spellbookSelectionId)
        {
            var skipLevels = new List<int>();
            foreach (var level in prestigeClass.Progression.LevelEntries)
            {
                var spellbookFeat = level.Features.FirstOrDefault(
                    s => (s as BlueprintFeatureSelection)?.AllFeatures.Any(
                        f => f is BlueprintFeatureReplaceSpellbook) == true);

                if (spellbookFeat != null)
                {
                    // Move the spellcaster selection earlier so the player can take Prestigious
                    // Spellcaster immediately.
                    //
                    // TODO: this seems to interact weirdly with BloodOfDragonsSelection which has the
                    // NoSelectionIfAlreadyHasFeature. You can click "Next" to bypass it, though.
                    level.Features.Remove(spellbookFeat);
                    prestigeClass.Progression.LevelEntries[0].Features.Insert(0, spellbookFeat);
                    break;
                }
                skipLevels.Add(level.Level);
            }
            // Add skip levels to simplify the algorithm later.
            var skipSpellProgression = prestigeClass.GetComponent<SkipLevelsForSpellProgression>();
            if (skipSpellProgression != null)
            {
                skipLevels.AddRange(skipSpellProgression.Levels);
                skipSpellProgression.Levels = skipLevels.ToArray();
            }
            else
            {
                prestigeClass.AddComponent(Helpers.Create<SkipLevelsForSpellProgression>(s => s.Levels = skipLevels.ToArray()));
            }
            var ranks = skipLevels.Count;
            // Log.Write($"createPrestigiousSpellcaster {prestigeClass.name} skip spells? " + String.Join(" ", skipLevels));
            // Log.Write($"class {prestigeClass.name} skip spell levels {ranks}");

            var favoredClassFeat = FavoredClassBonus.favoredPrestigeClass.Features[Helpers.prestigeClasses.IndexOf(prestigeClass)];
            var prestigiousCaster = Helpers.CreateFeature($"PrestigiousCaster{prestigeClass.name}",
                $"Prestigious Spellcaster — {prestigeClass.Name}",
                prestigeClass.LocalizedDescription,
                Helpers.MergeIds(prestigeClass.AssetGuid, "c526dfc221db493d9ec6291575086a99"),
                prestigeClass.Icon,
                FeatureGroup.Feat,
                favoredClassFeat.PrerequisiteFeature(),
                prestigeClass.PrerequisiteClassLevel(1),
                Helpers.Create<AddSkippedSpellbookLevel>(a => a.PrestigeClass = prestigeClass));

            // TODO: ranked feats do not show up correctly in the character classes page. After the
            // first selection, it shows up as a blank feat choice. We may need to "unroll" these feats
            // to get them to look correct in the UI (or figure out how to patch the UI).
            prestigiousCaster.Ranks = ranks;

            return prestigiousCaster;
        }

        // If we're gaining a level of prestige class that skips a caster level (e.g. level 5 of Dragon Disciple),
        // see if we already took enough Prestigious Spellcaster ranks to make up for it. If so, then we should
        // gain a caster level now.
        static bool ShouldGainCasterLevelOnLevelUp(LevelUpState state, UnitDescriptor unit)
        {
            var selectedClass = state.SelectedClass;
            if (prestigiousSpellcaster == null || !prestigiousSpellcasterClasses.Contains(selectedClass))
            {
                return false;
            }

            // See if we have the matching prestigious spellcaster feat.
            var fact = GetSpellcasterFact(unit, selectedClass);
            if (fact == null) return false;

            // If we already took the Prestigious Spellcaster feat for this skipped caster level, we can gain the level now.
            var spellSkipLevels = selectedClass.GetComponent<SkipLevelsForSpellProgression>().Levels;
            int skipIndex = Array.IndexOf(spellSkipLevels, state.NextClassLevel);
            Log.Write($" checking for skipped spell level, found: {skipIndex}, from skips: " + String.Join(" ", spellSkipLevels));
            if (skipIndex >= 0)
            {
                var rank = fact.GetRank();
                Log.Write($" Prestigious Spellcaster {fact.Blueprint.name} rank {rank}");
                if (rank > skipIndex)
                {
                    // We already took prestigious spellcaster for this level, so we need to gain a caster level.
                    return true;
                }
            }
            return false;
        }

        // TODO: this unfortunately duplicates a lot of ApplySpellbook.Apply, but it's needed because:
        // - Apply checks the skip spell levels internally, so we can't prevent the skip without mutating data structures.
        // - Apply only knows how to add one level, but sometimes we need to gain 2 (if Prestigious Spellcaster fills in
        //   a previous missing level, and another one is gained normally).
        internal static void IncreaseCasterLevel(LevelUpState state, UnitDescriptor unit)
        {
            var blueprintSpellbook = unit.Progression.GetClassData(state.SelectedClass)?.Spellbook;
            Log.Append($"IncreaseCasterLevel {unit.CharacterName}, class {state.SelectedClass}, blueprintSpellbook {blueprintSpellbook}");
            if (blueprintSpellbook == null) return;

            var spellbook = unit.DemandSpellbook(blueprintSpellbook);
            var selectedClassSpellbook = state.SelectedClass.Spellbook;
            if (selectedClassSpellbook != null && selectedClassSpellbook != blueprintSpellbook)
            {
                var oldSpellbook = unit.Spellbooks.FirstOrDefault((s) => s.Blueprint == selectedClassSpellbook);
                if (oldSpellbook != null)
                {
                    foreach (var known in oldSpellbook.GetAllKnownSpells())
                    {
                        spellbook.AddKnown(known.SpellLevel, known.Blueprint);
                    }
                    unit.DeleteSpellbook(selectedClassSpellbook);
                }
            }
            int oldCasterLevel = spellbook.CasterLevel;
            spellbook.AddCasterLevel();
            int newCasterLevel = spellbook.CasterLevel;
            Log.Write($"Level up from {oldCasterLevel} to {newCasterLevel}");
            var spellSelection = state.DemandSpellSelection(spellbook.Blueprint, spellbook.Blueprint.SpellList);
            var spellsKnown = spellbook.Blueprint.SpellsKnown;
            if (spellsKnown != null)
            {
                for (int i = 0; i <= 9; i++)
                {
                    int? oldSpellsKnown = spellsKnown.GetCount(oldCasterLevel, i);
                    int? newSpellsKnown = spellsKnown.GetCount(newCasterLevel, i);
                    var newSpells = newSpellsKnown.GetValueOrDefault() - oldSpellsKnown.GetValueOrDefault();
                    // Log.Write($"  gain spells {newSpells} at level {i}");
                    int existingNewSpells = spellSelection.LevelCount[i]?.SpellSelections.Length ?? 0;
                    spellSelection.SetLevelSpells(i, newSpells + existingNewSpells);
                }
            }
            int maxSpellLevel = spellbook.MaxSpellLevel;
            int spellsPerLevel = spellbook.Blueprint.SpellsPerLevel;
            if (spellsPerLevel > 0)
            {
                if (oldCasterLevel == 0)
                {
                    spellSelection.SetExtraSpells(0, maxSpellLevel);
                    spellSelection.ExtraByStat = true;
                    spellSelection.UpdateMaxLevelSpells(unit);
                }
                else
                {
                    spellSelection.ExtraMaxLevel = maxSpellLevel;
                    var existingExtra = spellSelection.ExtraSelected?.Length ?? 0;
                    spellSelection.ExtraSelected = new BlueprintAbility[spellsPerLevel + existingExtra];
                }
            }
            foreach (var customSpells in spellbook.Blueprint.GetComponents<AddCustomSpells>())
            {
                if (customSpells.CasterLevel == newCasterLevel)
                {
                    var customSelection = state.DemandSpellSelection(spellbook.Blueprint, customSpells.SpellList);
                    customSelection.SetExtraSpells(customSpells.Count, customSpells.MaxSpellLevel);
                }
            }
            ReplaceSpells.HandleUpdateCasterLevel(unit, state, spellbook);
        }

        internal static Fact GetSpellcasterFact(UnitDescriptor unit, BlueprintCharacterClass selectedClass)
        {
            var blueprint = prestigiousSpellcaster.AllFeatures[Array.IndexOf(prestigiousSpellcasterClasses, selectedClass)];
            return unit.Logic.GetFact(blueprint) ?? unit.Progression.Features.GetFact(blueprint);
        }

        internal const String martialWeaponProfId = "203992ef5b35c864390b4e4a1e200629";
    }

    // This intercepts ApplySpellbook.Apply, so we can update the spellbook whenever
    // Prestigious Spellcaster causes us to gain a casting level that otherwise would
    // not have been gained.
    //
    // I don't think there is any way around this, as there is no level-up event that
    // necessarily fires when ApplySpellbook.Apply and prestige class spellbook selection
    // happen. So we can't reliably handle taking Prestigious Spellcaster in advance, and
    // have it increase the spells known ofr that class.
    [Harmony12.HarmonyPatch(typeof(ApplySpellbook), "Apply", new Type[] { typeof(LevelUpState), typeof(UnitDescriptor) })]
    static class ApplySpellbook_Apply_Patch
    {
        internal static readonly List<Action<LevelUpState, UnitDescriptor, int>> onApplySpellbook = new List<Action<LevelUpState, UnitDescriptor, int>>();

        static int previousCasterLevel;

        static ApplySpellbook_Apply_Patch() => Main.ApplyPatch(typeof(ApplySpellbook_Apply_Patch), "Prestigious Spellcaster, spell replacement for spontaneous casters");

        static bool Prefix(ApplySpellbook __instance, LevelUpState state, UnitDescriptor unit)
        {
            try
            {
                previousCasterLevel = 0;
                // Note: we need to get the spellbook from class data, because spellbooks can get
                // replaced. For example, we took a Sorcerer level but the spellbook is actually
                // EmpyrealSorcerer's spellbook. Similar for prestige classes (e.g. we took a level
                // of Eldritch Knight, but the spellbook is actually the Wizard spellbook). 
                var spellbook = unit.GetSpellbook(state.SelectedClass);
                if (spellbook != null) previousCasterLevel = spellbook.CasterLevel;
            }
            catch (Exception e)
            {
                Log.Error($"Error in ApplySpellbook.Apply (prefix): {e}");
            }
            return true;
        }

        static void Postfix(ApplySpellbook __instance, LevelUpState state, UnitDescriptor unit)
        {
            try
            {
                foreach (var action in onApplySpellbook)
                {
                    action(state, unit, previousCasterLevel);
                }
            }
            catch (Exception e)
            {
                Log.Error($"Error in ApplySpellbook.Apply (postfix): {e}");
            }
        }
    }

    public class AddSkippedSpellbookLevel : ComponentAppliedOnceOnLevelUp
    {
        public BlueprintCharacterClass PrestigeClass;

        protected override void Apply(LevelUpState state)
        {
            Log.Append($"{GetType().Name}.Apply() prestige class {PrestigeClass.name}");
            if (ShouldGainCasterLevelOnFeatSelection(state, PrestigeClass, Owner, Fact.GetRank(), true))
            {
                PrestigiousSpellcaster.IncreaseCasterLevel(state, Owner);
            }
        }

        // If we just took Prestigious Spellcaster, see if there was a skipped caster level
        // (possibly the current level) that we can now make up for.
        //
        // Note that this can result in gaining two caster levels in a single level-up
        // (if we're making up for a previous level, and gaining one normally this round).
        // However the "normal" level was already added by ApplySpellbook.Apply, so we only
        // need to handle the additional caster level granted by Prestigious Spellcaster.
        internal static bool ShouldGainCasterLevelOnFeatSelection(LevelUpState state, BlueprintCharacterClass prestigeClass, UnitDescriptor owner, int ranks, bool log)
        {
            // This feat could be chosen when we gain a level in another class, it should work either way.
            var prestigeClassLevel = prestigeClass == state.SelectedClass ? state.NextClassLevel : owner.Progression.GetClassLevel(prestigeClass);

            // Are we behind in spell levels?
            int levelsBehind = 0;
            var spellSkipLevels = prestigeClass.GetComponent<SkipLevelsForSpellProgression>().Levels;
            foreach (var skip in spellSkipLevels)
            {
                if (skip > prestigeClassLevel) break;
                levelsBehind++;
            }

            if (log)
            {
                Log.Append($"  AddSkippedCasterLevels: {prestigeClass} level {prestigeClassLevel}");
                Log.Write($"  took Prestigious Spellcaster {ranks} times, behind {levelsBehind}");
            }

            return levelsBehind >= ranks;
        }
    }

    public class PrestigiousSpellcasterRecommendation : LevelUpRecommendationComponent
    {
        public override RecommendationPriority GetPriority(LevelUpState state)
        {
            var unit = state.Unit;
            foreach (var prestigeClass in PrestigiousSpellcaster.prestigiousSpellcasterClasses)
            {
                int level = unit.Progression.GetClassLevel(prestigeClass);
                if (level == 0) continue;

                // See if we have the matching prestigious spellcaster feat.
                var fact = PrestigiousSpellcaster.GetSpellcasterFact(unit, prestigeClass);
                var rank = fact?.GetRank() ?? 0;

                // If we can gain a caster level, recommend this feat.
                if (AddSkippedSpellbookLevel.ShouldGainCasterLevelOnFeatSelection(state, prestigeClass, unit, rank + 1, false))
                {
                    return RecommendationPriority.Good;
                }
            }
            return RecommendationPriority.Same;
        }
    }
}
