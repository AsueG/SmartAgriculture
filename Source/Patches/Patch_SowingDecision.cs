using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Modules 1, 2 and 3 all hook the same vanilla decision: WorkGiver_Grower.CalculateWantedPlantDef.
    /// This is a Postfix only - the vanilla answer is computed first and we may substitute it.
    /// Returning null is a state vanilla already handles (WorkGiver_GrowerSow.ExtraRequirements
    /// simply skips the zone), and WorkGiver_GrowerHarvest keeps harvesting mature plants because it
    /// only compares against this def when the zone forbids cutting.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_CalculateWantedPlantDef
    {
        private static MethodBase Resolve() => AccessTools.Method(typeof(WorkGiver_Grower), nameof(WorkGiver_Grower.CalculateWantedPlantDef));

        private static bool Prepare()
        {
            if (Resolve() == null)
            {
                Log.Error("[SACL] WorkGiver_Grower.CalculateWantedPlantDef not found - modules 1/2/3 stay idle.");
                return false;
            }
            return true;
        }

        private static MethodBase TargetMethod() => Resolve();

        private static void Postfix(IntVec3 c, Map map, ref ThingDef __result)
        {
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            if (map == null || (!s.moduleRotation && !s.moduleStock && !s.moduleSeasonal))
            {
                return;
            }
            SmartAgricultureMapComponent comp = SmartAgricultureMapComponent.For(map);
            if (comp == null || !comp.HasPlans)
            {
                return;
            }
            if (!(c.GetZone(map) is Zone_Growing zone))
            {
                return;
            }
            FieldPlan plan = comp.GetPlan(zone);
            if (plan == null || !plan.AnythingConfigured)
            {
                return;
            }
            __result = SowingResolver.Resolve(map, zone, plan, __result);
        }
    }
}
