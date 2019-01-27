// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Validation;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.Utility;
using Newtonsoft.Json;

namespace EldritchArcana
{
    static class OracleClass
    {
        static LibraryScriptableObject library => Main.library;

        // Convenience for accessing the class, and an array only containing the class.
        // Useful for prerequisties, progressions, ContextRankConfig, BlueprintAbilityResource, etc.
        internal static BlueprintCharacterClass oracle;
        internal static BlueprintCharacterClass[] oracleArray;

        internal static void Load()
        {
            if (OracleClass.oracle != null) return;

            var sorcerer = Helpers.GetClass("b3a505fb61437dc4097f43c3f8f9a4cf");
            var cleric = Helpers.GetClass("67819271767a9dd4fbfd4ae700befea0");

            var oracle = OracleClass.oracle = Helpers.Create<BlueprintCharacterClass>();
            oracleArray = new BlueprintCharacterClass[] { oracle };
            oracle.name = "OracleClass";
            library.AddAsset(oracle, "ec73f4790c1d4554871b81cde0b9399b");
            oracle.LocalizedName = Helpers.CreateString("Oracle.Name", "Oracle");
            oracle.LocalizedDescription = Helpers.CreateString("Oracle.Description", "Although the gods work through many agents, perhaps none is more mysterious than the oracle. These divine vessels are granted power without their choice, selected by providence to wield powers that even they do not fully understand. Unlike a cleric, who draws her magic through devotion to a deity, oracles garner strength and power from many sources, namely those patron deities who support their ideals. Instead of worshiping a single source, oracles tend to venerate all of the gods that share their beliefs. While some see the powers of the oracle as a gift, others view them as a curse, changing the life of the chosen in unforeseen ways.\n" +
                "Role: Oracles do not usually associate with any one church or temple, instead preferring to strike out on their own, or with a small group of like-minded individuals. Oracles typically use their spells and revelations to further their understanding of their mystery, be it through fighting mighty battles or tending to the poor and sick.");
            oracle.m_Icon = cleric.Icon;
            oracle.SkillPoints = Main.settings?.OracleHas3SkillPoints == true ? 3 : 4;
            oracle.HitDie = DiceType.D8;
            oracle.BaseAttackBonus = cleric.BaseAttackBonus;
            oracle.FortitudeSave = sorcerer.FortitudeSave;
            oracle.ReflexSave = sorcerer.ReflexSave;
            oracle.WillSave = sorcerer.WillSave;

            // TODO: Oracle will not work properly with Mystic Theurge.
            // Not sure it's worth fixing, but if desired the fix would be:
            // - patch spellbook selection feats, similar to what Crossblooded does.
            // - use a similar apporach as Theurge does for Inquisitor, to select new spells via the feat UI.
            var spellbook = Helpers.Create<BlueprintSpellbook>();
            spellbook.name = "OracleSpellbook";
            library.AddAsset(spellbook, "c26cdf7ee670428c96aad20225f3fdca");
            spellbook.Name = oracle.LocalizedName;
            spellbook.SpellsPerDay = sorcerer.Spellbook.SpellsPerDay;
            spellbook.SpellsKnown = sorcerer.Spellbook.SpellsKnown;
            spellbook.SpellList = cleric.Spellbook.SpellList;
            spellbook.Spontaneous = true;
            spellbook.IsArcane = false;
            spellbook.AllSpellsKnown = false;
            spellbook.CanCopyScrolls = false;
            spellbook.CastingAttribute = StatType.Charisma;
            spellbook.CharacterClass = oracle;
            spellbook.CantripsType = CantripsType.Orisions;
            oracle.Spellbook = spellbook;

            // Consolidated skills make this a bit of a judgement call. Explanation below.
            // Note that Mysteries add 2 more skills typically.
            oracle.ClassSkills = new StatType[] {
                // Oracles have Diplomacy and Sense Motive. Diplomacy is the main component of
                // Persuasion in PF:K. (Also: while Sense Motives should map to Perception with
                // consolidated skills, in PF:K it seems to be more in line with Persuasion).
                StatType.SkillPersuasion,
                // Oracles have Knowledge (history), which is a main component of (world).
                StatType.SkillKnowledgeWorld,
                // Oracles have Knowledge (planes) and Knowledge (religion) so this is an easy call,
                // because those skills are 100% of consolidated Religion skill.
                StatType.SkillLoreReligion,
            };

            oracle.IsDivineCaster = true;
            oracle.IsArcaneCaster = false;

            oracle.StartingGold = cleric.StartingGold; // all classes start with 411.
            oracle.PrimaryColor = cleric.PrimaryColor;
            oracle.SecondaryColor = cleric.SecondaryColor;

            oracle.RecommendedAttributes = new StatType[] { StatType.Charisma };
            oracle.NotRecommendedAttributes = new StatType[] { StatType.Intelligence };

            oracle.EquipmentEntities = cleric.EquipmentEntities;
            oracle.MaleEquipmentEntities = cleric.MaleEquipmentEntities;
            oracle.FemaleEquipmentEntities = cleric.FemaleEquipmentEntities;

            // Both of the restrictions here are relevant (no atheism feature, no animal class).
            oracle.ComponentsArray = cleric.ComponentsArray;
            oracle.StartingItems = cleric.StartingItems;

            var progression = Helpers.CreateProgression("OracleProgression",
                oracle.Name,
                oracle.Description,
                "317a0f107135425faa7def96cb8ef690",
                oracle.Icon,
                FeatureGroup.None);
            progression.Classes = oracleArray;
            var entries = new List<LevelEntry>();

            var orisons = library.CopyAndAdd<BlueprintFeature>(
                "e62f392949c24eb4b8fb2bc9db4345e3", // cleric orisions
                "OracleOrisonsFeature",
                "926891a8e8a74d9eac63a1e296b1a4f3");
            orisons.SetDescription("Oracles learn a number of orisons, or 0-level spells. These spells are cast like any other spell, but they do not consume any slots and may be used again.");
            orisons.SetComponents(orisons.ComponentsArray.Select(c =>
            {
                var bind = c as BindAbilitiesToClass;
                if (bind == null) return c;
                bind = UnityEngine.Object.Instantiate(bind);
                bind.CharacterClass = oracle;
                bind.Stat = StatType.Charisma;
                return bind;
            }));
            var proficiencies = library.CopyAndAdd<BlueprintFeature>(
                "8c971173613282844888dc20d572cfc9", // cleric proficiencies
                "OracleProficiencies",
                "baee2212dee249cb8136bda72a872ba4");
            proficiencies.SetName("Oracle Proficiencies");
            proficiencies.SetDescription("Oracles are proficient with all simple weapons, light armor, medium armor, and shields (except tower shields). Some oracle revelations grant additional proficiencies.");

            // Note: curses need to be created first, because some revelations use them (e.g. Cinder Dance).
            var curse = OracleCurses.CreateSelection();
            (var mystery, var revelation, var mysteryClassSkills) = CreateMysteryAndRevelationSelection();

            var cureOrInflictSpell = CreateCureOrInflictSpellSelection();

            var detectMagic = library.Get<BlueprintFeature>("ee0b69e90bac14446a4cf9a050f87f2e");
            entries.Add(Helpers.LevelEntry(1,
                proficiencies,
                mystery,
                curse,
                cureOrInflictSpell,
                revelation,
                orisons,
                mysteryClassSkills,
                library.Get<BlueprintFeature>("d3e6275cfa6e7a04b9213b7b292a011c"), // ray calculate feature
                library.Get<BlueprintFeature>("62ef1cdb90f1d654d996556669caf7fa"), // touch calculate feature
                library.Get<BlueprintFeature>("9fc9813f569e2e5448ddc435abf774b3"), // full caster
                detectMagic
            ));
            entries.Add(Helpers.LevelEntry(3, revelation));
            entries.Add(Helpers.LevelEntry(7, revelation));
            entries.Add(Helpers.LevelEntry(11, revelation));
            entries.Add(Helpers.LevelEntry(15, revelation));
            entries.Add(Helpers.LevelEntry(19, revelation));
            progression.UIDeterminatorsGroup = new BlueprintFeatureBase[] {
                mystery, curse, cureOrInflictSpell, proficiencies, orisons, mysteryClassSkills, detectMagic,
            };
            progression.UIGroups = Helpers.CreateUIGroups(
                revelation, revelation, revelation, revelation, revelation, revelation);
            progression.LevelEntries = entries.ToArray();

            oracle.Progression = progression;

            oracle.Archetypes = OracleArchetypes.Create(mystery, revelation, mysteryClassSkills).ToArray();

            oracle.RegisterClass();

            var extraRevelation = Helpers.CreateFeatureSelection("ExtraRevelation",
                "Extra Revelation", "You gain one additional revelation. You must meet all of the prerequisites for this revelation.\nSpecial: You can gain Extra Revelation multiple times.",
                "e91bd89bb5534ae2b61a3222a9b7325e",
                Helpers.GetIcon("fd30c69417b434d47b6b03b9c1f568ff"), // selective channel
                FeatureGroup.Feat,
                Helpers.PrerequisiteClassLevel(oracle, 1));
            var extras = revelation.Features.Select(
                // The level-up UI sometimes loses track of two selections at the same level
                // (e.g. taking Extra Revelations at 1st level),  so clone the feature selections.
                f => library.CopyAndAdd(f, $"{f.name}Extra", f.AssetGuid, "afc8ceb5eb2849d5976e07f5f02ab200")).ToList();
            extras.Add(UndoSelection.Feature.Value);
            extraRevelation.SetFeatures(extras);
            var abundantRevelations = Helpers.CreateFeatureSelection("AbundantRevelations",
                "Abundant Revelations",
                "Choose one of your revelations that has a number of uses per day. You gain 1 additional use per day of that revelation.\nSpecial: You can gain this feat multiple times. Its effects do not stack. Each time you take the feat, it applies to a new revelation.",
                "1614c7b40565481fa3728fd7375ddca0",
                Helpers.GetIcon("a2b2f20dfb4d3ed40b9198e22be82030"), // extra lay on hands
                FeatureGroup.Feat);
            var resourceChoices = new List<BlueprintFeature>();
            var prereqRevelations = new List<Prerequisite> { Helpers.PrerequisiteClassLevel(oracle, 1) };
            CreateAbundantRevelations(revelation, abundantRevelations, resourceChoices, prereqRevelations, new HashSet<BlueprintFeature>());
            abundantRevelations.SetFeatures(resourceChoices);
            abundantRevelations.SetComponents(prereqRevelations);

            library.AddFeats(extraRevelation, abundantRevelations);
        }

