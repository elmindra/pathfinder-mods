// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using HighlightingSystem;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Root;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.Controllers.Units;
using Kingmaker.EntitySystem;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.UI.Log;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using Kingmaker.View.MapObjects;
using Kingmaker.View.MapObjects.InteractionRestrictions;
using Kingmaker.Visual.FogOfWar;
using UnityEngine;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class KnockAndDetectSecrets
    {
        static LibraryScriptableObject library => Main.library;

        internal static void Load()
        {
            Main.SafeLoad(LoadDetectSecretDoors, "Detect Secret Doors");
            Main.SafeLoad(LoadKnock, "Knock");
        }

        static void LoadDetectSecretDoors()
        {
            var spell = Helpers.CreateAbility("DetectSecretDoors", "Detect Secret Doors",
                "You can detect secret doors, compartments, caches, and so forth. Only passages, doors, or openings that have been specifically constructed to escape detection are detected by this spell.\n" +
                "Each round, you can turn to detect secret doors in a new area. This spell requires concentration, and will end prematurely if you cast another spell, take a standard action, or fail a concentration check.",
                "5462b5e9578349ffa59bb469b94ffbb0",
                Helpers.GetIcon("4709274b2080b6444a3c11c6ebbe2404"), // find traps
                AbilityType.Spell, CommandType.Standard, AbilityRange.Personal, "concentration, up to 1 min/level", "");

            var foresightBuff = library.Get<BlueprintBuff>("8c385a7610aa409468f3a6c0f904ac92");

            var buff = Helpers.CreateBuff($"{spell.name}Buff", spell.Name, spell.Description,
                "0f93757378dc41c492b86b807b3a6625", spell.Icon, foresightBuff.FxOnStart, // common divination
                SpellConcentrationLogic.Create(),
                Helpers.Create<DetectSecretDoorsLogic>());

            spell.SetComponents(
                SpellSchool.Divination.CreateSpellComponent(),
                Helpers.CreateContextRankConfig(),
                Helpers.CreateRunActions(buff.CreateApplyBuff(
                    Helpers.CreateContextDuration(rate: DurationRate.Minutes), fromSpell: true)));
            spell.AvailableMetamagic = Metamagic.Quicken | Metamagic.Heighten | Metamagic.Extend;

            spell.AddToSpellList(Helpers.alchemistSpellList, 1);
            spell.AddToSpellList(Helpers.bardSpellList, 1);
            spell.AddToSpellList(Helpers.wizardSpellList, 1);
            Helpers.AddSpellAndScroll(spell, "5e9bd8e141c622a4a8f4e4654d022f40"); // find traps scroll

            Main.ApplyPatch(typeof(MapObjectView_UpdateHighlight_Patch), "Detect Secret Doors: reveal hidden passages");
        }

        static void LoadKnock()
        {
            var spell = Helpers.CreateAbility("KnockSpell", "Knock",
                "Knock opens stuck, barred, or locked doors, as well as those subject to hold portal or arcane lock. When you complete the casting of this spell, make a caster level check against the DC of the lock with a +10 bonus. If successful, knock opens up to two means of closure. This spell opens secret doors, as well as locked or trick-opening boxes or chests. It also loosens welds, shackles, or chains (provided they serve to hold something shut). If used to open an arcane locked door, the spell does not remove the arcane lock but simply suspends its functioning for 10 minutes. In all other cases, the door does not relock itself or become stuck again on its own. Knock does not raise barred gates or similar impediments (such as a portcullis), nor does it affect ropes, vines, and the like. The effect is limited by the area. Each casting can undo as many as two means of preventing access.",
                "1dc0c67a10a54387b2679712969cab27",
                Helpers.GetIcon("26a668c5a8c22354bac67bcd42e09a3f"), // adaptability
                AbilityType.Spell, CommandType.Standard, AbilityRange.Medium, "", "",
                SpellSchool.Transmutation.CreateSpellComponent(),
                FakeTargetsAround.Create(10.Feet()),
                Helpers.CreateRunActions(KnockAction.Create(1, 10.Feet())));
            spell.CanTargetPoint = true;
            spell.AvailableMetamagic = Metamagic.Quicken | Metamagic.Heighten | Metamagic.Reach;

            spell.AddToSpellList(Helpers.inquisitorSpellList, 2);
            spell.AddToSpellList(Helpers.wizardSpellList, 2);
            Helpers.AddSpellAndScroll(spell, "5e9bd8e141c622a4a8f4e4654d022f40"); // find traps scroll

            var massSpell = Helpers.CreateAbility("KnockMass", "Knock, Mass",
                $"This spell functions as knock, but works on multiple means of closure at once (up to 1/caster level).\n{spell.Description}",
                "551f0b78de034fe88b4391293ff20e1b",
                spell.Icon, // adaptability
                AbilityType.Spell, CommandType.Standard, AbilityRange.Close, "", "",
                SpellSchool.Transmutation.CreateSpellComponent(),
                Helpers.CreateContextRankConfig(),
                FakeTargetsAround.Create(30.Feet()),
                Helpers.CreateRunActions(KnockAction.Create(Helpers.CreateContextValueRank(), 30.Feet())));
            massSpell.CanTargetPoint = true;
            massSpell.AvailableMetamagic = Metamagic.Quicken | Metamagic.Heighten | Metamagic.Reach;

            massSpell.AddToSpellList(Helpers.clericSpellList, 6);
            massSpell.AddToSpellList(Helpers.inquisitorSpellList, 6);
            massSpell.AddToSpellList(Helpers.wizardSpellList, 6);
            Helpers.AddSpellAndScroll(massSpell, "5e9bd8e141c622a4a8f4e4654d022f40"); // find traps scroll
        }
    }

    // Adapted from ContextActionDetectSecretDoors, with these changes:
    // - doesn't detect traps
    // - detects doors to hidden areas (e.g. rooms in prologue with the statue puzzle)
    // - logs a notification message.
    public class DetectSecretDoorsLogic : BuffLogic, ITickEachRound
    {
        public override void OnTurnOn() => DetectSecrets();

        public void OnNewRound() => DetectSecrets();

        void DetectSecrets()
        {
            try
            {
                var caster = Context.MaybeCaster;
                foreach (var item in Game.Instance.State.LoadedAreaState.AllEntityData.OfType<StaticEntityData>())
                {
                    var mapObj = item.View as MapObjectView;
                    if (mapObj == null) continue;
                    var distance = caster.DistanceTo(mapObj.transform.position);
                    if (distance > 11.7f) continue;

                    Log.Append($"  map item: {mapObj.name}, type: {mapObj.GetType()}, distance: {distance}");
                    Log.Append($"  components: {mapObj.GetComponentsInChildren(typeof(Component)).StringJoin(c => c.GetType().Name)}");
                    var trap = mapObj as TrapObjectView;
                    if (trap != null) continue;

                    if (!item.IsPerceptionCheckPassed)
                    {
                        Helpers.GameLog.AddLogEntry(
                            "Detect Secret Doors: found hidden item.",
                            GameLogStrings.Instance.SkillCheckSuccess.Color, LogChannel.Combat);
                        item.IsPerceptionCheckPassed = true;
                    }

                    // Detect hidden areas: if we have a fog of war blocker, but it's not interactable (highlight on hover)
                    // then we (most likely) have found the door to a hidden chamber.
                    var fogOfWarBlocker = mapObj.GetComponentInChildren<FogOfWarBlocker>();
                    if (fogOfWarBlocker != null && !mapObj.Highlighted)
                    {
                        var highlightOnHover = mapObj.Data.IsRevealed && mapObj.Interactions.Any(i => (i.Type == InteractionType.Approach || i.Type == InteractionType.Direct) && i.Enabled && !i.ShowOvertip);
                        if (!highlightOnHover)
                        {
                            var c = mapObj.GetComponent<DetectedSecretComponent>();
                            if (c == null)
                            {
                                Helpers.GameLog.AddLogEntry(
                                    "Detect Secret Doors: found hidden door.",
                                    GameLogStrings.Instance.SkillCheckSuccess.Color, LogChannel.Combat);

                                // Add a component to reveal this door (used by MapObjectView_UpdateHighlight_Patch, below).
                                mapObj.EnsureComponent<DetectedSecretComponent>();
                                mapObj.UpdateHighlight();
                            }
                        }
                    }

                }
                Log.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    // Marker component used to add state indicating secret door.
    class DetectedSecretComponent : MonoBehaviour { }

    [Harmony12.HarmonyPatch(typeof(MapObjectView), "UpdateHighlight")]
    static class MapObjectView_UpdateHighlight_Patch
    {
        static void Postfix(MapObjectView __instance, Highlighter ___m_Highlighter)
        {
            var self = __instance;
            if (___m_Highlighter == null || self.GetComponent<DetectedSecretComponent>() == null) return;
            try
            {
                // Don't highlight if the secret door is open.
                if (self.Interactions.OfType<StandardDoor>().Any(d => d.GetState())) return;
                // Secret door, not opened: highlight it.
                ___m_Highlighter.ConstantOn(BlueprintRoot.Instance.UIRoot.PerceptedLootColor, 0);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class KnockAction : ContextAction
    {
        public ContextValue Amount;
        public Feet Radius;

        public static KnockAction Create(ContextValue value, Feet radius)
        {
            var k = Helpers.Create<KnockAction>();
            k.Amount = value;
            k.Radius = radius;
            return k;
        }

        public override string GetCaption() => $"Knock {Amount} locks with {Radius}ft radius";

        public override void RunAction()
        {
            try
            {
                var radius = Radius.Meters;
                var caster = Context.MaybeCaster;
                var target = Context.MainTarget.Point;
                Log.Append($"Knock::RunAction() caster {caster.CharacterName}, target: {target}, radius: {radius}m");

                var locks = new SortedDictionary<MapObjectView, DisableDeviceRestriction[]>(new DistanceComparer(target));
                foreach (var item in Game.Instance.State.LoadedAreaState.AllEntityData.OfType<StaticEntityData>())
                {
                    var mapObj = item.View as MapObjectView;
                    if (mapObj == null) continue;

                    var distance = GeometryUtils.MechanicsDistance(target, mapObj.transform.position);
                    if (distance > radius) continue;

                    Log.Append($"  map item: {mapObj.name}, type: {mapObj.GetType()}, distance: {distance}");
                    Log.Append($"  component: {mapObj.GetComponentsInChildren(typeof(Component)).StringJoin(c => c.GetType().Name)}");

                    var trap = mapObj as TrapObjectView;
                    if (trap != null) continue;
                    //Log.Append($"  trap active? {trap?.TrapActive} TrappedObject {trap?.TrappedObject}");
                    //if (trap != null && trap.TrapActive) continue;

                    var lockInfos = mapObj.GetComponents<DisableDeviceRestriction>();

                    Log.Append($"  lock count {lockInfos.Length}");
                    if (lockInfos.Length > 0) locks.Add(mapObj, lockInfos);
                }

                int amount = Amount.Calculate(Context);
                foreach (var lockInfos in locks.Values.Take(amount))
                {
                    foreach (var lockInfo in lockInfos)
                    {
                        if (TryOpenLock(lockInfo)) lockInfo.ShowSuccessBark(caster);
                    }
                }
                Log.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        bool TryOpenLock(DisableDeviceRestriction lockInfo)
        {
            var data = (DisableDeviceRestriction.DisableDeviceRestrictionData)lockInfo.Data;
            if (data.Unlocked) return true;

            var user = Context.MaybeCaster;
            int dc = (data.DCOverride != 0) ? data.DCOverride : lockInfo.DC;
            var statsAdjustmentsType = Game.Instance.Player.Difficulty.StatsAdjustmentsType;
            dc += BlueprintRoot.Instance.DifficultyList.GetAdjustmentPreset(statsAdjustmentsType).SkillCheckDCBonus;

            // Roll caster level + 10 against DC of lock
            var casterLevel = Context.Params.CasterLevel;
            var roll = RulebookEvent.Dice.D20;
            var result = casterLevel + 10 + roll;
            Log.Append($"Knock rolled {result} against DC {dc}, lock {lockInfo.name}");
            var success = data.Unlocked = result > dc;

            Helpers.GameLog.AddLogEntry(
                success ? "Knock successfully opened lock." : "Knock failed to open lock.",
                GameLogStrings.Instance.SkillCheckSuccess.Color, LogChannel.Combat,
                $"Knock result: {result} (roll {roll} + caster level {casterLevel} + bonus 10).\n" +
                $"Difficulty Class (DC): {dc}.\n" +
                $"Result: {(success ? "success" : "failure")}");

            EventBus.RaiseEvent((IPickLockHandler h) =>
            {
                if (success)
                {
                    h.HandlePickLockSuccess(user, lockInfo.MapObject);
                    return;
                }
                h.HandlePickLockFail(user, lockInfo.MapObject);
            });
            return success;
        }

        class DistanceComparer : IComparer<MapObjectView>
        {
            Vector3 center;
            public DistanceComparer(Vector3 center)
            {
                this.center = center;
            }

            public int Compare(MapObjectView x, MapObjectView y)
            {
                var xd = GeometryUtils.MechanicsDistance(center, x.transform.position);
                var yd = GeometryUtils.MechanicsDistance(center, y.transform.position);
                if (xd < yd) return -1;
                if (xd > yd) return 1;
                return 0;
            }
        }
    }
}
