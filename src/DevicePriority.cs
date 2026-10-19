using System;
using System.Collections.Generic;
using System.Linq;

namespace GearPower
{
    /// <summary>
    /// Priority used by the tray icon, hover card, menu and taskbar strip.
    /// A low battery device that is charging should not outrank a discharging
    /// device just because its percentage is lower.
    /// </summary>
    public static class DevicePriority
    {
        public static List<DeviceReading> Sort(IEnumerable<DeviceReading> readings)
        {
            if (readings == null) return new List<DeviceReading>();
            return readings
                .Where(r => r != null)
                .OrderBy(Rank)
                .ThenBy(r => r.Percent.HasValue ? r.Percent.Value : 101)
                .ToList();
        }

        public static DeviceReading Primary(IEnumerable<DeviceReading> readings)
        {
            if (readings == null) return null;
            List<DeviceReading> withLevel = readings
                .Where(r => r != null && r.Percent.HasValue)
                .ToList();
            if (withLevel.Count == 0) return null;

            List<DeviceReading> discharging = withLevel.Where(r => !r.Charging).ToList();
            IEnumerable<DeviceReading> pool = discharging.Count > 0 ? discharging : withLevel;
            return pool.OrderBy(r => r.Percent.Value).FirstOrDefault();
        }

        private static int Rank(DeviceReading reading)
        {
            if (!reading.Percent.HasValue) return 3;
            if (reading.Charging) return 2;
            return 0;
        }
    }
}
