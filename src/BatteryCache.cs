using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GearPower
{
    public sealed class CachedBattery
    {
        public int Percent;
        public bool Charging;
        public bool Full;
        public DateTime Updated;
    }

    public static class BatteryCache
    {
        private static readonly Dictionary<string, CachedBattery> Entries =
            new Dictionary<string, CachedBattery>(StringComparer.OrdinalIgnoreCase);

        private static readonly object Gate = new object();
        private static readonly string FilePath;
        private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

        static BatteryCache()
        {
            string local = "";
            try
            {
                local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            catch
            {
            }

            string legacyDirectory = string.IsNullOrEmpty(local) ? null : Path.Combine(local, "BatteryPeek");
            string[] candidates = new string[]
            {
                string.IsNullOrEmpty(local) ? null : Path.Combine(local, "GearPower"),
                AppDomain.CurrentDomain.BaseDirectory
            };

            foreach (string directory in candidates)
            {
                if (string.IsNullOrEmpty(directory)) continue;
                try
                {
                    Directory.CreateDirectory(directory);
                    FilePath = Path.Combine(directory, "battery-cache.tsv");
                    if (!File.Exists(FilePath) && !string.IsNullOrEmpty(legacyDirectory))
                    {
                        string legacyFile = Path.Combine(legacyDirectory, "battery-cache.tsv");
                        if (File.Exists(legacyFile))
                        {
                            try { File.Copy(legacyFile, FilePath, false); } catch { }
                        }
                    }
                    Load();
                    return;
                }
                catch
                {
                }
            }
        }

        /// <summary>
        /// Fill a sleeping device's card from the last successful reading, and
        /// remember every fresh reading for the next sleep.
        /// </summary>
        public static void Apply(DeviceReading reading)
        {
            if (reading == null || string.IsNullOrEmpty(reading.Id)) return;
            lock (Gate)
            {
                if (reading.Asleep)
                {
                    CachedBattery cached;
                    if (Entries.TryGetValue(reading.Id, out cached))
                    {
                        if (!reading.Percent.HasValue) reading.Percent = cached.Percent;
                        reading.Charging = cached.Charging;
                        reading.Full = cached.Full;
                        reading.Updated = cached.Updated;
                        reading.FromCache = true;
                        reading.Note = "设备休眠，保留上次读数";
                    }
                    return;
                }

                if (reading.Percent.HasValue)
                {
                    var cached = new CachedBattery();
                    cached.Percent = reading.Percent.Value;
                    cached.Charging = reading.Charging;
                    cached.Full = reading.Full;
                    cached.Updated = reading.Updated == DateTime.MinValue ? DateTime.Now : reading.Updated;
                    Entries[reading.Id] = cached;
                }
            }
        }

        public static void Save()
        {
            if (string.IsNullOrEmpty(FilePath)) return;
            lock (Gate)
            {
                try
                {
                    DateTime cutoff = DateTime.Now - MaxAge;
                    var lines = new List<string>();
                    foreach (KeyValuePair<string, CachedBattery> pair in Entries)
                    {
                        CachedBattery value = pair.Value;
                        if (value.Updated < cutoff) continue;
                        lines.Add(Escape(pair.Key) + "\t" +
                                  value.Percent.ToString(CultureInfo.InvariantCulture) + "\t" +
                                  (value.Charging ? "1" : "0") + "\t" +
                                  (value.Full ? "1" : "0") + "\t" +
                                  value.Updated.ToBinary().ToString(CultureInfo.InvariantCulture));
                    }
                    File.WriteAllLines(FilePath, lines, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Log.Debug("Battery cache save failed: " + ex.Message);
                }
            }
        }

        private static void Load()
        {
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;
            try
            {
                DateTime cutoff = DateTime.Now - MaxAge;
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length < 5) continue;

                    int percent;
                    if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out percent)) continue;
                    if (percent < 0 || percent > 100) continue;

                    long ticks;
                    if (!long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) continue;
                    DateTime updated;
                    try
                    {
                        updated = DateTime.FromBinary(ticks);
                    }
                    catch
                    {
                        continue;
                    }
                    if (updated < cutoff) continue;

                    var cached = new CachedBattery();
                    cached.Percent = percent;
                    cached.Charging = parts[2] == "1";
                    cached.Full = parts[3] == "1";
                    cached.Updated = updated;
                    Entries[parts[0]] = cached;
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Battery cache load failed: " + ex.Message);
            }
        }

        private static string Escape(string text)
        {
            return (text ?? "").Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
