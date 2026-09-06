using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Index of the field crates on one map so the harvest hook never scans the map.
    /// One instance per map, owned by SmartAgricultureMapComponent: a static index keyed by map id
    /// would outlive the game, and Map.uniqueID restarts at zero in every save, so entries from a
    /// previous session would answer for this one and keep its Map object alive.
    /// </summary>
    public class FieldCrateRegistry
    {
        private readonly List<Building_FieldCrate> crates = new List<Building_FieldCrate>();

        public int Count => crates.Count;

        public static FieldCrateRegistry For(Map map) => SmartAgricultureMapComponent.For(map)?.Crates;

        public void Register(Building_FieldCrate crate)
        {
            if (!crates.Contains(crate))
            {
                crates.Add(crate);
            }
        }

        public void Deregister(Building_FieldCrate crate)
        {
            crates.Remove(crate);
        }

        public Building_FieldCrate NearestAccepting(IntVec3 origin, Thing thing, float radius)
        {
            if (crates.Count == 0)
            {
                return null;
            }
            float radiusSq = radius * radius;
            Building_FieldCrate best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < crates.Count; i++)
            {
                Building_FieldCrate crate = crates[i];
                if (!crate.Spawned)
                {
                    continue;
                }
                float dist = (crate.Position - origin).LengthHorizontalSquared;
                if (dist > radiusSq || dist >= bestDist)
                {
                    continue;
                }
                if (!crate.CanTake(thing))
                {
                    continue;
                }
                best = crate;
                bestDist = dist;
            }
            return best;
        }

        /// <summary>Fires the grouped transport once a whole field has been harvested.</summary>
        public void ExportCratesNear(Zone_Growing zone)
        {
            if (crates.Count == 0 || zone.cells.Count == 0)
            {
                return;
            }
            float radius = SmartAgricultureMod.Settings.crateSearchRadius;
            float radiusSq = radius * radius;
            for (int i = 0; i < crates.Count; i++)
            {
                Building_FieldCrate crate = crates[i];
                if (!crate.Spawned || crate.Exporting || crate.StoredCount == 0)
                {
                    continue;
                }
                for (int j = 0; j < zone.cells.Count; j++)
                {
                    if ((zone.cells[j] - crate.Position).LengthHorizontalSquared <= radiusSq)
                    {
                        crate.BeginExport();
                        break;
                    }
                }
            }
        }
    }
}
