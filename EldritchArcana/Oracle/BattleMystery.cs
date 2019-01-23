// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.Controllers.Combat;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Utility;
using Kingmaker.View;
using Pathfinding;
using UnityEngine;
using static Kingmaker.RuleSystem.RulebookEvent;
using static Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityResourceLogic;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class BattleMystery
    {
        internal static BlueprintFeature finalRevelation;

        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass oracle => OracleClass.oracle;
        static BlueprintCharacterClass[] oracleArray => OracleClass.oracleArray;

        internal static (BlueprintFeature, BlueprintFeature) Create(String mysteryDescription, BlueprintFeature classSkillFeat)
        {
            var skill1 = StatType.SkillPerception;
            var skill2 = StatType.SkillMobility;

            var revelations = new List<BlueprintFeature> {
                CreateBattlecry(),
                CreateBattlefieldClarity(),
                CreateCombatHealer(),
                CreateIronSkin(),
                CreateManeuverMastery(),
                CreateResiliency(),
                CreateSkillAtArms(),
                CreateSurprisingCharge(),
                CreateWarSight(),
                CreateWeaponMastery(),
            };
            var description = new StringBuilder(mysteryDescription);
            description.AppendLine(
                $"\nClass skills: {UIUtility.GetStatText(skill1)}, {UIUtility.GetStatText(skill2)}\n" +
                "An oracle with the battle mystery can choose from any of the following revelations:");
            foreach (var r in revelations)
            {
                description.AppendLine($"• {r.Name}");
            }
            var mystery = Helpers.CreateProgression("MysteryBattleProgression", "Battle Mystery", description.ToString(),
                "4c1f09f08d984c05993c552a27a04b12",
                Helpers.GetIcon("27203d62eb3d4184c9aced94f22e1806"), // Transformation spell
                UpdateLevelUpDeterminatorText.Group,
                AddClassSkillIfHasFeature.Create(skill1, classSkillFeat),
                AddClassSkillIfHasFeature.Create(skill2, classSkillFeat));
            mystery.Classes = oracleArray;

            var spells = Bloodlines.CreateSpellProgression(mystery, new String[] {
                "c60969e7f264e6d4b84a1499fdcf9039", // enlarge person
                "5181c2ed0190fc34b8a1162783af5bf4", // stone call (should be: fog cloud)
                "2d4263d80f5136b4296d6eb43a221d7d", // magic vestment
                FireSpells.wallOfFire.AssetGuid,  // wall of fire 
                "90810e5cf53bf854293cbd5ea1066252", // righteous might
                "6a234c6dcde7ae94e94e9c36fd1163a7", // mass bull's strength
                "8eb769e3b583f594faabe1cfdb0bb696", // summon elemental, greater (should be: control weather)
                "7cfbefe0931257344b2cb7ddc4cdff6f", // stormbolts (should be: earthquake)
                "ba48abb52b142164eba309fd09898856", // polar midnight (should be: storm of vengeance)
            });

            var entries = new List<LevelEntry>();
            for (int level = 1; level <= 9; level++)
            {
                entries.Add(Helpers.LevelEntry(level * 2, spells[level - 1]));
            }
            entries.Add(Helpers.LevelEntry(20, CreateFinalRevelation()));

            mystery.LevelEntries = entries.ToArray();
            mystery.UIGroups = Helpers.CreateUIGroups(new List<BlueprintFeatureBase>(spells) { finalRevelation });

            var revelation = Helpers.CreateFeatureSelection("MysteryBattleRevelation", "Battle Revelation",
                mystery.Description, "3c553e119a484a179e70b0aada836283", null, FeatureGroup.None,
                mystery.PrerequisiteFeature());
            revelation.Mode = SelectionMode.OnlyNew;
            revelations.Add(UndoSelection.Feature.Value);
            revelation.SetFeatures(revelations);
            return (mystery, revelation);
        }

        static BlueprintFeature CreateFinalRevelation()
        {

            finalRevelation = Helpers.CreateFeature("MysteryBattleFinalRevelation", "Final Revelation",
                "Upon reaching 20th level, you become an avatar of battle. You can take a full-attack action and move up to your speed as a full-round action (you can move before or after the attacks). Whenever you score a critical hit, you can ignore any DR the target might possess. You gain a +4 insight bonus to your AC for the purpose of confirming critical hits against you.",
                "c37000f13f5d45baae9918c2dddfb993",
                Helpers.GetIcon("e15e5e7045fda2244b98c8f010adfe31"),
                FeatureGroup.None,
                Helpers.Create<IgnoreDamageReductionOnAttack>(i => i.CriticalHit = true),
                Helpers.Create<ACBonusAgainstCriticalHits>(a => { a.Bonus = 1; a.Descriptor = ModifierDescriptor.Insight; }));

            Main.ApplyPatch(typeof(UnitCombatState_IsFullAttackRestrictedBecauseOfMoveAction_Patch), "Battle Mystery Final Revelation full attack after moving");
            return finalRevelation;
        }

        static BlueprintFeature CreateBattlecry()
        {
            var bless = library.Get<BlueprintAbility>("90e59f4a4ada87243b7b3535a06d0638");
            var feat = Helpers.CreateFeature("MysteryBattleBattlecry", "Battlecry",
                "As a standard action, you can unleash an inspiring battlecry. All allies within 100 feet who hear your cry gain a +1 morale bonus on attack rolls, skill checks, and saving throws for a number of rounds equal to your Charisma modifier. At 10th level, this bonus increases to +2. You can use this ability once per day, plus one additional time per day at 5th level and for every five levels thereafter.",
                "18c60a9fcef24ebab71b146d543f47af",
                bless.Icon,
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "27d0f1d6fcd0499fb063a4cd005bf6f9", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 5, 1, 5, 1, 0, 0, oracleArray);

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                "504f08e9dd974b61a7ea6e9bb74df2f9", feat.Icon,
                null,
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.Custom, classes: oracleArray,
                    customProgression: new (int, int)[] { (9, 1), (20, 2) }),
                Helpers.CreateAddContextStatBonus(StatType.AdditionalAttackBonus, ModifierDescriptor.Morale),
                Helpers.CreateAddContextStatBonus(StatType.SaveFortitude, ModifierDescriptor.Morale),
                Helpers.CreateAddContextStatBonus(StatType.SaveReflex, ModifierDescriptor.Morale),
                Helpers.CreateAddContextStatBonus(StatType.SaveWill, ModifierDescriptor.Morale),
                Helpers.Create<BuffAllSkillsBonusAbilityValue>(a =>
                {
                    a.Descriptor = ModifierDescriptor.Morale;
                    a.Value = Helpers.CreateContextValueRank();
                }));

            var ability = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                "a17b453cc64b4e979202be0229dd1cb7", feat.Icon,
                AbilityType.Extraordinary, CommandType.Standard, AbilityRange.Personal,
                "Rounds/Charisma modifier", "",
                bless.GetComponent<AbilitySpawnFx>(),
                Helpers.CreateAbilityTargetsAround(100.Feet(), TargetType.Ally),
                Helpers.CreateResourceLogic(resource),
                Helpers.CreateRunActions(Helpers.CreateApplyBuff(buff,
                    Helpers.CreateContextDuration(),
                    fromSpell: false, dispellable: false)));
            ability.EffectOnAlly = AbilityEffectOnUnit.Helpful;

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateBattlefieldClarity()
        {
            var feat = Helpers.CreateFeature("MysteryBattleBattlefieldClarity", "Battlefield Clarity",
                "Once per day, as an immediate action, whenever you fail a saving throw that causes you to become blinded, deafened, frightened, panicked, paralyzed, shaken, or stunned, you may attempt that saving throw again, with a +4 insight bonus on the roll. You must take the second result, even if it is worse. At 7th and 15th level, you can use this ability one additional time per day.",
                "9f47a7738fb340c6818df733364a7b3c",
                Helpers.GetIcon("485a18c05792521459c7d06c63128c79"), // improved uncanny dodge
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "994db1296a5c47f7969ddb007c70a3aa", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 7, 1, 8, 1, 0, 0, oracleArray);

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                "42c8013402c24202af3e5a04a3918f26", feat.Icon, null,
                RerollSaveAgainstAbilities.Create(resource, 4, ModifierDescriptor.Insight,
                    SpellDescriptor.Blindness | SpellDescriptor.Frightened | SpellDescriptor.Paralysis |
                    SpellDescriptor.Shaken | SpellDescriptor.Stun));

            var ability = Helpers.CreateActivatableAbility($"{feat.name}ToggleAbility", feat.Name, feat.Description,
                "072bca27357f41bc8f92a62b0ee36693", feat.Icon, buff, AbilityActivationType.Immediately,
                CommandType.Free, null, Helpers.CreateActivatableResourceLogic(resource, ResourceSpendType.Never));
            ability.IsOnByDefault = true;

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        internal static BlueprintFeature CreateCombatHealer()
        {
            if (combatHealer != null) return combatHealer;
            var feat = Helpers.CreateFeature($"OracleCombatHealer", "Combat Healer",
                "Whenever you cast a cure spell (a spell with “cure” in its name), you can cast it as a swift action, as if using the Quicken Spell feat, by expending two spell slots. This does not increase the level of the spell. You can use this ability once per day at 7th level and one additional time per day for every four levels beyond 7th.",
                "64c5870b6dc44a07a395cadb57a8f472",
                Helpers.GetIcon("6b90c773a6543dc49b2505858ce33db5"), // cure moderate wounds
                FeatureGroup.None);
            combatHealer = feat;

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "",
                "8711721464b444b29612571264e1780b", null);
            resource.SetIncreasedByLevelStartPlusDivStep(0, 7, 1, 4, 1, 0, 0, oracleArray);

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description,
                "bd20acaa758847379d718710b145db00", feat.Icon, null);

            var ability = Helpers.CreateActivatableAbility($"{feat.name}ToggleAbility", feat.Name, feat.Description,
                "857fcf3a530d495aaaccbbbb1d4ec089",
                feat.Icon, buff, AbilityActivationType.Immediately,
                CommandType.Free, null, Helpers.CreateActivatableResourceLogic(resource, ResourceSpendType.Never));

            buff.SetComponents(Helpers.Create<AutoMetamagicExtraCost>(a =>
            {
                a.AbilitiesWhiteList = OracleClass.cureSpells.Value;
                a.Metamagic = Metamagic.Quicken;
                a.RodAbility = ability;
                a.RequiredResource = resource;
            }));

            feat.SetComponents(
                Helpers.PrerequisiteClassLevel(oracle, 7),
                resource.CreateAddAbilityResource(),
                ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateIronSkin()
        {
            var stoneskin = library.Get<BlueprintAbility>("c66e86905f7606c4eaa5c774f0357b2b");
            var feat = Helpers.CreateFeature("MysteryBattleIronSkin", "Iron Skin",
                "Once per day, your skin hardens and takes on the appearance of iron, granting you DR 10/adamantine. This functions as stoneskin, using your oracle level as the caster level. At 15th level, you can use this ability twice per day.",
                "e19be65f7e444601b40788d4e4f7d297",
                stoneskin.Icon,
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "04be323c93da4c2090b35fa5773f319d", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 15, 1, 15, 0, 0, 0, oracleArray);

            var ironskinBuff = library.CopyAndAdd<BlueprintBuff>("7aeaf147211349b40bb55c57fec8e28d", $"{feat.name}Buff", "0937e889fb6f45a18f4badd5c38be307");
            ironskinBuff.SetNameDescriptionIcon(feat.Name, feat.Description, feat.Icon);

            var ironskin = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                "5a2c138d10134b7a8c26712e61f1a88b",
                stoneskin.Icon,
                AbilityType.Supernatural,
                CommandType.Standard,
                AbilityRange.Personal,
                stoneskin.LocalizedDuration,
                stoneskin.LocalizedSavingThrow,
                Helpers.CreateResourceLogic(resource),
                Helpers.CreateContextRankConfig(ContextRankBaseValueType.ClassLevel, ContextRankProgression.AsIs, classes: oracleArray),
                Helpers.CreateRunActions(Helpers.CreateApplyBuff(ironskinBuff,
                    Helpers.CreateContextDuration(rate: DurationRate.TenMinutes),
                    fromSpell: false, dispellable: false)));

            feat.SetComponents(
                Helpers.PrerequisiteClassLevel(oracle, 11),
                resource.CreateAddAbilityResource(),
                ironskin.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateManeuverMastery()
        {
            // Note: reworked this slightly to swap 1st and 7th level abilities.
            // (PF:K does not let you use maneuvers without the Improved feat, so it needs to come first.)
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var feat = Helpers.CreateFeatureSelection("MysteryBattleManeuverMastery",
                "Maneuver Mastery",
                "Select one type of combat maneuver. You gain the Improved feat (such as Improved Trip) that grants you the ability to perform that maneuver. At the 7th level, you treat your oracle level as your base attack bonus when determining your CMB for the selected maneuver. At 11th level, you gain the Greater feat (such as Greater Trip) that grants you a bonus when performing that maneuver. You do not need to meet the prerequisites to receive these feats.",
                "44f54173a6cc4111af4e9e5c2c86a036",
                Helpers.GetIcon("4c44724ffa8844f4d9bedb5bb27d144a"), // combat expertise
                FeatureGroup.None,
                noFeature);

            // TODO: Overrun? The game has some support, but no improved/greater feats.
            feat.SetFeatures(
                CreateManeuverMastery(feat, "b3614622866fe7046b787a548bbd7f59", "72ba6ad46d94ecd41bad8e64739ea392"), // bull rush
                CreateManeuverMastery(feat, "ed699d64870044b43bb5a7fbe3f29494", "52c6b07a68940af41b270b3710682dc7"), // dirty trick
                CreateManeuverMastery(feat, "25bc9c439ac44fd44ac3b1e58890916f", "63d8e3a9ab4d72e4081a7862d7246a79"), // disarm
                CreateManeuverMastery(feat, "9719015edcbf142409592e2cbaab7fe1", "54d824028117e884a8f9356c7c66149b"), // sunder
                CreateManeuverMastery(feat, "0f15c6f70d8fb2b49aa6cc24239cc5fa", "4cc71ae82bdd85b40b3cfe6697bb7949")); // trip
            noFeature.Feature = feat;
            return feat;
        }

        static BlueprintFeature CreateManeuverMastery(BlueprintFeatureSelection revelation, String improvedId, String greaterId)
        {
            var improved = library.Get<BlueprintFeature>(improvedId);
            var greater = library.Get<BlueprintFeature>(greaterId);
            var maneuver = improved.GetComponent<ManeuverBonus>().Type;

            var feat = Helpers.CreateFeature($"{revelation.name}{maneuver}",
                $"{revelation.Name} — {improved.Name}",
                $"{revelation.Description}\n{improved.Description}",
                Helpers.MergeIds("df31be4c2a8c4287a47d605fcda291c0", improvedId),
                improved.Icon,
                FeatureGroup.None,
                Helpers.CreateAddFacts(improved),
                greater.CreateAddFactOnLevelRange(oracle, 11));

            var useClassAsBAB = Helpers.CreateFeature($"{feat.name}FullBAB",
                feat.Name,
                revelation.Description,
                Helpers.MergeIds("737d14e2838f427880df710c665a9097", improvedId),
                improved.Icon,
                FeatureGroup.None,
                Helpers.Create<OffensiveCombatTraining>(a => { a.Maneuver = maneuver; a.Class = oracle; }));

            feat.AddComponent(useClassAsBAB.CreateAddFactOnLevelRange(oracle, 7));

            return feat;
        }

        static BlueprintFeature CreateResiliency()
        {
            var diehard = library.Get<BlueprintFeature>("86669ce8759f9d7478565db69b8c19ad");
            return Helpers.CreateFeature("MysteryBattleResiliency", "Resiliency",
                $"You get Diehard as a bonus feat.\n{diehard.Description}",
                "2fb72ffd67934a30889fd6fc25022a9e",
                diehard.Icon,
                FeatureGroup.None,
                Helpers.PrerequisiteClassLevel(oracle, 7),
                AddMechanicsFeature.MechanicsFeatureType.Ferocity.CreateAddMechanics());
        }

        static BlueprintFeature CreateSkillAtArms()
        {
            var heavyArmor = library.Get<BlueprintFeature>("1b0f68188dcc435429fb87a022239681");
            var martialWeapons = library.Get<BlueprintFeature>("203992ef5b35c864390b4e4a1e200629");
            var scalemail = library.Get<BlueprintItemArmor>("d7963e1fcf260c148877afd3252dbc91");
            var feat = Helpers.CreateFeature("MysteryBattleSkillAtArms", "Skill at Arms",
                "You gain proficiency in all martial weapons and heavy armor.",
                "a4606d518d0046159ce30ab05b998a60",
                martialWeapons.Icon,
                FeatureGroup.None,
                Helpers.CreateAddFacts(heavyArmor, martialWeapons),
                Helpers.Create<AddStartingEquipment>(a =>
                {
                    a.CategoryItems = Array.Empty<WeaponCategory>();
                    a.RestrictedByClass = Array.Empty<BlueprintCharacterClass>();
                    a.BasicItems = new BlueprintItem[] { scalemail };
                }));
            return feat;
        }

        static BlueprintFeature CreateSurprisingCharge()
        {
            var expeditiousRetreatBuff = library.Get<BlueprintBuff>("9ea4ec3dc30cd7940a372a4d699032e7");
            var feat = Helpers.CreateFeature("MysteryBattleSurprisingCharge", "Surprising Charge",
                "Once per day, you can move up to your speed as an immediate action. You can use this ability one additional time per day at 7th level and 15th level.",
                "9896725bc76b437ebe2fa6911b78788c",
                expeditiousRetreatBuff.Icon,
                FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "badd46f106e54c2b95016964c6d01847", null);
            resource.SetIncreasedByLevelStartPlusDivStep(1, 7, 1, 8, 1, 0, 0, oracleArray);

            var ability = Helpers.CreateAbility($"{feat.name}Ability", feat.Name, feat.Description,
                "5c596ae44d174e858b0d65f5e6325b6d",
                feat.Icon,
                AbilityType.Extraordinary,
                // TODO: should be an immediate action
                CommandType.Free,
                AbilityRange.DoubleMove,
                "", "",
                Helpers.CreateResourceLogic(resource),
                Helpers.Create<AbilityCustomSurprisingCharge>());
            ability.CanTargetPoint = true;
            ability.CanTargetEnemies = true;
            ability.CanTargetFriends = true;

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());
            return feat;
        }

        static BlueprintFeature CreateWarSight()
        {
            return CreateRerollInitiative("MysteryBattleWarSight", "War Sight", "bd17cb0da953415691e63d17db26bb4b");
        }

        internal static BlueprintFeature CreateRerollInitiative(String name, String displayName, String assetId)
        {
            var feat = Helpers.CreateFeature(name, displayName,
                "Whenever you roll for initiative, you can roll twice and take either result. At 7th level, you can always act in the surprise round, but if you fail to notice the ambush, you act last, regardless of your initiative result (you act in the normal order in following rounds). At 11th level, you can roll for initiative three times and take any one of the results.",
                assetId,
                Helpers.GetIcon("797f25d709f559546b29e7bcb181cc74"), // improved initiative
                FeatureGroup.None);

            var feat2Rolls = library.CopyAndAdd(feat, $"{name}Rolls2", assetId, "e0b91d0fbc2e4753abe8a6afb521cd41");
            feat2Rolls.SetComponents(
                Helpers.Create<CanActInSurpriseRoundLogic>(a => { a.Class = oracle; a.MinLevel = 7; }),
                Helpers.Create<ModifyD20>(m => { m.Rule = RuleType.Intiative; m.RollsAmount = 2; m.TakeBest = true; }));

            var feat3Rolls = library.CopyAndAdd(feat, $"{name}Rolls3", assetId, "9407e4078e15415ab104f9186d6295e6");
            feat3Rolls.SetComponents(
                Helpers.Create<CanActInSurpriseRoundLogic>(a => { a.Class = oracle; a.MinLevel = 7; }),
                Helpers.Create<ModifyD20>(m => { m.Rule = RuleType.Intiative; m.RollsAmount = 3; m.TakeBest = true; }));

            feat.SetComponents(
                feat2Rolls.CreateAddFactOnLevelRange(oracle, maxLevel: 10),
                feat3Rolls.CreateAddFactOnLevelRange(oracle, minLevel: 11));
            return feat;
        }

        static BlueprintFeature CreateWeaponMastery()
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var weaponFocus = library.Get<BlueprintParametrizedFeature>("1e1f627d26ad36f43bbd26cc2bf8ac7e");
            var weaponFocusGreater = library.Get<BlueprintParametrizedFeature>("09c9e82965fb4334b984a1e9df3bd088");
            var improvedCritical = library.Get<BlueprintParametrizedFeature>("f4201c85a991369408740c6888362e20");
            var feat = Helpers.CreateParamSelection<WeaponMasteryCustomSelection>("MysteryBattleWeaponMastery",
                "Weapon Mastery",
                "Select one weapon with which you are proficient. You gain Weapon Focus with that weapon. At 8th level, you gain Improved Critical with that weapon. At 12th level, you gain Greater Weapon Focus with that weapon. You do not need to meet the prerequisites to receive these feats.",
                "7223e69cd8644b27aeb58150f0155900",
                weaponFocus.Icon,
                FeatureGroup.None,
                noFeature,
                weaponFocus.CreateAddFactOnLevelRange(oracle),
                improvedCritical.CreateAddFactOnLevelRange(oracle, 9),
                weaponFocusGreater.CreateAddFactOnLevelRange(oracle, 12));
            noFeature.Feature = feat;
            feat.WeaponFocus = weaponFocus;
            feat.WeaponFocusGreater = weaponFocusGreater;
            feat.improvedCritical = improvedCritical;
            return feat;
        }

        static BlueprintFeature combatHealer;
    }

    public class CanActInSurpriseRoundLogic : OwnedGameLogicComponent<UnitDescriptor>, IUnitInitiativeHandler
    {
        // The class to use for `MinLevel` and `MaxLevelInclusive`.
        // Optionally `AdditionalClasses` and `Archetypes` can be specified for more classes/archetypes.
        public BlueprintCharacterClass Class;

        // The min level in which to apply this feature.
        public int MinLevel = 1;

        public BlueprintCharacterClass[] AdditionalClasses = Array.Empty<BlueprintCharacterClass>();

        public BlueprintArchetype[] Archetypes = Array.Empty<BlueprintArchetype>();

        public void HandleUnitRollsInitiative(RuleInitiativeRoll rule)
        {
            if (rule.Initiator.Descriptor != Owner) return;

            int level = ReplaceCasterLevelOfAbility.CalculateClassLevel(Class, AdditionalClasses, Owner, Archetypes);
            if (level < MinLevel) return;

            // Are there other units not waiting on inititative?
            foreach (var unit in Game.Instance.State.Units.InCombat().CombatStates())
            {
                if (unit.IsWaitingInitiative) continue;

                // Another unit isn't waiting, but we just rolled, so it must be the surprise round.
                // Let us act too.
                Log.Write($"Can act in surprise round: {Owner.CharacterName}, because other unit could: {unit.Unit.Descriptor.CharacterName}");
                unit.Cooldown.Initiative = 0;
            }
        }
    }

    public class WeaponMasteryCustomSelection : BlueprintParametrizedFeature, IFeatureSelection
    {
        public BlueprintFeature WeaponFocus, WeaponFocusGreater, improvedCritical;

        public WeaponMasteryCustomSelection()
        {
            ParameterType = FeatureParameterType.WeaponCategory;
            RequireProficiency = true;
            CustomParameterVariants = BlueprintParameterVariants = Array.Empty<BlueprintScriptableObject>();
        }

        bool IFeatureSelection.CanSelect(UnitDescriptor unit, LevelUpState state, FeatureSelectionState selectionState, IFeatureSelectionItem item)
        {
            foreach (var fact in unit.Progression.Features.Enumerable)
            {
                if (fact.Param == item.Param &&
                    fact.Blueprint == WeaponFocus ||
                    fact.Blueprint == WeaponFocusGreater ||
                    fact.Blueprint == improvedCritical)
                {
                    return false;
                }
            }
            return base.CanSelect(unit, state, selectionState, item);
        }
    }

    [AllowedOn(typeof(BlueprintBuff))]
    public class RerollSaveAgainstAbilities : BuffLogic, IInitiatorRulebookHandler<RuleRollD20>
    {
        public BlueprintAbilityResource RequiredResource;
        public int SavingThrowBonus;
        public ModifierDescriptor BonusDescriptor;
        public SpellDescriptorWrapper Descriptor; // optional spell descriptor to save against

        public static RerollSaveAgainstAbilities Create(BlueprintAbilityResource resource, int bonus, ModifierDescriptor bonusDescriptor, SpellDescriptor spellDescriptor = SpellDescriptor.None)
        {
            var r = Helpers.Create<RerollSaveAgainstAbilities>();
            r.SavingThrowBonus = bonus;
            r.BonusDescriptor = bonusDescriptor;
            r.Descriptor = spellDescriptor;
            return r;
        }

        public void OnEventAboutToTrigger(RuleRollD20 evt)
        {
            var rule = Rulebook.CurrentContext.PreviousEvent as RuleSavingThrow;
            if (rule == null) return;

            // Is this saving throw suitable for reroll?
            var descriptor = rule.Reason.Context?.SpellDescriptor ?? SpellDescriptor.None;
            if (Descriptor != SpellDescriptor.None && !descriptor.Intersects(Descriptor)) return;

            // Note: RuleSavingThrow rolls the D20 before setting the stat bonus, so we need to pass it in.
            // (The game's ModifyD20 component has a bug because of this.)
            var modValue = Owner.Stats.GetStat(rule.StatType);
            var statValue = (modValue as ModifiableValueAttributeStat)?.Bonus ?? modValue.ModifiedValue;
            if (!rule.IsSuccessRoll(evt.PreRollDice(), statValue - rule.StatValue))
            {
                if (SavingThrowBonus > 0)
                {
                    var stats = evt.Initiator.Stats;
                    rule.AddTemporaryModifier(stats.SaveWill.AddModifier(SavingThrowBonus, this, BonusDescriptor));
                    rule.AddTemporaryModifier(stats.SaveReflex.AddModifier(SavingThrowBonus, this, BonusDescriptor));
                    rule.AddTemporaryModifier(stats.SaveFortitude.AddModifier(SavingThrowBonus, this, BonusDescriptor));
                }
                evt.SetReroll(1, true);
                Owner.Resources.Spend(RequiredResource, 1);
                if (Owner.Resources.GetResourceAmount(RequiredResource) == 0) Buff.Remove();
            }
        }

        public void OnEventDidTrigger(RuleRollD20 evt) { }
    }

    [ComponentName("Empower spell from list")]
    [AllowedOn(typeof(BlueprintUnitFact))]
    public class AutoMetamagicExtraCost : MetamagicRodMechanics, IInitiatorRulebookHandler<RuleCalculateAbilityParams>, IInitiatorRulebookHandler<RuleCastSpell>
    {
        public int ExtraSlotCost = 1;
        // This can be recovered from the ability, but it's easier to store it directly.
        public BlueprintAbilityResource RequiredResource;

        BlueprintAbility nextSpell;

        public AutoMetamagicExtraCost()
        {
            MaxSpellLevel = 9;
        }

        public new void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            var spellbook = evt.Spellbook;
            var spellData = evt.AbilityData;
            if (spellbook != null)
            {
                var spellLevel = spellbook.GetSpellLevel(spellData);
                var slots = spellbook.GetSpontaneousSlots(spellLevel);
                Log.Write($"Available spontaneous slots at level {spellLevel}: {slots}");
                if (slots >= 2)
                {
                    base.OnEventAboutToTrigger(evt);
                    nextSpell = evt.Spell;
                }
            }
        }

        public new void OnEventDidTrigger(RuleCastSpell evt)
        {
            if (evt.Spell.Blueprint != nextSpell) return;
            nextSpell = null;

            var amount = Owner.Resources.GetResourceAmount(RequiredResource);
            base.OnEventDidTrigger(evt);
            var currentAmount = Owner.Resources.GetResourceAmount(RequiredResource);
            if (currentAmount != amount)
            {
                // Expend the extra slot.
                Log.Write($"Spending extra slot for {evt.Spell}, spent {amount - currentAmount} resources");
                evt.Spell.SpendFromSpellbook();
            }
        }
    }


    [AllowedOn(typeof(BlueprintUnitFact))]
    public class OffensiveCombatTraining : RuleInitiatorLogicComponent<RuleCalculateCMB>
    {
        public CombatManeuver Maneuver;

        // The class to use for BAB.
        // Optionally `AdditionalClasses` and `Archetypes` can be specified for more classes/archetypes.
        public BlueprintCharacterClass Class;

        public BlueprintCharacterClass[] AdditionalClasses = Array.Empty<BlueprintCharacterClass>();

        public BlueprintArchetype[] Archetypes = Array.Empty<BlueprintArchetype>();

        public override void OnEventAboutToTrigger(RuleCalculateCMB evt)
        {
            if (evt.Type == Maneuver)
            {
                evt.Base.ReplaceBAB = ReplaceCasterLevelOfAbility.CalculateClassLevel(Class, AdditionalClasses, Owner, Archetypes);
            }
        }

        public override void OnEventDidTrigger(RuleCalculateCMB evt) { }
    }

    [Harmony12.HarmonyPatch(typeof(UnitCombatState), "get_IsFullAttackRestrictedBecauseOfMoveAction")]
    static class UnitCombatState_IsFullAttackRestrictedBecauseOfMoveAction_Patch
    {
        static void Postfix(UnitCombatState __instance, ref bool __result)
        {
            if (!__result) return;

            try
            {
                var unit = __instance.Unit.Descriptor;
                if (unit.Progression.CharacterLevel == 20 && unit.HasFact(BattleMystery.finalRevelation))
                {
                    __result = false;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }


    [AllowedOn(typeof(BlueprintUnitFact))]
    public class ACBonusAgainstCriticalHits : RuleTargetLogicComponent<RuleCalculateAC>
    {
        public int Bonus;
        public ModifierDescriptor Descriptor;

        public override void OnEventAboutToTrigger(RuleCalculateAC evt)
        {
            if (evt.IsCritical)
            {
                evt.AddTemporaryModifier(Owner.Stats.AC.AddModifier(Bonus, this, Descriptor));
            }
        }

        public override void OnEventDidTrigger(RuleCalculateAC evt) { }
    }

    public class AbilityCustomSurprisingCharge : AbilityCustomLogic, IAbilityMinRangeProvider, IAbilityTargetChecker
    {
        public override bool IsEngageUnit => true;

        public override IEnumerator<AbilityDeliveryTarget> Deliver(AbilityExecutionContext context, TargetWrapper targetWrapper)
        {
            Log.Write($"{GetType().Name}::Deliver");
            var caster = context.Caster;
            float maxDistance = GetMaxRangeMeters(caster);
            var startPoint = caster.Position;
            var endPoint = targetWrapper.IsUnit ? targetWrapper.Unit.Position : targetWrapper.Point;
            caster.View.StopMoving();
            var agent = caster.View.AgentASP;
            agent.IsCharging = true;
            agent.ForcePath(new ForcedPath(new List<Vector3> { startPoint, endPoint }));
            caster.Descriptor.State.IsCharging = true;
            var passedDistance = 0f;
            // TODO: right after loading, this doesn't work. But once the unit has moved, it does.
            Log.Write($"{GetType().Name}::Deliver agent.IsReallyMoving? {agent.IsReallyMoving}");
            while (agent.IsReallyMoving)
            {
                var currentMaxSpeedOverride = agent.MaxSpeedOverride ?? 0f;
                agent.MaxSpeedOverride = Math.Max(currentMaxSpeedOverride, caster.CombatSpeedMps * 4f);
                passedDistance += (caster.Position - caster.PreviousPosition).magnitude;
                if (passedDistance > maxDistance) break;
                var newEndPoint = targetWrapper.IsUnit ? targetWrapper.Unit.Position : targetWrapper.Point;
                var obstacle = ObstacleAnalyzer.TraceAlongNavmesh(caster.Position, newEndPoint);
                if (obstacle != newEndPoint)
                {
                    Log.Write($"{GetType().Name}::Deliver obstacle {obstacle}, newEndPoint {newEndPoint}");
                    break;
                }
                if (newEndPoint != endPoint)
                {
                    endPoint = newEndPoint;
                    agent.ForcePath(new ForcedPath(new List<Vector3> { caster.Position, endPoint }));
                }
                Log.Write($"{GetType().Name}::Deliver yield");
                yield return null;
                Log.Write($"{GetType().Name}::Deliver agent.IsReallyMoving? {agent.IsReallyMoving}");
            }
        }

        public override void Cleanup(AbilityExecutionContext context)
        {
            var caster = context.Caster;
            var agent = caster.View.AgentASP;
            agent.IsCharging = false;
            agent.MaxSpeedOverride = null;
            caster.Descriptor.State.IsCharging = false;
        }

        public float GetMinRangeMeters(UnitEntityData caster) => 10.Feet().Meters + caster.View.Corpulence;

        static float GetMaxRangeMeters(UnitEntityData caster) => caster.CombatSpeedMps * 6f;

        public bool CanTarget(UnitEntityData caster, TargetWrapper targetWrapper)
        {
            var targetUnit = targetWrapper.Unit;
            var position = targetUnit?.Position ?? targetWrapper.Point;
            var magnitude = (position - caster.Position).magnitude - (targetUnit?.View.Corpulence ?? 0f);
            if (magnitude > GetMaxRangeMeters(caster) || magnitude < GetMinRangeMeters(caster))
            {
                return false;
            }
            var obstacle = ObstacleAnalyzer.TraceAlongNavmesh(caster.Position, position);
            return obstacle == position;
        }
    }

}
