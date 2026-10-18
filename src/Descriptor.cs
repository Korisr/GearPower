using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GearPower
{
    public enum HidReportType
    {
        Input = 0,
        Output = 1,
        Feature = 2
    }

    public sealed class HidReportField
    {
        public int ReportId;
        public HidReportType Type;
        public uint UsagePage;
        public uint Usage;
        public int BitOffset;
        public int BitSize;
        public int Count;
        public int LogicalMin;
        public int LogicalMax;

        public int ByteOffset
        {
            get { return BitOffset / 8; }
        }

        public string Describe()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0} id={1} usage={2:X4}/{3:X4} bit={4}+{5} count={6} logical={7}..{8}",
                Type, ReportId, UsagePage, Usage, BitOffset, BitSize, Count, LogicalMin, LogicalMax);
        }
    }

    public sealed class HidReportDescriptor
    {
        public readonly List<HidReportField> Fields = new List<HidReportField>();
        public bool UsesReportIds;
        public ushort TopUsagePage;
        public ushort TopUsage;

        private sealed class GlobalState
        {
            public uint UsagePage;
            public int LogicalMin;
            public int LogicalMax;
            public int ReportSize;
            public int ReportCount;
            public int ReportId;
        }

        public static HidReportDescriptor Parse(byte[] data)
        {
            var result = new HidReportDescriptor();
            if (data == null || data.Length == 0) return result;

            uint usagePage = 0;
            int logicalMin = 0;
            int logicalMax = 0;
            int reportSize = 0;
            int reportCount = 0;
            int reportId = 0;
            int collectionDepth = 0;
            uint topPage = 0;
            uint topUsage = 0;

            var usages = new List<uint>();
            uint usageMin = 0;
            uint usageMax = 0;
            bool hasUsageRange = false;
            var pushStack = new Stack<GlobalState>();
            var offsets = new Dictionary<int, int>();

            int i = 0;
            while (i < data.Length)
            {
                byte prefix = data[i++];
                if (prefix == 0xFE)
                {
                    if (i + 2 > data.Length) break;
                    int longSize = data[i++];
                    i++; // long item tag
                    i += longSize;
                    continue;
                }

                int sizeCode = prefix & 0x03;
                int dataSize = sizeCode == 3 ? 4 : sizeCode;
                if (i + dataSize > data.Length) break;

                uint value = 0;
                for (int b = 0; b < dataSize; b++)
                {
                    value |= (uint)data[i + b] << (8 * b);
                }
                int signedValue = dataSize == 0 ? 0 : SignExtend(value, dataSize);
                i += dataSize;

                int type = (prefix >> 2) & 0x03;
                int tag = (prefix >> 4) & 0x0F;

                if (type == 0) // Main
                {
                    if (tag == 0x8 || tag == 0x9 || tag == 0xB)
                    {
                        HidReportType reportType = tag == 0x8 ? HidReportType.Input
                            : tag == 0x9 ? HidReportType.Output : HidReportType.Feature;

                        if (reportSize > 0 && reportCount > 0)
                        {
                            int key = reportId * 4 + (int)reportType;
                            int offset;
                            offsets.TryGetValue(key, out offset);

                            var field = new HidReportField();
                            field.ReportId = reportId;
                            field.Type = reportType;
                            field.UsagePage = usagePage;
                            field.Usage = ChooseUsage(usages, usageMin, usageMax, hasUsageRange, 0);
                            field.BitOffset = offset;
                            field.BitSize = reportSize;
                            field.Count = reportCount;
                            field.LogicalMin = logicalMin;
                            field.LogicalMax = logicalMax;
                            result.Fields.Add(field);

                            offsets[key] = offset + reportSize * reportCount;
                        }
                    }
                    else if (tag == 0xA) // Collection
                    {
                        if (collectionDepth == 0)
                        {
                            topPage = usagePage;
                            topUsage = ChooseUsage(usages, usageMin, usageMax, hasUsageRange, 0);
                        }
                        collectionDepth++;
                    }
                    else if (tag == 0xC) // End Collection
                    {
                        if (collectionDepth > 0) collectionDepth--;
                    }

                    usages.Clear();
                    usageMin = 0;
                    usageMax = 0;
                    hasUsageRange = false;
                }
                else if (type == 1) // Global
                {
                    switch (tag)
                    {
                        case 0x0: usagePage = value; break;
                        case 0x1: logicalMin = signedValue; break;
                        case 0x2: logicalMax = signedValue; break;
                        case 0x7: reportSize = (int)value; break;
                        case 0x8: reportId = (int)value; result.UsesReportIds = true; break;
                        case 0x9: reportCount = (int)value; break;
                        case 0xA:
                            var state = new GlobalState();
                            state.UsagePage = usagePage;
                            state.LogicalMin = logicalMin;
                            state.LogicalMax = logicalMax;
                            state.ReportSize = reportSize;
                            state.ReportCount = reportCount;
                            state.ReportId = reportId;
                            pushStack.Push(state);
                            break;
                        case 0xB:
                            if (pushStack.Count > 0)
                            {
                                GlobalState old = pushStack.Pop();
                                usagePage = old.UsagePage;
                                logicalMin = old.LogicalMin;
                                logicalMax = old.LogicalMax;
                                reportSize = old.ReportSize;
                                reportCount = old.ReportCount;
                                reportId = old.ReportId;
                            }
                            break;
                    }
                }
                else if (type == 2) // Local
                {
                    switch (tag)
                    {
                        case 0x0:
                            usages.Add(value);
                            break;
                        case 0x1:
                            usageMin = value;
                            hasUsageRange = true;
                            break;
                        case 0x2:
                            usageMax = value;
                            hasUsageRange = true;
                            break;
                    }
                }
            }

            result.TopUsagePage = (ushort)(topPage & 0xFFFF);
            result.TopUsage = (ushort)(topUsage & 0xFFFF);
            return result;
        }

        private static uint ChooseUsage(List<uint> usages, uint min, uint max, bool hasRange, int index)
        {
            if (usages.Count > 0)
            {
                return usages[Math.Min(index, usages.Count - 1)];
            }
            if (hasRange && min + (uint)index <= max)
            {
                return min + (uint)index;
            }
            return 0;
        }

        private static int SignExtend(uint value, int bytes)
        {
            if (bytes <= 0) return 0;
            if (bytes >= 4) return unchecked((int)value);
            int shift = 32 - bytes * 8;
            return unchecked((int)(value << shift) >> shift);
        }

        public HidReportField FindBattery()
        {
            HidReportField input = null;
            foreach (HidReportField field in Fields)
            {
                if (field.UsagePage == 0x06 && field.Usage == 0x20)
                {
                    if (field.Type == HidReportType.Feature) return field;
                    if (input == null && field.Type == HidReportType.Input) input = field;
                }
            }
            return input;
        }

        public HidReportField Find(uint usagePage, uint usage)
        {
            foreach (HidReportField field in Fields)
            {
                if (field.UsagePage == usagePage && field.Usage == usage) return field;
            }
            return null;
        }

        public string ToText()
        {
            var sb = new StringBuilder();
            if (Fields.Count == 0)
            {
                sb.AppendLine("(empty descriptor)");
                return sb.ToString();
            }
            foreach (HidReportField field in Fields)
            {
                sb.AppendLine(field.Describe());
            }
            return sb.ToString();
        }

        public static int ExtractBits(byte[] buffer, int dataOffset, int bitOffset, int bitSize, int logicalMax)
        {
            if (buffer == null || bitSize <= 0) return -1;
            int bit = dataOffset * 8 + bitOffset;
            int byteIndex = bit / 8;
            int shift = bit % 8;
            if (byteIndex >= buffer.Length) return -1;
            uint value = 0;
            int bytesNeeded = (shift + bitSize + 7) / 8;
            for (int i = 0; i < bytesNeeded; i++)
            {
                if (byteIndex + i >= buffer.Length) break;
                value |= (uint)buffer[byteIndex + i] << (8 * i);
            }
            value >>= shift;
            if (bitSize < 32)
            {
                uint mask = (1u << bitSize) - 1u;
                value &= mask;
            }
            int raw = unchecked((int)value);
            if (logicalMax > 0 && logicalMax < 100)
            {
                // Some devices advertise a 0..N logical range rather than 0..100.
                return (int)Math.Round(raw * 100.0 / logicalMax);
            }
            return raw;
        }
    }
}
