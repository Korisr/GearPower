using System;
using System.IO;
using System.Text;

namespace GearPower
{
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string DirectoryPath;
        private static string FilePath;
        private static string InitError;

        static Log()
        {
            string local = "";
            try
            {
                local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }
            catch
            {
            }

            string temp = "";
            try
            {
                temp = Path.GetTempPath();
            }
            catch
            {
            }

            string[] candidates = new string[]
            {
                string.IsNullOrEmpty(local) ? null : Path.Combine(local, "GearPower", "logs"),
                string.IsNullOrEmpty(temp) ? null : Path.Combine(temp, "GearPower", "logs")
            };

            foreach (string candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                try
                {
                    Directory.CreateDirectory(candidate);
                    string file = Path.Combine(candidate, "app.log");
                    if (File.Exists(file) && new FileInfo(file).Length > 1024 * 1024)
                    {
                        string old = Path.Combine(candidate, "app.old.log");
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(file, old);
                    }
                    File.AppendAllText(file, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [INFO] log started" + Environment.NewLine, Encoding.UTF8);
                    DirectoryPath = candidate;
                    FilePath = file;
                    return;
                }
                catch (Exception ex)
                {
                    InitError = (InitError ?? "") + candidate + ": " + ex.Message + "; ";
                }
            }
        }

        public static string LogPath
        {
            get { return FilePath; }
        }

        public static string LogDirectory
        {
            get { return DirectoryPath; }
        }

        public static string InitializationError
        {
            get { return InitError; }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Debug(string message)
        {
            Write("DEBUG", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        private static void Write(string level, string message)
        {
            if (FilePath == null) return;
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(FilePath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
            }
        }
    }
}
