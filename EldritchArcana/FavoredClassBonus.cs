// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EldritchArcana
{
    internal static class FavoredClassBonus
    {
        private static LibraryScriptableObject library => Main.library;

        internal static BlueprintFeatureSelection favoredPrestigeClass;
        private static BlueprintFeature alertnessFeat;
        private static BlueprintRace[] extraSpellRaces;

        private static Dictionary<StatType, BlueprintFeature> StatBonusTenRanksByStatType = new Dictionary<StatType, BlueprintFeature>();

        internal static void Load()
        {
            bonusHitPointFeat = Helpers.CreateFeature("FavoredClassBonusHitPoint",
                "Bonus Hit Point", "Gain +1 hit point",
                "33642179f90c4452a3122892ebc81692",
                Helpers.GetIcon("d09b20029e9abfe4480b356c92095623"), // toughness
                FeatureGroup.None,
                Helpers.Create<AddHitPointOnce>());
            bonusHitPointFeat.Ranks = 20;

            bonusSkillRankFeat = Helpers.CreateFeature("FavoredClassBonusSkillRank",
                "Bonus Skill Rank", "Gain +1 skill rank",
                "e0ede31846a0499d854391302f039ede",
                Helpers.GetIcon("3adf9274a210b164cb68f472dc1e4544"), // human skilled
                FeatureGroup.None,
                Helpers.Create<AddSkillRankOnce>());
            bonusSkillRankFeat.Ranks = 20;

            StatBonusTenRanksByStatType.Add(StatType.SkillAthletics, CreateStatOnRankBonuses("1c7c4b10f6d84dafb144df0a15ce6934", StatType.SkillAthletics));
            StatBonusTenRanksByStatType.Add(StatType.SkillMobility, CreateStatOnRankBonuses("c46f9346511b4093b2ea42f489138415", StatType.SkillMobility));
            StatBonusTenRanksByStatType.Add(StatType.SkillThievery, CreateStatOnRankBonuses("28d63316183144a893ecc3150286c253", StatType.SkillThievery));
            StatBonusTenRanksByStatType.Add(StatType.SkillStealth, CreateStatOnRankBonuses("46a57c5a615c4dde8f3becfde8d72e9d", StatType.SkillStealth));
            StatBonusTenRanksByStatType.Add(StatType.SkillKnowledgeArcana, CreateStatOnRankBonuses("c8b84115288447d6865d4705ca71aaba", StatType.SkillKnowledgeArcana));
            StatBonusTenRanksByStatType.Add(StatType.SkillKnowledgeWorld, CreateStatOnRankBonuses("7019078674b64f989e821de9d6acd58f", StatType.SkillKnowledgeWorld));
            StatBonusTenRanksByStatType.Add(StatType.SkillLoreNature, CreateStatOnRankBonuses("b6e43a0c81c844ac8ff1cee5f7aec195", StatType.SkillLoreNature));
            StatBonusTenRanksByStatType.Add(StatType.SkillLoreReligion, CreateStatOnRankBonuses("541f7713a3714631839f797a2caa320b", StatType.SkillLoreReligion));
            StatBonusTenRanksByStatType.Add(StatType.SkillPerception, CreateStatOnRankBonuses("a3491805d3a641ea94e5b0b6bcb5462e", StatType.SkillPerception));
            StatBonusTenRanksByStatType.Add(StatType.SkillPersuasion, CreateStatOnRankBonuses("3639dcca179f454b93e0c441d6d9e42b", StatType.SkillPersuasion));
            StatBonusTenRanksByStatType.Add(StatType.SkillUseMagicDevice, CreateStatOnRankBonuses("57dd61d05d3a47d894187eb673f9dbc0", StatType.SkillUseMagicDevice));


            LoadFavoredClass();
            LoadFavoredPrestigeClass();
            LoadDeitySelection();
        }

        private static void LoadFavoredPrestigeClass()
        {
            alertnessFeat = library.Get<BlueprintFeature>("1c04fe9a13a22bc499ffac03e6f79153");

            PrerequisiteNoFeature noFeature = Helpers.Create<PrerequisiteNoFeature>();
            favoredPrestigeClass = Helpers.CreateFeatureSelection(
                "FavoredPresitgeClassSelection",
                "Favored Prestige Class",
                "You have come to favor a certain prestige class, either because you are particularly devoted to the class’s cause, have trained more than most others have for that specific role, or have simply been destined to excel in the prestige class all along. Regardless of the reason, levels gained in your favored prestige class grant additional benefits in a way similar to those you gain for taking levels in your base favored class.\n" +
                "You can select this feat before you gain levels in your chosen favored prestige class, but the benefits of the feat do not apply until you actually gain at least 1 level in that prestige class.",
                "4fab2e6256e644daaa637093bc2421aa",
                Helpers.skillFocusFeat.Icon,
                FeatureGroup.Feat,
                noFeature,
                Helpers.Create<LevelUpRecommendation>(l =>
                {
                    // Mark this feat recommended if a prestige class is taken.
                    l.ClassPriorities = new ClassesPriority[] {
                        new ClassesPriority() {
                            Classes = Helpers.prestigeClasses.ToArray(),
                            Priority = RecommendationPriority.Good
                        }
                    };
                }));
            // Note: feat order should match Helpers.prestigeClasses so Prestigious Spellcaster can index into this.
            favoredPrestigeClass.SetFeatures(Helpers.prestigeClasses.Select(CreateFavoredPrestigeClass));
            noFeature.Feature = favoredPrestigeClass;

            library.AddFeats(favoredPrestigeClass);
        }

        private static BlueprintProgression CreateFavoredPrestigeClass(BlueprintCharacterClass prestigeClass)
        {

            // Create the progression that will allow +1 HP or skill rank.
            BlueprintProgression progression = Helpers.CreateProgression(
                $"FavoredPrestige{prestigeClass.name}",
                $"Favored Prestige Class — {prestigeClass.Name}",
                prestigeClass.LocalizedDescription,
                Helpers.MergeIds(prestigeClass.AssetGuid, "989807536776445d9b4875b4cfbfdd11"),
                prestigeClass.Icon,
                FeatureGroup.Feat);

            // Populate the progression so the HP or Skill choice is offered each level.
            FillFavoredClassProgression(progression, prestigeClass);

            return progression;
        }

        // Creates the progression that offers a choice of HP, Skill Rank, or other choices each level up.
        private static void FillFavoredClassProgression(BlueprintProgression favored, BlueprintCharacterClass favoredClass, List<BlueprintFeature> extraChoices = null)
        {
            bool isPrestige = favoredClass.PrestigeClass;

            favored.Classes = new BlueprintCharacterClass[] { favoredClass };
            favored.ExclusiveProgression = favoredClass;

            FeatureGroup group = UpdateLevelUpDeterminatorText.Group;
            if (isPrestige)
            {
                group = FeatureGroup.Feat;
            }

            BlueprintFeatureSelection selection = Helpers.CreateFeatureSelection(
                favored.name + "BonusSelection",
                favored.Name,
                favored.Description,
                Helpers.MergeIds(favored.AssetGuid, "5b99b7d724e048c08b384dd890826"),
                favoredClass.Icon,
                group);

            List<BlueprintFeature> choices = new List<BlueprintFeature> { bonusHitPointFeat, bonusSkillRankFeat };
            if(extraChoices != null) choices.AddRange(extraChoices);
            selection.SetFeatures(choices);

            List<LevelEntry> entries = new List<LevelEntry>();
            int maxLevel = isPrestige ? 10 : 20;
            for(int level = 1; level <= maxLevel; level++)
            {
                entries.Add(Helpers.LevelEntry(level, selection));
            }

            if(isPrestige)
            {
                // Create the skill selection feature that offers +2 bonus to class skill
                // (+4 bonus with 10 ranks invested).
                BlueprintFeatureSelection paramSkill = Helpers.CreateFeatureSelection(
                    favored.name + "SkillBonus",
                    favored.Name,
                    favored.Description + "\nYou gain a +2 bonus on checks using the skill you chose from that prestige class’s class skills. If you have 10 or more ranks in one of these skills, the bonus increases to +4 for that skill. This bonus stacks with the bonus granted by Skill Focus, but does not stack with a bonus granted by any other feat (such as Magical Aptitude or Persuasive).",
                    Helpers.MergeIds(favoredClass.AssetGuid, "15faccea8a364cb39d091dd01b513c3a"),
                    Helpers.skillFocusFeat.Icon,
                    FeatureGroup.None,
                    Helpers.Create<AddParameterizedStatBonus>(a => a.Descriptor = ModifierDescriptor.Feat));
                List<BlueprintFeature> classSkillFeatures = new List<BlueprintFeature>();
                foreach(StatType favoredClassClassSkill in favoredClass.ClassSkills)
                {
                    classSkillFeatures.Add(StatBonusTenRanksByStatType.Get(favoredClassClassSkill));
                }


                paramSkill.SetFeatures(classSkillFeatures);
                entries[0].Features.Add(paramSkill);
            }

            favored.LevelEntries = entries.ToArray();
        }

        private static BlueprintFeature CreateStatOnRankBonuses(string GUID, StatType statType)
        {
            return Helpers.CreateFeature(
                "SkillBonus" + statType,
                "Favored Prestige Class Skill Bonus (" + statType + ")",
                "You gain a +2 bonus on checks using the skill you chose from that prestige class’s class skills. If you have 10 or more ranks in one of these skills, the bonus increases to +4 for that skill. This bonus stacks with the bonus granted by Skill Focus, but does not stack with a bonus granted by any other feat (such as Magical Aptitude or Persuasive).",
                GUID,
                Helpers.skillFocusFeat.Icon,
                FeatureGroup.None,
                Helpers.Create<AddStatBonusBasedOnStatRanks>(x =>
                {
                    x.StatType = statType;
                    x.BaseBonus = 2;
                    x.IncreaseOnRank = 10;
                    x.IncreaseOnRankBonus = 2;
                }));
        }

        private static void LoadFavoredClass()
        {
            // Note: the favored class choice has no components for behavior, because the logic is implemented by
            // AddFavoredClassBonusChoice on level up. 
            BlueprintFeature favoredClassAny = Helpers.CreateFeature(
                "FavoredClassAny",
                "Favored Class — Any",
                "The favored class is automatically determined each level-up, and an extra hit point is awarded if gaining a level in that class. The favored class is your highest level non-prestige class. This is the default game behavior.",
                "ea5f395c351a4f00be7f7a300d3bb5b4",
                null,
                FeatureGroup.Feat);

            List<BlueprintFeature> choices = new List<BlueprintFeature> { favoredClassAny };

            // Per FAQ, half-elf and half-orcs can qualify for human favored class bonuses (or elf/orc ones, respectively).
            // In the game, Aasimar appear to be treated as having "Scion of Humanity" (they don't have Darkvision, but
            // spells that affect humanoids work on them), so they should also qualify.
            // TODO: Tiefling should qualify too.
            extraSpellRaces = new BlueprintRace[] { Helpers.halfElf, Helpers.halfOrc, Helpers.human, Helpers.aasimar, Helpers.tiefling };

            foreach(BlueprintCharacterClass characterClass in Helpers.classes)
            {
                if(characterClass.PrestigeClass) continue;
                choices.Add(CreateFavoredClassProgression(characterClass));
            }

            EventBus.Subscribe(new UpdateLevelUpDeterminatorText());

            PrerequisiteNoFeature noFeature = Helpers.PrerequisiteNoFeature(null);
            BlueprintFeatureSelection favoredClass = Helpers.CreateFeatureSelection(
                "FavoredClass",
                "Favored Class",
                "Each character begins play with a single favored class of their choosing—typically, this is the same class as the one they choose at 1st level. " +
                "Whenever a character gains a level in their favored class, they receive either +1 hit point, +1 skill rank, or the racial bonus associated with their favored class. " +
                "The choice of favored class cannot be changed once the character is created.",
                "bc4c271ef0954eceb808d84978c500f7",
                null,
                UpdateLevelUpDeterminatorText.Group,
                noFeature);
            noFeature.Feature = favoredClass;
            favoredClass.SetFeatures(choices);
            ApplyClassMechanics_Apply_Patch.onChargenApply.Add((state, unit) =>
            {
                favoredClass.AddSelection(state, unit, 1);
            });
        }

        private static BlueprintProgression CreateFavoredClassProgression(BlueprintCharacterClass favoredClass)
        {
            string className = favoredClass.Name.ToLower();
            // TODO: implement other classes/races favored class benefits.
            string description = $"Whenever you gain a {className} level, you can choose between +1 hit point, +1 skill rank, or the racial bonus associated with their favored class.";

            bool isSorcerer = favoredClass.AssetGuid == "b3a505fb61437dc4097f43c3f8f9a4cf";
            bool isBard = favoredClass.AssetGuid == "772c83a25e2268e448e841dcd548235f";
            bool isOracle = favoredClass == OracleClass.oracle;
            bool isExtraSpellClass = isSorcerer || isBard || isOracle;
            if(isExtraSpellClass)
            {
                description += "\nRacial favored class benefits:" +
                    $"\n  Human (and Half-Elf, Half-Orc, Aasimar) — Add one spell known from the {className} spell list. This spell must be at least one level below the highest {className} spell you can cast.";
            }

            BlueprintProgression favored = Helpers.CreateProgression(
                $"FavoredClass{favoredClass.name}Progression",
                $"Favored Class — {favoredClass.Name}",
                description,
                Helpers.MergeIds(favoredClass.AssetGuid, "081651146ada4d0a88f6e9190ac6b01a"),
                favoredClass.Icon,
                FeatureGroup.Feat,
                Helpers.Create<DisableAutomaticFavoredClassHitPoints>());

            List<BlueprintFeature> choices = new List<BlueprintFeature>();
            if(isExtraSpellClass)
            {
                for(int level = 1; level <= 8; level++)
                {
                    choices.Add(CreateExtraSpellChoice(favoredClass, level));
                }
            }

            FavoredClassBonus.FillFavoredClassProgression(favored, favoredClass, choices);
            return favored;
        }

        private static BlueprintFeature CreateExtraSpellChoice(BlueprintCharacterClass @class, int spellLevel)
        {
            string className = @class.Name.ToLower();

            List<BlueprintComponent> components = new List<BlueprintComponent>();
            components.Add(Helpers.PrerequisiteFeaturesFromList(extraSpellRaces));
            components.Add(PrerequisiteCasterSpellLevel.Create(@class, spellLevel + 1));
            components.Add(Helpers.Create<AddOneSpellChoice>(a => { a.CharacterClass = @class; a.SpellLevel = spellLevel; }));

            BlueprintFeature feat = Helpers.CreateFeature($"Favored{@class.name}BonusSpellLevel{spellLevel}",
                $"Bonus Known Spell (Level {spellLevel})",
                $"Add one level {spellLevel} spell known from the {className} spell list. This spell must be at least one level below the highest {className} spell you can cast.",
                Helpers.MergeIds(@class.AssetGuid, spellLevelGuids[spellLevel - 1]),
                Helpers.GetIcon("55edf82380a1c8540af6c6037d34f322"), // elven magic
                FeatureGroup.None,
                components.ToArray());
            feat.Ranks = 20;
            return feat;
        }

        private static void LoadDeitySelection()
        {
            // For RP flavor, this adds an optional deity/atheism selection to the character creation page.
            BlueprintFeatureSelection baseDeitySelection = library.Get<BlueprintFeatureSelection>("59e7a76987fe3b547b9cce045f4db3e4");
            BlueprintFeature atheismFeature = library.Get<BlueprintFeature>("92c0d2da0a836ce418a267093c09ca54");

            // Classes tagged with "no atheism" can't select it on the Deity selection.
            // (in practice, most of them wouldn't get the option, since they have their own deity selection feature.
            // This is mainly for Oracles, but it also serves as nice documentation.)
            atheismFeature.AddComponents(
                Helpers.classes.Where(c => c.GetComponents<PrerequisiteNoFeature>().Any(p => p.Feature == atheismFeature))
                    .Select(c => Helpers.Create<PrerequisiteNoClassLevel>(p => p.CharacterClass = c)));

            BlueprintFeatureSelection deitySelection = library.CopyAndAdd(baseDeitySelection, "DeitySelectionAny", "d5c3c9d4080043f98e6c09f4e843440e");
            deitySelection.Group = FeatureGroup.None; // to prevent "determinators" page clutter.
            BlueprintFeature noDeityChoice = Helpers.CreateFeature("SkipDeity", "(Skip)",
                "Choose this to skip selecting a deity at character creation. You may select one later if you gain a level in a class that requires it (such as Cleric, Inquisitor, or Paladin).",
                "e1f5711210404b34a805b00749eeba20",
                null, FeatureGroup.None);
            noDeityChoice.HideInUI = true;

            List<BlueprintFeature> choices = new List<BlueprintFeature> { noDeityChoice };
            choices.AddRange(baseDeitySelection.AllFeatures);
            choices.Add(atheismFeature);
            deitySelection.SetFeatures(choices);

            BlueprintFeatureSelection paladinDeitySelection = library.Get<BlueprintFeatureSelection>("a7c8b73528d34c2479b4bd638503da1d");
            ApplyClassMechanics_Apply_Patch.onChargenApply.Add((state, unit) =>
            {
                if(!state.Selections.Any(s => s.Selection.GetGroup() == FeatureGroup.Deities ||
                   (object)s.Selection == paladinDeitySelection))
                {
                    deitySelection.AddSelection(state, unit, 1);
                }
            });
        }

        private static BlueprintFeature bonusHitPointFeat, bonusSkillRankFeat;

        internal static readonly string[] spellLevelGuids = new string[] {
            "1541c1ef94e24659b1120cf18792094a",
            "5c570cda113846ea86b800d64a90c2d5",
            "2eae028571d54067949a046773069e2b",
            "7c0dcae4a4684ea883f8907bfcad3caa",
            "bc28b1e441634077b16616f19e23a2fe",
            "41273452987d45d5a979cd714cefffaf",
            "33973c98b8bf4ccea6f86ee91a83fc47",
            "4ca9c712af684d609b5e168b5ad4eec1",
            "15794bead9f4417796c77667fdba1068"
        };
    }

    // The game function `CharacterBuildController.DefineAvailibleData` [sic] is hard coded
    // to only allow certain FeatureGroup values to use the `Determinators` selection page
    // (choices that are chosen before the skills/stats).
    //
    // It's not easy to fix that method, so instead we reuse Channel Energy.
    // The text for it is updated to reflect what it actually means.
    internal class UpdateLevelUpDeterminatorText : ILevelUpSelectClassHandler, ILevelUpCompleteUIHandler
    {
        public static FeatureGroup Group = FeatureGroup.ChannelEnergy;
        private const string ChannelEnergyId = "d332c1748445e8f4f9e92763123e31bd";
        private readonly LocalizedString SavedText, SavedChoiceText;
        private readonly LocalizedString FavoredClass, ChooseFavoredClass;
        private readonly LocalizedString BonusText, ChooseBonusText;
        private readonly LocalizedString MysteryText, ChooseMysteryText;
        private readonly UICharGen CharGenText;

        public UpdateLevelUpDeterminatorText()
        {
            CharGenText = Game.Instance.BlueprintRoot.LocalizedTexts.UserInterfacesText.CharGen;
            SavedText = CharGenText.ChannelEnergy;
            SavedChoiceText = CharGenText.ChooseChannelEnergy;
            MysteryText = Helpers.CreateString("CharGen.Mystery", "Mystery");
            ChooseMysteryText = Helpers.CreateString("CharGen.ChooseMystery", "Choose Mystery");
            FavoredClass = Helpers.CreateString("CharGen.FavoredClass", "Favored Class");
            ChooseFavoredClass = Helpers.CreateString("CharGen.ChooseFavoredClass", "Choose Favored Class");
            BonusText = Helpers.CreateString("CharGen.FavoredClassBonus", "Favored Class");
            ChooseBonusText = Helpers.CreateString("CharGen.ChooseFavoredClassBonus", "Choose Favored Class Bonus");
        }

        void ILevelUpSelectClassHandler.HandleSelectClass(UnitDescriptor unit, LevelUpState state)
        {
            LevelEntry[] entries = state.SelectedClass.Progression.LevelEntries;
            bool isOracle = state.SelectedClass == OracleClass.oracle;
            bool hasChannelEnergy = entries.Any(l => l.Level == state.NextClassLevel &&
                l.Features.Any(f => f.AssetGuid == ChannelEnergyId));

            if(isOracle && state.NextClassLevel == 1)
            {
                SetText(MysteryText, ChooseMysteryText);
            }
            else if(hasChannelEnergy)
            {
                SetText(SavedText, SavedChoiceText);
            }
            else if(state.NextLevel == 1)
            {
                SetText(FavoredClass, ChooseFavoredClass);
            }
            else
            {
                SetText(BonusText, ChooseBonusText);
            }
        }

        void ILevelUpCompleteUIHandler.HandleLevelUpComplete(UnitEntityData unit, bool isChargen)
        {
            SetText(SavedText, SavedChoiceText);
        }

        private void SetText(LocalizedString text, LocalizedString choiceText)
        {
            CharGenText.ChannelEnergy = text;
            CharGenText.ChooseChannelEnergy = choiceText;
        }

    }

    public abstract class ComponentAppliedOnceOnLevelUp : OwnedGameLogicComponent<UnitDescriptor>, ILevelUpCompleteUIHandler
    {
        [JsonProperty]
        private int appliedRank;

        public override void OnFactActivate()
        {
            try
            {
                Log.Write($"{GetType()}.OnFactActivate(), applied rank? {appliedRank}");
                int rank = Fact.GetRank();
                if(appliedRank >= rank) return;

                // If we're in the level-up UI, apply the component
                LevelUpController levelUp = Game.Instance.UI.CharacterBuildController.LevelUpController;
                if(Owner == levelUp.Preview || Owner == levelUp.Unit)
                {
                    for(; appliedRank < rank; appliedRank++)
                    {
                        Apply(levelUp.State);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        // Optionally remove this fact to free some memory; useful if the fact is already applied
        // and there is no reason to track its overall rank.
        protected virtual bool RemoveAfterLevelUp => false;

        public void HandleLevelUpComplete(UnitEntityData unit, bool isChargen)
        {
            if(RemoveAfterLevelUp && unit.Descriptor == Owner)
            {
                Log.Write($"Removing fact {Fact.Blueprint.AssetGuid}");
                Owner.RemoveFact(Fact);
            }
        }

        protected abstract void Apply(LevelUpState state);
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    public class AddOneSpellChoice : ComponentAppliedOnceOnLevelUp
    {
        public BlueprintCharacterClass CharacterClass;
        public int SpellLevel;

        protected override void Apply(LevelUpState state)
        {
            if(CharacterClass != state.SelectedClass) return;

            Kingmaker.Blueprints.Classes.Spells.BlueprintSpellbook spellbook = Owner.Progression.GetClassData(CharacterClass).Spellbook;
            SpellSelectionData spellSelection = state.DemandSpellSelection(spellbook, spellbook.SpellList);
            int existingNewSpells = spellSelection.LevelCount[SpellLevel]?.SpellSelections.Length ?? 0;

            Log.Write($"Adding spell selection to level {SpellLevel}");
            spellSelection.SetLevelSpells(SpellLevel, 1 + existingNewSpells);
        }
    }


    [AllowedOn(typeof(BlueprintUnitFact))]
    public class AddHitPointOnce : ComponentAppliedOnceOnLevelUp
    {
        protected override void Apply(LevelUpState state)
        {
            Log.Write(GetType().Name + $" {state.SelectedClass}");
            Owner.Stats.HitPoints.BaseValue++;
        }
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    public class AddSkillRankOnce : ComponentAppliedOnceOnLevelUp
    {
        protected override void Apply(LevelUpState state)
        {
            Log.Write(GetType().Name + $" {state.SelectedClass}");
            state.ExtraSkillPoints++;
        }
    }

    public abstract class CustomPrerequisite : Prerequisite
    {
        public abstract string GetCaption();

        public override string GetUIText()
        {
            return GetCaption();
        }
    }

    [AllowMultipleComponents]
    public class PrerequisiteCasterSpellLevel : CustomPrerequisite
    {
        public BlueprintCharacterClass CharacterClass;

        public int RequiredSpellLevel;

        public static PrerequisiteCasterSpellLevel Create(BlueprintCharacterClass @class, int spellLevel)
        {
            PrerequisiteCasterSpellLevel p = Helpers.Create<PrerequisiteCasterSpellLevel>();
            p.CharacterClass = @class;
            p.RequiredSpellLevel = spellLevel;
            return p;
        }

        public override bool Check(FeatureSelectionState selectionState, UnitDescriptor unit, LevelUpState state)
        {
            return unit.GetSpellbook(CharacterClass)?.MaxSpellLevel >= RequiredSpellLevel;
        }

        public override string GetCaption()
        {
            return $"Can cast {CharacterClass.Name} spells of level: {RequiredSpellLevel}";
        }
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    public class DisableAutomaticFavoredClassHitPoints : OwnedGameLogicComponent<UnitDescriptor>, ILevelUpSelectClassHandler
    {
        public void HandleSelectClass(UnitDescriptor unit, LevelUpState state)
        {
            if(Owner == unit)
            {
                // handle subsequent level ups after level 1
                Apply(state);
            }
        }

        public override void OnFactActivate()
        {
            // Note: this is different from the other favored class bonus components,
            // because the feature remains on the character, and kicks in at each level up.
            LevelUpController levelUp = Game.Instance.UI.CharacterBuildController.LevelUpController;
            if(levelUp.State.NextLevel == 1 && (Owner == levelUp.Preview || Owner == levelUp.Unit))
            {
                // Handle the level 1 hit point adjustment in the character generator.
                Apply(levelUp.State);
            }
        }

        private void Apply(LevelUpState state)
        {
            // If a user-selectable favored class was chosen, then we need to disable the game's automatic favored class hit points.
            // TODO: could use a patch to skip ApplyClassMechanics.ApplyHitPoints instead of undoing it.
            BlueprintCharacterClass @class = state.SelectedClass;
            int nextLevel = state.NextLevel;
            BlueprintCharacterClass[] classes = BlueprintRoot.Instance.Progression.CharacterClasses;
            // This calculation was taken from ApplyClassMechanics.ApplyHitPoints.
            // All we want to do here is undo that function, so we replicate the logic as accurately as possible.
            //
            // Note: there is a bug in the base game (seen here): if a prestige class is higher level than a base class,
            // it won't allow the base class to get its favored class bonus.
            bool isFavoredClass = !@class.PrestigeClass && classes.Contains(@class) && !Owner.Progression.Classes.Any(c => c.Level >= nextLevel && c.CharacterClass != @class);

            if(isFavoredClass)
            {
                Log.Write($"Remove auto favored class hit point {state.SelectedClass}");
                Owner.Stats.HitPoints.BaseValue--;
            }
        }
    }

    public class CustomSkillSelection : BlueprintParametrizedFeature, IFeatureSelection
    {
        public StatType[] Skills;

        public CustomSkillSelection()
        {
            ParameterType = FeatureParameterType.Skill;
        }

        IEnumerable<IFeatureSelectionItem> IFeatureSelection.ExtractSelectionItems(UnitDescriptor beforeLevelUpUnit, UnitDescriptor previewUnit)
        {
            try
            {
                return base.ExtractSelectionItems(beforeLevelUpUnit, previewUnit)
                    .Where(f => Skills.Contains(f.Param.Value.StatType.Value));
            }
            catch(Exception e)
            {
                Log.Error(e);
                return Array.Empty<IFeatureSelectionItem>();
            }
        }
    }

    [AllowedOn(typeof(BlueprintFeature))]
    [AllowedOn(typeof(BlueprintBuff))]
    [AllowMultipleComponents]
    public class AddParameterizedStatBonus : ParametrizedFeatureComponent
    {
        public ModifierDescriptor Descriptor;

        [JsonProperty]
        private ModifiableValue.Modifier m_Modifier;

        public override void OnTurnOn()
        {
            StatType statParam = Param.StatType.GetValueOrDefault();
            if(statParam == StatType.Unknown) return;
            ModifiableValue stat = Owner.Stats.GetStat(statParam);

            int bonus = stat.BaseValue >= 10 ? 4 : 2;
            m_Modifier = stat.AddModifier(bonus, this, Descriptor);
        }

        public override void OnTurnOff()
        {
            m_Modifier?.Remove();
            m_Modifier = null;
        }
    }

    [AllowedOn(typeof(BlueprintFeature))]
    [AllowedOn(typeof(BlueprintBuff))]
    [AllowMultipleComponents]
    public class AddStatBonusBasedOnStatRanks : OwnedGameLogicComponent<UnitDescriptor>
    {
        public ModifierDescriptor Descriptor = ModifierDescriptor.Other;
        public StatType StatType;
        public int BaseBonus;
        public int IncreaseOnRank;
        public int IncreaseOnRankBonus;


        [JsonProperty]
        private ModifiableValue.Modifier m_Modifier;

        public override void OnTurnOn()
        {
            OnTurnOff();
            var stat = base.Owner.Stats.GetStat(StatType);
            int bonusValue = BaseBonus;
            if(stat.BaseValue >= IncreaseOnRank)
            {
                bonusValue += IncreaseOnRankBonus;
            }

            if(bonusValue > 0)
            {
                m_Modifier = stat.AddModifier(bonusValue, this, Descriptor);
            }
        }

        public override void OnTurnOff()
        {
            m_Modifier?.Remove();
            m_Modifier = null;
        }
    }

    // Adding the Favored Class selection is tricky:
    // - if we use ILevelUpSelectClassHandler or add the feature to the race, it will
    //   take priority over the class specific header text (e.g. "Bloodline"
    //   "Specialist School" "Discovery" etc). It's okay, but the UI isn't ideal.
    // - if we add the feature to classes, it will appear in the progression UI and
    //   multiclassing could cause issues.
    // - so instead, we use a patch that runs after ApplyClassMechanics.Apply
    [Harmony12.HarmonyPatch(typeof(ApplyClassMechanics), "Apply", new Type[] { typeof(LevelUpState), typeof(UnitDescriptor) })]
    internal static class ApplyClassMechanics_Apply_Patch
    {
        internal static readonly List<Action<LevelUpState, UnitDescriptor>> onChargenApply = new List<Action<LevelUpState, UnitDescriptor>>();

        static ApplyClassMechanics_Apply_Patch()
        {
            Main.ApplyPatch(typeof(ApplyClassMechanics_Apply_Patch), "Favored Class and Traits during character creation");
        }

        private static void Postfix(ApplyClassMechanics __instance, LevelUpState state, UnitDescriptor unit)
        {
            try
            {
                if(state.NextLevel == 1)
                {
                    foreach(Action<LevelUpState, UnitDescriptor> action in onChargenApply) action(state, unit);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}