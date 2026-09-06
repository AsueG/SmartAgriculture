using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 8 - soil rest.
    /// A fallow step banks a fertility reserve on the field's cells, which the crops that follow spend
    /// as they grow. The reserve is purely additive: vanilla puts plain soil at 1.0 fertility and
    /// PlantProperties.fertilityMin at 0.9, so there is no room at all under a field for a depletion
    /// model - it would make fields outright unsowable rather than merely slower. Adding on top is the
    /// only way a fallow can matter without ever taking anything away from a player who ignores it.
    /// </summary>
    public class SoilRestSystem
    {
        /// <summary>Reserve per cell index, sparse: only cells of a rested field are ever in here.</summary>
        private Dictionary<int, float> reserves = new Dictionary<int, float>();

        private readonly Map map;

        public SoilRestSystem(Map map)
        {
            this.map = map;
        }

        /// <summary>Read by the fertility patch on every cell query, so it has to stay a fast no.</summary>
        public bool Empty => reserves.Count == 0;

        public float ReserveAt(IntVec3 cell)
        {
            if (reserves.Count == 0)
            {
                return 0f;
            }
            if (!reserves.TryGetValue(map.cellIndices.CellToIndex(cell), out float value))
            {
                return 0f;
            }
            // Clamped on read so lowering the ceiling in the settings takes effect at once, rather than
            // only once the cells banked under the old ceiling have drained.
            return Mathf.Min(value, SmartAgricultureMod.Settings.soilRestCap);
        }

        /// <summary>Reserve averaged over the field, which is what the window shows and the auto length uses.</summary>
        public float AverageReserve(Zone_Growing zone)
        {
            List<IntVec3> cells = zone?.cells;
            if (reserves.Count == 0 || cells == null || cells.Count == 0)
            {
                return 0f;
            }
            CellIndices indices = map.cellIndices;
            float cap = SmartAgricultureMod.Settings.soilRestCap;
            float total = 0f;
            for (int i = 0; i < cells.Count; i++)
            {
                if (reserves.TryGetValue(indices.CellToIndex(cells[i]), out float value))
                {
                    total += Mathf.Min(value, cap);
                }
            }
            return total / cells.Count;
        }

        /// <summary>Days of rest still needed to top this field's reserve up to the cap.</summary>
        public int SuggestedFallowDays(Zone_Growing zone)
        {
            SmartAgricultureSettings settings = SmartAgricultureMod.Settings;
            if (settings.soilRestPerDay <= 0f)
            {
                return settings.defaultFallowDays;
            }
            float missing = settings.soilRestCap - AverageReserve(zone);
            return Mathf.Clamp(Mathf.CeilToInt(missing / settings.soilRestPerDay), 1, 60);
        }

        /// <summary>
        /// One field, one plan tick. A cell left deliberately bare banks reserve; a cell with something
        /// growing on it spends it, more slowly than it was banked. Settled per cell, where the growing
        /// actually happens, rather than from a field wide plant count.
        /// </summary>
        public void TickZone(Zone_Growing zone, FieldPlan plan, int ticksElapsed)
        {
            bool resting = IsResting(plan);
            if (!resting && reserves.Count == 0)
            {
                // Nothing banked and nothing being banked: skip the per cell pass entirely.
                return;
            }

            SmartAgricultureSettings settings = SmartAgricultureMod.Settings;
            float days = ticksElapsed / (float)GenDate.TicksPerDay;
            float step = settings.soilRestPerDay * days;
            float drain = settings.soilRestDrainPerDay * days;
            if (step <= 0f && drain <= 0f)
            {
                return;
            }
            float cap = settings.soilRestCap;

            List<IntVec3> cells = zone.cells;
            CellIndices indices = map.cellIndices;
            for (int i = 0; i < cells.Count; i++)
            {
                IntVec3 cell = cells[i];
                int index = indices.CellToIndex(cell);
                if (cell.GetPlant(map) == null)
                {
                    if (!resting || step <= 0f || cap <= 0f)
                    {
                        continue;
                    }
                    reserves.TryGetValue(index, out float banked);
                    if (banked < cap)
                    {
                        reserves[index] = Mathf.Min(cap, banked + step);
                    }
                    continue;
                }

                if (drain <= 0f || !reserves.TryGetValue(index, out float spending))
                {
                    continue;
                }
                spending -= drain;
                if (spending <= 0.0005f)
                {
                    reserves.Remove(index);
                }
                else
                {
                    reserves[index] = spending;
                }
            }
        }

        /// <summary>
        /// A field is resting only when the player asked for it to sit bare - a rotation fallow step or
        /// a season set to sow nothing. The gap between a harvest and the next sowing must not count,
        /// or every field would bank fertility on its own and a fallow step would be pointless.
        /// </summary>
        private bool IsResting(FieldPlan plan)
        {
            SmartAgricultureSettings settings = SmartAgricultureMod.Settings;
            if (settings.moduleSeasonal && plan.seasonalEnabled
                && plan.SlotFor(GenLocalDate.Season(map)).mode == SeasonMode.Fallow)
            {
                return true;
            }
            if (!settings.moduleRotation || !plan.RotationUsable || plan.phase != RotationPhase.Fallow)
            {
                return false;
            }
            RotationStage stage = plan.CurrentStage;
            return stage != null && stage.kind == StageKind.Fallow;
        }

        /// <summary>
        /// Drops the reserve of cells that no longer belong to a grow zone, so deleting a field does
        /// not leave enriched ground behind and the saved dictionary cannot grow without bound.
        /// </summary>
        public void Prune()
        {
            if (reserves.Count == 0)
            {
                return;
            }

            CellIndices indices = map.cellIndices;
            HashSet<int> live = new HashSet<int>();
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (!(zones[i] is Zone_Growing zone))
                {
                    continue;
                }
                List<IntVec3> cells = zone.cells;
                for (int c = 0; c < cells.Count; c++)
                {
                    live.Add(indices.CellToIndex(cells[c]));
                }
            }

            List<int> dead = null;
            foreach (KeyValuePair<int, float> kv in reserves)
            {
                if (!live.Contains(kv.Key))
                {
                    (dead ?? (dead = new List<int>())).Add(kv.Key);
                }
            }
            if (dead == null)
            {
                return;
            }
            for (int i = 0; i < dead.Count; i++)
            {
                reserves.Remove(dead[i]);
            }
        }

        /// <summary>Scribed straight into the map component's node rather than as a deep object.</summary>
        public void ExposeData()
        {
            Scribe_Collections.Look(ref reserves, "soilReserves", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && reserves == null)
            {
                reserves = new Dictionary<int, float>();
            }
        }
    }
}
