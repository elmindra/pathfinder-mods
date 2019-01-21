// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.Controllers.Combat;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UI.Log;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.Utility;
using Newtonsoft.Json;
using static Kingmaker.UI.GenericSlot.EquipSlotBase;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class Spells
    {
        internal const string hypnoticPatternId = "bd623ae7179a4d19a40b977ffca1b83f";

        static LibraryScriptableObject library => Main.library;

        internal static AbilitySpawnFx commonTransmutationFx;

        internal static void Load()
        {
            var angelicAspect = library.Get<BlueprintAbility>("75a10d5a635986641bfbcceceec87217");
            commonTransmutationFx = angelicAspect.GetComponent<AbilitySpawnFx>();

            Main.SafeLoad(FixElementalFormSpellcasting, "Enable spellcasting for elemental forms");

            Main.SafeLoad(DismissSpell.Load, "Ability to dismiss area effects");

            Main.SafeLoad(FireSpells.Load, "Fire spells");
            Main.SafeLoad(FlySpells.Load, "Fly and Air Walk spells");
            Main.SafeLoad(TimeStop.Load, "Time Stop");
            Main.SafeLoad(KnockAndDetectSecrets.Load, "Knock and Detect Secret Doors");

            Main.SafeLoad(ExperimentalSpells.LoadSpritualWeapon, "Spiritual Weapon");

            Main.SafeLoad(LoadGreaterMagicWeapon, "Greater Magic Weapon");
            Main.SafeLoad(LoadWeaponOfAwe, "Weapon of Awe");

            Main.SafeLoad(LoadHypnoticPattern, "Hypnotic Pattern");

            // TODO: divine spell: Blessing of Fervor, Atonement (w/ option to change to your alignment?)
            // TODO: blood money would be interesting.
            // Also lots of fun utility spells:
            //
            // - Web Bolt: ranged touch single target web
            // - Floating Disk: A temporary group carry boost
            // - Darting Duplicate: A single copy mirror image (enemies can ignore with will save though)
            // - Jump: target gets a bonus to Acrobatics
            // - Warding Weapon: caster does not provoke AoO when casting
            // - Communal Mount: increase map movement speed
            // - Touch of Idiocy
            // - Twilight Haze: A group stealth boost
            //
            // Interesting ones:
            // - Alarm / Bed of Iron: probably not difficult, if I can find the code that handles resting.
            // - Spectral Hand: not too bad if it just increases touch range
            //   (ignoring that the hand should technically be targetable).
            //   Worst case it could be a temporary large thievery boost.
            // - Charm person: maybe can work similar to confuse/daze, needs investigation.
            //
            // - Battering Blast (force): https://www.d20pfsrd.com/magic/all-spells/b/battering-blast/
            // - Forceful Strike: https://www.d20pfsrd.com/magic/all-spells/f/forceful-strike
            // - Fire seeds: https://www.d20pfsrd.com/magic/all-spells/f/fire-seeds/

            // TODO: Teleport/Shadow Walk/Greater Teleport?
            //
            // The biggest issue with these spells is they can break campaign scripts,
            // which rely on triggering events at certain locations. So to avoid that,
            // instead of an actual teleport, they could be a large movement speed bonus
            // on the world map (and possibly the normal map, but not too high to
            // break trigger scripts).
            //
            // Basically: cast teleport, exit to world map, and you can now move without time passing
            // or encumbrance. Nothing is revealed during this travel, so you can only go along routes
            // you've been previously (that removes the mishap chance).
            //
            // Shadow walk would be Teleport without the distance limit, and you can bring a full party
            // once you get the spell.
            //
            // Perhaps Greater Teleport allows you to see things as you travel. (Conceptually combine
            // scrying the route with movement.)
            //
            // Note: Teleport has a limit of how many people can be brought along.
            // You'd need to be 18th level to transport a full party of 6 (ignoring pets),
            // or you need 2 arcane casters. Since PF:K has bigger party size, we can improve
            // the scaling to 1 person/2 levels (instead of 1 per 3). That would mean the full party
            // can be transported at level 12, same as PnP.
        }

        static void FixElementalFormSpellcasting()
        {
            var spellIds = new string[] {
                "690c90a82bf2e58449c6b541cb8ea004", // elemental body i, ii, iii, iv
                "6d437be73b459594ab103acdcae5b9e2",
                "459e6d5aab080a14499e13b407eb3b85",
                "376db0590f3ca4945a8b6dc16ed14975"
            };
            foreach (var spellId in spellIds)
            {
                var baseSpell = library.Get<BlueprintAbility>(spellId);
                foreach (var spell in baseSpell.Variants)
                {
                    var buff = spell.GetComponent<AbilityEffectRunAction>().Actions.Actions
                            .OfType<ContextActionApplyBuff>().First().Buff;
                    buff.AddComponent(AddMechanicsFeature.MechanicsFeatureType.NaturalSpell.CreateAddMechanics());
                }
            }
        }

        static void LoadWeaponOfAwe()
        {
            var shaken = library.Get<BlueprintBuff>("25ec6cb6ab1845c48a95f9c20b034220");

            var enchantment = Helpers.Create<BlueprintWeaponEnchantment>();
            enchantment.name = "WeaponOfAweEnchantment";
            Helpers.SetLocalizedStringField(enchantment, "m_EnchantName", "Weapon of Awe");
            Helpers.SetLocalizedStringField(enchantment, "m_Description", "+2 sacred bonus on damage rolls, and target is shaken for 1 round on critical hits");
            Helpers.SetLocalizedStringField(enchantment, "m_Prefix", "");
            Helpers.SetLocalizedStringField(enchantment, "m_Suffix", "");
            library.AddAsset(enchantment, "21985d11a0f941a2b359c48b5d8a32da");
            enchantment.SetComponents(Helpers.Create<WeaponOfAweLogic>(w => w.Buff = shaken));

            var paladinWeaponBond = library.Get<BlueprintAbility>("7ff088ab58c69854b82ea95c2b0e35b4");
            var spell = Helpers.CreateAbility("WeaponOfAwe", "Weapon of Awe",
                "You transform a single weapon into an awe-inspiring instrument. The weapon gains a +2 sacred bonus on damage rolls, and if the weapon scores a critical hit, the target of that critical hit becomes shaken for 1 round with no saving throw.\n" +
                "This is a mind-affecting fear effect. A ranged weapon affected by this spell applies these effects to its ammunition.\n" +
                // TODO: does this work for an unarmed strike?
                "You can’t cast this spell on a natural weapon, but you can cast it on an unarmed strike.",
                "9c98a1de91a54ba583b9f4880d505766",
                paladinWeaponBond.Icon,
                AbilityType.Spell, CommandType.Standard, AbilityRange.Close,
                Helpers.minutesPerLevelDuration, "",
                Helpers.CreateContextRankConfig(),
                Helpers.CreateSpellComponent(SpellSchool.Transmutation),
                Helpers.CreateRunActions(Helpers.Create<ContextActionEnchantWornItem>(c =>
                {
                    c.Enchantment = enchantment;
                    c.Slot = SlotType.PrimaryHand;
                    c.DurationValue = Helpers.CreateContextDuration(rate: DurationRate.Minutes);
                })));
            spell.CanTargetSelf = true;
            spell.NeedEquipWeapons = true;
            // Note: the paladin animation is neat, but it's very long.
            //spell.Animation = paladinWeaponBond.Animation;
            var arcaneWeaponSwitchAbility = library.Get<BlueprintAbility>("3c89dfc82c2a3f646808ea250eb91b91");
            spell.Animation = arcaneWeaponSwitchAbility.Animation;
            spell.AvailableMetamagic = Metamagic.Quicken | Metamagic.Heighten | Metamagic.Extend;
            spell.CanTargetFriends = true;

            spell.AddToSpellList(Helpers.clericSpellList, 2);
            spell.AddToSpellList(Helpers.inquisitorSpellList, 2);
            spell.AddToSpellList(Helpers.paladinSpellList, 2);
            Helpers.AddSpellAndScroll(spell, "5739bf41893fddf4f98f8bd6a86b0a52"); // disrupting wepaon scroll
        }

        static void LoadHypnoticPattern()
        {
            var rainbowPattern = library.Get<BlueprintAbility>("4b8265132f9c8174f87ce7fa6d0fe47b");
            var rainbowPatternBuff = library.Get<BlueprintBuff>("6477ae917b0ec7a4ca76bc9f36b023ac");

            var spell = library.CopyAndAdd(rainbowPattern, "HypnoticPattern", hypnoticPatternId);
            spell.SetNameDescription("Hypnotic Pattern",
                "A twisting pattern of subtle, shifting colors weaves through the air, fascinating creatures within it. Roll 2d4 and add your caster level (maximum 10) to determine the total number of HD of creatures affected. Creatures with the fewest HD are affected first; and, among creatures with equal HD, those who are closest to the spell’s point of origin are affected first. HD that are not sufficient to affect a creature are wasted. Affected creatures become fascinated by the pattern of colors. Sightless creatures are not affected.");
            spell.LocalizedDuration = Helpers.CreateString($"{spell.name}.Duration", "Concentration + 2 rounds");

            var buff = library.CopyAndAdd(rainbowPatternBuff, $"{spell.name}Buff", "d5a5ac267e21484a9332d96f3be3452d");
            buff.SetNameDescription(spell.Name, spell.Description);

            // duration is 2 rounds after concentration expires
            buff.AddComponent(SpellConcentrationLogic.Create(Helpers.CreateContextDuration(2)));

            var constructType = library.Get<BlueprintFeature>("fd389783027d63343b4a5634bd81645f");
            var undeadType = library.Get<BlueprintFeature>("734a29b693e9ec346ba2951b27987e33");
            var bloodlineUndeadArcana = library.Get<BlueprintFeature>("1a5e7191279e7cd479b17a6ca438498c");

            spell.SetComponents(
                SpellSchool.Illusion.CreateSpellComponent(),
                Helpers.CreateAbilityTargetsAround(10.Feet(), TargetType.Enemy),
                rainbowPattern.GetComponent<SpellDescriptorComponent>(),
                // Adjust HD affected: 2d4 + caster level (max 10).
                Helpers.CreateCalculateSharedValue(
                    DiceType.D4.CreateContextDiceValue(2, AbilityRankType.StatBonus.CreateContextValue())),
                // Caster level max 10.
                Helpers.CreateContextRankConfig(type: AbilityRankType.StatBonus, max: 10),
                rainbowPattern.GetComponent<AbilitySpawnFx>(),
                Helpers.CreateRunActions(Helpers.CreateConditional(
                    Helpers.CreateConditionsCheckerOr(
                        // Can't apply if:
                        // - it's unconcious,
                        // - a construct,
                        // - undead (unless caster has undead bloodline arcnaa),
                        // - or insufficient HD remaining.
                        Helpers.Create<ContextConditionIsUnconscious>(),
                        constructType.CreateConditionHasFact(),
                        Helpers.CreateAndLogic(false, undeadType.CreateConditionHasFact(), bloodlineUndeadArcana.CreateConditionCasterHasFact(not: true)),
                        Helpers.Create<ContextConditionHitDice>(c => { c.AddSharedValue = true; c.Not = true; })
                    ),
                    null,
                    ifFalse: new GameAction[] {
                        SavingThrowType.Will.CreateActionSavingThrow(
                            // Will save faled: apply buff (permanent, buff uses concentration+2).
                            Helpers.CreateConditionalSaved(null, failed:
                                buff.CreateApplyBuff(Helpers.CreateContextDuration(0), fromSpell: true, permanent: true))),
                        // Regardless of will save, subtract these HD.
                        SharedValueChangeType.SubHD.CreateActionChangeSharedValue()
                    })));

            spell.AddToSpellList(Helpers.wizardSpellList, 2);
            spell.AddToSpellList(Helpers.bardSpellList, 2);
            Helpers.AddSpellAndScroll(spell, "84cd707a7ae9f934389ed6bbf51b023a"); // scroll rainbow pattern
        }

        static void LoadGreaterMagicWeapon()
        {
            var enchantments = new String[] {
                "d704f90f54f813043a525f304f6c0050",
                "9e9bab3020ec5f64499e007880b37e52",
                "d072b841ba0668846adeb007f623bd6c",
                "6a6a0901d799ceb49b33d4851ff72132",
                "746ee366e50611146821d61e391edf16",
            }.Select(library.Get<BlueprintWeaponEnchantment>).ToArray();

            var name = "GreaterMagicWeapon";
            for (int i = 0; i < enchantments.Length; i++)
            {
                var enchant = library.CopyAndAdd(enchantments[i], $"{name}Bonus{i + 1}", enchantments[i].AssetGuid,
                    "01a963207ccb484897f3de00344cad55");
                enchant.SetComponents(Helpers.Create<GreaterMagicWeaponBonusLogic>(g => g.EnhancementBonus = i + 1));
                enchantments[i] = enchant;
            }

            var enchantItem = Helpers.Create<ContextActionGreaterMagicWeapon>();
            enchantItem.Enchantments = enchantments;
            enchantItem.DurationValue = Helpers.CreateContextDuration(rate: DurationRate.Hours);

            var arcaneWeaponSwitchAbility = library.Get<BlueprintAbility>("3c89dfc82c2a3f646808ea250eb91b91");
            var spell = Helpers.CreateAbility(name, "Greater Magic Weapon",
                "This spell functions like magic weapon, except that it gives a weapon an enhancement bonus on attack and damage rolls of +1 per four caster levels (maximum +5). This bonus does not allow a weapon to bypass damage reduction aside from magic.",
                "6e513ce66905424eb441755cd264fbfa",
                arcaneWeaponSwitchAbility.Icon,
                AbilityType.Spell, CommandType.Standard, AbilityRange.Close,
                Helpers.hourPerLevelDuration, "",
                Helpers.CreateContextRankConfig(),
                Helpers.CreateSpellDescriptor(SpellDescriptor.None),
                Helpers.CreateSpellComponent(SpellSchool.Transmutation),
                Helpers.CreateRunActions(enchantItem)
            );
            spell.CanTargetSelf = true;
            spell.NeedEquipWeapons = true;
            spell.Animation = arcaneWeaponSwitchAbility.Animation;
            spell.AvailableMetamagic = Metamagic.Quicken | Metamagic.Heighten | Metamagic.Extend;
            spell.CanTargetFriends = true;

            spell.AddToSpellList(Helpers.wizardSpellList, 3);
            spell.AddToSpellList(Helpers.magusSpellList, 3);
            spell.AddToSpellList(Helpers.paladinSpellList, 3);
            spell.AddToSpellList(Helpers.inquisitorSpellList, 3);
            spell.AddToSpellList(Helpers.clericSpellList, 4);
            Helpers.AddSpellAndScroll(spell, "5739bf41893fddf4f98f8bd6a86b0a52"); // scroll disrupting weapon
        }
    }

    public class SpellConcentrationLogic : BuffLogic, IUnitCommandActHandler, IInitiatorRulebookHandler<RuleCastSpell>, ITargetRulebookHandler<RuleDealDamage>
    {
        [CanBeNull]
        public ContextDurationValue Duration;

        [JsonProperty]
        bool concentrating = true;

        public static SpellConcentrationLogic Create(ContextDurationValue duration = null)
        {
            var s = Helpers.Create<SpellConcentrationLogic>();
            s.Duration = duration;
            return s;
        }

        // Track concentration for a buff on ourselves, or a buff applied to others.
        public new UnitEntityData GetSubscribingUnit() => Buff.Context.MaybeCaster;

        public void HandleUnitCommandDidAct(UnitCommand command)
        {
            if (command.Executor != GetSubscribingUnit()) return;
            // Note: ignoring UnitInteractWithObject because things like opening doors should be a move action.
            if (command.Type == CommandType.Standard && !(command is UnitInteractWithObject))
            {
                RemoveBuff();
            }
        }

        public void OnEventAboutToTrigger(RuleCastSpell evt)
        {
            if (evt.Spell.Blueprint.Type == AbilityType.Spell) RemoveBuff();
        }

        public void OnEventAboutToTrigger(RuleDealDamage evt) { }

        public void OnEventDidTrigger(RuleCastSpell evt) { }

        public void OnEventDidTrigger(RuleDealDamage evt)
        {
            var spell = Buff.Context.SourceAbilityContext?.Ability;
            if (spell != null && !Rulebook.Trigger(new RuleCheckConcentration(evt.Target, spell, evt)).Success)
            {
                RemoveBuff();
            }
        }

        void RemoveBuff()
        {
            if (!concentrating) return;
            concentrating = false;
            var duration = Duration != null ? Duration.Calculate(Buff.Context).Seconds : new TimeSpan();
            var remaining = duration.TotalMilliseconds == 0 ? "now"
                : "in " + BlueprintRoot.Instance.Calendar.GetCompactPeriodString(duration);
            Helpers.GameLog.AddLogEntry(
                $"Not concentrating on {Buff.Name}, ends {remaining}.",
                GameLogStrings.Instance.InitiativeRoll.Color, LogChannel.Combat);
            Buff.RemoveAfterDelay(duration);
        }
    }

    // Adapted from ContextActionEnchantWornItem, adjusted so the bonus won't stack.
    public class ContextActionGreaterMagicWeapon : ContextAction
    {
        public BlueprintItemEnchantment[] Enchantments;

        public ContextDurationValue DurationValue;

        public override string GetCaption() => $"GreaterMagicWeapon enchant (for {DurationValue})";

        public override void RunAction()
        {
            var caster = Context.MaybeCaster;
            if (caster == null) return;

            var unit = Target.Unit;
            if (unit == null) return;

            var weapon = unit.GetThreatHand()?.Weapon;
            if (weapon == null) return;

            foreach (var enhance in Enchantments)
            {
                var fact = weapon.Enchantments.GetFact(enhance);
                if (fact != null && fact.IsTemporary)
                {
                    weapon.RemoveEnchantment(fact);
                }
            }

            // Calculate that weapon's existing enhancement bonus.
            var rule = Context.TriggerRule(new RuleCalculateWeaponStats(unit, weapon));

            // Greater Magic Weapon:
            // +1 enhancement per 4 caster levels.
            var casterLevel = Context.Params.CasterLevel;
            var bonus = Math.Min(Math.Max(1, casterLevel / 4), 5);

            var delta = bonus - rule.Enhancement;
            Log.Write($"{GetType().Name} existing bonus {rule.Enhancement} (partial enhancement {rule.EnhancementTotal}), target bonus: {bonus}");
            if (delta > 0)
            {
                var enchant = Enchantments[delta - 1];
                var rounds = DurationValue.Calculate(Context);
                Log.Write($"Add enchant {enchant} to {weapon} for {rounds} rounds.");
                weapon.AddEnchantment(enchant, Context, rounds);
            }
        }
    }

    // Adapted from WeaponEnhancementBonus, adjusted so it won't penetrate DR higher than magical (in theory).
    public class GreaterMagicWeaponBonusLogic : WeaponEnchantmentLogic, IInitiatorRulebookHandler<RuleCalculateWeaponStats>, IInitiatorRulebookHandler<RuleCalculateAttackBonusWithoutTarget>
    {
        public int EnhancementBonus;

        public void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
        {
            if (evt.Weapon != Owner) return;
            evt.AddBonusDamage(EnhancementBonus);
            evt.Enhancement += 1;
        }

        public void OnEventDidTrigger(RuleCalculateWeaponStats evt) { }

        public void OnEventAboutToTrigger(RuleCalculateAttackBonusWithoutTarget evt)
        {
            if (evt.Weapon != Owner) return;
            evt.AddBonus(EnhancementBonus, Fact);
        }

        public void OnEventDidTrigger(RuleCalculateAttackBonusWithoutTarget evt) { }
    }

    public class WeaponOfAweLogic : WeaponEnchantmentLogic, IInitiatorRulebookHandler<RuleCalculateWeaponStats>, IInitiatorRulebookHandler<RuleAttackWithWeapon>
    {
        public int Bonus = 2;
        public BlueprintBuff Buff;

        public void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
        {
            if (evt.Weapon != Owner) return;
            evt.AddTemporaryModifier(evt.Initiator.Stats.AdditionalDamage.AddModifier(Bonus, this, ModifierDescriptor.Sacred));
        }

        public void OnEventAboutToTrigger(RuleAttackWithWeapon evt) { }

        public void OnEventDidTrigger(RuleCalculateWeaponStats evt) { }

        public void OnEventDidTrigger(RuleAttackWithWeapon evt)
        {
            if (evt.Weapon == Owner && evt.AttackRoll.IsCriticalConfirmed && !evt.AttackRoll.FortificationNegatesCriticalHit)
            {
                evt.Target.Descriptor.AddBuff(Buff, evt.Initiator, 1.Rounds().Seconds);
            }
        }
    }

    public class ContextActionRangedTouchAttack : ContextAction
    {
        public BlueprintItemWeapon Weapon;

        public ActionList OnHit, OnMiss;

        internal static ContextActionRangedTouchAttack Create(GameAction[] onHit, GameAction[] onMiss = null)
        {
            var r = Helpers.Create<ContextActionRangedTouchAttack>();
            r.Weapon = Main.library.Get<BlueprintItemWeapon>("f6ef95b1f7bb52b408a5b345a330ffe8");
            r.OnHit = Helpers.CreateActionList(onHit);
            r.OnMiss = Helpers.CreateActionList(onMiss);
            return r;
        }

        public override string GetCaption() => $"Ranged touch attack";

        public override void RunAction()
        {
            try
            {
                var weapon = Weapon.CreateEntity<ItemEntityWeapon>();
                var context = AbilityContext;
                var attackRoll = context.AttackRoll ?? new RuleAttackRoll(context.MaybeCaster, Target.Unit, weapon, 0);
                attackRoll = context.TriggerRule(attackRoll);
                if (context.ForceAlwaysHit) attackRoll.SetFake(AttackResult.Hit);
                Log.Write($"Ranged touch attack on {Target.Unit}, hit? {attackRoll.IsHit}");
                if (attackRoll.IsHit)
                {
                    OnHit.Run();
                }
                else
                {
                    OnMiss.Run();
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }


    // Like AbilityTargetsAround, but actually uses either the main target point,
    // or the caster as the only target.
    //
    // Intended for delayed spells, so they can display the targeting area, even though the
    // initial spell (the delay buff/item) won't actually target them.
    public class FakeTargetsAround : AbilitySelectTarget, IAbilityAoERadiusProvider
    {
        public Feet AoERadius, SpreadSpeed;

        public TargetType Targets;

        public bool TargetCaster;

        public static FakeTargetsAround Create(Feet radius, TargetType targetType = TargetType.Any,
            Feet spreadSpeed = default(Feet), bool toCaster = false)
        {
            var f = Helpers.Create<FakeTargetsAround>();
            f.AoERadius = radius;
            f.Targets = targetType;
            f.SpreadSpeed = spreadSpeed;
            f.TargetCaster = toCaster;
            return f;
        }

        public override IEnumerable<TargetWrapper> Select(AbilityExecutionContext context, TargetWrapper anchor)
        {
            Log.Write($"FakeTargetsAround: anchor at {anchor}");
            return new TargetWrapper[] { TargetCaster ? context.Caster : anchor };
        }

        public override Feet GetSpreadSpeed() => SpreadSpeed;
        Feet IAbilityAoERadiusProvider.AoERadius => AoERadius;
        TargetType IAbilityAoERadiusProvider.Targets => Targets;
    }
}
