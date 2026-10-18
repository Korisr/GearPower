using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GearPower
{
    public static class SelfTest
    {
        public static bool Run()
        {
            var lines = new List<string>();
            int passed = 0;
            int failed = 0;

            Action<string, bool> check = (name, condition) =>
            {
                if (condition)
                {
                    passed++;
                    lines.Add("PASS  " + name);
                }
                else
                {
                    failed++;
                    lines.Add("FAIL  " + name);
                }
            };

            try
            {
                byte[] descriptor =
                {
                    0x05, 0x06, 0x09, 0x01, 0x15, 0x00, 0x25, 0x64, 0x75, 0x08, 0x95, 0x01, 0x85, 0x03, 0xB1, 0x02,
                    0x05, 0x06, 0x09, 0x20, 0x15, 0x00, 0x25, 0x64, 0x75, 0x08, 0x95, 0x01, 0x85, 0x03, 0xB1, 0x02
                };
                HidReportDescriptor parsed = HidReportDescriptor.Parse(descriptor);
                HidReportField battery = parsed.FindBattery();
                check("descriptor parses battery field", battery != null);
                check("battery field bit offset is 8", battery != null && battery.BitOffset == 8);
                check("battery field is a feature report", battery != null && battery.Type == HidReportType.Feature);
                check("descriptor uses report ids", parsed.UsesReportIds);

                byte[] featureBuffer = { 0x03, 0x05, 0x57 };
                check("feature battery extraction = 87",
                    HidReportDescriptor.ExtractBits(featureBuffer, 1, 8, 8, 100) == 87);
                byte[] unnumbered = { 0x05, 0x57 };
                check("unnumbered battery extraction = 87",
                    HidReportDescriptor.ExtractBits(unnumbered, 0, 8, 8, 100) == 87);

                byte[] razer = RazerProtocol.BuildRequest(91, 0x1F);
                check("razer request length 91", razer.Length == 91);
                check("razer request header", razer[6] == 0x02 && razer[7] == 0x07 && razer[8] == 0x80);
                check("razer checksum 0x85", razer[89] == 0x85);
                byte[] razerReply = new byte[91];
                razerReply[1] = 0x02;
                razerReply[10] = 128;
                check("razer battery = 50%", RazerProtocol.ParseBattery(razerReply) == 50);
                check("razer rejects empty reply", RazerProtocol.ParseBattery(new byte[3]) == null);

                byte[] nordic = Nordic52Protocol.BuildRequest();
                check("nordic52 packet", nordic.Length == 17 && nordic[0] == 0x08 && nordic[1] == 0x04 && nordic[16] == 0x49);
                byte[] nordicReply = new byte[17];
                nordicReply[0] = 0x08;
                nordicReply[1] = 0x04;
                nordicReply[6] = 35;
                bool wired;
                int? nordicPercent = Nordic52Protocol.ParseBattery(nordicReply, out wired);
                check("nordic52 battery = 35%", nordicPercent == 35 && !wired);

                byte[] nordic54 = Nordic54Protocol.BuildRequest(1);
                check("nordic54 packet", nordic54.Length == 64 && nordic54[2] == 0x72 && nordic54[6] == 0x07);
                byte[] nordic54Reply = new byte[64];
                nordic54Reply[1] = 0x72;
                nordic54Reply[5] = 0x07;
                nordic54Reply[7] = 100;
                bool answered;
                check("nordic54 battery = 100%", Nordic54Protocol.ParseBattery(nordic54Reply, out answered) == 100 && answered);

                byte[] lamzu = new byte[9];
                lamzu[1] = 0xA1;
                lamzu[6] = 0x83;
                lamzu[7] = 1;
                lamzu[8] = 87;
                bool lamzuCharging;
                check("lamzu battery = 87% charging", LamzuProtocol.ParseBattery(lamzu, out lamzuCharging) == 87 && lamzuCharging);

                byte[] asus = new byte[12];
                asus[1] = 0x12;
                asus[2] = 0x07;
                asus[5] = 76;
                asus[10] = 1;
                bool asusCharging;
                bool asusFull;
                check("asus battery = 76% charging", AsusProtocol.ParseBattery(asus, out asusCharging, out asusFull) == 76 && asusCharging);

                byte[] hyperx = { 0x21, 0xBB, 0x0B, 55 };
                check("hyperx alpha battery = 55%", HyperXProtocol.ParseAlpha(hyperx) == 55);

                int flightPercent;
                bool flightCharging;
                byte[] flight = { 0x21, 0xFF, 0x05, 0x0E, 0x94 };
                bool flightOk = HyperXProtocol.ParseFlight(flight, out flightPercent, out flightCharging);
                check("hyperx flight battery = 25%", flightOk && flightPercent == 25 && !flightCharging);
                byte[] flightChargingPacket = { 0x21, 0xFF, 0x05, 0x10, 0x14 };
                bool chargingOk = HyperXProtocol.ParseFlight(flightChargingPacket, out flightPercent, out flightCharging);
                check("hyperx flight charging", chargingOk && flightCharging);

                int flight20Percent;
                bool flight20Charging;
                bool flight20Full;
                byte[] flight20 = { 0x21, 0xFF, 0x05, 0x0F, 0x50 };
                bool flight20Ok = HyperXProtocol.ParseFlight20(flight20, out flight20Percent, out flight20Charging, out flight20Full);
                check("hyperx flight 20-byte gauge = 85%", flight20Ok && flight20Percent == 85);
                byte[] flight20Mid = { 0x21, 0xFF, 0x05, 0x0E, 0xCD };
                HyperXProtocol.ParseFlight20(flight20Mid, out flight20Percent, out flight20Charging, out flight20Full);
                check("hyperx flight 20-byte mid = 50%", flight20Percent == 50);
                byte[] flight20ChargingPacket = { 0x21, 0xFF, 0x05, 0x10, 0x05 };
                bool flight20ChargeOk = HyperXProtocol.ParseFlight20(flight20ChargingPacket, out flight20Percent, out flight20Charging, out flight20Full);
                check("hyperx flight 20-byte charging", flight20ChargeOk && flight20Charging && flight20Percent == -1);

                check("known model name", L10n.DisplayName(0x373B, 0x1031, "USB Receiver") == "ATK F1 Ultimate");
                check("headset kind by name", L10n.KindFromName("HyperX Cloud III Wireless") == DeviceKind.Headset);
                check("generic name detection", L10n.IsGenericName("2.4G Wireless Receiver"));
                check("empty descriptor is safe", HidReportDescriptor.Parse(new byte[0]).Fields.Count == 0);
                check("garbage descriptor is safe", HidReportDescriptor.Parse(new byte[] { 0xFF, 0xFF, 0xFF, 0x00 }).Fields.Count == 0);
            }
            catch (Exception ex)
            {
                failed++;
                lines.Add("EXCEPTION  " + ex);
            }

            lines.Add("");
            lines.Add("RESULT  passed=" + passed + " failed=" + failed);
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log");
                File.WriteAllLines(path, lines, Encoding.UTF8);
                lines.Add("LOG  " + path);
            }
            catch
            {
            }
            return failed == 0;
        }
    }
}
