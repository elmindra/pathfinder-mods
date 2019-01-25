// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UI.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.Visual.Sound;
using Newtonsoft.Json;

namespace EldritchArcana
{
    static class OracleCurses
    {
        static LibraryScriptableObject library => Main.library;
        static BlueprintCharacterClass oracle => OracleClass.oracle;
        static BlueprintCharacterClass[] oracleArray => OracleClass.oracleArray;

        internal static BlueprintFeatureSelection CreateSelection()
        {
            var selection = Helpers.CreateFeatureSelection("OracleCurseSelection", "Curse",
                "Each oracle is cursed, but this curse comes with a benefit as well as a hindrance.This choice is made at 1st level, and once made, it cannot be changed.The oracle’s curse cannot be removed or dispelled without the aid of a deity.An oracle’s curse is based on her oracle level plus one for every two levels or Hit Dice other than oracle.Each oracle must choose one of the following curses.",
                "b4c9164ec94a47589eeb2a6688b24320",
                null,
                UpdateLevelUpDeterminatorText.Group);

            // Note: most curses can't be implemented as written, so they've undergone some
            // adaptation to work in PK:K, attempting to capture the sprit and RP flavor.
            //
            // Powerful (3rd party) ones we could add:
            // - Branded, Frenetic (Cha to Fortitude/Reflex respectively)
            var curses = new List<BlueprintProgression>();

            curses.Add(CreateBlackenedCurse());

            curses.Add(CreateCloudedVision());

            curses.Add(CreateCovetousCurse());

            curses.Add(CreateDeafCurse());

            curses.Add(CreateHauntedCurse());

            curses.Add(CreateLameCurse());

            curses.Add(CreateTonguesCurse());

            curses.Add(CreateWastingCurse());

            // Note: BlueprintProgression.CalcLevels is patched to handle curse progression.
            // (Curses should advance by +1/2 per character level in other classes.)
            foreach (var curse in curses)
            {
                BlueprintProgression_CalcLevel_Patch.onCalcLevel.Add(curse, CalculateCurseLevel);
            }

            selection.SetFeatures(curses);
            return selection;
        }

        internal static int CalculateCurseLevel(UnitDescriptor unit)
        {
            // Calculate curse level: Oracle levels + 1/2 other levels.
            int oracleLevel = unit.Progression.GetClassLevel(OracleClass.oracle);
            int characterLevel = unit.Progression.CharacterLevel;
            return oracleLevel + (characterLevel - oracleLevel) / 2;
        }

        static BlueprintProgression CreateCloudedVision()
        {
            // Clouded Vision: targets greater than max sight distance are treated as having concealment
            // (similar to the fog spell). Can't target spells at creatures beyond max range.
            //
            // Distances are adjusted to make sense in game, which generally operates in closer quarters.
            // The game significantly reduces spell range: close/medium/long is 30/40/50ft, with no caster
            // level increase, but in PnP it starts at 25/100/400ft and those increase with level.
            //
            // For that reason, clouded vision is reduced from 30/60ft to 20/30ft. That way it still has
            // an effect (otherwise, 60ft would effectively remove the penalty completely).
            //
            // Note: I tried altering fog of war settings, but this seems to break some of the game's
            // cutscene scripts, so those patches are removed.
            var curse = Helpers.CreateProgression("OracleCurseCloudedVision", "Clouded Vision",
                "Your eyes are obscured, making it difficult for you to see.\nYou cannot see anything beyond 20 feet. " +
                "Targets beyond this range have concealment, and you cannot target any point past that range.",
                "a4556beb36e742db9361c50587de9514",
                Helpers.GetIcon("46fd02ad56c35224c9c91c88cd457791"), // blindness
                FeatureGroup.None,
                Helpers.Create<CloudedVisionLogic>());

            var level5 = Helpers.CreateFeature($"{curse.name}Level5", curse.Name,
                "At 5th level, your vision distance increases to 30 feet.",
                "9ee32f1d54984aa7b635891fa778205d", curse.Icon, FeatureGroup.None);

            var level10 = Helpers.CreateFeature($"{curse.name}Blindsense", "Blindsense",
                "At 10th level, you gain blindsense out to a range of 30 feet.",
                "b92a0776b8984f19b6ae0a83c4b90579",
                Helpers.GetIcon("30e5dc243f937fc4b95d2f8f4e1b7ff3"), // see invisible
                FeatureGroup.None,
                Helpers.Create<Blindsense>(b => b.Range = 30.Feet()));

            var level15 = Helpers.CreateFeature($"{curse.name}Blindsight", "Blindsight",
                "At 15th level, you gain blindsight out to a range of 15 feet.",
                "69c483cbe48647f2af576275c2a30b59",
                Helpers.GetIcon("4cf3d0fae3239ec478f51e86f49161cb"), // true seeing
                FeatureGroup.None,
                Helpers.Create<Blindsense>(b => { b.Range = 15.Feet(); b.Blindsight = true; }));

            curse.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(5, level5),
                Helpers.LevelEntry(10, level10),
                Helpers.LevelEntry(15, level15)
            };
            curse.UIGroups = Helpers.CreateUIGroups(level5, level10, level15);
            curse.Classes = oracleArray;
            return curse;
        }

