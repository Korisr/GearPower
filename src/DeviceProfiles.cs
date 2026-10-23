using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace GearPower
{
    public sealed class DeviceProfile
    {
        public int schemaVersion = 1;
        public string id;
        public string name;
        public string vendor;
        public string builtinProtocol;
        public string kind = "mouse";
        public bool enabled = true;
        public int priority = 100;
        public ProfileMatch[] matches;
        public ProfileStep[] steps;
        public ProfileResult result;
        [ScriptIgnore] public string SourcePath;
        [ScriptIgnore] public bool UserFile;

        public override string ToString()
        {
            return name + "\n" + id + (UserFile ? " · 用户" : " · 随附") + (enabled ? "" : " · 停用");
        }
    }

    public sealed class ProfileMatch
    {
        public string vid;
        public string pid;
        public string usagePage;
        public string usage;
        public string transport;
        public string productContains;
        public int inputLength = -1;
        public int outputLength = -1;
        public int featureLength = -1;
    }

    public sealed class ProfileStep
    {
        public string id;
        public string method = "output";
        public string whenVid;
        public string request;
        public int reportId;
        public int attempts = 1;
        public int timeoutMs = 400;
        public ProfileChecksum requestChecksum;
        public ProfileResponse response;
        public ProfileCondition[] require;
        public string failureNote;
    }

    public sealed class ProfileResponse
    {
        public int length;
        public string prefix;
        public ProfileChecksum checksum;
    }

    public sealed class ProfileChecksum
    {
        public string algorithm = "sum8";
        public int start;
        public int count;
        public int offset;
    }

    public class ProfileField
    {
        public string step;
        public int offset = -1;
        public int size = 1;
        public string endian = "little";
        public long mask = 4294967295L;
        public int shift;
    }

    public sealed class ProfilePercent : ProfileField
    {
        public double scale = 1;
        public double add;
    }

    public sealed class ProfileCondition : ProfileField
    {
        public long[] values;
    }

    public sealed class ProfileFlag : ProfileField
    {
        public long[] trueValues;
        public long[] falseValues;
    }

    public sealed class ProfileResult
    {
        public ProfilePercent percent;
        public ProfileFlag charging;
        public ProfileFlag full;
        public ProfileCondition[] percentUnavailableWhen;
        public string note;
    }

    public static class ProfileJson
    {
        public const int MaxBytes = 131072;

        private static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = MaxBytes, RecursionLimit = 32 };
        }

        public static DeviceProfile Parse(string text)
        {
            if (text == null || text.Length > MaxBytes) throw new FormatException("配置为空或超过 128 KB。");
            var serializer = Serializer();
            object raw = serializer.DeserializeObject(text);
            CheckShape(raw, typeof(DeviceProfile), "配置");
            DeviceProfile profile = serializer.Deserialize<DeviceProfile>(text);
            Validate(profile);
            return profile;
        }

        public static string Serialize(DeviceProfile profile)
        {
            return Serializer().Serialize(profile);
        }

        public static string Pretty(DeviceProfile profile)
        {
            string json = Serialize(profile);
            var output = new System.Text.StringBuilder();
            int depth = 0;
            bool quoted = false;
            bool escaped = false;
            Action newline = () =>
            {
                output.AppendLine();
                output.Append(' ', depth * 2);
            };
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (quoted)
                {
                    output.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') { quoted = true; output.Append(c); }
                else if (c == '{' || c == '[')
                {
                    output.Append(c);
                    depth++;
                    if (i + 1 < json.Length && json[i + 1] != '}' && json[i + 1] != ']') newline();
                }
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (i > 0 && json[i - 1] != '{' && json[i - 1] != '[') newline();
                    output.Append(c);
                }
                else if (c == ',') { output.Append(c); newline(); }
                else if (c == ':') output.Append(": ");
                else output.Append(c);
            }
            return output.ToString();
        }

        private static void CheckShape(object raw, Type type, string path)
        {
            if (raw == null) return;
            if (type.IsArray)
            {
                var items = raw as object[];
                if (items == null) throw new FormatException(path + " 必须是数组。");
                foreach (object item in items) CheckShape(item, type.GetElementType(), path + "[]");
                return;
            }
            if (type == typeof(string) || type.IsPrimitive || type == typeof(decimal)) return;
            var dict = raw as IDictionary<string, object>;
            if (dict == null) throw new FormatException(path + " 必须是对象。");
            foreach (var entry in dict)
            {
                FieldInfo field = type.GetField(entry.Key, BindingFlags.Public | BindingFlags.Instance);
                if (field == null || Attribute.IsDefined(field, typeof(ScriptIgnoreAttribute)))
                    throw new FormatException(path + " 中存在未知字段：" + entry.Key);
                CheckShape(entry.Value, field.FieldType, path + "." + entry.Key);
            }
        }

        public static ushort HexId(string text)
        {
            ushort value;
            if (text == null || !Regex.IsMatch(text, "^(0[xX])?[0-9a-fA-F]{1,4}$") ||
                !ushort.TryParse(text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text.Substring(2) : text,
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                throw new FormatException("设备 ID 和 Usage 必须是 1–4 位十六进制。");
            return value;
        }

        public static byte[] HexBytes(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new byte[0];
            string[] tokens = Regex.Split(text.Trim(), @"[\s,\-]+");
            var bytes = new List<byte>();
            foreach (string token in tokens)
            {
                string t = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token.Substring(2) : token;
                byte value;
                if (t.Length < 1 || t.Length > 2 || !byte.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    throw new FormatException("报文必须是用空格分隔的十六进制字节。");
                bytes.Add(value);
            }
            return bytes.ToArray();
        }

        public static void Validate(DeviceProfile p)
        {
            if (p == null || p.schemaVersion != 1) throw new FormatException("仅支持 schemaVersion = 1。");
            if (p.id == null || !Regex.IsMatch(p.id, "^[a-z0-9][a-z0-9_-]{0,63}$"))
                throw new FormatException("id 只能包含小写字母、数字、下划线和连字符，最多 64 字符。");
            if (string.IsNullOrWhiteSpace(p.name) || p.name.Length > 120 || (p.vendor ?? "").Length > 120)
                throw new FormatException("请填写设备名称（不超过 120 字符）。");
            if (!IsKind(p.kind)) throw new FormatException("kind 应为 mouse、keyboard、headset、microphone、dock、receiver、controller、speaker 或 other。");
            if (p.priority < 0 || p.priority > 1000) throw new FormatException("priority 范围为 0–1000。");
            bool native = !string.IsNullOrEmpty(p.builtinProtocol);
            if (native)
            {
                if (p.steps != null || p.result != null)
                    throw new FormatException("builtinProtocol 不能混用 steps/result。");
                if (p.matches == null || p.matches.Length == 0) return;
            }
            if (p.matches == null || p.matches.Length < 1 || p.matches.Length > 8)
                throw new FormatException("需提供 1–8 组设备匹配规则。");
            foreach (ProfileMatch m in p.matches)
            {
                if (m == null) throw new FormatException("matches 不能包含空项。");
                HexId(m.vid); HexId(m.pid); HexId(m.usagePage); HexId(m.usage);
                if (m.transport != null && !new[] { "wired", "wireless", "bluetooth" }.Contains(m.transport))
                    throw new FormatException("transport 应为 wired、wireless 或 bluetooth。");
                if ((m.productContains ?? "").Length > 120)
                    throw new FormatException("productContains 最多 120 字符。");
                if (!native && HexId(m.usagePage) < 0xFF00)
                    throw new FormatException("自定义配置仅支持厂商 Usage Page（FF00–FFFF）。");
                foreach (int length in new[] { m.inputLength, m.outputLength, m.featureLength })
                    if (length < 0 || length > 4096) throw new FormatException("需明确填写 0–4096 范围内的三个报告长度。");
            }
            if (native) return;
            if (p.steps == null || p.steps.Length < 1 || p.steps.Length > 8)
                throw new FormatException("需提供 1–8 个读取步骤。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (ProfileStep step in p.steps)
            {
                if (step == null || string.IsNullOrEmpty(step.id) ||
                    !Regex.IsMatch(step.id, "^[a-zA-Z0-9_-]{1,40}$") || !ids.Add(step.id))
                    throw new FormatException("每个步骤需要唯一 id。");
                if (!new[] { "output", "feature", "featureQuery", "input" }.Contains(step.method))
                    throw new FormatException("不支持的 method：" + step.method);
                if (step.attempts < 1 || step.attempts > 6 || step.timeoutMs < 20 || step.timeoutMs > 1500 ||
                    step.reportId < 0 || step.reportId > 255)
                    throw new FormatException("attempts 范围为 1–6，timeoutMs 为 20–1500，reportId 为 0–255。");
                if (step.response == null || step.response.length < 1 || step.response.length > 4096)
                    throw new FormatException("每个步骤需填写 response.length。");
                byte[] prefix = HexBytes(step.response.prefix);
                if (prefix.Length == 0 || prefix.Length > step.response.length)
                    throw new FormatException("需填写用于辨认响应类型的 response.prefix。");
                CheckChecksum(step.response.checksum, step.response.length);
                ProfileMatch[] applicable = p.matches.Where(m => string.IsNullOrEmpty(step.whenVid) ||
                    HexId(m.vid) == HexId(step.whenVid)).ToArray();
                if (applicable.Length == 0) throw new FormatException("whenVid 未对应任何设备匹配规则。");
                foreach (ProfileMatch m in applicable)
                {
                    bool feature = step.method == "feature" || step.method == "featureQuery";
                    int responseLength = feature ? m.featureLength : m.inputLength;
                    int requestLength = feature ? m.featureLength : m.outputLength;
                    if (step.response.length != responseLength)
                        throw new FormatException("response.length 必须等于该接口的输入或 Feature 报告长度。");
                    if (step.method == "output" || step.method == "featureQuery")
                    {
                        int length = HexBytes(step.request).Length;
                        if (length == 0 || length > requestLength)
                            throw new FormatException("请填写查询 request，其长度不能超过接口报告长度。");
                        CheckChecksum(step.requestChecksum, requestLength);
                    }
                    else if (step.requestChecksum != null || !string.IsNullOrEmpty(step.request))
                        throw new FormatException("feature/input 直接读取不使用 request；请填写 reportId。");
                }
                if (step.require != null)
                {
                    if (step.require.Length > 16) throw new FormatException("每个步骤最多 16 个条件。");
                    foreach (ProfileCondition c in step.require) CheckCondition(c, step.response.length);
                }
            }
            if (p.result == null || p.result.percent == null) throw new FormatException("需填写 result.percent。");
            CheckResultField(p, p.result.percent);
            if (double.IsNaN(p.result.percent.scale) || double.IsInfinity(p.result.percent.scale) ||
                double.IsNaN(p.result.percent.add) || double.IsInfinity(p.result.percent.add))
                throw new FormatException("电量缩放参数必须是有限数字。");
            foreach (ProfileFlag flag in new[] { p.result.charging, p.result.full })
            {
                if (flag == null) continue;
                CheckResultField(p, flag);
                if (flag.trueValues == null || flag.trueValues.Length == 0 || flag.trueValues.Length > 64 ||
                    flag.falseValues == null || flag.falseValues.Length == 0 || flag.falseValues.Length > 64 ||
                    flag.trueValues.Intersect(flag.falseValues).Any())
                    throw new FormatException("状态字段需提供不重叠的 trueValues 和 falseValues。");
            }
            if (p.result.percentUnavailableWhen != null)
            {
                if (p.result.percentUnavailableWhen.Length > 16) throw new FormatException("最多 16 个百分比不可用条件。");
                foreach (ProfileCondition c in p.result.percentUnavailableWhen)
                {
                    CheckResultField(p, c);
                    CheckCondition(c, FindStep(p, c.step).response.length);
                }
            }
            if ((p.result.note ?? "").Length > 500 || p.steps.Any(x => (x.failureNote ?? "").Length > 500))
                throw new FormatException("说明文本最多 500 字符。");
        }

        public static DeviceKind ParseKind(string kind)
        {
            string value = (kind ?? "").ToLowerInvariant();
            if (value == "receiver") return DeviceKind.Other;
            DeviceKind parsed;
            return Enum.TryParse<DeviceKind>(value, true, out parsed) ? parsed : DeviceKind.Other;
        }

        private static bool IsKind(string kind)
        {
            string value = (kind ?? "").ToLowerInvariant();
            if (value == "receiver") return true;
            DeviceKind parsed;
            return Enum.TryParse<DeviceKind>(value, true, out parsed) && Enum.IsDefined(typeof(DeviceKind), parsed);
        }

        private static void CheckChecksum(ProfileChecksum c, int length)
        {
            if (c == null) return;
            if ((c.algorithm != "sum8" && c.algorithm != "xor8") || c.start < 0 || c.count < 1 ||
                (long)c.start + c.count > length || c.offset < 0 || c.offset >= length ||
                (c.offset >= c.start && c.offset < c.start + c.count))
                throw new FormatException("校验范围或校验字段位置不正确。");
        }

        private static void CheckField(ProfileField f, int length)
        {
            if (f == null || f.offset < 0 || (f.size != 1 && f.size != 2 && f.size != 4) ||
                f.offset > length - f.size || f.shift < 0 || f.shift > 31 ||
                f.mask < 0 || f.mask > uint.MaxValue || (f.endian != "little" && f.endian != "big"))
                throw new FormatException("字段 offset/size/mask/shift/endian 无效或越界。");
        }

        private static void CheckCondition(ProfileCondition c, int length)
        {
            CheckField(c, length);
            if (c.values == null || c.values.Length == 0 || c.values.Length > 64)
                throw new FormatException("条件需提供 values。");
        }

        internal static ProfileStep FindStep(DeviceProfile p, string id)
        {
            ProfileStep step = p.steps.FirstOrDefault(s => s.id == id);
            if (step == null) throw new FormatException("字段引用了不存在的步骤：" + id);
            return step;
        }

        private static void CheckResultField(DeviceProfile p, ProfileField field)
        {
            if (field == null) throw new FormatException("结果字段不能为空。");
            CheckField(field, FindStep(p, field.step).response.length);
        }
    }
}
