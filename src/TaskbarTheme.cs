using System;
using Microsoft.Win32;

namespace GearPower
{
    /// <summary>
    /// The tray icon is drawn directly on the taskbar (no badge), so the
    /// battery outline needs to contrast with the taskbar background. Windows
    /// stores the taskbar's light/dark choice as SystemUsesLightTheme.
    /// </summary>
    public static class TaskbarTheme
    {
        public static bool IsLight
        {
            get
            {
                try
                {
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                    {
                        if (key == null) return false;
                        object value = key.GetValue("SystemUsesLightTheme");
                        if (value is int) return (int)value != 0;
                    }
                }
                catch
                {
                }
                return false;
            }
        }
    }
}