        static BlueprintProgression CreateDeafCurse()
        {
            var pcVoiceNone = library.Get<BlueprintUnitAsksList>("e7b22776ba8e2b84eaaff98e439639a7");
            var curse = Helpers.CreateProgression("OracleCurseDeaf", "Deaf",
                "You cannot hear and suffer all of the usual penalties for being deafened: -4 penalty on initiative and -4 perception. You cast all of your spells as if they were modified by the Silent Spell feat. This does not increase their level or casting time.",
                "a69e00e4787d4f4c9bf38540c88fce13",
                Helpers.GetIcon("c3893092a333b93499fd0a21845aa265"), // sound burst
                FeatureGroup.None,
                AddStatBonusOnCurseLevel.Create(StatType.Initiative, -4, ModifierDescriptor.Penalty, maxLevel: 4),
                AddStatBonusOnCurseLevel.Create(StatType.SkillPerception, -4, ModifierDescriptor.Penalty, maxLevel: 4),
                Helpers.Create<ReplaceAsksList>(r => r.Asks = pcVoiceNone),
                Helpers.Create<OracleCurseLogic>(o => o.Curse = OracleCurse.Deaf));
            curse.Classes = oracleArray;
            Main.ApplyPatch(typeof(AbilityData_VoiceIntensity_Patch), "Oracle Deaf curse, cast using Silent Spell");

            var level5 = Helpers.CreateFeature($"{curse.name}Level5", curse.Name,
                "At 5th level, you no longer receive a penalty on Perception checks, and the initiative penalty for being deaf is reduced to –2.",
                "373c4a9b4d304cbfa77472613010a367", curse.Icon, FeatureGroup.None,
                AddStatBonusOnCurseLevel.Create(StatType.Initiative, -2, ModifierDescriptor.Penalty, minLevel: 5, maxLevel: 9));

            var level10 = Helpers.CreateFeature($"{curse.name}Level10", curse.Name,
                "At 10th level, you receive a +3 competence bonus on Perception checks, and you do not suffer any penalty on initiative checks due to being deaf.",
                "649e4b7f719b4a5d93c322d12ed4ae5b",
                Helpers.GetIcon("c927a8b0cd3f5174f8c0b67cdbfde539"), // remove blindness
                FeatureGroup.None,
                AddStatBonusOnCurseLevel.Create(StatType.SkillPerception, 3, ModifierDescriptor.Competence, minLevel: 10));

            var tremorsense = Helpers.CreateFeature($"{curse.name}Tremorsense", "Tremorsense",
                "At 15th level, you gain tremorsense out to a range of 30 feet.",
                "26c9d319adb04110b4ee687a3d573190",
                Helpers.GetIcon("30e5dc243f937fc4b95d2f8f4e1b7ff3"), // see invisible
                FeatureGroup.None,
                Helpers.Create<Blindsense>(b => b.Range = 30.Feet()));

            curse.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(5, level5),
                Helpers.LevelEntry(10, level10),
                Helpers.LevelEntry(15, tremorsense)
            };
            curse.UIGroups = Helpers.CreateUIGroups(level5, level10, tremorsense);
            return curse;
        }

        static BlueprintProgression CreateBlackenedCurse()
        {
            var burningHands = library.Get<BlueprintAbility>("4783c3709a74a794dbe7c8e7e0b1b038");
            var curse = Helpers.CreateProgression("OracleCurseBlackened", "Blackened",
                "Your hands and forearms are shriveled and blackened, as if you had plunged your arms into a blazing fire, and your thin, papery skin is sensitive to the touch.\n" +
                "You take a –4 penalty on weapon attack rolls, but you add burning hands to your list of spells known.",
                "753f68b73c73472db713c06057a6009f",
                burningHands.Icon,
                FeatureGroup.None);
            curse.Classes = oracleArray;

            var attackPenalty1 = Helpers.CreateFeature($"{curse.name}Level1", curse.Name, curse.Description,
                "a32003ed18444246bb6a92a79bb478b9",
                curse.Icon,
                FeatureGroup.None,
                Helpers.Create<AttackTypeAttackBonus>(a =>
                {
                    a.Type = AttackTypeAttackBonus.WeaponRangeType.Normal;
                    a.Descriptor = ModifierDescriptor.Penalty;
                    a.Value = 1; // Value and AttackBonus are multiplied
                    a.AttackBonus = -4;
                }));

            var scorchingRay = library.Get<BlueprintAbility>("cdb106d53c65bbc4086183d54c3b97c7");
            var burningArc = library.Get<BlueprintAbility>("eaac3d36e0336cb479209a6f65e25e7c");
            var level5 = Helpers.CreateFeature($"{curse.name}Level5", curse.Name,
                "At 5th level, add scorching ray and burning arc to your list of spells known.",
                "a27670ecc84f4b1d9dd9d434eeb1e782",
                scorchingRay.Icon,
                FeatureGroup.None,
                scorchingRay.CreateAddKnownSpell(oracle, 2),
                burningArc.CreateAddKnownSpell(oracle, 2));

            var wallOfFire = FireSpells.wallOfFire;
            var level10 = Helpers.CreateFeature($"{curse.name}Level10", curse.Name,
                "At 10th level, add wall of fire to your list of spells known and your penalty on weapon attack rolls is reduced to –2.",
                "3fb920932967478687bae1d71ffe5c97",
                wallOfFire.Icon,
                FeatureGroup.None,
                wallOfFire.CreateAddKnownSpell(oracle, 4),
                Helpers.Create<RemoveFeatureOnApply>(r => r.Feature = attackPenalty1),
                Helpers.Create<AttackTypeAttackBonus>(a =>
                {
                    a.Type = AttackTypeAttackBonus.WeaponRangeType.Normal;
                    a.Descriptor = ModifierDescriptor.Penalty;
                    a.Value = 1; // Value and AttackBonus are multiplied
                    a.AttackBonus = -2;
                }));

            curse.SetComponents(
                burningHands.CreateAddKnownSpell(oracle, 1),
                Helpers.Create<AddFeatureIfHasFact>(a => { a.Not = true; a.CheckedFact = level10; a.Feature = attackPenalty1; }));

            var delayedBlastFireball = FireSpells.delayedBlastFireball;
            var level15 = Helpers.CreateFeature($"{curse.name}Level15", curse.Name,
                "At 15th level, add delayed blast fireball to your list of spells known.",
                "330d3fca05884799aef73b546dd27aa5",
                delayedBlastFireball.Icon,
                FeatureGroup.None,
                delayedBlastFireball.CreateAddKnownSpell(oracle, 7));

            burningHands.AddRecommendNoFeature(curse);
            scorchingRay.AddRecommendNoFeature(curse);
            burningArc.AddRecommendNoFeature(curse);
            delayedBlastFireball.AddRecommendNoFeature(curse);

            curse.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(5, level5),
                Helpers.LevelEntry(10, level10),
                Helpers.LevelEntry(15, level15),
            };
            curse.UIGroups = Helpers.CreateUIGroups(level5, level10, level15);
            return curse;
        }

        static BlueprintProgression CreateCovetousCurse()
        {
            // Note: this was reworked to be based on wealth (instead of fancy clothes).
            // Also the value was increased 2x because wealth doesn't require spending it as clothes would,
            // and gold is shared by the party.
            //
            var debuff = library.CopyAndAdd<BlueprintBuff>("4e42460798665fd4cb9173ffa7ada323",
                "OracleCurseCovetousSickened", "be50bd73d0fd4c22be3c26954e097c8c");

            var curse = Helpers.CreateProgression("OracleCurseCovetous", "Covetous",
                "You find yourself drawn to the luster of wealthy living.\nYou must have a gold reserve worth at least 100 gp + 200 gp per character level you have beyond 1st. If you do not have sufficient wealth, you feel a strong desire (but are not compelled) to sell existing items or steal from others to obtain it. You are sickened whenever you do not meet this requirement. Use Magic Device becomes a class skill for you.",
                "e42c5119978c438b9c445a90198632b0",
                library.Get<BlueprintItemEquipmentRing>("ba4276197d204314d9b4a69a4366b2a3").Icon, // Gold ring
                FeatureGroup.None,
                Helpers.Create<AddClassSkill>(a => a.Skill = StatType.SkillUseMagicDevice),
                CovetousCurseLogic.Create(debuff));
            curse.Classes = oracleArray;

            debuff.SetDescription($"{debuff.Description}\n{curse.Name}: {curse.Description}");

            var level5 = Helpers.CreateFeature($"{curse.name}Level5", curse.Name,
                $"At 5th level, you gain a +4 insight bonus on {UIUtility.GetStatText(StatType.SkillUseMagicDevice)} checks.",
                "04d79bcdcf7d44ea97fd5f09763bb7bc",
                Helpers.GetSkillFocus(StatType.SkillUseMagicDevice).Icon,
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SkillUseMagicDevice, 4, ModifierDescriptor.Insight));

            // Note: reworked; "Fabricate" spell is not in game.
            // It's now the ability to use UMD to identify items (based on the identify bonus from level 5).
            var level10 = Helpers.CreateFeature($"{curse.name}Level10", curse.Name,
                $"At 10th level, you can use your {UIUtility.GetStatText(StatType.SkillUseMagicDevice)} skill to identify items.",
                "2a32af175975459b9a960b79cfcaaf64",
                Helpers.GetSkillFocus(StatType.SkillUseMagicDevice).Icon,
                FeatureGroup.None,
                Helpers.Create<IdentifySkillReplacement>(i => Helpers.SetField(i, "m_SkillType", (int)StatType.SkillUseMagicDevice)));

            // Note: reworked to Thievery since there's no steal checks against PC.
            var level15 = Helpers.CreateFeature($"{curse.name}Level15", curse.Name,
                $"At 15th level, you gain a +4 insight bonus on {UIUtility.GetStatText(StatType.SkillThievery)} checks.",
                "c761a8e5ac6e40c087678a3ede5d9bdd",
                Helpers.GetSkillFocus(StatType.SkillThievery).Icon,
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SkillThievery, 4, ModifierDescriptor.Insight));

            curse.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(5, level5),
                Helpers.LevelEntry(10, level10),
                Helpers.LevelEntry(15, level15)
            };
            curse.UIGroups = Helpers.CreateUIGroups(level5, level10, level15);
            return curse;
        }

        static BlueprintProgression CreateHauntedCurse()
        {
            // Note: bonus spells reworked, as none of them exist in game. New theme: invisibility spells.
            // (Most of these are situational in PF:K, except Greater Invisibility, which goes into a level 5 slot.)
            // Alternate ideas: Blur or Mirror Image, Phatasmal Web, ... ?

            // Should be: mage hand/ghost sound
            var vanish = library.Get<BlueprintAbility>("f001c73999fb5a543a199f890108d936");
            // Should be: minor image
            var invisibility = library.Get<BlueprintAbility>("89940cde01689fb46946b2f8cd7b66b7");
            // Should be: telekinesis
            var invisibilityGreater = library.Get<BlueprintAbility>("ecaa0def35b38f949bd1976a6c9539e0");
            // Should be: reverse gravity
            var invisibilityMass = library.Get<BlueprintAbility>("98310a099009bbd4dbdf66bcef58b4cd");

            var curse = Helpers.CreateProgression("OracleCurseHaunted", "Haunted",
                "Malevolent spirits follow you wherever you go, causing minor mishaps and strange occurrences (such as unexpected breezes, small objects moving on their own, and faint noises).\n" +
                "Retrieving any stored item from your gear requires a standard action, unless it would normally take longer.Any item you drop lands 10 feet away from you in a random direction.\n" +
                $"Add {vanish.Name} to your list of spells known.",
                "e2aa739f54c94f7199f550d7a499a2a0",
                Helpers.GetIcon("c83447189aabc72489164dfc246f3a36"), // frigid touch
                FeatureGroup.None,
                vanish.CreateAddKnownSpell(oracle, 1),
                Helpers.Create<HauntedCurseLogic>());
            curse.Classes = oracleArray;

            var level5 = Helpers.CreateFeature($"{curse.name}Level5", invisibility.Name,
                $"At 5th level, add {invisibility.Name} to your list of spells known.",
                "84247c143a9b4d478f4ac3241cce32ab",
                invisibility.Icon,
                FeatureGroup.None,
                invisibility.CreateAddKnownSpell(oracle, 2));

            var level10 = Helpers.CreateFeature($"{curse.name}Level10", invisibilityGreater.Name,
                $"At 10th level, add {invisibilityGreater.Name} to your list of spells known.",
                "bd62288494144997b3c32cbaa04b25ab",
                invisibilityGreater.Icon,
                FeatureGroup.None,
                invisibilityGreater.CreateAddKnownSpell(oracle, 5));

            var level15 = Helpers.CreateFeature($"{curse.name}Level15", invisibilityMass.Name,
                $"At 15th level, add {invisibilityMass.Name} to your list of spells known.",
                "90d84dca2e06494cae92566ede0ca6f0",
                invisibilityMass.Icon,
                FeatureGroup.None,
                invisibilityMass.CreateAddKnownSpell(oracle, 7));

            curse.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(5, level5),
                Helpers.LevelEntry(10, level10),
                Helpers.LevelEntry(15, level15)
            };
            curse.UIGroups = Helpers.CreateUIGroups(level5, level10, level15);

            Main.ApplyPatch(typeof(ItemsCollection_DropItem_Patch), "Haunted curse (moving items away)");

            return curse;
        }

        static BlueprintProgression CreateLameCurse()
        {
            Main.ApplyPatch(typeof(PartyEncumbranceController_UpdatePartyEncumbrance_Patch), "Lame curse (party speed not reduced by encumbrance");
            Main.ApplyPatch(typeof(UnitPartEncumbrance_GetSpeedPenalty_Patch), "Lame curse (speed not reduced by encumbrance)");

            var curse = Helpers.CreateProgression("OracleCurseLame", "Lame",
                "One of your legs is permanently wounded, reducing your base land speed by 10 feet if your base speed is 30 feet or more. If your base speed is less than 30 feet, your speed is reduced by 5 feet. Your speed is never reduced due to encumbrance.",
                "08f1f729406a43f5ab9fece5e92579b6",
                Helpers.GetIcon("f492622e473d34747806bdb39356eb89"), // slow
                FeatureGroup.None,
                Helpers.Create<OracleCurseLameSpeedPenalty>());

            curse.Classes = oracleArray;
            var fatigueImmunity = Helpers.CreateFeature("OracleCurseLameFatigueImmunity", "Immune to Fatigue",
                "At 5th level, you are immune to the fatigued condition (but not exhaustion).",
                "b2b9ef97c1b54faeb552247e731d7270",
                Helpers.GetIcon("e5aa306af9b91974a9b2f2cbe702f562"), // mercy fatigue
                FeatureGroup.None,
                UnitCondition.Fatigued.CreateImmunity(),
                SpellDescriptor.Fatigue.CreateBuffImmunity());

            var effortlessArmor = Helpers.CreateFeature("OracleCurseLameEffortlessArmor", "Effortless Armor",
                "At 10th level, your speed is never reduced by armor.",
                "fbe8560cf3f14cd58f380a8dc630b1c7",
                Helpers.GetIcon("e1291272c8f48c14ab212a599ad17aac"), // effortless armor
                FeatureGroup.None,
                // Conceptually similar to ArmorSpeedPenaltyRemoval, but doesn't need 2 ranks in the feat to work.
                AddMechanicsFeature.MechanicsFeatureType.ImmunToMediumArmorSpeedPenalty.CreateAddMechanics(),
                AddMechanicsFeature.MechanicsFeatureType.ImmunToArmorSpeedPenalty.CreateAddMechanics());

            var exhaustionImmunity = Helpers.CreateFeature("OracleCurseLameExhaustionImmunity", "Immune to Exhausted",
                "At 15th level, you are immune to the exhausted condition.",
                "be45e9251c134ac9baee97e1e3ffc30a",
                Helpers.GetIcon("25641bda25467224e930e8c70eaf9a83"), // mercy exhausted
                FeatureGroup.None,
                UnitCondition.Exhausted.CreateImmunity(),
                SpellDescriptor.Exhausted.CreateBuffImmunity());

            curse.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(5, fatigueImmunity),
                Helpers.LevelEntry(10, effortlessArmor),
                Helpers.LevelEntry(15, exhaustionImmunity)
            };
            curse.UIGroups = Helpers.CreateUIGroups(fatigueImmunity, effortlessArmor, exhaustionImmunity);

            return lameCurse = curse;
        }

        static BlueprintProgression CreateTonguesCurse()
        {
            // Tongues:
            // - the PC can't order party members in combat, unless the NPC has 1 rank in
            //   Knowledge: World (linguistics).
            // - once NPC levels up, this will be a non-issue
            // - disable the restriction until level 2 (e.g. so prologue isn't affected).
            //
            // Basically, it's only an issue until each new NPC is leveled up once.
            //
            // Since languages aren't implemented, this instead gives a bonus to knowledge skills.
            var linguistics = UIUtility.GetStatText(StatType.SkillKnowledgeWorld);
            var curse = Helpers.CreateProgression("OracleCurseTongues", "Tongues",
                "In times of stress or unease, you speak in tongues.\n" +
                "Pick one of the following languages: Abyssal, Aklo, Aquan, Auran, Celestial, Ignan, Infernal, or Terran.\n" +
                $"Whenever you are in combat, you can only speak and understand the selected language. This does not interfere with spellcasting, but it does apply to spells that are language dependent. You know the selected language, and gain a +2 bonus to {linguistics} representing your knowledge of otherworldly languages.\n" +
                $"If your party members have at least 1 rank in {linguistics} they can communicate with you in combat, allowing you to issue orders to them or vice versa.",
                "983b66fc844a496da24acbcbdceebede",
                Helpers.GetIcon("f09453607e683784c8fca646eec49162"), // shout
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SkillKnowledgeWorld, 2, ModifierDescriptor.UntypedStackable),
                Helpers.Create<OracleCurseLogic>(o => o.Curse = OracleCurse.Tongues));
            curse.Classes = oracleArray;

            var level5 = Helpers.CreateFeature($"{curse.name}Level5", "Bonus Language",
                $"At 5th level, you learn a new language, gaining an additional +2 {linguistics} representing this knowledge.",
                "7b08ed37b3034c94b5e00c7f507f1000",
                curse.Icon,
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SkillKnowledgeWorld, 2, ModifierDescriptor.UntypedStackable));

            var level10 = Helpers.CreateFeature($"{curse.name}Level10", "Understand All Languages",
                $"At 10th level, you can understand any spoken language, as if under the effects of tongues, even during combat.\nYou gain an additional +4 {linguistics} representing this knowledge.",
                "9a38bf8a757e4980b4d07298d7cdad52",
                curse.Icon,
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SkillKnowledgeWorld, 4, ModifierDescriptor.UntypedStackable));

            var level15 = Helpers.CreateFeature($"{curse.name}Level15", "Speak All Languages",
                $"At 15th level, you can speak and understand any language, but your speech is still restricted during combat.\nYou gain an additional +4 {linguistics} representing this knowledge.",
                "40ef931c66c94183a3a6b34454e6cde1",
                curse.Icon,
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SkillKnowledgeWorld, 4, ModifierDescriptor.UntypedStackable));

            curse.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(5, level5),
                Helpers.LevelEntry(10, level10),
                Helpers.LevelEntry(15, level15)
            };
            curse.UIGroups = Helpers.CreateUIGroups(level5, level10, level15);
            Main.ApplyPatch(typeof(UnitEntityData_IsDirectlyControllable_Patch), "Tongues curse (party members not controllable without 1 rank Knowledge: world)");
            return curse;
        }

        static BlueprintProgression CreateWastingCurse()
        {
            var curse = Helpers.CreateProgression("OracleCurseWasting", "Wasting",
                "Your body is slowly rotting away.\nYou take a –4 penalty on Charisma-based skill checks, except for Intimidate. You gain a +4 competence bonus on saves made against disease.",
                "12fcf38c71064c9a8e9a79e5d7c115bc",
                Helpers.GetIcon("4e42460798665fd4cb9173ffa7ada323"), // sickened
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.CheckDiplomacy, -4, ModifierDescriptor.Penalty),
                Helpers.CreateAddStatBonus(StatType.CheckBluff, -4, ModifierDescriptor.Penalty),
                Helpers.CreateAddStatBonus(StatType.SkillUseMagicDevice, -4, ModifierDescriptor.Penalty),
                Helpers.Create<SavingThrowBonusAgainstDescriptor>(s =>
                {
                    s.Value = 4;
                    s.SpellDescriptor = SpellDescriptor.Disease;
                    s.ModifierDescriptor = ModifierDescriptor.Competence;
                }));

            curse.Classes = oracleArray;

            var level5 = Helpers.CreateFeature($"{curse.name}SickenImmunity", "Immune to Sickened",
                "At 5th level, you are immune to the sickened condition (but not nauseated).",
                "a325e582ba97456784cb3c0e206de8e0",
                Helpers.GetIcon("7ee2ef06226a4884f80b7647a2aa2dee"), // mercy sickened
                FeatureGroup.None,
                UnitCondition.Sickened.CreateImmunity(),
                SpellDescriptor.Sickened.CreateBuffImmunity());

            var level10 = Helpers.CreateFeature($"{curse.name}DiseaseImmunity", "Immune to Disease",
                "At 10th level, you gain immunity to disease.",
                "ffebfb47717246c58304a01223c26086",
                Helpers.GetIcon("3990a92ce97efa3439e55c160412ce14"), // mercy diseased
                FeatureGroup.None,
                SpellDescriptor.Disease.CreateSpellImmunity(),
                SpellDescriptor.Disease.CreateBuffImmunity());

            var level15 = Helpers.CreateFeature($"{curse.name}NauseatedImmunity", "Immune to Nauseated",
                "At 15th level, you are immune to the nauseated condition.",
                "9fb165ed9340414085930eb72b0661b6",
                Helpers.GetIcon("a0cacf71d872d2a42ae3deb6bf977962"), // mercy nauseated
                FeatureGroup.None,
                UnitCondition.Nauseated.CreateImmunity(),
                SpellDescriptor.Nauseated.CreateBuffImmunity());

            curse.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(5, level5),
                Helpers.LevelEntry(10, level10),
                Helpers.LevelEntry(15, level15)
            };
            curse.UIGroups = Helpers.CreateUIGroups(level5, level10, level15);
            curse.Classes = oracleArray;
            return curse;
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

        // Used by Flame Mystery Cinder Dance (to mark it incompatible).
        internal static BlueprintProgression lameCurse;
    }

    class CovetousCurseLogic : OwnedGameLogicComponent<UnitDescriptor>, IUnitGainLevelHandler, IItemsCollectionHandler
    {
        public BlueprintBuff Debuff;

        [JsonProperty]
        Buff appliedBuff;

        [JsonProperty]
        long lastMoney = 0;

        public static CovetousCurseLogic Create(BlueprintBuff debuff)
        {
            var c = Helpers.Create<CovetousCurseLogic>();
            c.Debuff = debuff;
            return c;
        }

        public override void OnTurnOn()
        {
            CheckCovetous(false);
            base.OnTurnOn();
        }

        public override void OnTurnOff()
        {
            appliedBuff?.Remove();
            appliedBuff = null;
            base.OnTurnOff();
        }

        public void HandleUnitGainLevel(UnitDescriptor unit, BlueprintCharacterClass @class)
        {
            if (unit == Owner) CheckCovetous(false);
        }

        public void HandleItemsAdded(ItemsCollection collection, ItemEntity item, int count) => CheckCovetous(true);

        public void HandleItemsRemoved(ItemsCollection collection, ItemEntity item, int count) => CheckCovetous(true);

        void CheckCovetous(bool checkMoneyChanged)
        {
            try
            {
                long money;
                if (Owner.IsPlayerFaction)
                {
                    money = Game.Instance.Player.Money;
                }
                else
                {
                    money = Owner.Inventory.Count(BlueprintRoot.Instance.SystemMechanics.GoldCoin);
                }

                if (checkMoneyChanged && money == lastMoney) return;
                lastMoney = money;

                var requiredMoney = Owner.Progression.CharacterLevel * 200 + 100;
                Log.Append($"Covetous curse: check {Owner.CharacterName}, money {money}, requires {requiredMoney}");
                if (money < requiredMoney)
                {
                    if (appliedBuff == null)
                    {
                        appliedBuff = Owner.AddBuff(Debuff, Owner.Unit);
                        if (appliedBuff == null) return;
                        appliedBuff.IsNotDispelable = true;
                        appliedBuff.IsFromSpell = false;
                    }
                }
                else
                {
                    appliedBuff?.Remove();
                    appliedBuff = null;
                }

            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowedOn(typeof(BlueprintUnit))]
    [AllowMultipleComponents]
    public class OracleCurseLogic : OwnedGameLogicComponent<UnitDescriptor>
    {
        public OracleCurse Curse;

        public override void OnTurnOn() => Owner.Ensure<UnitPartOracleCurse>().Curses |= Curse;

        public override void OnTurnOff() => Owner.Ensure<UnitPartOracleCurse>().Curses &= ~Curse;
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowMultipleComponents]
    public class OracleCurseLameSpeedPenalty : OracleCurseLogic
    {
        [JsonProperty]
        private ModifiableValue.Modifier m_Modifier;

        public OracleCurseLameSpeedPenalty()
        {
            Curse = OracleCurse.Lame;
        }

        public override void OnTurnOn()
        {
            var speed = Owner.Stats.Speed;
            var penalty = speed.Racial >= 30 ? -10 : -5;
            m_Modifier = speed.AddModifier(penalty, this, ModifierDescriptor.Penalty);
            base.OnTurnOn();
        }

        public override void OnTurnOff()
        {
            m_Modifier?.Remove();
            m_Modifier = null;
            base.OnTurnOff();
        }
    }

    [Harmony12.HarmonyPatch(typeof(UnitPartEncumbrance), "GetSpeedPenalty", typeof(UnitDescriptor), typeof(Encumbrance))]
    static class UnitPartEncumbrance_GetSpeedPenalty_Patch
    {
        static void Postfix(UnitDescriptor owner, Encumbrance encumbrance, ref int __result)
        {
            if (__result < 0 && owner.Get<UnitPartOracleCurse>()?.HasLame == true)
            {
                __result = 0;
            }
        }
    }
    // Kingmaker.Controllers.PartyEncumbranceController
    [Harmony12.HarmonyPatch(typeof(PartyEncumbranceController), "UpdatePartyEncumbrance", new Type[0])]
    static class PartyEncumbranceController_UpdatePartyEncumbrance_Patch
    {
        static bool Prefix()
        {
            try
            {
                var player = Game.Instance.Player;
                if (player.Party.Any(u => u.Get<UnitPartOracleCurse>()?.HasLame == true))
                {
                    if (player.Encumbrance != Encumbrance.Light)
                    {
                        player.Encumbrance = Encumbrance.Light;
                        EventBus.RaiseEvent((IPartyEncumbranceHandler p) => p.ChangePartyEncumbrance());
                    }
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


    [Harmony12.HarmonyPatch(typeof(UnitEntityData), "get_IsDirectlyControllable", new Type[0])]
    static class UnitEntityData_IsDirectlyControllable_Patch
    {
        static void Postfix(UnitEntityData __instance, ref bool __result)
        {
            try
            {
                if (!__result) return;
                if (Main.settings?.RelaxTonguesCurse == true) return;

                // Tongues only has effect in combat.
                var self = __instance;
                if (!self.IsInCombat) return;

                // PC and PC's pet are always controllable.
                var mainChar = Game.Instance.Player.MainCharacter;
                var npc = self.Descriptor;
                if (self == mainChar || npc.Master == mainChar) return;

                if (npc.IsPet) npc = npc.Master.Value?.Descriptor ?? npc;

                // Don't apply the penalty until we've had an opportunity to level up.
                var pc = mainChar.Value.Descriptor;
                if (pc.Progression.CharacterLevel < 2) return;

                // If either PC or NPC has the Tongues curse, and the other party
                // doesn't have 1 rank in linguistics (Knowledge: World), then they
                // can't be communicated with in combat (i.e. ordered around).
                if (pc.Stats.SkillKnowledgeWorld.BaseValue == 0 && npc.Get<UnitPartOracleCurse>()?.HasTongues == true ||
                    npc.Stats.SkillKnowledgeWorld.BaseValue == 0 && pc.Get<UnitPartOracleCurse>()?.HasTongues == true)
                {
                    // Tongues curse: can't talk to this party member in combat.
                    __result = false;
                    return;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    // Note: this patch could be avoided if curses were BlueprintFeatures, with a component that
    // knows how to compute the curse level. But that doesn't look as nice in the UI as progressions.
    //
    // Another option is to have a Progression just for the UI (no functionality), but that seems
    // rather complex and might lead to issues (e.g. mismatch between UI and implementation.)
    [Harmony12.HarmonyPatch(typeof(BlueprintProgression), "CalcLevel", new Type[] { typeof(UnitDescriptor) })]
    static class BlueprintProgression_CalcLevel_Patch
    {
        internal static readonly Dictionary<BlueprintProgression, Func<UnitDescriptor, int>> onCalcLevel = new Dictionary<BlueprintProgression, Func<UnitDescriptor, int>>();

        static BlueprintProgression_CalcLevel_Patch() => Main.ApplyPatch(typeof(BlueprintProgression_CalcLevel_Patch), "Oracle curse advancement for non-oracle levels");

        static bool Prefix(BlueprintProgression __instance, UnitDescriptor unit, ref int __result)
        {
            try
            {
                Func<UnitDescriptor, int> calcLevel;
                if (onCalcLevel.TryGetValue(__instance, out calcLevel))
                {
                    __result = calcLevel(unit);
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

    [Flags]
    public enum OracleCurse
    {
        Tongues = 0x1,
        Haunted = 0x2,
        Lame = 0x4,
        Deaf = 0x8
    }

    // Used for curses where we need to be able to quickly look up information on the unit.
    // We don't track all curses this way, only those that need method patching (or that
    // need fast lookups for some other reason.) This is similar to UnitMechanicsFeatures.
    public class UnitPartOracleCurse : UnitPart
    {
        [JsonProperty]
        public OracleCurse Curses;

        [JsonProperty]
        public float CloudedVisionDistance;

        public bool HasCloudedVision => CloudedVisionDistance != 0;

        public bool HasTongues => (Curses & OracleCurse.Tongues) != 0;

        public bool HasHaunted => (Curses & OracleCurse.Haunted) != 0;

        public bool HasLame => (Curses & OracleCurse.Lame) != 0;

        public bool HasDeaf => (Curses & OracleCurse.Deaf) != 0;
    }

    [AllowedOn(typeof(BlueprintProgression))]
    public class CloudedVisionLogic : RuleInitiatorLogicComponent<RuleConcealmentCheck>, ILevelUpCompleteUIHandler
    {
        static CloudedVisionLogic()
        {
            var description = "Oracle Clouded Vision curse reduced range";
            Main.ApplyPatch(typeof(AbilityData_GetVisualDistance_Patch), description);
            Main.ApplyPatch(typeof(AbilityData_GetApproachDistance_Patch), description);
        }

        public override void OnTurnOn()
        {
            Log.Write($"{GetType().Name}::OnTurnOn");
            UpdateRange();
        }

        public override void OnTurnOff()
        {
            Log.Write($"{GetType().Name}::OnTurnOff");
            Owner.Ensure<UnitPartOracleCurse>().CloudedVisionDistance = 0;
        }

        public override void OnEventAboutToTrigger(RuleConcealmentCheck evt) { }

        public override void OnEventDidTrigger(RuleConcealmentCheck evt)
        {
            try
            {
                var initiator = evt.Initiator;
                var target = evt.Target;
                var part = initiator.Get<UnitPartOracleCurse>();
                if (part == null) return;

                var distance = initiator.DistanceTo(target);
                var sightDistance = part.CloudedVisionDistance;
                sightDistance += (initiator.View?.Corpulence ?? 0.5f) + (target?.View.Corpulence ?? 0.5f);
                if (distance > sightDistance)
                {
                    var isFar = distance > (sightDistance + 5.Feet().Meters);
                    set_Concealment(evt, isFar ? Concealment.Total : Concealment.Partial);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        void ILevelUpCompleteUIHandler.HandleLevelUpComplete(UnitEntityData unit, bool isChargen)
        {
            if (unit.Descriptor == Owner) UpdateRange();
        }

        void UpdateRange()
        {
            try
            {
                int level = ((BlueprintProgression)Fact.Blueprint).CalcLevel(Owner);
                var range = (level >= 5 ? 30 : 20).Feet().Meters;
                Owner.Ensure<UnitPartOracleCurse>().CloudedVisionDistance = range;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        static FastSetter set_Concealment = Helpers.CreateSetter<RuleConcealmentCheck>("Concealment");
    }

    [Harmony12.HarmonyPatch(typeof(AbilityData), "GetVisualDistance")]
    static class AbilityData_GetVisualDistance_Patch
    {
        static void Postfix(AbilityData __instance, ref float __result)
        {
            AbilityData_GetApproachDistance_Patch.Postfix(__instance, null, ref __result);
        }
    }

    [Harmony12.HarmonyPatch(typeof(AbilityData), "GetApproachDistance")]
    static class AbilityData_GetApproachDistance_Patch
    {
        internal static void Postfix(AbilityData __instance, UnitEntityData target, ref float __result)
        {
            try
            {
                var caster = __instance.Caster;
                var part = caster.Get<UnitPartOracleCurse>();
                if (part?.HasCloudedVision == true)
                {
                    var maxRange = part.CloudedVisionDistance + (caster.Unit.View?.Corpulence ?? 0.5f) + (target?.View.Corpulence ?? 0.5f);
                    var original = __result;
                    __result = Math.Min(maxRange, original);
                    Log.Write($"Clouded Vision: adjust range from {original} to {__result} (max range: {maxRange})");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    // This handles the "retrieving an item is a standard action".
    // "Moving dropped item randomly" is handled by ItemsCollection_DropItem_Patch, below.
    public class HauntedCurseLogic : OracleCurseLogic, IUnitEquipmentHandler
    {
        public HauntedCurseLogic()
        {
            Curse = OracleCurse.Haunted;
        }

        public void HandleEquipmentSlotUpdated(ItemSlot slot, ItemEntity previousItem)
        {
            if (slot.Owner == Owner && Owner.Unit.IsInCombat && slot.HasItem &&
                (bool)Helpers.GetField(typeof(ItemsCollection), null, "s_RaiseEvents"))
            {
                Log.Write($"Haunted curse: used standard action to retrieve item \"{slot.Item.Name}\"");
                Owner.Unit.CombatState.Cooldown.StandardAction += 6;
            }
        }
    }

    [Harmony12.HarmonyPatch(typeof(ItemsCollection), "DropItem", typeof(ItemEntity))]
    static class ItemsCollection_DropItem_Patch
    {
        static bool Prefix(ItemsCollection __instance, ItemEntity item)
        {
            try
            {
                var self = __instance;
                if (item.Collection != self || !item.Collection.IsPlayerInventory) return true;

                // We don't know which character dropped it, so we need to check everyone.
                // TODO: cache this check and update on level up?
                if (!Game.Instance.Player.Party.Any(p => p.Descriptor.Get<UnitPartOracleCurse>()?.HasHaunted == true))
                {
                    return true;
                }

                Log.Write("Haunted curse: moving dropped item randomly 10ft away.");
                var player = Game.Instance.Player.MainCharacter.Value;
                var position = player.Position + GeometryUtils.To3D(UnityEngine.Random.insideUnitCircle * 10.Feet().Meters);
                var drop = Game.Instance.EntityCreator.SpawnEntityView(BlueprintRoot.Instance.Prefabs.DroppedLootBag, position, player.View.transform.rotation, Game.Instance.State.LoadedAreaState.MainState);
                drop.Loot = new ItemsCollection();
                drop.IsDroppedByPlayer = true;
                self.Transfer(item, drop.Loot);
                return false;
            }
            catch (Exception e)
            {
                Log.Error(e);
                return true;
            }
        }
    }

    [Harmony12.HarmonyPatch(typeof(AbilityData), "get_VoiceIntensity")]
    static class AbilityData_VoiceIntensity_Patch
    {
        static void Postfix(AbilityData __instance, ref AbilityData.VoiceIntensityType __result)
        {
            var self = __instance;
            try
            {
                if (__result != AbilityData.VoiceIntensityType.None &&
                    self.Caster.Get<UnitPartOracleCurse>()?.HasDeaf == true)
                {
                    Log.Write("Deaf curse: cast spells using Silent Spell");
                    __result = AbilityData.VoiceIntensityType.None;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }


    [ComponentName("Add stat bonus based on character level")]
    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowedOn(typeof(BlueprintUnit))]
    [AllowMultipleComponents]
    public class AddStatBonusOnCurseLevel : AddStatBonusOnLevel
    {
        public static AddStatBonusOnCurseLevel Create(StatType stat, int value, ModifierDescriptor descriptor,
            int minLevel = 1, int maxLevel = 20)
        {
            var addStat = Helpers.Create<AddStatBonusOnCurseLevel>();
            addStat.Stat = stat;
            addStat.Value = value;
            addStat.Descriptor = descriptor;
            addStat.MinLevel = minLevel;
            addStat.MaxLevelInclusive = maxLevel;
            return addStat;
        }

        protected override bool CheckLevel(UnitDescriptor unit)
        {
            int level = OracleCurses.CalculateCurseLevel(unit);
            return level >= MinLevel && level <= MaxLevelInclusive;
        }
    }
}
