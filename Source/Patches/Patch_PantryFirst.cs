using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 7 - pantry first.
    /// Vanilla orders candidate ingredients by squared distance only. This void Prefix reorders the
    /// very same list in place by remaining shelf life and then tells the original method the list is
    /// already sorted, so vanilla skips its own distance sort and otherwise runs untouched: no
    /// ingredient is added or removed, only the order changes.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_TryFindBestBillIngredientsInSet
    {
        private const string MethodName = "TryFindBestBillIngredientsInSet";

        private static readonly List<Candidate> buffer = new List<Candidate>();

        private struct Candidate
        {
            public Thing thing;
            public int rotTicks;
            public float distance;
        }

        private static MethodBase Resolve() => AccessTools.Method(typeof(WorkGiver_DoBill), MethodName);

        private static bool Prepare()
        {
            if (Resolve() == null)
            {
                Log.Error("[SACL] WorkGiver_DoBill." + MethodName + " not found - module 7 stays idle.");
                return false;
            }
            return true;
        }

        private static MethodBase TargetMethod() => Resolve();

        private static void Prefix(List<Thing> availableThings, Bill bill, IntVec3 rootCell, ref bool alreadySorted)
        {
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            if (!s.modulePantryFirst || availableThings == null || availableThings.Count < 2)
            {
                return;
            }
            if (s.pantryCookingOnly && !IsCooking(bill))
            {
                return;
            }

            int horizon = (int)(s.pantryHorizonDays * GenDate.TicksPerDay);
            bool anyPerishable = false;

            buffer.Clear();
            for (int i = 0; i < availableThings.Count; i++)
            {
                Thing thing = availableThings[i];
                int rot = int.MaxValue;
                CompRottable rottable = thing.TryGetComp<CompRottable>();
                if (rottable != null)
                {
                    int ticks = rottable.TicksUntilRotAtCurrentTemp;
                    if (ticks <= horizon)
                    {
                        rot = ticks;
                        anyPerishable = true;
                    }
                }
                buffer.Add(new Candidate
                {
                    thing = thing,
                    rotTicks = rot,
                    distance = (thing.PositionHeld - rootCell).LengthHorizontalSquared
                });
            }

            if (!anyPerishable)
            {
                buffer.Clear();
                return;
            }

            buffer.Sort(Compare);
            for (int i = 0; i < buffer.Count; i++)
            {
                availableThings[i] = buffer[i].thing;
            }
            buffer.Clear();

            // Vanilla would otherwise re-sort by distance and undo the whole point of this module.
            alreadySorted = true;
        }

        private static int Compare(Candidate a, Candidate b)
        {
            if (a.rotTicks != b.rotTicks)
            {
                return a.rotTicks < b.rotTicks ? -1 : 1;
            }
            return a.distance.CompareTo(b.distance);
        }

        private static bool IsCooking(Bill bill)
        {
            RecipeDef recipe = bill?.recipe;
            return recipe != null && recipe.workSkill == SkillDefOf.Cooking;
        }
    }
}
