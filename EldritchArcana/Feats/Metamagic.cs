// Copyright (c) 2019 Jennifer Messerly
// This code is licensed under MIT license (see LICENSE for details)

using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Loot;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Designers.Mechanics.Recommendations;
using Kingmaker.ElementsSystem;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums.Damage;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Utility;

namespace EldritchArcana
{

    // The bits for new metamagics introduced by this mod. This corresponds to the `Metamagic` enum
    // in PF:K. The game only uses 6 bits (out of 32), so there's a lot more we can use.
    //
    // To reduce the chance of conflict with any metamagic added by the devs, we use higher bits.
    //
    // For now, focus on the popular metamagics because it's a big list (https://www.d20pfsrd.com/feats/metamagic-feats/).
    [Flags]
    public enum ModMetamagic
    {
        Intensified = 0x40000000,
        Dazing = 0x20000000,
        Persistent = 0x10000000,
        Rime = 0x08000000,
        Toppling = 0x04000000,
        Selective = 0x02000000,
        ElementalFire = 0x01000000,
        ElementalCold = 0x00800000,
        ElementalElectricity = 0x00400000,
        ElementalAcid = 0x00200000,
        Elemental = ElementalFire | ElementalCold | ElementalElectricity | ElementalAcid
    }

    static class MetamagicFeats
    {
        internal static BlueprintFeature persistentSpellFeat;

        static List<BlueprintFeature> metamagicFeats;

