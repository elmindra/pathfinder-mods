// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.Root;
using Kingmaker.Controllers;
using Kingmaker.Controllers.Combat;
using Kingmaker.Controllers.Units;
using Kingmaker.Designers;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Enums.Damage;
using Kingmaker.Items;
using Kingmaker.Localization;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.AreaEffects;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Buffs.Components;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.UnitLogic.Mechanics.Conditions;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.Utility;
using Kingmaker.View;
using Kingmaker.View.Animation;
using Kingmaker.View.MapObjects;
using Kingmaker.Visual.Animation;
using Kingmaker.Visual.Animation.Actions;
using Kingmaker.Visual.Animation.Kingmaker;
using Kingmaker.Visual.Animation.Kingmaker.Actions;
using Kingmaker.Visual.Particles;
using Newtonsoft.Json;
using UnityEngine;
using static Kingmaker.UI.GenericSlot.EquipSlotBase;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class ExperimentalSpells
    {
        internal static BlueprintAbility emergencyForceSphere, delayedBlastFireball;

        static LibraryScriptableObject library => Main.library;

        internal static void LoadSpritualWeapon()
        {
            // TODO: This needs real visuals. A summoned unit would work better, but needs
            // research in how to make a BlueprintUnit, and then make it invisible, while still allowing
            // the weapon to be visible.

            // TODO: Create an ability button so you can retarget the attack?

            var spell = Helpers.CreateAbility("SpiritualWeapon", "Spiritual Weapon",
                "A weapon made of force appears and attacks foes at a distance, as you direct it, dealing 1d8 force damage per hit, +1 point per three caster levels (maximum +5 at 15th level). " +
                "The weapon takes the shape of a weapon favored by your deity or a weapon with some spiritual significance or symbolism to you (see below) and has the same threat range and critical multipliers as a real weapon of its form. " +
                "It strikes the opponent you designate, starting with one attack in the round the spell is cast and continuing each round thereafter on your turn. " +
                "It uses your base attack bonus (possibly allowing it multiple attacks per round in subsequent rounds) plus your Wisdom modifier (or primary casting ability, if cast as a spell) as its attack bonus. " +
                "It strikes as a spell, not as a weapon, so for example, it can damage creatures that have damage reduction. " +
                "As a force effect, it can strike incorporeal creatures without the reduction in damage associated with incorporeality. " +
                "The weapon always strikes from your direction. It does not get a flanking bonus or help a combatant get one. Your feats or combat actions do not affect the weapon. " +
                //"If the weapon goes beyond the spell range, if it goes out of your sight, or if you are not directing it, the weapon returns to you and hovers. " +
                "\n" +
                //"Each round after the first, you can use a move action to redirect the weapon to a new target. If you do not, the weapon continues to attack the previous round’s target. " +
                "Each round after the first, the weapon continues to attack the previous round’s target. If the target dies, it will attack the next nearest enemy, if any. " +
                "On any round that the weapon switches targets, it gets one attack. Subsequent rounds of attacking allow the weapon to make multiple attacks if your base attack bonus would allow it to." +
                //"Even if the spiritual weapon is a ranged weapon, use the spell’s range, not the weapon’s normal range increment, and switching targets still is a move action." +
                "\n" +
                "A spiritual weapon cannot be attacked or harmed by physical attacks, but dispel magic, disintegrate, a sphere of annihilation, or a rod of cancellation affects it." +
                //"A spiritual weapon‘s AC against touch attacks is 12 (10 + size bonus for Tiny object)." +
                "\n" +
                "If an attacked creature has Spell Resistance, you make a caster level check (1d20 + caster level) against that Spell Resistance the first time the spiritual weapon strikes it." +
                "If the weapon is successfully resisted, the spell is dispelled. If not, the weapon has its normal full effect on that creature for the duration of the spell." +
                "\n" +
                "The weapon that you get is often a force replica of your deity’s own personal weapon. A cleric without a deity gets a weapon based on their alignment." +
                "A neutral cleric without a deity can create a spiritual weapon of any alignment, provided they are acting at least generally in accord with that alignment at the time." +
                "The weapons associated with each alignment are as follows: chaos (battleaxe), evil (light flail), good (warhammer), law (longsword).",
                "3e68ea1392d8451681656639c54e2155",
                Helpers.GetIcon("46c96cc3a3ef35243915ff3452dfacf5"), // disrupting weapon
                AbilityType.Spell,
                CommandType.Standard,
                AbilityRange.Medium,
                Helpers.roundsPerLevelDuration,
                Helpers.savingThrowNone);
            spell.CanTargetEnemies = true;
            spell.CanTargetPoint = true;
            spell.EffectOnEnemy = AbilityEffectOnUnit.Harmful;
            spell.SpellResistance = true;

            var magicMissle = library.Get<BlueprintAbility>("4ac47ddb9fa1eaf43a1b6809980cfbd2");
            var projectiles = magicMissle.GetComponent<AbilityDeliverProjectile>().Projectiles;

            var buff = Helpers.CreateBuff($"{spell.name}Buff", spell.Name, spell.Description,
                "1ed5a762bab141fcbad9d2aa2944f845", spell.Icon,
                null,
                SpiritualWeaponAttackAction.Create(projectiles));
            buff.Stacking = StackingType.Stack;

            spell.SetComponents(
                SpellSchool.Evocation.CreateSpellComponent(),
                SpellDescriptor.Force.CreateSpellDescriptor(),
                Helpers.CreateContextRankConfig(),
                Helpers.CreateRunActions(
                    Helpers.CreateApplyBuff(buff, Helpers.CreateContextDuration(),
                        fromSpell: true, toCaster: true)));

            // TODO: this spell is disabled until the visuals can be improved
            //spell.AddToSpellList(Helpers.clericSpellList, 2);
            //spell.AddToSpellList(Helpers.inquisitorSpellList, 2);
            //Helpers.AddSpellAndScroll(spell, "5739bf41893fddf4f98f8bd6a86b0a52"); // scroll disrupting weapon
        }

        static void LoadDelayedBlastFireball()
        {
            var fireball = library.Get<BlueprintAbility>("2d81362af43aeac4387a3d4fced489c3");
            var spell = Helpers.CreateAbility("DelayedBlastFireball", "Delayed Blast Fireball",
                "This spell functions like fireball, except that it is more powerful and can detonate up to 5 rounds after the spell is cast. The burst of flame deals 1d6 points of fire damage per caster level (maximum 20d6). The glowing bead created by delayed blast fireball can detonate immediately if you desire, or you can choose to delay the burst for as many as 5 rounds. You select the amount of delay upon completing the spell, and that time cannot change once it has been set unless someone touches the bead. If you choose a delay, the glowing bead sits at its destination until it detonates. A creature can pick up and hurl the bead as a thrown weapon (range increment 10 feet). If a creature handles and moves the bead within 1 round of its detonation, there is a 25% chance that the bead detonates while being handled.",
                "dfe891561c4d48ed8235268b0e7692e7",
                fireball.Icon, AbilityType.Spell, CommandType.Standard, fireball.Range,
                "5 rounds or less; see text", fireball.LocalizedSavingThrow);
            spell.SpellResistance = true;

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
            spell0.SetComponents(spell0.ComponentsArray.Where(c => !(c is SpellListComponent)).Select(c =>
            {
                var config = c as ContextRankConfig;
                if (config != null)
                {
                    c = config = UnityEngine.Object.Instantiate(config);
                    Helpers.SetField(c, "m_UseMax", false);
                    Helpers.SetField(c, "m_Max", 20);
                }
                return c;
            }));
            spell0.SpellResistance = true;

            var fireballItem = library.CopyAndAdd<BlueprintItem>("6922eeb3b29e3fb488e955311bfc5cdc", // ruby
                "DelayedBlastFireballItem", "b2c945d1955943bbaaad581682cde624");
            Helpers.SetField(fireballItem, "m_DisplayNameText", spell.GetName());
            Helpers.SetField(fireballItem, "m_DescriptionText", spell.GetDescription());
            Helpers.SetField(fireballItem, "m_Cost", 0);
            var scorchingRay = library.Get<BlueprintAbility>("cdb106d53c65bbc4086183d54c3b97c7");
            var deliverProjectile = UnityEngine.Object.Instantiate(scorchingRay.GetComponent<AbilityDeliverProjectile>());
            deliverProjectile.NeedAttackRoll = false;
            deliverProjectile.Projectiles = new BlueprintProjectile[] { deliverProjectile.Projectiles[0] };

            // Note: if the item fireball thing proves too complex, a much simpler way would be to use
            // SetBuffOnsetDelay, and have a buff cast the spell.
            var variants = new List<BlueprintAbility> { spell0 };
            for (int delay = 1; delay <= 5; delay++)
            {
                var delaySpell = library.CopyAndAdd(spell0, $"{spell.name}Delay{delay}", delayIds[delay]);
                delaySpell.SetName($"{spell.Name} ({delay} rounds)");
                delaySpell.SetComponents(
                    fireball.GetComponent<SpellComponent>(),
                    deliverProjectile,
                    Helpers.CreateRunActions(Helpers.Create<SpawnItemWithDelayedSpell>(c =>
                    {
                        c.Item = fireballItem;
                        c.Spell = spell0;
                        c.SpellDelay = Helpers.CreateContextDuration(delay);
                    })));
                variants.Add(delaySpell);
            }

            spell.SetComponents(
                Helpers.CreateSpellComponent(fireball.School),
                Helpers.CreateSpellDescriptor(fireball.SpellDescriptor),
                spell.CreateAbilityVariants(variants));

            // spell.AddToSpellList(Helpers.wizardSpellList, 7);
            // Helpers.AddSpell(spell);
            delayedBlastFireball = spell;
        }

        internal static void LoadTimeStop()
        {
            // Time stop, things that are working:
            // - the core idea of slowing the game clock works & looks cool.
            // - buff durations, combat cooldowns, and casting works.
            // - spells are set to deliver instantly (otherwise they'd get frozen until later).
            //
            // Open issues:
            // - I haven't figured out the walking animation; it doesn't work, although actual movement
            //   speed seems fine.
            // - AOE effects (e.g. incendiary clouds) may not be working correctly.
            //   You should be able to place these, but targets are immune during time stop.
            // - Delayed blast fireball: should tick rounds during Time Stop.
            var dispelMagicGreater = library.Get<BlueprintAbility>("f0f761b808dc4b149b08eaf44b99f633");
            var expeditiousRetreat = library.Get<BlueprintAbility>("4f8181e7a7f1d904fbaea64220e83379");
            var haste = library.Get<BlueprintAbility>("486eaff58293f6441a5c2759c4872f98");

            var buff = Helpers.CreateBuff("TimeStopBuff", "Time Stop",
                "This spell seems to make time cease to flow for everyone but you. In fact, you speed up so greatly that all other creatures seem frozen, though they are actually still moving at their normal speeds. You are free to act for 1d4+1 rounds of apparent time. Normal and magical fire, cold, gas, and the like can still harm you. While the time stop is in effect, other creatures are invulnerable to your attacks and spells; you cannot target such creatures with any attack or spell. A spell that affects an area and has a duration longer than the remaining duration of the time stop have their normal effects on other creatures once the time stop ends. Most spellcasters use the additional time to improve their defenses, summon allies, or flee from combat.\n" +
                "You cannot move or harm items held, carried, or worn by a creature stuck in normal time, but you can affect any item that is not in another creature’s possession.\n" +
                "You are undetectable while time stop lasts. You cannot enter an area protected by an antimagic field while under the effect of time stop.",
                "a5adb4794e364485bca802e7ecfb694a",
                dispelMagicGreater.Icon, null,
                Helpers.Create<TimeStopExperiment>());

            TimeStopExperiment.timeStopBuff = buff;

            var spell = Helpers.CreateAbility("TimeStop", buff.Name, buff.Description,
                "661c8d61f47d4c5c93e34f7d8692e81b", buff.Icon, AbilityType.Spell,
                CommandType.Standard, AbilityRange.Personal, "1d4+1 rounds (apparent time)", "None",
                expeditiousRetreat.GetComponent<AbilitySpawnFx>(),
                Helpers.CreateSpellComponent(SpellSchool.Transmutation),
                Helpers.CreateRunActions(Helpers.CreateApplyBuff(buff,
                    Helpers.CreateContextDuration(1, diceType: DiceType.D4, diceCount: 1),
                    fromSpell: true,
                    toCaster: true,
                    dispellable: true)));
            spell.AvailableMetamagic = Metamagic.Extend | Metamagic.Quicken;
            spell.CanTargetSelf = true;

            // spell.AddToSpellList(Helpers.wizardSpellList, 9);
            // Helpers.AddSpell(spell);
        }

        static void LoadEmergencyForceSphere()
        {
            // Emergency Force Sphere - open issues:
            // - how to get a continuous/looping sphere animation (answer: BuffParticleEffectPlay)
            // - how to get an immediate action (free action requires it to be player's turn)
            // - how to get the sphere closer to PnP rules. Implementing it as a buff
            //   for the player kind of works, but it leaves some rough edges (e.g.
            //   does not block all effects it should; doesn't make the sphere as
            //   easy to attack as it should be).
            // - implementing it as a summon would work better, if that's possible, but still
            //   need to make the caster untargetable.
            //
            // The current design for "Emergency Force Sphere" is to simulate the effect,
            // preventing spell effects from entering/leaving the sphere and letting
            // melee attacks hit, by adding temp HP and damage reduction 20/- to
            // match the Sphere's hardness.
            //
            // There are a few problems with this, though. First: weapons are targetting the player,
            // which leads to some incorrect calculations:
            // - Hits have to hit the player's AC/displacement, instead of the easier-to-hit sphere.
            // - Hits will hit mirror images because that check happens first.
            // - The final attack that break the sphere will affect the player, but it shouldn't.
            //
            // Can this be solved with RuleAttackWithWeapon.ReplaceTarget?
            //
            // The other issues are related to line-of-effect, and whether spells/abilities can
            // bypass the sphere. Almost none should (Dimension Door type spells being an exception)
            // Current problems:
            // - Spells that don't have SR bypass the rules for spell immunity (this is a game bug:
            //   spell immunities in Pathfinder don't depend on SR, but the game implements them
            //   using the RuleSpellResistanceCheck). For example, AOE zones.
            // - The player may be effected by things that shouldn't have been, such as conditions.
            // - The sphere is incorrectly immune to spell damage (because spell immunity is
            //   all-or-nothing, it isn't easy to allow the damage part through but prevent all
            //   other effects).
            // - The player is able to cast non-SR spells (like Grease) through the sphere.
            //
            // To fix the spell issues:
            // - Fixing targeting in/out of the sphere: presumably we need to prevent ability
            //   targeting. Unfortunately that seems tricky. OnTryToApplyAbilityEffect might
            //   be a way to redirect the target, though:
            //   - Player targetting out: hits sphere.
            //   - Other caster targetting in: hits sphere.
            // - for persistent AOEs (e.g. BlueprintAbilityAreaEffect) either patch
            //   AreaEffectEntityData.UnitsInside, or try a combo of Damage/Buff/Condition
            //  immunities.
            //
            // Overall the "retargetting" approach seems cleanest. Need a unit to represent the sphere.
            // It would probably need to overlap the player, though. Not sure how to make an invisible
            // unit (and/or make the "sphere" casting animation persistent).
            //
            var protectionFromSonic = library.Get<BlueprintAbility>("0cee375b4e5265a46a13fc269beb8763");
            var protectionSonicBuff = library.Get<BlueprintBuff>("e40277752759edb49b557ce8399596bc");
            Log.Write(protectionFromSonic);
            Log.Write(protectionSonicBuff);

            // TODO: update to use newer helpers method, e.g. buff/ability creation
            var buff = Helpers.Create<BlueprintBuff>();
            buff.Stacking = StackingType.Replace;
            buff.Frequency = DurationRate.Rounds;
            var buffAddDismiss = Helpers.CreateAddFacts();
            buff.SetComponents(
                Helpers.Create<AddCondition>(c => c.Condition = UnitCondition.CanNotAttack),
                Helpers.Create<AddCondition>(c => c.Condition = UnitCondition.MovementBan),
                Helpers.Create<AddCondition>(c => c.Condition = UnitCondition.ImmuneToCombatManeuvers),
                Helpers.Create<TemporaryHitPointsPerCasterLevel>(t =>
                {
                    t.RemoveWhenHitPointsEnd = true;
                    t.Descriptor = ModifierDescriptor.UntypedStackable;
                    t.HitPointsPerLevel = 10;
                }),
                Helpers.Create<AddDamageResistancePhysical>(t =>
                {
                    t.Value = 20;
                }),
                Helpers.Create<ForceSphereEffect>(),
                buffAddDismiss);
            // TODO: ITickEachRound to trigger the animation again?
            // It's not the cleanest solution but it might work.
            buff.FxOnStart = protectionFromSonic.GetComponent<AbilitySpawnFx>().PrefabLink;
            var fx = buff.FxOnStart.Load();
            Log.Write(fx.GetType().ToString());

            buff.name = "EmergencyForceSphereBuff";
            var description = "As wall of force, except you create a hemispherical dome of force with hardness 20 and a number of hit points equal to 10 per caster level. The bottom edge of the dome forms a relatively watertight space if you are standing on a reasonably flat surface. The dome shape means that falling debris (such as rocks from a collapsing ceiling) tend to tumble to the side and pile up around the base of the dome. If you make a DC 20 Craft (stonemasonry), Knowledge (engineering), or Profession (architect or engineer) check, the debris is stable enough that it retains its dome-like configuration when the spell ends, otherwise it collapses.\nNormally this spell is used to buy time for dealing with avalanches, floods, and rock-slides, though it is also handy in dealing with ambushes.";
            // TODO: figure out why buff icon did not show up immediately
            buff.SetNameDescriptionIcon("Emergency Force Sphere",
                description,
                protectionFromSonic.Icon);
            library.AddAsset(buff, "2d61e248f56c47979c009b00451c45ac");
            Log.Write(buff);

            var spell = Helpers.Create<BlueprintAbility>();
            spell.ResourceAssetIds = Array.Empty<string>();
            // TODO: this should be an immediate action.
            spell.ActionType = CommandType.Free;
            spell.AvailableMetamagic = Metamagic.Extend | Metamagic.Heighten;
            spell.Range = AbilityRange.Personal;
            spell.CanTargetSelf = true;
            // TODO: Figure out how to get this text, maybe copy from another spell?
            spell.LocalizedDuration = Helpers.CreateString("Spell.Duration.RoundsPerLevel", "1 round/level");
            // TODO: Figure out how to hide this for personal spells.
            spell.LocalizedSavingThrow = Helpers.CreateString("Spell.SavingThrow.None", "None");
            spell.name = "EmergencyForceSphere";
            spell.SetNameDescriptionIcon("Emergency Force Sphere",
                description,
                protectionFromSonic.Icon);
            library.AddAsset(spell, "60908c0563da4c1fbec017980a34e5c5");
            var components = new List<BlueprintComponent>();
            // TODO: this graphic is perfect, figure out how to keep it showing.
            components.Add(protectionFromSonic.GetComponent<AbilitySpawnFx>());
            components.Add(Helpers.Create<SpellComponent>(s => s.School = SpellSchool.Evocation));
            components.Add(Helpers.Create<SpellDescriptorComponent>(s => s.Descriptor = SpellDescriptor.Force));
            components.Add(Helpers.Create<AbilityEffectRunAction>(a => a.Actions = new ActionList()
            {
                Actions = new GameAction[] {
                    new ContextActionApplyBuff() {
                        Buff = buff,
                            IsFromSpell = true,
                            ToCaster = true,
                            IsNotDispelable = true,
                            AsChild = false,
                            DurationValue = new ContextDurationValue() {
                                DiceCountValue = 0,
                                    Rate = DurationRate.Rounds,
                                    BonusValue = new ContextValue() {
                                        ValueType = ContextValueType.Rank,
                                    }
                            }
                    }
                }
            }));
            spell.SetComponents(components);

            // spell.AddToSpellList(Helpers.wizardSpellList, 4);
            // Helpers.AddSpell(spell);

            var dismiss = Helpers.Create<BlueprintAbility>();
            dismiss.Type = AbilityType.Special;
            dismiss.ResourceAssetIds = Array.Empty<string>();
            dismiss.ActionType = CommandType.Standard;
            dismiss.Range = AbilityRange.Personal;
            dismiss.CanTargetSelf = true;
            dismiss.name = "DismissEmergencyForceSphere";
            dismiss.SetNameDescriptionIcon("Dismiss Spell (Emergency Force Sphere)",
                "Dismissible\nYou can dismiss this spell at will. Dismissing a spell is a standard action that does not provoke attacks of opportunity.",
                spell.Icon);
            dismiss.SetComponents(
                Helpers.Create<AbilityEffectRunAction>(a => a.Actions = new ActionList()
                {
                    Actions = new GameAction[] { new ContextActionRemoveBuff() { Buff = buff, ToCaster = true } }
                }));
            buffAddDismiss.Facts = new BlueprintUnitFact[] { dismiss };
            library.AddAsset(dismiss, "0064db5783a34064a11d673f1fba21b1");

            emergencyForceSphere = spell;
        }

        internal static void SetGameDeltaTime(this TimeController self, float value)
        {
            set_GameDeltaTime(self, value);
        }

        internal static void SetDeltaTime(this TimeController self, float value)
        {
            set_DeltaTime(self, value);
        }

        static FastSetter set_GameDeltaTime = Helpers.CreateSetter<TimeController>("GameDeltaTime");
        static FastSetter set_DeltaTime = Helpers.CreateSetter<TimeController>("DeltaTime");
    }

    public class SpawnItemWithDelayedSpell : ContextAction
    {
        public BlueprintItem Item;
        public ContextDurationValue SpellDelay;
        public BlueprintAbility Spell;

        public override string GetCaption() => $"Spawn item `{Item.name}` and cast spell `{Spell?.name}` on it after {SpellDelay} rounds.";

        public override void RunAction()
        {
            try
            {
                Log.Append(GetCaption());
                var targetPoint = Context.MainTarget.Point;
                var caster = Context.MaybeCaster;
                var drop = Game.Instance.EntityCreator.SpawnEntityView(
                    BlueprintRoot.Instance.Prefabs.DroppedLootBag,
                    targetPoint,
                    caster.View.transform.rotation,
                    Game.Instance.State.LoadedAreaState.MainState);
                drop.Loot = new ItemsCollection();
                var item = drop.Loot.Add(Item);
                Log.Append($"  item collection {item.Collection}");
                if (Spell != null)
                {
                    // Create a component on the loot that will cast the spell.
                    // (This ensures that it persists across saves/loads.)
                    var c = drop.gameObject.AddComponent<DelayCastSpellOnItemComponent>();
                    drop.Data.EnsureComponentData(c);
                    c.DelayCastSpell(SpellDelay.Calculate(Context), Spell, AbilityContext, item, targetPoint);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    // TODO: there's probably a cleaner way to implement this. Most of the state is to ensure the delayed spell is
    // cast with the original ability parameters (caster level, save DC, etc).
    class DelayCastSpellOnItemComponent : MapObjectComponent, IInitiatorRulebookHandler<RuleCalculateAbilityParams>
    {
        class SpellData : MapObjectComponentPersistentData
        {
            [JsonProperty]
            public float remainingDelay;

            [JsonProperty]
            public BlueprintAbility spell;

            [JsonProperty]
            public UnitEntityData caster;

            [JsonProperty]
            public AbilityParams spellParams;

            [JsonProperty]
            public Ability fromAbility;

            [JsonProperty]
            public BlueprintSpellbook fromSpellbook;

            [JsonProperty]
            public ItemEntity item;

            [JsonProperty]
            public Vector3 originalTarget;
        }

        public override MapObjectComponentPersistentData CreateData() => new SpellData();

        new SpellData Data => (SpellData)base.Data;

        protected override void OnEnable()
        {
            Log.Write($"Enabling {GetType().Name}, resume spell? {Data.spell != null}");
            if (Data.spell != null) MapObject.StartCoroutine(DelayCastSpell());
        }

        internal void DelayCastSpell(Rounds rounds, BlueprintAbility spell, AbilityExecutionContext context, ItemEntity item, Vector3 originalTarget)
        {
            Data.remainingDelay = (float)(rounds.Seconds.TotalMilliseconds / 1000);
            Data.spell = spell;
            Data.caster = context.Caster;
            Data.spellParams = context.Params;
            Data.fromAbility = context.Ability.Fact;
            Data.fromSpellbook = context.Ability.Spellbook?.Blueprint;

            Data.item = item;
            Data.originalTarget = originalTarget;
            Log.Write($"Start coroutine for {GetType().Name}, spell: `{spell.Name}`");
            MapObject.StartCoroutine(DelayCastSpell());
        }

        IEnumerator<object> DelayCastSpell()
        {
            var spell = Data.spell;
            Log.Write($"Start delay cast spell `{spell.Name}`, waiting {Data.remainingDelay} secs)");
            while (Data.remainingDelay > 0)
            {
                Data.remainingDelay -= 0.1f;
                yield return new WaitForSeconds(0.1f);
            }
            try
            {
                Log.Append($"Casting delayed spell `{spell.Name}` now!");

                var drop = (DroppedLoot)MapObject;
                TargetWrapper target;
                var item = Data.item;
                Log.Append($"  item.Collection {item.Collection} isPlayerInventory? {item.Collection?.IsPlayerInventory}");
                if (item.Collection?.IsPlayerInventory == true)
                {
                    target = Game.Instance.Player.Party.Nearest(Data.originalTarget);
                    Log.Append($"  Target is unit {target.Unit.CharacterName}");
                }
                else
                {
                    var loot = drop.Loot.Contains(item) ? drop
                        : UnityEngine.Object.FindObjectsOfType<DroppedLoot>().FirstOrDefault(l => l.Loot.Contains(item));
                    if (loot == null)
                    {
                        Log.Append($"  Item `${item.Name}` is not in inventory or dropped loot. Using original target ${Data.originalTarget}");
                        target = Data.originalTarget;
                    }
                    else
                    {
                        Log.Append($"  Target is loot container `${loot.name}`");
                        target = loot.transform.position;
                    }
                }
                item.Collection.Remove(item);

                // TODO: shows wrong cast location (the original caster)
                // Tricky to fix that without messing with `AbilityDeliverProjectile`.
                var spellData = new AbilityData(spell, Data.caster.Descriptor, Data.fromAbility, Data.fromSpellbook);
                EventBus.Subscribe(this); // Intercept RuleCalculateAbilityParams.
                var castSpell = Rulebook.Trigger(new RuleCastSpell(spellData, target));
                castSpell.ExecutionProcess.InstantDeliver();
                EventBus.Unsubscribe(this);

                Log.Write("  done casting spell!");

                // Cleanup the drop and spell state.
                base.Data = new SpellData();
                if (drop.Loot.Items.Count == 0)
                {
                    drop.Destroy();
                }
                else
                {
                    drop.GetComponent<LootComponent>().DestroyWhenEmpty = true;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        void IRulebookHandler<RuleCalculateAbilityParams>.OnEventAboutToTrigger(RuleCalculateAbilityParams evt)
        {
            if (evt.Spell == Data.spell)
            {
                var spellParams = Data.spellParams;
                evt.ReplaceCasterLevel = spellParams.CasterLevel;
                evt.ReplaceConcentration = spellParams.Concentration;
                evt.ReplaceDC = spellParams.DC;
                evt.ReplaceSpellLevel = spellParams.SpellLevel;
                evt.AddMetamagic(spellParams.Metamagic);
            }
        }

        void IRulebookHandler<RuleCalculateAbilityParams>.OnEventDidTrigger(RuleCalculateAbilityParams evt) { }

        UnitEntityData IInitiatorRulebookSubscriber.GetSubscribingUnit() => Data.caster;
    }

    public class ForceSphereEffect : BuffLogic,
        // Can't target others with these effects
        IInitiatorRulebookHandler<RuleCombatManeuver>,
        IInitiatorRulebookHandler<RuleAttackRoll>,
        IInitiatorRulebookHandler<RuleSpellResistanceCheck>,
        IInitiatorRulebookHandler<RuleSavingThrow>,
        IInitiatorRulebookHandler<RuleDealDamage>,
        IInitiatorRulebookHandler<RuleDrainEnergy>,
        IInitiatorRulebookHandler<RuleDealStatDamage>,
        // Can't be targeted by these affects
        // TODO: should allow targetting by damage spells, and let them hit the sphere.
        // (e.g. Disintegrate should immediately end the sphere.)
        // But need to prevent other effects (e.g. buffs, conditions)
        // TODO: also prevent spells that don't have spell resistance.
        ITargetRulebookHandler<RuleSpellResistanceCheck>,
        ITargetRulebookHandler<RuleDealStatDamage>,
        ITargetRulebookHandler<RuleDrainEnergy>
    {
        public void OnEventAboutToTrigger(RuleCombatManeuver evt)
        {
            evt.AutoFailure = true;
        }
        public void OnEventDidTrigger(RuleCombatManeuver evt) { }

        public void OnEventAboutToTrigger(RuleSavingThrow evt)
        {
            evt.AutoPass = true;
        }
        public void OnEventDidTrigger(RuleSavingThrow evt) { }

        public void OnEventAboutToTrigger(RuleSpellResistanceCheck evt) { }

        public void OnEventDidTrigger(RuleSpellResistanceCheck evt)
        {
            // TODO: AOE spells that bypass spell resistance may not be correctly resisted. 
            if ((evt.Ability.SpellDescriptor & SpellDescriptor.GazeAttack) != 0)
            {
                targetIsImmune(evt, true);
            }
        }

        public void OnEventAboutToTrigger(RuleAttackRoll evt)
        {
            evt.AutoMiss = true;
        }
        public void OnEventDidTrigger(RuleAttackRoll evt) { }

        public void OnEventAboutToTrigger(RuleDealDamage evt)
        {
            foreach (var dmg in evt.DamageBundle)
            {
                dmg.Immune = true;
            }
        }
        public void OnEventDidTrigger(RuleDealDamage evt) { }

        public void OnEventAboutToTrigger(RuleDealStatDamage evt)
        {
            evt.Immune = true;
        }
        public void OnEventDidTrigger(RuleDealStatDamage evt) { }

        public void OnEventAboutToTrigger(RuleDrainEnergy evt)
        {
            evt.TargetIsImmune = true;
        }
        public void OnEventDidTrigger(RuleDrainEnergy evt) { }

        static readonly FastSetter targetIsImmune = Helpers.CreateSetter<RuleSpellResistanceCheck>("TargetIsImmune");
    }

    [AllowedOn(typeof(BlueprintUnitFact))]
    public class TemporaryHitPointsPerCasterLevel : BuffLogic, ITargetRulebookHandler<RuleDealDamage>, IRulebookHandler<RuleDealDamage>, ITargetRulebookSubscriber
    {
        public ModifierDescriptor Descriptor;

        public int HitPointsPerLevel;

        public bool RemoveWhenHitPointsEnd;

        [JsonProperty]
        private ModifiableValue.Modifier m_Modifier;

        public override void OnFactActivate()
        {
            int value = HitPointsPerLevel * Buff.Context.Params.CasterLevel;
            m_Modifier = Owner.Stats.TemporaryHitPoints.AddModifier(value, this, Descriptor);
        }

        public override void OnFactDeactivate()
        {
            m_Modifier?.Remove();
            m_Modifier = null;
        }

        public void OnEventAboutToTrigger(RuleDealDamage evt) { }

        public void OnEventDidTrigger(RuleDealDamage evt)
        {
            if (RemoveWhenHitPointsEnd && m_Modifier.AppliedTo == null)
            {
                base.Owner.RemoveFact(base.Fact);
            }
        }
    }

    public class TimeStopExperiment : BuffLogic,
        // Can't target others with these effects
        IInitiatorRulebookHandler<RuleCombatManeuver>,
        IInitiatorRulebookHandler<RuleAttackRoll>,
        IInitiatorRulebookHandler<RuleSpellResistanceCheck>,
        IInitiatorRulebookHandler<RuleSavingThrow>,
        IInitiatorRulebookHandler<RuleDealDamage>,
        IInitiatorRulebookHandler<RuleDrainEnergy>,
        IInitiatorRulebookHandler<RuleDealStatDamage>,
        IInitiatorRulebookHandler<RuleCastSpell>,
        // Can't be targeted by attacks of opportunity (TODO: can these be prevented?)
        ITargetRulebookHandler<RuleAttackRoll>
    {
        [JsonProperty]
        bool isActive;

        [JsonProperty]
        float originalTimeScale;

        //[JsonProperty]
        //float originalAnimationSpeed;

        internal const int TimeMultiplier = 200;

        internal static bool IsActive = false;

        internal static BlueprintBuff timeStopBuff;

        internal static bool IsActiveOnUnit(UnitEntityData unit)
        {
            return IsActive && unit != null && unit.Buffs.HasFact(timeStopBuff);
        }

        internal static Buff GetBuff(BuffCollection buffs)
        {
            return IsActive ? buffs.GetBuff(timeStopBuff) : null;
        }

        public override void PostLoad()
        {
            IsActive = isActive;
        }

        public override void OnTurnOn()
        {
            try
            {
                // Note: use DebugTimeScale because it's not overwritten by anything.
                var time = Game.Instance.TimeController;
                originalTimeScale = time.DebugTimeScale;
                time.DebugTimeScale /= TimeMultiplier;
                IsActive = isActive = true;
                Log.Append($"Slow down time scale from {originalTimeScale} to {time.DebugTimeScale}");

                // Speed up all existing animations, and new ones.
                var unit = Buff.Owner.Unit;
                var view = unit.View;
                //var animator = view.Animator;
                //originalAnimationSpeed = animator.speed;
                //animator.speed *= TimeMultiplier; // TODO: this does not seem to be used by much.

                // TODO: walk speed animations are still a bit odd looking, perhaps due to acceleration issues?
                //view.AgentASP.MaxSpeedOverride = unit.CombatSpeedMps * TimeMultiplier * 2f;
                foreach (var action in view.AnimationManager.ActiveActions)
                {
                    action.SpeedScale *= TimeMultiplier;
                    Log.Append($"speed up anim {action} ({action.GetType().Name}) to {action.SpeedScale}");
                }
                Log.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public override void OnTurnOff()
        {
            try
            {
                var time = Game.Instance.TimeController;
                time.DebugTimeScale = originalTimeScale;
                var view = Buff.Owner.Unit.View;
                //var animator = view.Animator;
                //animator.speed = originalAnimationSpeed;
                //view.AgentASP.MaxSpeedOverride = null;

                foreach (var action in view.AnimationManager.ActiveActions)
                {
                    action.SpeedScale /= TimeMultiplier;
                    Log.Append($"slow down anim {action} ({action.GetType().Name}) to {action.SpeedScale}");
                }
                IsActive = isActive = false;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void OnEventAboutToTrigger(RuleCombatManeuver evt)
        {
            evt.AutoFailure = true;
        }
        public void OnEventDidTrigger(RuleCombatManeuver evt) { }

        public void OnEventAboutToTrigger(RuleSavingThrow evt)
        {
            evt.AutoPass = true;
        }
        public void OnEventDidTrigger(RuleSavingThrow evt) { }

        public void OnEventAboutToTrigger(RuleSpellResistanceCheck evt) { }

        public void OnEventDidTrigger(RuleSpellResistanceCheck evt)
        {
            targetIsImmune(evt, true);
        }

        public void OnEventAboutToTrigger(RuleAttackRoll evt)
        {
            evt.AutoMiss = true;
        }
        public void OnEventDidTrigger(RuleAttackRoll evt) { }

        public void OnEventAboutToTrigger(RuleDealDamage evt)
        {
            foreach (var dmg in evt.DamageBundle)
            {
                dmg.Immune = true;
            }
        }
        public void OnEventDidTrigger(RuleDealDamage evt) { }

        public void OnEventAboutToTrigger(RuleDealStatDamage evt)
        {
            evt.Immune = true;
        }
        public void OnEventDidTrigger(RuleDealStatDamage evt) { }

        public void OnEventAboutToTrigger(RuleDrainEnergy evt)
        {
            evt.TargetIsImmune = true;
        }
        public void OnEventDidTrigger(RuleDrainEnergy evt) { }

        public void OnEventAboutToTrigger(RuleCastSpell evt) { }

        public void OnEventDidTrigger(RuleCastSpell evt)
        {
            evt.ExecutionProcess.InstantDeliver();
        }

        static readonly FastSetter targetIsImmune = Helpers.CreateSetter<RuleSpellResistanceCheck>("TargetIsImmune");
    }

    // Inspired by ContextActionMeleeAttack
    public class SpiritualWeaponAttackAction : BuffLogic, ITickEachRound, IInitiatorRulebookHandler<RuleCalculateWeaponStats>
    {
        public BlueprintProjectile[] Projectiles;
        ItemEntityWeapon weapon;

        [JsonProperty]
        UnitEntityData target;

        public static SpiritualWeaponAttackAction Create(BlueprintProjectile[] projectiles)
        {
            var s = Helpers.Create<SpiritualWeaponAttackAction>();
            s.Projectiles = projectiles;
            return s;
        }

        public override void OnTurnOn()
        {
            EventBus.Subscribe(this);
            DoAttack(true);
            EventBus.Unsubscribe(this);
        }

        public void OnNewRound() => DoAttack(false);

        void DoAttack(bool first)
        {
            try
            {
                bool changedTarget = false;
                if (first)
                {
                    target = Context.SourceAbilityContext.MainTarget.Unit;
                    changedTarget = true;
                    Log.Append($"Spiritual Weapon: using initial target {target.CharacterName}");
                }
                else if (!Owner.Unit.IsInCombat)
                {
                    Log.Write($"Spiritual Weapon: not in combat");
                    target = null;
                    return;
                }
                var caster = Context.MaybeCaster;
                if (target == null || target.Descriptor.State.IsDead || target.Descriptor.State.IsUntargetable)
                {
                    // Do we have a move action to change target?
                    var cooldowns = caster.CombatState.Cooldown;
                    if (cooldowns.MoveAction > 0)
                    {
                        Log.Write($"Spiritual Weapon: can't change targets, waiting move cooldown: {cooldowns.MoveAction}");
                        return;
                    }
                    target = ContextActionMeleeAttack.SelectTarget(caster, 100.Feet().Meters, true, caster);
                    if (target == null)
                    {
                        Log.Write($"Spiritual Weapon: no target within 100 ft, skipping attack.");
                        return;
                    }
                    changedTarget = true;

                    // Use a move action to change targets
                    cooldowns.MoveAction += 3f;
                    Log.Append($"Spiritual Weapon: changed target using move action, move cooldown: {cooldowns.MoveAction}");
                }

                if (weapon == null)
                {
                    weapon = (ItemEntityWeapon)GetSpiritualWeapon(caster.Descriptor).CreateEntity();
                    weapon.AddEnchantment(Helpers.ghostTouch, Context);
                }

                Log.Append($"Spiritual Weapon: caster {caster.CharacterName}, target {target.CharacterName}, weapon {weapon.Name}");

                var rule = Context.TriggerRule(new RuleCalculateAttackBonus(caster, target, weapon, 0));
                Log.Append("  Triggered attack bonus rule");
                var baseAttackBonus = caster.Stats.BaseAttackBonus.PermanentValue;
                Log.Append($"  baseAttackBonus {baseAttackBonus}");
                var context = Buff.Context.SourceAbilityContext;
                var ability = context.Ability;
                // TODO: if cast via Wish/Miracle, we can't find the casting attribute.
                var castingAttribute = ability.Spellbook?.Blueprint.CastingAttribute ?? StatType.Wisdom;
                var castingBonus = caster.Stats.GetStat<ModifiableValueAttributeStat>(castingAttribute).Bonus;
                Log.Append($"  castingAttribute {castingAttribute}");
                var realAttackBonus = baseAttackBonus + castingBonus;
                Log.Append($"  Calculated attack bonus {rule.Result}, should be {realAttackBonus}, adjusting.");
                var penalty = rule.Result - realAttackBonus;
                if (changedTarget)
                {
                    DoAttack(target, penalty, false, Projectiles[0]);
                }
                else
                {
                    for (int attackBonus = baseAttackBonus, i = 0; attackBonus > 0; attackBonus -= 5, i++)
                    {
                        DoAttack(target, penalty + baseAttackBonus - attackBonus, true, Projectiles[i]);
                    }
                }
                Log.Flush();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        void DoAttack(UnitEntityData target, int attackPenalty, bool isFullAttack, BlueprintProjectile projectile)
        {
            Log.Append($"  Starting attack with {weapon.Name}, attack penalty {attackPenalty}");
            var ruleAttack = new RuleAttackWithWeapon(Context.MaybeCaster, target, weapon, attackPenalty);
            ruleAttack.IsFullAttack = isFullAttack;
            var visualParams = weapon.Blueprint.VisualParameters;

            // TODO: this is a hack, it can be avoided by creating spiritual weapon blueprints upfront.
            var savedProjectiles = visualParams.Projectiles;
            Helpers.SetField(visualParams, "m_Projectiles", new BlueprintProjectile[] { projectile });
            Context.TriggerRule(ruleAttack);
            Helpers.SetField(visualParams, "m_Projectiles", savedProjectiles);
        }

        BlueprintItemWeapon GetSpiritualWeapon(UnitDescriptor caster)
        {
            // Get the deity feat, if the caster has one.
            var deityFeat = caster.Progression.Features.Enumerable.Select(f => f.Blueprint).FirstOrDefault(
                f => f.HasGroup(FeatureGroup.Deities) && !(f is BlueprintFeatureSelection));
            WeaponCategory? category = null;
            if (deityFeat != null)
            {
                var items = deityFeat.GetComponent<AddStartingEquipment>()?.CategoryItems;
                if (items != null && items.Length > 0)
                {
                    category = items[0];
                    Log.Append($"Found deity {deityFeat.Name}, favored weapon {category}.");
                }
            }
            if (category == null)
            {
                foreach (var alignment in UnitAlignment.GetAlignmentsSortedByDistance(caster.Alignment.Vector))
                {
                    switch (alignment)
                    {
                        case Alignment.NeutralGood:
                            category = WeaponCategory.Warhammer;
                            break;
                        case Alignment.LawfulNeutral:
                            category = WeaponCategory.Longsword;
                            break;
                        case Alignment.ChaoticNeutral:
                            category = WeaponCategory.Battleaxe;
                            break;
                        case Alignment.NeutralEvil:
                            category = WeaponCategory.Flail;
                            break;
                        default:
                            continue;
                    }
                    break;
                }
            }

            var defaultWeapons = Game.Instance.BlueprintRoot.Progression.CategoryDefaults.Entries;
            return defaultWeapons.First((p) => p.Key == category)?.DefaultWeapon;
        }

        public void OnEventAboutToTrigger(RuleCalculateWeaponStats evt)
        {
            if (evt.Weapon != weapon) return;
            var casterLevel = Context.Params.CasterLevel;
            var bonusDamage = Math.Min(5, casterLevel / 3);
            evt.AddBonusDamage(bonusDamage);
            evt.OverrideDamageBonusStatMultiplier(0);
            Log.Append($"Adjusted weapon stats: bonus damage {bonusDamage}, enhancement {evt.Enhancement}");
        }

        public void OnEventDidTrigger(RuleCalculateWeaponStats evt)
        {
            foreach (var dmg in evt.DamageDescription)
            {
                dmg.TypeDescription.Type = DamageType.Force;
            }
        }
    }

#if EXPERIMENTAL_TimeStop

    [Harmony12.HarmonyPatch(typeof(UnitAnimationManager), "Execute", new Type[] { typeof(AnimationActionHandle) })]
    static class UnitAnimationManager_Execute_Patch
    {
        static bool Prefix(UnitAnimationManager __instance, AnimationActionHandle handle)
        {
            var self = __instance;
            try
            {
                if (TimeStopExperiment.IsActiveOnUnit(self.View?.EntityData))
                {
                    handle.SpeedScale *= TimeStopExperiment.TimeMultiplier;
                    Log.Append($"speed up anim {handle} ({handle.GetType().Name}) to {handle.SpeedScale}");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return true;
        }
    }

    /*
    [Harmony12.HarmonyPatch(typeof(UnitAnimationActionLocoMotion), "OnUpdate", typeof(UnitAnimationActionHandle), typeof(float))]
    static class UnitAnimationActionLocoMotion_OnUpdate_Patch
    {
        static bool Prefix(UnitAnimationActionLocoMotion __instance, UnitAnimationActionHandle handle, float deltaTime)
        {
            var self = __instance;
            var unit = handle.Unit?.EntityData;
            if (TimeStopExperiment.IsActiveOnUnit(unit))
            {
                Log.Append($"UnitAnimationActionLocoMotion_OnUpdate_Patch, unit {unit.CharacterName}");
                Log.Append($"  deltaTime {deltaTime}");
                Log.Append($"  handle.GetTime() {handle.GetTime()}");
                Log.Append($"  handle.SpeedScale {handle.SpeedScale}");
                Log.Append($"  handle.ActiveAnimation.GetSpeed() {handle.ActiveAnimation.GetSpeed()}");
                Log.Append($"  handle.ActiveAnimation.GetTime() {handle.ActiveAnimation.GetTime()}");
                Log.Flush();
            }
            return true;
        }
    }*/

    // TODO: maybe patch UnitAnimationActionLocoMotion directly instead?
    [Harmony12.HarmonyPatch(typeof(UnitAnimationManager), "Tick", new Type[0])]
    static class UnitAnimationManager_Tick_Patch
    {
        static bool Prefix(UnitAnimationManager __instance)
        {
            try
            {
                var self = __instance;
                var view = self.View;
                var unit = view?.EntityData;
                if (TimeStopExperiment.IsActiveOnUnit(unit))
                {
                    var speed = self.Speed;
                    Log.Append($"Can move? {unit.Descriptor.State.CanMove}");
                    Log.Append($"  view.AgentASP.Speed {view.AgentASP.Speed}");
                    Log.Append($"  GetSpeedAnimationCoeff {view.GetSpeedAnimationCoeff(self.WalkSpeedType, self.IsInCombat)}");
                    Log.Append($"  self.WalkSpeedType {self.WalkSpeedType}");
                    Log.Append($"  self.IsInCombat {self.IsInCombat}");

                    var estimatedSpeed = (!unit.Descriptor.State.CanMove ? 0f : (view.AgentASP.Speed * view.GetSpeedAnimationCoeff(self.WalkSpeedType, self.IsInCombat)));
                    if (Mathf.Approximately(speed, estimatedSpeed))
                    {
                        self.Speed *= TimeStopExperiment.TimeMultiplier;
                        Log.Append($"speed up movement for {unit.CharacterName} from {estimatedSpeed} to {self.Speed}");
                    }
                    Log.Flush();
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return true;
        }
    }

    [Harmony12.HarmonyPatch(typeof(UnitAnimationActionCastSpell), "OnStart", typeof(UnitAnimationActionHandle))]
    static class UnitAnimationActionCastSpell_OnStart_Patch
    {
        static void Postfix(UnitAnimationActionCastSpell __instance, UnitAnimationActionHandle handle)
        {
            var self = __instance;
            if (TimeStopExperiment.IsActiveOnUnit(handle.Unit.EntityData))
            {
                var multiplier = TimeStopExperiment.TimeMultiplier;
                //self.CastSpeedup *= multiplier;
                //self.PrecastSpeedup *= multiplier;
                //handle.CastingTime /= multiplier;
                //handle.SpeedScale *= multiplier;
                handle.ActiveAnimation.SetSpeed(handle.ActiveAnimation.GetSpeed() * multiplier);
                Log.Write($"Speed up cast animation for {handle.Unit.EntityData.CharacterName}, casting time {handle.CastingTime}, speed scale {handle.SpeedScale}, animation speed {handle.ActiveAnimation.GetSpeed()}");
            }
        }
    }


    [Harmony12.HarmonyPatch(typeof(UnitAnimationActionCastSpell), "OnUpdate", typeof(UnitAnimationActionHandle), typeof(float))]
    static class UnitAnimationActionCastSpell_OnUpdate_Patch
    {
        static float? animationSpeed;

        static bool Prefix(UnitAnimationActionCastSpell __instance, UnitAnimationActionHandle handle, float deltaTime)
        {
            if (TimeStopExperiment.IsActiveOnUnit(handle.Unit.EntityData))
            {
                animationSpeed = handle.ActiveAnimation.GetSpeed();
            }
            return true;
        }
        static void Postfix(UnitAnimationActionCastSpell __instance, UnitAnimationActionHandle handle, float deltaTime)
        {
            var self = __instance;
            if (animationSpeed.HasValue)
            {
                var animation = handle.ActiveAnimation;
                if (animation.GetSpeed() != animationSpeed)
                {
                    var multiplier = TimeStopExperiment.TimeMultiplier;
                    animation.SetSpeed(animation.GetSpeed() * multiplier);
                    Log.Write($"Speed up cast animation for {handle.Unit.EntityData.CharacterName}, casting time {handle.CastingTime}, new speed {animation.GetSpeed()}");
                }
                animationSpeed = null;
            }
        }
    }

    [Harmony12.HarmonyPatch(typeof(FxHelper), "SpawnFxOnUnit", typeof(GameObject), typeof(UnitEntityView), typeof(string), typeof(Vector3))]
    static class FxHelper_SpanwFxOnUnit_Patch
    {
        public static void Postfix(GameObject prefab, UnitEntityView unit, GameObject __result)
        {
            if (__result == null) return;

            if (!TimeStopExperiment.IsActiveOnUnit(unit.EntityData)) return;

            var particleSystems = __result.GetComponentsInChildren<ParticleSystem>();
            if (particleSystems != null)
            {
                foreach (var system in particleSystems)
                {
                    var main = system.main;
                    main.simulationSpeed *= TimeStopExperiment.TimeMultiplier;
                    Log.Append($"speed up particle system {system.name} to {main.simulationSpeed}");
                }
                Log.Flush();
            }

        }
    }
    /*
        [Harmony12.HarmonyPatch(typeof(FxHelper), "SpawnFxOnGameObject", typeof(GameObject), typeof(GameObject), typeof(string), typeof(Vector3), typeof(float))]
        static class FxHelper_SpanwFxOnGameObject_Patch
        {
            public static void Postfix(GameObject prefab, GameObject __result)
            {
                FxHelper_SpanwFxOnPoint_Patch.Postfix(prefab, __result);
            }
        }

        [Harmony12.HarmonyPatch(typeof(FxHelper), "SpawnFxOnPoint", typeof(GameObject), typeof(Vector3), typeof(Quaternion))]
        static class FxHelper_SpanwFxOnPoint_Patch
        {
            public static void Postfix(GameObject prefab, GameObject __result)
            {
                if (__result == null) return;

                if (!TimeStopExperiment.IsActive) return;

                var particleSystems = __result.GetComponentsInChildren<ParticleSystem>();
                if (particleSystems == null) return;

                foreach (var system in particleSystems)
                {
                    var main = system.main;
                    main.simulationSpeed *= TimeStopExperiment.TimeMultiplier;
                    Log.Append($"speed up particle system {system.name} to {main.simulationSpeed}");
                }
                Log.Flush();
            }
        }*/

    [Harmony12.HarmonyPatch(typeof(BuffCollection), "Tick")]
    static class BuffCollection_Tick_Patch
    {
        static bool Prefix(BuffCollection __instance)
        {
            var self = __instance;

            var timeStopBuff = TimeStopExperiment.GetBuff(self);
            if (timeStopBuff == null) return true;

            var deltaTime = Game.Instance.TimeController.GameDeltaTime;
            var apparentTime = TimeSpan.FromSeconds(deltaTime * TimeStopExperiment.TimeMultiplier - deltaTime);

            foreach (var fact in self.RawFacts)
            {
                var buff = (Buff)fact;
                var endTime = buff.EndTime;
                if (endTime != TimeSpan.MaxValue)
                {
                    buff.EndTime = endTime - apparentTime;
                }
                var nextTick = (TimeSpan)getNextTickTime(buff);
                if (nextTick != TimeSpan.MaxValue)
                {
                    setNextTickTime(buff, nextTick - apparentTime);
                }
            }
            return true;
        }

        static FastGetter getNextTickTime = Helpers.CreateGetter<Buff>("NextTickTime");
        static FastSetter setNextTickTime = Helpers.CreateSetter<Buff>("NextTickTime");
    }

    [Harmony12.HarmonyPatch(typeof(UnitCombatCooldownsController), "TickOnUnit", new Type[] { typeof(UnitEntityData) })]
    static class UnitCombatCooldownsController_TickOnUnit_Patch
    {
        static float? savedGameDeltaTime;
        static TimeController Time => Game.Instance.TimeController;

        static bool Prefix(UnitCombatCooldownsController __instance, UnitEntityData unit)
        {
            if (TimeStopExperiment.IsActiveOnUnit(unit))
            {
                savedGameDeltaTime = Time.GameDeltaTime;
                Time.SetGameDeltaTime(Time.GameDeltaTime * TimeStopExperiment.TimeMultiplier);
                Log.Append($"UnitCombatCooldownsController_TickOnUnit_Patch time is now {Time.GameDeltaTime}");
            }
            return true;
        }

        static void Postfix(UnitCombatCooldownsController __instance, UnitEntityData unit)
        {
            if (savedGameDeltaTime.HasValue)
            {
                Time.SetGameDeltaTime(savedGameDeltaTime.Value);
                savedGameDeltaTime = null;
            }
        }
    }

    [Harmony12.HarmonyPatch(typeof(UnitTicksController), "TickOnUnit", new Type[] { typeof(UnitEntityData) })]
    static class UnitTicksController_TickOnUnit_Patch
    {
        static float? savedGameDeltaTime;
        static TimeController Time => Game.Instance.TimeController;

        static bool Prefix(UnitTicksController __instance, UnitEntityData unit)
        {
            if (TimeStopExperiment.IsActiveOnUnit(unit))
            {
                savedGameDeltaTime = Time.GameDeltaTime;
                Time.SetGameDeltaTime(Time.GameDeltaTime * TimeStopExperiment.TimeMultiplier);
                Log.Append($"UnitTicksController_TickOnUnit_Patch time is now {Time.GameDeltaTime}");
            }
            return true;
        }

        static void Postfix(UnitTicksController __instance, UnitEntityData unit)
        {
            if (savedGameDeltaTime.HasValue)
            {
                Time.SetGameDeltaTime(savedGameDeltaTime.Value);
                savedGameDeltaTime = null;
            }
        }
    }

    [Harmony12.HarmonyPatch(typeof(UnitActivatableAbilitiesController), "TickOnUnit", new Type[] { typeof(UnitEntityData) })]
    static class UnitActivatableAbilitiesController_TickOnUnit_Patch
    {
        static float? savedGameDeltaTime;
        static TimeController Time => Game.Instance.TimeController;

        static bool Prefix(UnitTicksController __instance, UnitEntityData unit)
        {
            if (TimeStopExperiment.IsActiveOnUnit(unit))
            {
                savedGameDeltaTime = Time.GameDeltaTime;
                Time.SetGameDeltaTime(Time.GameDeltaTime * TimeStopExperiment.TimeMultiplier);
                Log.Append($"UnitActivatableAbilitiesController_TickOnUnit_Patch time is now {Time.GameDeltaTime}");
            }
            return true;
        }

        static void Postfix(UnitTicksController __instance, UnitEntityData unit)
        {
            if (savedGameDeltaTime.HasValue)
            {
                Time.SetGameDeltaTime(savedGameDeltaTime.Value);
                savedGameDeltaTime = null;
            }
        }
    }

    [Harmony12.HarmonyPatch(typeof(UnitMovementAgent), "TickMovement", new Type[] { typeof(float) })]
    static class UnitMovementAgent_TickMovement_Patch
    {
        static float? savedGameDeltaTime;
        static TimeController Time => Game.Instance.TimeController;

        static bool Prefix(UnitMovementAgent __instance, ref float deltaTime)
        {
            var self = __instance;
            var unit = self.Unit;
            if (TimeStopExperiment.IsActiveOnUnit(unit?.EntityData))
            {
                savedGameDeltaTime = Time.GameDeltaTime;
                Time.SetGameDeltaTime(deltaTime = Time.GameDeltaTime * TimeStopExperiment.TimeMultiplier);
                Log.Append($"UnitMovementAgent_TickMovement_Patch time is now {Time.GameDeltaTime}");
                Log.Append($"  Speed: {self.Speed}, m_WarmupTime: {Helpers.GetField(self, "m_WarmupTime")}");
                Log.Append($"  unit.IsCommandsPreventMovement? {unit.IsCommandsPreventMovement}");
                Log.Append($"  unit.AnimationManager?.IsPreventingMovement? {unit.AnimationManager?.IsPreventingMovement}");
                Log.Append($"  IsReallyMoving? {self.IsReallyMoving} m_FirstTick? {Helpers.GetField(self, "m_FirstTick")}");
                Log.Flush();
            }
            return true;
        }

        static void Postfix(UnitMovementAgent __instance)
        {
            if (savedGameDeltaTime.HasValue)
            {
                Time.SetGameDeltaTime(savedGameDeltaTime.Value);
                savedGameDeltaTime = null;
            }
        }
    }

    // TODO: this is aborting commands if they take longer than 1 round.
    // Casting spells is taking too long (some kind of delay)
    [Harmony12.HarmonyPatch(typeof(UnitCommand), "Tick", new Type[0])]
    static class UnitCommand_Tick_Patch
    {
        static float? savedDeltaTime;
        static TimeController Time => Game.Instance.TimeController;

        static bool Prefix(UnitCommand __instance)
        {
            var self = __instance;
            if (TimeStopExperiment.IsActiveOnUnit(self.Executor))
            {
                savedDeltaTime = Time.DeltaTime;
                Time.SetDeltaTime(Time.DeltaTime * TimeStopExperiment.TimeMultiplier);
            }
            return true;
        }

        static void Postfix(UnitTicksController __instance)
        {
            if (savedDeltaTime.HasValue)
            {
                Time.SetDeltaTime(savedDeltaTime.Value);
                savedDeltaTime = null;
            }
        }
    }
#endif

#if false

    [Harmony12.HarmonyPatch(typeof(UnitCommand), "get_IsOneFrameCommand", new Type[0])]
    static class UnitCommand_IsOneFrameCommand_Patch
    {
        static float? savedDeltaTime;
        static TimeController Time => Game.Instance.TimeController;

        static void Postfix(UnitCommand __instance, ref bool __result)
        {
            var self = __instance;
            if (!__result && TimeStopExperiment.IsActiveOnUnit(self.Executor))
            {
                __result = !(self is UnitAttack);
                if (__result) Log.Write($"Using one frame command for {self.Executor.CharacterName}, spell: {(self as UnitUseAbility)?.Spell}");
            }
        }

        static void Postfix(UnitTicksController __instance)
        {
            if (savedDeltaTime.HasValue)
            {
                Time.SetDeltaTime(savedDeltaTime.Value);
                savedDeltaTime = null;
            }
        }
    }
    
    [Harmony12.HarmonyPatch(typeof(UnitUseAbility), "Init", new Type[] { typeof(UnitEntityData) })]
    static class UnitUseAbility_Init_Patch
    {
        static void Postfix(UnitUseAbility __instance, UnitEntityData executor)
        {
            var self = __instance;
            try
            {
                if (TimeStopExperiment.IsActiveOnUnit(executor))
                {
                    // TODO: this does not seem to be working.
                    //set_CastTime(self, (float)get_CastTime(self) / TimeStopExperiment.TimeMultiplier);
                    set_CastTime(self, 0f);
                    set_Special(self, UnitAnimationActionCastSpell.SpecialBehaviourType.NoCast);
                    set_CastAnimStyle(self, UnitAnimationActionCastSpell.CastAnimationStyle.Self);
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        static FastSetter set_CastTime = Helpers.CreateFieldSetter<UnitUseAbility>("m_CastTime");
        static FastSetter set_Special = Helpers.CreateFieldSetter<UnitUseAbility>("m_Special");
        static FastSetter set_CastAnimStyle = Helpers.CreateFieldSetter<UnitUseAbility>("m_CastAnimStyle");

        static FastGetter get_CastTime = Helpers.CreateFieldGetter<UnitUseAbility>("m_CastTime");
    }
#endif

#if EXPERIMENTAL_EmergencyForceSpehere
    [Harmony12.HarmonyPatch(typeof(UnitUseAbility), "GetActionType", new Type[] { typeof(AbilityData) })]
    static class UnitUseAbility_GetActionType_Patch
    {
        static bool Prefix(UnitUseAbility __instance, AbilityData spell, ref CommandType __result)
        {
            var self = __instance;
            try
            {
                // This was an attempt to get an instant action; it didn't seem to work.
                if (spell.Blueprint == ExperimentalSpells.emergencyForceSphere)
                {
                    self.IgnoreCooldown();
                    __result = CommandType.Free;
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
#endif
}