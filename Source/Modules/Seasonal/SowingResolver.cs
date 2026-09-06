using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Single decision point for "what should this zone sow right now".
    /// Layer order: season plan (module 3) selects the regime, rotation (module 1) picks the crop
    /// inside that regime, stock thresholds (module 2) can downgrade it to a secondary crop or to
    /// nothing at all. Returning null simply makes WorkGiver_GrowerSow skip the zone, exactly like
    /// an empty "plant to grow" would - no vanilla behaviour is bypassed.
    /// </summary>
    public static class SowingResolver
    {
        public static ThingDef Resolve(Map map, Zone_Growing zone, FieldPlan plan, ThingDef vanillaChoice)
        {
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            ThingDef wanted = vanillaChoice;
            bool rotationRunning = s.moduleRotation && plan.RotationUsable;

            if (s.moduleSeasonal && plan.seasonalEnabled)
            {
                Season season = GenLocalDate.Season(map);
                SeasonSlot slot = plan.SlotFor(season);
                if (plan.lastSeason != season)
                {
                    plan.lastSeason = season;
                    if (slot.mode == SeasonMode.Random)
                    {
                        // Season just turned, so the crop this slot rolled last year is stale.
                        slot.plant = null;
                    }
                }

                switch (slot.mode)
                {
                    case SeasonMode.Fallow:
                        return null;
                    case SeasonMode.FixedCrop:
                        wanted = slot.plant;
                        rotationRunning = false;
                        break;
                    case SeasonMode.Random:
                        // Rolled once and kept for the season, the same way a random rotation stage
                        // keeps its crop for the whole lap - a field that re-rolled every check would
                        // sow a different plant on every cell.
                        if (slot.plant == null)
                        {
                            slot.plant = PlantChoices.RandomForSeason(map, zone);
                        }
                        wanted = slot.plant;
                        rotationRunning = false;
                        break;
                    case SeasonMode.Rotation:
                        // Unlike Inherit, this season is driven by the rotation and nothing else: with
                        // no usable rotation the field stays bare instead of falling back to the crop
                        // set on the zone. The window warns about that in yellow.
                        if (!rotationRunning)
                        {
                            return null;
                        }
                        break;
                }
            }

            if (rotationRunning)
            {
                wanted = RotationController.PlantForSowing(plan);
            }

            if (s.moduleStock && plan.stockEnabled)
            {
                wanted = StockGate.Gate(map, plan, wanted);
            }

            return wanted;
        }
    }
}
