using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GearPower
{
    public sealed class RecentLogBuffer
    {
        private readonly object gate = new object();
        private readonly Queue<string> lines = new Queue<string>();
        private long revision;

        public long Revision
        {
            get { lock (gate) return revision; }
        }

        public string[] Snapshot()
        {
            lock (gate) return lines.ToArray();
        }

        public void Append(string message)
        {
            string normalized = (message ?? "").Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
            lock (gate)
            {
                foreach (string line in normalized.Split('\n'))
                {
                    lines.Enqueue(line.Length > 4096 ? line.Substring(0, 4096) + " …" : line);
                    while (lines.Count > 1000) lines.Dequeue();
                }
                revision++;
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                lines.Clear();
                revision++;
            }
        }

        public void Export(string path)
        {
            File.WriteAllLines(path, Snapshot(), new UTF8Encoding(false));
        }
    }
}
