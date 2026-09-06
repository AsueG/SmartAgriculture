using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Configuration window opened from the grow zone gizmo: rotation list, one tab per season and
    /// the stock thresholds. Every plant list is built from PlantChoices, so modded crops appear
    /// automatically.
    /// </summary>
    public class Dialog_FieldPlan : Window
    {
        private const float RowHeight = 30f;
        private const float TabHeight = 32f;
        private const int MaxStages = 4;

        /// <summary>Room kept under the stage list for the add button, the status lines and the restart button.</summary>
        private const float FooterHeight = 126f;

        private readonly Zone_Growing zone;
        private readonly FieldPlan plan;
        private readonly List<TabRecord> tabs = new List<TabRecord>();

        /// <summary>
        /// Edit buffer per fallow stage. TextFieldNumeric displays the buffer rather than the value, so
        /// rebuilding it every frame would refill the field the instant the player cleared it.
        /// </summary>
        private readonly Dictionary<RotationStage, string> fallowDayBuffers = new Dictionary<RotationStage, string>();

        private Tab tab = Tab.Rotation;
        private string highBuffer;
        private string lowBuffer;
        private Vector2 scroll = Vector2.zero;

        /// <summary>Marks the season the colony is actually in. Not yellow: Draw uses yellow for hover.</summary>
        private static readonly Color CurrentSeasonColor = new Color(0.55f, 0.9f, 0.6f);

        private enum Tab
        {
            Rotation,
            Spring,
            Summer,
            Fall,
            Winter,
            Stock
        }

        /// <summary>
        /// TabRecord carries no tooltip field; GetTip is the hook TabDrawer calls for one. Subclassing
        /// a pure drawing helper touches nothing that is ever saved.
        /// </summary>
        private sealed class TipTabRecord : TabRecord
        {
            private readonly string tip;

            public TipTabRecord(string label, Action clickedAction, bool selected, string tip)
                : base(label, clickedAction, selected)
            {
                this.tip = tip;
            }

            public override string GetTip() => tip;
        }

        public Dialog_FieldPlan(Zone_Growing zone, FieldPlan plan)
        {
            this.zone = zone;
            this.plan = plan;
            doCloseX = true;
            draggable = true;
            resizeable = true;
            forcePause = false;
            absorbInputAroundWindow = false;
            closeOnClickedOutside = false;
            onlyOneOfTypeAllowed = true;
            optionalTitle = "SACL.FieldPlanTitle".Translate(zone.label);
        }

        public override Vector2 InitialSize => new Vector2(660f, 560f);

        public override void DoWindowContents(Rect inRect)
        {
            if (zone.Map == null || zone.Cells.Count == 0)
            {
                Close();
                return;
            }

            Rect body = new Rect(inRect.x, inRect.y + TabHeight, inRect.width, inRect.height - TabHeight);
            EnsureTabAvailable();
            BuildTabs();
            if (tabs.Count == 0)
            {
                Widgets.Label(body.ContractedBy(12f), "SACL.AllModulesOff".Translate());
                return;
            }
            TabDrawer.DrawTabs(body, tabs);

            Rect content = body.ContractedBy(12f);
            switch (tab)
            {
                case Tab.Rotation:
                    DrawRotationTab(content);
                    break;
                case Tab.Stock:
                    DrawStockTab(content);
                    break;
                default:
                    DrawSeasonTab(content, SeasonOfTab(tab), plan.SlotFor(SeasonOfTab(tab)));
                    break;
            }
        }

        private void BuildTabs()
        {
            tabs.Clear();
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            if (s.moduleRotation)
            {
                tabs.Add(new TabRecord("SACL.TabRotation".Translate(), () => tab = Tab.Rotation, tab == Tab.Rotation));
            }
            if (s.moduleSeasonal)
            {
                AddSeasonTab(Tab.Spring, Season.Spring);
                AddSeasonTab(Tab.Summer, Season.Summer);
                AddSeasonTab(Tab.Fall, Season.Fall);
                AddSeasonTab(Tab.Winter, Season.Winter);
            }
            if (s.moduleStock)
            {
                tabs.Add(new TabRecord("SACL.TabStock".Translate(), () => tab = Tab.Stock, tab == Tab.Stock));
            }
        }

        /// <summary>Modules can be toggled while this window is open, so the active tab may vanish.</summary>
        private void EnsureTabAvailable()
        {
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            bool available = tab == Tab.Rotation ? s.moduleRotation : (tab == Tab.Stock ? s.moduleStock : s.moduleSeasonal);
            if (available)
            {
                return;
            }
            if (s.moduleRotation)
            {
                tab = Tab.Rotation;
            }
            else if (s.moduleSeasonal)
            {
                tab = Tab.Spring;
            }
            else if (s.moduleStock)
            {
                tab = Tab.Stock;
            }
        }

        private void AddSeasonTab(Tab which, Season season)
        {
            bool current = IsCurrentSeason(season);
            string tip = (current ? "SACL.SeasonTabTipCurrent" : "SACL.SeasonTabTip").Translate(season.LabelCap());
            TabRecord record = new TipTabRecord(season.LabelCap(), () => tab = which, tab == which, tip);
            if (current)
            {
                record.labelColor = CurrentSeasonColor;
            }
            tabs.Add(record);
        }

        /// <summary>
        /// Whether that tab is the one steering the field right now. Goes through the slot mapping
        /// rather than comparing seasons: an equatorial tile reports Season.PermanentSummer, which is
        /// driven by the summer tab but is not equal to Season.Summer.
        /// </summary>
        private bool IsCurrentSeason(Season season)
        {
            return FieldPlan.SlotIndexOf(GenLocalDate.Season(zone.Map)) == FieldPlan.SlotIndexOf(season);
        }

        private static Season SeasonOfTab(Tab t)
        {
            switch (t)
            {
                case Tab.Spring:
                    return Season.Spring;
                case Tab.Fall:
                    return Season.Fall;
                case Tab.Winter:
                    return Season.Winter;
                default:
                    return Season.Summer;
            }
        }

        // ---------------------------------------------------------------- rotation

        private void DrawRotationTab(Rect rect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);
            bool wasEnabled = plan.rotationEnabled;
            list.CheckboxLabeled("SACL.RotationEnable".Translate(), ref plan.rotationEnabled, "SACL.RotationEnableDesc".Translate());
            if (plan.rotationEnabled && !wasEnabled && plan.rotation.Count == 0)
            {
                PrefillRotation();
            }
            list.Gap(6f);

            if (!plan.rotationEnabled)
            {
                GUI.color = Color.gray;
                list.Label("SACL.RotationDisabledHint".Translate());
                GUI.color = Color.white;
                list.End();
                return;
            }

            list.Label("SACL.RotationStagesHeader".Translate(plan.rotation.Count, MaxStages));

            // Everything stays inside the one listing: Begin opens a GUI group, so a rect handed out
            // by GetRect is group-local and would land in the wrong place if used after End.
            float listHeight = rect.height - list.CurHeight - FooterHeight;
            DrawStageRows(list.GetRect(Mathf.Max(RowHeight, listHeight)));
            list.Gap(6f);

            if (plan.rotation.Count < MaxStages && list.ButtonText("SACL.RotationAddStage".Translate()))
            {
                OpenStageMenu(null);
            }
            if (plan.RotationUsable)
            {
                list.Label(RotationController.StatusLine(plan));
                if (list.ButtonText("SACL.RotationRestart".Translate()))
                {
                    plan.ResetRotation(zone.Map, zone);
                }
            }
            else
            {
                GUI.color = Color.gray;
                list.Label("SACL.RotationNeedsTwo".Translate());
                GUI.color = Color.white;
            }
            DrawSoilReserve(list);
            list.End();
        }

        /// <summary>What the fallow steps have actually bought this field, in plain fertility points.</summary>
        private void DrawSoilReserve(Listing_Standard list)
        {
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            if (!s.moduleSoilRest)
            {
                return;
            }
            SoilRestSystem rest = SmartAgricultureMapComponent.For(zone.Map)?.SoilRest;
            if (rest == null)
            {
                return;
            }

            float reserve = rest.AverageReserve(zone);
            Rect row = list.GetRect(22f);
            GUI.color = reserve > 0f ? Color.white : Color.gray;
            Widgets.Label(row, "SACL.SoilReserve".Translate(
                reserve.ToString("0.00"),
                s.soilRestCap.ToString("0.00"),
                (s.soilRestCap > 0f ? Mathf.Clamp01(reserve / s.soilRestCap) : 0f).ToStringPercent()));
            GUI.color = Color.white;
            TooltipHandler.TipRegion(row, "SACL.SoilReserveDesc".Translate());
        }

        private void DrawStageRows(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(4f);
            float viewHeight = plan.rotation.Count * (RowHeight + 4f);
            Rect view = new Rect(0f, 0f, inner.width - 16f, viewHeight);
            Widgets.BeginScrollView(inner, ref scroll, view);

            float y = 0f;
            for (int i = 0; i < plan.rotation.Count; i++)
            {
                RotationStage stage = plan.rotation[i];
                Rect row = new Rect(0f, y, view.width, RowHeight);
                if (i == plan.rotationIndex && plan.RotationUsable)
                {
                    Widgets.DrawHighlight(row);
                }

                Rect numRect = new Rect(row.x, row.y, 26f, row.height);
                Widgets.Label(numRect, (i + 1).ToString() + ".");

                Rect pickRect = new Rect(numRect.xMax, row.y, row.width * 0.45f, row.height);
                if (Widgets.ButtonText(pickRect, stage.Label))
                {
                    OpenStageMenu(i);
                }

                float x = pickRect.xMax + 6f;
                if (stage.kind == StageKind.Fallow)
                {
                    Rect labelRect = new Rect(x, row.y, 42f, row.height);
                    Widgets.Label(labelRect, "SACL.Days".Translate());
                    TooltipHandler.TipRegion(labelRect, FallowTooltip("SACL.DaysDesc"));
                    Rect fieldRect = new Rect(labelRect.xMax, row.y + 3f, 52f, row.height - 6f);
                    if (stage.fallowAuto)
                    {
                        // The computed length is shown rather than edited, so the number the rotation
                        // will actually use is never a mystery.
                        GUI.color = Color.gray;
                        Widgets.Label(fieldRect, stage.fallowDays.ToString());
                        GUI.color = Color.white;
                    }
                    else
                    {
                        int days = stage.fallowDays;
                        fallowDayBuffers.TryGetValue(stage, out string buffer);
                        Widgets.TextFieldNumeric(fieldRect, ref days, ref buffer, 1f, 60f);
                        fallowDayBuffers[stage] = buffer;
                        stage.fallowDays = days;
                    }

                    Rect autoRect = new Rect(fieldRect.xMax + 6f, row.y, 92f, row.height);
                    bool auto = stage.fallowAuto;
                    Widgets.CheckboxLabeled(autoRect, "SACL.FallowAuto".Translate(), ref auto);
                    TooltipHandler.TipRegion(autoRect, "SACL.FallowAutoDesc".Translate());
                    if (auto != stage.fallowAuto)
                    {
                        stage.fallowAuto = auto;
                        fallowDayBuffers.Remove(stage);
                        if (auto)
                        {
                            stage.fallowDays = FieldPlan.SuggestFallowDays(zone.Map, zone);
                        }
                    }
                    x = autoRect.xMax + 6f;
                }
                else if (stage.plant != null)
                {
                    Rect yieldRect = new Rect(x, row.y, row.width - x - 34f, row.height);
                    GUI.color = Color.gray;
                    Widgets.Label(yieldRect, "SACL.Yields".Translate(PlantChoices.YieldLabel(stage.plant)));
                    GUI.color = Color.white;
                }

                Rect delRect = new Rect(row.xMax - 26f, row.y + 3f, 24f, 24f);
                if (Widgets.ButtonImage(delRect, TexButton.Delete))
                {
                    RotationStage running = plan.CurrentStage;
                    fallowDayBuffers.Remove(stage);
                    plan.rotation.RemoveAt(i);

                    // Deleting another row must not switch the field to a different stage: follow the
                    // running one to its new position, and only re-enter when it was the one removed.
                    int moved = plan.rotation.IndexOf(running);
                    if (moved >= 0)
                    {
                        plan.rotationIndex = moved;
                    }
                    else
                    {
                        if (plan.rotationIndex >= plan.rotation.Count)
                        {
                            plan.rotationIndex = 0;
                        }
                        plan.EnterStage(zone.Map, zone);
                    }
                    Widgets.EndScrollView();
                    return;
                }

                y += RowHeight + 4f;
            }

            Widgets.EndScrollView();
        }

        /// <summary>
        /// With soil rest switched off a fallow really is just a wait, so the tooltips must not promise
        /// an effect the player has turned off. Each key has a "Bare" variant that says so.
        /// </summary>
        private static string FallowTooltip(string key)
        {
            return SmartAgricultureMod.Settings.moduleSoilRest
                ? key.Translate().ToString()
                : (key + "Bare").Translate().ToString();
        }

        /// <summary>index null = append a new stage.</summary>
        private void OpenStageMenu(int? index)
        {
            RotationStage existing = index.HasValue ? plan.rotation[index.Value] : null;
            List<Dialog_PickCrop.Extra> extras = new List<Dialog_PickCrop.Extra>
            {
                new Dialog_PickCrop.Extra
                {
                    label = "SACL.FallowOption".Translate(),
                    tooltip = FallowTooltip("SACL.FallowOptionDesc"),
                    action = () => SetStage(index, StageKind.Fallow, null)
                },
                new Dialog_PickCrop.Extra
                {
                    label = "SACL.RandomOption".Translate(),
                    tooltip = "SACL.RandomOptionDesc".Translate(),
                    action = () => SetStage(index, StageKind.Random, null)
                }
            };

            OpenCropPicker("SACL.PickCropForStage".Translate(), existing?.plant, extras,
                picked => SetStage(index, StageKind.Crop, picked));
        }

        /// <summary>index null = append a new stage instead of rewriting one.</summary>
        private void SetStage(int? index, StageKind kind, ThingDef plant)
        {
            RotationStage running = plan.CurrentStage;
            RotationStage stage = index.HasValue && index.Value < plan.rotation.Count
                ? plan.rotation[index.Value]
                : Append();
            stage.kind = kind;
            stage.plant = plant;
            if (kind == StageKind.Fallow)
            {
                // Filled in straight away so the row never shows a length the rotation will not use.
                if (stage.fallowAuto)
                {
                    stage.fallowDays = FieldPlan.SuggestFallowDays(zone.Map, zone);
                }
                else if (stage.fallowDays <= 0)
                {
                    stage.fallowDays = SmartAgricultureMod.Settings.defaultFallowDays;
                }
            }

            // Only the stage the field is actually on may be re-entered: EnterStage re-rolls a random
            // stage's crop and resets its phase, so doing it after editing any other row would
            // reshuffle the running stage under the player's nose.
            if (stage == running || plan.CurrentStage != running)
            {
                plan.EnterStage(zone.Map, zone);
            }
        }

        /// <summary>Shared entry point so all three crop choices in this window look the same.</summary>
        private void OpenCropPicker(string title, ThingDef current, List<Dialog_PickCrop.Extra> extras, Action<ThingDef> onPick)
        {
            List<ThingDef> choices = new List<ThingDef>(PlantChoices.SowableIn(zone));
            if (choices.Count == 0 && extras.NullOrEmpty())
            {
                Messages.Message("SACL.NoSowablePlants".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            Find.WindowStack.Add(new Dialog_PickCrop(title, choices, current, onPick, extras));
        }

        /// <summary>
        /// Turning rotation on used to leave an empty list behind the "needs at least two stages"
        /// warning. Start from the crop the zone already grows, then a random step and a fallow break,
        /// so the feature is usable and self-explanatory from the first click.
        /// </summary>
        private void PrefillRotation()
        {
            ThingDef currentCrop = zone.GetPlantDefToGrow();
            if (currentCrop != null && currentCrop.plant != null && !currentCrop.plant.IsTree)
            {
                plan.rotation.Add(new RotationStage { plant = currentCrop });
            }
            plan.rotation.Add(new RotationStage { kind = StageKind.Random });
            plan.rotation.Add(new RotationStage
            {
                kind = StageKind.Fallow,
                fallowDays = FieldPlan.SuggestFallowDays(zone.Map, zone)
            });
            plan.rotationIndex = 0;
            plan.EnterStage(zone.Map, zone);
        }

        private RotationStage Append()
        {
            RotationStage stage = new RotationStage { fallowDays = SmartAgricultureMod.Settings.defaultFallowDays };
            plan.rotation.Add(stage);
            return stage;
        }

        // ---------------------------------------------------------------- seasons

        private void DrawSeasonTab(Rect rect, Season season, SeasonSlot slot)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);
            list.CheckboxLabeled("SACL.SeasonalEnable".Translate(), ref plan.seasonalEnabled, "SACL.SeasonalEnableDesc".Translate());
            list.GapLine(8f);

            if (!plan.seasonalEnabled)
            {
                GUI.color = Color.gray;
                list.Label("SACL.SeasonalDisabledHint".Translate());
                GUI.color = Color.white;
                list.End();
                return;
            }

            list.Label("SACL.SeasonHeader".Translate(season.LabelCap()));
            if (IsCurrentSeason(season))
            {
                // Spells out what the coloured tab label means, since a tab cannot carry that text.
                GUI.color = CurrentSeasonColor;
                list.Label("SACL.SeasonCurrent".Translate());
                GUI.color = Color.white;
            }
            list.Gap(4f);

            DrawModeRadio(list, slot, SeasonMode.Inherit, "SACL.ModeInherit", "SACL.ModeInheritDesc");
            DrawModeRadio(list, slot, SeasonMode.FixedCrop, "SACL.ModeFixed", "SACL.ModeFixedDesc");
            DrawModeRadio(list, slot, SeasonMode.Random, "SACL.ModeRandom", "SACL.ModeRandomDesc");
            DrawModeRadio(list, slot, SeasonMode.Rotation, "SACL.ModeRotation", "SACL.ModeRotationDesc");
            DrawModeRadio(list, slot, SeasonMode.Fallow, "SACL.ModeFallow", "SACL.ModeFallowDesc");

            if (slot.mode == SeasonMode.Random)
            {
                list.Gap(8f);
                GUI.color = Color.gray;
                list.Label(slot.plant != null
                    ? "SACL.RandomStageRolled".Translate(slot.plant.LabelCap)
                    : "SACL.PhaseRandomWaiting".Translate());
                GUI.color = Color.white;
            }
            else if (slot.mode == SeasonMode.FixedCrop)
            {
                list.Gap(8f);
                string label = slot.plant != null ? slot.plant.LabelCap.ToString() : "SACL.PickCrop".Translate().ToString();
                if (list.ButtonText(label))
                {
                    OpenSeasonCropMenu(slot);
                }
                if (slot.plant != null)
                {
                    GUI.color = Color.gray;
                    list.Label("SACL.Yields".Translate(PlantChoices.YieldLabel(slot.plant)));
                    GUI.color = Color.white;
                }
            }
            else if (slot.mode == SeasonMode.Rotation && !plan.RotationUsable)
            {
                list.Gap(8f);
                GUI.color = Color.yellow;
                list.Label("SACL.RotationNeedsTwo".Translate());
                GUI.color = Color.white;
            }

            list.End();
        }

        private static void DrawModeRadio(Listing_Standard list, SeasonSlot slot, SeasonMode mode, string labelKey, string descKey)
        {
            Rect row = list.GetRect(28f);
            if (Widgets.RadioButtonLabeled(row, labelKey.Translate(), slot.mode == mode))
            {
                slot.mode = mode;
            }
            TooltipHandler.TipRegion(row, descKey.Translate());
        }

        private void OpenSeasonCropMenu(SeasonSlot slot)
        {
            OpenCropPicker("SACL.PickCropForSeason".Translate(), slot.plant, null, picked => slot.plant = picked);
        }

        // ---------------------------------------------------------------- stock

        private void DrawStockTab(Rect rect)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(rect);
            list.CheckboxLabeled("SACL.StockEnable".Translate(), ref plan.stockEnabled, "SACL.StockEnableDesc".Translate());
            list.GapLine(8f);

            if (!plan.stockEnabled)
            {
                GUI.color = Color.gray;
                list.Label("SACL.StockDisabledHint".Translate());
                GUI.color = Color.white;
                list.End();
                return;
            }

            ThingDef watched = StockGate.WatchedYield(zone, plan);
            if (watched == null)
            {
                GUI.color = Color.yellow;
                list.Label("SACL.StockNoYield".Translate());
                GUI.color = Color.white;
                list.End();
                return;
            }

            int stock = StockGate.CurrentStock(zone.Map, watched);
            list.Label("SACL.StockWatching".Translate(watched.LabelCap, stock));
            DrawGauge(list.GetRect(24f), stock, plan.stockHigh, plan.stockLow);
            list.Gap(10f);

            if (highBuffer == null)
            {
                highBuffer = plan.stockHigh.ToString();
                lowBuffer = plan.stockLow.ToString();
            }
            list.TextFieldNumericLabeled("SACL.StockHigh".Translate(), ref plan.stockHigh, ref highBuffer, 1f, 1000000f);
            list.TextFieldNumericLabeled("SACL.StockLow".Translate(), ref plan.stockLow, ref lowBuffer, 0f, 1000000f);
            if (plan.stockLow >= plan.stockHigh)
            {
                plan.stockLow = Mathf.Max(0, plan.stockHigh - 1);
                lowBuffer = plan.stockLow.ToString();
            }
            GUI.color = Color.gray;
            list.Label("SACL.StockHysteresisHint".Translate(plan.stockHigh, plan.stockLow));
            GUI.color = Color.white;

            list.Gap(10f);
            list.Label("SACL.StockFallbackHeader".Translate());
            if (list.ButtonText(plan.FallbackLabel))
            {
                OpenFallbackMenu();
            }

            list.Gap(8f);
            GUI.color = plan.stockSuspended ? Color.yellow : Color.gray;
            list.Label(plan.stockSuspended
                ? "SACL.StockStateSuspended".Translate()
                : "SACL.StockStateSowing".Translate());
            GUI.color = Color.white;
            list.End();
        }

        private static void DrawGauge(Rect rect, int stock, int high, int low)
        {
            float pct = high > 0 ? Mathf.Clamp01(stock / (float)high) : 0f;
            Widgets.FillableBar(rect, pct);
            if (high > 0 && low > 0 && low < high)
            {
                float x = rect.x + rect.width * (low / (float)high);
                Widgets.DrawLineVertical(x, rect.y, rect.height);
            }
            TextAnchor old = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, stock + " / " + high);
            Text.Anchor = old;
        }

        private void OpenFallbackMenu()
        {
            List<Dialog_PickCrop.Extra> extras = new List<Dialog_PickCrop.Extra>
            {
                new Dialog_PickCrop.Extra
                {
                    label = "SACL.StockFallbackNone".Translate(),
                    tooltip = "SACL.StockFallbackNoneDesc".Translate(),
                    action = () => SetFallback(FallbackMode.None, null)
                },
                new Dialog_PickCrop.Extra
                {
                    label = "SACL.StockFallbackRandom".Translate(),
                    tooltip = "SACL.StockFallbackRandomDesc".Translate(),
                    action = () => SetFallback(FallbackMode.Random, null)
                }
            };
            OpenCropPicker("SACL.PickCropForFallback".Translate(), plan.fallbackPlant, extras,
                picked => SetFallback(FallbackMode.Crop, picked));
        }

        private void SetFallback(FallbackMode mode, ThingDef plant)
        {
            plan.fallbackMode = mode;
            plan.fallbackPlant = plant;
        }
    }
}
