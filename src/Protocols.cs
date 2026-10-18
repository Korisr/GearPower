using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace GearPower
{
    public sealed class DeviceGroup
    {
        public string Key;
        public string Name;
        public string Vendor;
        public string Product;
        public List<HidInterfaceInfo> Interfaces = new List<HidInterfaceInfo>();
        public KnownModel Known;
        public DeviceKind Kind;
        public bool Bluetooth;

        public ushort Vid
        {
            get { return Interfaces.Count > 0 ? Interfaces[0].Vid : (ushort)0; }
        }

        public ushort Pid
        {
            get { return Interfaces.Count > 0 ? Interfaces[0].Pid : (ushort)0; }
        }

        public HidInterfaceInfo FirstWithUsage(ushort usagePage, ushort usage)
        {
            return Interfaces.FirstOrDefault(i => i.UsagePage == usagePage && i.Usage == usage);
        }
    }

    public abstract class HidProtocol
    {
        public abstract string Name { get; }

        public abstract bool Matches(DeviceGroup group);

        public abstract ProbeResult Probe(DeviceGroup group);

        protected static HidInterfaceInfo FindByUsage(DeviceGroup group, ushort page, ushort usage)
        {
            return group.Interfaces.FirstOrDefault(i => i.UsagePage == page && i.Usage == usage);
        }

        protected static ProbeResult Result(int? percent, bool charging, bool full, bool asleep, string protocol, string note)
        {
            var r = new ProbeResult();
            r.Percent = percent;
            r.Charging = charging;
            r.Full = full;
            r.Asleep = asleep;
            r.Protocol = protocol;
            r.Note = note;
            return r;
        }

        protected static int ClampPercent(int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        protected static byte[] Pad(byte[] data, int length)
        {
            if (data.Length == length) return data;
            var result = new byte[length];
            Buffer.BlockCopy(data, 0, result, 0, Math.Min(data.Length, length));
            return result;
        }
    }

    public static class ProtocolRegistry
    {
        private static readonly HidProtocol[] Protocols = new HidProtocol[]
        {
            new AsusProtocol(),
            new RazerProtocol(),
            new AtkProtocol(),
            new LamzuProtocol(),
            new HyperXProtocol(),
            new LogitechProtocol(),
            new GenericHidBatteryProtocol()
        };

        public static ProbeResult Probe(DeviceGroup group)
        {
            foreach (HidProtocol protocol in Protocols)
            {
                try
                {
                    if (!protocol.Matches(group)) continue;
                    ProbeResult result = protocol.Probe(group);
                    if (result != null)
                    {
                        if (string.IsNullOrEmpty(result.ModelName)) result.ModelName = group.Name;
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(protocol.Name + " probe failed for " + group.Name + ": " + ex.Message);
                }
            }
            return null;
        }
    }

    // ---------------------------------------------------------------------
    // ASUS ROG (0x0B05): feature report [id, 0x12, 0x07], battery at byte 5,
    // charging flag at byte 10. Protocol documented by G-Helper.
    // ---------------------------------------------------------------------
    public sealed class AsusProtocol : HidProtocol
    {
        public override string Name
        {
            get { return "ASUS HID"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            if (group.Vid != 0x0B05) return false;
            string name = (group.Name + " " + group.Product).ToLowerInvariant();
            if (name.Contains("rog") || name.Contains("asus") || name.Contains("tuf") || name.Contains("strix")
                || name.Contains("delta") || name.Contains("cetra") || name.Contains("pelta") || name.Contains("clavis")
                || name.Contains("harpe") || name.Contains("gladius") || name.Contains("keris") || name.Contains("chakram")
                || name.Contains("spatha") || name.Contains("pugio") || name.Contains("azoth") || name.Contains("falchion"))
                return true;
            return false;
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            var candidates = group.Interfaces
                .Where(i => i.FeatureLength >= 6 && (i.UsagePage >= 0xFF00 || i.UsagePage == 0x0001))
                .OrderByDescending(i => i.UsagePage >= 0xFF00)
                .ToList();

            foreach (HidInterfaceInfo iface in candidates)
            {
                foreach (byte reportId in new byte[] { 0x00, 0x02, 0x05 })
                {
                    byte[] request = BuildRequest(iface.FeatureLength, reportId);
                    byte[] response;
                    if (!Transact(iface, request, out response)) continue;
                    bool charging;
                    bool full;
                    int? percent = ParseBattery(response, out charging, out full);
                    if (!percent.HasValue) continue;
                    return Result(percent, charging, full, false, Name, "ASUS 0x12/0x07 · 报告 ID " + reportId);
                }
            }
            return null;
        }

        public static byte[] BuildRequest(int featureLength, byte reportId)
        {
            int length = Math.Max(6, featureLength);
            var buffer = new byte[length];
            buffer[0] = reportId;
            buffer[1] = 0x12;
            buffer[2] = 0x07;
            return buffer;
        }

        public static int? ParseBattery(byte[] response, out bool charging, out bool full)
        {
            charging = false;
            full = false;
            if (response == null || response.Length < 6) return null;
            if (response[1] != 0x12 || response[2] != 0x07) return null;
            int percent = response[5];
            if (percent < 0 || percent > 100) return null;
            charging = response.Length > 10 && response[10] != 0;
            full = charging && percent >= 100;
            return percent;
        }

        private static bool Transact(HidInterfaceInfo iface, byte[] request, out byte[] response)
        {
            response = null;
            string error;
            using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
            {
                if (handle == null) return false;
                if (!handle.SetFeature(request)) return false;
                Thread.Sleep(140);
                var buffer = new byte[request.Length];
                buffer[0] = request[0];
                if (!handle.GetFeature(buffer)) return false;
                response = buffer;
                return true;
            }
        }
    }

    // ---------------------------------------------------------------------
    // Razer (0x1532): 90-byte feature report, command class 0x02 / id 0x07,
    // XOR checksum, raw battery at data byte 9 (buffer byte 10).
    // ---------------------------------------------------------------------
    public sealed class RazerProtocol : HidProtocol
    {
        private const byte TransactionId = 0x1F;

        public override string Name
        {
            get { return "Razer HID"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            return group.Vid == 0x1532;
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            var candidates = group.Interfaces.Where(i => i.FeatureLength >= 91).ToList();
            foreach (HidInterfaceInfo iface in candidates)
            {
                foreach (byte tx in new byte[] { 0x1F, 0x3F, 0xFF })
                {
                    byte[] request = BuildRequest(iface.FeatureLength, tx);
                    string error;
                    using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
                    {
                        if (handle == null) continue;
                        if (!handle.SetFeature(request)) continue;
                        Thread.Sleep(340);
                        var response = new byte[request.Length];
                        response[0] = 0;
                        if (!handle.GetFeature(response)) continue;
                        int? percent = ParseBattery(response);
                        if (!percent.HasValue) continue;
                        return Result(percent, false, percent.Value >= 100, percent.Value == 0,
                            Name, "Razer 90 字节特性报告 · TX " + tx.ToString("X2"));
                    }
                }
            }
            return null;
        }

        public static byte[] BuildRequest(int featureLength, byte transactionId)
        {
            int length = Math.Max(91, featureLength);
            var data = new byte[length];
            // data (after report id at index 0): status, tx, 0,0,0, class 0x02, cmd 0x07, 0x80
            data[0] = 0x00;
            data[1] = 0x00;
            data[2] = transactionId;
            data[3] = 0x00;
            data[4] = 0x00;
            data[5] = 0x00;
            data[6] = 0x02;
            data[7] = 0x07;
            data[8] = 0x80;
            byte crc = 0;
            for (int i = 3; i <= 8; i++) crc ^= data[i];
            data[89] = crc;
            data[90] = 0x00;
            return data;
        }

        public static int? ParseBattery(byte[] response)
        {
            if (response == null || response.Length < 11) return null;
            if (response[1] != 0x02) return null;
            int raw = response[10];
            return ClampPercent((int)Math.Round(raw / 255.0 * 100.0));
        }
    }

    // ---------------------------------------------------------------------
    // Compx / Nordic 52840 (ATK, VXE, VGN, ...): output report id 8, 17 bytes.
    // ---------------------------------------------------------------------
    public sealed class Nordic52Protocol : HidProtocol
    {
        private static readonly ushort[] VendorIds = new ushort[] { 0x373B, 0x3554, 0x260D, 0x33E4 };

        public override string Name
        {
            get { return "ATK / VXE 私有协议"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            return VendorIds.Contains(group.Vid) && group.Interfaces.Any(i => i.OutputLength >= 17);
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            HidInterfaceInfo iface = FindByUsage(group, 0xFF02, 0x0002);
            if (iface == null)
            {
                iface = group.Interfaces
                    .Where(i => i.OutputLength >= 17)
                    .OrderByDescending(i => i.UsagePage >= 0xFF00)
                    .FirstOrDefault();
            }
            if (iface == null) return null;
            byte[] request = Pad(BuildRequest(), Math.Max(17, iface.OutputLength));
            string error;
            using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
            {
                if (handle == null)
                {
                    Log.Debug("Nordic52 open failed: " + error);
                    return null;
                }
                byte[] response = null;
                bool sent = handle.Write(request, 500);
                if (!sent) sent = handle.SetOutputReport(request);
                if (sent) response = handle.Read(17, 700);
                if (response == null)
                {
                    var buffer = new byte[17];
                    if (handle.GetInputReport(buffer)) response = buffer;
                }
                bool wired;
                int? percent = ParseBattery(response, out wired);
                if (!percent.HasValue)
                {
                    Log.Debug("Nordic52 no reply on " + iface.UsageText + " out=" + iface.OutputLength);
                    return null;
                }
                return Result(percent, wired && percent.Value < 100, wired && percent.Value >= 100, false,
                    Name, wired ? "有线连接" : "2.4G 接收器");
            }
        }

        public static byte[] BuildRequest()
        {
            var report = new byte[17];
            report[0] = 0x08;
            report[1] = 0x04;
            report[16] = 0x49;
            return report;
        }

        public static int? ParseBattery(byte[] response, out bool wired)
        {
            wired = false;
            if (response == null || response.Length < 8) return null;
            if (response[0] != 0x08 || response[1] != 0x04) return null;
            int percent = response[6];
            if (percent < 0 || percent > 100) return null;
            wired = response[7] != 0;
            return percent;
        }
    }

    // ---------------------------------------------------------------------
    // Compx / Nordic 54L15 (ATK Zero): 64-byte report, command 0x72, sub 7.
    // ---------------------------------------------------------------------
    public sealed class Nordic54Protocol : HidProtocol
    {
        public override string Name
        {
            get { return "ATK Nordic 54"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            if (group.Vid != 0x373B && group.Vid != 0x3554 && group.Vid != 0x260D && group.Vid != 0x33E4) return false;
            return group.Interfaces.Any(i => i.OutputLength >= 64);
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            HidInterfaceInfo iface = FindByUsage(group, 0xFF05, 0x0001);
            if (iface == null)
            {
                iface = group.Interfaces
                    .Where(i => i.OutputLength >= 64)
                    .OrderByDescending(i => i.UsagePage >= 0xFF00)
                    .FirstOrDefault();
            }
            if (iface == null) return null;
            foreach (byte link in new byte[] { 0x01, 0x00 })
            {
                byte[] request = BuildRequest(link);
                string error;
                using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
                {
                    if (handle == null)
                    {
                        Log.Debug("Nordic54 open failed: " + error);
                        return null;
                    }
                    if (!handle.Write(request, 500) && !handle.SetOutputReport(request))
                    {
                        Log.Debug("Nordic54 write failed on " + iface.UsageText);
                        continue;
                    }                    byte[] response = handle.Read(64, 800);
                    bool answered;
                    int? percent = ParseBattery(response, out answered);
                    if (!percent.HasValue)
                    {
                        Log.Debug("Nordic54 no reply on " + iface.UsageText + " link=" + link);
                        continue;
                    }
                    if (!answered) return Result(null, false, false, true, Name, "接收器在线，设备未唤醒");
                    bool wired = link == 0x00;
                    return Result(percent, wired && percent.Value < 100, wired && percent.Value >= 100, false,
                        Name, wired ? "有线连接" : "2.4G 接收器");
                }
            }
            return null;
        }

        public static byte[] BuildRequest(byte link)
        {
            var report = new byte[64];
            report[0] = 0x08;
            report[1] = 0x7D;
            report[2] = 0x72;
            report[3] = 0x02;
            report[5] = link;
            report[6] = 0x07;
            report[7] = 0x01;
            return report;
        }

        public static int? ParseBattery(byte[] response, out bool answered)
        {
            answered = false;
            if (response == null || response.Length < 10) return null;
            if (response[1] != 0x72 || response[5] != 0x07) return null;
            answered = response[2] == 0x00;
            if (!answered) return 0;
            int percent = response[7];
            if (percent < 0 || percent > 100) return null;
            return percent;
        }
    }

    // ---------------------------------------------------------------------
    // ATK / VXE / VGN and other Compx-based 2.4G devices. The receiver can
    // expose the battery on a 64-byte (Nordic 54) or 17-byte (Nordic 52)
    // report; try both before reporting the receiver as "asleep".
    // ---------------------------------------------------------------------
    public sealed class AtkProtocol : HidProtocol
    {
        private static readonly ushort[] VendorIds = new ushort[] { 0x373B, 0x3554, 0x260D, 0x33E4 };

        public override string Name
        {
            get { return "ATK / VXE 2.4G"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            if (group.Known != null && group.Known.Kind == DeviceKind.Headset && !group.Known.Wireless)
                return false;
            return VendorIds.Contains(group.Vid) && group.Interfaces.Any(i => i.OutputLength >= 17);
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            if (group.Interfaces.Any(i => i.OutputLength >= 64))
            {
                ProbeResult sixtyFour = new Nordic54Protocol().Probe(group);
                if (sixtyFour != null) return sixtyFour;
            }
            ProbeResult seventeen = new Nordic52Protocol().Probe(group);
            if (seventeen != null) return seventeen;
            if (group.Known != null && group.Known.Wireless)
                return Result(null, false, false, true, Name, "接收器在线，设备未唤醒");
            return null;
        }
    }

    // ---------------------------------------------------------------------
    // Lamzu (0x373E / 0x37B0): 65-byte feature report, command 0x83.
    // ---------------------------------------------------------------------
    public sealed class LamzuProtocol : HidProtocol
    {
        public override string Name
        {
            get { return "Lamzu HID"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            return group.Vid == 0x373E || group.Vid == 0x37B0;
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            var candidates = group.Interfaces.Where(i => i.FeatureLength >= 9).ToList();
            foreach (HidInterfaceInfo iface in candidates)
            {
                byte[] request = BuildRequest(iface.FeatureLength);
                string error;
                using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
                {
                    if (handle == null)
                    {
                        Log.Debug("Lamzu open failed: " + error);
                        continue;
                    }
                    if (!handle.SetFeature(request))
                    {
                        Log.Debug("Lamzu set feature failed on " + iface.UsageText);
                        continue;
                    }
                    Thread.Sleep(140);
                    var response = new byte[request.Length];
                    if (!handle.GetFeature(response))
                    {
                        Log.Debug("Lamzu get feature failed on " + iface.UsageText);
                        continue;
                    }
                    bool charging;
                    int? percent = ParseBattery(response, out charging);
                    if (!percent.HasValue)
                    {
                        Log.Debug("Lamzu unexpected reply " + BitConverter.ToString(response, 0, Math.Min(10, response.Length)));
                        continue;
                    }
                    return Result(percent, charging && percent.Value < 100, charging && percent.Value >= 100,
                        false, Name, "Lamzu 0x83 特性报告");
                }
            }
            if (group.Known != null && group.Known.Wireless)
                return Result(null, false, false, true, Name, "接收器在线，设备未唤醒");
            return null;
        }

        public static byte[] BuildRequest(int featureLength)
        {
            int length = Math.Max(65, featureLength);
            var report = new byte[length];
            report[0] = 0x00;
            report[3] = 0x02;
            report[4] = 0x02;
            report[6] = 0x83;
            return report;
        }

        public static int? ParseBattery(byte[] response, out bool charging)
        {
            charging = false;
            if (response == null || response.Length < 9) return null;
            if (response[1] != 0xA1 || response[6] != 0x83) return null;
            charging = response[7] == 0x01;
            int percent = response[8];
            if (percent < 0 || percent > 100) return null;
            return percent;
        }
    }

    // ---------------------------------------------------------------------
    // HyperX: Cloud Alpha Wireless (03F0:098D) and Cloud III Wireless
    // (03F0:05B7). Protocols documented by alphabat and the Linux kernel.
    // ---------------------------------------------------------------------
    public sealed class HyperXProtocol : HidProtocol
    {
        private static readonly ushort[] FlightPids = new ushort[] { 0x0E90, 0x1723, 0x1749, 0x16C4, 0x1718 };
        private static readonly ushort[] FlightThresholds = new ushort[]
        {
            3328, 3584, 3674, 3704, 3732, 3744, 3754, 3764, 3774, 3784,
            3794, 3804, 3824, 3840, 3860, 3890, 3910, 3940, 3960, 3970
        };
        private static readonly byte[] FlightPercents = new byte[]
        {
            5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60, 65, 70, 75, 80, 85, 90, 95, 100
        };

        public override string Name
        {
            get { return "HyperX HID"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            if (group.Vid != 0x03F0 && group.Vid != 0x0951) return false;
            if (group.Known != null && group.Known.Kind == DeviceKind.Headset && !group.Known.Wireless)
                return false;
            if (FlightPids.Contains(group.Pid)) return true;
            if (group.Pid == 0x098D || group.Pid == 0x05B7) return true;
            string name = (group.Name + " " + group.Product).ToLowerInvariant();
            return name.Contains("hyperx") || name.Contains("cloud");
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            if (FlightPids.Contains(group.Pid)) return ProbeFlight(group);
            if (group.Pid == 0x05B7) return ProbeCloudIII(group);
            return ProbeAlpha(group);
        }

        private ProbeResult ProbeFlight(DeviceGroup group)
        {
            // Cloud Flight / 0951:1723 uses a 20-byte report on its FF90 control
            // collection: 21 FF 05, reply byte 3 = charge state, byte 4 = gauge.
            var small = group.Interfaces
                .Where(i => i.OutputLength >= 5 && i.OutputLength <= 32)
                .OrderBy(i => Math.Abs(i.OutputLength - 20))
                .ThenByDescending(i => i.UsagePage == 0xFF90)
                .ToList();
            foreach (HidInterfaceInfo iface in small)
            {
                int length = Math.Max(20, iface.OutputLength);
                var request = new byte[length];
                request[0] = 0x21;
                request[1] = 0xFF;
                request[2] = 0x05;
                string error;
                using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
                {
                    if (handle == null) continue;
                    if (!handle.Write(request, 500) && !handle.SetFeature(request))
                    {
                        Log.Debug("HyperX Flight write failed on " + iface.UsageText);
                        continue;
                    }
                    byte[] response = handle.Read(length, 700);
                    int percent;
                    bool charging;
                    bool full;
                    if (!ParseFlight20(response, out percent, out charging, out full))
                    {
                        Log.Debug("HyperX Flight unexpected reply on " + iface.UsageText + ": "
                            + (response == null ? "null" : BitConverter.ToString(response)));
                        continue;
                    }
                    return Result(percent >= 0 ? (int?)percent : null, charging, full, false, Name,
                        charging ? "HyperX Cloud Flight · 充电中" : "HyperX Cloud Flight 20 字节电量包");
                }
            }

            // Fallback for variants that expose the 64-byte voltage packet.
            var candidates = group.Interfaces
                .Where(i => i.OutputLength >= 64)
                .OrderByDescending(i => i.UsagePage == 0xFF00)
                .ToList();
            foreach (HidInterfaceInfo iface in candidates)
            {
                var request = new byte[Math.Max(64, iface.OutputLength)];
                request[0] = 0x21;
                request[1] = 0xFF;
                request[2] = 0x05;
                string error;
                using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
                {
                    if (handle == null) continue;
                    if (!handle.Write(request, 500) && !handle.SetFeature(request))
                    {
                        Log.Debug("HyperX Flight write failed on " + iface.UsageText);
                        continue;
                    }
                    DateTime deadline = DateTime.UtcNow.AddMilliseconds(900);
                    while (DateTime.UtcNow < deadline)
                    {
                        byte[] response = handle.Read(64, 250);
                        if (response == null) continue;
                        int percent;
                        bool charging;
                        if (!ParseFlight(response, out percent, out charging)) continue;
                        return Result(percent >= 0 ? (int?)percent : null, charging, false, false,
                            Name, charging ? "HyperX Cloud Flight · 充电中" : "HyperX Cloud Flight 电量包");
                    }
                    Log.Debug("HyperX Flight no reply on " + iface.UsageText);
                }
            }
            if (small.Count > 0 || candidates.Count > 0)
                return Result(null, false, false, true, Name, "接收器在线，耳机未开机");
            return null;
        }

        public static bool ParseFlight20(byte[] response, out int percent, out bool charging, out bool full)
        {
            percent = -1;
            charging = false;
            full = false;
            if (response == null || response.Length < 5) return false;
            if (response[0] != 0x21 || response[1] != 0xFF || response[2] != 0x05) return false;

            int state = response[3];
            int gauge = response[4] != 0 ? response[4] : state;

            if (state == 0x10)
            {
                if (gauge >= 11)
                {
                    percent = 100;
                    full = true;
                }
                else
                {
                    charging = true;
                }
                return true;
            }
            if (state == 0x0F)
            {
                if (gauge >= 130) percent = 100;
                else if (gauge >= 120) percent = 95;
                else if (gauge >= 100) percent = 90;
                else if (gauge >= 70) percent = 85;
                else if (gauge >= 50) percent = 80;
                else if (gauge >= 20) percent = 75;
                else percent = 70;
                return true;
            }
            if (state == 0x0E)
            {
                if (gauge > 240) percent = 65;
                else if (gauge >= 220) percent = 60;
                else if (gauge >= 208) percent = 55;
                else if (gauge >= 200) percent = 50;
                else if (gauge >= 190) percent = 45;
                else if (gauge >= 180) percent = 40;
                else if (gauge >= 169) percent = 35;
                else if (gauge >= 159) percent = 30;
                else if (gauge >= 148) percent = 25;
                else if (gauge >= 119) percent = 20;
                else if (gauge >= 90) percent = 15;
                else percent = 10;
                return true;
            }
            return false;
        }

        public static bool ParseFlight(byte[] response, out int percent, out bool charging)
        {
            percent = -1;
            charging = false;
            if (response == null || response.Length < 5) return false;
            if (response[0] != 0x21 || response[1] != 0xFF || response[2] != 0x05) return false;
            int upper = response[3];
            int lower = response[4];
            if ((upper == 16 && lower >= 20) || upper >= 17)
            {
                charging = true;
                return true;
            }
            int millivolts = (upper << 8) | lower;
            int index = 0;
            for (int i = 0; i < FlightThresholds.Length; i++)
            {
                if (millivolts >= FlightThresholds[i]) index = i;
                else break;
            }
            percent = FlightPercents[index];
            return true;
        }

        private ProbeResult ProbeAlpha(DeviceGroup group)
        {
            var candidates = group.Interfaces.Where(i => i.OutputLength >= 3).ToList();
            foreach (HidInterfaceInfo iface in candidates)
            {
                byte[] request = Pad(new byte[] { 0x21, 0xBB, 0x0B }, Math.Max(3, iface.OutputLength));
                string error;
                using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
                {
                    if (handle == null) continue;
                    if (!handle.Write(request, 500) && !handle.SetOutputReport(request) && !handle.SetFeature(request)) continue;
                    byte[] response = handle.Read(10, 700);
                    int? percent = ParseAlpha(response);
                    if (!percent.HasValue) continue;
                    return Result(percent, false, percent.Value >= 100, false, Name, "HyperX 0x21BB0B");
                }
            }
            if (group.Known != null && group.Known.Kind == DeviceKind.Headset)
                return Result(null, false, false, true, Name, "接收器在线，耳机未开机");
            return null;
        }

        public static int? ParseAlpha(byte[] response)
        {
            if (response == null || response.Length < 4) return null;
            if (response[2] != 0x0B) return null;
            int percent = response[3];
            if (percent < 0 || percent > 100) return null;
            return percent;
        }

        private ProbeResult ProbeCloudIII(DeviceGroup group)
        {
            var candidates = group.Interfaces.Where(i => i.OutputLength >= 62).ToList();
            foreach (HidInterfaceInfo iface in candidates)
            {
                string error;
                using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
                {
                    if (handle == null) continue;
                    int? percent = QueryCloudIII(handle, iface.OutputLength, iface.FeatureLength, 0x89, 0x0D);
                    if (!percent.HasValue) continue;
                    int? chargingValue = QueryCloudIII(handle, iface.OutputLength, iface.FeatureLength, 0x8A, 0x0C);
                    bool charging = chargingValue.HasValue && chargingValue.Value == 1;
                    return Result(percent, charging && percent.Value < 100, charging && percent.Value >= 100,
                        false, Name, "HyperX Cloud III 0x66");
                }
            }
            if (group.Known != null && group.Known.Kind == DeviceKind.Headset)
                return Result(null, false, false, true, Name, "接收器在线，耳机未开机");
            return null;
        }

        private static int? QueryCloudIII(HidHandle handle, int reportLength, int featureLength, byte command, byte expected)
        {
            int length = Math.Max(62, reportLength);
            var request = new byte[length];
            request[0] = 0x66;
            request[1] = command;
            if (!handle.Write(request, 500) && !handle.SetOutputReport(request))
            {
                if (featureLength < 5) return null;
                request = Pad(request, Math.Max(featureLength, length));
                if (!handle.SetFeature(request)) return null;
            }
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(900);
            while (DateTime.UtcNow < deadline)
            {
                byte[] response = handle.Read(62, 250);
                if (response == null) continue;
                if (response.Length >= 5 && response[1] == expected)
                {
                    if (expected == 0x0C) return response[2];
                    int percent = response[4];
                    if (percent >= 0 && percent <= 100) return percent;
                }
            }
            return null;
        }
    }

    // ---------------------------------------------------------------------
    // Logitech HID++ 2.0 (0x046D): UnifiedBattery feature 0x1004.
    // ---------------------------------------------------------------------
    public sealed class LogitechProtocol : HidProtocol
    {
        private const byte ShortReport = 0x10;
        private const byte LongReport = 0x11;
        private const byte SoftwareId = 0x01;
        private const byte RootFeature = 0x00;
        private const ushort UnifiedBattery = 0x1004;

        public override string Name
        {
            get { return "Logitech HID++ 2.0"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            return group.Vid == 0x046D && group.Interfaces.Any(i => i.UsagePage == 0xFF00);
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            HidInterfaceInfo shortIface = group.Interfaces.FirstOrDefault(i => i.UsagePage == 0xFF00 && i.Usage == 0x0001);
            HidInterfaceInfo longIface = group.Interfaces.FirstOrDefault(i => i.UsagePage == 0xFF00 && i.Usage == 0x0002);
            if (shortIface == null) shortIface = group.Interfaces.FirstOrDefault(i => i.UsagePage == 0xFF00);
            if (longIface == null) longIface = shortIface;
            if (shortIface == null) return null;

            string error;
            using (HidHandle shortHandle = HidHandle.Open(shortIface.Path, true, out error))
            {
                if (shortHandle == null) return null;
                using (HidHandle longHandle = longIface == shortIface ? null : HidHandle.Open(longIface.Path, true, out error))
                {
                    HidHandle longH = longHandle ?? shortHandle;
                    byte dev = ResolveDeviceIndex(shortHandle, longH);
                    if (dev == 0) return null;
                    int featureIndex = FeatureIndex(shortHandle, longH, dev, UnifiedBattery);
                    if (featureIndex <= 0) return null;
                    byte[] response = Call(shortHandle, longH, dev, (byte)featureIndex, 0x01, 0, 0, 0, 320);
                    if (response == null || response.Length < 7) return null;
                    int percent = response[4];
                    if (percent < 0 || percent > 100) return null;
                    int chargingStatus = response[6];
                    bool charging = chargingStatus == 1 || chargingStatus == 2 || chargingStatus == 4;
                    bool full = chargingStatus == 3 || percent >= 100;
                    return Result(percent, charging && percent < 100, full, false, Name,
                        "Logitech UnifiedBattery · 设备索引 " + dev);
                }
            }
        }

        private static byte ResolveDeviceIndex(HidHandle shortH, HidHandle longH)
        {
            for (byte dev = 1; dev <= 6; dev++)
            {
                byte[] response = Call(shortH, longH, dev, RootFeature, 0x01, 0, 0, 0xAA, 140);
                if (response != null && response.Length > 6 && response[6] == 0xAA) return dev;
            }
            byte[] direct = Call(shortH, longH, 0xFF, RootFeature, 0x01, 0, 0, 0xAA, 180);
            if (direct != null && direct.Length > 6 && direct[6] == 0xAA) return 0xFF;
            return 0;
        }

        private static int FeatureIndex(HidHandle shortH, HidHandle longH, byte dev, ushort featureId)
        {
            byte[] response = Call(shortH, longH, dev, RootFeature, 0x00,
                (byte)(featureId >> 8), (byte)(featureId & 0xFF), 0, 220);
            if (response == null || response.Length < 5) return 0;
            return response[4];
        }

        private static byte[] Call(HidHandle shortH, HidHandle longH, byte dev, byte featureIndex, byte function, byte p0, byte p1, byte p2, int timeoutMs)
        {
            var request = new byte[7];
            request[0] = ShortReport;
            request[1] = dev;
            request[2] = featureIndex;
            request[3] = (byte)((function << 4) | SoftwareId);
            request[4] = p0;
            request[5] = p1;
            request[6] = p2;
            if (!shortH.Write(request, 250)) return null;

            byte address = request[3];
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                byte[] response = shortH.Read(20, 90);
                if (response != null && IsMatch(response, dev, featureIndex, address)) return response;
                if (longH != shortH)
                {
                    response = longH.Read(20, 90);
                    if (response != null && IsMatch(response, dev, featureIndex, address)) return response;
                }
            }
            return null;
        }

        private static bool IsMatch(byte[] response, byte dev, byte featureIndex, byte address)
        {
            if (response.Length < 4) return false;
            if (response[1] != dev) return false;
            if (response[2] == 0xFF && response[3] == featureIndex) return false;
            return response[2] == featureIndex && response[3] == address;
        }
    }

    // ---------------------------------------------------------------------
    // Generic HID Battery Strength (Usage Page 0x06 / Usage 0x20).
    // ---------------------------------------------------------------------
    public sealed class GenericHidBatteryProtocol : HidProtocol
    {
        public override string Name
        {
            get { return "HID 标准电量"; }
        }

        public override bool Matches(DeviceGroup group)
        {
            return true;
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            foreach (HidInterfaceInfo iface in group.Interfaces)
            {
                HidValueCap field = iface.FindBatteryValueCap();
                if (field == null) continue;

                int? percent = null;
                if (field.ReportType == HidReportType.Feature && iface.FeatureLength > 0)
                {
                    percent = ReadFeature(iface, field);
                }
                else if (field.ReportType == HidReportType.Input && iface.InputLength > 0)
                {
                    percent = ReadInput(iface, field);
                }
                if (!percent.HasValue) continue;
                return Result(percent, false, percent.Value >= 100, false, Name,
                    field.ReportType + " 报告 " + field.ReportId + " · 位偏移 " + field.BitOffset);
            }
            return null;
        }

        private static int? ReadFeature(HidInterfaceInfo iface, HidValueCap field)
        {
            string error;
            using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
            {
                if (handle == null) return null;
                var buffer = new byte[iface.FeatureLength];
                buffer[0] = field.ReportId;
                if (!handle.GetFeature(buffer)) return null;
                return ReadUsage(handle, field, 2, buffer);
            }
        }

        private static int? ReadInput(HidInterfaceInfo iface, HidValueCap field)
        {
            string error;
            using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
            {
                if (handle == null) return null;
                var buffer = new byte[iface.InputLength];
                buffer[0] = field.ReportId;
                if (!handle.GetInputReport(buffer))
                {
                    buffer = handle.Read(iface.InputLength, 250);
                    if (buffer == null) return null;
                }
                return ReadUsage(handle, field, 0, buffer);
            }
        }

        private static int? ReadUsage(HidHandle handle, HidValueCap field, int reportType, byte[] report)
        {
            IntPtr preparsed;
            if (!NativeMethods.HidD_GetPreparsedData(handle.HandleForQuery, out preparsed)) return null;
            try
            {
                ushort[] links = new ushort[] { field.LinkCollection, 0 };
                foreach (ushort link in links)
                {
                    uint raw;
                    int status = NativeMethods.HidP_GetUsageValue(reportType, 0x06, link, 0x20, out raw, preparsed,
                        report, report.Length);
                    if (status < 0) continue;
                    int value = (int)raw;
                    if (value > 100)
                    {
                        int max = field.LogicalMax > 100 ? field.LogicalMax : 255;
                        value = (int)Math.Round(value * 100.0 / max);
                    }
                    if (value >= 0 && value <= 100) return value;
                }
            }
            finally
            {
                NativeMethods.HidD_FreePreparsedData(preparsed);
            }
            return null;
        }
    }
}
