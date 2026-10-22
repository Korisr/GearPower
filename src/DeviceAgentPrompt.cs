using System;
using System.Text;

namespace GearPower
{
    public static class DeviceAgentPrompt
    {
        public static string Build(string diagnostic)
        {
            var sb = new StringBuilder();
            sb.AppendLine("请根据下面的 HID 诊断信息，为 GearPower 设计一个 JSON 设备适配配置。");
            sb.AppendLine();
            sb.AppendLine("要求：");
            sb.AppendLine("1. 先判断设备类型、匹配接口和可用报告长度。");
            sb.AppendLine("2. 不要凭描述符猜测厂商命令；没有协议资料或真实抓包时只生成 enabled:false 的草稿。");
            sb.AppendLine("3. 偏移从 0 开始，包含 Windows Report ID。");
            sb.AppendLine("4. 说明哪些字段仍需实机验证。");
            sb.AppendLine();
            sb.AppendLine("HID 诊断信息：");
            sb.AppendLine("```text");
            sb.AppendLine((diagnostic ?? "").Trim());
            sb.AppendLine("```");
            return sb.ToString();
        }
    }
}