        internal static IEnumerable<BlueprintFeature> CreateMetamagicFeats()
        {
            if (metamagicFeats != null) return metamagicFeats;
            var library = Main.library;

            // Patch the cost computation for metamagics so the new ones are recognized.
            Main.ApplyPatch(typeof(MetamagicHelper_DefaultCost_Patch), "Spell level cost for new metamagic feats");

            foreach (var spell in Helpers.allSpells)
            {
                var newMetamagic = (Metamagic)0;
                if (spell.AoERadius.Meters > 0f || spell.ProjectileType != AbilityProjectileType.Simple)
                {
                    newMetamagic |= (Metamagic)ModMetamagic.Selective;
                }

                if (spell.EffectOnEnemy == AbilityEffectOnUnit.Harmful)
                {
                    // TODO: check if it actually has a saving throw.
                    // (Need to recursively search for ContextActionSavingThrow.)
                    // TODO: spells like Web may not working with persistent spell.
                    newMetamagic |= (Metamagic)ModMetamagic.Persistent;

                    var dealsDamage = (spell.AvailableMetamagic & Metamagic.Empower) == Metamagic.Empower;
                    var descriptor = spell.SpellDescriptor;
                    var hasElement = (descriptor & (SpellDescriptor.Fire | SpellDescriptor.Cold | SpellDescriptor.Electricity | SpellDescriptor.Acid)) != 0;
                    if (dealsDamage || hasElement)
                    {
                        // TODO: many spells did not have the cold descriptor set.
                        // We'll need to scan for elemental damage (any element can be cold if used with
                        // Elemental Spell or the elemental arcana).
                        newMetamagic |= (Metamagic)ModMetamagic.Rime;

                        newMetamagic |= (Metamagic)ModMetamagic.Elemental;

                        // TODO: this won't work for spells that don't have variable damage components.
                        // We'll need to traverse the components to look for ContextActionDealDamage.
                        newMetamagic |= (Metamagic)ModMetamagic.Dazing;

                        // TODO: this has false positives (spells that can't actually benefit, because they
                        // already scale to 20th level). Presumably we could look for ContextRankConfig?
                        newMetamagic |= (Metamagic)ModMetamagic.Intensified;
                    }
                    var hasForce = (descriptor & SpellDescriptor.Force) != 0;
                    if (hasForce || spell.AssetGuid == "4ac47ddb9fa1eaf43a1b6809980cfbd2")
                    {
                        // Magic Missile does not correctly have the force descriptor set.
                        // Scan for damage actions?
                        newMetamagic |= (Metamagic)ModMetamagic.Toppling;
                    }
                }

                spell.AvailableMetamagic |= newMetamagic;
                if (spell.Parent != null) spell.Parent.AvailableMetamagic |= newMetamagic;
            }

            // TODO: need some new icons for these feats.
            // The spell schools here are very arbitrary, it's just a way to get unique icons for each.
            var feats = new List<BlueprintFeature>();
            feats.Add(CreateMetamagicFeat(
                ModMetamagic.Dazing, "2a9007dd7d9e4dfab2e5eef02d1cb596", "Dazing Spell",
                "You can modify a spell to daze a creature damaged by the spell. When a creature takes damage from this spell, they become dazed for a number of rounds equal to the original level of the spell. If the spell allows a saving throw, a successful save negates the daze effect. If the spell does not allow a save, the target can make a Will save to negate the daze effect. If the spell effect also causes the creature to become dazed, the duration of this metamagic effect is added to the duration of the spell." +
                "\nLevel Increase: +3",
                /*SpellFocusEnchantment*/
                "c5bf645f128c39b40850cde005b8538f",
                Helpers.Create<DazingMetamagic>(d => d.DazeBuff = library.Get<BlueprintBuff>("9934fedff1b14994ea90205d189c8759"))));
            feats.Add(CreateMetamagicFeat(
                ModMetamagic.Intensified, "ac3a3d3cdf4e4723bd99c14782092e8e", "Intensified Spell",
                "An intensified spell increases the maximum number of damage dice by 5 levels. You must actually have sufficient caster levels to surpass the maximum in order to benefit from this feat. No other variables of the spell are affected, and spells that inflict damage that is not modified by caster level are not affected by this feat." +
                "\nLevel Increase: +1",
                /*SpellFocusDivination*/
                "955e97411611d384db2cbc00d7ed5ead"));
            // patch to adjust the maximum dice cap.
            ContextRankConfig_GetValue_Patch.Apply();

            feats.Add(CreateMetamagicFeat(
                ModMetamagic.Rime, "72e78961aec04ecfb92c50c280e9d8bb", "Rime Spell",
                "The frost of your cold spell clings to the target, impeding it for a short time. A rime spell causes creatures that takes cold damage from the spell to become entangled for a number of rounds equal to the original level of the spell." +
                "\nThis feat only affects spells with the cold descriptor." +
                "\nLevel Increase: +1",
                /*SpellFocusNecromancy*/
                "8791da25011fd1844ad61a3fea6ece54",
                Helpers.Create<RimeMetamagic>(r => r.EntangleBuff = library.Get<BlueprintBuff>("f7f6330726121cf4b90a6086b05d2e38"))));
            feats.Add(CreateMetamagicFeat(
                ModMetamagic.Toppling, "566739fdc0e2417f9165b72bab7ba421", "Toppling Spell",
                "The impact of your force spell is strong enough to knock the target prone. If the target takes damage, fails its saving throw, or is moved by your force spell, make a trip check against the target, using your caster level plus your casting ability score bonus (Wisdom for clerics, Intelligence for wizards, and so on). This does not provoke an attack of opportunity. If the check fails, the target cannot attempt to trip you or the force effect in response." +
                "\nThis feat only affects spells with the force descriptor." +
                "\nLevel Increase: +1",
                /*SpellFocusConjuration*/
                "d342cc595f499434687f9765f56d525c",
                Helpers.Create<TopplingMetamagic>()));
            feats.Add(persistentSpellFeat = CreateMetamagicFeat(
                ModMetamagic.Persistent, "fd0df5bbcadb4a5abb3d3030aeceb9a9", "Persistent Spell",
                "Whenever a creature targeted by a persistent spell or within its area succeeds on its saving throw against the spell, it must make another saving throw against the effect. If a creature fails this second saving throw, it suffers the full effects of the spell, as if it had failed its first saving throw." +
                "\nLevel Increase: +2",
                /*SpellFocusIllusion*/
                "e588279a80eb7a24b813fadad4bc83b5",
                Helpers.Create<PersistentMetamagic>()));

            // Note: this is different from PnP. Since it's impossible to selecti which targets to exclude,
            // all friendly targets are excluded (rather than limiting it to the caster's ability bonus).
            feats.Add(CreateMetamagicFeat(
                ModMetamagic.Selective, "7b6cf55779314114aa85e1b1577e94bf", "Selective Spell",
                "When casting a selective spell with an area effect and a duration of instantaneous, friendly targets in the area are excluded from the effects of your spell." +
                "\nLevel Increase: +1",
                /*SpellFocusAbjuration*/
                "71a3f1c1ac77ae3488b9b3d6d2aac01a",
                Helpers.Create<SelectiveMetamagic>(),
                Helpers.PrerequisiteStatValue(StatType.SkillKnowledgeArcana, 10)));
            RuleSpellTargetCheck.ApplyPatch();

            var elementalSpellFeat = Helpers.CreateFeatureSelection(
                "MetamagicElementalSpellSelection",
                "Metamagic (Elemental Spell)",
                "Choose one energy type: acid, cold, electricity, or fire. You may replace a spell’s normal damage with that energy type.\nLevel Increase: +1",
                "3c4cf55166884d7093366d26f90b609c",
                Helpers.GetIcon("bb24cc01319528849b09a3ae8eec0b31"), // ElementalFocusSelection
                FeatureGroup.Feat,
                Helpers.Create<RecommendationRequiresSpellbook>());
            elementalSpellFeat.Groups = new FeatureGroup[] { FeatureGroup.WizardFeat, FeatureGroup.Feat };
            elementalSpellFeat.SetFeatures(
                CreateElementalMetamagicFeat(ModMetamagic.ElementalFire, DamageEnergyType.Fire, "8fe8989edd8847968d36c57f90a1f344", /*ElementalFocusFire*/ "13bdf8d542811ac4ca228a53aa108145"),
                CreateElementalMetamagicFeat(ModMetamagic.ElementalCold, DamageEnergyType.Cold, "9fafef03ad234c389b36e9c13199e60a", /*ElementalFocusCold*/ "2ed9d8bf76412ba4a8afe38fa9925fca"),
                CreateElementalMetamagicFeat(ModMetamagic.ElementalElectricity, DamageEnergyType.Electricity, "f506a40aa32440839ba83177781d18b5", /*ElementalFocusElectricity*/ "d439691f37d17804890bd9c263ae1e80"),
                CreateElementalMetamagicFeat(ModMetamagic.ElementalAcid, DamageEnergyType.Acid, "4a78281cc87d43929a5417ae0ccb8e43", /*ElementalFocusAcid*/ "52135eada006e9045a848cd659749608"));

            feats.Add(elementalSpellFeat);

            Main.SafeLoad(AddMetamagicRodsToVendors, "Metamagic rods");
            return metamagicFeats = feats;
        }