        static void CreateAbundantRevelations(BlueprintFeature revelation, BlueprintFeatureSelection abundantRevelations, List<BlueprintFeature> resourceChoices, List<Prerequisite> prereqRevelations, HashSet<BlueprintFeature> seen)
        {
            if (revelation == LifeMystery.lifeLink) return;

            bool first = true;
            foreach (var resourceLogic in revelation.GetComponents<AddAbilityResources>())
            {
                if (!seen.Add(revelation)) continue;
                var resource = resourceLogic.Resource;
                var feature = Helpers.CreateFeature($"{abundantRevelations.name}{revelation.name}",
                    $"{abundantRevelations.Name} — {revelation.Name}",
                    $"{abundantRevelations.Description}\n{revelation.Description}",
                    Helpers.MergeIds("d2f3b9be00b04940805bff7b7f60381f", revelation.AssetGuid, resource.AssetGuid),
                    revelation.Icon,
                    FeatureGroup.None,
                    revelation.PrerequisiteFeature(),
                    resource.CreateIncreaseResourceAmount(1));
                resourceChoices.Add(feature);
                if (first)
                {
                    prereqRevelations.Add(revelation.PrerequisiteFeature(true));
                    first = false;
                }
            }
            var selection = revelation as BlueprintFeatureSelection;
            if (selection == null) return;

            foreach (var r in selection.Features)
            {
                CreateAbundantRevelations(r, abundantRevelations, resourceChoices, prereqRevelations, seen);
            }
        }

