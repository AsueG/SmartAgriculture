using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 6 - weather driven priorities.
    /// JobGiver_Work walks the pawn's work givers in priority order and asks PawnCanUseWorkGiver for
    /// each one. Declining the outdoor givers while the outside is lethal makes the pawn fall through
    /// to the next entry in its own work list, which in practice means indoor work - no priority
    /// numbers are rewritten and nothing is persisted, so removing the mod changes nothing.
    /// Caveat: a throttled work type is paused everywhere, including inside a greenhouse; each work
    /// type is individually opt-out in the mod settings.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_PawnCanUseWorkGiver
    {
        private static MethodBase Resolve() => AccessTools.Method(typeof(JobGiver_Work), "PawnCanUseWorkGiver");

        private static bool Prepare()
        {
            if (Resolve() == null)
            {
                Log.Error("[SACL] JobGiver_Work.PawnCanUseWorkGiver not found - module 6 stays idle.");
                return false;
            }
            return true;
        }

        private static MethodBase TargetMethod() => Resolve();

        private static void Postfix(Pawn pawn, WorkGiver giver, ref bool __result)
        {
            if (!__result || !SmartAgricultureMod.Settings.moduleWeatherPriorities)
            {
                return;
            }
            if (pawn == null || pawn.Map == null || pawn.Drafted || pawn.Faction != Faction.OfPlayer)
            {
                return;
            }
            if (!HazardWatcher.OutdoorWorkIsHazardous(pawn.Map))
            {
                return;
            }
            if (HazardWatcher.IsThrottledWorkType(giver?.def?.workType))
            {
                __result = false;
            }
        }
    }
}