        static BlueprintFeature CreateElementalMetamagicFeat(ModMetamagic metamagic, DamageEnergyType energyType, String assetId, String iconAssetId)
        {
            var friendlyName = $"Elemental Spell — {energyType}";
            var shortName = energyType.ToString().ToLower();
            var description = $"You can manipulate the elemental nature of your spells. You may replace a spell’s normal damage with {shortName} damage.\nLevel Increase: +1";
            return CreateMetamagicFeat(metamagic, assetId, friendlyName, description, iconAssetId,
                Helpers.Create<ElementalMetamagic>(e => e.EnergyType = energyType));
        }

        static BlueprintFeature CreateMetamagicFeat(ModMetamagic metamagic, String assetId, String friendlyName, String description, String iconAssetId, BlueprintComponent logic = null, params BlueprintComponent[] extra)
        {
            var components = new List<BlueprintComponent> {
                Helpers.Create<AddMetamagicFeat>(m => m.Metamagic = (Metamagic) metamagic),
                Helpers.Create<RecommendationRequiresSpellbook>()
            };
            if (logic != null) components.Add(logic);
            components.AddRange(extra);
            var feat = Helpers.Create<BlueprintFeature>();
            feat.name = metamagic.ToString() + "SpellFeat";
            feat.Groups = new FeatureGroup[] { FeatureGroup.WizardFeat, FeatureGroup.Feat };
            feat.SetComponents(components);
            friendlyName = friendlyName ?? (metamagic.ToString() + " Spell");
            feat.SetNameDescriptionIcon($"Metamagic ({friendlyName})", description, Helpers.GetIcon(iconAssetId));
            Main.library.AddAsset(feat, assetId);

            Main.SafeLoad(() => CreateMetamagicRods(feat, metamagic, friendlyName, logic), "Metamagic rods");
            return feat;
        }

