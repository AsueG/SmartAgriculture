using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 5 - field crate.
    /// A normal Building_Storage (so vanilla hauling, storage groups, ITab_Storage and Pick Up And
    /// Haul all work untouched). While it is filling it keeps a high storage priority so haulers
    /// leave it alone; once it is full or its field has been cleared it drops to Low, which is what
    /// makes vanilla haulers move the whole batch to the cold room in one go.
    /// </summary>
    public class Building_FieldCrate : Building_Storage
    {
        private bool autoExportWhenFull = true;
        private bool exporting;
        private StoragePriority fillingPriority = StoragePriority.Preferred;

        public bool Exporting => exporting;

        public int StoredCount
        {
            get
            {
                int total = 0;
                foreach (Thing t in slotGroup.HeldThings)
                {
                    total += t.stackCount;
                }
                return total;
            }
        }

        /// <summary>How many stacks the crate can hold at once.</summary>
        public int StackCapacity => slotGroup.CellsList.Count * Mathf.Max(1, def.building.maxItemsInCell);

        public int StackCount
        {
            get
            {
                int stacks = 0;
                foreach (Thing t in slotGroup.HeldThings)
                {
                    stacks++;
                }
                return stacks;
            }
        }

        public bool IsFull
        {
            get
            {
                int stacks = 0;
                foreach (Thing t in slotGroup.HeldThings)
                {
                    stacks++;
                    if (t.stackCount < t.def.stackLimit)
                    {
                        return false;
                    }
                }
                return stacks >= StackCapacity;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            FieldCrateRegistry.For(map)?.Register(this);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            // Before base, which clears Map.
            FieldCrateRegistry.For(Map)?.Deregister(this);
            base.DeSpawn(mode);
        }

        public override void TickRare()
        {
            base.TickRare();
            if (!SmartAgricultureMod.Settings.moduleFieldCrates)
            {
                return;
            }
            if (exporting)
            {
                if (StoredCount == 0)
                {
                    EndExport();
                }
            }
            else if (autoExportWhenFull && IsFull)
            {
                BeginExport();
            }
        }

        public void BeginExport()
        {
            if (exporting)
            {
                return;
            }
            exporting = true;
            fillingPriority = settings.Priority;
            if (settings.Priority > StoragePriority.Low)
            {
                settings.Priority = StoragePriority.Low;
            }
        }

        public void EndExport()
        {
            if (!exporting)
            {
                return;
            }
            exporting = false;
            settings.Priority = fillingPriority;
        }

        /// <summary>Called by the harvest patch: can this crate still swallow that stack?</summary>
        public bool CanTake(Thing thing)
        {
            if (exporting || !settings.AllowedToAccept(thing) || Map == null)
            {
                return false;
            }
            foreach (IntVec3 c in slotGroup.CellsList)
            {
                if (StoreUtility.IsGoodStoreCell(c, Map, thing, null, Faction))
                {
                    return true;
                }
            }
            return false;
        }

        public IntVec3 BestCellFor(Thing thing)
        {
            foreach (IntVec3 c in slotGroup.CellsList)
            {
                if (StoreUtility.IsGoodStoreCell(c, Map, thing, null, Faction))
                {
                    return c;
                }
            }
            return IntVec3.Invalid;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos())
            {
                yield return g;
            }
            if (!SmartAgricultureMod.Settings.moduleFieldCrates)
            {
                yield break;
            }
            yield return new Command_Toggle
            {
                defaultLabel = "SACL.CrateAutoExport".Translate(),
                defaultDesc = "SACL.CrateAutoExportDesc".Translate(),
                icon = SaclTex.Crate,
                isActive = () => autoExportWhenFull,
                toggleAction = () => autoExportWhenFull = !autoExportWhenFull
            };
            yield return new Command_Action
            {
                defaultLabel = exporting ? "SACL.CrateHold".Translate() : "SACL.CrateExportNow".Translate(),
                defaultDesc = "SACL.CrateExportNowDesc".Translate(),
                icon = SaclTex.Crate,
                action = () =>
                {
                    if (exporting)
                    {
                        EndExport();
                    }
                    else
                    {
                        BeginExport();
                    }
                }
            };
            // Crates built before the default filter was fixed have an empty one saved, and a saved
            // filter is never re-derived from the def. This is how a player gets those back.
            yield return new Command_Action
            {
                defaultLabel = "SACL.CrateResetFilter".Translate(),
                defaultDesc = "SACL.CrateResetFilterDesc".Translate(),
                icon = SaclTex.Crate,
                action = () =>
                {
                    FieldCrateDefaults.ApplyTo(settings.filter);
                    Messages.Message("SACL.CrateResetFilterDone".Translate(PlantChoices.CropYields.Count),
                        this, MessageTypeDefOf.TaskCompletion, false);
                }
            };
        }

        public override string GetInspectString()
        {
            string basic = base.GetInspectString();
            string state = exporting
                ? "SACL.CrateStateExporting".Translate()
                : (IsFull ? "SACL.CrateStateFull".Translate() : "SACL.CrateStateFilling".Translate());
            // The priority is worth showing even though ITab_Storage has it too: this crate changes it
            // by itself when it starts exporting, so the value on screen is not always the one set.
            string priority = exporting
                ? "SACL.CratePriorityExporting".Translate(settings.Priority.Label(), fillingPriority.Label())
                : "SACL.CratePriority".Translate(settings.Priority.Label());
            string line = "SACL.CrateFill".Translate(StackCount, StackCapacity, StoredCount) + " - " + state
                + "\n" + priority;
            return basic.NullOrEmpty() ? line : basic + "\n" + line;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref autoExportWhenFull, "sacl_autoExportWhenFull", true);
            Scribe_Values.Look(ref exporting, "sacl_exporting", false);
            Scribe_Values.Look(ref fillingPriority, "sacl_fillingPriority", StoragePriority.Preferred);
        }
    }
}
