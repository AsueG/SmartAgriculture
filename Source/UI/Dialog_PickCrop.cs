using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Searchable icon grid used everywhere this mod asks for a crop.
    ///
    /// The pretty plant menus (Nice Plants Menu, Dubs Mint Menus) cannot be reused here: they patch
    /// Command_SetPlantToGrow.ProcessInput with a prefix that takes no __instance and opens their own
    /// window with no argument, which then rebuilds its target from Find.Selector. They can therefore
    /// only ever write a zone's own crop, never a rotation stage or a season slot.
    /// </summary>
    public class Dialog_PickCrop : Window
    {
        private const float TileWidth = 92f;
        private const float TileHeight = 94f;
        private const float IconSize = 54f;
        private const float ExtraRowHeight = 32f;

        /// <summary>A choice that is not a crop: fallow, random, or "nothing".</summary>
        public struct Extra
        {
            public string label;
            public string tooltip;
            public Action action;
        }

        private readonly ThingDef current;
        private readonly Action<ThingDef> onPick;
        private readonly List<Extra> extras;
        private readonly List<ThingDef> sowable;
        private readonly List<ThingDef> shown = new List<ThingDef>();
        private readonly QuickSearchWidget search = new QuickSearchWidget();

        private Vector2 scroll = Vector2.zero;
        private bool dirty = true;

        /// <summary>Takes ownership of <paramref name="sowable"/>, which it sorts in place.</summary>
        public Dialog_PickCrop(string title, List<ThingDef> sowable, ThingDef current, Action<ThingDef> onPick, List<Extra> extras = null)
        {
            this.sowable = sowable;
            this.current = current;
            this.onPick = onPick;
            this.extras = extras ?? new List<Extra>();

            // Same order the vanilla menu uses: food first, then medicine, beauty, everything else.
            sowable.SortBy(d => 0f - PlantListPriority(d), d => d.label);

            optionalTitle = title;
            doCloseX = true;
            draggable = true;
            resizeable = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            onlyOneOfTypeAllowed = true;
        }

        public override Vector2 InitialSize => new Vector2(680f, 560f);

        public override void PostOpen()
        {
            base.PostOpen();
            search.Focus();
        }

        private static float PlantListPriority(ThingDef def)
        {
            if (def.plant.IsTree)
            {
                return 1f;
            }
            switch (def.plant.purpose)
            {
                case PlantPurpose.Food:
                    return 4f;
                case PlantPurpose.Health:
                    return 3f;
                case PlantPurpose.Beauty:
                    return 2f;
                default:
                    return 0f;
            }
        }

        private void Rebuild()
        {
            shown.Clear();
            for (int i = 0; i < sowable.Count; i++)
            {
                if (!search.filter.Active || search.filter.Matches(sowable[i]))
                {
                    shown.Add(sowable[i]);
                }
            }
            dirty = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (dirty)
            {
                Rebuild();
            }

            float y = inRect.y;
            search.OnGUI(new Rect(inRect.x, y, Mathf.Min(340f, inRect.width), QuickSearchWidget.WidgetHeight), () => dirty = true);
            y += QuickSearchWidget.WidgetHeight + 8f;

            for (int i = 0; i < extras.Count; i++)
            {
                Extra extra = extras[i];
                Rect row = new Rect(inRect.x, y, inRect.width, ExtraRowHeight);
                if (!extra.tooltip.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(row, extra.tooltip);
                }
                if (Widgets.ButtonText(row, extra.label))
                {
                    extra.action();
                    Close();
                    return;
                }
                y += ExtraRowHeight + 4f;
            }

            if (extras.Count > 0)
            {
                y += 4f;
            }

            Rect gridRect = new Rect(inRect.x, y, inRect.width, inRect.yMax - y);
            Widgets.DrawMenuSection(gridRect);
            if (shown.Count == 0)
            {
                Widgets.NoneLabelCenteredVertically(gridRect, "SACL.NoCropMatches".Translate());
                return;
            }
            DrawGrid(gridRect.ContractedBy(4f));
        }

        private void DrawGrid(Rect rect)
        {
            int perRow = Mathf.Max(1, Mathf.FloorToInt((rect.width - 16f) / TileWidth));
            int rows = Mathf.CeilToInt(shown.Count / (float)perRow);
            Rect view = new Rect(0f, 0f, rect.width - 16f, rows * TileHeight);
            Widgets.BeginScrollView(rect, ref scroll, view);

            // The pick is applied after EndScrollView: closing the window from inside the scroll view
            // leaves the GUI stack unbalanced, which is what "more calls to BeginScrollView than
            // EndScrollView" in the log was about.
            ThingDef picked = null;
            for (int i = 0; i < shown.Count; i++)
            {
                ThingDef def = shown[i];
                Rect tile = new Rect((i % perRow) * TileWidth, (i / perRow) * TileHeight, TileWidth, TileHeight);
                if (def == current)
                {
                    Widgets.DrawHighlightSelected(tile);
                }
                Widgets.DrawHighlightIfMouseover(tile);

                Rect icon = new Rect(tile.x + (TileWidth - IconSize) * 0.5f, tile.y + 6f, IconSize, IconSize);
                Widgets.ThingIcon(icon, def);

                Rect label = new Rect(tile.x + 3f, icon.yMax + 2f, TileWidth - 6f, 30f);
                GameFont oldFont = Text.Font;
                TextAnchor oldAnchor = Text.Anchor;
                bool oldWrap = Text.WordWrap;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperCenter;
                Text.WordWrap = false;
                Widgets.Label(label, def.LabelCap.ToString().Truncate(label.width));
                Text.Font = oldFont;
                Text.Anchor = oldAnchor;
                Text.WordWrap = oldWrap;

                TooltipHandler.TipRegion(tile, Tip(def));
                if (Widgets.ButtonInvisible(tile))
                {
                    picked = def;
                    break;
                }
            }

            Widgets.EndScrollView();

            if (picked != null)
            {
                onPick(picked);
                Close();
            }
        }

        private static string Tip(ThingDef def)
        {
            string tip = def.LabelCap + "\n\n" + "SACL.Yields".Translate(PlantChoices.YieldLabel(def));
            tip += "\n" + "SACL.TipGrowDays".Translate(def.plant.growDays.ToString("0.#"));
            if (def.plant.sowMinSkill > 0)
            {
                tip += "\n" + "MinSkill".Translate() + ": " + def.plant.sowMinSkill;
            }
            return tip;
        }
    }
}
