using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GearPower
{
    public sealed class InventoryCategory
    {
        public string KindId;
        public string Name;
        public List<InventoryDeviceGroup> Devices = new List<InventoryDeviceGroup>();
        public int InterfaceCount { get { return Devices.Sum(device => device.Interfaces.Count); } }
    }

    public sealed class InventoryDeviceGroup
    {
        public string Id;
        public string Name;
        public string KindId;
        public string VidPid;
        public List<InventoryRow> Interfaces = new List<InventoryRow>();
        public bool Supported { get { return Interfaces.Any(row => row.IsSupported); } }
    }

    public static class InventoryGroups
    {
        public static string KindId(DeviceKind kind)
        {
            switch (kind)
            {
                case DeviceKind.Keyboard: return "keyboard";
                case DeviceKind.Mouse: return "mouse";
                case DeviceKind.Headset: return "headset";
                case DeviceKind.Controller: return "controller";
                case DeviceKind.Speaker: return "speaker";
                case DeviceKind.Microphone: return "microphone";
                case DeviceKind.Dock: return "dock";
                default: return "other";
            }
        }

        public static List<InventoryCategory> Build(List<InventoryRow> rows)
        {
            var groups = new List<InventoryDeviceGroup>();
            foreach (var entry in rows.Select((row, index) => new { Row = row, Index = index })
                .GroupBy(item => string.IsNullOrEmpty(item.Row.GroupId)
                    ? (!string.IsNullOrEmpty(item.Row.Path) ? "interface:" + item.Row.Path : "unknown:" + item.Index.ToString(CultureInfo.InvariantCulture))
                    : item.Row.GroupId, StringComparer.Ordinal))
            {
                var interfaces = entry.Select(item => item.Row)
                    .OrderBy(row => InterfaceNumber(row.Interface))
                    .ThenBy(row => row.Usage, StringComparer.Ordinal)
                    .ToList();
                string kind = new[] { "mouse", "keyboard", "headset", "microphone", "dock", "receiver" }
                    .FirstOrDefault(candidate => interfaces.Any(row => row.KindId == candidate)) ?? "other";
                InventoryRow representative = interfaces.Where(row => row.KindId == kind)
                    .OrderByDescending(row => row.IsSupported).FirstOrDefault() ?? interfaces[0];
                groups.Add(new InventoryDeviceGroup
                {
                    Id = entry.Key,
                    Name = DeviceName(representative.Name),
                    KindId = kind,
                    VidPid = string.Join(" / ", interfaces.Select(row => row.VidPid).Distinct()),
                    Interfaces = interfaces
                });
            }

            return groups.GroupBy(group => group.KindId)
                .OrderBy(group => CategoryOrder(group.Key))
                .Select(group => new InventoryCategory
                {
                    KindId = group.Key,
                    Name = CategoryLabel(group.Key),
                    Devices = group.OrderByDescending(device => device.Supported)
                        .ThenBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
                        .ThenBy(device => device.VidPid, StringComparer.Ordinal)
                        .ThenBy(device => device.Id, StringComparer.Ordinal).ToList()
                }).ToList();
        }

        public static string CategoryLabel(string kind)
        {
            switch (kind)
            {
                case "keyboard": return "键盘";
                case "mouse": return "鼠标";
                case "headset": return "耳机";
                case "receiver": return "接收器";
                case "controller": return "手柄";
                case "speaker": return "音箱";
                case "microphone": return "麦克风";
                case "dock": return "拓展坞";
                default: return "其他设备";
            }
        }

        private static int CategoryOrder(string kind)
        {
            switch (kind)
            {
                case "keyboard": return 0;
                case "mouse": return 1;
                case "headset": return 2;
                case "receiver": return 3;
                case "controller": return 4;
                case "speaker": return 5;
                case "microphone": return 6;
                case "dock": return 7;
                default: return 8;
            }
        }

        private static string DeviceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "未知设备";
            int cut = name.IndexOf("  ·  IF", StringComparison.Ordinal);
            return cut > 0 ? name.Substring(0, cut) : name;
        }

        private static int InterfaceNumber(string value)
        {
            int number;
            return int.TryParse(value, out number) ? number : int.MaxValue;
        }
    }
}
