using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components.Base;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Utility;
using Newtonsoft.Json;
using static Kingmaker.UnitLogic.Commands.Base.UnitCommand;

namespace EldritchArcana
{
    static class DismissSpell
    {
        internal static void Load()
        {
            EventBus.Subscribe(new AreaEffectDismissal());
        }
    }

    // Provides an option to dismiss dismissible area effects.
    class AreaEffectDismissal : IAreaEffectHandler
    {
        readonly BlueprintAbility dismiss;

        public AreaEffectDismissal()
        {
            dismiss = Helpers.CreateAbility("DismissAreaEffectSpell", "Dismiss Area Effect Spell",
                "Some spells can be dismissed at will, others when you are out of combat. You must be within range of the spell’s effect. Dismissing a spell is a standard action that does not provoke attacks of opportunity.\n" +
                "A spell that depends on concentration is dismissible by its very nature, and dismissing it does not take an action, since all you have to do to end the spell is to stop concentrating on your turn.\n" +
                "(If this ability is enabled, it means you have a currently active effect spell that can be dismissed.)",
                "da09c33c33d44be485c5757262923bfb",
                Helpers.GetIcon("95f7cdcec94e293489a85afdf5af1fd7"), // dismissal
                AbilityType.Extraordinary, // (Ex) so it doesn't provoke an attack of opportunity, and works w/ antimagic field.
                CommandType.Standard,
                AbilityRange.Long, // Note: should be the spell range, but this should be good enough.
                "", "",
                Helpers.Create<DismissAreaEffectLogic>(),
                Helpers.CreateRunActions(Helpers.Create<DismissAreaEffectAction>()));
            dismiss.CanTargetPoint = true;
        }

        public void HandleAreaEffectDestroyed(AreaEffectEntityData areaEffect)
        {
            Log.Write($"HandleAreaEffectDestroyed({areaEffect.Blueprint.name})");
            var caster = areaEffect.Context.MaybeCaster;
            if (caster?.IsPlayerFaction == true &&
                DismissAreaEffectLogic.GetCasterAreaEffects(caster).All(a => a == areaEffect))
            {
                caster.Descriptor.RemoveFact(dismiss);
            }
        }

        public void HandleAreaEffectSpawned(AreaEffectEntityData areaEffect)
        {
            Log.Write($"HandleAreaEffectSpawned({areaEffect.Blueprint.name})");
            var caster = areaEffect.Context.MaybeCaster;
            if (caster?.IsPlayerFaction == true && DismissAreaEffectLogic.IsAreaEffectSpell(areaEffect) &&
                !caster.Descriptor.HasFact(dismiss))
            {
                caster.Descriptor.AddFact(dismiss);
            }
        }
    }

    public class DismissAreaEffectLogic : GameLogicComponent, IAbilityTargetChecker, IAbilityAvailabilityProvider
    {
        public bool CanTarget(UnitEntityData caster, TargetWrapper target) => GetTargetAreaEffect(caster, target) != null;

        public bool IsAvailableFor(AbilityData ability)
        {
            var caster = ability.Caster.Unit;
            foreach (var area in GetCasterAreaEffects(caster))
            {
                if (CanDismiss(caster, area)) return true;
            }
            return false;
        }

        public string GetReason() => $"No area effects to dismiss (only certain spells can be dismissed in combat).";

        internal static void EndTargetAreaEffect(UnitEntityData caster, TargetWrapper target)
        {
            var area = GetTargetAreaEffect(caster, target);
            if (area == null) return;

            string buffId;
            if (dismissibleAreaBuffs.TryGetValue(area.Blueprint.AssetGuid, out buffId))
            {
                caster.Buffs.RemoveFact(Main.library.Get<BlueprintBuff>(buffId));
            }
            else
            {
                area.ForceEnd();
            }
        }

        internal static AreaEffectEntityData GetTargetAreaEffect(UnitEntityData caster, TargetWrapper target)
        {
            foreach (var area in GetCasterAreaEffects(caster))
            {
                if (area.View.Shape.Contains(target.Point) && CanDismiss(caster, area)) return area;
            }
            return null;
        }

        internal static IEnumerable<AreaEffectEntityData> GetCasterAreaEffects(UnitEntityData caster)
        {
            return Game.Instance.State.AreaEffects.Where(area => area.Context.MaybeCaster == caster && IsAreaEffectSpell(area));
        }

        internal static bool IsAreaEffectSpell(AreaEffectEntityData area)
        {
            return area.Blueprint.AffectEnemies && area.Context.SourceAbility?.Type == AbilityType.Spell;
        }

        internal static bool CanDismiss(UnitEntityData caster, AreaEffectEntityData area) =>
            !caster.IsInCombat || dismissibleAreas.Contains(area.Blueprint.AssetGuid);

        static readonly HashSet<string> dismissibleAreas = new HashSet<string> {
            "cae4347a512809e4388fb3949dc0bc67", // Blade Barrier
            "6c116b31887c6284fbd41c070f6422f6", // Cloak of Dreams
            "bcb6329cefc66da41b011299a43cc681", // Entangle
            "d46313be45054b248a1f1656ddb38614", // Grease
            "4c695315962bf9a4ea7fc7e2bb3e2f60", // Ice Storm
            "6b2b1ba6ec6487f46b8c76b603abba6b", // Ice Storm (shadow)
            FireSpells.incendiaryCloudAreaId,   // Incendiary Cloud
            "e09010a73354a794293ebc7b33c2d130", // Obscuring Mist
            "b21bc337e2beaa74b8823570cd45d6dd", // Sirocco
            "bb87c7513a16b9a44b4948a4e932a81b", // Sirocco (shadow)
            "16e0e4c6a16f68c49832340b93706499", // Spike Growth
            "1d649d8859b25024888966ba1cc291d1", // Volcanic Storm
            "1f45c8b0a735097439a9dac04f5b0161", // Volcanic Storm (shadow)
            "fd323c05f76390749a8555b13156813d", // Web
        };

        static readonly Dictionary<String, String> dismissibleAreaBuffs = new Dictionary<String, String>
        {
            ["6c116b31887c6284fbd41c070f6422f6"] = "2e4b85213927f0a4ea2198e0f2a6028b" // Cloak of Dreams
        };
    }

    public class DismissAreaEffectAction : ContextAction
    {
        public override string GetCaption() => "Dismiss caster's area effect spell near target";

        public override void RunAction()
        {
            var context = Context.SourceAbilityContext;
            if (context == null) return;

            DismissAreaEffectLogic.EndTargetAreaEffect(context.Caster, Target);
        }
    }
}