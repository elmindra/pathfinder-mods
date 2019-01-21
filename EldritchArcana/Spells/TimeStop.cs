// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System.Collections.Generic;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.Utility;
using Newtonsoft.Json;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class TimeStop
    {
        internal static BlueprintAbility spell;
        static LibraryScriptableObject library => Main.library;

        internal static void Load()
        {
            // This is a simplified version of Time Stop:
            // - Freezes all other units for 1d4+1 round
            // - Stops area effects from ticking
            //
            // I tried a slow motion version (adjust game time rate), but it takes a lot of patches to
            // keep the player's animations/cooldowns moving at normal speed. Walk/run animations in particular
            // are complicated, and I didn't find a clean way to patch that.
            //
            // I also had a version that gave the player 1d4+1 instant cast spells/abilities and a large
            // speeed boost. It works pretty nicely with "pause". But it's neat to see everyone else frozen
            // and "apparent time" ticking for the caster.
            //
            // TODO: this doesn't stop particle effects (or spells-in-flight, etc). Also it
            // doesn't change the end time for area spells ()
            var expeditiousRetreat = library.Get<BlueprintAbility>("4f8181e7a7f1d904fbaea64220e83379");
            var expeditiousRetreatBuff = library.Get<BlueprintBuff>("9ea4ec3dc30cd7940a372a4d699032e7");
            var dispelMagicGreater = library.Get<BlueprintAbility>("f0f761b808dc4b149b08eaf44b99f633");

            var freezeBuff = Helpers.CreateBuff("TimeStopFreezeBuff", "Frozen in Time",
                "Time stop is active for another unit, freezing apparent time for them.",
                "fb33065de053485394dd9bfe99a86337",
                dispelMagicGreater.Icon, null,
                UnitCondition.CantAct.CreateAddCondition(),
                UnitCondition.CantMove.CreateAddCondition(),
                UnitCondition.Paralyzed.CreateAddCondition(),
                Helpers.Create<Untargetable>(),
                Helpers.Create<EraseFromTimeEffect>());

            var buff = Helpers.CreateBuff("TimeStopBuff", "Time Stop",
                "This spell seems to make time cease to flow for everyone but you. In fact, you speed up so greatly that all other creatures seem frozen, though they are actually still moving at their normal speeds. You are free to act for 1d4+1 rounds of apparent time. Normal and magical fire, cold, gas, and the like can still harm you. While the time stop is in effect, other creatures are invulnerable to your attacks and spells; you cannot target such creatures with any attack or spell. A spell that affects an area and has a duration longer than the remaining duration of the time stop have their normal effects on other creatures once the time stop ends. Most spellcasters use the additional time to improve their defenses, summon allies, or flee from combat.\n" +
                "You cannot move or harm items held, carried, or worn by a creature stuck in normal time, but you can affect any item that is not in another creature’s possession.\n" +
                "You are undetectable while time stop lasts. You cannot enter an area protected by an antimagic field while under the effect of time stop.",
                "a5adb4794e364485bca802e7ecfb694a",
                freezeBuff.Icon, expeditiousRetreatBuff.FxOnStart,
                UnitCondition.ImmuneToAttackOfOpportunity.CreateAddCondition(),
                UnitCondition.CanNotAttack.CreateAddCondition(),
                Helpers.Create<TimeStopEffect>(t => t.FreezeTime = freezeBuff));

            var spell = TimeStop.spell = Helpers.CreateAbility("TimeStop", buff.Name, buff.Description,
                "661c8d61f47d4c5c93e34f7d8692e81b", buff.Icon, AbilityType.Spell,
                CommandType.Standard, AbilityRange.Personal, "1d4+1 rounds (apparent time)", "",
                expeditiousRetreat.GetComponent<AbilitySpawnFx>(),
                Helpers.CreateSpellComponent(SpellSchool.Transmutation),
                Helpers.CreateRunActions(Helpers.CreateApplyBuff(buff,
                    Helpers.CreateContextDuration(1, diceType: DiceType.D4, diceCount: 1),
                    fromSpell: true, toCaster: true, dispellable: false)));
            spell.AvailableMetamagic = Metamagic.Extend | Metamagic.Quicken;
            spell.CanTargetSelf = true;

            spell.AddToSpellList(Helpers.wizardSpellList, 9);
            Helpers.AddSpellAndScroll(spell, "b3365694d86108842b58609d90b6d05c"); // scroll greater dispel
        }
    }

    public class TimeStopEffect : BuffLogic,
        // Can't target others with these effects
        IInitiatorRulebookHandler<RuleSpellTargetCheck>,
        IInitiatorRulebookHandler<RuleSavingThrow>,
        IInitiatorRulebookHandler<RuleDealDamage>,
        IInitiatorRulebookHandler<RuleDrainEnergy>,
        IInitiatorRulebookHandler<RuleDealStatDamage>,
        IAreaEffectHandler,
        IUnitHandler
    {
        public BlueprintBuff FreezeTime;

        [JsonProperty]
        List<Buff> frozenBuffs = new List<Buff>();
        List<UnitDescriptor> immuneParalysisUnits = new List<UnitDescriptor>();

        public override void OnTurnOn()
        {
            Game.Instance.State.Units.ForEach(MaybeFreezeUnit);
            Game.Instance.State.AreaEffects.ForEach(FreezeAreaEffect);
        }

        void FreezeAreaEffect(AreaEffectEntityData areaEffect)
        {
            // TODO: unfortunately area effect duration is read-only. If we want to keep them going,
            // we'll need to modify the result of AreaEffectEntityData.IsEnded, and track which areas
            // were affected (probably by adding a component, similar to "detect secret doors").
            // In the meantime, we can stop the "next round" ticking.
            //
            // TODO: freeze particle system effects.
            var nextRound = (float)getTimeToNextRound(areaEffect);
            setTimeToNextRound(areaEffect, nextRound + (float)Buff.TimeLeft.TotalSeconds);
        }

        void MaybeFreezeUnit(UnitEntityData unit)
        {
            var descriptor = unit.Descriptor;
            if (descriptor == Owner) return;

            // Note: skip units that aren't in party/enemies so we don't break scripts that use them.
            // (This isn't quite ideal, but trying to play it safe.)
            if (!unit.IsPlayersEnemy && !unit.IsPlayerFaction)
            {
                Log.Write($"Time stop: skip {unit.CharacterName}, they aren't a party member or an enemy");
                return;
            }
            if (descriptor.State.HasConditionImmunity(UnitCondition.Paralyzed))
            {
                immuneParalysisUnits.Add(descriptor);
                descriptor.State.RemoveConditionImmunity(UnitCondition.Paralyzed);
            }

            frozenBuffs.Add(descriptor.AddBuff(FreezeTime, descriptor.Unit, Buff.TimeLeft));
        }

        static readonly FastGetter getTimeToNextRound = Helpers.CreateFieldGetter<AreaEffectEntityData>("m_TimeToNextRound");
        static readonly FastSetter setTimeToNextRound = Helpers.CreateFieldSetter<AreaEffectEntityData>("m_TimeToNextRound");

        public override void OnTurnOff()
        {
            immuneParalysisUnits.ForEach(u => u.State.AddConditionImmunity(UnitCondition.Paralyzed));
            immuneParalysisUnits.Clear();
            frozenBuffs.ForEach(b => b?.Remove());
            frozenBuffs.Clear();
        }

        public void OnEventAboutToTrigger(RuleSavingThrow evt)
        {
            if (evt.Initiator != Owner.Unit) return;
            evt.AutoPass = true;
        }
        public void OnEventDidTrigger(RuleSavingThrow evt) { }

        public void OnEventAboutToTrigger(RuleSpellTargetCheck evt)
        {
            if (evt.Initiator != Owner.Unit) return;
            evt.IsImmune = evt.Target != Owner.Unit;
        }

        public void OnEventDidTrigger(RuleSpellTargetCheck evt) { }

        public void OnEventAboutToTrigger(RuleDealDamage evt)
        {
            if (evt.Initiator != Owner.Unit) return;
            foreach (var dmg in evt.DamageBundle)
            {
                dmg.Immune = true;
            }
        }
        public void OnEventDidTrigger(RuleDealDamage evt) { }

        public void OnEventAboutToTrigger(RuleDealStatDamage evt)
        {
            if (evt.Initiator != Owner.Unit) return;
            evt.Immune = true;
        }
        public void OnEventDidTrigger(RuleDealStatDamage evt) { }

        public void OnEventAboutToTrigger(RuleDrainEnergy evt)
        {
            if (evt.Initiator != Owner.Unit) return;
            evt.TargetIsImmune = true;
        }
        public void OnEventDidTrigger(RuleDrainEnergy evt) { }

        public void OnEventAboutToTrigger(RuleCastSpell evt) { }

        public void HandleAreaEffectSpawned(AreaEffectEntityData areaEffect) => FreezeAreaEffect(areaEffect);

        public void HandleAreaEffectDestroyed(AreaEffectEntityData entityData) { }

        public void HandleUnitSpawned(UnitEntityData unit) => MaybeFreezeUnit(unit);

        public void HandleUnitDestroyed(UnitEntityData entityData) { }

        public void HandleUnitDeath(UnitEntityData entityData) { }
    }
}
