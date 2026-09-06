using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SmartAgriculture
{
    /// <summary>
    /// Module 4 - passive greenhouse thermodynamics.
    /// Enclosed rooms that keep their own temperature and hold at least one transparent roof cell
    /// gain heat in proportion to outdoor sunlight, capped at a configurable delta above the
    /// outdoor temperature so a greenhouse never runs away.
    /// Transparent roofs come from ModCompat, so a third party skylight mod drives this system
    /// without any duplicate building of ours.
    /// </summary>
    public class GreenhouseThermalSystem
    {
        private const int MaxTrackedRoomCells = 600;

        private struct Entry
        {
            public IntVec3 anchor;
            public int transparentCells;
        }

        private readonly Map map;
        private readonly List<Entry> entries = new List<Entry>();

        public GreenhouseThermalSystem(Map map)
        {
            this.map = map;
        }

        public int TrackedRoomCount => entries.Count;

        /// <summary>Rooms are looked up again from an anchor cell on every use, so region rebuilds cannot leave stale references.</summary>
        public void Rescan()
        {
            entries.Clear();
            if (ModCompat.TransparentRoofs.Count == 0)
            {
                return;
            }
            IReadOnlyList<Room> rooms = map.regionGrid.AllRooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                Room room = rooms[i];
                if (room == null || room.IsDoorway || room.UsesOutdoorTemperature || room.CellCount <= 0 || room.CellCount > MaxTrackedRoomCells)
                {
                    continue;
                }
                int transparent = TransparentCellsOf(room);
                IntVec3 anchor = IntVec3.Invalid;
                foreach (IntVec3 c in room.Cells)
                {
                    anchor = c;
                    break;
                }
                if (transparent > 0 && anchor.IsValid)
                {
                    entries.Add(new Entry { anchor = anchor, transparentCells = transparent });
                }
            }
        }

        public void ApplySolarGain(int ticksElapsed)
        {
            if (entries.Count == 0)
            {
                return;
            }
            float glow = map.skyManager.CurSkyGlow;
            if (glow <= 0.01f)
            {
                return;
            }
            SmartAgricultureSettings s = SmartAgricultureMod.Settings;
            float seconds = ticksElapsed / 60f;
            float outdoor = map.mapTemperature.OutdoorTemp;
            float cap = outdoor + s.greenhouseMaxDelta;

            for (int i = 0; i < entries.Count; i++)
            {
                Room room = entries[i].anchor.GetRoom(map);
                if (room == null || room.UsesOutdoorTemperature || room.CellCount <= 0)
                {
                    continue;
                }
                float headroom = cap - room.Temperature;
                if (headroom <= 0f)
                {
                    continue;
                }
                float degrees = glow * s.solarHeatPerCellPerSecond * seconds * entries[i].transparentCells / room.CellCount;
                room.PushHeat(Mathf.Min(degrees, headroom) * room.CellCount);
            }
        }

        public int TransparentCellsOf(Room room)
        {
            if (room == null)
            {
                return 0;
            }
            int transparent = 0;
            foreach (IntVec3 c in room.Cells)
            {
                if (ModCompat.IsTransparentRoof(map.roofGrid.RoofAt(c)))
                {
                    transparent++;
                }
            }
            return transparent;
        }
    }
}
