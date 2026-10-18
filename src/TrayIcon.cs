using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Threading;
using WpfColor = System.Windows.Media.Color;
using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace GearPower
{
    public sealed class TrayMenuEventArgs : EventArgs
    {
        public int ScreenX { get; private set; }
        public int ScreenY { get; private set; }
        public List<DeviceReading> Readings { get; private set; }

        public TrayMenuEventArgs(int screenX, int screenY, List<DeviceReading> readings)
        {
            ScreenX = screenX;
            ScreenY = screenY;
            Readings = readings;
        }
    }

    /// <summary>
    /// System tray icon: one ring for the device with the lowest battery,
    /// plus a large two-digit number in the center. Ring color follows the
    /// battery level (green / amber / red). All devices remain available in
    /// the tooltip and the custom tray menu.
    /// </summary>
    public sealed class TrayController : IDisposable
    {
        private readonly WinForms.NotifyIcon notify;
        private List<DeviceReading> readings = new List<DeviceReading>();
        private readonly List<Drawing.Icon> retiredIcons = new List<Drawing.Icon>();
        private readonly DispatcherTimer hoverDelay;
        private readonly DispatcherTimer hoverWatch;
        private readonly DispatcherTimer taskbarThemeWatch;
        private bool taskbarLight;
        private Drawing.Point hoverAnchor;
        private DateTime hoverShownAt;
        private Drawing.Icon currentIcon;
        private bool balloonShown;
        private bool startupHintShown;
        private bool disposed;

        public bool HoverEnabled { get; set; }

        public event EventHandler OpenRequested;
        public event EventHandler<TrayMenuEventArgs> MenuRequested;

        public TrayController()
        {
            HoverEnabled = true;
            hoverDelay = new DispatcherTimer();
            hoverDelay.Interval = TimeSpan.FromMilliseconds(520);
            hoverDelay.Tick += (s, e) =>
            {
                hoverDelay.Stop();
                ShowHover();
            };
            hoverWatch = new DispatcherTimer();
            hoverWatch.Interval = TimeSpan.FromMilliseconds(180);
            hoverWatch.Tick += (s, e) => WatchHover();

            taskbarLight = TaskbarTheme.IsLight;
            taskbarThemeWatch = new DispatcherTimer();
            taskbarThemeWatch.Interval = TimeSpan.FromSeconds(3);
            taskbarThemeWatch.Tick += (s, e) =>
            {
                bool light = TaskbarTheme.IsLight;
                if (light == taskbarLight) return;
                taskbarLight = light;
                Update(readings);
            };

            notify = new WinForms.NotifyIcon();
            notify.Text = "";
            notify.Visible = true;
            notify.DoubleClick += (s, e) => RaiseOpen();
            notify.MouseMove += (s, e) =>
            {
                if (!HoverEnabled || TrayHoverWindow.IsVisible) return;
                hoverAnchor = WinForms.Cursor.Position;
                hoverDelay.Stop();
                hoverDelay.Start();
            };
            notify.MouseClick += (s, e) =>
            {
                CancelHover();
                if (e.Button == WinForms.MouseButtons.Left) RaiseOpen();
                else if (e.Button == WinForms.MouseButtons.Right) RaiseMenu();
            };
            notify.Icon = TrayIconRenderer.Render(readings);
            currentIcon = notify.Icon;
            taskbarThemeWatch.Start();
            Log.Info("Tray icon created, visible=" + notify.Visible);
        }

        public void Update(List<DeviceReading> newReadings)
        {
            readings = newReadings == null ? new List<DeviceReading>() : new List<DeviceReading>(newReadings);
            try
            {
                Drawing.Icon icon = TrayIconRenderer.Render(readings);
                Drawing.Icon old = currentIcon;
                currentIcon = icon;
                notify.Icon = icon;
                if (old != null)
                {
                    // Keep a couple of old handles alive: Explorer can still be
                    // using the previous handle while switching icons.
                    retiredIcons.Add(old);
                    while (retiredIcons.Count > 2)
                    {
                        retiredIcons[0].Dispose();
                        retiredIcons.RemoveAt(0);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug("Tray icon update failed: " + ex.Message);
            }
            notify.Text = "";
            if (TrayHoverWindow.IsVisible) CancelHover();
        }

        public void ShowBalloonOnce()
        {
            if (balloonShown || notify == null) return;
            balloonShown = true;
            try
            {
                notify.BalloonTipTitle = "GearPower 仍在后台运行";
                notify.BalloonTipText = "双击托盘图标可以重新打开主界面。";
                notify.BalloonTipIcon = WinForms.ToolTipIcon.Info;
                notify.ShowBalloonTip(3000);
            }
            catch
            {
            }
        }

        public void EnsureVisible()
        {
            if (notify == null) return;
            try
            {
                // Re-add once the WPF message loop is running. Some Windows 11
                // builds ignore the first NIM_ADD if it happens too early.
                if (notify.Visible) notify.Visible = false;
                notify.Visible = true;
                Log.Info("Tray icon re-added, visible=" + notify.Visible);
            }
            catch (Exception ex)
            {
                Log.Error("Tray icon re-add failed: " + ex.Message);
            }
        }

        public void ShowStartupHint()
        {
            if (startupHintShown || notify == null) return;
            startupHintShown = true;
            try
            {
                notify.BalloonTipTitle = "GearPower 已在后台待命";
                notify.BalloonTipText = "如果任务栏没有图标，请点击 ^ 打开隐藏图标区，把 GearPower 拖到任务栏。";
                notify.BalloonTipIcon = WinForms.ToolTipIcon.Info;
                notify.ShowBalloonTip(6000);
            }
            catch
            {
            }
        }

        private void RaiseOpen()
        {
            EventHandler handler = OpenRequested;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void RaiseMenu()
        {
            EventHandler<TrayMenuEventArgs> handler = MenuRequested;
            if (handler == null) return;
            Drawing.Point point = WinForms.Cursor.Position;
            handler(this, new TrayMenuEventArgs(point.X, point.Y, new List<DeviceReading>(readings)));
        }

        private void ShowHover()
        {
            if (disposed || TrayHoverWindow.IsVisible) return;
            Drawing.Point point = WinForms.Cursor.Position;
            if (Math.Abs(point.X - hoverAnchor.X) > 24 || Math.Abs(point.Y - hoverAnchor.Y) > 24)
                return;
            hoverShownAt = DateTime.Now;
            TrayHoverWindow.Show(point.X, point.Y, readings);
            hoverWatch.Start();
        }

        private void WatchHover()
        {
            if (!TrayHoverWindow.IsVisible)
            {
                hoverWatch.Stop();
                return;
            }
            if ((DateTime.Now - hoverShownAt).TotalSeconds > 9)
            {
                CancelHover();
                return;
            }
            Drawing.Point point = WinForms.Cursor.Position;
            if (Math.Abs(point.X - hoverAnchor.X) > 30 || Math.Abs(point.Y - hoverAnchor.Y) > 30)
                CancelHover();
        }

        private void CancelHover()
        {
            hoverDelay.Stop();
            hoverWatch.Stop();
            TrayHoverWindow.Hide();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                hoverDelay.Stop();
                hoverWatch.Stop();
                taskbarThemeWatch.Stop();
                TrayHoverWindow.Hide();
                notify.Visible = false;
                notify.Dispose();
            }
            catch
            {
            }
            foreach (Drawing.Icon icon in retiredIcons)
            {
                try { icon.Dispose(); } catch { }
            }
            retiredIcons.Clear();
            if (currentIcon != null) currentIcon.Dispose();
        }
    }

    public static class TrayIconRenderer
    {
        public static Drawing.Icon Render(List<DeviceReading> readings)
        {
            int size = 16;
            try
            {
                uint dpi = GetDpiForSystem();
                if (dpi > 0) size = (int)Math.Round(16.0 * dpi / 96.0);
            }
            catch
            {
            }
            size = Math.Max(16, Math.Min(32, size));

            using (Drawing.Bitmap bitmap = RenderBitmap(readings, size, TaskbarTheme.IsLight))
            {
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Drawing.Icon temporary = Drawing.Icon.FromHandle(handle))
                    {
                        return (Drawing.Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForSystem();

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        public static DeviceReading Primary(List<DeviceReading> readings)
        {
            if (readings == null || readings.Count == 0) return null;
            DeviceReading best = null;
            foreach (DeviceReading reading in readings)
            {
                if (best == null)
                {
                    best = reading;
                    continue;
                }
                if (!reading.Percent.HasValue) continue;
                if (!best.Percent.HasValue || reading.Percent.Value < best.Percent.Value)
                    best = reading;
            }
            return best;
        }

        public static WpfColor ColorFor(DeviceReading reading)
        {
            return ColorFor(reading != null && reading.Percent.HasValue ? reading.Percent : null);
        }

        public static WpfColor ColorFor(int? percent)
        {
            if (!percent.HasValue) return WpfColor.FromRgb(0x94, 0xA3, 0xB8);
            int value = Math.Max(0, Math.Min(100, percent.Value));
            if (value <= 20) return WpfColor.FromRgb(0xF8, 0x71, 0x71); // red
            if (value <= 40) return WpfColor.FromRgb(0xFB, 0xBF, 0x24); // amber
            return WpfColor.FromRgb(0x4A, 0xDE, 0x80); // green
        }

        public static Drawing.Bitmap RenderBitmap(List<DeviceReading> readings, int size)
        {
            return RenderBitmap(readings, size, false);
        }

        public static Drawing.Bitmap RenderBitmap(List<DeviceReading> readings, int size, bool lightTaskbar)
        {
            var bitmap = new Drawing.Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Drawing.Graphics graphics = Drawing.Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = size <= 26
                    ? System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit
                    : System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                graphics.Clear(Drawing.Color.Transparent);

                DeviceReading primary = Primary(readings);
                bool hasPercent = primary != null && primary.Percent.HasValue;
                int? percent = hasPercent ? primary.Percent : null;
                bool stale = primary != null && (primary.FromCache || primary.Asleep);
                WpfColor color = ColorFor(percent);
                Drawing.Color draw = ToDrawing(color);

                // iPhone-style horizontal battery: rounded body, terminal nub,
                // and a left-to-right fill. No outer badge.
                float bodyWidth = size * 0.76f;
                float bodyHeight = size * 0.52f;
                float capWidth = Math.Max(1.7f, size * 0.10f);
                float gap = Math.Max(0.6f, size * 0.035f);
                float totalWidth = bodyWidth + gap + capWidth;
                float left = (size - totalWidth) / 2f;
                float top = (size - bodyHeight) / 2f;
                float outlineWidth = Math.Max(1f, size * 0.055f);
                float bodyRadius = Math.Max(1.2f, size * 0.12f);

                Drawing.Color outlineColor;
                if (!hasPercent) outlineColor = lightTaskbar
                    ? Drawing.Color.FromArgb(190, 100, 116, 139)
                    : Drawing.Color.FromArgb(190, 148, 163, 184);
                else outlineColor = lightTaskbar
                    ? Drawing.Color.FromArgb(230, 30, 41, 59)
                    : Drawing.Color.FromArgb(235, 248, 250, 252);

                var body = new Drawing.RectangleF(left, top, bodyWidth, bodyHeight);
                using (var bodyPath = RoundedRect(body, bodyRadius))
                using (var outline = new Drawing.Pen(outlineColor, outlineWidth))
                {
                    outline.LineJoin = LineJoin.Round;
                    graphics.DrawPath(outline, bodyPath);
                }

                float capHeight = bodyHeight * 0.46f;
                var cap = new Drawing.RectangleF(left + bodyWidth + gap, top + (bodyHeight - capHeight) / 2f,
                    capWidth, capHeight);
                using (var capPath = RoundedRect(cap, capWidth * 0.35f))
                using (var capBrush = new Drawing.SolidBrush(outlineColor))
                {
                    graphics.FillPath(capBrush, capPath);
                }

                if (hasPercent)
                {
                    int value = Math.Max(0, Math.Min(100, percent.Value));
                    float pad = Math.Max(1f, size * 0.07f);
                    var inner = new Drawing.RectangleF(
                        body.X + pad, body.Y + pad,
                        Math.Max(0, body.Width - pad * 2), Math.Max(0, body.Height - pad * 2));
                    if (value > 0)
                    {
                        float fillWidth = inner.Width * value / 100f;
                        fillWidth = Math.Max(Math.Min(inner.Width, 1.4f), fillWidth);
                        var fill = new Drawing.RectangleF(inner.X, inner.Y, fillWidth, inner.Height);
                        using (var fillPath = RoundedRect(fill, Math.Max(0.7f, size * 0.035f)))
                        using (var fillBrush = new Drawing.SolidBrush(
                            Drawing.Color.FromArgb(stale ? 155 : 255, draw.R, draw.G, draw.B)))
                        {
                            graphics.FillPath(fillBrush, fillPath);
                        }
                    }

                    // At 16px there is no room for digits. At 24px+ (125-200%
                    // scaling) the number fits inside the battery body.
                    if (size >= 24)
                    {
                        string text = percent.Value.ToString();
                        float fontSize = size * 0.30f;
                        if (text.Length >= 3) fontSize *= 0.86f;
                        Drawing.Color textColor = lightTaskbar
                            ? Drawing.Color.FromArgb(245, 15, 23, 42)
                            : Drawing.Color.FromArgb(245, 255, 255, 255);
                        using (var font = new Drawing.Font("Segoe UI", fontSize, Drawing.FontStyle.Bold, Drawing.GraphicsUnit.Pixel))
                        using (var brush = new Drawing.SolidBrush(textColor))
                        using (var format = new StringFormat())
                        {
                            format.Alignment = StringAlignment.Center;
                            format.LineAlignment = StringAlignment.Center;
                            format.FormatFlags = StringFormatFlags.NoWrap;
                            var textRect = new Drawing.RectangleF(body.X, body.Y - size * 0.01f, body.Width, body.Height);
                            graphics.DrawString(text, font, brush, textRect, format);
                        }
                    }
                }
            }
            return bitmap;
        }

        private static GraphicsPath RoundedRect(Drawing.RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Max(0.5f, Math.Min(radius * 2, Math.Min(rect.Width, rect.Height)));
            path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static Drawing.Color ToDrawing(WpfColor color)
        {
            return Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
        }
    }
}
