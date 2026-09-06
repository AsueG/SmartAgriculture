using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    public enum RotationPhase
    {
        /// <summary>Sowing the current stage; the resolver hands out the stage crop.</summary>
        Sowing,

        /// <summary>Waiting for the whole field to be harvested; nothing gets sown, cut cells stay bare.</summary>
        Ripening,

        /// <summary>Deliberately bare for a number of days.</summary>
        Fallow
    }

    public enum SeasonMode
    {
        /// <summary>Leave the season alone: rotation (if any) or the zone's own plant applies.</summary>
        Inherit,

        /// <summary>Grow one fixed crop all season.</summary>
        FixedCrop,

        /// <summary>Run the rotation list during this season.</summary>
        Rotation,

        /// <summary>Sow nothing at all this season.</summary>
        Fallow,

        /// <summary>
        /// Draw one crop that suits the weather and grow it all season. Appended last on purpose:
        /// Scribe_Values stores the enum by name, so inserting it here cannot shift a saved value.
        /// </summary>
        Random
    }

    public enum StageKind
    {
        /// <summary>Sow the crop held in <see cref="RotationStage.plant"/>.</summary>
        Crop,

        /// <summary>Roll a crop that suits the weather each time the stage comes round.</summary>
        Random,

        /// <summary>Leave the field bare for a number of days.</summary>
        Fallow
    }

    public enum FallbackMode
    {
        /// <summary>Sow nothing at all while the store is full.</summary>
        None,

        /// <summary>Sow the crop held in <see cref="FieldPlan.fallbackPlant"/>.</summary>
        Crop,

        /// <summary>Roll a crop that suits the weather each time the store fills up.</summary>
        Random
    }

    public class RotationStage : IExposable
    {
        public StageKind kind = StageKind.Crop;

        /// <summary>On a random stage this holds the crop rolled for the current cycle, not a fixed choice.</summary>
        public ThingDef plant;
        public int fallowDays = 3;

        /// <summary>
        /// Let the length follow the soil's needs: recomputed to whatever tops the field's fertility
        /// reserve back up to the cap each time the stage comes round. New stages start on auto; the
        /// save default is deliberately the opposite, so a rotation written before this existed keeps
        /// the length its player typed instead of having it silently recomputed on load.
        /// </summary>
        public bool fallowAuto = true;

        public RotationStage()
        {
        }

        public RotationStage(ThingDef plant)
        {
            this.plant = plant;
        }

        /// <summary>True once this stage can actually be acted on; an empty crop stage is discarded.</summary>
        public bool Usable => kind != StageKind.Crop || plant != null;

        public string Label
        {
            get
            {
                switch (kind)
                {
                    case StageKind.Fallow:
                        return "SACL.FallowStage".Translate(fallowDays).ToString();
                    case StageKind.Random:
                        return plant != null
                            ? "SACL.RandomStageRolled".Translate(plant.LabelCap).ToString()
                            : "SACL.RandomStage".Translate().ToString();
                    default:
                        return plant != null ? plant.LabelCap.ToString() : "SACL.NoCrop".Translate().ToString();
                }
            }
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref plant, "plant");
            Scribe_Values.Look(ref kind, "kind", StageKind.Crop);
            Scribe_Values.Look(ref fallowDays, "fallowDays", 3);
            Scribe_Values.Look(ref fallowAuto, "fallowAuto", false);

            if (Scribe.mode == LoadSaveMode.LoadingVars && kind == StageKind.Crop)
            {
                // Saves written while the stage kind was still two separate booleans. Without this the
                // fallow and random stages of an existing rotation would come back as empty crop
                // stages and get dropped by FieldPlan.ExposeData. A genuine crop stage has both
                // booleans absent, so it reads back as Crop either way.
                bool fallow = false;
                bool random = false;
                Scribe_Values.Look(ref fallow, "fallow", false);
                Scribe_Values.Look(ref random, "random", false);
                kind = fallow ? StageKind.Fallow : (random ? StageKind.Random : StageKind.Crop);
            }
        }
    }

    public class SeasonSlot : IExposable
    {
        public SeasonMode mode = SeasonMode.Inherit;

        /// <summary>
        /// The crop to grow. In FixedCrop mode the player picks it; in Random mode it holds the crop
        /// that was drawn for the current year, exactly as RotationStage.plant holds a rolled stage.
        /// </summary>
        public ThingDef plant;

        public void ExposeData()
        {
            Scribe_Values.Look(ref mode, "mode", SeasonMode.Inherit);
            Scribe_Defs.Look(ref plant, "plant");
        }
    }

    /// <summary>
    /// All the metadata this mod attaches to a grow zone. It lives in our own MapComponent, keyed by
    /// Zone.ID, so vanilla Zone_Growing is never subclassed or modified and saves stay loadable
    /// without the mod.
    /// </summary>
    public class FieldPlan : IExposable
    {
        /// <summary>Save node per season slot, in the order they sit in the array.</summary>
        private static readonly string[] SeasonNodeNames = { "spring", "summer", "fall", "winter" };

        public int zoneId;

        public bool rotationEnabled;
        public List<RotationStage> rotation = new List<RotationStage>();
        public int rotationIndex;
        public RotationPhase phase = RotationPhase.Sowing;
        public int fallowTicksLeft;

        /// <summary>
        /// Plants of the current stage still standing, as counted by the last rotation tick.
        /// The status line is drawn every UI frame, so it reads this instead of rescanning the field.
        /// </summary>
        public int aliveCount;

        public bool seasonalEnabled;
        private SeasonSlot[] seasons = { new SeasonSlot(), new SeasonSlot(), new SeasonSlot(), new SeasonSlot() };

        /// <summary>
        /// Season the seasonal plan last resolved in, so a Random season knows to draw a fresh crop
        /// when its season comes round again instead of keeping the one it rolled last year.
        /// </summary>
        public Season lastSeason = Season.Undefined;

        public bool stockEnabled;
        public int stockHigh = 1200;
        public int stockLow = 900;
        public FallbackMode fallbackMode = FallbackMode.None;

        /// <summary>On a random fallback this holds the crop rolled for the current suspension.</summary>
        public ThingDef fallbackPlant;
        public bool stockSuspended;

        public FieldPlan()
        {
        }

        public FieldPlan(int zoneId)
        {
            this.zoneId = zoneId;
            stockEnabled = SmartAgricultureMod.Settings.defaultStockEnabled;
            stockHigh = SmartAgricultureMod.Settings.defaultStockHigh;
            stockLow = SmartAgricultureMod.Settings.defaultStockLow;
        }

        public bool AnythingConfigured => rotationEnabled || seasonalEnabled || stockEnabled;

        public bool RotationUsable => rotationEnabled && rotation.Count >= 2;

        public RotationStage CurrentStage
        {
            get
            {
                if (rotation.Count == 0)
                {
                    return null;
                }
                if (rotationIndex < 0 || rotationIndex >= rotation.Count)
                {
                    rotationIndex = 0;
                }
                return rotation[rotationIndex];
            }
        }

        public string FallbackLabel
        {
            get
            {
                switch (fallbackMode)
                {
                    case FallbackMode.Crop:
                        return fallbackPlant != null
                            ? fallbackPlant.LabelCap.ToString()
                            : "SACL.PickCrop".Translate().ToString();
                    case FallbackMode.Random:
                        return fallbackPlant != null
                            ? "SACL.RandomStageRolled".Translate(fallbackPlant.LabelCap).ToString()
                            : "SACL.StockFallbackRandom".Translate().ToString();
                    default:
                        return "SACL.StockFallbackNone".Translate().ToString();
                }
            }
        }

        public SeasonSlot SlotFor(Season season) => seasons[SlotIndexOf(season)];

        /// <summary>
        /// Which of the four slots drives a season. Public so the window can tell whether a tab is the
        /// one in charge right now: on an equatorial or polar tile the game reports PermanentSummer or
        /// PermanentWinter, which no tab is labelled with, so seasons cannot be compared directly.
        /// </summary>
        public static int SlotIndexOf(Season season)
        {
            switch (season)
            {
                case Season.Spring:
                    return 0;
                case Season.Fall:
                    return 2;
                case Season.Winter:
                case Season.PermanentWinter:
                    return 3;
                default:
                    return 1;
            }
        }

        public void AdvanceStage(Map map, Zone_Growing zone)
        {
            if (rotation.Count == 0)
            {
                phase = RotationPhase.Sowing;
                return;
            }
            rotationIndex = (rotationIndex + 1) % rotation.Count;
            EnterStage(map, zone);
        }

        public void EnterStage(Map map, Zone_Growing zone)
        {
            RotationStage stage = CurrentStage;
            if (stage == null)
            {
                phase = RotationPhase.Sowing;
                fallowTicksLeft = 0;
                return;
            }

            if (stage.kind == StageKind.Random)
            {
                // Re-rolled every time the stage comes round, so the same slot varies year to year.
                stage.plant = PlantChoices.RandomForSeason(map, zone);
            }

            if (stage.kind == StageKind.Fallow && stage.fallowAuto)
            {
                stage.fallowDays = SuggestFallowDays(map, zone);
            }

            aliveCount = 0;
            if (stage.kind == StageKind.Fallow || (stage.kind == StageKind.Crop && stage.plant == null))
            {
                phase = RotationPhase.Fallow;
                fallowTicksLeft = (stage.kind == StageKind.Fallow ? stage.fallowDays : 0) * GenDate.TicksPerDay;
            }
            else
            {
                phase = RotationPhase.Sowing;
                fallowTicksLeft = 0;
            }
        }

        /// <summary>
        /// Rest long enough to fill this field's fertility reserve. Falls back to the settings default
        /// when soil rest is off, since with no reserve to fill there is nothing to compute from.
        /// </summary>
        public static int SuggestFallowDays(Map map, Zone_Growing zone)
        {
            if (SmartAgricultureMod.Settings.moduleSoilRest)
            {
                SoilRestSystem rest = SmartAgricultureMapComponent.For(map)?.SoilRest;
                if (rest != null)
                {
                    return rest.SuggestedFallowDays(zone);
                }
            }
            return SmartAgricultureMod.Settings.defaultFallowDays;
        }

        public void ResetRotation(Map map, Zone_Growing zone)
        {
            rotationIndex = 0;
            EnterStage(map, zone);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref zoneId, "zoneId", 0);

            Scribe_Values.Look(ref rotationEnabled, "rotationEnabled", false);
            Scribe_Collections.Look(ref rotation, "rotation", LookMode.Deep);
            Scribe_Values.Look(ref rotationIndex, "rotationIndex", 0);
            Scribe_Values.Look(ref phase, "phase", RotationPhase.Sowing);
            Scribe_Values.Look(ref fallowTicksLeft, "fallowTicksLeft", 0);
            Scribe_Values.Look(ref aliveCount, "aliveCount", 0);

            Scribe_Values.Look(ref seasonalEnabled, "seasonalEnabled", false);
            for (int i = 0; i < seasons.Length; i++)
            {
                Scribe_Deep.Look(ref seasons[i], SeasonNodeNames[i]);
            }
            Scribe_Values.Look(ref lastSeason, "lastSeason", Season.Undefined);

            Scribe_Values.Look(ref stockEnabled, "stockEnabled", false);
            Scribe_Values.Look(ref stockHigh, "stockHigh", 1200);
            Scribe_Values.Look(ref stockLow, "stockLow", 900);
            Scribe_Values.Look(ref fallbackMode, "fallbackMode", FallbackMode.None);
            Scribe_Defs.Look(ref fallbackPlant, "fallbackPlant");
            Scribe_Values.Look(ref stockSuspended, "stockSuspended", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (rotation == null)
                {
                    rotation = new List<RotationStage>();
                }
                rotation.RemoveAll(st => st == null || !st.Usable);

                // Saves written before the fallback had a mode: a crop was stored on its own.
                if (fallbackMode == FallbackMode.None && fallbackPlant != null)
                {
                    fallbackMode = FallbackMode.Crop;
                }
                for (int i = 0; i < seasons.Length; i++)
                {
                    if (seasons[i] == null)
                    {
                        seasons[i] = new SeasonSlot();
                    }
                }
            }
        }
    }
}