        static void CreateMetamagicRods(BlueprintFeature feature, ModMetamagic modMetamagic, String friendlyName, BlueprintComponent logic)
        {
            // Create a metamagic rod by cloning an existing one.
            var rodIds = existingRodIds[Metamagic.Empower];
            var rodCosts = metamagicRodCosts[modMetamagic.OriginalCost() - 1];

            var library = Main.library;
            var names = new string[] { "Lesser", "Normal", "Greater" };
            var displayPrefix = new string[] { "Lesser ", "", "Greater " };
            var maxLevel = new string[] { "3rd", "6th", "9th " };

            for (int i = 0; i < 3; i++)
            {
                var displayName = displayPrefix[i] + friendlyName.Replace(" Spell", " Metamagic Rod");
                var description = $"The wielder can cast up to three spells per day that are affected as though the spells were augmented with the {friendlyName} feat.\n" +
                    $"{displayPrefix[i]} rods can be used with spells of {maxLevel[i]} level or lower.\n" +
                    $"{friendlyName}: {feature.Description}";

                // We need to clone 3 things:
                // - the item
                // - the activatable ability
                // - the buff
                var existingRod = library.Get<BlueprintItemEquipmentUsable>(rodIds[i]);
                var existingAbility = existingRod.ActivatableAbility;
                var existingBuff = existingAbility.Buff;

                var newRod = library.CopyAndAdd(existingRod, $"MetamagicRod{names[i]}{modMetamagic}", feature.AssetGuid, existingRod.AssetGuid);
                var newAbility = library.CopyAndAdd(existingAbility, $"{newRod.name}ToggleAbility", feature.AssetGuid, existingAbility.AssetGuid);
                var newBuff = library.CopyAndAdd(existingBuff, $"{newRod.name}Buff", feature.AssetGuid, existingBuff.AssetGuid);
                newRod.ActivatableAbility = newAbility;
                newAbility.Buff = newBuff;
                if (logic != null) newAbility.AddComponent(logic);

                Helpers.SetField(newRod, "m_Cost", rodCosts[i]);
                Helpers.SetField(newRod, "m_DisplayNameText", Helpers.CreateString(newRod.name + ".Name", displayName));
                Helpers.SetField(newRod, "m_DescriptionText", Helpers.CreateString(newRod.name + ".Description", description));

                newAbility.SetNameDescriptionIcon(displayName, description, newAbility.Icon);

                newBuff.SetNameDescriptionIcon(displayName, description, newBuff.Icon);
                newBuff.SetComponents(newBuff.ComponentsArray.Select(c =>
                {
                    var mechanics = c as MetamagicRodMechanics;
                    if (mechanics == null) return c;
                    var newMechanics = UnityEngine.Object.Instantiate(mechanics);
                    newMechanics.Metamagic = (Metamagic)modMetamagic;
                    newMechanics.RodAbility = newAbility;
                    return newMechanics;
                }));

                newRods.PutIfAbsent(GetMetamagicRodKey(i, modMetamagic.OriginalCost()),
                    () => new List<BlueprintItemEquipmentUsable>()).Add(newRod);
            }
        }

        // Both of these values are really small so we can merge them.
        // (level is one of: 0, 1, 2; cost is one of: 1, 2, 3, 4.)
        static int GetMetamagicRodKey(int level, int cost) => (cost << 2) | level;

        static void AddMetamagicRodsToVendors()
        {
            var rodMetmagic = new Dictionary<String, Metamagic>();
            foreach (var rodIds in existingRodIds)
            {
                foreach (var id in rodIds.Value)
                {
                    rodMetmagic[id] = rodIds.Key;
                }
            }

            // Scan through all vendor tables and update them.
            var matchingKeys = new HashSet<int>();
            Helpers.AddNearSimilarLoot((item, count) =>
            {
                if (!rodMetmagic.ContainsKey(item.AssetGuid)) return null;

                var id = item.AssetGuid;
                var metamagic = rodMetmagic[id];
                int key = GetMetamagicRodKey(
                    Array.IndexOf(existingRodIds[metamagic], id),
                    Math.Min(metamagic.OriginalCost(), 3));

                if (!matchingKeys.Add(key)) return null;

                List<BlueprintItemEquipmentUsable> rods;
                newRods.TryGetValue(key, out rods);
                return rods;

            }, (loot) =>
            {
                matchingKeys.Clear();
                return true;
            });
        }

        static readonly Dictionary<int, List<BlueprintItemEquipmentUsable>> newRods = new Dictionary<int, List<BlueprintItemEquipmentUsable>>();

        // Ids for rods for each metamagic: lesser, normal, greater.
        static readonly Dictionary<Metamagic, string[]> existingRodIds = new Dictionary<Metamagic, string[]> {
            { Metamagic.Empower, new string[] { "1e7a5a4d257cf434a87e687c9ee7a872", "a02f06b63af839a448147dadff3724f2", "81d504243708f504dbfe3f8f72efdeda" } },
            { Metamagic.Extend, new string[] { "1cf04842d5dbd0f49946b1af1022cd1a", "1b2a09528da9e9948aa9026037bada90", "9bab0e37c72be78418516e57a5e78a99" } },
            { Metamagic.Maximize, new string[] { "651b0460f600d5f42b0467e7186aab80", "9a511d3b04f08944eb3db4462f88c2c0", "d0b7d29c9bea99d4bb25f8f6a29261c5" } },
            { Metamagic.Quicken, new string[] { "55a059b32df920c4abe65b8ee8b56056", "551dcb2932443c944a6f120048c7d9f7", "843ae85d505be8441b9fbb47b04e19e0" } },
            { Metamagic.Reach, new string[] { "8b0261621069c9a41a70f1aaefa21c75", "648f56fbbaa71624c8ba968ade382ac6", "08942f26792fed84cb28b8e97b2de5c7" } },
        };