        static (BlueprintFeatureSelection, BlueprintFeatureSelection, BlueprintFeature) CreateMysteryAndRevelationSelection()
        {
            // This feature allows archetypes to replace mystery class skills with something else.
            var classSkill = Helpers.CreateFeature("MysteryClassSkills", "Bonus Class Skills",
                "Oracles receive additional class skills depending upon their oracle mystery.",
                "3949c44664d047c99d870b1f3728457c",
                null,
                FeatureGroup.None);

            // TODO: need some additional mysteries.
            //
            // Implemented:
            // - Dragon
            // - Battle
            // - Flame
            // - Life
            // - Time (with Ancient Lorekeeper archetype)
            //
            // Other interesting ones:
            // - Bone (necromancy seems fun; need to learn how summons work though)
            // - Heavens (flashy rainbow spells! Cha to all saves at lvl 20. Some revelations won't work in CRPG.)
            // - Nature (druid-ish, has a restricted animal companion. Some revelations wouldn't work well in CRPG.)
            // - Ancestor (to go with Ancient Lorekeeper)
            //
            // Ancestor would lose 2 revelations because they wouldn't do anything without
            // a GM: Voice of the Grave, Wisdom of the Ancestor. Maybe they can be redesigned
            // to offer a temporary "insight" bonus to certain stats, to represent the
            // information you learned? Or maybe one of these can be reworked into
            // Heroism/Greater Heroism, since that's the 2 spells Loremaster gives up,
            // and duplicating spells as SLAs is common for revelations.)

            var mysteryDescription = "Each oracle draws upon a divine mystery to grant her spells and powers. This mystery also grants additional class skills and other special abilities. This mystery can represent a devotion to one ideal, prayers to deities that support the concept, or a natural calling to champion a cause. For example, an oracle with the waves mystery might have been born at sea and found a natural calling to worship the gods of the oceans, rivers, and lakes, be they benign or malevolent. Regardless of its source, the mystery manifests in a number of ways as the oracle gains levels. An oracle must pick one mystery upon taking her first level of oracle. Once made, this choice cannot be changed.\n" +
                            "At 2nd level, and every two levels thereafter, an oracle learns an additional spell derived from her mystery. These spells are in addition to the number of chosen known spells. They cannot be exchanged for different spells at higher levels.";

            var mysteriesAndRevelations = new (BlueprintFeature, BlueprintFeature)[] {
                BattleMystery.Create(mysteryDescription, classSkill),
                DragonMystery.Create(mysteryDescription, classSkill),
                FlameMystery.Create(mysteryDescription, classSkill),
                LifeMystery.Create(mysteryDescription, classSkill),
                TimeMystery.Create(mysteryDescription, classSkill),
            };
            var mysteryChoice = Helpers.CreateFeatureSelection("OracleMysterySelection", "Mystery",
                            mysteryDescription,
                            "ec3a4ede658f4b2696c89bdd590b5e04",
                            null,
                            UpdateLevelUpDeterminatorText.Group);
            mysteryChoice.SetFeatures(mysteriesAndRevelations.Select(m => m.Item1));
            var revelationChoice = Helpers.CreateFeatureSelection("OracleRevelationSelection", "Revelation",
                "At 1st level, 3rd level, and every four levels thereafter (7th, 11th, and so on), an oracle uncovers a new secret about her mystery that grants her powers and abilities. The oracle must select a revelation from the list of revelations available to her mystery (see FAQ at right). If a revelation is chosen at a later level, the oracle gains all of the abilities and bonuses granted by that revelation based on her current level. Unless otherwise noted, activating the power of a revelation is a standard action.\n" +
                "Unless otherwise noted, the DC to save against these revelations is equal to 10 + 1 / 2 the oracle’s level + the oracle’s Charisma modifier.",
                "1dd88ec42dc249ca94bf3c2fc239064d",
                null,
                FeatureGroup.None);
            revelationChoice.SetFeatures(mysteriesAndRevelations.Select(m => m.Item2));

            return (mysteryChoice, revelationChoice, classSkill);
        }

