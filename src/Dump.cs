using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GearPower
{
    public static class Dump
    {
        public static void Write()
        {
            var sb = new StringBuilder();
            sb.AppendLine("GearPower HID dump");
            sb.AppendLine("Time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("OS: " + Environment.OSVersion.VersionString);
            sb.AppendLine();

            ScanResult result = Scanner.Scan();
            sb.AppendLine("HID interfaces: " + result.HidCount + "  Bluetooth: " + result.BluetoothCount + "  Seconds: " + result.Seconds.ToString("0.00"));
            sb.AppendLine();

            sb.AppendLine("=== READINGS ===");
            foreach (DeviceReading reading in result.Readings)
            {
                sb.AppendLine(reading.Name + " | " + reading.Vendor + " | " + reading.Transport + " | " + reading.Protocol
                    + " | " + (reading.Percent.HasValue ? reading.Percent.Value + "%" : "?")
                    + (reading.Charging ? " charging" : "") + (reading.Asleep ? " asleep" : ""));
            }
            sb.AppendLine();
            sb.AppendLine("=== INTERFACES ===");
            foreach (InventoryRow row in result.Inventory)
            {
                sb.AppendLine(row.Detail);
                sb.AppendLine("----------------------------------------");
            }

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hid-dump.txt");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Log.Info("Dump written to " + path);
        }
    }
}
