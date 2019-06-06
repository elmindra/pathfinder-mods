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
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Controllers.Combat;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI.Common;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Parts;

namespace EldritchArcana
{
    static class Traits
    {
        static LibraryScriptableObject library => Main.library;

        internal static void Load()
        {
            // Create the trait selection (https://www.d20pfsrd.com/traits/).
            // TODO: ideally we'd use FeatureGroup.Trait, but it's not recognized by the game code.
            var featureGroup = FeatureGroup.Feat;
            var traitSelection = Helpers.CreateFeatureSelection("TraitSelection1", "Traits",
                "Character traits are abilities that are not tied to your character’s race or class. They can enhance your character’s skills, racial abilities, class abilities, or other statistics, enabling you to further customize them. At its core, a character trait is approximately equal in power to half a feat, so two character traits are roughly equivalent to a bonus feat. Yet a character trait isn’t just another kind of power you can add on to your character—it’s a way to quantify (and encourage) building a character background that fits into your campaign world. Think of character traits as “story seeds” for your background; after you pick your two traits, you’ll have a point of inspiration from which to build your character’s personality and history. Alternatively, if you’ve already got a background in your head or written down for your character, you can view picking their traits as a way to quantify that background, just as picking race and class and ability scores quantifies their other strengths and weaknesses.",
                "f243a1b828714591b5fa0635b0cefb5b", null, featureGroup);
            var traitSelection2 = Helpers.CreateFeatureSelection("TraitSelection2", "Traits",
                traitSelection.Description, "d3a6541d2d384b1390d8ea26bb02b8cd", null, featureGroup);

            var choices = new List<BlueprintFeatureSelection>();
            choices.Add(CreateCombatTraits());
            choices.Add(CreateFaithTraits());
            choices.Add(CreateMagicTraits());
            BlueprintFeatureSelection adopted;
            choices.Add(CreateSocialTraits(out adopted));
            choices.Add(CreateRaceTraits(adopted));
            choices.Add(CreateCampaignTraits());
            choices.Add(CreateRegionalTraits());

            traitSelection.SetFeatures(choices);
            traitSelection2.SetFeatures(traitSelection.Features);
            ApplyClassMechanics_Apply_Patch.onChargenApply.Add((state, unit) =>
            {
                traitSelection.AddSelection(state, unit, 1);
                traitSelection2.AddSelection(state, unit, 1);
            });

            // Create the "Additional Traits" feat.
            var additionalTraits = Helpers.CreateFeature("AdditionalTraitsProgression",
                "Additional Traits",
                "You have more traits than normal.\nBenefit: You gain two character traits of your choice. These traits must be chosen from different lists, and cannot be chosen from lists from which you have already selected a character trait. You must meet any additional qualifications for the character traits you choose — this feat cannot enable you to select a dwarf character trait if you are an elf, for example.",
                "02dbb324cc334412a55e6d8f9fe87009",
                Helpers.GetIcon("0d3651b2cb0d89448b112e23214e744e"), // Extra Performance
                FeatureGroup.Feat);

            var additionalTrait1 = Helpers.CreateFeatureSelection("AdditionalTraitSelection1", "Traits",
                traitSelection.Description,
                "a85fbbe3c9184137a31a12f4b0b7904a", null, FeatureGroup.Feat);
            var additionalTrait2 = Helpers.CreateFeatureSelection("AdditionalTraitSelection2", "Traits",
                traitSelection.Description,
                "0fdd6f51c19c44938b9d64b147cf32f8", null, FeatureGroup.Feat);
            additionalTrait1.SetFeatures(traitSelection.Features);
            additionalTrait2.SetFeatures(traitSelection.Features);

            SelectFeature_Apply_Patch.onApplyFeature.Add(additionalTraits, (state, unit) =>
            {
                additionalTrait1.AddSelection(state, unit, 1);
                additionalTrait2.AddSelection(state, unit, 1);
            });

            library.AddFeats(additionalTraits);
        }

        static BlueprintFeatureSelection CreateCombatTraits()
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var combatTraits = Helpers.CreateFeatureSelection("CombatTrait", "Combat Trait",
                "Combat traits focus on martial and physical aspects of your character’s background.",
                "fab4225be98a4b3e9717883f22086c82", null, FeatureGroup.None, noFeature);
            noFeature.Feature = combatTraits;

            var choices = new List<BlueprintFeature>();
            choices.Add(Helpers.CreateFeature("AnatomistTrait", "Anatomist",
                "You have studied the workings of anatomy, either as a student at university or as an apprentice mortician or necromancer. You know where to aim your blows to strike vital organs.\nBenefit: You gain a +1 trait bonus on all rolls made to confirm critical hits.",
                "69245ef4b4ba44ddac917fc2aa10fbad",
                Helpers.GetIcon("f4201c85a991369408740c6888362e20"), // Improved Critical
                FeatureGroup.None,
                Helpers.Create<CriticalConfirmationBonus>(a => { a.Bonus = 1; a.Value = 0; })));

            choices.Add(Helpers.CreateFeature("ArmorExpertTrait", "Armor Expert",
                "You have worn armor as long as you can remember, either as part of your training to become a knight’s squire or simply because you were seeking to emulate a hero. Your childhood armor wasn’t the real thing as far as protection, but it did encumber you as much as real armor would have, and you’ve grown used to moving in such suits with relative grace.\nBenefit: When you wear armor of any sort, reduce that suit’s armor check penalty by 1, to a minimum check penalty of 0.",
                "94d526372a964b6db97c64291a3cb846",
                Helpers.GetIcon("3bc6e1d2b44b5bb4d92e6ba59577cf62"), // Armor Focus (light)
                FeatureGroup.None,
                Helpers.Create<ArmorCheckPenaltyIncrease>(a => a.Bonus = -1)));

            var rageResource = library.Get<BlueprintAbilityResource>("24353fcf8096ea54684a72bf58dedbc9");
            choices.Add(Helpers.CreateFeature("BerserkerOfTheSocietyTrait", "Berserker of the Society",
                "Your time spent as a society member has taught you new truths about the origins of the your rage ability.\nBenefit: You may use your rage ability for 3 additional rounds per day.",
                "8acfcecfed05442594eed93fe448ab3d",
                Helpers.GetIcon("1a54bbbafab728348a015cf9ffcf50a7"), // Extra Rage
                FeatureGroup.None,
                rageResource.CreateIncreaseResourceAmount(3)));

            choices.Add(Helpers.CreateFeature("BladeOfTheSocietyTrait", "Blade of the Society",
                "You have studied and learned the weak spots of many humanoids and monsters.\nBenefit: You gain a +1 trait bonus to damage rolls from sneak attacks.",
                "ff8c90626a58436997cc41e4b121be9a",
                Helpers.GetIcon("9f0187869dc23744292c0e5bb364464e"), // Accomplished Sneak Attacker
                FeatureGroup.None,
                Helpers.Create<AdditionalDamageOnSneakAttack>(a => a.Value = 1)));

            choices.Add(Helpers.CreateFeature("DefenderOfTheSocietyTrait", "Defender of the Society",
                "Your time spent fighting and studying the greatest warriors of the society has taught you new defensive skills while wearing armor.\nBenefit: You gain a +1 trait bonus to Armor Class when wearing medium or heavy armor.",
                "545bf7e13346473caf48f179083df894",
                Helpers.GetIcon("7dc004879037638489b64d5016997d12"), // Armor Focus Medium
                FeatureGroup.None,
                Helpers.Create<ArmorFocus>(a => a.ArmorCategory = ArmorProficiencyGroup.Medium),
                Helpers.Create<ArmorFocus>(a => a.ArmorCategory = ArmorProficiencyGroup.Heavy)));

            choices.Add(Helpers.CreateFeature("DeftDodgerTrait", "Deft Dodger",
                "Growing up in a rough neighborhood or a dangerous environment has honed your senses.\nBenefit: You gain a +1 trait bonus on Reflex saves.",
                "7b57d86503314d32b753f77909c909bc",
                Helpers.GetIcon("15e7da6645a7f3d41bdad7c8c4b9de1e"), // Lightning Reflexes
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SaveReflex, 1, ModifierDescriptor.Trait)));

            choices.Add(Helpers.CreateFeature("DirtyFighterTrait", "Dirty Fighter",
                "You wouldn’t have lived to make it out of childhood without the aid of a sibling, friend, or companion you could always count on to distract your enemies long enough for you to do a little bit more damage than normal. That companion may be another PC or an NPC (who may even be recently departed from your side).\n" +
                "Benefit: When you hit a foe you are flanking, you deal 1 additional point of damage (this damage is added to your base damage, and is multiplied on a critical hit). This additional damage is a trait bonus.",
                "ac47c14063574a0a9ea6927bf637a02a",
                Helpers.GetIcon("5662d1b793db90c4b9ba68037fd2a768"), // precise strike
                FeatureGroup.None,
                DamageBonusAgainstFlankedTarget.Create(1)));

            var kiPowerResource = library.Get<BlueprintAbilityResource>("9d9c90a9a1f52d04799294bf91c80a82");
            choices.Add(Helpers.CreateFeature("HonoredFistOfTheSocietyTrait", "Honored First of the Society",
                "You have studied dozens of ancient texts on martial arts that only the Society possesses, and are more learned in these arts than most.\nBenefit: You increase your ki pool by 1 point.",
                "ee9c230cbbc2484084af61ac97e47e72",
                Helpers.GetIcon("7dc004879037638489b64d5016997d12"), // Armor Focus Medium
                FeatureGroup.None,
                kiPowerResource.CreateIncreaseResourceAmount(1)));

            // TODO: Killer

