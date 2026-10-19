using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace GearPower
{
    internal sealed class TaskbarSnapshot
    {
        public bool Found;
        public bool Visible;
        public bool Centered;
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int StartLeft;
        public int TrayLeft;
        public string Reason;

        public int FreeLeft
        {
            get { return StartLeft - Left; }
        }
    }

    internal static class TaskbarGeometry
    {
        private const int QunsPresentationMode = 4;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string className, string title);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string title);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder text, int count);

        [DllImport("shell32.dll")]
        private static extern int SHQueryUserNotificationState(out int state);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static TaskbarSnapshot Get()
        {
            var snapshot = new TaskbarSnapshot();
            IntPtr taskbar = FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero)
            {
                snapshot.Reason = "任务栏不可见";
                return snapshot;
            }
            snapshot.Found = true;
            if (!IsWindowVisible(taskbar))
            {
                snapshot.Reason = "任务栏不可见";
                return snapshot;
            }

            RECT rect;
            if (!GetWindowRect(taskbar, out rect))
            {
                snapshot.Reason = "任务栏不可见";
                return snapshot;
            }
            snapshot.Left = rect.Left;
            snapshot.Top = rect.Top;
            snapshot.Right = rect.Right;
            snapshot.Bottom = rect.Bottom;
            if (snapshot.Right - snapshot.Left < 100 || snapshot.Bottom - snapshot.Top < 20)
            {
                snapshot.Reason = "任务栏不可见";
                return snapshot;
            }

            Drawing.Rectangle bounds = WinForms.Screen.FromHandle(taskbar).Bounds;
            if (snapshot.Top >= bounds.Bottom - 4)
            {
                snapshot.Reason = "任务栏自动隐藏";
                return snapshot;
            }

            int state;
            if (SHQueryUserNotificationState(out state) == 0)
            {
                if (state == 1 || state == 3 || state == QunsPresentationMode)
                {
                    snapshot.Reason = "全屏应用";
                    return snapshot;
                }
            }

            if (IsForegroundFullscreen(taskbar, bounds))
            {
                snapshot.Reason = "全屏应用";
                return snapshot;
            }

            snapshot.Centered = ReadCentered();
            IntPtr start = FindWindowEx(taskbar, IntPtr.Zero, "Start", null);
            RECT startRect;
            if (start != IntPtr.Zero && GetWindowRect(start, out startRect))
                snapshot.StartLeft = startRect.Left;
            else
                snapshot.StartLeft = snapshot.Left + (snapshot.Right - snapshot.Left) / 3;

            IntPtr tray = FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
            RECT trayRect;
            snapshot.TrayLeft = tray != IntPtr.Zero && GetWindowRect(tray, out trayRect)
                ? trayRect.Left
                : snapshot.Right;

            snapshot.Visible = true;
            return snapshot;
        }

        private static bool IsForegroundFullscreen(IntPtr taskbar, Drawing.Rectangle bounds)
        {
            IntPtr foreground = GetForegroundWindow();
            if (foreground == IntPtr.Zero || foreground == taskbar) return false;

            var className = new System.Text.StringBuilder(128);
            if (GetClassName(foreground, className, className.Capacity) > 0)
            {
                string name = className.ToString();
                if (name.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Shell_SecondaryTrayWnd", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            RECT rect;
            if (!GetWindowRect(foreground, out rect)) return false;
            return rect.Left <= bounds.Left + 2 &&
                   rect.Top <= bounds.Top + 2 &&
                   rect.Right >= bounds.Right - 2 &&
                   rect.Bottom >= bounds.Bottom - 2;
        }

        private static bool ReadCentered()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("TaskbarAl");
                        if (value is int) return (int)value != 0;
                    }
                }
            }
            catch
            {
            }
            return true;
        }

        public static double DpiScale
        {
            get
            {
                try
                {
                    uint dpi = GetDpiForSystem();
                    if (dpi > 0) return dpi / 96.0;
                }
                catch
                {
                }
                return 1.0;
            }
        }

        [DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();
    }

    /// <summary>
    /// Display-only overlay for the empty part of a centered Windows 11
    /// taskbar. It never injects into Explorer and is click-through.
    /// </summary>
    public sealed class TaskbarWidgetController : IDisposable
    {
        private const int SwpNoMove = 0x0002;
        private const int SwpNoSize = 0x0001;
        private const int SwpNoActivate = 0x0010;
        private const int GwlExStyle = -20;
        private const int WsExTransparent = 0x00000020;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExNoActivate = 0x08000000;
        private const uint EventSystemForeground = 0x0003;
        private const uint WineventOutofcontext = 0x0000;

        private delegate void WinEventDelegate(IntPtr hook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint thread, uint time);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr module,
            WinEventDelegate callback, uint processId, uint threadId, uint flags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hook);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, int flags);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

        private readonly Action<string> statusChanged;
        private readonly DispatcherTimer timer;
        private readonly DispatcherTimer hideTimer;
        private readonly WinEventDelegate winEventProc;
        private IntPtr winEventHook;
        private List<DeviceReading> readings = new List<DeviceReading>();
        private Window window;
        private int mode;
        private bool contentDirty = true;
        private bool disposed;
        private string lastStatus;
        private bool highContrast = true;
        private Color normalColor = Color.FromRgb(0x4A, 0xDE, 0x80);
        private Color warnColor = Color.FromRgb(0xFB, 0xBF, 0x24);
        private Color dangerColor = Color.FromRgb(0xF8, 0x71, 0x71);

        public TaskbarWidgetController(Action<string> statusChanged)
        {
            this.statusChanged = statusChanged;
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.Tick += (s, e) => Refresh();

            hideTimer = new DispatcherTimer();
            hideTimer.Interval = TimeSpan.FromMilliseconds(450);
            hideTimer.Tick += (s, e) =>
            {
                hideTimer.Stop();
                if (!disposed) RefreshInternal(true);
            };

            winEventProc = OnForegroundChanged;
            try
            {
                winEventHook = SetWinEventHook(EventSystemForeground, EventSystemForeground,
                    IntPtr.Zero, winEventProc, 0, 0, WineventOutofcontext);
            }
            catch
            {
                winEventHook = IntPtr.Zero;
            }
        }

        public void SetMode(int value)
        {
            mode = value;
            contentDirty = true;
            if (mode == 0)
            {
                timer.Stop();
                hideTimer.Stop();
                HideWindow();
                Report("已关闭");
                return;
            }
            timer.Start();
            Refresh();
        }

        public void SetColors(Color normal, Color warn, Color danger)
        {
            normalColor = normal;
            warnColor = warn;
            dangerColor = danger;
            contentDirty = true;
            if (mode != 0) Refresh();
        }

        public void SetHighContrast(bool enabled)
        {
            highContrast = enabled;
            contentDirty = true;
            if (mode != 0) Refresh();
        }

        public void Update(List<DeviceReading> newReadings)
        {
            readings = newReadings == null ? new List<DeviceReading>() : new List<DeviceReading>(newReadings);
            contentDirty = true;
            if (mode != 0) Refresh();
        }

        public void RefreshTheme()
        {
            contentDirty = true;
            if (mode != 0) Refresh();
        }

        public void OnMainWindowHidden()
        {
            if (disposed || mode == 0) return;
            Refresh();
            if (window != null && window.IsVisible)
            {
                window.Topmost = true;
                ReassertTopmost();
            }
        }

        public void Refresh()
        {
            RefreshInternal(false);
        }

        private void RefreshInternal(bool forceHide)
        {
            if (disposed || mode == 0)
            {
                hideTimer.Stop();
                HideWindow();
                return;
            }

            TaskbarSnapshot snapshot = TaskbarGeometry.Get();
            if (!snapshot.Found || !snapshot.Visible)
            {
                HandleHidden(snapshot.Reason ?? "任务栏不可见", forceHide);
                return;
            }
            if (!snapshot.Centered)
            {
                HandleHidden("徽标靠左，已隐藏", forceHide);
                return;
            }
            if (readings.Count == 0)
            {
                HandleHidden("未发现设备", forceHide);
                return;
            }

            int count = Math.Min(readings.Count, 3);
            int width = 36 + count * 108;
            double scale = TaskbarGeometry.DpiScale;
            int available = snapshot.FreeLeft - 16;
            if (available < 120 * scale)
            {
                HandleHidden("左侧空间不足", forceHide);
                return;
            }
            int maxWidthDip = (int)(available / scale);
            if (maxWidthDip < width)
            {
                HandleHidden("左侧空间不足", forceHide);
                return;
            }
            if (width < 120)
            {
                HandleHidden("左侧空间不足", forceHide);
                return;
            }

            hideTimer.Stop();
            if (window == null) CreateWindow();
            window.Width = width;
            if (contentDirty)
            {
                window.Content = BuildContent(readings);
                contentDirty = false;
            }
            window.UpdateLayout();

            window.Left = (snapshot.Left + 8) / scale;
            window.Top = (snapshot.Top + (snapshot.Bottom - snapshot.Top - 32 * scale) / 2) / scale;
            if (!window.IsVisible) window.Show();
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle != IntPtr.Zero)
            {
                long style = GetWindowLongPtr(handle, GwlExStyle).ToInt64();
                style |= WsExTransparent | WsExToolWindow | WsExNoActivate;
                SetWindowLongPtr(handle, GwlExStyle, new IntPtr(style));
                ReassertTopmost();
            }
            Report("已显示：任务栏左侧");
        }

        private void HandleHidden(string reason, bool forceHide)
        {
            if (!forceHide && reason != "全屏应用")
            {
                if (!hideTimer.IsEnabled) hideTimer.Start();
                return;
            }
            hideTimer.Stop();
            HideWindow();
            Report(reason);
        }

        private void OnForegroundChanged(IntPtr hook, uint eventType, IntPtr hwnd,
            int idObject, int idChild, uint thread, uint time)
        {
            if (disposed || mode == 0 || window == null) return;
            try
            {
                if (window.Dispatcher.CheckAccess())
                {
                    ReassertTopmost();
                    Refresh();
                }
                else
                {
                    window.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (disposed) return;
                        ReassertTopmost();
                        Refresh();
                    }));
                }
            }
            catch
            {
            }
        }

        private void ReassertTopmost()
        {
            if (window == null) return;
            IntPtr handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            SetWindowPos(handle, new IntPtr(-1), 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        private int Mode
        {
            get { return mode; }
        }

        private void CreateWindow()
        {
            window = new Window();
            window.Owner = null;
            window.WindowStyle = WindowStyle.None;
            window.ResizeMode = ResizeMode.NoResize;
            window.AllowsTransparency = true;
            window.Background = Brushes.Transparent;
            window.ShowInTaskbar = false;
            window.Topmost = true;
            window.ShowActivated = false;
            window.Focusable = false;
            window.IsHitTestVisible = false;
            window.SizeToContent = SizeToContent.Manual;
            window.Height = 32;
        }

        private Color ColorFor(DeviceReading reading)
        {
            if (reading == null || !reading.Percent.HasValue) return Theme.MutedColor;
            int value = Math.Max(0, Math.Min(100, reading.Percent.Value));
            if (value <= 20) return dangerColor;
            if (value <= 40) return warnColor;
            return normalColor;
        }

        private UIElement BuildContent(List<DeviceReading> data)
        {
            var card = new Border();
            if (highContrast)
            {
                card.Background = new SolidColorBrush(Color.FromRgb(0x0E, 0x13, 0x18));
                card.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x36, 0x40));
                card.CornerRadius = new CornerRadius(8);
                card.Padding = new Thickness(9, 4, 9, 4);
                card.Opacity = 0.99;
            }
            else
            {
                card.Background = Theme.Panel;
                card.BorderBrush = Theme.PanelBorder;
                card.CornerRadius = new CornerRadius(9);
                card.Padding = new Thickness(8, 3, 8, 3);
                card.Opacity = 0.97;
            }
            card.BorderThickness = new Thickness(1);
            TextOptions.SetTextFormattingMode(card, TextFormattingMode.Display);

            var row = new StackPanel();
            row.Orientation = Orientation.Horizontal;
            row.VerticalAlignment = VerticalAlignment.Center;

            int count = Math.Min(data.Count, 3);
            var ordered = DevicePriority.Sort(data).Take(count).ToList();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (i > 0)
                {
                    var divider = new Border();
                    divider.Width = 1;
                    divider.Height = 16;
                    divider.Background = highContrast
                        ? new SolidColorBrush(Color.FromRgb(0x33, 0x3E, 0x49))
                        : Theme.PanelBorder;
                    divider.Margin = new Thickness(8, 0, 8, 0);
                    divider.Opacity = highContrast ? 1 : 0.7;
                    row.Children.Add(divider);
                }

                DeviceReading reading = ordered[i];
                Color color = ColorFor(reading);
                var chip = new StackPanel();
                chip.Orientation = Orientation.Horizontal;
                chip.VerticalAlignment = VerticalAlignment.Center;

                var dot = new Ellipse();
                dot.Width = 6;
                dot.Height = 6;
                dot.Fill = new SolidColorBrush(color);
                dot.VerticalAlignment = VerticalAlignment.Center;
                dot.Margin = new Thickness(0, 0, 5, 0);
                chip.Children.Add(dot);

                var name = new TextBlock();
                name.Text = reading.Name;
                name.FontFamily = Theme.UiFont;
                name.FontSize = highContrast ? 9.4 : 9.2;
                name.Foreground = highContrast
                    ? new SolidColorBrush(Color.FromRgb(0xE8, 0xED, 0xF2))
                    : Theme.Secondary;
                name.MaxWidth = 62;
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                name.VerticalAlignment = VerticalAlignment.Center;
                chip.Children.Add(name);

                var value = new TextBlock();
                value.Text = reading.Percent.HasValue
                    ? (reading.FromCache ? "~" : "") + reading.Percent.Value + "%"
                    : "—";
                value.FontFamily = Theme.UiFont;
                value.FontSize = highContrast ? 10.2 : 9.8;
                value.FontWeight = FontWeights.SemiBold;
                value.Foreground = reading.Percent.HasValue ? new SolidColorBrush(color) : Theme.Muted;
                value.Margin = new Thickness(5, 0, 0, 0);
                value.VerticalAlignment = VerticalAlignment.Center;
                chip.Children.Add(value);

                row.Children.Add(chip);
            }

            card.Child = row;
            return card;
        }

        private void HideWindow()
        {
            if (window != null && window.IsVisible) window.Hide();
        }

        private void Report(string text)
        {
            if (text == lastStatus) return;
            lastStatus = text;
            Log.Info("Taskbar widget: " + text);
            if (statusChanged != null) statusChanged(text);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            timer.Stop();
            hideTimer.Stop();
            if (winEventHook != IntPtr.Zero)
            {
                try { UnhookWinEvent(winEventHook); } catch { }
                winEventHook = IntPtr.Zero;
            }
            if (window != null)
            {
                try { window.Close(); } catch { }
                window = null;
            }
        }
    }
}
