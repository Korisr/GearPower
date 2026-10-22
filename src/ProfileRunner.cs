using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace GearPower
{
    public static class ProfileRunner
    {
        private static List<DeviceProfile> profiles = new List<DeviceProfile>();

        public static void Reload()
        {
            List<string> errors;
            profiles = ProfileStore.Load(out errors);
            foreach (string error in errors) Log.Debug("Profile load: " + error);
        }

        public static ProbeResult TryProbe(DeviceGroup group)
        {
            if (group == null || profiles.Count == 0) return null;
            foreach (DeviceProfile profile in profiles)
            {
                if (!profile.enabled || !string.IsNullOrEmpty(profile.builtinProtocol)) continue;
                ProfileMatch match = null;
                HidInterfaceInfo iface = null;
                foreach (ProfileMatch candidate in profile.matches ?? new ProfileMatch[0])
                {
                    if (candidate == null) continue;
                    HidInterfaceInfo found = FindInterface(candidate, group.Interfaces);
                    if (found == null) continue;
                    match = candidate;
                    iface = found;
                    break;
                }
                if (iface == null) continue;
                ProbeResult result = Run(profile, match, iface, false);
                if (result != null) return result;
            }
            return null;
        }

        public static string TestProfile(DeviceProfile profile)
        {
            ProfileJson.Validate(profile);
            ProfileMatch match = null;
            HidInterfaceInfo iface = null;
            foreach (ProfileMatch candidate in profile.matches ?? new ProfileMatch[0])
            {
                HidInterfaceInfo found = FindInterface(candidate, HidEnumerator.Enumerate());
                if (found == null) continue;
                match = candidate;
                iface = found;
                break;
            }
            if (iface == null) return "未找到匹配的已连接接口。请检查 VID/PID、Usage 和三个报告长度。";

            var trace = new StringBuilder();
            ProbeResult result = Run(profile, match, iface, true, trace);
            if (result == null)
            {
                trace.AppendLine();
                trace.AppendLine("结果：未读到有效电量。");
            }
            else
            {
                trace.AppendLine();
                trace.AppendLine("结果：" + (result.Percent.HasValue ? result.Percent.Value + "%" : "电量未知")
                    + (result.Charging ? " · 充电中" : "")
                    + (result.Asleep ? " · 睡眠/等待" : ""));
            }
            return trace.ToString().TrimEnd();
        }

        private static ProbeResult Run(DeviceProfile profile, ProfileMatch match, HidInterfaceInfo iface, bool trace)
        {
            return Run(profile, match, iface, trace, new StringBuilder());
        }

        private static ProbeResult Run(DeviceProfile profile, ProfileMatch match, HidInterfaceInfo iface,
            bool trace, StringBuilder log)
        {
            if (profile == null || profile.steps == null || profile.result == null) return null;
            var responses = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            string error;
            using (HidHandle handle = HidHandle.Open(iface.Path, true, out error))
            {
                if (handle == null)
                {
                    if (trace) log.AppendLine("打开失败：" + error);
                    return null;
                }

                foreach (ProfileStep step in profile.steps)
                {
                    if (!string.IsNullOrEmpty(step.whenVid) &&
                        ProfileJson.HexId(step.whenVid) != iface.Vid) continue;

                    byte[] response = ExecuteStep(handle, step, iface, trace, log);
                    if (response == null)
                    {
                        if (trace) log.AppendLine("步骤 " + step.id + "：失败"
                            + (string.IsNullOrEmpty(step.failureNote) ? "" : " · " + step.failureNote));
                        return null;
                    }
                    if (!Satisfies(step.require, response))
                    {
                        if (trace) log.AppendLine("步骤 " + step.id + "：条件不满足"
                            + (string.IsNullOrEmpty(step.failureNote) ? "" : " · " + step.failureNote));
                        return null;
                    }
                    responses[step.id] = response;
                }
            }

            byte[] percentBuffer = FindStepBuffer(responses, profile.result.percent.step);
            if (percentBuffer == null) return null;
            bool unavailable = ConditionsMatch(profile.result.percentUnavailableWhen, responses);
            int? percent = null;
            if (!unavailable)
            {
                long raw = ReadField(profile.result.percent, percentBuffer);
                double value = raw * profile.result.percent.scale + profile.result.percent.add;
                int rounded = (int)Math.Round(value);
                if (rounded >= 0 && rounded <= 100) percent = rounded;
            }

            bool? charging = ReadFlag(profile.result.charging, responses);
            bool? full = ReadFlag(profile.result.full, responses);
            ProbeResult result = new ProbeResult();
            result.Percent = percent;
            result.Charging = charging == true;
            result.Full = full == true;
            result.Asleep = false;
            result.Protocol = "设备配置";
            result.Note = string.IsNullOrEmpty(profile.result.note)
                ? "通过设备配置读取"
                : profile.result.note;
            result.ModelName = profile.name;
            result.Kind = ProfileJson.ParseKind(profile.kind);

            if (trace)
            {
                log.AppendLine();
                log.AppendLine("配置：" + profile.id + " · " + profile.name);
                log.AppendLine("匹配：" + iface.VidPid + " · " + iface.UsageText
                    + " · IN " + iface.InputLength + " / OUT " + iface.OutputLength
                    + " / F " + iface.FeatureLength);
            }
            return result;
        }

        private static byte[] ExecuteStep(HidHandle handle, ProfileStep step, HidInterfaceInfo iface,
            bool trace, StringBuilder log)
        {
            for (int attempt = 0; attempt < step.attempts; attempt++)
            {
                byte[] result = null;
                if (step.method == "feature")
                {
                    var buffer = new byte[step.response.length];
                    buffer[0] = (byte)step.reportId;
                    if (handle.GetFeature(buffer, step.timeoutMs))
                        result = buffer;
                }
                else if (step.method == "featureQuery")
                {
                    byte[] request = Pad(ProfileJson.HexBytes(step.request), step.response.length);
                    if (request.Length > 0) request[0] = (byte)step.reportId;
                    ApplyChecksum(request, step.requestChecksum);
                    if (trace) log.AppendLine("TX " + Hex(request));
                    if (handle.SetFeature(request, step.timeoutMs))
                    {
                        Thread.Sleep(30);
                        var buffer = new byte[step.response.length];
                        buffer[0] = (byte)step.reportId;
                        if (handle.GetFeature(buffer, step.timeoutMs))
                            result = buffer;
                    }
                }
                else if (step.method == "output")
                {
                    byte[] request = Pad(ProfileJson.HexBytes(step.request), iface.OutputLength);
                    ApplyChecksum(request, step.requestChecksum);
                    if (trace) log.AppendLine("TX " + Hex(request));
                    if (handle.Write(request, step.timeoutMs))
                    {
                        Thread.Sleep(25);
                        result = handle.Read(step.response.length, step.timeoutMs);
                    }
                }
                else if (step.method == "input")
                {
                    result = handle.Read(step.response.length, step.timeoutMs);
                }

                if (result != null && MatchesResponse(result, step.response))
                {
                    if (trace) log.AppendLine("RX " + Hex(result));
                    return result;
                }
                if (trace && result != null) log.AppendLine("忽略不匹配响应：" + Hex(result));
                Thread.Sleep(40);
            }
            return null;
        }

        private static HidInterfaceInfo FindInterface(ProfileMatch match, IEnumerable<HidInterfaceInfo> interfaces)
        {
            ushort vid;
            ushort pid;
            ushort usagePage;
            ushort usage;
            try
            {
                vid = ProfileJson.HexId(match.vid);
                pid = ProfileJson.HexId(match.pid);
                usagePage = ProfileJson.HexId(match.usagePage);
                usage = ProfileJson.HexId(match.usage);
            }
            catch
            {
                return null;
            }
            return interfaces.FirstOrDefault(iface =>
                iface.Vid == vid && iface.Pid == pid &&
                iface.UsagePage == usagePage && iface.Usage == usage &&
                (match.inputLength < 0 || iface.InputLength == match.inputLength) &&
                (match.outputLength < 0 || iface.OutputLength == match.outputLength) &&
                (match.featureLength < 0 || iface.FeatureLength == match.featureLength));
        }

        private static bool MatchesResponse(byte[] response, ProfileResponse expected)
        {
            if (response == null || response.Length != expected.length) return false;
            byte[] prefix = ProfileJson.HexBytes(expected.prefix);
            if (prefix.Length > response.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (response[i] != prefix[i]) return false;
            if (expected.checksum != null && !ChecksumMatches(response, expected.checksum)) return false;
            return true;
        }

        private static bool ChecksumMatches(byte[] buffer, ProfileChecksum checksum)
        {
            int value = 0;
            for (int i = 0; i < checksum.count; i++)
            {
                int index = checksum.start + i;
                if (index < 0 || index >= buffer.Length) return false;
                if (checksum.algorithm == "xor8") value ^= buffer[index];
                else value += buffer[index];
            }
            return (byte)(value & 0xFF) == buffer[checksum.offset];
        }

        private static void ApplyChecksum(byte[] buffer, ProfileChecksum checksum)
        {
            if (checksum == null || buffer == null) return;
            int value = 0;
            for (int i = 0; i < checksum.count; i++)
            {
                int index = checksum.start + i;
                if (index < 0 || index >= buffer.Length) return;
                if (checksum.algorithm == "xor8") value ^= buffer[index];
                else value += buffer[index];
            }
            if (checksum.offset >= 0 && checksum.offset < buffer.Length)
                buffer[checksum.offset] = (byte)(value & 0xFF);
        }

        private static bool Satisfies(ProfileCondition[] conditions, byte[] buffer)
        {
            if (conditions == null) return true;
            foreach (ProfileCondition condition in conditions)
            {
                long value = ReadField(condition, buffer);
                if (condition.values == null || !condition.values.Contains(value)) return false;
            }
            return true;
        }

        private static bool ConditionsMatch(ProfileCondition[] conditions, Dictionary<string, byte[]> responses)
        {
            if (conditions == null || conditions.Length == 0) return false;
            foreach (ProfileCondition condition in conditions)
            {
                byte[] buffer = FindStepBuffer(responses, condition.step);
                if (buffer == null) return false;
                long value = ReadField(condition, buffer);
                if (condition.values == null || !condition.values.Contains(value)) return false;
            }
            return true;
        }

        private static bool? ReadFlag(ProfileFlag flag, Dictionary<string, byte[]> responses)
        {
            if (flag == null) return null;
            byte[] buffer = FindStepBuffer(responses, flag.step);
            if (buffer == null) return null;
            long value = ReadField(flag, buffer);
            if (flag.trueValues != null && flag.trueValues.Contains(value)) return true;
            if (flag.falseValues != null && flag.falseValues.Contains(value)) return false;
            return null;
        }

        private static byte[] FindStepBuffer(Dictionary<string, byte[]> responses, string id)
        {
            byte[] buffer;
            return responses.TryGetValue(id, out buffer) ? buffer : null;
        }

        private static long ReadField(ProfileField field, byte[] buffer)
        {
            long value = 0;
            for (int i = 0; i < field.size; i++)
            {
                int index = field.endian == "big" ? field.offset + i : field.offset + field.size - 1 - i;
                if (index < 0 || index >= buffer.Length) return 0;
                value = (value << 8) | buffer[index];
            }
            value &= field.mask;
            if (field.shift > 0) value >>= field.shift;
            return value;
        }

        private static byte[] Pad(byte[] data, int length)
        {
            var result = new byte[Math.Max(0, length)];
            if (data != null)
            {
                int count = Math.Min(data.Length, result.Length);
                Buffer.BlockCopy(data, 0, result, 0, count);
            }
            return result;
        }

        private static string Hex(byte[] data)
        {
            if (data == null) return "";
            var sb = new StringBuilder(data.Length * 3);
            for (int i = 0; i < data.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
