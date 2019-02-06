// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.AreaLogic;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.PubSubSystem;
using Kingmaker.ResourceLinks;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Buffs.Conditions;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.Utility;
using Newtonsoft.Json;
using static Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbilityResourceLogic;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class Bloodlines
    {
        internal static BlueprintFeature orcArcana;

        static LibraryScriptableObject library => Main.library;

        internal static void Load()
        {
            // TODO: the Djinni bloodlines might be interesting. Limited Wish without material component is a neat capstone.
            Main.SafeLoad(LoadOrcBloodline, "Orc Bloodline");

            Main.SafeLoad(LoadMetamagicAdept, "Metamagic Adept (Arcane Bloodline)");
        }

        static void LoadMetamagicAdept()
        {
            // Adds the Metamagic Adept power to the Arcane Bloodline.
            var bloodline = library.Get<BlueprintProgression>("4d491cf9631f7e9429444f4aed629791");

            var arcaneApotheosis = library.Get<BlueprintFeature>("2086d8c0d40e35b40b86d47e47fb17e4");
            arcaneApotheosis.AddComponent(Helpers.Create<FastMetamagicLogic>());
            arcaneApotheosis.SetDescription(arcaneApotheosis.Description + "\nYou can also add any metamagic feats that you know to your spells without increasing their casting time, although you must still expend higher-level spell slots.");

            var combatCastingAdept1 = library.Get<BlueprintFeature>("7aa83ee3526a946419561d8d1aa09e75");
            var combatCastingAdept2 = library.Get<BlueprintFeature>("3d7b19c8a1d03464aafeb306342be000");

            var feat = Helpers.CreateFeature("BloodlineArcaneMetamagicAdept", "Metamagic Adept",
                "At 3rd level, you can apply any one metamagic feat you know to a spell you are about to cast without increasing the casting time. You must still expend a higher-level spell slot to cast this spell. You can use this ability once per day at 3rd level and one additional time per day for every four sorcerer levels you possess beyond 3rd, up to five times per day at 19th level. At 20th level, this ability is replaced by arcane apotheosis.",
                "2edfee19cc574b17944308c3cee1da8b",
                combatCastingAdept1.Icon, FeatureGroup.None);

            var resource = Helpers.CreateAbilityResource($"{feat.name}Resource", "", "", "f49ed72de2ef4cc09319fbc008dd7a51", null);
            resource.SetIncreasedByLevelStartPlusDivStep(0, 3, 1, 4, 1, 0, 0, bloodline.Classes, bloodline.Archetypes);

            var buff = Helpers.CreateBuff($"{feat.name}Buff", feat.Name, feat.Description, "e2d7839c3d08437d8b233c08d79f3611",
                feat.Icon, null, Helpers.Create<FastMetamagicLogic>(f => { f.Once = true; f.Resource = resource; }));
            var ability = Helpers.CreateActivatableAbility($"{feat.name}ToggleAbility", feat.Name, feat.Description,
                "8f979bd26f8c49ab8fd5a099e2694cd5", feat.Icon, buff, AbilityActivationType.Immediately, CommandType.Free, null,
                resource.CreateActivatableResourceLogic(ResourceSpendType.Never));

            feat.SetComponents(resource.CreateAddAbilityResource(), ability.CreateAddFact());

            combatCastingAdept1.AddComponent(feat.CreateAddFactOnLevelRange(bloodline.Classes, bloodline.Archetypes, maxLevel: 19));
            combatCastingAdept1.SetDescription(combatCastingAdept1.Description + $"\n{feat.Name}: {feat.Description}");
            combatCastingAdept2.SetDescription(combatCastingAdept2.Description + $"\n{feat.Name}: {feat.Description}");
        }

        static void LoadOrcBloodline()
        {
            var orcBloodline = Helpers.CreateProgression("BloodlineOrcProgression",
                "Orc Bloodline",
                "The rage of your ancestors burns within you, and the taint of savage orc blood flows through your veins. Your anger is never far from the surface, giving you strength and driving you to seek greater power.\n" +
                "Bonus Feats of the Orc Bloodline: Diehard, Endurance, Great Fortitude, Intimidating Prowess, Improved Bull Rush, Power Attack, Toughness, Reach Spell.",
                "25bef329930b4830882b2cb51a46a535",
                Helpers.GetIcon("c99f3405d1ef79049bd90678a666e1d7"), // Half-Orc Ferocity
                FeatureGroup.BloodLine);

            var orcBonusFeatSelection = Helpers.CreateFeatureSelection("BloodlineOrcFeatSelection",
                "Bloodline Feat Selection",
                "At 7th level, and every six levels thereafter, a sorcerer receives one bonus feat, chosen from a list specific to each bloodline. The sorcerer must meet the prerequisites for these bonus feats.",
                "ddf4cc44b3554b17a8d9da4bf3682d1d",
                null,
                FeatureGroup.Feat,
                orcBloodline.PrerequisiteFeature());
            orcBonusFeatSelection.SetFeatures(new string[] {
                "86669ce8759f9d7478565db69b8c19ad", // diehard
                "54ee847996c25cd4ba8773d7b8555174", // endurance
                "79042cb55f030614ea29956177977c52", // great fortitude
                "d76497bfc48516e45a0831628f767a0f", // intimidating prowess
                "b3614622866fe7046b787a548bbd7f59", // improved bull rush (should be: improved overrun)
                "9972f33f977fc724c838e59641b2fca5", // power attack
                "d09b20029e9abfe4480b356c92095623", // toughness
                "46fad72f54a33dc4692d3b62eca7bb78", // reach spell (should be: widen spell)
            }.Select(id => library.Get<BlueprintFeature>(id)));

            var bloodlineBonusFeat = library.Get<BlueprintFeatureSelection>("3a60f0c0442acfb419b0c03b584e1394");
            bloodlineBonusFeat.SetFeatures(bloodlineBonusFeat.AllFeatures.AddToArray(orcBonusFeatSelection));

            var bloodlineSelection = Helpers.bloodlineSelection;
            var bloodlines = bloodlineSelection.AllFeatures.Cast<BlueprintProgression>().ToList();
            orcBloodline.Classes = new BlueprintCharacterClass[] { Helpers.sorcererClass, Helpers.magusClass };
            orcBloodline.Archetypes = new BlueprintArchetype[] { Helpers.eldritchScionArchetype };

            // Light sensitivitiy is added to this when we create the "Fearless" trait below.
            orcArcana = Helpers.CreateFeature("BloodlineOrcArcana", "Orc Bloodline Arcana",
                "You gain the orc subtype, including light sensitivity. Whenever you cast a spell that deals damage, that spell deals +1 point of damage per die rolled.",
                "a4cf06211879416ea82e10ca3062bb81",
                Helpers.GetIcon("ac04aa27a6fd8b4409b024a6544c4928"), // Gold dragon arcana, aka Fireball
                FeatureGroup.None,
                Helpers.Create<OrcBloodlineArcana>());

            var orcClassSkill = library.CopyAndAdd<BlueprintFeature>(
                "243f474ce797acb4086c2dbc58660c4a", // Fey class skill
                "BloodlineOrcClassSkill", "e4255be19b474fd4a86c8f14ff5b3c1f");

            // Bonus Spells:
            var spells = CreateSpellProgression(orcBloodline, new String[] {
                "4783c3709a74a794dbe7c8e7e0b1b038", // burning hands
                "4c3d08935262b6544ae97599b3a9556d", // bull's strength
                "97b991256e43bb140b263c326f690ce2", // rage
                FireSpells.wallOfFire.AssetGuid,
                "548d339ba87ee56459c98e80167bdf10", // cloudkill
                "27203d62eb3d4184c9aced94f22e1806", // transformation
                FireSpells.delayedBlastFireball.AssetGuid,
                "e788b02f8d21014488067bdd3ba7b325", // frightful aspect, should be: Iron Body
                FireSpells.meteorSwarm.AssetGuid,
            });

            var powers = new List<BlueprintFeatureBase>();
            Func<BlueprintFeature, BlueprintFeature> addPower = (power) =>
            {
                powers.Add(power);
                return power;
            };

            // 1st level is: Arcana, ClassSkill, and power
            // Powers are gained at levels: 1, 3, 9, 15, 20
            // Spells are gained at levels: 3, 5, 7, 9, 11, 13, 15, 17, 19
            orcBloodline.LevelEntries = new LevelEntry[] {
                Helpers.LevelEntry(1, orcArcana, orcClassSkill, CreateTouchOfRage(orcBloodline)),
                Helpers.LevelEntry(3, spells[0], addPower(CreateFearless(orcBloodline))),
                Helpers.LevelEntry(5, spells[1]),
                Helpers.LevelEntry(7, spells[2]),
                Helpers.LevelEntry(9, spells[3], addPower(CreateStrengthOfTheBeast(orcBloodline, 1))),
                Helpers.LevelEntry(11, spells[4]),
                Helpers.LevelEntry(13, spells[5], addPower(CreateStrengthOfTheBeast(orcBloodline, 2))),
                Helpers.LevelEntry(15, spells[6], addPower(CreatePowerOfGiants(orcBloodline))),
                Helpers.LevelEntry(17, spells[7]),
                Helpers.LevelEntry(19, spells[8]),
                Helpers.LevelEntry(20, addPower(CreateWarlordReborn(orcBloodline))),
            };
            orcBloodline.UIGroups = new UIGroup[] { Helpers.CreateUIGroup(spells), Helpers.CreateUIGroup(powers) };
            bloodlineSelection.SetFeatures(bloodlineSelection.AllFeatures.AddToArray(orcBloodline));
            //Log.Write(orcBloodline, showSelection: true);
        }

        static BlueprintFeature CreateFearless(BlueprintProgression bloodline)
        {
            var dazzled = library.CopyAndAdd<BlueprintBuff>("df6d1025da07524429afbae248845ecc",
                "LightSensitivityDazzled", "949ae82dd5a441b08da1390c1c3c8d84");

            // Add Light Sensitivity until we get fearless rank 2.
            var lightSensitive = Helpers.CreateFeature("BloodlineOrcLightSensitivity", "Light Sensitivity",
                "Creatures with light sensitivity are dazzled in areas of bright sunlight or within the radius of a daylight spell.",
                "6a25396d38094003b6492648891abbf4",
                Helpers.GetIcon("bf0accce250381a44b857d4af6c8e10d"), // searing light
                FeatureGroup.None,
                Helpers.Create<OrcLightSensitivity>());
            OrcLightSensitivity.DazzledBuff = dazzled;

            dazzled.SetDescription($"{dazzled.Description}\n{lightSensitive.Name}: {lightSensitive.Description}");

            var fearless1 = Helpers.CreateFeature("BloodlineOrcFearlessLevel1",
                "Fearless",
                "At 3rd level, you gain a +4 bonus on saving throws made against fear and a +1 natural armor bonus. At 9th level, you lose your light sensitivity, gain immunity to fear, and your natural armor bonus increases to +2.",
                "6c28b22514804978adcc4223c4c37791",
                Helpers.GetIcon("e45ab30f49215054e83b4ea12165409f"), // Aura of Courage
                FeatureGroup.None,
                Helpers.Create<SavingThrowBonusAgainstDescriptor>(a =>
                {
                    a.Bonus = 4;
                    a.ModifierDescriptor = ModifierDescriptor.Inherent;
                    a.SpellDescriptor = SpellDescriptor.Fear;
                }),
                Helpers.CreateAddStatBonus(StatType.AC, 1, ModifierDescriptor.NaturalArmor));
            var fearless2 = Helpers.CreateFeature("BloodlineOrcFearlessLevel2",
                fearless1.Name,
                fearless1.Description,
                "37224d428eac41cebba8b5710872401c",
                fearless1.Icon,
                FeatureGroup.None,
                SpellDescriptor.Fear.CreateBuffImmunity(),
                SpellDescriptor.Fear.CreateSpellImmunity(),
                Helpers.CreateAddStatBonus(StatType.AC, 2, ModifierDescriptor.NaturalArmor),
                Helpers.Create<RemoveFeatureOnApply>(r => r.Feature = lightSensitive));

            orcArcana.AddComponent(Helpers.Create<AddFeatureIfHasFact>(a =>
            {
                a.Feature = lightSensitive;
                a.CheckedFact = fearless2;
                a.Not = true;
            }));

            var addFeat = Helpers.CreateFeature($"BloodlineOrcFearlessAdd",
                fearless1.Name, fearless1.Description,
                "1afbd239a03744f39a8e66ad8ae74f75",
                fearless1.Icon,
                FeatureGroup.None,
                fearless1.CreateAddFeatureOnClassLevel(9, bloodline.Classes, bloodline.Archetypes, before: true),
                fearless2.CreateAddFeatureOnClassLevel(9, bloodline.Classes, bloodline.Archetypes));
            return addFeat;
        }

        static BlueprintFeature CreatePowerOfGiants(BlueprintProgression bloodline)
        {
            var icon = Helpers.GetIcon("da1b292d91ba37948893cdbe9ea89e28"); // legendary proportions
            var baseName = "BloodlineOrcPowerOfGiants";
            var displayName = "Power of Giants";
            var description = "At 15th level, you may grow to Large size as a standard action. At this size you gain a +6 size bonus to Strength, a –2 penalty to Dexterity, a +4 size bonus to Constitution, and a +4 natural armor bonus. You may return to your normal size as a standard action. You may remain in this size for up to 1 minute per character level per day; this duration does not need to be consecutive, but it must be used in 1 minute increments.";

            var resource = Helpers.CreateAbilityResource($"{baseName}Resource", "", "", "887f8bb5ca65428684d42623d1ed2d09", icon);
            resource.SetIncreasedByLevel(0, 1, bloodline.Classes, bloodline.Archetypes);

            var strengthDomainBuff = library.Get<BlueprintBuff>("94dfcf5f3a72ce8478c8de5db69e752b");
            var buff = Helpers.CreateBuff(
                $"{baseName}Buff", displayName, description,
                "8c6ad79fd7f04f93aba2b8743afe9506", icon,
                strengthDomainBuff.FxOnStart,
                Helpers.Create<ChangeUnitSize>(a => a.Size = Size.Large),
                Helpers.Create<AddGenericStatBonus>(a =>
                {
                    a.Descriptor = ModifierDescriptor.Size;
                    a.Stat = StatType.Strength;
                    a.Value = 6;
                }),
                Helpers.Create<AddGenericStatBonus>(a =>
                {
                    a.Descriptor = ModifierDescriptor.Size;
                    a.Stat = StatType.Dexterity;
                    a.Value = -2;
                }),
                Helpers.Create<AddGenericStatBonus>(a =>
                {
                    a.Descriptor = ModifierDescriptor.Size;
                    a.Stat = StatType.Constitution;
                    a.Value = 4;
                }),
                Helpers.Create<AddGenericStatBonus>(a =>
                {
                    a.Descriptor = ModifierDescriptor.NaturalArmor;
                    a.Stat = StatType.AC;
                    a.Value = 4;
                }));

            var abilityBuff = LingeringBuffLogic.CreateBuffForAbility(buff,
                "a7f51053fa0b453c90bf413a3b5afd23", resource, DurationRate.Minutes);

            // Create activatable ability
            var inspireCourage = library.Get<BlueprintActivatableAbility>("2ce9653d7d2e9d948aa0b03e50ae52a6");
            var ability = Helpers.CreateActivatableAbility($"{baseName}ToggleAbility",
                displayName, description, "0c68bb4d1b0b48babd5f93a44d6a5f66",
                icon, abilityBuff, AbilityActivationType.WithUnitCommand,
                CommandType.Standard, inspireCourage.ActivateWithUnitAnimation,
                Helpers.CreateActivatableResourceLogic(resource, ResourceSpendType.Never));

            return Helpers.CreateFeature(baseName, displayName,
                description,
                "6c42a66a8aed4ed8aba182d2b4bc7c3d",
                icon,
                FeatureGroup.None,
                ability.CreateAddFact(),
                resource.CreateAddAbilityResource());
        }

        static BlueprintFeature CreateWarlordReborn(BlueprintProgression bloodline)
        {
            var transformationSpell = library.Get<BlueprintAbility>("27203d62eb3d4184c9aced94f22e1806");

            var resource = Helpers.CreateAbilityResource($"BloodlineOrcTransformationResource", "", "",
                "955f0d751ea5426ea66fe96839ce7132", transformationSpell.Icon);
            resource.SetFixedResource(1);

            var ability = library.CopyAndAdd(transformationSpell, "BloodlineOrcTransformationAbility", "c3f91df156974ce09cfa5b4f49b931ac");
            ability.Type = AbilityType.SpellLike;
            ability.AddComponent(Helpers.CreateResourceLogic(resource));

            return Helpers.CreateFeature("BloodlineOrcWarlordReborn", "Warlord Reborn",
                "At 20th level, you become a true orc warlord of legend. You gain immunity to fire and DR 5/—. Once per day, you can cast transformation as a spell-like ability using your sorcerer level as your caster level.",
                "65e67606c3c94f75b924c13906a0b4b0",
                ability.Icon, // transformation
                FeatureGroup.None,
                ability.CreateAddFact(),
                Helpers.Create<AddDamageResistancePhysical>(t => t.Value = 5),
                Helpers.Create<AddEnergyDamageImmunity>(a => a.EnergyType = DamageEnergyType.Fire),
                resource.CreateAddAbilityResource(),
                ability.CreateBindToClass(bloodline, StatType.Charisma));
        }

        static BlueprintFeature CreateStrengthOfTheBeast(BlueprintProgression bloodline, int rank)
        {
            var strFeat = CreateStrengthOfTheBeastBonus(rank);
            var addFeat = Helpers.CreateFeature($"BloodlineOrcStrengthOfTheBeastAddLevel{rank}",
                strFeat.Name, strFeat.Description,
                strengthOfTheBeastIds[rank + 2],
                strFeat.Icon,
                FeatureGroup.None,
                strFeat.CreateAddFeatureOnClassLevel(rank == 1 ? 13 : 17, bloodline.Classes, bloodline.Archetypes, before: true));

            if (rank == 2)
            {
                addFeat.AddComponent(CreateStrengthOfTheBeastBonus(3)
                    .CreateAddFeatureOnClassLevel(17, bloodline.Classes, bloodline.Archetypes));
            }
            return addFeat;
        }

        static BlueprintFeature CreateStrengthOfTheBeastBonus(int rank)
        {
            return Helpers.CreateFeature($"BloodlineOrcStrengthOfTheBeastLevel{rank}",
                "Strength of the Beast",
                "At 9th level, you gain a +2 inherent bonus to your Strength. This bonus increases to +4 at 13th level, and to +6 at 17th level.",
                strengthOfTheBeastIds[rank - 1],
                Helpers.GetIcon("489c8c4a53a111d4094d239054b26e32"), // Abyssal Bloodline Strength
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.Strength, 2 * rank, ModifierDescriptor.Inherent));
        }

        static String[] strengthOfTheBeastIds = new String[] {
            "695f5aee1e3a471f80fa4584173ea96a",
            "7ab4f2ccf28244138c94d6590f1e3144",
            "1714419d74954fcd9596a959c4e0cefb",
            "b1f6b1367c11408e947d46c8b275c397",
            "c24843eca20644ea982015e940093e5a",
        };

        internal static List<BlueprintFeatureBase> CreateSpellProgression(BlueprintProgression bloodline, String[] spellIds)
        {
            var fullCaster = bloodline.Classes[0];
            var isOracle = fullCaster == OracleClass.oracle;
            var description = isOracle ? "At 2nd level, and every two levels thereafter, an oracle learns an additional spell derived from their mystery." :
                "At 3rd level, and every two levels thereafter, a sorcerer learns an additional spell derived from their bloodline.";
            var result = new List<BlueprintFeatureBase>();
            for (int spellLevel = 1; spellLevel <= 9; spellLevel++)
            {
                var spellId = spellLevel <= spellIds.Length ? spellIds[spellLevel - 1] : null;
                if (spellId == null) continue;

                var spell = library.Get<BlueprintAbility>(spellId);
                spell.AddRecommendNoFeature(bloodline);

                var bloodlineName = bloodline.name.Replace("Progression", "");
                var addSpell = Helpers.CreateFeature($"{bloodlineName}SpellLevel{spellLevel}",
                    spell.Name,
                    $"{description} These spells cannot be exchanged for different spells at higher levels.\n{spell.Description}",
                    Helpers.MergeIds(bloodline.AssetGuid, spell.AssetGuid),
                    spell.Icon,
                    FeatureGroup.None,
                    spell.CreateAddKnownSpell(fullCaster, spellLevel));

                if (spellLevel <= 6 && !isOracle)
                {
                    addSpell.AddComponent(spell.CreateAddKnownSpell(bloodline.Classes[1], spellLevel, bloodline.Archetypes[0])); // Eldritch Scion
                }
                result.Add(addSpell);
            }
            return result;
        }

        static BlueprintFeature CreateTouchOfRage(BlueprintProgression bloodline)
        {
            var strengthDomainAbility = library.Get<BlueprintAbility>("1d6364123e1f6a04c88313d83d3b70ee");
            var strengthDomainBuff = library.Get<BlueprintBuff>("94dfcf5f3a72ce8478c8de5db69e752b");

            var resource = Helpers.CreateAbilityResource($"BloodlineOrcTouchOfRageResource", "", "",
                            "32190a29f4c84beb9efa0a61a514a22c", strengthDomainBuff.Icon);
            resource.SetIncreasedByStat(3, StatType.Charisma);

            var description = "At 1st level, you can touch a creature as a standard action, giving it a morale bonus on attack rolls, damage rolls, and Will saving throws equal to 1/2 your sorcerer level (minimum 1) for 1 round. You can use this ability a number of times per day equal to 3 + your Charisma modifier.";

            var buff = Helpers.CreateBuff("BloodlineOrcTouchOfRageBuff", "Touch of Rage", description,
                "fa7e64f4667d4d6a886cb9cf3bc243ad", strengthDomainBuff.Icon,
                strengthDomainBuff.FxOnStart,
                Helpers.CreateContextRankConfig(progression: ContextRankProgression.Div2),
                Helpers.Create<AddStatBonusAbilityValue>(a =>
                {
                    a.Descriptor = ModifierDescriptor.Morale;
                    a.Stat = StatType.AdditionalAttackBonus;
                    a.Value = Helpers.CreateContextValueRank();
                }),
                Helpers.Create<AddStatBonusAbilityValue>(a =>
                {
                    a.Descriptor = ModifierDescriptor.Morale;
                    // TODO: does this work with spells? (it should)
                    a.Stat = StatType.AdditionalDamage;
                    a.Value = Helpers.CreateContextValueRank();
                }),
                Helpers.Create<AddStatBonusAbilityValue>(a =>
                {
                    a.Descriptor = ModifierDescriptor.Morale;
                    a.Stat = StatType.SaveWill;
                    a.Value = Helpers.CreateContextValueRank();
                }));

            var ability = Helpers.CreateAbility("BloodlineOrcTouchOfRageAbility",
                "Touch of Rage", description, "b88aabade3ec4c8ab62f8e77d731c408",
                strengthDomainAbility.Icon,
                AbilityType.SpellLike, CommandType.Standard, AbilityRange.Touch,
                strengthDomainAbility.LocalizedDuration,
                strengthDomainAbility.LocalizedSavingThrow,
                Helpers.CreateSpellDescriptor(),
                Helpers.CreateDeliverTouch(),
                Helpers.CreateRunActions(Helpers.CreateApplyBuff(buff, Helpers.CreateContextDuration(1), fromSpell: false)));
            ability.CanTargetFriends = true;
            ability.CanTargetSelf = true;
            ability.EffectOnAlly = AbilityEffectOnUnit.Helpful;
            var touchAbility = ability.CreateTouchSpellCast(resource);

            var feat = Helpers.CreateFeature("BloodlineOrcTouchOfRage", "Touch of Rage", description,
                "36c83cf43622429f8b2021660911d090",
                strengthDomainAbility.Icon, // strength domain ability
                FeatureGroup.None,
                touchAbility.CreateAddFact(),
                resource.CreateAddAbilityResource(),
                Helpers.Create<ReplaceCasterLevelOfAbility>(r =>
                {
                    r.Spell = touchAbility;
                    r.Class = bloodline.Classes[0];
                    r.Archetypes = bloodline.Archetypes;
                    r.AdditionalClasses = bloodline.Classes.Skip(1).ToArray();
                }));
            feat.Ranks = 1;
            return feat;
        }
    }

    public class OrcBloodlineArcana : RuleInitiatorLogicComponent<RuleCalculateDamage>
    {
        public override void OnEventAboutToTrigger(RuleCalculateDamage evt)
        {
            if (evt.ParentRule.SourceAbility?.Type == AbilityType.Spell)
            {
                foreach (BaseDamage item in evt.DamageBundle)
                {
                    item.AddBonus(item.Dice.Rolls);
                }
            }
        }

        public override void OnEventDidTrigger(RuleCalculateDamage evt) { }
    }

    public class OrcLightSensitivity : OwnedGameLogicComponent<UnitDescriptor>, IWeatherChangeHandler, IAreaActivationHandler
    {
        internal static BlueprintBuff DazzledBuff;

        [JsonProperty]
        private Buff appliedBuff;

        public void OnWeatherChange() => Apply();

        public void OnAreaActivated() => Apply();

        public override void OnTurnOn() => Apply();

        public override void OnTurnOff()
        {
            appliedBuff?.Remove();
            appliedBuff = null;
        }

        void Apply()
        {
            try
            {
                var game = Game.Instance;
                var weather = game?.Player?.Weather?.ActualWeather;
                var timeOfDay = game?.TimeOfDay;
                var area = game?.CurrentlyLoadedArea;
                // Light sensitivity: dazzled if outdoors, clear weather, morning/daytime/evening.
                if (weather == InclemencyType.Clear && area != null && !area.IsIndoor &&
                    // 6:00 to 18:00.
                    (timeOfDay == TimeOfDay.Morning || timeOfDay == TimeOfDay.Day))
                {
                    if (appliedBuff == null)
                    {
                        Log.Write($"{GetType().Name}: add {DazzledBuff} to {Owner.CharacterName}");
                        appliedBuff = Owner.AddBuff(DazzledBuff, Owner.Unit);
                        if (appliedBuff == null) return;
                        appliedBuff.IsNotDispelable = true;
                        appliedBuff.IsFromSpell = false;
                    }
                }
                else if (appliedBuff != null)
                {
                    Log.Write($"{GetType().Name}: remove {DazzledBuff} from {Owner.CharacterName}");
                    appliedBuff.Remove();
                    appliedBuff = null;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    // Support for lingering buffs of activatable abilities, such as those with duration of 1 minute.
    //
    // For example, consider Orc bloodline's "Power of Giants". It takes a standard action to activate,
    // and costs a resource every minute. So you need to manage those resources carefully, activating
    // it before battle and deactivating when not needed. The game only supports per-round activated
    // abilities and no linger. (Bard's lingering song is implemented with a hard coded check).
    //
    // To support these, we create two buffs: the one added by the activated ability, and the real one.
    // The real buff is added with a ticking duration, so you can see the time until refresh.
    // Then there's another hidden buff, that's only used to handle the resource accounting.
    // It's the one that is tied to the activatable ability toggle.
    //
    // This allows the buffs to linger. They can't be ended immediately, which is a bit of a downside,
    // but it's not clear that is legal anyways in most cases (e.g. rules that say the ability must be
    // used in fixed 1-minute increments). The `removeWhenTurnedOff` option can be used to enable
    // immediate removal, if needed.
    public class LingeringBuffLogic : BuffLogic, ITickEachRound
    {
        public BlueprintAbilityResource RequiredResource;
        public BlueprintBuff RealBuff;
        public DurationRate BuffDuration;
        public bool Dispellable;
        public bool RemoveWhenTurnedOff;

        internal static BlueprintBuff CreateBuffForAbility(BlueprintBuff realBuff, String assetId,
            BlueprintAbilityResource resource, DurationRate duration,
            bool dispellable = true, bool removeWhenTurnedOff = false)
        {
            // Clone the real buff, but remove its functionality and UI.
            var clonedBuff = UnityEngine.Object.Instantiate(realBuff);
            clonedBuff.name = $"{realBuff.name}ResourceBuff";
            Main.library.AddAsset(clonedBuff, Helpers.MergeIds(realBuff.AssetGuid, assetId));
            var flags = realBuff.GetBuffFlags();
            clonedBuff.SetBuffFlags(flags | BuffFlags.HiddenInUi);
            clonedBuff.FxOnStart = new PrefabLink();
            clonedBuff.FxOnRemove = new PrefabLink();

            var logic = Helpers.Create<LingeringBuffLogic>();
            logic.RequiredResource = resource;
            logic.BuffDuration = duration;
            logic.RealBuff = realBuff;
            logic.Dispellable = dispellable;
            logic.RemoveWhenTurnedOff = removeWhenTurnedOff;

            clonedBuff.SetComponents(logic);
            return clonedBuff;
        }

        public override void OnFactActivate() => Apply();

        public void OnNewRound() => Apply();

        void Apply()
        {
            var realBuff = Owner.Buffs.GetBuff(RealBuff);
            if (realBuff == null || realBuff.TimeLeft.TotalSeconds < (1.Rounds().Seconds.TotalSeconds * 0.99))
            {
                var resources = Owner.Resources;
                if (resources.GetResourceAmount(RequiredResource) > 0)
                {
                    resources.Spend(RequiredResource, 1);

                    Buff.RunActionInContext(
                        Helpers.CreateActionList(
                            RealBuff.CreateApplyBuff(
                                Helpers.CreateContextDuration(1, BuffDuration),
                                RealBuff.IsFromSpell, Dispellable, asChild: RemoveWhenTurnedOff)),
                        Owner.Unit);
                }
            }
        }
    }
}