            choices.Add(Helpers.CreateFeature("ReactionaryTrait", "Reactionary",
                "You were bullied often as a child, but never quite developed an offensive response. Instead, you became adept at anticipating sudden attacks and reacting to danger quickly.\nBenefit: You gain a +2 trait bonus on initiative checks.",
                "fa2c636580ee431297de8806a046044a",
                Helpers.GetIcon("797f25d709f559546b29e7bcb181cc74"), // Improved Initiative
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.Initiative, 2, ModifierDescriptor.Trait)));

            choices.Add(CreateSkillFeat("RecklessTrait", "Reckless",
                "You have a tendency for rash behavior, often disregarding your own safety as you move across the battlefield.",
                "edb2f4d0c2c34c7baccad11f2b5bfbd4",
                StatType.SkillMobility));

            choices.Add(Helpers.CreateFeature("ResilientTrait", "Resilient",
                "Growing up in a poor neighborhood or in the unforgiving wilds often forced you to subsist on food and water from doubtful sources. You’ve built up your constitution as a result.\nBenefit: You gain a +1 trait bonus on Fortitude saves.",
                "789d02217b6542ce8b0302249c86d49d",
                Helpers.GetIcon("79042cb55f030614ea29956177977c52"), // Great Fortitude
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SaveFortitude, 1, ModifierDescriptor.Trait)));

            choices.Add(CreateSkillFeat("WittyReparteeTrait", "Witty Repartee",
                "You are quick with your tongue and have always possessed the talent to quickly admonish your enemies.",
                "c6dbc457c5de40dbb4cb9fe4d7706cd9",
                StatType.SkillPersuasion));

            combatTraits.SetFeatures(choices);
            return combatTraits;
        }

        static BlueprintFeatureSelection CreateFaithTraits()
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var faithTraits = Helpers.CreateFeatureSelection("FaithTrait", "Faith Trait",
                "Faith traits focus on the character's religious and philosophical leanings.",
                "21d0fe2d88e44e5cbfb28becadf86110", null, FeatureGroup.None, noFeature);
            noFeature.Feature = faithTraits;

            var choices = new List<BlueprintFeature>();
            choices.Add(Helpers.CreateFeature("BirthmarkTrait", "Birthmark",
                "You were born with a strange birthmark that looks very similar to the holy symbol of the god you chose to worship later in life.\nBenefits: This birthmark increases your devotion to your god. You gain a +2 trait bonus on all saving throws against charm and compulsion effects.",
                "ebf720b1589d43a2b6cfad26aeda34f9",
                Helpers.GetIcon("2483a523984f44944a7cf157b21bf79c"), // Elven Immunities
                FeatureGroup.None,
                Helpers.Create<SavingThrowBonusAgainstSchool>(a =>
                {
                    a.School = SpellSchool.Enchantment;
                    a.Value = 2;
                    a.ModifierDescriptor = ModifierDescriptor.Trait;
                })));

            choices.Add(CreateSkillFeat("ChildOfTheTempleTrait", "Child of the Temple",
                "You have long served at a temple in a city, where you picked up on many of the nobility’s customs in addition to spending much time in the temple libraries studying your faith.",
                "cb79816f17d84a51b173ef74aa325561",
                StatType.SkillLoreReligion));

            choices.Add(CreateSkillFeat("DevoteeOfTheGreenTrait", "Devotee of the Green",
                "Your faith in the natural world or one of the gods of nature makes it easy for you to pick up on related concepts.",
                "6b8e68de9fc04139af0f1127d2a33984",
                StatType.SkillLoreNature));

            choices.Add(CreateSkillFeat("EaseOfFaithTrait", "Ease of Faith",
                "Your mentor, the person who invested your faith in you from an early age, took steps to ensure you understood that what powers your divine magic is no different from that which powers the magic of other religions. This philosophy makes it easier for you to interact with others who may not share your views.",
                "300d727a858d4992a3e01c8165a4c25f",
                StatType.SkillPersuasion));

            var channelEnergyResource = library.Get<BlueprintAbilityResource>("5e2bba3e07c37be42909a12945c27de7");
            var channelEnergyEmpyrealResource = library.Get<BlueprintAbilityResource>("f9af9354fb8a79649a6e512569387dc5");
            var channelEnergyHospitalerResource = library.Get<BlueprintAbilityResource>("b0e0c7716ab27c64fb4b131c9845c596");
            choices.Add(Helpers.CreateFeature("ExaltedOfTheSocietyTrait", "Exalted of the Society",
                "The vaults of the great city contain many secrets of the divine powers of the gods, and you have studied your god extensively.\nBenefit: You may channel energy 1 additional time per day.",
                "3bb1b077ad0845b59663c0e1b343011a",
                Helpers.GetIcon("cd9f19775bd9d3343a31a065e93f0c47"), // Extra Channel
                FeatureGroup.None,
                channelEnergyResource.CreateIncreaseResourceAmount(1),
                channelEnergyEmpyrealResource.CreateIncreaseResourceAmount(1),
                channelEnergyHospitalerResource.CreateIncreaseResourceAmount(1),
                LifeMystery.channelResource.CreateIncreaseResourceAmount(1)));

            choices.Add(Helpers.CreateFeature("FatesFavoredTrait", "Fate's Favored",
                "Whenever you are under the effect of a luck bonus of any kind, that bonus increases by 1.",
                "0c5dcccc21e148cdaf0fb3c643249bfb",
                Helpers.GetIcon("9a7e3cd1323dfe347a6dcce357844769"), // blessing luck & resolve
                FeatureGroup.None,
                Helpers.Create<ExtraLuckBonus>()));

            choices.Add(Helpers.CreateFeature("IndomitableFaithTrait", "Indomitable Faith",
                "You were born in a region where your faith was not popular, but you still have never abandoned it. Your constant struggle to maintain your own faith has bolstered your drive.\nBenefit: You gain a +1 trait bonus on Will saves.",
                "e50acadad65b4028884dd4a74f14e727",
                Helpers.GetIcon("175d1577bb6c9a04baf88eec99c66334"), // Iron Will
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SaveWill, 1, ModifierDescriptor.Trait)));

            choices.Add(CreateSkillFeat("ScholarOfTheGreatBeyondTrait", "Scholar of the Great Beyond",
                "Your greatest interests as a child did not lie with current events or the mundane—you have always felt out of place, as if you were born in the wrong era. You take to philosophical discussions of the Great Beyond and of historical events with ease.",
                "0896fea4f7ca4635aa4e5338a673610d",
                StatType.SkillKnowledgeWorld));

            // TODO: Stalwart of the Society

            faithTraits.SetFeatures(choices);
            return faithTraits;
        }

        static BlueprintFeatureSelection CreateMagicTraits()
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var magicTraits = Helpers.CreateFeatureSelection("MagicTrait", "Magic Trait",
                "Magic traits focus on any magical events or training your character may have had in their past.",
                "d89181c607e4431084f9d97532c5c554", null, FeatureGroup.None, noFeature);
            noFeature.Feature = magicTraits;

            var choices = new List<BlueprintFeature>();
            choices.Add(CreateSkillFeat("ClassicallySchooledTrait", "Classically Schooled",
                "Your greatest interests as a child did not lie with current events or the mundane—you have always felt out of place, as if you were born in the wrong era. You take to philosophical discussions of the Great Beyond and of historical events with ease.",
                "788098518aa9436782397fa318c64c69",
                StatType.SkillKnowledgeArcana));

            choices.Add(CreateSkillFeat("DangerouslyCuriousTrait", "Dangerously Curious",
                "You have always been intrigued by magic, possibly because you were the child of a magician or priest. You often snuck into your parent’s laboratory or shrine to tinker with spell components and magic devices, and frequently caused quite a bit of damage and headaches for your parent as a result.",
                "0c72c573cc404b42916dc7265ea6f59a",
                StatType.SkillUseMagicDevice));

            choices.Add(Helpers.CreateFeature("FocusedMindTrait", "Focused Mind",
                "Your childhood was dominated either by lessons of some sort (whether musical, academic, or other) or by a horrible home life that encouraged your ability to block out distractions and focus on the immediate task at hand.\nBenefit: You gain a +2 trait bonus on concentration checks.",
                "e34889a2dd7e4e9ebfdfa76bfb8f5556",
                Helpers.GetIcon("06964d468fde1dc4aa71a92ea04d930d"), // Combat Casting
                FeatureGroup.None,
                Helpers.Create<ConcentrationBonus>(a => a.Value = 2)));

            var giftedAdept = Helpers.CreateFeatureSelection("GiftedAdeptTrait", "Gifted Adept",
                "Your interest in magic was inspired by witnessing a spell being cast in a particularly dramatic method, perhaps even one that affected you physically or spiritually. This early exposure to magic has made it easier for you to work similar magic on your own.\nBenefit: Pick one spell when you choose this trait—from this point on, whenever you cast that spell, its effects manifest at +1 caster level.",
                "5eb0b8050ed5466986846cffca0b35b6",
                Helpers.GetIcon("fe9220cdc16e5f444a84d85d5fa8e3d5"), // Spell Specialization Progression
                FeatureGroup.None);
            FillSpellSelection(giftedAdept, 1, 9, Helpers.Create<IncreaseCasterLevelForSpell>());
            choices.Add(giftedAdept);

            choices.Add(Helpers.CreateFeature("MagicalKnackTrait", "Magical Knack",
                "You were raised, either wholly or in part, by a magical creature, either after it found you abandoned in the woods or because your parents often left you in the care of a magical minion. This constant exposure to magic has made its mysteries easy for you to understand, even when you turn your mind to other devotions and tasks.\nBenefit: Pick a class when you gain this trait—your caster level in that class gains a +2 trait bonus as long as this bonus doesn’t raise your caster level above your current Hit Dice.",
                "8fd15d5aa003497aa7f976530d21e430",
                Helpers.GetIcon("16fa59cc9a72a6043b566b49184f53fe"), // Spell Focus
                FeatureGroup.None,
                Helpers.Create<IncreaseCasterLevelUpToCharacterLevel>()));

            var magicalLineage = Helpers.CreateFeatureSelection("MagicalLineageTrait", "Magical Lineage",
                "One of your parents was a gifted spellcaster who not only used metamagic often, but also developed many magical items and perhaps even a new spell or two—and you have inherited a fragment of this greatness.\nBenefit: Pick one spell when you choose this trait. When you apply metamagic feats to this spell that add at least 1 level to the spell, treat its actual level as 1 lower for determining the spell’s final adjusted level.",
                "1785787fb62a4c529104ba53d0de99ae",
                Helpers.GetIcon("ee7dc126939e4d9438357fbd5980d459"), // Spell Penetration
                FeatureGroup.None);
            FillSpellSelection(magicalLineage, 1, 9, Helpers.Create<ReduceMetamagicCostForSpell>(r => r.Reduction = 1));
            choices.Add(magicalLineage);

            choices.Add(UndoSelection.Feature.Value);
            magicTraits.SetFeatures(choices);
            return magicTraits;
        }

        static BlueprintFeatureSelection CreateSocialTraits(out BlueprintFeatureSelection adopted)
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var socialTraits = Helpers.CreateFeatureSelection("SocialTrait", "Social Trait",
                "Social traits focus on your character’s social class or upbringing.",
                "9e41e60c929e45bc84ded046148c07ec", null, FeatureGroup.None, noFeature);
            noFeature.Feature = socialTraits;
            var choices = new List<BlueprintFeature>();

            // This trait is finished by CreateRaceTraits.
            adopted = Helpers.CreateFeatureSelection("AdoptedTrait", "Adopted",
                "You were adopted and raised by someone not of your race, and raised in a society not your own.\nBenefit: As a result, you picked up a race trait from your adoptive parents and society, and may immediately select a race trait from your adoptive parents’ race.",
                "b4b37968273b4782b29d31c0ca215f41",
                Helpers.GetIcon("26a668c5a8c22354bac67bcd42e09a3f"), // Adaptability
                FeatureGroup.None);

            adopted.IgnorePrerequisites = true;
            adopted.Obligatory = true;
            choices.Add(adopted);

            choices.Add(CreateSkillFeat("ChildOfTheStreetsTrait", "Child of the Streets",
                "You grew up on the streets of a large city, and as a result you have developed a knack for picking pockets and hiding small objects on your person.",
                "a181fd2561134715a04e1b05776ab7a3",
                StatType.SkillThievery));

            choices.Add(CreateSkillFeat("FastTalkerTrait", "Fast-Talker",
                "You had a knack for getting yourself into trouble as a child, and as a result developed a silver tongue at an early age.",
                "509458a5ded54ecd9a2a4ef5388de2b7",
                StatType.SkillPersuasion));

            var performanceResource = library.Get<BlueprintAbilityResource>("e190ba276831b5c4fa28737e5e49e6a6");
            choices.Add(Helpers.CreateFeature("MaestroOfTheSocietyTrait", "Maestro of the Society",
                "The skills of the greatest musicians are at your fingertips, thanks to the vast treasure trove of musical knowledge in the vaults you have access to.\nBenefit: You may use bardic performance 3 additional rounds per day.",
                "847cdf262e4147cda2c670db81852c58",
                Helpers.GetIcon("0d3651b2cb0d89448b112e23214e744e"),
                FeatureGroup.None,
                Helpers.Create<IncreaseResourceAmount>(i => { i.Resource = performanceResource; i.Value = 3; })));

            choices.Add(CreateSkillFeat("SuspiciousTrait", "Suspicious",
                "You discovered at an early age that someone you trusted, perhaps an older sibling or a parent, had lied to you, and lied often, about something you had taken for granted, leaving you quick to question the claims of others.",
                "2f4e86a9d42547bc85b4c829a47d054c",
                StatType.SkillPerception));

            choices.Add(UndoSelection.Feature.Value);
            socialTraits.SetFeatures(choices);
            return socialTraits;
        }

        static BlueprintFeatureSelection CreateRaceTraits(BlueprintFeatureSelection adopted)
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var raceTraits = Helpers.CreateFeatureSelection("RaceTrait", "Race Trait",
                "Race traits are keyed to specific races or ethnicities, which your character must belong to in order to select the trait.",
                "6264aa9515be40cda55892da93685764", null, FeatureGroup.None,
                Helpers.PrerequisiteNoFeature(adopted), noFeature);
            noFeature.Feature = raceTraits;

            var humanReq = Helpers.PrerequisiteFeaturesFromList(Helpers.human, Helpers.halfElf, Helpers.halfOrc,
                // Note: Aasimar/Tiefling included under the assumption they have "Scion of Humanity"/"Pass for Human"
                Helpers.aasimar, Helpers.tiefling);

            var halfElfReq = Helpers.PrerequisiteFeature(Helpers.halfElf);
            var halfOrcReq = Helpers.PrerequisiteFeature(Helpers.halfOrc);
            var elfReq = Helpers.PrerequisiteFeaturesFromList(Helpers.elf, Helpers.halfElf);
            var dwarfReq = Helpers.PrerequisiteFeature(Helpers.dwarf);
            var halflingReq = Helpers.PrerequisiteFeature(Helpers.halfling);
            var gnomeReq = Helpers.PrerequisiteFeature(Helpers.gnome);
            var aasimarReq = Helpers.PrerequisiteFeature(Helpers.aasimar);
            var tieflingReq = Helpers.PrerequisiteFeature(Helpers.tiefling);

            // TODO: how do we code prerequisites so they aren't ignored by "Adopted"?
            // (only race prereq should be ignored, not others)
            //
            // Note: half-elf, half-orc can take traits from either race.
            // Also Aasimar/Tiefling are treated as having Scion of Humanity/Pass for Human in the game.
            var choices = new List<BlueprintFeature>();

            // Human:
            // - Carefully Hidden (+1 will save, +2 vs divination)
            // - Fanatic (Arcana)
            // - Historian (World and +1 bardic knowledge if Bard)
            // - Shield Bearer (+1 dmg shield bash)
            // - Superstitious (+1 save arcane spells)
            // - World Traveler (choose: persuasion, perception, or world)

            var components = new List<BlueprintComponent> { humanReq };
            components.Add(Helpers.CreateAddStatBonus(StatType.SaveWill, 1, ModifierDescriptor.Trait));
            components.Add(Helpers.Create<SavingThrowBonusAgainstSchool>(a =>
            {
                a.School = SpellSchool.Divination;
                a.Value = 2;
                a.ModifierDescriptor = ModifierDescriptor.Trait;
            }));
            choices.Add(Helpers.CreateFeature("CarefullyHiddenTrait", "Carefully Hidden (Human)",
                "Your life as a member of an unpopular ethnic group has given you an uncanny knack for avoiding detection.\nBenefit: You gain a +1 trait bonus to Will saves and a +2 trait bonus to saving throws versus divination effects.",
                "38b92d2ebb4c4cdb8e946e29f5b2f178",
                Helpers.GetIcon("175d1577bb6c9a04baf88eec99c66334"), // Iron Will
                FeatureGroup.None,
                components.ToArray()));
            choices.Add(CreateSkillFeat("FanaticTrait", "Fanatic (Human)",
                "Your years spent in libraries reading every musty tome you could find about ancient lost civilizations has given you insight into the subjects of history and the arcane.",
                "6427e81ba399406c93b463c284a42055",
                StatType.SkillKnowledgeArcana,
                humanReq));

            var bardicKnowledge = library.Get<BlueprintFeature>("65cff8410a336654486c98fd3bacd8c5");
            components.Clear();
            components.Add(humanReq);
            components.AddRange((new StatType[] {
                StatType.SkillKnowledgeArcana,
                    StatType.SkillKnowledgeWorld,
                    StatType.SkillLoreNature,
                    StatType.SkillLoreReligion,
            }).Select((skill) => Helpers.Create<AddStatBonusIfHasFact>(a =>
            {
                a.Stat = skill;
                a.Value = 1;
                a.CheckedFact = bardicKnowledge;
                a.Descriptor = ModifierDescriptor.UntypedStackable;
            })));

            var historian = CreateSkillFeat("HistorianTrait", "Historian (Human)",
                "Your parents were scholars of history, whether genealogists of your own family tree, sages on the subject of ancient empires, or simply hobbyists with a deep and abiding love for the past.\nBenefits: You gain a +1 trait bonus on Knowledge (history) checks and bardic knowledge checks, and Knowledge (history) is always a class skill for you.",
                "4af3871899e4440bae03d4c33d4b52fd",
                StatType.SkillKnowledgeWorld,
                components.ToArray());
            choices.Add(historian);

            components.Clear();
            components.Add(humanReq);
            components.AddRange(new String[] {
                "98a0dc03586a6d04791901c41700e516", // SpikedLightShield
                "1fd965e522502fe479fdd423cca07684", // WeaponLightShield
                "a1b85d048fb5003438f34356df938a9f", // SpikedHeavyShield
                "be9b6408e6101cb4997a8996484baf19"  // WeaponHeavyShield
            }.Select(id => Helpers.Create<WeaponTypeDamageBonus>(w => { w.DamageBonus = 1; w.WeaponType = library.Get<BlueprintWeaponType>(id); })));

            choices.Add(Helpers.CreateFeature("ShieldBearerTrait", "Shield Bearer (Human)",
                "You have survived many battles thanks to your skill with your shield.\nBenefit: When performing a shield bash, you deal 1 additional point of damage.",
                "044ebbbadfba4d58afa11bfbf38df199",
                Helpers.GetIcon("121811173a614534e8720d7550aae253"), // Shield Bash
                FeatureGroup.None,
                components.ToArray()));

            choices.Add(Helpers.CreateFeature("SuperstitiousTrait", "Superstitious (Human)",
                "You have a healthy fear of sorcerers’ speech and wizards’ words that has helped you to survive their charms.\nBenefit: You gain a +1 trait bonus on saving throws against arcane spells.",
                "f5d79e5fbb87473ca0b13ed15b742079",
                Helpers.GetIcon("2483a523984f44944a7cf157b21bf79c"), // Elven Immunities
                FeatureGroup.None,
                humanReq,
                Helpers.Create<SavingThrowBonusAgainstSpellSource>()));

            var travelerDescription = "Your family has taken the love of travel to an extreme, roaming the world extensively. You’ve seen dozens of cultures and have learned to appreciate the diversity of what the world has to offer.";
            var worldTraveler = Helpers.CreateFeatureSelection("WorldTravelerTrait", "World Traveler (Human)",
                travelerDescription + "\nBenefits: Select one of the following skills: Persuasion, Knowledge (world), or Perception. You gain a +1 trait bonus on checks with that skill, and it is always a class skill for you.",
                "ecacfcbeddfe453cafc8d60fc1db7d34",
                Helpers.GetIcon("3adf9274a210b164cb68f472dc1e4544"), // Human Skilled
                FeatureGroup.None,
                humanReq);

            var travelerFeats = new StatType[] {
                StatType.SkillPersuasion,
                StatType.SkillKnowledgeWorld,
                StatType.SkillPerception
            }.Select(skill => CreateSkillFeat(
                $"WorldTraveler{skill}Trait",
                $"World Traveler — {UIUtility.GetStatText(skill)}",
                travelerDescription,
                Helpers.MergeIds(Helpers.GetSkillFocus(skill).AssetGuid, "9b03b7ff17394007a3fbec18aa42604b"),
                skill)).ToArray();
            worldTraveler.SetFeatures(travelerFeats);
            choices.Add(worldTraveler);

            // Elf:
            // - Dilettante Artist (persuasion)
            // - Forlorn (+1 fort save)
            // - Warrior of the Old (+2 init)
            // - Youthful Mischief (+1 ref)
            choices.Add(CreateSkillFeat("DilettanteArtistTrait", "Dilettante Artist (Elf)",
                "Art for you is a social gateway and you use it to influence and penetrate high society.",
                "ac5a16e72ef74b4884c674dcbb61692c", StatType.SkillPersuasion, elfReq));

            choices.Add(Helpers.CreateFeature("ForlornTrait", "Forlorn (Elf)",
                "Having lived outside of traditional elf society for much or all of your life, you know the world can be cruel, dangerous, and unforgiving of the weak.\nBenefit: You gain a +1 trait bonus on Fortitude saving throws.",
                "1511289c92ea4233b14c4f51072ea10f",
                Helpers.GetIcon("79042cb55f030614ea29956177977c52"), // Great Fortitude
                FeatureGroup.None,
                elfReq,
                Helpers.CreateAddStatBonus(StatType.SaveFortitude, 1, ModifierDescriptor.Trait)));

            choices.Add(Helpers.CreateFeature("WarriorOfOldTrait", "Warrior of Old (Elf)",
                "As a child, you put in long hours on combat drills, and though time has made this training a dim memory, you still have a knack for quickly responding to trouble.\nBenefit: You gain a +2 trait bonus on initiative checks.",
                "dc36a2c52abb4e6dbff549ac65a5a171",
                Helpers.GetIcon("797f25d709f559546b29e7bcb181cc74"), // Improved Initiative
                FeatureGroup.None,
                elfReq,
                Helpers.CreateAddStatBonus(StatType.Initiative, 2, ModifierDescriptor.Trait)));

            choices.Add(Helpers.CreateFeature("YouthfulMischiefTrait", "Youthful Mischeif (Elf)",
                "Though you gave up the life of a padfoot, scout, or minstrel decades before, you still know how to roll with the punches when things turn sour.\nBenefit: You gain a +1 trait bonus on Reflex saves.",
                "bfcc574d1f214455ac369fa46e07200e",
                Helpers.GetIcon("15e7da6645a7f3d41bdad7c8c4b9de1e"), // Lightning Reflexes
                FeatureGroup.None,
                elfReq,
                Helpers.CreateAddStatBonus(StatType.SaveReflex, 1, ModifierDescriptor.Trait)));

            // Half-orc:
            // - Brute (persuasion)
            // - Legacy of Sand (+1 will save)
            var brute = CreateSkillFeat("BruteTrait", "Brute (Half-Orc)",
                "You have worked for a crime lord, either as a low-level enforcer or as a guard, and are adept at frightening away people.",
                "1ee0ce55ace74ccbb798e2fdc13181f6", StatType.SkillPersuasion, halfOrcReq);
            brute.SetIcon(Helpers.GetIcon("885f478dff2e39442a0f64ceea6339c9")); // Intimidating
            choices.Add(brute);

            choices.Add(Helpers.CreateFeature("LegacyOfSandTrait", "Legacy of Sand (Half-Orc)",
                "A large tribe of orcs adapted to life in the desert once dwelt in southeastern Katapesh. Although this tribe is long extinct, some half-orcs of Katapesh carry the traits of this tribe in their particularly large jaws, broad shoulders, and shockingly pale eyes. You often have dreams of hunts and strange ceremonies held under moonlight in the desert sands. Some ascribe these dreams to racial memory, others to visions or prophecies. These dreams have instilled in you a fierce sense of tradition.\nBenefit: You gain a +1 trait bonus on all Will saving throws.",
                "e5fb1675eb6e4ef9accef7eb3a10862a",
                Helpers.GetIcon("175d1577bb6c9a04baf88eec99c66334"), // Iron Will
                FeatureGroup.None,
                halfOrcReq,
                Helpers.CreateAddStatBonus(StatType.SaveWill, 1, ModifierDescriptor.Trait)));

            // Half-elf:
            // - Elven Relexes (+2 initiative)
            // - Failed Apprentice (+1 save arcane spells)
            choices.Add(Helpers.CreateFeature("ElvenReflexsTrait", "Elven Reflexes (Half-Elf)",
                "One of your parents was a member of a wild elven tribe, and you’ve inherited a portion of your elven parent’s quick reflexes.\nBenefit: You gain a +2 trait bonus on initiative checks.",
                "9975678ce2fc420da9cd6ec4fe8c8b9b",
                Helpers.GetIcon("797f25d709f559546b29e7bcb181cc74"), // Improved Initiative
                FeatureGroup.None,
                halfElfReq,
                Helpers.CreateAddStatBonus(StatType.Initiative, 2, ModifierDescriptor.Trait)));

            choices.Add(Helpers.CreateFeature("FailedAprenticeTrait", "Failed Apprentice (Half-Elf)",
                "You have a healthy fear of sorcerers’ speech and wizards’ words that has helped you to survivAs a child, your parents sent you to a distant wizard’s tower as an apprentice so that you could learn the arcane arts. Unfortunately, you had no arcane talent whatsoever, though you did learn a great deal about the workings of spells and how to resist them.\nBenefit: You gain a +1 trait bonus on saves against arcane spells.",
                "8ed66066751f43c2920055dd6358adc8",
                Helpers.GetIcon("2483a523984f44944a7cf157b21bf79c"), // Elven Immunities
                FeatureGroup.None,
                halfElfReq,
                Helpers.Create<SavingThrowBonusAgainstSpellSource>()));

            // Halfling:
            // - Freed Slave (world)
            // - Freedom Fighter (mobility)
            // - Well-Informed (persuasion)
            choices.Add(CreateSkillFeat("FreedSlaveTrait", "Freed Slave (Halfling)",
                "You grew up as a slave and know the ins and outs of nobility better than most.",
                "d2fc5fe0c64142a79e0ebee18f14b0be", StatType.SkillKnowledgeWorld, halflingReq));
            choices.Add(CreateSkillFeat("FreedomFighterTrait", "Freedom Fighter (Halfling)",
                "Your parents allowed escaping slaves to hide in your home, and the stories you’ve heard from them instilled into you a deep loathing of slavery, and a desire to help slaves evade capture and escape.",
                "3a4d2cd14dc446319085c865570ccc3d", StatType.SkillMobility, halflingReq));
            choices.Add(CreateSkillFeat("WellInformedTrait", "Well-Informed (Halfling)",
                "You make it a point to know everyone and to be connected to everything around you. You frequent the best taverns, attend all of the right events, and graciously help anyone who needs it.",
                "940ced5d41594b9aa22ee22217fbd46f", StatType.SkillPersuasion, halflingReq));

            // Dwarf:
            // - Grounded (+2 mobility, +1 reflex)
            // - Militant Merchant (perception)
            // - Ruthless (+1 confirm crits)
            // - Zest for Battle (+1 trait dmg if has morale attack bonus)
            choices.Add(Helpers.CreateFeature("GroundedTrait", "Grounded (Dwarf)",
                "You are well balanced, both physically and mentally.\nBenefit: You gain a +2 trait bonus on Mobility checks, and a +1 trait bonus on Reflex saves.",
                "9b13923527a64c3bbf8de904c5a9ef8b",
                Helpers.GetIcon("3a8d34905eae4a74892aae37df3352b9"), // Skill Focus Stealth (mobility)
                FeatureGroup.None,
                dwarfReq,
                Helpers.CreateAddStatBonus(StatType.SkillMobility, 2, ModifierDescriptor.Trait),
                Helpers.CreateAddStatBonus(StatType.SaveReflex, 1, ModifierDescriptor.Trait)));

            choices.Add(CreateSkillFeat("MilitantMerchantTrait", "Militant Merchant (Dwarf)",
                "You know what it takes to get your goods to market and will stop at nothing to protect your products. Years of fending off thieves, cutthroats, and brigands have given you a sixth sense when it comes to danger.",
                "38226f4ad9ed4211878ef95497d01857", StatType.SkillPerception, dwarfReq));

            choices.Add(Helpers.CreateFeature("RuthlessTrait", "Ruthless (Dwarf)",
                "You never hesitate to strike a killing blow.\nBenefit: You gain a +1 trait bonus on attack rolls to confirm critical hits.",
                "58d18289cb7f4ad4a690d9502d397a3a",
                Helpers.GetIcon("f4201c85a991369408740c6888362e20"), // Improved Critical
                FeatureGroup.None,
                dwarfReq,
                Helpers.Create<CriticalConfirmationBonus>(a => { a.Bonus = 1; a.Value = 0; })));

            choices.Add(Helpers.CreateFeature("ZestForBattleTrait", "Zest for Battle (Dwarf)",
                "Your greatest joy is being in the thick of battle, and smiting your enemies for a righteous or even dastardly cause.\nBenefit: Whenever you have a morale bonus to weapon attack rolls, you also receive a +1 trait bonus on weapon damage rolls.",
                "a987f5e69db44cdd98983985e37a6c2a",
                Helpers.GetIcon("31470b17e8446ae4ea0dacd6c5817d86"), // Weapon Specialization
                FeatureGroup.None,
                dwarfReq,
                Helpers.Create<DamageBonusIfMoraleBonus>()));

            // Gnome:
            // - Animal Friend (+1 will save and lore nature class skill, must have familar or animal companion)
            // - Rapscallion (+1 init, +1 thievery)
            components.Clear();
            components.Add(gnomeReq);
            components.Add(Helpers.Create<AddClassSkill>(a => a.Skill = StatType.SkillLoreNature));
            // TODO: is there a cleaner way to implement this rather than a hard coded list?
            // (Ideally: it should work if a party NPC has a familiar/animal companion too.)
            // See also: PrerequisitePet.
            components.AddRange((new String[] {
                // Animal companions
                "f6f1cdcc404f10c4493dc1e51208fd6f",
                "afb817d80b843cc4fa7b12289e6ebe3d",
                "f9ef7717531f5914a9b6ecacfad63f46",
                "f894e003d31461f48a02f5caec4e3359",
                "e992949eba096644784592dc7f51a5c7",
                "aa92fea676be33d4dafd176d699d7996",
                "2ee2ba60850dd064e8b98bf5c2c946ba",
                "6adc3aab7cde56b40aa189a797254271",
                "ece6bde3dfc76ba4791376428e70621a",
                "126712ef923ab204983d6f107629c895",
                "67a9dc42b15d0954ca4689b13e8dedea",
                // Familiars
                "1cb0b559ca2e31e4d9dc65de012fa82f",
                "791d888c3f87da042a0a4d0f5c43641c",
                "1bbca102706408b4cb97281c984be5d5",
                "f111037444d5b6243bbbeb7fc9056ed3",
                "7ba93e2b888a3bd4ba5795ae001049f8",
                "97dff21a036e80948b07097ad3df2b30",
                "952f342f26e2a27468a7826da426f3e7",
                "61aeb92c176193e48b0c9c50294ab290",
                "5551dd90b1480e84a9caf4c5fd5adf65",
                "adf124729a6e01f4aaf746abbed9901d",
                "4d48365690ea9a746a74d19c31562788",
                "689b16790354c4c4c9b0f671f68d85fc",
                "3c0b706c526e0654b8af90ded235a089",
            }).Select(id => Helpers.Create<AddStatBonusIfHasFact>(a =>
            {
                a.Stat = StatType.SaveWill;
                a.Value = 1;
                a.Descriptor = ModifierDescriptor.Trait;
                a.CheckedFact = library.Get<BlueprintFeature>(id);
            })));

            choices.Add(Helpers.CreateFeature("AnimalFriendTrait", "Animal Friend (Gnome)",
                "You’ve long been a friend to animals, and feel safer when animals are nearby.\nBenefits: You gain a +1 trait bonus on Will saving throws as long as you have an animal companion or familiar, and Lore (Nature) is always a class skill for you.",
                "91c612b225d54adaa4ce4c633501b58e",
                Helpers.GetIcon("1670990255e4fe948a863bafd5dbda5d"), // Boon Companion
                FeatureGroup.None,
                components.ToArray()));

            choices.Add(Helpers.CreateFeature("Rapscallion", "Rapscallion (Gnome)",
                "You’ve spent your entire life thumbing your nose at the establishment and take pride in your run-ins with the law. Somehow, despite all your mischievous behavior, you’ve never been caught.\nBenefits: You gain a +1 trait bonus on Mobility checks and a +1 trait bonus on initiative checks.",
                "4f95abdcc70e4bda818be5b8860585c5",
                Helpers.GetSkillFocus(StatType.SkillMobility).Icon,
                FeatureGroup.None,
                gnomeReq,
                Helpers.CreateAddStatBonus(StatType.SkillMobility, 1, ModifierDescriptor.Trait),
                Helpers.CreateAddStatBonus(StatType.Initiative, 1, ModifierDescriptor.Trait)));

            // Aasimar:
            // - Martyr’s Blood (+1 attack if HP below half).
            // - Toxophilite (+2 crit confirm with bows)
            // - Wary (+1 perception/persuasion)

            // TODO: Enlightened Warrior

            choices.Add(Helpers.CreateFeature("MartyrsBloodTrait", "Martyr’s Blood (Aasimar)",
                "You carry the blood of a self-sacrificing celestial, and strive to live up to your potential for heroism.\nBenefit(s): As long as your current hit point total is less than half of your maximum hit points possible, you gain a +1 trait bonus on attack rolls against evil foes.",
                "729d27ad020d485f843264844f0f2155",
                Helpers.GetIcon("3ea2215150a1c8a4a9bfed9d9023903e"), // Iron Will Improved
                FeatureGroup.None,
                aasimarReq,
                Helpers.Create<AttackBonusIfAlignmentAndHealth>(a =>
                {
                    a.TargetAlignment = AlignmentComponent.Evil;
                    a.Descriptor = ModifierDescriptor.Trait;
                    a.Value = 1;
                    a.HitPointPercent = 0.5f;
                })));

            choices.Add(Helpers.CreateFeature("ToxophiliteTrait", "Toxophilite (Aasimar)",
                "You’ve inherited some of your celestial ancestor’s prowess with the bow.\nBenefit: You gain a +2 trait bonus on attack rolls made to confirm critical hits with bows.",
                "6c434f07c8984971b1d842cecdf144c6",
                Helpers.GetIcon("f4201c85a991369408740c6888362e20"), // Improved Critical
                FeatureGroup.None,
                aasimarReq,
                Helpers.Create<CriticalConfirmationBonus>(a =>
                {
                    a.Bonus = 2;
                    a.Value = 0;
                    a.CheckWeaponRangeType = true;
                    a.Type = AttackTypeAttackBonus.WeaponRangeType.RangedNormal;
                })));

            choices.Add(Helpers.CreateFeature("WaryTrait", "Wary (Aasimar)",
                "You grew up around people who were jealous of and hostile toward you. Perhaps your parents were not pleased to have a child touched by the divine—they may have berated or beaten you, or even sold you into slavery for an exorbitant price. You grew up mistrustful of others and believing your unique appearance to be a curse.\nBenefit: You gain a +1 trait bonus on Persuasion and Perception checks.",
                "7a72a0e956784cc38ea049e503189810",
                Helpers.GetIcon("86d93a5891d299d4983bdc6ef3987afd"), // Persuasive
                FeatureGroup.None,
                aasimarReq,
                Helpers.CreateAddStatBonus(StatType.SkillPersuasion, 1, ModifierDescriptor.Trait),
                Helpers.CreateAddStatBonus(StatType.SkillPerception, 1, ModifierDescriptor.Trait)));

            // Tiefling:
            // - Ever Wary (retain half dex bonus AC during surpise round)
            // - Prolong Magic (racial spell-like abilities get free extend spell)
            // - God Scorn (Demodand heritage; +1 saves vs divine spells)
            // - Shadow Stabber (+2 damage if opponent can't see you)

            choices.Add(Helpers.CreateFeature("EverWaryTrait", "Ever wary (Tiefling)",
                "Constant fear that your fiendish nature might provoke a sudden attack ensures that you never completely let down your guard.\nBenefit During the surprise round and before your first action in combat, you can apply half your Dexterity bonus (if any) to your AC. You still count as flat-footed for the purposes of attacks and effects.",
                "0400c9c99e704a1f81a769aa88044a03",
                Helpers.GetIcon("3c08d842e802c3e4eb19d15496145709"), // uncanny dodge
                FeatureGroup.None,
                tieflingReq,
                Helpers.Create<ACBonusDuringSurpriseRound>()));

            var tieflingHeritageDemodand = library.Get<BlueprintFeature>("a53d760a364cd90429e16aa1e7048d0a");
            choices.Add(Helpers.CreateFeature("GodScornTrait", "God Scorn (Demodand Tiefling)",
                "Your contempt for the gods and their sad little priests makes it easier to shake off the effects of their prayers.\nBenefit You gain a +1 trait bonus on saving throws against divine spells.",
                "db41263f6fd3450ea0a3bc45c98330f7",
                Helpers.GetIcon("2483a523984f44944a7cf157b21bf79c"), // Elven Immunities
                FeatureGroup.None,
                Helpers.PrerequisiteFeature(tieflingHeritageDemodand),
                Helpers.Create<SavingThrowBonusAgainstSpellSource>(s => s.Source = SpellSource.Divine)));

            var tieflingHeritageSelection = library.Get<BlueprintFeatureSelection>("c862fd0e4046d2d4d9702dd60474a181");
            choices.Add(Helpers.CreateFeature("ProlongMagicTrait", "Prolong Magic (Tiefling)",
                "Constant drills and preparation allow you to get more out of your innate magic.\nBenefit Whenever you use a spell - like ability gained through your tiefling heritage, it automatically acts as if affected by the Extend Spell metamagic feat.",
                "820f697f59114993a55c46044c98bf9c",
                tieflingHeritageSelection.Icon,
                FeatureGroup.None,
                tieflingReq,
                // TODO: double check that this actually works for SLAs.
                Helpers.Create<AutoMetamagic>(a => { a.Metamagic = Metamagic.Extend; a.Abilities = CollectTieflingAbilities(tieflingHeritageSelection); })));

            choices.Add(Helpers.CreateFeature("ShadowStabberTrait", "Shadow Stabber (Tiefling)",
                "An instinct for dishonorable conduct serves you well when fighting opponents who are blind, oblivious, or blundering around in the dark.\nBenefit You gain a +2 trait bonus on melee weapon damage rolls made against foes that cannot see you.",
                "b67d04e21a9147e3b8f9bd81ba36f409",
                Helpers.GetIcon("9f0187869dc23744292c0e5bb364464e"), // accomplished sneak attacker
                FeatureGroup.None,
                tieflingReq,
                Helpers.Create<DamageBonusIfInvisibleToTarget>(d => d.Bonus = 2)));

            choices.Add(UndoSelection.Feature.Value);
            raceTraits.SetFeatures(choices);
            adopted.SetFeatures(raceTraits.Features);
            adopted.AddComponent(Helpers.PrerequisiteNoFeature(raceTraits));

            return raceTraits;
        }

        static List<BlueprintAbility> CollectTieflingAbilities(BlueprintFeatureSelection selection)
        {
            var result = new List<BlueprintAbility>();
            foreach (var heritage in selection.AllFeatures)
            {
                foreach (var addFact in heritage.GetComponents<AddFacts>())
                {
                    result.AddRange(addFact.Facts.OfType<BlueprintAbility>());
                }
            }
            return result;
        }

        static BlueprintFeatureSelection CreateCampaignTraits()
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var campaignTraits = Helpers.CreateFeatureSelection("CampaignTrait", "Campaign Trait",
                "Campaign traits are specifically tailored to relate to the Kingmaker campaign.",
                "f3c611a76bbc482c9c15219fa982fa17", null, FeatureGroup.None, noFeature);
            noFeature.Feature = campaignTraits;

            var choices = new List<BlueprintFeature>();
            choices.Add(Helpers.CreateFeature("BastardTrait", "Bastard",
                "One of your parents was a member of one of the great families of Brevoy, perhaps even of the line of Rogarvia itself. Yet you have no substantive proof of your nobility, and you’ve learned that claiming nobility without evidence makes you as good as a liar. While you might own a piece of jewelry, a scrap of once-rich fabric, or an aged confession of love, none of this directly supports your claim. Thus, you’ve lived your life in the shadow of nobility, knowing that you deserve the comforts and esteem of the elite, even though the contempt of fate brings you nothing but their scorn. Whether a recent attempt to prove your heritage has brought down the wrath of a noble family’s henchmen or you merely seek to prove the worth of the blood in your veins, you’ve joined an expedition into the Stolen Lands, hoping to make a name all your own. You take a –1 penalty on all Charisma-based skill checks made when dealing with members of Brevic nobility but gain a +1 trait bonus on Will saves as a result of your stubbornness and individuality. (The penalty aspect of this trait is removed if you ever manage to establish yourself as a true noble.)",
                "d4f7e0915bd941cbac6f655927135817",
                Helpers.GetIcon("175d1577bb6c9a04baf88eec99c66334"), // Iron Will
                FeatureGroup.None,
                Helpers.Create<PrerequisiteFeature>(p => p.Feature = Helpers.human),
                // Other than the Prologue, there aren't many persuasion checks against members of the
                // nobility, prior to becoming a Baron. For simplicity, we simply remove the penalty after level 2.
                // (Ultimately this trait is more for RP flavor than anything.)
                Helpers.CreateAddStatBonusOnLevel(StatType.SkillPersuasion, -1, ModifierDescriptor.Penalty, 1, 2),
                Helpers.CreateAddStatBonus(StatType.SaveWill, 1, ModifierDescriptor.Trait)));

            /* TODO: Noble Born. This will require some adaptation to the game.
            var nobleBorn = Helpers.CreateFeatureSelection("NobleBornTrait", "Noble Born",
                "You claim a tangential but legitimate connection to one of Brevoy’s noble families. If you aren’t human, you were likely adopted by one of Brevoy’s nobles or were instead a favored servant or even a childhood friend of a noble scion. Whatever the cause, you’ve had a comfortable life, but one far from the dignity and decadence your distant cousins know. Although you are associated with an esteemed name, your immediate family is hardly well to do, and you’ve found your name to be more of a burden to you than a boon in many social situations. You’ve recently decided to test yourself, to see if you can face the world without the aegis of a name you have little real claim or care for. An expedition into the storied Stolen Lands seems like just the test to see if you really are worth the title “noble.”",
                "a820521d923f4e569c3c69d091bf8865",
                null,
                FeatureGroup.None);
            choices.Add(nobleBorn);
            var families = new List<BlueprintFeature>();
            // TODO: Garess, Lebeda are hard to adapt to PF:K, need to invent new bonuses.
            // Idea for Garess:
            // - Feather Step SLA 1/day?
            // Lebeda:
            // - Start with extra gold? Or offer a permanent sell price bonus (perhaps 5%?)
            //
            families.Add();
            */

            choices.Add(Helpers.CreateFeature("RostlanderTrait", "Rostlander",
                "You were raised in the south of Brevoy, a land of dense forests and rolling plains, of crystalline rivers and endless sapphire skies. You come from hearty stock and were raised with simple sensibilities of hard work winning well-deserved gains, the importance of charity and compassion, and the value of personal and familial honor. Yours is the country of the Aldori swordlords and the heroes who refused to bend before the armies of a violent conqueror. You care little for matters of politics and nobles or of deception and schemes. As you are thoroughly Brevic, the call for champions willing to expand your land’s influence into the Stolen Lands has inflamed your sense of patriotism and honor, and so you have joined an expedition to quest southward. Your hardy nature grants you a +1 trait bonus on all Fortitude saves.",
                "d99b9398af66406cac173884df308eb7",
                Helpers.GetIcon("79042cb55f030614ea29956177977c52"), // Great Fortitude
                FeatureGroup.None,
                Helpers.CreateAddStatBonus(StatType.SaveFortitude, 1, ModifierDescriptor.Trait)));

            var duelingSword = library.Get<BlueprintWeaponType>("a6f7e3dc443ff114ba68b4648fd33e9f");
            var longsword = library.Get<BlueprintWeaponType>("d56c44bc9eb10204c8b386a02c7eed21");
            choices.Add(Helpers.CreateFeature("SwordScionTrait", "Sword Scion",
                "You have lived all your life in and around the city of Restov, growing up on tales of Baron Sirian Aldori and the exploits of your home city’s heroic and legendary swordlords. Perhaps one of your family members was an Aldori swordlord, you have a contact among their members, or you have dreamed since childhood of joining. Regardless, you idolize the heroes, styles, and philosophies of the Aldori and have sought to mimic their vaunted art. Before you can petition to join their ranks, however, you feel that you must test your mettle. Joining an expedition into the Stolen Lands seems like a perfect way to improve your skills and begin a legend comparable to that of Baron Aldori. You begin play with a longsword or Aldori dueling sword and gain a +1 trait bonus on all attacks and combat maneuvers made with such weapons.",
                "e16eb56b2f964321a29076226dccb29e",
                Helpers.GetIcon("c3a66c1bbd2fb65498b130802d5f183a"), // DuelingMastery
                FeatureGroup.None,
                Helpers.Create<AddStartingEquipment>(a =>
                {
                    a.CategoryItems = new WeaponCategory[] { WeaponCategory.DuelingSword, WeaponCategory.Longsword };
                    a.RestrictedByClass = Array.Empty<BlueprintCharacterClass>();
                    a.BasicItems = Array.Empty<BlueprintItem>();
                }),
                Helpers.Create<WeaponAttackAndCombatManeuverBonus>(a => { a.WeaponType = duelingSword; a.AttackBonus = 1; a.Descriptor = ModifierDescriptor.Trait; }),
                Helpers.Create<WeaponAttackAndCombatManeuverBonus>(a => { a.WeaponType = longsword; a.AttackBonus = 1; a.Descriptor = ModifierDescriptor.Trait; })));

            campaignTraits.SetFeatures(choices);
            return campaignTraits;
        }

        static BlueprintFeatureSelection CreateRegionalTraits()
        {
            var noFeature = Helpers.PrerequisiteNoFeature(null);
            var regionalTraits = Helpers.CreateFeatureSelection("RegionalTrait", "Regional Trait",
                "Regional traits are keyed to specific regions, be they large (such as a nation or geographic region) or small (such as a city or a specific mountain). In order to select a regional trait, your PC must have spent at least a year living in that region. At 1st level, you can only select one regional trait (typically the one tied to your character’s place of birth or homeland), despite the number of regions you might wish to write into your character’s background.",
                "6158dd4ad2544c27bc3a9b48c2e8a2ca", null, FeatureGroup.None, noFeature);
            noFeature.Feature = regionalTraits;

            // TODO: more regional traits.

            // Note: use the generic feat names/text to let players RP this as they choose.
            var choices = new List<BlueprintFeature>();
            var signatureSpell = Helpers.CreateFeatureSelection("SignatureSpellTrait", "Signature Spell",
                "You have learned a mystical secret that empowers your spellcasting.\nBenefit: Pick one spell when you choose this trait—from this point on, whenever you cast that spell, you do so at +1 caster level.",
                "6a3dfe274f45432b85361bdbb0a3009b",
                Helpers.GetIcon("fe9220cdc16e5f444a84d85d5fa8e3d5"), // Spell Specialization Progression
                FeatureGroup.None,
                Helpers.Create<IncreaseCasterLevelForSpell>());
            FillSpellSelection(signatureSpell, 1, 9, Helpers.Create<IncreaseCasterLevelForSpell>());
            choices.Add(signatureSpell);

            var metamagicMaster = Helpers.CreateFeatureSelection("MetamagicMasterTrait", "Metamagic Master",
                "Your ability to alter your spell of choice is greater than expected.\nBenefit: Select one spell of 3rd level or below; when you use the chosen spell with a metamagic feat, it uses up a spell slot one level lower than it normally would.",
                "00844f940e434033ab826e5ff5929011",
                Helpers.GetIcon("ee7dc126939e4d9438357fbd5980d459"), // Spell Penetration
                FeatureGroup.None);
            FillSpellSelection(metamagicMaster, 1, 3, Helpers.Create<ReduceMetamagicCostForSpell>(r => { r.Reduction = 1; r.MaxSpellLevel = 3; }));
            choices.Add(metamagicMaster);

            choices.Add(UndoSelection.Feature.Value);
            regionalTraits.SetFeatures(choices);
            return regionalTraits;
        }

        // Very large spell selections momentarily hang the UI, so we split the spells by level.
        // It also makes it easier to find the spell you're looking for.
        // (There's some `O(N^2)` at least, possibly a higher polynomial in the game code?)
        internal static void FillSpellSelection(BlueprintFeatureSelection selection, int minLevel, int maxLevel, params BlueprintComponent[] components)
        {
            FillSpellSelection(selection, minLevel, maxLevel, null, (_) => components);
        }

        internal static void FillSpellSelection(BlueprintFeatureSelection selection, int minLevel, int maxLevel, BlueprintSpellList spellList, Func<int, BlueprintComponent[]> createComponents, BlueprintCharacterClass learnSpellClass = null)
        {
            var choices = new List<BlueprintFeature>();
            for (int level = minLevel; level <= maxLevel; level++)
            {
                var spellChoice = Helpers.CreateParamSelection<SelectAnySpellAtLevel>(
                    $"{selection.name}Level{level}",
                    $"{selection.Name} (Spell Level {level})",
                    selection.Description,
                    Helpers.MergeIds(selection.AssetGuid, FavoredClassBonus.spellLevelGuids[level - 1]),
                    null,
                    FeatureGroup.None,
                    createComponents(level));
                spellChoice.SpellList = spellList;
                spellChoice.SpellLevel = level;
                spellChoice.SpellcasterClass = learnSpellClass;
                spellChoice.CheckNotKnown = learnSpellClass != null;
                choices.Add(spellChoice);
            }
            choices.Add(UndoSelection.Feature.Value);
            selection.SetFeatures(choices);
        }


        static BlueprintFeature CreateSkillFeat(String name, String displayName, String description, String assetId, StatType skill, params BlueprintComponent[] extraComponents)
        {
            var components = extraComponents.ToList();
            components.Add(Helpers.Create<AddClassSkill>(a => a.Skill = skill));
            components.Add(Helpers.CreateAddStatBonus(skill, 1, ModifierDescriptor.Trait));
            var skillText = UIUtility.GetStatText(skill);
            return Helpers.CreateFeature(name,
                displayName,
                $"{description}\nBenefits: You gain a +1 trait bonus on {skillText} checks, and {skillText} is always a class skill for you.",
                assetId,
                Helpers.GetSkillFocus(skill).Icon,
                FeatureGroup.None,
                components.ToArray());
        }
    }

    [ComponentName("Add stat bonus based on character level")]
    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowedOn(typeof(BlueprintUnit))]
    [AllowMultipleComponents]
    public class AddStatBonusOnLevel : AddStatBonus, IHandleEntityComponent<UnitEntityData>, IUnitGainLevelHandler
    {
        public int MinLevel = 1;

        public int MaxLevelInclusive = 20;

        public override void OnTurnOn()
        {
            if (CheckLevel(Owner)) base.OnTurnOn();
        }

        void IHandleEntityComponent<UnitEntityData>.OnEntityCreated(UnitEntityData entity)
        {
            if (Fact == null && CheckLevel(entity.Descriptor)) base.OnEntityCreated(entity);
        }

        protected virtual bool CheckLevel(UnitDescriptor unit)
        {
            int level = unit.Progression.CharacterLevel;
            return level >= MinLevel && level <= MaxLevelInclusive;
        }

        void IUnitGainLevelHandler.HandleUnitGainLevel(UnitDescriptor unit, BlueprintCharacterClass @class)
        {
            if (unit != Owner) return;
            OnTurnOff();
            OnTurnOn();
        }
    }

    [AllowedOn(typeof(BlueprintParametrizedFeature))]
    public class IncreaseCasterLevelForSpell : ParametrizedFeatureComponent, IInitiatorRulebookHandler<RuleCalculateAbilityParams>
    {
        public int Bonus = 1;
        public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            var spell = Param.Blueprint;
            if (evt.Spell != spell && evt.Spell?.Parent != spell) return;
            Log.Write($"Increase caster level of {spell.name} by {Bonus}");
            evt.AddBonusCasterLevel(Bonus);
        }

        public void OnEventDidTrigger(RuleCalculateAbilityParams evt) { }
    }

    // Implements Magical Knack's +2 CL (up to character level) bonus.
    //
    // Note: this is implemented as a rulebook event bonus, rather than increasing the
    // Spellbook's m_CasterLevelInternal, because that variable is used to determine
    // spells per day/spells known. Magical Knack should not affect those variables.
    //
    // This unfortunately means that some things do not properly account for the bonus/
    // The spellbook UI is fixed with a patch.
    [AllowedOn(typeof(BlueprintParametrizedFeature))]
    public class IncreaseCasterLevelUpToCharacterLevel : OwnedGameLogicComponent<UnitDescriptor>, IInitiatorRulebookHandler<RuleCalculateAbilityParams>
    {
        public int MaxBonus = 2;
        public void OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            var spellbook = evt.Spellbook;
            if (spellbook == null) return;

            int bonus = GetBonus(spellbook);
            Log.Write($"Increase caster level of {evt.Spell?.name} by {bonus}");
            evt.AddBonusCasterLevel(bonus);
        }

        public void OnEventDidTrigger(RuleCalculateAbilityParams evt) { }

        internal int GetBonus(Spellbook spellbook)
        {
            return Math.Min(spellbook.Owner.Progression.CharacterLevel - spellbook.CasterLevel, MaxBonus);
        }

        static IncreaseCasterLevelUpToCharacterLevel()
        {
            Main.ApplyPatch(typeof(SpellBookCharacteristics_Setup_Patch), "Magical Knack showing caster level in spellbook UI");
        }
    }

    // Selects any spell at `SpellLevel`, either from the provided `SpellList` or from all spells.
    //
    // If `CheckNotKnown` is set, it will also check that the `SpellcasterClass` spellbook does not
    // already contain this spell.
    public class SelectAnySpellAtLevel : CustomParamSelection
    {
        public bool CheckNotKnown;

        protected override IEnumerable<BlueprintScriptableObject> GetItems(UnitDescriptor beforeLevelUpUnit, UnitDescriptor previewUnit)
        {
            if (SpellList != null)
            {
                return SpellList.SpellsByLevel[SpellLevel].Spells;
            }

            // For traits: it's valid to take any spell, even one not from your current
            // class that you may be able to cast later.
            var spells = new List<BlueprintAbility>();
            foreach (var spell in Helpers.allSpells)
            {
                if (spell.Parent != null) continue;

                var spellLists = spell.GetComponents<SpellListComponent>();
                if (spellLists.FirstOrDefault() == null) continue;

                var level = spellLists.Min(l => l.SpellLevel);
                if (level == SpellLevel) spells.Add(spell);
            }
            return spells;
        }

        protected override IEnumerable<BlueprintScriptableObject> GetAllItems() => Helpers.allSpells;

        protected override bool CanSelect(UnitDescriptor unit, FeatureParam param)
        {
            // TODO: this doesn't seem to work.
            return !CheckNotKnown || !unit.GetSpellbook(SpellcasterClass).IsKnown(param.Blueprint as BlueprintAbility);
        }
    }


    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowMultipleComponents]
    public class SavingThrowBonusAgainstSpellSource : RuleInitiatorLogicComponent<RuleSavingThrow>
    {
        public int Value = 1;
        public SpellSource Source = SpellSource.Arcane;

        public ModifierDescriptor ModifierDescriptor = ModifierDescriptor.Trait;

        public override void OnEventAboutToTrigger(RuleSavingThrow evt)
        {
            var ability = evt.Reason?.Ability;
            if (ability != null && ability.Blueprint.Type == AbilityType.Spell &&
                ability.SpellSource == Source)
            {
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveWill.AddModifier(Value, this, ModifierDescriptor));
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveReflex.AddModifier(Value, this, ModifierDescriptor));
                evt.AddTemporaryModifier(evt.Initiator.Stats.SaveFortitude.AddModifier(Value, this, ModifierDescriptor));
            }
        }

        public override void OnEventDidTrigger(RuleSavingThrow evt) { }
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowMultipleComponents]
    public class WeaponAttackAndCombatManeuverBonus : RuleInitiatorLogicComponent<RuleCalculateAttackBonusWithoutTarget>, IInitiatorRulebookHandler<RuleCalculateCMB>
    {
        public BlueprintWeaponType WeaponType;
        public int AttackBonus;
        public ModifierDescriptor Descriptor;

        public override void OnEventAboutToTrigger(RuleCalculateAttackBonusWithoutTarget evt)
        {
            var weaponType = evt.Weapon?.Blueprint.Type;
            if (weaponType == WeaponType)
            {
                var bonus = AttackBonus * Fact.GetRank();

                // TODO: this bonus should be a "trait" bonus. But doing it this way shows up in the UI.
                evt.AddBonus(bonus, Fact);
                //evt.AddTemporaryModifier(evt.Initiator.Stats.AdditionalAttackBonus.AddModifier(bonus, this, ModifierDescriptor.Trait));
            }
        }
        public override void OnEventDidTrigger(RuleCalculateAttackBonusWithoutTarget evt) { }

        public void OnEventAboutToTrigger(RuleCalculateCMB evt)
        {
            var weaponType = evt.Initiator.GetThreatHand()?.Weapon?.Blueprint.Type;
            if (weaponType == WeaponType)
            {
                var bonus = AttackBonus * Fact.GetRank();
                // TODO: this bonus should be a "trait" bonus. But doing it this way shows up in the UI.
                evt.AddBonus(bonus, Fact);
                //evt.AddTemporaryModifier(evt.Initiator.Stats.AdditionalCMB.AddModifier(bonus, this, ModifierDescriptor.Trait));
            }
        }
        public void OnEventDidTrigger(RuleCalculateCMB evt) { }
    }

    [AllowedOn(typeof(BlueprintFeature))]
    public class DamageBonusIfMoraleBonus : RuleInitiatorLogicComponent<RuleCalculateWeaponStats>
    {
        public override void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
        {
            var attackBonus = evt.Initiator.Stats.AdditionalAttackBonus;
            if (attackBonus.ContainsModifier(ModifierDescriptor.Morale) &&
                attackBonus.GetDescriptorBonus(ModifierDescriptor.Morale) > 0)
            {
                evt.AddBonusDamage(1);
            }
        }

        public override void OnEventDidTrigger(RuleCalculateWeaponStats evt) { }
    }

    [AllowedOn(typeof(BlueprintFeature))]
    public class ExtraLuckBonus : RuleInitiatorLogicComponent<RuleCalculateWeaponStats>, IInitiatorRulebookHandler<RuleCalculateAttackBonusWithoutTarget>, IInitiatorRulebookHandler<RuleSkillCheck>, IInitiatorRulebookHandler<RuleSavingThrow>
    {
        void IncreaseLuckBonus(RulebookEvent evt, StatType stat)
        {
            int luck = GetLuckBonus(evt, stat);
            if (luck > 0)
            {
                var mod = Owner.Stats.GetStat(stat).AddModifier(luck + 1, this, ModifierDescriptor.Luck);
                evt.AddTemporaryModifier(mod);
            }
        }

        int GetLuckBonus(RulebookEvent evt, StatType stat)
        {
            var value = Owner.Stats.GetStat(stat);
            if (value.ContainsModifier(ModifierDescriptor.Luck))
            {
                return value.GetDescriptorBonus(ModifierDescriptor.Luck);
            }
            return 0;
        }

        public override void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
        {
            int luck = GetLuckBonus(evt, StatType.AdditionalDamage);
            if (luck > 0) evt.AddBonusDamage(1);
        }
        public void OnEventAboutToTrigger(RuleCalculateAttackBonusWithoutTarget evt)
        {
            int luck = GetLuckBonus(evt, StatType.AdditionalAttackBonus);
            if (luck > 0) evt.AddBonus(1, Fact);
        }
        public void OnEventAboutToTrigger(RuleSavingThrow evt) => IncreaseLuckBonus(evt, evt.StatType);
        public void OnEventAboutToTrigger(RuleSkillCheck evt) => IncreaseLuckBonus(evt, evt.StatType);

        public override void OnEventDidTrigger(RuleCalculateWeaponStats evt) { }
        public void OnEventDidTrigger(RuleCalculateAttackBonusWithoutTarget evt) { }
        public void OnEventDidTrigger(RuleSavingThrow evt) { }
        public void OnEventDidTrigger(RuleSkillCheck evt) { }
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowMultipleComponents]
    public class AttackBonusIfAlignmentAndHealth : RuleInitiatorLogicComponent<RuleCalculateAttackBonus>
    {
        public AlignmentComponent TargetAlignment;

        public ModifierDescriptor Descriptor;

        public int Value;

        public float HitPointPercent;

        public override void OnEventAboutToTrigger(RuleCalculateAttackBonus evt)
        {
            var hpPercent = ((float)Owner.HPLeft) / Owner.MaxHP;
            //Log.Write($"RuleCalculateAttackBonus HP {hpPercent}%, alignment {evt.Target.Descriptor.Alignment.Value}, TargetAlignment {TargetAlignment}, bonus {Value}, descriptor {Descriptor}");
            if (hpPercent < HitPointPercent &&
                evt.Target.Descriptor.Alignment.Value.HasComponent(TargetAlignment))
            {
                // TODO: this bonus should be a "trait" bonus. But doing it this way shows up in the UI.
                evt.AddBonus(Value, Fact);
                //evt.AddTemporaryModifier(evt.Initiator.Stats.AdditionalAttackBonus.AddModifier(Value, this, Descriptor));
            }
        }

        public override void OnEventDidTrigger(RuleCalculateAttackBonus evt) { }
    }


    // This adds support for a feat adding additional selections  (e.g. Additional Traits, Dragon Magic).
    //
    // The game doesn't natively support this, except via BlueprintProgression. However,
    // BlueprintProgression doesn't work for things you select later, because it only adds
    // the current level's features. Essentially, progressions are only designed to work for
    // class features awarded at fixed levels (typically 1st level). There isn't a notion of
    // progressions that are relative to the level you picked them at.
    //
    // So to support adding selections, we patch SelectFeature.Apply to add the follow-up features.
    //
    // However that wouldn't work for cases where a feat can change the progression level, as with
    // Greater Eldritch Heritage.
    //
    // TODO: alternative design2: use IUnitGainFactHandler. I think I tried that and it didn't work,
    // but don't recall why (unit not active during chargen?).
    [Harmony12.HarmonyPatch(typeof(SelectFeature), "Apply", new Type[] { typeof(LevelUpState), typeof(UnitDescriptor) })]
    static class SelectFeature_Apply_Patch
    {
        internal static Dictionary<BlueprintFeature, Action<LevelUpState, UnitDescriptor>> onApplyFeature = new Dictionary<BlueprintFeature, Action<LevelUpState, UnitDescriptor>>();

        static SelectFeature_Apply_Patch() => Main.ApplyPatch(typeof(SelectFeature_Apply_Patch), "Feats that offer 2 selections, such as Additional Traits, Spell Blending, etc");

        static void Postfix(SelectFeature __instance, LevelUpState state, UnitDescriptor unit)
        {
            try
            {
                var self = __instance;
                var item = self.Item;
                if (item == null) return;

                Action<LevelUpState, UnitDescriptor> action;
                if (onApplyFeature.TryGetValue(item.Feature, out action))
                {
                    action(state, unit);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class ACBonusDuringSurpriseRound : RuleTargetLogicComponent<RuleCalculateAC>
    {
        public override void OnEventAboutToTrigger(RuleCalculateAC evt)
        {
            var combatState = Owner.Unit.CombatState;
            if (combatState.IsInCombat && combatState.IsWaitingInitiative)
            {
                evt.AddBonus(Owner.Stats.AC.DexterityBonus / 2, Fact);
            }
        }

        public override void OnEventDidTrigger(RuleCalculateAC evt)
        {
            throw new NotImplementedException();
        }
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    public class UndoSelection : ComponentAppliedOnceOnLevelUp
    {
        public static Lazy<BlueprintFeature> Feature = new Lazy<BlueprintFeature>(() =>
            Helpers.CreateFeature("UndoSelectionChoice", "(Go back)",
                "Select this to go back to the previous selection, allowing you to pick something else.",
                "48963ed6422b41e5ba23d1f3f0fbe7c7", null, FeatureGroup.None,
                Helpers.Create<UndoSelection>()));

        protected override void Apply(LevelUpState state)
        {
            Log.Write($"{GetType().Name}: trying to unselect");
            var selection = state.Selections.FirstOrDefault(s => s.SelectedItem?.Feature == Fact.Blueprint);
            if (selection != null)
            {
                Log.Write($"Unselecting selection {selection.Index}");
                Game.Instance.UI.CharacterBuildController.LevelUpController.UnselectFeature(selection);
            }
        }

        protected override bool RemoveAfterLevelUp => true;
    }


    [AllowedOn(typeof(BlueprintUnitFact))]
    [AllowMultipleComponents]
    public class DamageBonusAgainstFlankedTarget : RuleInitiatorLogicComponent<RuleCalculateDamage>
    {
        public int Bonus;

        public static DamageBonusAgainstFlankedTarget Create(int bonus)
        {
            var d = Helpers.Create<DamageBonusAgainstFlankedTarget>();
            d.Bonus = bonus;
            return d;
        }

        public override void OnEventAboutToTrigger(RuleCalculateDamage evt)
        {
            if (evt.Target.CombatState.IsFlanked && evt.DamageBundle.Weapon?.Blueprint.IsMelee == true)
            {
                evt.DamageBundle.WeaponDamage?.AddBonusTargetRelated(Bonus);
            }
        }

        public override void OnEventDidTrigger(RuleCalculateDamage evt) { }
    }

    public class DamageBonusIfInvisibleToTarget : RuleInitiatorLogicComponent<RuleCalculateDamage>
    {
        public int Bonus;

        public override void OnEventAboutToTrigger(RuleCalculateDamage evt)
        {
            if (evt.DamageBundle.Weapon?.Blueprint.IsMelee != true) return;

            var initiator = evt.Initiator;
            var target = evt.Target;
            // Flat-footed isn't enough, but we need to run the rule to assess the other variables
            // (such as IgnoreVisibility and IgnoreConcealment)
            var rule = Rulebook.Trigger(new RuleCheckTargetFlatFooted(initiator, target));
            if (rule.IsFlatFooted)
            {
                var targetCannotSeeUs = target.Descriptor.State.IsHelpless || // sleeping, etc
                    !target.Memory.Contains(initiator) && !rule.IgnoreVisibility || // hasn't seen us, e.g. stealth/ambush
                    UnitPartConcealment.Calculate(target, initiator) == Concealment.Total && !rule.IgnoreConcealment; // invisibility/blindness etc

                if (targetCannotSeeUs)
                {
                    evt.DamageBundle.First?.AddBonusTargetRelated(Bonus);
                }
            }
        }

        public override void OnEventDidTrigger(RuleCalculateDamage evt) { }
    }

    [Harmony12.HarmonyPatch(typeof(SpellBookCharacteristics), "Setup", new Type[0])]
    static class SpellBookCharacteristics_Setup_Patch
    {
        static void Postfix(SpellBookCharacteristics __instance)
        {
            var self = __instance;
            try
            {
                var controller = Game.Instance.UI.SpellBookController;
                var spellbook = controller.CurrentSpellbook;
                if (spellbook != null && spellbook.CasterLevel > 0)
                {
                    int bonus = 0;
                    foreach (var feat in spellbook.Owner.Progression.Features.Enumerable)
                    {
                        foreach (var c in feat.SelectComponents<IncreaseCasterLevelUpToCharacterLevel>())
                        {
                            //bonus = Math.Max(bonus, c.GetBonus(spellbook));
                            bonus += c.GetBonus(spellbook);
                        }
                    }
                    if (bonus > 0)
                    {
                        self.CasterLevel.text = (spellbook.CasterLevel + bonus).ToString();
                        self.Concentration.text = (spellbook.GetConcentration() + bonus).ToString();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}