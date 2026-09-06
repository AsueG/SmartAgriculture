using Verse;

namespace SmartAgriculture
{
    public class SmartAgricultureSettings : ModSettings
    {
        public bool moduleRotation = true;
        public bool moduleStock = true;
        public bool moduleSeasonal = true;
        public bool moduleGreenhouse = true;
        public bool moduleFieldCrates = true;
        public bool moduleWeatherPriorities = true;
        public bool modulePantryFirst = true;
        public bool moduleSoilRest = true;

        public bool defaultStockEnabled = true;
        public int defaultStockHigh = 1200;
        public int defaultStockLow = 900;
        public int defaultFallowDays = 3;

        /// <summary>Fertility banked per rested day. The cap is reached in cap/this days.</summary>
        public float soilRestPerDay = 0.04f;

        /// <summary>
        /// Fertility spent per day of growth. Deliberately slower than the accrual: fertility only
        /// scales growth rate, and vanilla fertilitySensitivity of 0.5 turns a full +0.4 reserve into
        /// no more than +20% speed, so a reserve that drained as fast as it filled would repay a fallow
        /// with about one day of growth for ten days of rest - invisible. Draining slower spreads the
        /// bonus over several harvests instead, which is what makes resting worth doing.
        /// </summary>
        public float soilRestDrainPerDay = 0.02f;

        /// <summary>Ceiling on the banked reserve. 0.4 on top of plain soil's 1.0 matches rich soil.</summary>
        public float soilRestCap = 0.4f;

        public float solarHeatPerCellPerSecond = 0.6f;
        public float greenhouseMaxDelta = 25f;
        public bool hideFallbackSkylightWhenModded = true;

        public float crateSearchRadius = 8f;

        public float hazardHeatThreshold = 42f;
        public float hazardColdThreshold = -25f;
        public bool hazardBlocksGrowing = true;
        public bool hazardBlocksPlantCutting = true;
        public bool hazardBlocksMining = true;

        public bool pantryCookingOnly = true;
        public int pantryHorizonDays = 4;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref moduleRotation, "moduleRotation", true);
            Scribe_Values.Look(ref moduleStock, "moduleStock", true);
            Scribe_Values.Look(ref moduleSeasonal, "moduleSeasonal", true);
            Scribe_Values.Look(ref moduleGreenhouse, "moduleGreenhouse", true);
            Scribe_Values.Look(ref moduleFieldCrates, "moduleFieldCrates", true);
            Scribe_Values.Look(ref moduleWeatherPriorities, "moduleWeatherPriorities", true);
            Scribe_Values.Look(ref modulePantryFirst, "modulePantryFirst", true);
            Scribe_Values.Look(ref moduleSoilRest, "moduleSoilRest", true);

            Scribe_Values.Look(ref defaultStockEnabled, "defaultStockEnabled", true);
            Scribe_Values.Look(ref defaultStockHigh, "defaultStockHigh", 1200);
            Scribe_Values.Look(ref defaultStockLow, "defaultStockLow", 900);
            Scribe_Values.Look(ref defaultFallowDays, "defaultFallowDays", 3);
            Scribe_Values.Look(ref soilRestPerDay, "soilRestPerDay", 0.04f);
            Scribe_Values.Look(ref soilRestDrainPerDay, "soilRestDrainPerDay", 0.02f);
            Scribe_Values.Look(ref soilRestCap, "soilRestCap", 0.4f);

            Scribe_Values.Look(ref solarHeatPerCellPerSecond, "solarHeatPerCellPerSecond", 0.6f);
            Scribe_Values.Look(ref greenhouseMaxDelta, "greenhouseMaxDelta", 25f);
            Scribe_Values.Look(ref hideFallbackSkylightWhenModded, "hideFallbackSkylightWhenModded", true);

            Scribe_Values.Look(ref crateSearchRadius, "crateSearchRadius", 8f);

            Scribe_Values.Look(ref hazardHeatThreshold, "hazardHeatThreshold", 42f);
            Scribe_Values.Look(ref hazardColdThreshold, "hazardColdThreshold", -25f);
            Scribe_Values.Look(ref hazardBlocksGrowing, "hazardBlocksGrowing", true);
            Scribe_Values.Look(ref hazardBlocksPlantCutting, "hazardBlocksPlantCutting", true);
            Scribe_Values.Look(ref hazardBlocksMining, "hazardBlocksMining", true);

            Scribe_Values.Look(ref pantryCookingOnly, "pantryCookingOnly", true);
            Scribe_Values.Look(ref pantryHorizonDays, "pantryHorizonDays", 4);
        }
    }
}
