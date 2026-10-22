using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace GearPower
{
    // Read-only Apple rechargeable keyboard query. Report 0x90 is documented by
    // gozaltech/mkBatteryChecker and fixtan/MagicKeyBattery. This is deliberately
    // limited to the 2015 keyboard and 2017 numeric model; other Apple devices
    // must not be decoded using the same layout without hardware verification.
    public sealed class AppleKeyboardProtocol : HidProtocol
    {
        public override string Name { get { return "Apple 键盘电量"; } }

        public override bool Matches(DeviceGroup group)
        {
            return group.Interfaces.Any(IsKeyboard);
        }

        private static bool IsKeyboard(HidInterfaceInfo iface)
        {
            return (iface.Vid == 0x004C || iface.Vid == 0x05AC)
                && (iface.Pid == 0x0267 || iface.Pid == 0x026C);
        }

        public override ProbeResult Probe(DeviceGroup group)
        {
            if (!Matches(group)) return null;
            var attempts = new List<string>();
            // The consumer-control collection is accessible without taking over
            // the keyboard's input stream. No SetFeature/Write/driver changes.
            foreach (HidInterfaceInfo iface in group.Interfaces.Where(IsKeyboard)
                .OrderBy(i => i.UsagePage == 0x0C ? 0 : 1))
            {
                string error;
                using (HidHandle handle = HidHandle.Open(iface.Path, false, out error))
                {
                    string label = iface.UsageText;
                    if (handle == null)
                    {
                        attempts.Add(label + " 打开失败: " + error);
                        continue;
                    }
                    // Some Windows stacks require a larger buffer than the
                    // three-byte reply, as observed by MagicKeyBattery.
                    byte[] response = new byte[Math.Max(8, iface.InputLength)];
                    response[0] = 0x90;
                    response[2] = 0xFF;
                    bool ok = handle.GetInputReport(response);
                    int nativeError = Marshal.GetLastWin32Error();
                    int? percent = ok ? ParseBattery(response) : null;
                    if (percent.HasValue)
                    {
                        ProbeResult result = Result(percent, false, percent == 100, false, Name,
                            "只读 Input 0x90 · " + label);
                        result.Kind = DeviceKind.Keyboard;
                        return result;
                    }
                    attempts.Add(label + (ok ? " 响应无有效电量" : " Input 0x90 读取失败 (" + nativeError + ")"));
                }
            }

            // Keep a connected keyboard visible, without inventing a battery
            // level or incorrectly marking it as asleep. System battery property
            // fallback can still supply a real level later in Scanner.Scan.
            return new ProbeResult
            {
                DeviceDetected = true,
                Kind = DeviceKind.Keyboard,
                Protocol = Name,
                Note = "已识别苹果键盘，当前驱动未返回电量。" + string.Join("；", attempts.ToArray())
                    + "。可检查第三方键盘过滤驱动；不会自动修改驱动或蓝牙配对。"
            };
        }

        public static int? ParseBattery(byte[] response)
        {
            if (response == null || response.Length < 3 || response[0] != 0x90 || response[2] > 100)
                return null;
            return response[2];
        }
    }
}
