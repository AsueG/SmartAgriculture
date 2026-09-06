using System.Collections.Generic;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 1 - field wide rotation.
    /// The zone only moves to the next crop once every living plant of the current stage is gone,
    /// which is what stops the checkerboard: cells harvested early are simply left bare
    /// (the resolver returns null while the field is in the Ripening phase).
    /// </summary>
    public static class RotationController
    {
        public static void Evaluate(Map map, Zone_Growing zone, FieldPlan plan, int ticksElapsed)
        {
            RotationStage stage = plan.CurrentStage;
            if (stage == null)
            {
                return;
            }

            switch (plan.phase)
            {
                case RotationPhase.Sowing:
                    if (stage.kind == StageKind.Fallow)
                    {
                        plan.EnterStage(map, zone);
                        return;
                    }
                    if (stage.plant == null)
                    {
                        if (stage.kind != StageKind.Random)
                        {
                            plan.EnterStage(map, zone);
                            return;
                        }
                        // Nothing suits the current weather yet; leave the field bare and try again later.
                        stage.plant = PlantChoices.RandomForSeason(map, zone);
                        if (stage.plant == null)
                        {
                            return;
                        }
                    }
                    Scan(map, zone, stage.plant, out int emptyCells, out int alive, out bool anyMature);
                    plan.aliveCount = alive;
                    if (alive > 0 && (emptyCells == 0 || anyMature))
                    {
                        plan.phase = RotationPhase.Ripening;
                    }
                    break;

                case RotationPhase.Ripening:
                    Scan(map, zone, stage.plant, out _, out int stillAlive, out _);
                    plan.aliveCount = stillAlive;
                    if (stillAlive == 0)
                    {
                        plan.AdvanceStage(map, zone);
                        SmartAgricultureMapComponent.For(map)?.NotifyFieldCleared(zone);
                    }
                    break;

                case RotationPhase.Fallow:
                    plan.fallowTicksLeft -= ticksElapsed;
                    if (plan.fallowTicksLeft <= 0)
                    {
                        plan.AdvanceStage(map, zone);
                    }
                    break;
            }
        }

        /// <summary>Crop the rotation wants sown right now, or null while the field ripens or rests.</summary>
        public static ThingDef PlantForSowing(FieldPlan plan)
        {
            if (plan.phase != RotationPhase.Sowing)
            {
                return null;
            }
            RotationStage stage = plan.CurrentStage;
            return stage != null && stage.kind != StageKind.Fallow ? stage.plant : null;
        }

        public static void Scan(Map map, Zone_Growing zone, ThingDef stagePlant, out int emptyCells, out int alive, out bool anyMature)
        {
            emptyCells = 0;
            alive = 0;
            anyMature = false;
            List<IntVec3> cells = zone.cells;
            for (int i = 0; i < cells.Count; i++)
            {
                Plant plant = cells[i].GetPlant(map);
                if (plant == null)
                {
                    emptyCells++;
                }
                else if (plant.def == stagePlant)
                {
                    alive++;
                    if (!anyMature && (plant.HarvestableNow || plant.LifeStage == PlantLifeStage.Mature))
                    {
                        anyMature = true;
                    }
                }
            }
        }

        /// <summary>
        /// Drawn every frame by the zone gizmo and by the field plan window, so it never scans the
        /// field: the standing plant count comes from the last rotation tick.
        /// </summary>
        public static string StatusLine(FieldPlan plan)
        {
            RotationStage stage = plan.CurrentStage;
            if (stage == null)
            {
                return "SACL.RotationEmpty".Translate();
            }
            switch (plan.phase)
            {
                case RotationPhase.Sowing:
                    if (stage.kind == StageKind.Random && stage.plant == null)
                    {
                        return "SACL.PhaseRandomWaiting".Translate();
                    }
                    return "SACL.PhaseSowing".Translate(stage.Label);
                case RotationPhase.Ripening:
                    return "SACL.PhaseRipening".Translate(stage.Label, plan.aliveCount);
                default:
                    return "SACL.PhaseFallow".Translate((plan.fallowTicksLeft / (float)GenDate.TicksPerDay).ToString("0.0"));
            }
        }
    }
}
