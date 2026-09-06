using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 6 - weather driven priorities.
    /// The expensive part (reading conditions and temperature) runs once per map every 250 ticks;
    /// the Harmony hook only reads a cached bool, so job scanning stays cheap.
    /// </summary>
    public class HazardWatcher
    {
        private readonly Map map;

        private string hazardReason;

        public HazardWatcher(Map map)
        {
            this.map = map;
        }

        /// <summary>Read from the work giver hook, which runs for every pawn and every giver.</summary>
        public static bool OutdoorWorkIsHazardous(Map map)
        {
            HazardWatcher watcher = SmartAgricultureMapComponent.For(map)?.Hazard;
            return watcher != null && watcher.hazardReason != null;
        }

        public void Recheck()
        {
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            if (!s.moduleWeatherPriorities)
            {
                hazardReason = null;
                return;
            }

            string reason = null;
            float outdoor = map.mapTemperature.OutdoorTemp;

            if (outdoor >= s.hazardHeatThreshold)
            {
                reason = "SACL.HazardReasonHeat".Translate(outdoor.ToStringTemperature("F0"));
            }
            else if (outdoor <= s.hazardColdThreshold)
            {
                reason = "SACL.HazardReasonCold".Translate(outdoor.ToStringTemperature("F0"));
            }
            else if (ConditionActive(GameConditionDefOf.ToxicFallout))
            {
                reason = GameConditionDefOf.ToxicFallout.LabelCap;
            }
            else if (ConditionActive(GameConditionDefOf.NoxiousHaze))
            {
                reason = GameConditionDefOf.NoxiousHaze.LabelCap;
            }
            else if (ConditionActive(GameConditionDefOf.BloodRain))
            {
                reason = GameConditionDefOf.BloodRain.LabelCap;
            }
            else if (ConditionActive(GameConditionDefOf.Flashstorm))
            {
                reason = GameConditionDefOf.Flashstorm.LabelCap;
            }
            else if (ConditionActive(GameConditionDefOf.LavaFlow))
            {
                reason = GameConditionDefOf.LavaFlow.LabelCap;
            }

            hazardReason = reason;
        }

        private bool ConditionActive(GameConditionDef def) => def != null && map.gameConditionManager.ConditionIsActive(def);

        /// <summary>Work types that get pushed back while the outside is lethal.</summary>
        public static bool IsThrottledWorkType(WorkTypeDef workType)
        {
            if (workType == null)
            {
                return false;
            }
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            if (s.hazardBlocksGrowing && workType == WorkTypeDefOf.Growing)
            {
                return true;
            }
            if (s.hazardBlocksPlantCutting && workType == WorkTypeDefOf.PlantCutting)
            {
                return true;
            }
            if (s.hazardBlocksMining && workType == WorkTypeDefOf.Mining)
            {
                return true;
            }
            return false;
        }
    }
}
