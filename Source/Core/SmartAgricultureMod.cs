using HarmonyLib;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    public class SmartAgricultureMod : Mod
    {
        public const string HarmonyId = "sacl.smartagriculture";

        public static SmartAgricultureSettings Settings { get; private set; }

        private Vector2 scrollPos;
        /// <summary>
        /// Deliberately taller than the whole list can ever be. Listing_Standard.Begin clips to the rect
        /// it is given, so on the very first frame - before CurHeight is known - a guess that is too
        /// short does not just hide the last modules, it drops them out of the scroll range entirely.
        /// </summary>
        private float lastContentHeight = 1800f;

        public SmartAgricultureMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<SmartAgricultureSettings>();
            new Harmony(HarmonyId).PatchAll();
        }

        public override string SettingsCategory() => "SACL.ModTitle".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            SmartAgricultureSettings s = Settings;
            // Never shorter than the viewport: Listing_Standard.Begin opens a GUI group, which clips,
            // so a height guess that is too small would hide the modules at the bottom of the list.
            Rect viewRect = new Rect(0f, 0f, inRect.width - 20f, Mathf.Max(lastContentHeight, inRect.height));
            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);

            Listing_Standard list = new Listing_Standard();
            list.Begin(viewRect);

            list.Label("SACL.SettingsHint".Translate());
            list.GapLine();

            list.CheckboxLabeled("SACL.Module1".Translate(), ref s.moduleRotation, "SACL.Module1Desc".Translate());
            if (s.moduleRotation)
            {
                s.defaultFallowDays = (int)list.SliderLabeled("SACL.DefaultFallowDays".Translate(s.defaultFallowDays), s.defaultFallowDays, 0f, 15f);
            }
            list.Gap(6f);

            list.CheckboxLabeled("SACL.Module2".Translate(), ref s.moduleStock, "SACL.Module2Desc".Translate());
            if (s.moduleStock)
            {
                list.CheckboxLabeled("SACL.DefaultStockEnabled".Translate(), ref s.defaultStockEnabled, "SACL.DefaultStockEnabledDesc".Translate());
                s.defaultStockHigh = (int)list.SliderLabeled("SACL.DefaultStockHigh".Translate(s.defaultStockHigh), s.defaultStockHigh, 50f, 20000f);
                s.defaultStockLow = (int)list.SliderLabeled("SACL.DefaultStockLow".Translate(s.defaultStockLow), s.defaultStockLow, 0f, s.defaultStockHigh);
            }
            list.Gap(6f);

            list.CheckboxLabeled("SACL.Module3".Translate(), ref s.moduleSeasonal, "SACL.Module3Desc".Translate());
            list.Gap(6f);

            list.CheckboxLabeled("SACL.Module4".Translate(), ref s.moduleGreenhouse, "SACL.Module4Desc".Translate());
            if (s.moduleGreenhouse)
            {
                s.solarHeatPerCellPerSecond = list.SliderLabeled("SACL.SolarGain".Translate(s.solarHeatPerCellPerSecond.ToString("0.00")), s.solarHeatPerCellPerSecond, 0.05f, 3f);
                s.greenhouseMaxDelta = list.SliderLabeled("SACL.GreenhouseMaxDelta".Translate(s.greenhouseMaxDelta.ToString("0")), s.greenhouseMaxDelta, 2f, 60f);
                list.CheckboxLabeled("SACL.HideFallbackSkylight".Translate(), ref s.hideFallbackSkylightWhenModded, "SACL.HideFallbackSkylightDesc".Translate());
                list.Label("SACL.GreenhouseCompatStatus".Translate(ModCompat.TransparentRoofSourceLabel));
            }
            list.Gap(6f);

            list.CheckboxLabeled("SACL.Module5".Translate(), ref s.moduleFieldCrates, "SACL.Module5Desc".Translate());
            if (s.moduleFieldCrates)
            {
                s.crateSearchRadius = list.SliderLabeled("SACL.CrateRadius".Translate(s.crateSearchRadius.ToString("0")), s.crateSearchRadius, 2f, 20f);
            }
            list.Gap(6f);

            list.CheckboxLabeled("SACL.Module6".Translate(), ref s.moduleWeatherPriorities, "SACL.Module6Desc".Translate());
            if (s.moduleWeatherPriorities)
            {
                s.hazardHeatThreshold = list.SliderLabeled("SACL.HazardHeat".Translate(s.hazardHeatThreshold.ToString("0")), s.hazardHeatThreshold, 25f, 60f);
                s.hazardColdThreshold = list.SliderLabeled("SACL.HazardCold".Translate(s.hazardColdThreshold.ToString("0")), s.hazardColdThreshold, -60f, 5f);
                list.CheckboxLabeled("SACL.HazardGrowing".Translate(), ref s.hazardBlocksGrowing);
                list.CheckboxLabeled("SACL.HazardPlantCutting".Translate(), ref s.hazardBlocksPlantCutting);
                list.CheckboxLabeled("SACL.HazardMining".Translate(), ref s.hazardBlocksMining);
            }
            list.Gap(6f);

            list.CheckboxLabeled("SACL.Module7".Translate(), ref s.modulePantryFirst, "SACL.Module7Desc".Translate());
            if (s.modulePantryFirst)
            {
                list.CheckboxLabeled("SACL.PantryCookingOnly".Translate(), ref s.pantryCookingOnly, "SACL.PantryCookingOnlyDesc".Translate());
                s.pantryHorizonDays = (int)list.SliderLabeled("SACL.PantryHorizon".Translate(s.pantryHorizonDays), s.pantryHorizonDays, 1f, 20f);
            }
            list.Gap(6f);

            list.CheckboxLabeled("SACL.Module8".Translate(), ref s.moduleSoilRest, "SACL.Module8Desc".Translate());
            if (s.moduleSoilRest)
            {
                // Rounded to whole hundredths so the day count below is a stable number to read.
                s.soilRestCap = Round2(list.SliderLabeled("SACL.SoilRestCap".Translate(s.soilRestCap.ToString("0.00")), s.soilRestCap, 0.05f, 1f));
                s.soilRestPerDay = Round2(list.SliderLabeled("SACL.SoilRestPerDay".Translate(s.soilRestPerDay.ToString("0.00")), s.soilRestPerDay, 0.01f, 0.2f));
                s.soilRestDrainPerDay = Round2(list.SliderLabeled("SACL.SoilRestDrain".Translate(s.soilRestDrainPerDay.ToString("0.00")), s.soilRestDrainPerDay, 0.01f, 0.2f));
                GUI.color = Color.gray;
                list.Label("SACL.SoilRestHint".Translate(
                    Mathf.CeilToInt(s.soilRestCap / Mathf.Max(0.01f, s.soilRestPerDay)),
                    Mathf.CeilToInt(s.soilRestCap / Mathf.Max(0.01f, s.soilRestDrainPerDay))));
                GUI.color = Color.white;
            }

            lastContentHeight = list.CurHeight + 24f;
            list.End();
            Widgets.EndScrollView();
        }

        private static float Round2(float value) => Mathf.Round(value * 100f) / 100f;
    }
}
