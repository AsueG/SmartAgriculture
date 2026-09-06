using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    /// <summary>Appends the field plan gizmo to the vanilla grow zone gizmo row.</summary>
    [HarmonyPatch(typeof(Zone_Growing), nameof(Zone_Growing.GetGizmos))]
    internal static class Patch_ZoneGrowingGizmos
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, Zone_Growing __instance)
        {
            foreach (Gizmo gizmo in values)
            {
                yield return gizmo;
            }

            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            if (!s.moduleRotation && !s.moduleStock && !s.moduleSeasonal)
            {
                yield break;
            }
            if (__instance.Map == null)
            {
                yield break;
            }

            SmartAgricultureMapComponent comp = SmartAgricultureMapComponent.For(__instance.Map);
            if (comp == null)
            {
                yield break;
            }

            FieldPlan plan = comp.GetPlan(__instance);
            yield return new Command_Action
            {
                defaultLabel = "SACL.FieldPlanGizmo".Translate(),
                defaultDesc = plan != null && plan.AnythingConfigured
                    ? "SACL.FieldPlanGizmoDescActive".Translate(RotationController.StatusLine(plan))
                    : "SACL.FieldPlanGizmoDesc".Translate(),
                icon = SaclTex.FieldPlan,
                action = () => Find.WindowStack.Add(new Dialog_FieldPlan(__instance, comp.GetOrCreatePlan(__instance)))
            };
        }
    }
}
