using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace GearPower
{
    public static class LogWindow
    {
        private static Window window;
        private static TextBox output;
        private static DispatcherTimer timer;
        private static long revision = -1;

        public static void Show(Window owner)
        {
            if (window != null && window.IsVisible)
            {
                window.Activate();
                return;
            }

            window = new Window();
            window.Title = "GearPower 实时日志";
            window.Width = 840;
            window.Height = 560;
            window.MinWidth = 560;
            window.MinHeight = 320;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (owner != null) window.Owner = owner;
            window.Background = Theme.Panel;
            window.Foreground = Theme.Text;

            var root = new DockPanel();
            root.Margin = new Thickness(14);

            var actions = new StackPanel();
            actions.Orientation = Orientation.Horizontal;
            actions.Margin = new Thickness(0, 0, 0, 10);
            DockPanel.SetDock(actions, Dock.Top);
            root.Children.Add(actions);

            var title = new TextBlock();
            title.Text = "最近 1000 行日志";
            title.FontSize = 12;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Theme.Text;
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(0, 0, 16, 0);
            actions.Children.Add(title);

            Button refresh = MakeButton("刷新", owner);
            refresh.Click += (s, e) => Refresh(true);
            actions.Children.Add(refresh);

            Button clear = MakeButton("清空", owner);
            clear.Margin = new Thickness(6, 0, 0, 0);
            clear.Click += (s, e) =>
            {
                Log.Recent.Clear();
                Refresh(true);
            };
            actions.Children.Add(clear);

            Button export = MakeButton("导出", owner);
            export.Margin = new Thickness(6, 0, 0, 0);
            export.Click += (s, e) => Export();
            actions.Children.Add(export);

            Button close = MakeButton("关闭", owner);
            close.Margin = new Thickness(6, 0, 0, 0);
            close.Click += (s, e) => window.Close();
            actions.Children.Add(close);

            var hint = new TextBlock();
            hint.Text = Log.LogPath ?? "日志文件不可用";
            hint.FontSize = 9.5;
            hint.Foreground = Theme.Muted;
            hint.VerticalAlignment = VerticalAlignment.Center;
            hint.Margin = new Thickness(12, 0, 0, 0);
            hint.TextTrimming = TextTrimming.CharacterEllipsis;
            actions.Children.Add(hint);

            output = new TextBox();
            output.FontFamily = new FontFamily("Consolas");
            output.FontSize = 11;
            output.IsReadOnly = true;
            output.AcceptsReturn = true;
            output.TextWrapping = TextWrapping.NoWrap;
            output.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            output.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            output.Background = Theme.Input;
            output.Foreground = Theme.Text;
            output.BorderBrush = Theme.InputBorder;
            output.Padding = new Thickness(10);
            root.Children.Add(output);

            window.Content = root;
            window.Closed += (s, e) =>
            {
                if (timer != null) timer.Stop();
                timer = null;
                output = null;
                window = null;
            };

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(900);
            timer.Tick += (s, e) => Refresh(false);
            timer.Start();

            window.Show();
            Refresh(true);
        }

        private static Button MakeButton(string text, Window owner)
        {
            var button = new Button();
            button.Content = text;
            button.FontSize = 10;
            button.Padding = new Thickness(10, 4, 10, 4);
            button.Foreground = Theme.Secondary;
            button.Background = Theme.GhostHover;
            button.BorderBrush = Theme.OptionBorder;
            button.BorderThickness = new Thickness(1);
            try
            {
                Style style = owner != null ? owner.TryFindResource("GhostButton") as Style : null;
                if (style != null) button.Style = style;
            }
            catch
            {
            }
            return button;
        }

        private static void Refresh(bool force)
        {
            if (output == null) return;
            long current = Log.Recent.Revision;
            if (!force && current == revision) return;
            revision = current;
            string text = string.Join(Environment.NewLine, Log.Recent.Snapshot());
            output.Text = text;
            if (force) output.ScrollToEnd();
        }

        private static void Export()
        {
            var dialog = new SaveFileDialog();
            dialog.Title = "导出 GearPower 日志";
            dialog.Filter = "文本文件 (*.txt)|*.txt|日志文件 (*.log)|*.log";
            dialog.FileName = "GearPower-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            if (dialog.ShowDialog(window) != true) return;
            try
            {
                Log.Recent.Export(dialog.FileName);
                MessageBox.Show(window, "已导出：" + dialog.FileName, "GearPower", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(window, "导出失败：" + ex.Message, "GearPower", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
