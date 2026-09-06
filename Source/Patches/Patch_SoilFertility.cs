using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 8 - soil rest.
    /// FertilityGrid.CalculateFertilityAt is the single choke point every fertility read goes through:
    /// FertilityAt just forwards to it, and nothing is cached, so a Postfix here reaches plant growth,
    /// the sowing check and the fertility overlay alike. CalculateFertilityAt is patched rather than
    /// FertilityAt because the latter is a one-line forward and small enough to be inlined away.
    /// The reserve is only ever added, never subtracted, so no field can drop below fertilityMin.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_SoilFertility
    {
        private static AccessTools.FieldRef<FertilityGrid, Map> mapOf;

        private static MethodBase Resolve() => AccessTools.Method(typeof(FertilityGrid), "CalculateFertilityAt");

        private static bool Prepare()
        {
            if (Resolve() == null)
            {
                Log.Error("[SACL] FertilityGrid.CalculateFertilityAt not found - module 8 stays idle.");
                return false;
            }
            FieldInfo field = AccessTools.Field(typeof(FertilityGrid), "map");
            if (field == null)
            {
                Log.Error("[SACL] FertilityGrid.map not found - module 8 stays idle.");
                return false;
            }
            mapOf = AccessTools.FieldRefAccess<FertilityGrid, Map>(field);
            return true;
        }

        private static MethodBase TargetMethod() => Resolve();

        private static void Postfix(FertilityGrid __instance, IntVec3 loc, ref float __result)
        {
            // Rock and water read 0: a rested field must never turn unfarmable ground into farmland.
            if (__result <= 0f || !SmartAgricultureMod.Settings.moduleSoilRest)
            {
                return;
            }

            Map map = mapOf(__instance);
            SoilRestSystem rest = SmartAgricultureMapComponent.For(map)?.SoilRest;
            if (rest == null || rest.Empty)
            {
                return;
            }
            float reserve = rest.ReserveAt(loc);
            if (reserve <= 0f)
            {
                return;
            }

            // Only reached for the handful of cells that hold a reserve. Hydroponics publish their own
            // fertility through the edifice: the soil underneath is not what the crop is rooted in.
            Thing edifice = loc.GetEdifice(map);
            if (edifice != null && edifice.def.AffectsFertility)
            {
                return;
            }
            __result += reserve;
        }
    }
}
