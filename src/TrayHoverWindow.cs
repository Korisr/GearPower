using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace GearPower
{
    /// <summary>
    /// Rich hover card shown while the cursor rests on the tray icon. It is a
    /// non-activating, click-through top-level window so hovering never steals
    /// focus from the current application.
    /// </summary>
    public static class TrayHoverWindow
    {
        private static Window current;

        public static bool IsVisible
        {
            get { return current != null && current.IsVisible; }
        }

        public static void Show(int screenX, int screenY, List<DeviceReading> readings)
        {
            Hide();

            List<DeviceReading> data = readings == null
                ? new List<DeviceReading>()
                : new List<DeviceReading>(readings);
            DeviceReading primary = TrayIconRenderer.Primary(data);

            var card = new Border();
            card.Width = 312;
            card.Background = Theme.Panel;
            card.BorderBrush = Theme.PanelBorder;
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(13);
            card.Padding = new Thickness(6);
            card.Effect = new DropShadowEffect
            {
                BlurRadius = 24,
                ShadowDepth = 7,
                Opacity = 0.34,
                Color = Colors.Black
            };
            TextOptions.SetTextFormattingMode(card, TextFormattingMode.Display);
            card.SnapsToDevicePixels = true;

            var root = new StackPanel();
            root.Children.Add(BuildHeader(data, primary));

            if (data.Count == 0)
            {
                var empty = Text("未发现设备", 10.5, Theme.Muted, FontWeights.Normal);
                empty.Margin = new Thickness(9, 6, 9, 10);
                root.Children.Add(empty);
            }
            else
            {
                int shown = 0;
                foreach (DeviceReading reading in data)
                {
                    if (shown >= 6)
                    {
                        var more = Text("还有 " + (data.Count - shown) + " 台设备", 9, Theme.Muted, FontWeights.Normal);
                        more.Margin = new Thickness(29, 2, 9, 7);
                        root.Children.Add(more);
                        break;
                    }
                    root.Children.Add(BuildRow(reading, primary, data.Count));
                    shown++;
                }
            }

            card.Child = root;

            var host = new Grid();
            host.Margin = new Thickness(13);
            host.Children.Add(card);

            var hover = new Window();
            hover.WindowStyle = WindowStyle.None;
            hover.ResizeMode = ResizeMode.NoResize;
            hover.AllowsTransparency = true;
            hover.Background = Brushes.Transparent;
            hover.ShowInTaskbar = false;
            hover.Topmost = true;
            hover.ShowActivated = false;
            hover.Focusable = false;
            hover.IsHitTestVisible = false;
            hover.SizeToContent = SizeToContent.WidthAndHeight;
            hover.WindowStartupLocation = WindowStartupLocation.Manual;
            hover.Content = host;
            hover.Opacity = 0;
            hover.Closed += (s, e) =>
            {
                if (ReferenceEquals(current, hover)) current = null;
            };

            current = hover;
            hover.Show();
            hover.UpdateLayout();
            Position(hover, screenX, screenY);

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(100));
            hover.BeginAnimation(Window.OpacityProperty, fade);
        }

        private static UIElement BuildHeader(List<DeviceReading> readings, DeviceReading primary)
        {
            var header = new Grid();
            header.Margin = new Thickness(9, 7, 9, 6);
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var title = Text("GearPower", 11.5, Theme.Text, FontWeights.SemiBold);
            header.Children.Add(title);

            string subtitleText;
            if (readings.Count == 0) subtitleText = "0 台设备";
            else if (primary != null && primary.Percent.HasValue)
                subtitleText = readings.Count + " 台设备 · 最低 " + primary.Percent.Value + "%";
            else subtitleText = readings.Count + " 台设备 · 暂无电量";
            var subtitle = Text(subtitleText, 9, Theme.Muted, FontWeights.Normal);
            subtitle.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(subtitle, 1);
            header.Children.Add(subtitle);
            return header;
        }

        private static UIElement BuildRow(DeviceReading reading, DeviceReading primary, int count)
        {
            Color color = TrayIconRenderer.ColorFor(reading);
            var row = new Grid();
            row.Margin = new Thickness(5, 2, 5, 2);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new Ellipse();
            dot.Width = 7;
            dot.Height = 7;
            dot.Fill = new SolidColorBrush(color);
            dot.VerticalAlignment = VerticalAlignment.Center;
            dot.Margin = new Thickness(5, 0, 0, 0);
            row.Children.Add(dot);

            var texts = new StackPanel();
            texts.VerticalAlignment = VerticalAlignment.Center;
            texts.Margin = new Thickness(2, 0, 8, 0);
            var nameRow = new StackPanel();
            nameRow.Orientation = Orientation.Horizontal;
            var name = Text(reading.Name, 10.5, Theme.Text, FontWeights.Normal);
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.MaxWidth = 150;
            nameRow.Children.Add(name);
            bool lowest = count > 1 && primary != null && primary.Percent.HasValue &&
                          ReferenceEquals(reading, primary);
            if (lowest)
            {
                var badge = new Border();
                badge.Background = Theme.AccentDim;
                badge.CornerRadius = new CornerRadius(3.5);
                badge.Padding = new Thickness(3, 0, 3, 0);
                badge.Margin = new Thickness(5, 0, 0, 0);
                badge.VerticalAlignment = VerticalAlignment.Center;
                badge.Child = Text("最低", 8, Theme.Accent, FontWeights.SemiBold);
                nameRow.Children.Add(badge);
            }
            texts.Children.Add(nameRow);

            string status;
            if (reading.FromCache) status = "上次读数";
            else if (reading.Asleep) status = "设备休眠";
            else if (reading.Charging) status = "正在充电";
            else if (!string.IsNullOrEmpty(reading.Protocol)) status = reading.Protocol;
            else status = reading.Transport;
            var statusText = Text(status, 8.5, Theme.Muted, FontWeights.Normal);
            statusText.Margin = new Thickness(0, 1, 0, 0);
            statusText.TextTrimming = TextTrimming.CharacterEllipsis;
            statusText.MaxWidth = 190;
            texts.Children.Add(statusText);
            Grid.SetColumn(texts, 1);
            row.Children.Add(texts);

            var values = new StackPanel();
            values.VerticalAlignment = VerticalAlignment.Center;
            values.HorizontalAlignment = HorizontalAlignment.Right;
            string value = reading.Percent.HasValue
                ? (reading.FromCache ? "上次 " : "") + reading.Percent.Value + "%"
                : (reading.Asleep ? "休眠" : "—");
            Brush valueBrush = reading.Percent.HasValue ? new SolidColorBrush(color) : Theme.Muted;
            var valueText = Text(value, 11.5, valueBrush, FontWeights.SemiBold);
            valueText.HorizontalAlignment = HorizontalAlignment.Right;
            values.Children.Add(valueText);
            Grid.SetColumn(values, 2);
            row.Children.Add(values);

            return row;
        }

        private static TextBlock Text(string value, double size, Brush brush, FontWeight weight)
        {
            var text = new TextBlock();
            text.Text = value;
            text.FontFamily = Theme.UiFont;
            text.FontSize = size;
            text.Foreground = brush;
            text.FontWeight = weight;
            text.VerticalAlignment = VerticalAlignment.Center;
            return text;
        }

        private static void Position(Window hover, int screenX, int screenY)
        {
            try
            {
                Drawing.Rectangle work = WinForms.Screen.FromPoint(new Drawing.Point(screenX, screenY)).WorkingArea;
                DpiScale dpi = VisualTreeHelper.GetDpi(hover);
                double scale = dpi.DpiScaleX <= 0 ? 1 : dpi.DpiScaleX;
                double x = screenX / scale;
                double y = screenY / scale;
                double width = hover.ActualWidth;
                double height = hover.ActualHeight;
                double left = work.Left / scale;
                double top = work.Top / scale;
                double right = work.Right / scale;
                double bottom = work.Bottom / scale;

                x = x - 18;
                if (x + width > right - 6) x = right - width - 6;
                if (x < left + 6) x = left + 6;
                if (y - height - 10 > top + 6) y = y - height - 10;
                else y = y + 18;
                if (y + height > bottom - 6) y = bottom - height - 6;

                hover.Left = x;
                hover.Top = y;
            }
            catch
            {
            }
        }

        public static void Hide()
        {
            Window hover = current;
            current = null;
            if (hover == null) return;
            try { hover.Close(); } catch { }
        }
    }
}
