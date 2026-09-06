using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Fills the crate's default storage filter with the harvest yields of every sowable plant present,
    /// modded crops included.
    ///
    /// It has to happen on the def rather than on the built thing: Frame.CompleteConstruction ends with
    /// ThingStoreSettings.CopyFrom(frame settings), and those settings come from the blueprint, which
    /// copies them from defaultStorageSettings. Anything a Building_FieldCrate sets for itself in
    /// PostMake is therefore overwritten the moment construction finishes.
    ///
    /// A static constructor is the right moment: every ThingDef is loaded and resolved by then, so the
    /// crop list is complete, and no blueprint can exist yet.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class FieldCrateDefaults
    {
        static FieldCrateDefaults()
        {
            ThingDef crate = DefDatabase<ThingDef>.GetNamedSilentFail("SACL_FieldCrate");
            if (crate?.building == null)
            {
                return;
            }
            // The fixed filter is pinned to the same list, which is how vanilla keeps a specialised
            // store clean - the nutrient paste hopper pins its own to FoodRaw. It does two things the
            // default filter cannot: Listing_TreeThingFilter.Visible hides every def, category and
            // special filter the parent refuses, so weapons and apparel disappear from the storage tab
            // instead of sitting there red-crossed, and StorageSettings.AllowedToAccept walks up to the
            // parent, so even a crate saved with a wide filter stops taking them.
            if (crate.building.fixedStorageSettings != null)
            {
                ApplyTo(crate.building.fixedStorageSettings.filter);
            }
            if (crate.building.defaultStorageSettings == null)
            {
                crate.building.defaultStorageSettings = new StorageSettings();
            }
            ApplyTo(crate.building.defaultStorageSettings.filter);
        }

        /// <summary>Allow exactly the crop yields, nothing else. Also used by the crate's reset gizmo.</summary>
        public static void ApplyTo(ThingFilter filter)
        {
            // An empty list would leave a crate that accepts nothing and cannot be widened, since this
            // is applied to the fixed filter too.
            if (filter == null || PlantChoices.CropYields.Count == 0)
            {
                return;
            }
            filter.SetDisallowAll();
            foreach (ThingDef yield in PlantChoices.CropYields)
            {
                filter.SetAllow(yield, true);
            }
            // The tab roots its tree at the parent filter's narrowest enclosing category, and that is
            // cached from whatever the def declared at load time.
            filter.RecalculateDisplayRootCategory();
        }
    }
}
