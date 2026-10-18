using System;
using System.Runtime.InteropServices;

namespace GearPower
{
    /// <summary>
    /// Windows blocks low-integrity processes from adding notification-area
    /// icons (UIPI). Detect it so the app can explain why the tray icon is
    /// missing instead of silently pretending it is there.
    /// </summary>
    public static class ProcessIntegrity
    {
        private const int TokenQuery = 0x0008;
        private const int TokenIntegrityLevel = 25;

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int needed);

        [DllImport("advapi32.dll")]
        private static extern IntPtr GetSidSubAuthority(IntPtr sid, int index);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        public static int Level
        {
            get
            {
                IntPtr token = IntPtr.Zero;
                IntPtr buffer = IntPtr.Zero;
                try
                {
                    if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token)) return 0;
                    int needed;
                    GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out needed);
                    if (needed <= 0) return 0;
                    buffer = Marshal.AllocHGlobal(needed);
                    if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, needed, out needed)) return 0;
                    IntPtr sid = Marshal.ReadIntPtr(buffer);
                    IntPtr sub = GetSidSubAuthority(sid, 0);
                    return Marshal.ReadInt32(sub);
                }
                catch
                {
                    return 0;
                }
                finally
                {
                    if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                    if (token != IntPtr.Zero) CloseHandle(token);
                }
            }
        }

        public static bool IsLow
        {
            get { return Level == 0x1000; }
        }

        public static string LevelName
        {
            get
            {
                int level = Level;
                if (level == 0x1000) return "Low";
                if (level == 0x2000) return "Medium";
                if (level == 0x3000) return "High";
                if (level == 0x4000) return "System";
                return "Unknown(" + level + ")";
            }
        }
    }
}
