using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 2 - stock triggered farming.
    /// Reads Map.resourceCounter (already maintained by the game, refreshed roughly once per second)
    /// instead of walking the map, and uses a low/high hysteresis so the zone does not flip-flop.
    /// </summary>
    public static class StockGate
    {
        /// <summary>Runs on the map component tick so the sowing resolver itself stays side effect free.</summary>
        public static void UpdateHysteresis(Map map, Zone_Growing zone, FieldPlan plan)
        {
            ThingDef watched = WatchedYield(zone, plan);
            if (watched == null)
            {
                plan.stockSuspended = false;
                return;
            }
            int count = map.resourceCounter.GetCount(watched);
            if (plan.stockSuspended)
            {
                if (count <= plan.stockLow)
                {
                    plan.stockSuspended = false;
                    if (plan.fallbackMode == FallbackMode.Random)
                    {
                        // Dropped so the next time the store fills up the roll is made afresh.
                        plan.fallbackPlant = null;
                    }
                }
            }
            else if (count >= plan.stockHigh)
            {
                plan.stockSuspended = true;
            }

            // Rolled here rather than in Gate, because the resolver must stay side effect free.
            if (plan.stockSuspended && plan.fallbackMode == FallbackMode.Random && plan.fallbackPlant == null)
            {
                plan.fallbackPlant = PlantChoices.RandomForSeason(map, zone, PrimaryPlant(zone, plan));
            }
        }

        /// <summary>Applies the gate to whatever crop the rotation/season layer asked for.</summary>
        public static ThingDef Gate(Map map, FieldPlan plan, ThingDef wanted)
        {
            if (wanted == null || !plan.stockSuspended)
            {
                return wanted;
            }
            if (plan.fallbackMode == FallbackMode.None)
            {
                return null;
            }

            ThingDef fallback = plan.fallbackPlant;
            if (fallback == null || fallback == wanted)
            {
                return null;
            }
            // Falling back onto something we are equally overstocked on would defeat the point.
            ThingDef fallbackYield = PlantChoices.YieldOf(fallback);
            if (fallbackYield != null && map.resourceCounter.GetCount(fallbackYield) >= plan.stockHigh)
            {
                return null;
            }
            return fallback;
        }

        /// <summary>The crop this zone is really growing right now: the rotation's, else the zone's own.</summary>
        public static ThingDef PrimaryPlant(Zone_Growing zone, FieldPlan plan)
        {
            ThingDef primary = null;
            if (SmartAgricultureMod.Settings.moduleRotation && plan.RotationUsable)
            {
                RotationStage stage = plan.CurrentStage;
                if (stage != null && stage.kind != StageKind.Fallow)
                {
                    primary = stage.plant;
                }
            }
            return primary ?? zone.GetPlantDefToGrow();
        }

        /// <summary>The resource whose stock drives this zone: the primary crop's yield.</summary>
        public static ThingDef WatchedYield(Zone_Growing zone, FieldPlan plan)
            => PlantChoices.YieldOf(PrimaryPlant(zone, plan));

        public static int CurrentStock(Map map, ThingDef yieldDef) => yieldDef == null ? 0 : map.resourceCounter.GetCount(yieldDef);
    }
}
