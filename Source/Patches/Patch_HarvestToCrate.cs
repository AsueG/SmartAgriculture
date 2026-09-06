using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 5 - harvested produce goes straight into a nearby field crate.
    /// JobDriver_PlantWork places the yield on the ground and then calls Plant.PlantCollected, so a
    /// Postfix on that call is the first safe moment to redirect the fresh stack. Nothing is
    /// destroyed or duplicated: the stack is moved into the crate, and if no crate qualifies the
    /// vanilla ground pile stays exactly where it was.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_PlantCollected
    {
        private static MethodBase Resolve() => AccessTools.Method(typeof(Plant), nameof(Plant.PlantCollected));

        private static bool Prepare()
        {
            if (Resolve() == null)
            {
                Log.Error("[SACL] Plant.PlantCollected not found - field crates fall back to plain ground piles.");
                return false;
            }
            return true;
        }

        private static MethodBase TargetMethod() => Resolve();

        private static void Postfix(Plant __instance, Pawn by, PlantDestructionMode plantDestructionMode)
        {
            if (!SmartAgricultureMod.Settings.moduleFieldCrates || plantDestructionMode != PlantDestructionMode.Cut)
            {
                return;
            }
            ThingDef yieldDef = __instance.def.plant?.harvestedThingDef;
            if (yieldDef == null || by == null || by.Map == null || by.Faction != Faction.OfPlayer)
            {
                return;
            }
            Map map = by.Map;
            FieldCrateRegistry registry = FieldCrateRegistry.For(map);
            if (registry == null || registry.Count == 0)
            {
                return;
            }

            float radius = SmartAgricultureMod.Settings.crateSearchRadius;
            foreach (IntVec3 cell in GenAdjFast.AdjacentCellsCardinal(by.Position))
            {
                TryStore(registry, map, cell, yieldDef, radius);
            }
            TryStore(registry, map, by.Position, yieldDef, radius);
        }

        private static void TryStore(FieldCrateRegistry registry, Map map, IntVec3 cell, ThingDef yieldDef, float radius)
        {
            if (!cell.InBounds(map))
            {
                return;
            }
            List<Thing> things = cell.GetThingList(map);
            for (int i = things.Count - 1; i >= 0; i--)
            {
                Thing thing = things[i];
                if (thing.def != yieldDef || thing.IsInAnyStorage() || thing.IsForbidden(Faction.OfPlayer))
                {
                    continue;
                }
                Building_FieldCrate crate = registry.NearestAccepting(cell, thing, radius);
                if (crate == null)
                {
                    continue;
                }
                IntVec3 target = crate.BestCellFor(thing);
                if (!target.IsValid)
                {
                    continue;
                }
                thing.DeSpawn();
                if (!GenPlace.TryPlaceThing(thing, target, map, ThingPlaceMode.Direct))
                {
                    // Put it back exactly where vanilla had left it rather than losing the stack.
                    GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
                }
            }
        }
    }
}