        internal static void MaybeUpdateSkillPoints()
        {
            if (oracle == null) return;
            oracle.SkillPoints = Main.settings?.OracleHas3SkillPoints == true ? 3 : 4;
        }

        static BlueprintFeatureSelection CreateCureOrInflictSpellSelection()
        {
            var selection = Helpers.CreateFeatureSelection("OracleCureOrInflictSpellSelection", "Cure or Inflict Spells",
                "In addition to the spells gained by oracles as they gain levels, each oracle also adds all of either the cure spells or the inflict spells to her list of spells known (cure spells include all spells with “cure” in the name, inflict spells include all spells with “inflict” in the name). These spells are added as soon as the oracle is capable of casting them. This choice is made when the oracle gains her first level and cannot be changed.",
                "4e685b25900246939394662b7fa36295",
                null,
                UpdateLevelUpDeterminatorText.Group);

            var cureProgression = Helpers.CreateProgression("OracleCureSpellProgression",
                "Cure Spells",
                selection.Description,
                "99b17564aaf94886b6858c92eec20285",
                Helpers.GetIcon("47808d23c67033d4bbab86a1070fd62f"), // cure light wounds
                FeatureGroup.None);
            cureProgression.Classes = oracleArray;

            var cureSpells = Bloodlines.CreateSpellProgression(cureProgression, cureSpellIds);
            var cureEntries = new List<LevelEntry>();
            for (int level = 1; level <= 8; level++)
            {
                int classLevel = level == 1 ? 1 : level * 2;
                cureEntries.Add(Helpers.LevelEntry(classLevel, cureSpells[level - 1]));
            }
            cureProgression.LevelEntries = cureEntries.ToArray();
            cureProgression.UIGroups = Helpers.CreateUIGroups(cureSpells);

            var inflictProgression = Helpers.CreateProgression("OracleInflictSpellProgression",
                "Inflict Spells",
                selection.Description,
                "1ad92576cf214c9a8890cd9ef6a06a31",
                Helpers.GetIcon("e5cb4c4459e437e49a4cd73fde6b9063"), // inflict light wounds
                FeatureGroup.None);
            inflictProgression.Classes = oracleArray;

            var inflictSpells = Bloodlines.CreateSpellProgression(inflictProgression, new String[] {
                "e5af3674bb241f14b9a9f6b0c7dc3d27", // inflict light wounds
                "65f0b63c45ea82a4f8b8325768a3832d", // moderate
                "bd5da98859cf2b3418f6d68ea66cabbe", // serious
                "651110ed4f117a948b41c05c5c7624c0", // critical
                "9da37873d79ef0a468f969e4e5116ad2", // light, mass
                "03944622fbe04824684ec29ff2cec6a7", // moderate, mass
                "820170444d4d2a14abc480fcbdb49535", // serious, mass
                "5ee395a2423808c4baf342a4f8395b19", // critical, mass
            });
            var inflictEntries = new List<LevelEntry>();
            for (int level = 1; level <= 8; level++)
            {
                int classLevel = level == 1 ? 1 : level * 2;
                inflictEntries.Add(Helpers.LevelEntry(classLevel, inflictSpells[level - 1]));
            }
            inflictProgression.LevelEntries = inflictEntries.ToArray();
            inflictProgression.UIGroups = Helpers.CreateUIGroups(inflictSpells);

            selection.SetFeatures(cureProgression, inflictProgression);
            return selection;
        }

