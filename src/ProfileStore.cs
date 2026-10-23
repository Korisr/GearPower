using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace GearPower
{
    public static class ProfileStore
    {
        public static readonly string UserDirectory =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GearPower", "profiles");
        public static readonly string BundledDirectory =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles");

        public static List<DeviceProfile> Load(out List<string> errors)
        {
            errors = new List<string>();
            var profiles = new Dictionary<string, DeviceProfile>(StringComparer.Ordinal);
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            foreach (string resource in assembly.GetManifestResourceNames()
                .Where(n => n.StartsWith("profiles/", StringComparison.Ordinal) &&
                            n.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(resource))
                    {
                        if (stream == null) throw new FileNotFoundException("缺少内嵌配置：" + resource);
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            DeviceProfile profile = ProfileJson.Parse(reader.ReadToEnd());
                            profile.SourcePath = "resource:" + resource;
                            profiles[profile.id] = profile;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(resource + "：" + ex.Message);
                }
            }
            foreach (string directory in new[] { BundledDirectory, UserDirectory })
            {
                try
                {
                    if (!Directory.Exists(directory)) continue;
                    foreach (string path in Directory.GetFiles(directory, "*.json")
                        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Take(64))
                    {
                        try
                        {
                            if (new FileInfo(path).Length > ProfileJson.MaxBytes)
                                throw new FormatException("文件超过 128 KB");
                            DeviceProfile profile = ProfileJson.Parse(File.ReadAllText(path, Encoding.UTF8));
                            profile.SourcePath = path;
                            profile.UserFile = string.Equals(directory, UserDirectory, StringComparison.OrdinalIgnoreCase);
                            if (profiles.ContainsKey(profile.id) &&
                                profiles[profile.id].UserFile == profile.UserFile &&
                                !profiles[profile.id].SourcePath.StartsWith("resource:", StringComparison.Ordinal) &&
                                !string.Equals(profiles[profile.id].SourcePath, path, StringComparison.OrdinalIgnoreCase))
                                throw new FormatException("同一目录存在重复配置 id：" + profile.id);
                            profiles[profile.id] = profile;
                        }
                        catch (Exception ex)
                        {
                            errors.Add(Path.GetFileName(path) + "：" + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(directory + "：" + ex.Message);
                }
            }
            return profiles.Values.OrderByDescending(p => p.priority)
                .ThenBy(p => p.id, StringComparer.Ordinal).ToList();
        }

        public static string Import(string text)
        {
            DeviceProfile profile = ProfileJson.Parse(text);
            Directory.CreateDirectory(UserDirectory);
            string path = UserPath(profile.id);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, ProfileJson.Pretty(profile), Encoding.UTF8);
            try
            {
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            return path;
        }

        public static string UserPath(string id)
        {
            if (id == null || !Regex.IsMatch(id, "^[a-z0-9][a-z0-9_-]{0,63}$"))
                throw new FormatException("配置 id 无效。");
            string root = Path.GetFullPath(UserDirectory) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, id + ".json"));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new FormatException("配置路径无效。");
            return path;
        }

        public static void Remove(string id)
        {
            string path = UserPath(id);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