        // Metamagic rod costs, first by spell level increase minus one, then by: lesser, normal, greater.
        static readonly int[][] metamagicRodCosts = new int[][] {
            new int[] { 3000, 11000, 24500 },
            new int[] { 9000, 32500, 73000 },
            new int[] { 14000, 54000, 121500 },
        };
    }

    // This is similar to ChangeSpellElementalDamage, but it checks for metamagic first.
    public class ElementalMetamagic : RuleInitiatorLogicComponent<RulePrepareDamage>, IInitiatorRulebookHandler<RuleCastSpell>
    {
        public DamageEnergyType EnergyType;

        public void OnEventAboutToTrigger(RuleCastSpell evt) { }

        public void OnEventDidTrigger(RuleCastSpell evt)
        {
            try
            {
                var context = evt.Context;
                if (context.AbilityBlueprint.Type == AbilityType.Spell &&
                    (context.Params.Metamagic & (Metamagic)ModMetamagic.Elemental) != 0)
                {
                    Log.Write($"{GetType().Name}: apply {EnergyType} energy type to spell {context.AbilityBlueprint.name}");
                    context.RemoveSpellDescriptor(SpellDescriptor.Fire);
                    context.RemoveSpellDescriptor(SpellDescriptor.Cold);
                    context.RemoveSpellDescriptor(SpellDescriptor.Acid);
                    context.RemoveSpellDescriptor(SpellDescriptor.Electricity);
                    context.AddSpellDescriptor(ElementToSpellDescriptor(EnergyType));
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
        public override void OnEventAboutToTrigger(RulePrepareDamage evt)
        {
            try
            {
                var context = Helpers.GetMechanicsContext()?.SourceAbilityContext;
                if (context?.Params != null && context.AbilityBlueprint.Type == AbilityType.Spell &&
                    (context.Params.Metamagic & (Metamagic)ModMetamagic.Elemental) != 0)
                {
                    // Replace the energy type with the desired energy type.
                    Log.Write($"Replace elemental damage with: {EnergyType}");
                    foreach (BaseDamage item in evt.DamageBundle)
                    {
                        (item as EnergyDamage)?.ReplaceEnergy(EnergyType);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public override void OnEventDidTrigger(RulePrepareDamage evt) { }

        private static SpellDescriptor ElementToSpellDescriptor(DamageEnergyType element)
        {
            switch (element)
            {
                case DamageEnergyType.Fire:
                    return SpellDescriptor.Fire;
                case DamageEnergyType.Cold:
                    return SpellDescriptor.Cold;
                case DamageEnergyType.Electricity:
                    return SpellDescriptor.Electricity;
                case DamageEnergyType.Acid:
                    return SpellDescriptor.Acid;
                default:
                    return SpellDescriptor.Fire;
            }
        }
    }

    class DazingMetamagic : RuleInitiatorLogicComponent<RuleDealDamage>
    {
        public BlueprintBuff DazeBuff;

        public override void OnEventAboutToTrigger(RuleDealDamage evt) { }
        public override void OnEventDidTrigger(RuleDealDamage evt)
        {
            try
            {
                var context = Helpers.GetMechanicsContext();
                var spellContext = context?.SourceAbilityContext;
                var target = Helpers.GetTargetWrapper()?.Unit;
                if (spellContext == null || target == null) return;

                var spellbook = spellContext.Ability.Spellbook;
                if (evt.Damage > 0 && context.HasMetamagic((Metamagic)ModMetamagic.Dazing))
                {
                    var savingThrow = context.SavingThrow;
                    if (savingThrow == null)
                    {
                        // Will save to negate the effect
                        savingThrow = new RuleSavingThrow(target, SavingThrowType.Will, context.Params.DC);
                        savingThrow.Reason = context;
                        context.TriggerRule(savingThrow);
                        Log.Write($"Dazing spell -- will saving throw: {savingThrow.Reason} was {savingThrow.D20}");
                    }
                    if (!savingThrow.IsPassed)
                    {
                        var spellLevel = context.Params.SpellLevel;
                        Log.Write($"Dazing spell -- apply daze for {spellLevel} rounds.");
                        Helpers.CreateApplyBuff(DazeBuff, Helpers.CreateContextDuration(spellLevel), fromSpell: true).RunAction();
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    class RimeMetamagic : RuleInitiatorLogicComponent<RuleDealDamage>
    {
        public BlueprintBuff EntangleBuff;
        public override void OnEventAboutToTrigger(RuleDealDamage evt) { }
        public override void OnEventDidTrigger(RuleDealDamage evt)
        {
            try
            {
                var context = Helpers.GetMechanicsContext();
                var spellContext = context?.SourceAbilityContext;
                var target = Helpers.GetTargetWrapper()?.Unit;
                if (spellContext == null || target == null) return;

                if (evt.Damage > 0 && context.HasMetamagic((Metamagic)ModMetamagic.Rime) &&
                    evt.DamageBundle.Any(b => (b as EnergyDamage)?.EnergyType == DamageEnergyType.Cold))
                {
                    var spellLevel = context.Params.SpellLevel;
                    Log.Write($"Rime spell -- apply entangled for {spellLevel} rounds.");
                    Helpers.CreateApplyBuff(EntangleBuff, Helpers.CreateContextDuration(spellLevel), fromSpell: true).RunAction();
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    class TopplingMetamagic : RuleInitiatorLogicComponent<RuleDealDamage>
    {
        public override void OnEventAboutToTrigger(RuleDealDamage evt) { }
        public override void OnEventDidTrigger(RuleDealDamage evt)
        {
            try
            {
                var context = Helpers.GetMechanicsContext();
                var spellContext = context?.SourceAbilityContext;
                var target = Helpers.GetTargetWrapper()?.Unit;
                if (spellContext == null || target == null) return;

                var spellbook = spellContext.Ability.Spellbook;

                // Note: if force spells are added that require saving throws but don't deal damage,
                // then we'll need to listen on another rule.
                if (context.HasMetamagic((Metamagic)ModMetamagic.Toppling) &&
                    evt.DamageBundle.Any(b => b.Type == DamageType.Force) &&
                    (evt.Damage > 0 || context.SavingThrow?.IsPassed == false))
                {
                    Log.Write("Toppling spell -- attempt to trip.");

                    if (!target.Descriptor.State.HasCondition(UnitCondition.ImmuneToCombatManeuvers))
                    {
                        var casterStat = spellbook != null ? spellbook.Blueprint.CastingAttribute : StatType.Charisma;
                        var primaryStat = spellContext.Caster.Stats.GetStat<ModifiableValueAttributeStat>(casterStat);

                        var initiatorCMB = context.Params.CasterLevel + primaryStat.Bonus;
                        var targetCMD = Rulebook.Trigger(new RuleCalculateCMD(spellContext.Caster, target, CombatManeuver.Trip)).Result;
                        var roll = RulebookEvent.Dice.D20;
                        var successValue = initiatorCMB + roll - targetCMD;
                        Log.Write($"  roll {roll} CMB {initiatorCMB} CMD {targetCMD}");
                        if (successValue > 0 && target.CanBeKnockedOff())
                        {
                            Log.Write("  success! tripping");
                            target.Descriptor.State.Prone.ShouldBeActive = true;
                            EventBus.RaiseEvent((IKnockOffHandler h) => h.HandleKnockOff(spellContext.Caster, target));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }


    public class PersistentMetamagic : OwnedGameLogicComponent<UnitDescriptor>, IGlobalRulebookHandler<RuleRollD20>
    {
        public void OnEventAboutToTrigger(RuleRollD20 evt)
        {
            try
            {
                var rule = Rulebook.CurrentContext.PreviousEvent as RuleSavingThrow;
                if (rule == null) return;

                var context = rule.Reason.Context?.SourceAbilityContext;
                if (context?.Caster != Owner.Unit ||
                    !context.HasMetamagic((Metamagic)ModMetamagic.Persistent) ||
                    context.AbilityBlueprint.Type != AbilityType.Spell)
                {
                    return;
                }

                // Note: RuleSavingThrow rolls the D20 before setting the stat bonus, so we need to pass it in.
                // (The game's ModifyD20 component has a bug because of this.)
                var modValue = Owner.Stats.GetStat(rule.StatType);
                var statValue = (modValue as ModifiableValueAttributeStat)?.Bonus ?? modValue.ModifiedValue;
                int roll = evt.PreRollDice();
                if (rule.IsSuccessRoll(roll, statValue - rule.StatValue))
                {
                    Log.Write($"Persistent spell ({context}): roll {roll} passed, force reroll.");
                    evt.SetReroll(1, false);
                }
                else
                {
                    Log.Write($"Persistent spell ({context}): roll {roll} failed, not rerolling.");
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public void OnEventDidTrigger(RuleRollD20 evt)
        {
            if (evt.RerollsAmount > 0)
            {
                Log.Write($"Persistent spell: reroll {evt.Result}.");
            }
        }
    }

    [Harmony12.HarmonyPatch(typeof(ContextRankConfig), "GetValue", new Type[] { typeof(MechanicsContext) })]
    static class ContextRankConfig_GetValue_Patch
    {
        internal static void Apply() => Main.ApplyPatch(typeof(ContextRankConfig_GetValue_Patch),
            "Intensified Spell, Life Oracle enhanced cures, Eldritch Heritage/Crossblood advancement");

        static bool Prefix(ContextRankConfig __instance, MechanicsContext context, ref int __result, bool ___m_UseMax, int ___m_Max, BlueprintCharacterClass[] ___m_Class)
        {
            try
            {
                var self = __instance;
                var baseValue = GetBaseValue(self, context);

                if (self.IsBasedOnClassLevel && ___m_Class.Contains(Helpers.sorcererClass))
                {
                    var part = context.MaybeCaster.Get<UnitPartBloodline>();
                    Log.Write($"ContextRankConfig based on class level {self}, part {part}, blueprint {context.AssociatedBlueprint.name}, {context.AssociatedBlueprint.GetType().Name}");

                    var level = part?.CalcLevel(context.AssociatedBlueprint);
                    if (level.HasValue)
                    {
                        Log.Write($"ContextRankConfig: modify level of {context.AssociatedBlueprint.name}: {level}");
                        baseValue = context.Params.RankBonus + level.Value;
                    }
                }

                var value = (int)ApplyProgression(self, baseValue);

                // TODO: this may allow intensifying something that isn't damage dice.
                // We'd need to scan for ContextActionDealDamage, and then see if its ContextValue matches.
                var spell = context.SourceAbility;
                if (spell != null && ___m_UseMax)
                {
                    if (context.HasMetamagic((Metamagic)ModMetamagic.Intensified))
                    {
                        // Intensified spell: increase the maximum damage dice by 5 levels.
                        int max = ___m_Max;
                        // Figure out what base value results in the max.
                        int maxBaseValue = 1;
                        for (; (int)ApplyProgression(self, maxBaseValue) < max && maxBaseValue <= 20; maxBaseValue++) ;
                        int newMax = (int)ApplyProgression(self, maxBaseValue + 5);
                        __result = Math.Min(value, newMax);
                        Log.Write($"Intensified spell: result {__result}, value {value}, max {max} (reached at level {maxBaseValue}), adjusted max {newMax}");
                        return false;
                    }
                    if ((spell.SpellDescriptor & SpellDescriptor.Cure) != 0 && OracleClass.cureSpells.Value.Contains(spell))
                    {
                        var progressionData = context.MaybeCaster?.Descriptor.Progression;
                        if (progressionData?.Features.HasFact(LifeMystery.enhancedCures) == true)
                        {
                            __result = Math.Min(value, progressionData.GetClassLevel(OracleClass.oracle));
                            Log.Write($"Enhanced cures: result {__result}, value {value}, spell {spell.name}, caster {context.MaybeCaster}");
                            return false;
                        }
                    }
                }
                __result = (int)ApplyMinMax(self, value);
                return false;
            }
            catch (Exception e)
            {
                Log.Error($"caught error in ApplyMinMax {e}");
            }
            return true;
        }

        static readonly FastInvoke GetBaseValue = Helpers.CreateInvoker<ContextRankConfig>("GetBaseValue");
        static readonly FastInvoke ApplyProgression = Helpers.CreateInvoker<ContextRankConfig>("ApplyProgression");
        static readonly FastInvoke ApplyMinMax = Helpers.CreateInvoker<ContextRankConfig>("ApplyMinMax");
    }

    public class SelectiveMetamagic : RuleInitiatorLogicComponent<RuleSpellTargetCheck>
    {
        public override void OnEventAboutToTrigger(RuleSpellTargetCheck evt)
        {
            try
            {
                //Log.Write($"Selective Spell checking target: {evt.Target.CharacterName}, has metamagic? {evt.Context.HasMetamagic((Metamagic)ModMetamagic.Selective)}");
                if (evt.Context.HasMetamagic((Metamagic)ModMetamagic.Selective) && evt.Initiator.IsAlly(evt.Target))
                {
                    Log.Write($"Selective Spell setting target to be immune: {evt.Target.CharacterName}");
                    evt.IsImmune = true;
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public override void OnEventDidTrigger(RuleSpellTargetCheck evt) { }
    }

    [Harmony12.HarmonyPatch(typeof(MetamagicHelper), "DefaultCost", new Type[] { typeof(Metamagic) })]
    static class MetamagicHelper_DefaultCost_Patch
    {
        internal static bool Prefix(Metamagic metamagic, ref int __result)
        {
            switch ((ModMetamagic)metamagic)
            {
                case ModMetamagic.Dazing:
                    __result = 3;
                    return false;
                case ModMetamagic.Persistent:
                    __result = 2;
                    return false;
                case ModMetamagic.Rime:
                case ModMetamagic.Toppling:
                case ModMetamagic.Intensified:
                case ModMetamagic.ElementalFire:
                case ModMetamagic.ElementalCold:
                case ModMetamagic.ElementalElectricity:
                case ModMetamagic.ElementalAcid:
                case ModMetamagic.Selective:
                    __result = 1;
                    return false;
            }
            return true;
        }
    }

    [Harmony12.HarmonyPatch(typeof(AbilityEffectRunAction), "Apply", typeof(AbilityExecutionContext), typeof(TargetWrapper))]
    static class AbilityEffectRunAction_Apply_Patch
    {
        internal static bool Prefix(AbilityExecutionContext context, TargetWrapper target)
        {
            try
            {
                return !target.IsUnit || context.TriggerRule(new RuleSpellTargetCheck(context, target.Unit)).CanTargetUnit;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
            return true;
        }
    }

    [Harmony12.HarmonyPatch(typeof(AbilityEffectRunActionOnClickedTarget), "Apply", typeof(AbilityExecutionContext))]
    static class AbilityEffectRunActionOnClickedTarget_Apply_Patch
    {
        static bool Prefix(AbilityExecutionContext context) => AbilityEffectRunAction_Apply_Patch.Prefix(context, context.MainTarget);
    }

    [Harmony12.HarmonyPatch(typeof(AreaEffectEntityData), "ShouldUnitBeInside", typeof(UnitEntityData))]
    static class AreaEffectEntityData_ShouldUnitBeInside_Patch
    {
        static void Postfix(AreaEffectEntityData __instance, UnitEntityData unit, ref bool __result)
        {
            try
            {
                if (!__result) return;
                var self = __instance;
                var context = self.Context;
                __result = context.TriggerRule(new RuleSpellTargetCheck(context, unit, self)).CanTargetUnit;
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }

    // A very simple rule, designed to make it easier to prevent units from being affected by spells.
    //
    // This is similar to RuleSpellResistanceCheck, but fires under all conditions of a unit being
    // targeted by an ability (unlike SR, where a caster can't resist their own spell, or spells with
    // SR: no). This allows handling various things such as:
    // - excluding targets from Selective Spell.
    // - when the caster does not have "line of effect" to the target (such as force effects).
    // - prevents spells cast during Time Stop from affecting other targets.
    //
    // This rule is also fires for AoEs (see `AreaEffect`).
    public class RuleSpellTargetCheck : RulebookTargetEvent
    {
        public readonly MechanicsContext Context;

        /// The area effect, if this check is from one, otherwise null.
        public readonly AreaEffectEntityData AreaEffect;

        public bool CanTargetUnit { get; private set; }

        public bool IsImmune { get; set; }

        public RuleSpellTargetCheck(MechanicsContext context, UnitEntityData target, AreaEffectEntityData areaEffect = null)
            : base(context.MaybeCaster, target)
        {
            Context = context;
            AreaEffect = areaEffect;
        }

        public override void OnTrigger(RulebookEventContext context)
        {
            CanTargetUnit = !IsImmune;
            //Log.Write($"RuleSpellTargetCheck: caster {Initiator} can target {Target}? {CanTargetUnit}");
        }

        internal static void ApplyPatch()
        {
            Main.ApplyPatch(typeof(AbilityEffectRunAction_Apply_Patch), "Selective Spell, and similar effect that prevent units being targeted by most spells");
            Main.ApplyPatch(typeof(AbilityEffectRunActionOnClickedTarget_Apply_Patch), "Selective Spell for a few AoE spells immediate effects (Ice/Volcanic Storm, Obsidian Flow)");
            Main.ApplyPatch(typeof(AreaEffectEntityData_ShouldUnitBeInside_Patch), "Selective Spell for AoE areas (clouds, ground effect, burning, etc)");
        }
    }
}