        internal static BindAbilitiesToClass CreateBindToOracle(params BlueprintAbility[] abilities)
        {
            return Helpers.Create<BindAbilitiesToClass>(b =>
            {
                b.Stat = StatType.Charisma;
                b.Abilites = abilities;
                b.CharacterClass = oracle;
                b.AdditionalClasses = Array.Empty<BlueprintCharacterClass>();
                b.Archetypes = Array.Empty<BlueprintArchetype>();
            });
        }

        internal static Lazy<BlueprintAbility[]> cureSpells = new Lazy<BlueprintAbility[]>(() =>
            OracleClass.cureSpellIds.Select(library.Get<BlueprintAbility>).ToArray());

        static String[] cureSpellIds = new String[] {
            "5590652e1c2225c4ca30c4a699ab3649", // cure light wounds
            "6b90c773a6543dc49b2505858ce33db5", // moderate
            "3361c5df793b4c8448756146a88026ad", // serious
            "41c9016596fe1de4faf67425ed691203", // critical
            "5d3d689392e4ff740a761ef346815074", // light, mass
            "571221cc141bc21449ae96b3944652aa", // moderate, mass
            "0cea35de4d553cc439ae80b3a8724397", // serious, mass
            "1f173a16120359e41a20fc75bb53d449", // critical, mass
        };
    }

