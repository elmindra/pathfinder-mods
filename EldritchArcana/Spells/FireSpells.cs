// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums.Damage;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Actions;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.Utility;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class FireSpells
    {
        internal static BlueprintAbility wallOfFire, delayedBlastFireball, incendiaryCloud, meteorSwarm;
        internal static BlueprintBuff delayedBlastFireballBuff;
        internal const string incendiaryCloudAreaId = "55e718bec0ea45caafb63ebad37d1829";

        static LibraryScriptableObject library => Main.library;

        internal static void Load()
        {
            Main.SafeLoad(LoadWallOfFire, "Wall of Fire");
            Main.SafeLoad(LoadDelayedBlastFireball, "Delayed Blast Fireball");
            Main.SafeLoad(LoadIncendiaryCloud, "Incendiary Cloud");
            Main.SafeLoad(LoadMeteorSwarm, "Meteor Swarm");
        }

        static void LoadIncendiaryCloud()
        {
            var cloudArea = library.CopyAndAdd<BlueprintAbilityAreaEffect>(
                "a892a67daaa08514cb62ad8dcab7bd90", // IncendiaryCloudArea, used by brass golem breath
                "IncendiaryCloudSpellArea",
                incendiaryCloudAreaId);

            // TODO: should be 20ft.
            // Visual effects can probably be scaled by hooking IAreaEffectHandler,
            // and getting AreaEffectView.m_SpawnedFx, then .GetComponentsInChildren<ParticleSystem>(),
            // then .transform.localScale or something like that.
            cloudArea.Size = 15.Feet();
            cloudArea.SetComponents(Helpers.CreateAreaEffectRunAction(round:
                Helpers.CreateActionSavingThrow(SavingThrowType.Reflex,
                    Helpers.CreateActionDealDamage(DamageEnergyType.Fire,
                        DiceType.D6.CreateContextDiceValue(6), halfIfSaved: true))));

            var spell = Helpers.CreateAbility("IncendiaryCloud", "Incendiary Cloud",
                "An incendiary cloud spell creates a cloud of roiling smoke shot through with white-hot embers. The smoke obscures all sight as a fog cloud does. In addition, the white-hot embers within the cloud deal 6d6 points of fire damage to everything within the cloud on your turn each round. All targets can make Reflex saves each round to take half damage.\n" +
                "As with a cloudkill spell, the smoke moves away from you at 10 feet per round. Figure out the smoke’s new spread each round based on its new point of origin, which is 10 feet farther away from where you were when you cast the spell. By concentrating, you can make the cloud move as much as 60 feet each round.Any portion of the cloud that would extend beyond your maximum range dissipates harmlessly, reducing the remainder’s spread thereafter.\n" +
                "As with fog cloud, wind disperses the smoke, and the spell can’t be cast underwater.",
                "85923af68485439dac5c3e9ddd2dd66c",
                Helpers.GetIcon("e3d0dfe1c8527934294f241e0ae96a8d"), // fire storm
                AbilityType.Spell, CommandType.Standard, AbilityRange.Medium,
                Helpers.roundsPerLevelDuration, Helpers.reflexHalfDamage,
                Helpers.CreateSpellComponent(SpellSchool.Conjuration),
                Helpers.CreateSpellDescriptor(SpellDescriptor.Fire),
                FakeTargetsAround.Create(cloudArea.Size),
                Helpers.CreateContextRankConfig(),
                Helpers.CreateRunActions(Helpers.Create<ContextActionSpawnAreaEffect>(c =>
                {
                    c.DurationValue = Helpers.CreateContextDuration();
                    c.AreaEffect = cloudArea;
                })));
            spell.SpellResistance = false;
            spell.CanTargetEnemies = true;
            spell.CanTargetPoint = true;
            spell.CanTargetFriends = true;
            spell.CanTargetPoint = true;
            spell.EffectOnAlly = AbilityEffectOnUnit.Harmful;
            spell.EffectOnEnemy = AbilityEffectOnUnit.Harmful;
            spell.AvailableMetamagic = Metamagic.Empower | Metamagic.Extend | Metamagic.Maximize | Metamagic.Quicken | Metamagic.Heighten | Metamagic.Reach;
            incendiaryCloud = spell;
            spell.AddToSpellList(Helpers.wizardSpellList, 8);
            Helpers.AddSpellAndScroll(spell, "1cbb88fbf2a6bb74aa437fadf6946d22"); // scroll fire storm
            spell.FixDomainSpell(8, "d8f30625d1b1f9d41a24446cbf7ac52e"); // fire domain
        }

        static void LoadDelayedBlastFireball()
        {
            // Note: this was reworked a bit. It does not spawn an item.
            // The original version did use an item, and it worked (the code is still in ExperimentalSpells).
            //
            // But this version has nicer UX, IMO: you get the AOE targeting circle, and you can see the
            // buff tick down, and the projectile is fired later from the caster's position, which looks neat.
            // And it doesn't spawn the loot bag. I'm more confident it'll work correctly with saves and such.
            var fireball = library.Get<BlueprintAbility>("2d81362af43aeac4387a3d4fced489c3");
            var spell = Helpers.CreateAbility("DelayedBlastFireball", "Delayed Blast Fireball",
                "This spell functions like fireball, except that it is more powerful and can detonate up to 5 rounds after the spell is cast. The burst of flame deals 1d6 points of fire damage per caster level (maximum 20d6). " +
                "The glowing bead created by delayed blast fireball can detonate immediately if you desire, or you can choose to delay the burst for as many as 5 rounds. " +
                "You select the amount of delay upon completing the spell.",//", and that time cannot change once it has been set unless someone touches the bead. If you choose a delay, the glowing bead sits at its destination until it detonates. " +
                                                                            //"A creature can pick up and hurl the bead as a thrown weapon (range increment 10 feet). If a creature handles and moves the bead within 1 round of its detonation, there is a 25% chance that the bead detonates while being handled.",
                "dfe891561c4d48ed8235268b0e7692e7",
                fireball.Icon, AbilityType.Spell, CommandType.Standard, fireball.Range,
                "5 rounds or less; see text", fireball.LocalizedSavingThrow);
            spell.SpellResistance = true;
            spell.AvailableMetamagic = Metamagic.Empower | Metamagic.Heighten | Metamagic.Maximize | Metamagic.Quicken | Metamagic.Reach;

            var delayIds = new String[] {
                "1e403a3188214a5c94ad63ede5928f81",
                "2b6efa3759d842f7a549b85712784ee2",
                "d762acc02c71446b834723ac20eb722a",
                "2ca70c4525574cba8661beaef0a6b35f",
                "45f6b2f4c3ce424d98d269548691d6bc",
                "c1b683e809c348428011f0ed2e9da67b",
            };

            var spell0 = library.CopyAndAdd(fireball, $"{spell.name}Delay0", delayIds[0]);

            spell0.SetNameDescriptionIcon(spell);

            spell0.ReplaceContextRankConfig(c =>
            {
                Helpers.SetField(c, "m_UseMax", false);
                Helpers.SetField(c, "m_Max", 20);
            });
            spell0.SpellResistance = true;

            var buff = Helpers.CreateBuff($"{spell.name}Buff", spell.Name, spell.Description, "fc9490e3a7d24723a017609397521ea1",
                spell.Icon, null,
                Helpers.CreateAddFactContextActions(
                    deactivated: ActionCastSpellWithOriginalParams.Create(spell0)));
            buff.Stacking = StackingType.Stack;
            delayedBlastFireballBuff = buff;

            var variants = new List<BlueprintAbility> { spell0 };
            for (int delay = 1; delay <= 5; delay++)
            {
                var delaySpell = library.CopyAndAdd(spell0, $"{spell.name}Delay{delay}", delayIds[delay]);
                delaySpell.SetName($"{spell.Name} ({delay} rounds)");
                delaySpell.SetComponents(
                    spell0.GetComponent<SpellComponent>(),
                    FakeTargetsAround.Create(20.Feet(), toCaster: true),
                    Helpers.CreateRunActions(
                        Helpers.CreateApplyBuff(buff, Helpers.CreateContextDuration(delay),
                            fromSpell: true, dispellable: false, toCaster: true)));
                variants.Add(delaySpell);
            }

            spell.SetComponents(
                Helpers.CreateSpellComponent(fireball.School),
                Helpers.CreateSpellDescriptor(fireball.SpellDescriptor),
                spell.CreateAbilityVariants(variants));

            spell.AddToSpellList(Helpers.wizardSpellList, 7);
            Helpers.AddSpellAndScroll(spell, "5b172c2c3e356eb43ba5a8f8008a8a5a", 1); // scroll of fireball
            delayedBlastFireball = spell;
        }

        static void LoadMeteorSwarm()
        {
            var fireball = library.Get<BlueprintAbility>("2d81362af43aeac4387a3d4fced489c3");

            var spell = library.CopyAndAdd(fireball, "MeteorSwarm", "2d18f8a4de6742e2ba954da0f19a4957");
            spell.SetNameDescriptionIcon("Meteor Swarm",
                "Meteor swarm is a very powerful and spectacular spell that is similar to fireball in many aspects. When you cast it, four 2-foot-diameter spheres spring from your outstretched hand and streak in straight lines to the spots you select. The meteor spheres leave a fiery trail of sparks.\n" +
                "If you aim a sphere at a specific creature, you may make a ranged touch attack to strike the target with the meteor.Any creature struck by a sphere takes 2d6 points of bludgeoning damage(no save) and has to make a saving throw against +4 DC for the sphere’s fire damage(see below).If a targeted sphere misses its target, it simply explodes at the nearest corner of the target’s space. You may aim more than one sphere at the same target.\n" +
                "Once a sphere reaches its destination, it explodes in a 40-foot-radius spread, dealing 6d6 points of fire damage to each creature in the area.If a creature is within the area of more than one sphere, it must save separately against each.Despite stemming from separate spheres, all of the fire damage is added together after the saves have been made, and fire resistance is applied only once.",
                Helpers.GetIcon("f72f8f03bf0136c4180cd1d70eb773a5")); // controlled fireball
            spell.LocalizedSavingThrow = Helpers.CreateString($"{spell.name}.SavingThrow", "None or Reflex half, see text");

            var deliverProjectile = UnityEngine.Object.Instantiate(fireball.GetComponent<AbilityDeliverProjectile>());
            var fireballProjectile = deliverProjectile.Projectiles[0];
            deliverProjectile.Projectiles.AddToArray(fireballProjectile, fireballProjectile);

            var fire24d6 = Helpers.CreateActionDealDamage(DamageEnergyType.Fire,
                DiceType.D6.CreateContextDiceValue(24), isAoE: true, halfIfSaved: true);
            var fireDmgSave = Helpers.CreateActionSavingThrow(SavingThrowType.Reflex, fire24d6);

            var conditionalDCIncrease = Harmony12.AccessTools.Inner(typeof(ContextActionSavingThrow), "ConditionalDCIncrease");
            var dcAdjustArray = Array.CreateInstance(conditionalDCIncrease, 1);
            var dcAdjust = Activator.CreateInstance(conditionalDCIncrease);
            // If the main target was hit by the ranged touch attack, increase the DC by 4.
            Helpers.SetField(dcAdjust, "Condition", Helpers.CreateConditionsCheckerAnd(ContextConditionNearMainTarget.Create()));
            Helpers.SetField(dcAdjust, "Value", AbilitySharedValue.StatBonus.CreateContextValue());
            dcAdjustArray.SetValue(dcAdjust, 0);
            Helpers.SetField(fireDmgSave, "m_ConditionalDCIncrease", dcAdjustArray);

            var increaseDCBy4 = Helpers.Create<ContextActionChangeSharedValue>(c =>
            {
                c.SharedValue = AbilitySharedValue.StatBonus;
                c.SetValue = 4;
                c.Type = SharedValueChangeType.Set;
            });

            var physical2d6 = Helpers.CreateActionDealDamage(PhysicalDamageForm.Bludgeoning,
                DiceType.D6.CreateContextDiceValue(2));

            spell.SetComponents(
                SpellSchool.Evocation.CreateSpellComponent(),
                SpellDescriptor.Fire.CreateSpellDescriptor(),
                deliverProjectile,
                Helpers.CreateAbilityTargetsAround(40.Feet(), TargetType.Any),
                Helpers.CreateRunActions(
                    Helpers.CreateConditional(
                        // Check if this unit is the primary target
                        ContextConditionNearMainTarget.Create(),
                        // Perform a ranged touch attack on the primary target
                        ContextActionRangedTouchAttack.Create(
                            // If ranged touch succeeded:
                            // - 4 meteors hit for 2d6 bludgeoning damage each.
                            // - target takes 24d6 fire damage, -4 penalty to reflex save for half
                            new GameAction[] {
                                physical2d6, physical2d6, physical2d6, physical2d6,
                                increaseDCBy4
                            })),
                    // Apply fire damage to all targets in range.
                    fireDmgSave));
            spell.AddToSpellList(Helpers.wizardSpellList, 9);
            Helpers.AddSpellAndScroll(spell, "5b172c2c3e356eb43ba5a8f8008a8a5a"); // scroll of fireball
            meteorSwarm = spell;
        }

        static void LoadWallOfFire()
        {
            // Convert the Evocation school's Elemental Wall (Sp) ability to a spell.
            var areaEffect = library.CopyAndAdd<BlueprintAbilityAreaEffect>("ac8737ccddaf2f948adf796b5e74eee7",
                "WallOfFireAreaEffect", "ff829c48791146aa9203fa243603d807");
            var spell = wallOfFire = library.CopyAndAdd<BlueprintAbility>("77d255c06e4c6a745b807400793cf7b1",
                "WallOfFire", "3d3be0a2b36f456384c6f5d50bc6daf2");

            var areaComponents = areaEffect.ComponentsArray.Where(c => !(c is ContextRankConfig)).ToList();
            areaComponents.Add(Helpers.CreateContextRankConfig());
            areaEffect.SetComponents(areaComponents);

            spell.name = "WallOfFire";
            spell.SetNameDescriptionIcon("Wall of Fire",
                "An immobile, blazing curtain of shimmering violet fire springs into existence. One side of the wall, selected by you, sends forth waves of heat, dealing 2d4 points of fire damage to creatures within 10 feet and 1d4 points of fire damage to those past 10 feet but within 20 feet. The wall deals this damage when it appears, and to all creatures in the area on your turn each round. In addition, the wall deals 2d6 points of fire damage + 1 point of fire damage per caster level (maximum +20) to any creature passing through it. The wall deals double damage to undead creatures.\n" +
                "If you evoke the wall so that it appears where creatures are, each creature takes damage as if passing through the wall. If any 5-foot length of wall takes 20 points or more of cold damage in 1 round, that length goes away. (Do not divide cold damage by 2, as normal for objects.)",
                Helpers.GetIcon("9256a86aec14ad14e9497f6b60e26f3f")); // BlessingOfTheSalamander
            spell.Type = AbilityType.Spell;
            spell.SpellResistance = true;
            spell.Parent = null;
            spell.AvailableMetamagic = Metamagic.Empower | Metamagic.Extend | Metamagic.Maximize | Metamagic.Quicken | Metamagic.Heighten | Metamagic.Reach;
            var components = spell.ComponentsArray.Where(
                c => !(c is AbilityResourceLogic) && !(c is ContextRankConfig) && !(c is AbilityEffectRunAction)).ToList();
            components.Add(Helpers.CreateRunActions(Helpers.Create<ContextActionSpawnAreaEffect>(c =>
            {
                c.AreaEffect = areaEffect;
                c.DurationValue = Helpers.CreateContextDuration();
            })));
            components.Add(Helpers.CreateContextRankConfig());
            spell.SetComponents(components);
            spell.AddToSpellList(Helpers.wizardSpellList, 4);
            spell.AddToSpellList(Helpers.magusSpellList, 4);
            spell.AddToSpellList(Helpers.druidSpellList, 5);
            spell.AddSpellAndScroll("4b0ff254dca06894cba7eace7eef6bfe"); // scroll controlled fireball
            spell.FixDomainSpell(4, "d8f30625d1b1f9d41a24446cbf7ac52e"); // fire domain
        }
    }

    // Like ContextActionCastSpell, but preserves the spell's original ability params (caster level, DC, etc).
    public class ActionCastSpellWithOriginalParams : BuffAction
    {
        public BlueprintAbility Spell;

        public bool IsInstant;

        public static ActionCastSpellWithOriginalParams Create(BlueprintAbility spell, bool instant = false)
        {
            var a = Helpers.Create<ActionCastSpellWithOriginalParams>();
            a.Spell = spell;
            a.IsInstant = instant;
            return a;
        }

        public override string GetCaption() => $"Cast spell {Spell.name} with original ability params" + (IsInstant ? " instantly" : "");

        public override void RunAction()
        {
            try
            {
                //Log.Append($"{GetType().Name}.RunAction()");
                var context = Buff.Context.SourceAbilityContext;
                var ability = context.Ability;
                //Log.Append($"  ability {ability} caster {context.Caster.CharacterName} fact {ability.Fact}");
                //Log.Append($"  spellbook {ability.Spellbook?.Blueprint} main target {context.MainTarget}");
                //Log.Flush();
                var spellData = new AbilityData(Spell, context.Caster.Descriptor, ability.Fact, ability.Spellbook?.Blueprint);
                spellData.OverrideSpellLevel = Context.Params.SpellLevel;
                var castSpell = new RuleCastSpell(spellData, context.MainTarget);
                // Disable spell failure: we already cast the spell.
                castSpell.IsCutscene = true;
                castSpell = Rulebook.Trigger(castSpell);
                if (IsInstant) castSpell.ExecutionProcess.InstantDeliver();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    // Like ContextConditionIsMainTarget, but fixes targeting for spells that can also
    // be cast at points (these spells always record a point as their target)
    //
    // TODO: AbilityEffectRunActionOnClickedTarget might be the way to solve this with
    // the game's existing components.
    public class ContextConditionNearMainTarget : ContextCondition
    {
        public static ContextConditionNearMainTarget Create() => Helpers.Create<ContextConditionNearMainTarget>();

        protected override string GetConditionCaption() => "Is near main target?";

        protected override bool CheckCondition()
        {
            var mainTarget = Context.MainTarget;
            var target = Target;
            Log.Append($"{GetType().Name}: main target {mainTarget}, target {target}");
            if (mainTarget.IsUnit || !target.IsUnit) return mainTarget == target;
            var distance = target.Unit.DistanceTo(mainTarget.Point);
            Log.Append($"  distance: {distance}, corpulence: {target.Unit.Corpulence}");
            return distance < target.Unit.Corpulence;
        }
    }
}
