using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GearPower
{
    public enum ThemeMode
    {
        System,
        Light,
        Dark
    }

    public static class ThemeManager
    {
        private const string SettingsPath = @"Software\GearPower";
        private const string LegacySettingsPath = @"Software\BatteryPeek";
        private const string PersonalizePath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        private static Window window;
        private static Dictionary<string, object> values = BuildDark();
        private static DispatcherTimer titleTimer;

        public static ThemeMode Mode { get; private set; }
        public static bool IsDark { get; private set; }
        public static event EventHandler Changed;

        public static void Initialize(Window owner)
        {
            window = owner;
            Mode = LoadMode();
            Apply(false);
            if (window != null)
            {
                window.SourceInitialized += (s, e) => ApplyTitleBar();
                window.Activated += (s, e) => ApplyTitleBar();
                window.Loaded += (s, e) =>
                {
                    ApplyTitleBar();
                    ScheduleTitleBar();
                };
                window.ContentRendered += (s, e) =>
                {
                    ApplyTitleBar();
                    ScheduleTitleBar();
                };
            }
        }

        public static void SetMode(ThemeMode mode)
        {
            SetMode(mode, true);
        }

        public static void SetMode(ThemeMode mode, bool save)
        {
            Mode = mode;
            if (save) SaveMode();
            Apply(true);
        }

        public static void RefreshSystem()
        {
            if (Mode != ThemeMode.System || window == null) return;
            if (SystemUsesDark() != IsDark) Apply(true);
        }

        public static object GetResource(string key)
        {
            object value;
            if (values.TryGetValue(key, out value)) return value;
            return null;
        }

        public static void ApplyTitleBar()
        {
            if (window == null) return;
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            HidEnumerator.SetTitleBarTheme(hwnd, IsDark);
        }

        private static void ScheduleTitleBar()
        {
            if (window == null) return;
            if (titleTimer == null)
            {
                titleTimer = new DispatcherTimer();
                titleTimer.Interval = TimeSpan.FromMilliseconds(2000);
                titleTimer.Tick += (s, e) =>
                {
                    titleTimer.Stop();
                    ApplyTitleBar();
                };
            }
            titleTimer.Stop();
            titleTimer.Start();
        }

        private static void Apply(bool notify)
        {
            IsDark = Mode == ThemeMode.Dark || (Mode == ThemeMode.System && SystemUsesDark());
            values = IsDark ? BuildDark() : BuildLight();

            if (window != null)
            {
                foreach (KeyValuePair<string, object> pair in values)
                {
                    window.Resources[pair.Key] = pair.Value;
                }
            }

            ApplyTitleBar();
            ScheduleTitleBar();
            if (window != null)
            {
                window.Dispatcher.BeginInvoke(new Action(ApplyTitleBar), DispatcherPriority.ApplicationIdle);
            }

            if (notify)
            {
                EventHandler handler = Changed;
                if (handler != null) handler(null, EventArgs.Empty);
            }
        }

        private static ThemeMode LoadMode()
        {
            ThemeMode? mode = ReadMode(SettingsPath);
            if (!mode.HasValue) mode = ReadMode(LegacySettingsPath);
            return mode.HasValue ? mode.Value : ThemeMode.System;
        }

        private static ThemeMode? ReadMode(string path)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
                {
                    if (key != null)
                    {
                        string text = key.GetValue("ThemeMode") as string;
                        if (!string.IsNullOrEmpty(text))
                        {
                            if (text.Equals("light", StringComparison.OrdinalIgnoreCase)) return ThemeMode.Light;
                            if (text.Equals("dark", StringComparison.OrdinalIgnoreCase)) return ThemeMode.Dark;
                            if (text.Equals("system", StringComparison.OrdinalIgnoreCase)) return ThemeMode.System;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static void SaveMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SettingsPath))
                {
                    if (key != null) key.SetValue("ThemeMode", Mode.ToString().ToLowerInvariant());
                }
            }
            catch
            {
            }
        }

        private static bool SystemUsesDark()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(PersonalizePath))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("AppsUseLightTheme");
                        if (value is int) return (int)value == 0;
                    }
                }
            }
            catch
            {
            }
            return false;
        }

        private static Dictionary<string, object> BuildDark()
        {
            var d = new Dictionary<string, object>();
            AddGradient(d, "AppBackgroundBrush", "#090D13", "#0B1119", "#080C12");
            AddColor(d, "TextPrimaryBrush", "#E9F0F8");
            AddColor(d, "TextSecondaryBrush", "#93A5BC");
            AddColor(d, "TextMutedBrush", "#63748C");
            AddColor(d, "AccentBrush", "#4ADE80");
            AddColor(d, "AccentDimBrush", "#173325");
            AddColor(d, "WarnBrush", "#FBBF24");
            AddColor(d, "DangerBrush", "#F87171");
            AddColor(d, "InfoBrush", "#60A5FA");
            AddColor(d, "PanelBrush", "#0F161F");
            AddColor(d, "PanelBorderBrush", "#1E2A38");
            AddColor(d, "OptionBorderBrush", "#2A3746");
            AddColor(d, "OptionSelectedBorderBrush", "#3F916A");
            AddColor(d, "CardBorderBrush", "#212D3C");
            AddGradient(d, "CardBackgroundBrush", "#16202C", "#111923", null);
            AddGradient(d, "CardHoverBackgroundBrush", "#1A2634", "#141E29", null);
            AddColor(d, "TrackBrush", "#26313F");
            AddColor(d, "TabBarBrush", "#0F161F");
            AddColor(d, "TabActiveBrush", "#1D2938");
            AddColor(d, "TabActiveForegroundBrush", "#F1F6FC");
            AddColor(d, "TabInactiveForegroundBrush", "#8294AA");
            AddColor(d, "HeaderIconBgBrush", "#13251C");
            AddColor(d, "HeaderIconBorderBrush", "#20462F");
            AddColor(d, "GhostForegroundBrush", "#A9B8CC");
            AddColor(d, "GhostHoverBrush", "#16202C");
            AddColor(d, "GhostPressedBrush", "#1B2837");
            AddColor(d, "SwitchOnBrush", "#34C759");
            AddColor(d, "SwitchOffBrush", "#2C2C2E");
            AddColor(d, "SwitchBorderBrush", "#3A3A3C");
            AddColor(d, "SwitchKnobBrush", "#FFFFFF");
            AddColor(d, "SummaryBrush", "#0E151E");
            AddColor(d, "SummaryBorderBrush", "#1C2836");
            AddColor(d, "InputBrush", "#0C131B");
            AddColor(d, "InputBorderBrush", "#1D2937");
            AddColor(d, "RowBorderBrush", "#17212C");
            AddColor(d, "RowHoverBrush", "#131D28");
            AddAlpha(d, "RowSupportedBrush", 28, "#4ADE80");
            AddColor(d, "ScrollThumbBrush", "#2A3647");
            AddColor(d, "TooltipBgBrush", "#141C26");
            AddColor(d, "TooltipBorderBrush", "#2A3647");
            AddColor(d, "TooltipForegroundBrush", "#DCE6F2");
            AddColor(d, "ToastBgBrush", "#16251D");
            AddColor(d, "ToastBorderBrush", "#2C5A40");
            AddColor(d, "ToastTextBrush", "#BDF3D4");
            AddColor(d, "DemoBgBrush", "#2B2414");
            AddColor(d, "DemoBorderBrush", "#5C4A1C");
            AddColor(d, "DemoTextBrush", "#FBBF24");
            AddColor(d, "EmptyOrbBrush", "#0E151E");
            AddColor(d, "EmptyRingBrush", "#243244");
            AddColor(d, "EmptyGlyphBrush", "#3B4C60");
            AddColor(d, "PrimaryButtonBgBrush", "#4ADE80");
            AddColor(d, "PrimaryButtonForegroundBrush", "#07130C");
            AddColor(d, "PrimaryButtonHoverBrush", "#6EE79A");
            AddColor(d, "PrimaryButtonPressedBrush", "#3CCB6F");
            AddColor(d, "DividerBrush", "#1E2A38");
            AddColor(d, "RefreshGlyphBrush", "#B7C6D8");
            return d;
        }

        private static Dictionary<string, object> BuildLight()
        {
            var d = new Dictionary<string, object>();
            AddGradient(d, "AppBackgroundBrush", "#F7F9FC", "#EFF3F8", "#FAFCFE");
            AddColor(d, "TextPrimaryBrush", "#17202B");
            AddColor(d, "TextSecondaryBrush", "#53657A");
            AddColor(d, "TextMutedBrush", "#8796A8");
            AddColor(d, "AccentBrush", "#16A34A");
            AddColor(d, "AccentDimBrush", "#DCFCE7");
            AddColor(d, "WarnBrush", "#D97706");
            AddColor(d, "DangerBrush", "#DC2626");
            AddColor(d, "InfoBrush", "#2563EB");
            AddColor(d, "PanelBrush", "#FFFFFF");
            AddColor(d, "PanelBorderBrush", "#E3E9F1");
            AddColor(d, "OptionBorderBrush", "#D4DDE8");
            AddColor(d, "OptionSelectedBorderBrush", "#8BCFA8");
            AddColor(d, "CardBorderBrush", "#E1E8F0");
            AddGradient(d, "CardBackgroundBrush", "#FFFFFF", "#F5F9FD", null);
            AddGradient(d, "CardHoverBackgroundBrush", "#FFFFFF", "#EAF3FC", null);
            AddColor(d, "TrackBrush", "#E2E8F0");
            AddColor(d, "TabBarBrush", "#EEF2F7");
            AddColor(d, "TabActiveBrush", "#FFFFFF");
            AddColor(d, "TabActiveForegroundBrush", "#0F172A");
            AddColor(d, "TabInactiveForegroundBrush", "#6B7A8C");
            AddColor(d, "HeaderIconBgBrush", "#E9F8EF");
            AddColor(d, "HeaderIconBorderBrush", "#C9EBD7");
            AddColor(d, "GhostForegroundBrush", "#53657A");
            AddColor(d, "GhostHoverBrush", "#E9EFF6");
            AddColor(d, "GhostPressedBrush", "#DDE7F2");
            AddColor(d, "SwitchOnBrush", "#34C759");
            AddColor(d, "SwitchOffBrush", "#E9E9EA");
            AddColor(d, "SwitchBorderBrush", "#D1D1D6");
            AddColor(d, "SwitchKnobBrush", "#FFFFFF");
            AddColor(d, "SummaryBrush", "#FFFFFF");
            AddColor(d, "SummaryBorderBrush", "#E3E9F1");
            AddColor(d, "InputBrush", "#F1F5F9");
            AddColor(d, "InputBorderBrush", "#E2E8F0");
            AddColor(d, "RowBorderBrush", "#EDF1F6");
            AddColor(d, "RowHoverBrush", "#F3F7FC");
            AddAlpha(d, "RowSupportedBrush", 30, "#16A34A");
            AddColor(d, "ScrollThumbBrush", "#C7D2E0");
            AddColor(d, "TooltipBgBrush", "#FFFFFF");
            AddColor(d, "TooltipBorderBrush", "#D8E2ED");
            AddColor(d, "TooltipForegroundBrush", "#1B2430");
            AddColor(d, "ToastBgBrush", "#ECFDF3");
            AddColor(d, "ToastBorderBrush", "#BBF7D0");
            AddColor(d, "ToastTextBrush", "#166534");
            AddColor(d, "DemoBgBrush", "#FFF7E6");
            AddColor(d, "DemoBorderBrush", "#FDE68A");
            AddColor(d, "DemoTextBrush", "#B45309");
            AddColor(d, "EmptyOrbBrush", "#FFFFFF");
            AddColor(d, "EmptyRingBrush", "#D8E2EC");
            AddColor(d, "EmptyGlyphBrush", "#B6C4D2");
            AddColor(d, "PrimaryButtonBgBrush", "#16A34A");
            AddColor(d, "PrimaryButtonForegroundBrush", "#FFFFFF");
            AddColor(d, "PrimaryButtonHoverBrush", "#15803D");
            AddColor(d, "PrimaryButtonPressedBrush", "#166534");
            AddColor(d, "DividerBrush", "#E2E8F0");
            AddColor(d, "RefreshGlyphBrush", "#53657A");
            return d;
        }

        private static Color Parse(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        private static SolidColorBrush Frozen(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static void AddColor(Dictionary<string, object> target, string key, string hex)
        {
            target[key] = Frozen(Parse(hex));
        }

        private static void AddAlpha(Dictionary<string, object> target, string key, byte alpha, string hex)
        {
            Color c = Parse(hex);
            target[key] = Frozen(Color.FromArgb(alpha, c.R, c.G, c.B));
        }

        private static void AddGradient(Dictionary<string, object> target, string key, string start, string end, string middle)
        {
            var brush = new LinearGradientBrush();
            brush.StartPoint = new Point(0, 0);
            brush.EndPoint = new Point(1, 1);
            brush.GradientStops.Add(new GradientStop(Parse(start), 0));
            if (!string.IsNullOrEmpty(middle))
                brush.GradientStops.Add(new GradientStop(Parse(middle), 0.55));
            brush.GradientStops.Add(new GradientStop(Parse(end), 1));
            brush.Freeze();
            target[key] = brush;
        }
    }
}