    // Adds a feature based on a level range and conditional.
    //
    // This makes it easier to implement complex conditions (e.g. Oracle Dragon Mystery Resistances)
    // without needing to create nested BlueprintFeatures and/or BlueprintProgressions.
    //
    // Essentially this combines AddFeatureIfHasFact with two AddFeatureOnClassLevels, to express:
    //
    //     if (MinLevel <= ClassLevel && ClassLevel <= MaxLevelInclusive &&
    //         (CheckedFact == null || HasFeature(CheckedFact) != Not)) {
    //         AddFact(Feature);
    //     }
    //
    [AllowMultipleComponents]
    [AllowedOn(typeof(BlueprintUnitFact))]
    public class AddFactOnLevelRange : AddFactOnLevelUpCondtion
    {
        // The class to use for `MinLevel` and `MaxLevelInclusive`.
        // Optionally `AdditionalClasses` and `Archetypes` can be specified for more classes/archetypes.
        public BlueprintCharacterClass Class;

        // Optionally specifies the feature to check for.        
        public BlueprintUnitFact CheckedFact;

        // If `CheckedFact` is supplied, this indicates whether we want it to be present or
        // not present.
        public bool Not;

        public BlueprintCharacterClass[] AdditionalClasses = Array.Empty<BlueprintCharacterClass>();

        public BlueprintArchetype[] Archetypes = Array.Empty<BlueprintArchetype>();

        protected override int CalcLevel() => ReplaceCasterLevelOfAbility.CalculateClassLevel(Class, AdditionalClasses, Owner, Archetypes);

        protected override bool IsFeatureShouldBeApplied(int level)
        {
            return base.IsFeatureShouldBeApplied(level) && (CheckedFact == null || Owner.HasFact(CheckedFact) != Not);
        }
    }


    // A customizable "add fact on level", that also can interact with the level-up UI
    // in a similar way to progressions and features the player picked.
    // (This is similar to how the level-up UI calls LevelUpHelper.AddFeatures).
    public abstract class AddFactOnLevelUpCondtion : OwnedGameLogicComponent<UnitDescriptor>, IUnitGainLevelHandler
    {
        // The min and max (inclusive) levels in which to apply this feature.
        public int MinLevel = 1, MaxLevelInclusive = 20;

        // The feature to add, if the condition(s) are met.
        public BlueprintUnitFact Feature;

        [JsonProperty]
        private Fact appliedFact;

        public override void OnFactActivate() => Apply();

        public override void OnFactDeactivate()
        {
            Owner.RemoveFact(appliedFact);
            appliedFact = null;
        }

        public override void PostLoad()
        {
            base.PostLoad();
            if (appliedFact != null && !Owner.HasFact(appliedFact))
            {
                appliedFact.Dispose();
                appliedFact = null;
                if (BlueprintRoot.Instance.PlayerUpgradeActions.AllowedForRestoreFeatures.HasItem(Feature))
                {
                    Apply();
                }
            }
        }

