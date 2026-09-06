using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Every plant list in this mod comes from here: the DefDatabase is queried at runtime so
    /// crops added by Vanilla Plants Expanded, VGP, etc. show up without any patch or hard reference.
    /// </summary>
    public static class PlantChoices
    {
        private static List<ThingDef> allSowable;
        private static List<ThingDef> cropYields;

        /// <summary>
        /// Growth window per crop, as a 12 bit mask of twelfths, for the tile we last asked about.
        /// GenTemperature.TwelfthsInAverageTemperatureRange costs 12 x 120 season temperature samples
        /// and allocates a list on every call, and the answer only depends on the tile and on the
        /// crop's optimal range - so it is worth computing once per crop per tile.
        /// </summary>
        private static readonly Dictionary<ThingDef, int> growthWindows = new Dictionary<ThingDef, int>();
        private static PlanetTile growthWindowsTile = PlanetTile.Invalid;

        public static List<ThingDef> AllSowable
        {
            get
            {
                if (allSowable == null)
                {
                    allSowable = new List<ThingDef>();
                    foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
                    {
                        if (def.plant != null && def.plant.Sowable)
                        {
                            allSowable.Add(def);
                        }
                    }
                    allSowable.SortBy(d => d.label);
                }
                return allSowable;
            }
        }

        /// <summary>Plants the colony can actually sow in this zone right now (research + terrain rules).</summary>
        public static IEnumerable<ThingDef> SowableIn(Zone_Growing zone)
        {
            foreach (ThingDef def in AllSowable)
            {
                if (!ResearchDone(def))
                {
                    continue;
                }
                if (!PlantUtility.CanSowOnGrower(def, zone))
                {
                    continue;
                }
                yield return def;
            }
        }

        public static bool ResearchDone(ThingDef plant)
        {
            List<ResearchProjectDef> prereqs = plant.plant.sowResearchPrerequisites;
            if (prereqs == null)
            {
                return true;
            }
            for (int i = 0; i < prereqs.Count; i++)
            {
                if (!prereqs[i].IsFinished)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Crop for a "random" rotation stage: sowable here, able to grow at the current outdoor
        /// temperature, and preferably able to reach maturity before the growing window closes.
        /// Returns null when nothing suits the season, which leaves the field bare until it does.
        /// </summary>
        public static ThingDef RandomForSeason(Map map, Zone_Growing zone, ThingDef exclude = null)
        {
            if (map == null || zone == null || zone.cells.Count == 0)
            {
                return null;
            }

            IntVec3 cell = zone.cells[0];
            List<ThingDef> canMature = new List<ThingDef>();
            List<ThingDef> growable = new List<ThingDef>();

            foreach (ThingDef def in SowableIn(zone))
            {
                // A tree would hold the rotation for years, since the next stage waits for a bare field.
                if (def == exclude || def.plant.IsTree || !PlantUtility.GrowthSeasonNow(cell, map, def))
                {
                    continue;
                }
                growable.Add(def);
                if (def.plant.growDays <= GrowingDaysLeft(map, def))
                {
                    canMature.Add(def);
                }
            }

            if (canMature.Count > 0)
            {
                return canMature.RandomElement();
            }
            return growable.Count > 0 ? growable.RandomElement() : null;
        }

        /// <summary>Days of usable outdoor growing weather left for this plant, counting from today.</summary>
        private static float GrowingDaysLeft(Map map, ThingDef plant)
        {
            if (map.IsPocketMap || !map.Tile.Valid)
            {
                return float.MaxValue;
            }

            int window = GrowthWindow(map, plant);
            if (window == 0)
            {
                return 0f;
            }
            if (window == 0xFFF)
            {
                return float.MaxValue;
            }

            Twelfth current = GenLocalDate.Twelfth(map);
            if (!InWindow(window, current))
            {
                return 0f;
            }

            int run = 0;
            while (run < 12 && InWindow(window, current))
            {
                run++;
                current = TwelfthUtility.NextTwelfth(current);
            }
            return run * 5f - GenLocalDate.DayOfTwelfth(map);
        }

        private static bool InWindow(int mask, Twelfth twelfth) => (mask & (1 << (int)twelfth)) != 0;

        private static int GrowthWindow(Map map, ThingDef plant)
        {
            if (map.Tile != growthWindowsTile)
            {
                growthWindows.Clear();
                growthWindowsTile = map.Tile;
            }
            if (growthWindows.TryGetValue(plant, out int mask))
            {
                return mask;
            }

            List<Twelfth> twelfths = GenTemperature.TwelfthsInAverageTemperatureRange(
                map.Tile, plant.plant.minOptimalGrowthTemperature, plant.plant.maxOptimalGrowthTemperature);
            mask = 0;
            if (twelfths != null)
            {
                for (int i = 0; i < twelfths.Count; i++)
                {
                    mask |= 1 << (int)twelfths[i];
                }
            }
            growthWindows[plant] = mask;
            return mask;
        }

        /// <summary>
        /// Everything a field can actually put in a crate: the yield of every sowable plant. Trees are
        /// left out on purpose - their yield is wood, and a crate that accepts wood turns into a
        /// hauling destination for the colony's whole woodpile.
        /// </summary>
        public static List<ThingDef> CropYields
        {
            get
            {
                if (cropYields == null)
                {
                    cropYields = new List<ThingDef>();
                    foreach (ThingDef def in AllSowable)
                    {
                        ThingDef yield = def.plant.harvestedThingDef;
                        if (yield != null && !def.plant.IsTree && !cropYields.Contains(yield))
                        {
                            cropYields.Add(yield);
                        }
                    }
                }
                return cropYields;
            }
        }

        public static ThingDef YieldOf(ThingDef plant) => plant?.plant?.harvestedThingDef;

        public static string YieldLabel(ThingDef plant)
        {
            ThingDef yield = YieldOf(plant);
            return yield != null ? yield.label : "SACL.NoYield".Translate().ToString();
        }
    }
}
