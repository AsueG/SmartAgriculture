using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Refuses the cell of a wall, a door or solid rock. Both greenhouse buildings only ever act on a
    /// room's interior - GreenhouseThermalSystem counts transparent roofs across room.Cells, and
    /// CompRoofVent pushes heat into the room it stands in - and neither a wall cell nor a doorway
    /// belongs to one. Placing there used to be accepted and then do nothing at all.
    /// </summary>
    public class PlaceWorker_RoomInterior : PlaceWorker
    {
        public override AcceptanceReport AllowsPlacing(BuildableDef checkingDef, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
        {
            Building edifice = loc.GetEdifice(map);
            if (edifice == null || edifice == thingToIgnore)
            {
                return true;
            }
            if (edifice.def.holdsRoof || edifice.def.IsDoor)
            {
                return "SACL.NeedsRoomInterior".Translate(edifice.def.label);
            }
            return true;
        }
    }
}
