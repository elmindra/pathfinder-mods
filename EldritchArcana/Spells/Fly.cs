// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.ResourceLinks;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using UnityEngine;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class FlySpells
    {
        internal static BlueprintAbility flySpell, overlandFlight;

        static LibraryScriptableObject library => Main.library;

        static AbilitySpawnFx commonTransmutationFx => Spells.commonTransmutationFx;

        internal static void Load()
        {
            Main.SafeLoad(FixWingsImmunities, "Wings/flight provides immunity to ground spells/trip");

            // Note: Air Walk is generated first, because Fly and Overland Flight suppress it.
            // (they're generally better due to the speed boost).
            var airWalkBuff = LoadAirWalk();
            LoadFly(airWalkBuff);
        }

        static void FixWingsImmunities()
        {
            // Flying creatures should not be affected by ground spells, or combat
            // maneuvers such as trip.
            //
            // Note: some spells like Web can be anchored to points above ground,
            // so they can catch flying/air walking creatures. However, a character
            // that was aware of the web (i.e. after it had been cast, or after
            // untangling themselves) should be able to fly/walk over it safely.
            // Not sure how to implement that.
            //
            // TODO: add other spells as needed.
            var factIds = new string[] {
                "95851f6e85fe87d4190675db0419d112", // Grease
                "5f9910ccdd124294e905b391d01b4ade", // GreaseBuff
                "e48638596c955a74c8a32dbc90b518c1", // ObsidianFlow
                "7a9542dc5650a634a8c085d7e9993716", // ObsidianFlowChargeBuff
                "6e2eeb312ec681d4b9d089e53399a168", // ObsidianFlowDifficultTerrainBuff
                "d675f7d2254711d4b8fc069925874a06", // ObsidianFlowEntangledBuff
                "7d700cdf260d36e48bb7af3a8ca5031f", // TarPool
                "a3cbe662a479aa14eafdeb10ef555f68", // TarPoolDazeBuff
                "525e4ff20086404419b3aab63917d6a0", // TarPoolDifficultTerrainBuff
                "631d255f6b89afe45b32cf66c35a4205", // TarPoolEntangledBuff
            };

            foreach (var factId in factIds)
            {
                var fact = library.Get<BlueprintUnitFact>(factId);
                var component = fact.GetComponent<SpellDescriptorComponent>();
                if (component == null)
                {
                    fact.AddComponent(SpellDescriptor.Ground.CreateSpellDescriptor());
                }
                else
                {
                    component.Descriptor = component.Descriptor.Value | SpellDescriptor.Ground;
                }
            }

            foreach (var buff in wingBuffs.Value)
            {
                buff.AddComponents(SpellDescriptor.Ground.CreateBuffImmunity(), SpellDescriptor.Ground.CreateSpellImmunity(),
                    Helpers.Create<ManeuverImmunity>(m => m.Type = CombatManeuver.Trip));
            }

            // Fiery Body should grant flight bonuses too.
            var buffWingsDraconicRed = library.Get<BlueprintBuff>("08ae1c01155a2184db869e9ebedc758d");
            var fieryBodyBuff = library.Get<BlueprintBuff>("b574e1583768798468335d8cdb77e94c");
            fieryBodyBuff.AddComponent(Helpers.CreateAddFactContextActions(
                buffWingsDraconicRed.CreateApplyBuff(Helpers.CreateContextDuration(0),
                    fromSpell: true, toCaster: true, asChild: true, permanent: true)));
        }


        static void LoadFly(BlueprintBuff airWalkBuff)
        {
            var wings = wingBuffs.Value;
            // Wings all have the generic wing icon, which is fine, but bloodline icons are nicer.
            var wingToIcon = new Dictionary<BlueprintBuff, Sprite>();
            foreach (var bloodline in Helpers.bloodlineSelection.AllFeatures.OfType<BlueprintProgression>())
            {
                foreach (var feat in bloodline.GetLevelEntry(15).Features)
                {
                    var addFacts = feat.GetComponent<AddFacts>();
                    if (addFacts == null) continue;
                    var ability = addFacts.Facts.OfType<BlueprintActivatableAbility>()
                        .FirstOrDefault(a => wings.Contains(a.Buff));
                    if (ability != null)
                    {
                        wingToIcon.Add(ability.Buff, bloodline.Icon);
                        break;
                    }
                }
            }

            var variants = new List<FlySpellInfo>();
            foreach (var wing in wings)
            {
                var name = wing.name.Replace("BuffWings", "");
                if (name.StartsWith("Draconic")) name = $"{name.Replace("Draconic", "")} Dragon";
                variants.Add(CreateFly(wing, name, wingToIcon[wing]));
            }

            // Each Fly variant needs to suppress wing buffs, air walk, and previous fly buffs
            var suppressBuffs = wingBuffs.Value.ToList();
            suppressBuffs.Add(airWalkBuff);
            foreach (var variant in variants)
            {
                variant.FlyBuff.AddComponent(Helpers.CreateSuppressBuffs(suppressBuffs));
                suppressBuffs.Add(variant.FlyBuff);
            }
            // Each Overland flight variant needs to suppress all the buffs Fly suppresses,
            // all fly buffs, and previous overland flight buffs.
            foreach (var variant in variants)
            {
                variant.OverlandFlightBuff.AddComponent(Helpers.CreateSuppressBuffs(suppressBuffs));
                suppressBuffs.Add(variant.OverlandFlightBuff);
            }

            var fly = library.CopyAndAdd(variants[0].Fly, "FlySpell", "20ab2cd848c04d46882625e696c921bf");
            fly.SetName("Fly");
            fly.SetComponents(
                SpellSchool.Transmutation.CreateSpellComponent(),
                Helpers.CreateSpellDescriptor(),
                fly.CreateAbilityVariants(variants.Select(v => v.Fly)));
            fly.EffectOnAlly = AbilityEffectOnUnit.Helpful;
            fly.AvailableMetamagic = Metamagic.Quicken | Metamagic.Extend | Metamagic.Heighten;
            fly.AddToSpellList(Helpers.wizardSpellList, 3);
            fly.AddToSpellList(Helpers.magusSpellList, 3);
            fly.AddToSpellList(Helpers.alchemistSpellList, 3);
            Helpers.AddSpellAndScroll(fly, "8f1956fa46b122b4f86c1ce383ad8af7", 0); // scroll righteous might
            FlySpells.flySpell = fly;

            // Fix Draconic bloodline to use Fly as the 3rd level choice.
            WishSpells.FixBloodlineSpell(fly, "7bd143ead2d6c3a409aad6ee22effe34", "606cdd9198fb270429ab6dce1a6b14f1");
            // Fix Travel domain to use Fly for the 3rd level spell.
            fly.FixDomainSpell(3, "ab90308db82342f47bf0d636fe941434");

            var overlandFlight = library.CopyAndAdd(variants[0].OverlandFlight, "OverlandFlight", "8b5ea075097e4c7e999266b7569ee39d");
            overlandFlight.SetName("Overland Flight");
            overlandFlight.SetComponents(
                SpellSchool.Transmutation.CreateSpellComponent(),
                Helpers.CreateSpellDescriptor(),
                overlandFlight.CreateAbilityVariants(variants.Select(v => v.OverlandFlight)));
            overlandFlight.EffectOnAlly = AbilityEffectOnUnit.Helpful;
            overlandFlight.AvailableMetamagic = Metamagic.Extend | Metamagic.Quicken | Metamagic.Heighten;
            overlandFlight.AddToSpellList(Helpers.wizardSpellList, 5);
            overlandFlight.AddToSpellList(Helpers.magusSpellList, 5);
            overlandFlight.AddToSpellList(Helpers.alchemistSpellList, 5);
            Helpers.AddSpellAndScroll(overlandFlight, "8f1956fa46b122b4f86c1ce383ad8af7", 0); // scroll righteous might
            FlySpells.overlandFlight = overlandFlight;
        }

        static FlySpellInfo CreateFly(BlueprintBuff baseWingsBuff, String wingsName, Sprite spellIcon)
        {
            var flyBuff = library.CopyAndAdd(baseWingsBuff, $"Fly{baseWingsBuff.name}", "79d9e52c48a0422b9b4ac39c956d088a", baseWingsBuff.AssetGuid);

            // TODO: a quick look over sorcerer bloodlines indicates that their wings should also grant
            // a speed bonus. Aasimar feat wings should not. Also ground immunity should probably be added to all.

            // Note: this spell is adapted from PnP.
            //
            // PF:K grants +3 dodge bonus from having wings, and immunity to difficult terrain, which is a nice adaptation.
            //
            // This spell also grants a movement speed increase (similar to Expeditious Retreat) and a Mobility bonus.
            // The Mobility bonus is to convert the Fly skill bonus, but also: if you can fly, obstacles like walking
            // across a tree or jumping over a gap shouldn't be as challenging.
            //
            // I'd also like to grant immunity to ground-based effects (e.g. Grease), as it doesn't really make sense
            // for a flying player to get caught in that. Not sure how to implement it though, short of listing the
            // spells explicitly (SpellDescriptor.Ground isn't used by spells like Grease).
            var wingsDescription = "The wings grant a +3 dodge bonus to AC against melee attacks, immunity to difficult terrain, and a bonus on Mobility skill checks equal to 1/2 your caster level. This does not stack with other spells that grant wings or flight.";
            var flySpell = Helpers.CreateAbility($"Fly{baseWingsBuff.name.Replace("Buff", "Spell")}", $"Fly — {wingsName}",
                "The subject gains a pair of wings, and movement speed increases to 60 feet (or 40 feet if it wears medium or heavy armor, or if it carries a medium or heavy load).\n" +
                wingsDescription,
                Helpers.MergeIds("5e2cc60e071a44e09ada9cbb299d654d", baseWingsBuff.AssetGuid),
                spellIcon, AbilityType.Spell,
                CommandType.Standard, AbilityRange.Touch, Helpers.minutesPerLevelDuration, "",
                SpellSchool.Transmutation.CreateSpellComponent(),
                Helpers.CreateContextRankConfig(),
                Helpers.CreateDeliverTouch(),
                commonTransmutationFx,
                Helpers.CreateRunActions(
                    flyBuff.CreateApplyBuff(
                        Helpers.CreateContextDuration(rate: DurationRate.Minutes),
                        fromSpell: true)));
            flySpell.CanTargetSelf = true;
            flySpell.CanTargetFriends = true;
            flySpell.EffectOnAlly = AbilityEffectOnUnit.Helpful;

            flyBuff.AddComponents(
                Helpers.CreateAddStatBonusScaled(StatType.SkillMobility,
                    ModifierDescriptor.Other, Helpers.CreateBuffScaling(divModifier: 2)),
                Helpers.Create<BuffMovementSpeed>(b =>
                {
                    b.CappedOnMultiplier = true;
                    b.MultiplierCap = 2;
                    b.Value = 30;
                }));

            flyBuff.SetNameDescription(flySpell);

            var flyCast = flySpell.CreateTouchSpellCast();

            var overlandBuff = library.CopyAndAdd(flyBuff, $"OverlandFlight{baseWingsBuff.name}", "52347193617844a59da6ebc0c1a41921", baseWingsBuff.AssetGuid);

            var overlandFlight = library.CopyAndAdd(flySpell, $"OverlandFlight{baseWingsBuff.name.Replace("Buff", "")}",
                "c4c313ae43694d36b5c62492e033c25b", baseWingsBuff.AssetGuid);
            overlandFlight.SetNameDescriptionIcon($"Overland Flight — {wingsName}",
                "This spell grants you a pair of wings, increasing your movement speed is increased to 40 feet (30 feet if wearing medium or heavy armor, or if carrying a medium or heavy load). This spell is designed for long-distance movement, and prevents fatigue for the duration of the spell.\n" +
                wingsDescription, spellIcon);
            overlandFlight.LocalizedDuration = Helpers.hourPerLevelDuration;
            overlandFlight.CanTargetFriends = false;
            overlandFlight.Range = AbilityRange.Personal;
            var spellComponents = flySpell.ComponentsArray.Where(
                c => !(c is AbilityDeliverTouch) && !(c is AbilityEffectRunAction)).ToList();
            spellComponents.Add(Helpers.CreateRunActions(
                overlandBuff.CreateApplyBuff(
                    Helpers.CreateContextDuration(rate: DurationRate.Hours),
                    fromSpell: true, toCaster: true)));
            spellComponents.Add(Helpers.CreateSuppressBuffs()); // for backwards compatibility
            overlandFlight.SetComponents(spellComponents);

            overlandBuff.SetNameDescription(overlandFlight);
            var buffComponents = flyBuff.ComponentsArray.Select(c =>
            {
                if (!(c is BuffMovementSpeed)) return c;
                return Helpers.Create<BuffMovementSpeed>(b =>
                {
                    b.CappedOnMultiplier = true;
                    b.MultiplierCap = 1.5f;
                    b.Value = 10;
                });
            }).ToList();
            buffComponents.Add(UnitCondition.Fatigued.CreateImmunity());
            buffComponents.Add(SpellDescriptor.Fatigue.CreateBuffImmunity());
            overlandBuff.SetComponents(buffComponents);
            return new FlySpellInfo(flyCast, overlandFlight, flyBuff, overlandBuff);
        }

        static BlueprintBuff LoadAirWalk()
        {
            var wings = wingBuffs.Value;
            var buffAngelWings = wings[0];
            var featherStep = library.Get<BlueprintAbility>("f3c0b267dd17a2a45a40805e31fe3cd1");
            var airWalkBuff = library.CopyAndAdd(buffAngelWings, "AirWalkBuff", "7d37b0529b3144b3ac1899614f1e29f5");
            // Air walk does not grant wings visually, as that doesn't seem to match the theme of the spell.
            //
            // Air Walk is similar to Fly, but it doesn't provide movement speed bonus, and provides a flat +10
            // to Mobility. (Moving over/around obstacles is easy if you can walk on air!)
            // Also in PnP, Air Walk is nice because it doesn't require Fly checks, so that's reflected in
            // the higher, fixed mobility boost.

            airWalkBuff.FxOnStart = new PrefabLink();

            var airWalk = Helpers.CreateAbility("AirWalk", "Air Walk",
                "The subject can tread on air as if walking on solid ground. This grants a +3 dodge bonus to AC against melee attacks, immunity to difficult terrain, and +10 to Mobility skill checks. This does not stack with other spells that grant wings or flight.",
                "dea7c9fd73aa40b89bb2a641e83d2b8e", featherStep.Icon, AbilityType.Spell,
                CommandType.Standard, AbilityRange.Touch, Helpers.tenMinPerLevelDuration, "",
                SpellSchool.Transmutation.CreateSpellComponent(),
                Helpers.CreateContextRankConfig(),
                Helpers.CreateDeliverTouch(),
                commonTransmutationFx,
                Helpers.CreateRunActions(
                    airWalkBuff.CreateApplyBuff(
                        Helpers.CreateContextDuration(rate: DurationRate.TenMinutes),
                        fromSpell: true)));
            airWalk.CanTargetSelf = true;
            airWalk.CanTargetFriends = true;
            airWalk.EffectOnAlly = AbilityEffectOnUnit.Helpful;
            airWalk.AvailableMetamagic = Metamagic.Extend | Metamagic.Quicken | Metamagic.Heighten;

            airWalkBuff.SetNameDescriptionIcon(airWalk);
            airWalkBuff.AddComponents(
                Helpers.CreateSuppressBuffs(wings),
                Helpers.CreateAddStatBonus(StatType.SkillMobility, 10, ModifierDescriptor.Other));

            var airWalkCast = airWalk.CreateTouchSpellCast();
            airWalkCast.AddToSpellList(Helpers.clericSpellList, 4);
            airWalkCast.AddToSpellList(Helpers.druidSpellList, 4);
            airWalkCast.AddToSpellList(Helpers.alchemistSpellList, 4);
            Helpers.AddSpellAndScroll(airWalkCast, "1b3b15e90ba582047a40f2d593a70e5e"); // scroll feather step.

            // Note: duration is not divided, and area is 30ft, to match similar communal abilities in PF:K
            var communalWalk = library.CopyAndAdd(airWalk, "AirWalkCommunal", "591043d80ad54fe1af29d4cbf3141ca6");
            communalWalk.SetNameDescriptionIcon("Air Walk, Communal",
                $"The caster and all allies in a 30-feet radius gain the benefits of Air Walk:\n{airWalk.Description}",
                airWalk.Icon);
            communalWalk.CanTargetFriends = false;
            communalWalk.Range = AbilityRange.Personal;

            communalWalk.SetComponents(airWalk.ComponentsArray.Select(
                c => c is AbilityDeliverTouch ? Helpers.CreateAbilityTargetsAround(30.Feet(), TargetType.Ally) : c));
            communalWalk.AddToSpellList(Helpers.clericSpellList, 5);
            communalWalk.AddToSpellList(Helpers.druidSpellList, 5);
            Helpers.AddSpellAndScroll(communalWalk, "1b3b15e90ba582047a40f2d593a70e5e"); // scroll feather step.
            return airWalkBuff;
        }

        static readonly Lazy<BlueprintBuff[]> wingBuffs = new Lazy<BlueprintBuff[]>(() => new String[] {
            "d596694ff285f3f429528547f441b1c0", // angel
            "3c958be25ab34dc448569331488bee27", // demon
            "38431e32f0e210342968d3a997eb233e", // devil
            "ddfe6e85e1eed7a40aa911280373c228", // black dragon
            "800cde038f9e6304d95365edc60ab0a4", // blue dragon
            "7f5acae38fc1e0f4c9325d8a4f4f81fc", // brass
            "482ee5d001527204bb86e34240e2ce65", // bronze
            "a25d6fc69cba80548832afc6c4787379", // copper
            "984064a3dd0f25444ad143b8a33d7d92", // gold
            "a4ccc396e60a00f44907e95bc8bf463f", // green
            "08ae1c01155a2184db869e9ebedc758d", // red
            "5a791c1b0bacee3459d7f5137fa0bd5f", // silver
            "381a168acd79cd54baf87a17ca861d9b", // white
        }.Select(library.Get<BlueprintBuff>).ToArray());
    }

    class FlySpellInfo
    {
        public readonly BlueprintAbility Fly, OverlandFlight;
        public readonly BlueprintBuff FlyBuff, OverlandFlightBuff;

        internal FlySpellInfo(BlueprintAbility fly, BlueprintAbility overlandFlight, BlueprintBuff flyBuff, BlueprintBuff overlandBuff)
        {
            this.Fly = fly;
            this.OverlandFlight = overlandFlight;
            this.FlyBuff = flyBuff;
            this.OverlandFlightBuff = overlandBuff;
        }
    }
}