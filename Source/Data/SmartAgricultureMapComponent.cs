using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Single owner of every piece of per-map state this mod adds. Vanilla classes stay untouched;
    /// removing the mod simply drops this component and the save keeps loading.
    /// </summary>
    public class SmartAgricultureMapComponent : MapComponent
    {
        private const int PlanInterval = 250;
        private const int GreenhouseInterval = 250;
        private const int RoomScanInterval = 900;
        private const int HazardInterval = 250;

        private Dictionary<int, FieldPlan> plans = new Dictionary<int, FieldPlan>();

        private readonly GreenhouseThermalSystem greenhouse;
        private readonly HazardWatcher hazard;
        private readonly SoilRestSystem soilRest;
        private readonly FieldCrateRegistry crates = new FieldCrateRegistry();

        private static Map lastMap;
        private static SmartAgricultureMapComponent lastComp;

        public SmartAgricultureMapComponent(Map map) : base(map)
        {
            greenhouse = new GreenhouseThermalSystem(map);
            hazard = new HazardWatcher(map);
            soilRest = new SoilRestSystem(map);
        }

        public FieldCrateRegistry Crates => crates;

        public HazardWatcher Hazard => hazard;

        public SoilRestSystem SoilRest => soilRest;

        public static SmartAgricultureMapComponent For(Map map)
        {
            if (map == null)
            {
                return null;
            }
            if (ReferenceEquals(map, lastMap) && lastComp != null)
            {
                return lastComp;
            }
            SmartAgricultureMapComponent comp = map.GetComponent<SmartAgricultureMapComponent>();
            lastMap = map;
            lastComp = comp;
            return comp;
        }

        public bool HasPlans => plans.Count > 0;

        public FieldPlan GetPlan(Zone_Growing zone)
        {
            if (zone == null)
            {
                return null;
            }
            return plans.TryGetValue(zone.ID, out FieldPlan plan) ? plan : null;
        }

        public FieldPlan GetOrCreatePlan(Zone_Growing zone)
        {
            FieldPlan plan = GetPlan(zone);
            if (plan == null)
            {
                plan = new FieldPlan(zone.ID);
                plans[zone.ID] = plan;
            }
            return plan;
        }

        public void DropPlan(Zone_Growing zone)
        {
            if (zone != null)
            {
                plans.Remove(zone.ID);
            }
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
            PrunePlans();
            greenhouse.Rescan();
            hazard.Recheck();
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            if (ReferenceEquals(map, lastMap))
            {
                lastMap = null;
                lastComp = null;
            }
        }

        public override void MapComponentTick()
        {
            int tick = Find.TickManager.TicksGame;
            int offset = map.uniqueID;

            if ((tick + offset) % PlanInterval == 0 && plans.Count > 0)
            {
                TickPlans();
            }
            if ((tick + offset) % HazardInterval == 0)
            {
                hazard.Recheck();
            }
            if (SmartAgricultureMod.Settings.moduleGreenhouse)
            {
                if ((tick + offset) % RoomScanInterval == 0)
                {
                    greenhouse.Rescan();
                }
                if ((tick + offset) % GreenhouseInterval == 0)
                {
                    greenhouse.ApplySolarGain(GreenhouseInterval);
                }
            }
            if ((tick + offset) % 2000 == 0)
            {
                PrunePlans();
                soilRest.Prune();
            }
        }

        private void TickPlans()
        {
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                Zone_Growing zone = zones[i] as Zone_Growing;
                if (zone == null)
                {
                    continue;
                }
                if (!plans.TryGetValue(zone.ID, out FieldPlan plan))
                {
                    continue;
                }

                // Ahead of the AnythingConfigured gate, because a field the player has since switched
                // off may still hold a soil reserve and that reserve has to keep draining. Ahead of the
                // rotation too, so it accounts for the state the field was actually in over the
                // interval that just elapsed rather than the one the rotation is about to move to.
                if (SmartAgricultureMod.Settings.moduleSoilRest)
                {
                    soilRest.TickZone(zone, plan, PlanInterval);
                }

                if (!plan.AnythingConfigured)
                {
                    continue;
                }
                if (SmartAgricultureMod.Settings.moduleStock && plan.stockEnabled)
                {
                    StockGate.UpdateHysteresis(map, zone, plan);
                }
                if (SmartAgricultureMod.Settings.moduleRotation && plan.RotationUsable)
                {
                    RotationController.Evaluate(map, zone, plan, PlanInterval);
                }
            }
        }

        private void PrunePlans()
        {
            if (plans.Count == 0)
            {
                return;
            }
            HashSet<int> live = new HashSet<int>();
            List<Zone> zones = map.zoneManager.AllZones;
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] is Zone_Growing zone)
                {
                    live.Add(zone.ID);
                }
            }
            List<int> dead = null;
            foreach (KeyValuePair<int, FieldPlan> kv in plans)
            {
                if (!live.Contains(kv.Key))
                {
                    (dead ?? (dead = new List<int>())).Add(kv.Key);
                }
            }
            if (dead != null)
            {
                for (int i = 0; i < dead.Count; i++)
                {
                    plans.Remove(dead[i]);
                }
            }
        }

        /// <summary>Called by the rotation controller once a field has been fully harvested.</summary>
        public void NotifyFieldCleared(Zone_Growing zone)
        {
            if (!SmartAgricultureMod.Settings.moduleFieldCrates)
            {
                return;
            }
            crates.ExportCratesNear(zone);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref plans, "fieldPlans", LookMode.Value, LookMode.Deep);
            soilRest.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit && plans == null)
            {
                plans = new Dictionary<int, FieldPlan>();
            }
        }
    }
}
