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
        /// <summary>Every slot is taken, so only a top-up of a crop already inside can still arrive.</summary>
        private const int StallTicksNoRoom = 2500;

        /// <summary>Room is left, so the wait has to outlast a night or a field would take two trips.</summary>
        private const int StallTicksPartial = 30000;

        private bool autoExportWhenFull = true;
        private bool exporting;
        private StoragePriority fillingPriority = StoragePriority.Preferred;
        private int lastStoredCount;
        private int lastChangeTick = -1;

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

        /// <summary>Not one more item fits, whatever it is.</summary>
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

        /// <summary>
        /// No free slot left, so nothing but more of a crop already inside can ever enter. A crate
        /// holding three different crops sits here with every stack still partial: IsFull is false and
        /// stays false, which is what used to leave it at a high priority for good.
        /// </summary>
        public bool NoRoomLeft => StackCount >= StackCapacity;

        /// <summary>Ticks since the contents last changed, or -1 while the crate has never been fed.</summary>
        public int IdleTicks => lastChangeTick < 0 ? -1 : Find.TickManager.TicksGame - lastChangeTick;

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
            int stored = StoredCount;
            if (stored != lastStoredCount)
            {
                lastStoredCount = stored;
                lastChangeTick = Find.TickManager.TicksGame;
            }

            if (exporting)
            {
                if (stored == 0)
                {
                    EndExport();
                }
                return;
            }
            if (!autoExportWhenFull || stored == 0)
            {
                return;
            }
            if (IsFull)
            {
                BeginExport();
                return;
            }
            // Otherwise wait for the deposits to stop rather than for the last stack to reach its
            // stackLimit, which mixed contents never do. This is also what exports a half filled crate
            // once its field is done: NotifyFieldCleared only fires for a zone driven by the rotation.
            int idle = IdleTicks;
            if (idle >= 0 && idle >= (NoRoomLeft ? StallTicksNoRoom : StallTicksPartial))
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
            string state;
            if (exporting)
            {
                state = "SACL.CrateStateExporting".Translate();
            }
            else if (IsFull)
            {
                state = "SACL.CrateStateFull".Translate();
            }
            else if (NoRoomLeft)
            {
                state = "SACL.CrateStateNoRoom".Translate();
            }
            else
            {
                state = "SACL.CrateStateFilling".Translate();
            }
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
            Scribe_Values.Look(ref lastStoredCount, "sacl_lastStoredCount", 0);
            Scribe_Values.Look(ref lastChangeTick, "sacl_lastChangeTick", -1);
        }
    }
}