        protected abstract int CalcLevel();

        protected virtual bool IsFeatureShouldBeApplied(int level)
        {
            Log.Write($"AddFactOnLevelUpCondtion::IsFeatureShouldBeApplied({level}), MinLevel {MinLevel}, MaxLevelInclusive {MaxLevelInclusive}");
            return level >= MinLevel && level <= MaxLevelInclusive;
        }

        public void HandleUnitGainLevel(UnitDescriptor unit, BlueprintCharacterClass @class)
        {
            if (unit == Owner) Apply();
        }

        private Fact Apply()
        {
            Log.Write($"AddFactOnLevelUpCondtion::Apply(), name: {Fact.Blueprint.name}");
            var level = CalcLevel();
            if (IsFeatureShouldBeApplied(level))
            {
                if (appliedFact == null)
                {
                    appliedFact = Owner.AddFact(Feature, null, (Fact as Feature)?.Param);
                    OnAddLevelUpFeature(level);
                }
            }
            else if (appliedFact != null)
            {
                Owner.RemoveFact(appliedFact);
                appliedFact = null;
            }
            return appliedFact;
        }

        private void OnAddLevelUpFeature(int level)
        {
            Log.Write($"AddFactOnLevelUpCondtion::OnAddLevelUpFeature(), name: {Fact.Blueprint.name}");
            var fact = appliedFact;
            if (fact == null) return;

            var feature = fact.Blueprint as BlueprintFeature;
            if (feature == null) return;

            // If we're in the level-up UI, update selections/progressions as needed.
            var unit = Owner;
            var levelUp = Game.Instance.UI.CharacterBuildController.LevelUpController;
            if (unit == levelUp.Preview || unit == levelUp.Unit)
            {
                var selection = feature as BlueprintFeatureSelection;
                if (selection != null)
                {
                    Log.Write($"{GetType().Name}: add selection ${selection.name}");
                    levelUp.State.AddSelection(null, selection, selection, level);
                }
                var progression = feature as BlueprintProgression;
                if (progression != null)
                {
                    Log.Write($"{GetType().Name}: update progression ${selection.name}");
                    LevelUpHelper.UpdateProgression(levelUp.State, unit, progression);
                }
            }
        }
    }



    [AllowMultipleComponents]
    public class AddClassSkillIfHasFeature : OwnedGameLogicComponent<UnitDescriptor>, IUnitGainLevelHandler
    {
        public StatType Skill;
        public BlueprintUnitFact CheckedFact;

        [JsonProperty]
        bool applied;

        internal static AddClassSkillIfHasFeature Create(StatType skill, BlueprintUnitFact feature)
        {
            var a = Helpers.Create<AddClassSkillIfHasFeature>();
            a.name = $"AddClassSkillIfHasFeature${skill}";
            a.Skill = skill;
            a.CheckedFact = feature;
            return a;
        }

        public override void OnTurnOn()
        {
            base.OnTurnOn();
            Apply();
        }

        public override void OnTurnOff()
        {
            base.OnTurnOff();
            Remove();
        }

        void Apply()
        {
            if (Owner.HasFact(CheckedFact))
            {
                if (!applied)
                {
                    var stat = Owner.Stats.GetStat<ModifiableValueSkill>(Skill);
                    stat?.ClassSkill.Retain();
                    stat?.UpdateValue();
                    applied = true;
                }
            }
            else
            {
                Remove();
            }
        }

        void Remove()
        {
            if (applied)
            {
                var stat = Owner.Stats.GetStat<ModifiableValueSkill>(Skill);
                stat?.ClassSkill.Release();
                stat?.UpdateValue();
                applied = false;
            }
        }

        public void HandleUnitGainLevel(UnitDescriptor unit, BlueprintCharacterClass @class)
        {
            if (unit == Owner) Apply();
        }
    }
}
