using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    public class CompProperties_RoofVent : CompProperties
    {
        public float ventDegreesPerSecond = 0.35f;
        public float defaultTargetTemperature = 26f;

        public CompProperties_RoofVent()
        {
            compClass = typeof(CompRoofVent);
        }
    }

    /// <summary>
    /// Automated roof hatch: opens on its own as soon as the room climbs above the target and dumps
    /// heat towards the outdoor temperature. It can never cool below outdoors, which keeps it
    /// honest against vanilla coolers.
    /// </summary>
    public class CompRoofVent : ThingComp
    {
        private float targetTemperature = 26f;
        private bool automatic = true;
        private bool openNow;

        public CompProperties_RoofVent Props => (CompProperties_RoofVent)props;

        public bool IsOpen => openNow;

        public override void Initialize(CompProperties props)
        {
            base.Initialize(props);
            targetTemperature = Props.defaultTargetTemperature;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref targetTemperature, "sacl_targetTemperature", 26f);
            Scribe_Values.Look(ref automatic, "sacl_automatic", true);
            Scribe_Values.Look(ref openNow, "sacl_openNow", false);
        }

        public override void CompTickRare()
        {
            base.CompTickRare();
            if (!SmartAgricultureMod.Settings.moduleGreenhouse || !automatic || parent.Map == null)
            {
                openNow = false;
                return;
            }
            Room room = RegionAndRoomQuery.GetRoom(parent);
            if (room == null || room.UsesOutdoorTemperature || room.CellCount <= 0)
            {
                openNow = false;
                return;
            }
            float floor = Mathf.Max(parent.Map.mapTemperature.OutdoorTemp, targetTemperature);
            float excess = room.Temperature - floor;
            openNow = excess > 0.1f;
            if (!openNow)
            {
                return;
            }
            float drop = Mathf.Min(excess, Props.ventDegreesPerSecond * (GenTicks.TickRareInterval / 60f));
            room.PushHeat(-drop * room.CellCount);
        }

        public override string CompInspectStringExtra()
        {
            string state = !automatic
                ? "SACL.VentClosed".Translate().ToString()
                : (openNow ? "SACL.VentOpen".Translate().ToString() : "SACL.VentStandby".Translate().ToString());
            return "SACL.VentTarget".Translate(targetTemperature.ToStringTemperature("F0")) + "\n" + "SACL.VentState".Translate(state);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return new Command_Toggle
            {
                defaultLabel = "SACL.VentAutoLabel".Translate(),
                defaultDesc = "SACL.VentAutoDesc".Translate(),
                icon = SaclTex.Vent,
                isActive = () => automatic,
                toggleAction = () => automatic = !automatic
            };
            yield return new Command_Action
            {
                defaultLabel = "-1 " + "SACL.Degrees".Translate(),
                defaultDesc = "SACL.VentTargetDesc".Translate(),
                icon = SaclTex.TempLower,
                action = () => targetTemperature = Mathf.Clamp(targetTemperature - 1f, -50f, 80f)
            };
            yield return new Command_Action
            {
                defaultLabel = "+1 " + "SACL.Degrees".Translate(),
                defaultDesc = "SACL.VentTargetDesc".Translate(),
                icon = SaclTex.TempRaise,
                action = () => targetTemperature = Mathf.Clamp(targetTemperature + 1f, -50f, 80f)
            };
        }
    }
}
