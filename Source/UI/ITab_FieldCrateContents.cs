using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Lists what a field crate is holding, the way the bookcase lists its books. ITab_ContentsBase
    /// draws the whole list already; only where the things come from differs. Dropping is switched off
    /// because a crate's contents are not held in a container - they sit on its cell, and vanilla
    /// hauling is what takes them away.
    /// </summary>
    public class ITab_FieldCrateContents : ITab_ContentsBase
    {
        public override IList<Thing> container
        {
            get
            {
                List<Thing> held = new List<Thing>();
                if (SelThing is Building_FieldCrate crate && crate.Spawned)
                {
                    foreach (Thing t in crate.slotGroup.HeldThings)
                    {
                        held.Add(t);
                    }
                }
                return held;
            }
        }

        public ITab_FieldCrateContents()
        {
            labelKey = "TabCasketContents";
            containedItemsKey = "TabCasketContents";
            canRemoveThings = false;
            size = new Vector2(340f, 240f);
        }
    }
}
