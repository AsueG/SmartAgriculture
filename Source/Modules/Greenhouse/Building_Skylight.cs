using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Fallback glazing used only when no third party skylight mod is present: it swaps the roof
    /// above itself for our own transparent RoofDef and puts the previous roof back when removed.
    /// </summary>
    public class Building_Skylight : Building
    {
        private RoofDef previousRoof;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            TryGlaze();
        }

        /// <summary>Retried because the frame is often placed before the colonists have finished roofing.</summary>
        public override void TickRare()
        {
            base.TickRare();
            TryGlaze();
        }

        private void TryGlaze()
        {
            RoofDef glass = ModCompat.FallbackGlassRoof;
            if (glass == null || Map == null)
            {
                return;
            }
            foreach (IntVec3 c in this.OccupiedRect())
            {
                RoofDef current = Map.roofGrid.RoofAt(c);
                // A bare cell is left alone rather than glazed. Roofs are held up by edifices with
                // holdsRoof within 6.9 cells (RoofCollapseUtility) and this frame is deliberately not
                // an edifice, so roofing an open cell here would conjure a roof nothing supports -
                // and nothing would ever check it, since collapse is only tested when a wall is lost.
                // On load the cell already holds glass, so previousRoof keeps its saved value.
                if (current == null || current == glass)
                {
                    continue;
                }
                previousRoof = current;
                Map.roofGrid.SetRoof(c, glass);
            }
        }

        public bool Glazed => Map != null && ModCompat.IsTransparentRoof(Map.roofGrid.RoofAt(Position));

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = Map;
            CellRect rect = this.OccupiedRect();
            RoofDef restore = previousRoof;
            base.DeSpawn(mode);
            if (map == null)
            {
                return;
            }
            foreach (IntVec3 c in rect)
            {
                if (ModCompat.IsTransparentRoof(map.roofGrid.RoofAt(c)))
                {
                    map.roofGrid.SetRoof(c, restore);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref previousRoof, "sacl_previousRoof");
        }

        public override string GetInspectString()
        {
            string basic = base.GetInspectString();
            SmartAgricultureMapComponent comp = SmartAgricultureMapComponent.For(Map);
            if (comp == null)
            {
                return basic;
            }
            string line = Glazed
                ? "SACL.SkylightActive".Translate(ModCompat.TransparentRoofSourceLabel)
                : (string)"SACL.SkylightNoRoof".Translate();
            return basic.NullOrEmpty() ? line : basic + "\n" + line;
        }
    }
}
