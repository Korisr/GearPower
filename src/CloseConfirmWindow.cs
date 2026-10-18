using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace GearPower
{
    public enum CloseChoice
    {
        Cancel,
        Tray,
        Exit
    }

    /// <summary>
    /// Themed close confirmation: close to tray, exit completely, or cancel.
    /// The optional checkbox stores the chosen action for next time.
    /// </summary>
    public static class CloseConfirmWindow
    {
        public static CloseChoice Show(Window owner, out bool remember)
        {
            CloseChoice result = CloseChoice.Cancel;
            bool rememberChoice = false;

            var card = new Border();
            card.Width = 420;
            card.Background = Theme.Panel;
            card.BorderBrush = Theme.PanelBorder;
            card.BorderThickness = new Thickness(1);
            card.CornerRadius = new CornerRadius(14);
            card.Padding = new Thickness(22);
            card.Effect = new DropShadowEffect
            {
                BlurRadius = 28,
                ShadowDepth = 8,
                Opacity = 0.36,
                Color = Colors.Black
            };
            TextOptions.SetTextFormattingMode(card, TextFormattingMode.Display);

            var root = new StackPanel();

            var title = new TextBlock();
            title.Text = "关闭 GearPower";
            title.FontFamily = Theme.UiFont;
            title.FontSize = 15;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = Theme.Text;
            root.Children.Add(title);

            var message = new TextBlock();
            message.Text = "关闭主窗口后，GearPower 可以继续在系统托盘运行，也可以完全退出。";
            message.FontFamily = Theme.UiFont;
            message.FontSize = 11;
            message.Foreground = Theme.Secondary;
            message.TextWrapping = TextWrapping.Wrap;
            message.Margin = new Thickness(0, 8, 0, 0);
            root.Children.Add(message);

            var rememberBox = new CheckBox();
            rememberBox.Content = "记住我的选择，下次不再询问";
            rememberBox.FontFamily = Theme.UiFont;
            rememberBox.FontSize = 10.5;
            rememberBox.Foreground = Theme.Text;
            rememberBox.Margin = new Thickness(0, 16, 0, 0);
            root.Children.Add(rememberBox);

            var buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 20, 0, 0);

            var cancel = MakeButton(owner, "取消", "GhostButton");
            cancel.Click += (s, e) =>
            {
                result = CloseChoice.Cancel;
                rememberChoice = false;
                CloseDialog(owner);
            };
            buttons.Children.Add(cancel);

            var exit = MakeButton(owner, "退出程序", "GhostButton");
            exit.Foreground = Theme.Danger;
            exit.Margin = new Thickness(8, 0, 0, 0);
            exit.Click += (s, e) =>
            {
                result = CloseChoice.Exit;
                rememberChoice = rememberBox.IsChecked == true;
                CloseDialog(owner);
            };
            buttons.Children.Add(exit);

            var tray = MakeButton(owner, "关闭到托盘", "PrimaryButton");
            tray.Margin = new Thickness(8, 0, 0, 0);
            tray.Background = Theme.Accent;
            tray.Foreground = Brushes.White;
            tray.BorderBrush = Theme.Accent;
            tray.FontWeight = FontWeights.SemiBold;
            tray.Click += (s, e) =>
            {
                result = CloseChoice.Tray;
                rememberChoice = rememberBox.IsChecked == true;
                CloseDialog(owner);
            };
            buttons.Children.Add(tray);
            root.Children.Add(buttons);

            card.Child = root;

            var host = new Grid();
            host.Margin = new Thickness(16);
            host.Children.Add(card);

            var dialog = new Window();
            dialog.Owner = owner;
            dialog.WindowStyle = WindowStyle.None;
            dialog.ResizeMode = ResizeMode.NoResize;
            dialog.AllowsTransparency = true;
            dialog.Background = Brushes.Transparent;
            dialog.ShowInTaskbar = false;
            dialog.SizeToContent = SizeToContent.WidthAndHeight;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            dialog.Content = host;
            dialog.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    result = CloseChoice.Cancel;
                    dialog.Close();
                }
            };
            dialog.Closed += (s, e) =>
            {
                // Keep the last selected result; Cancel is the default.
            };

            owner.Tag = dialog;
            dialog.ShowDialog();
            owner.Tag = null;

            remember = rememberChoice;
            return result;
        }

        private static void CloseDialog(Window owner)
        {
            Window dialog = owner.Tag as Window;
            if (dialog != null) dialog.Close();
        }

        private static Button MakeButton(Window owner, string text, string styleKey)
        {
            var button = new Button();
            button.Content = text;
            button.MinWidth = 88;
            button.Padding = new Thickness(14, 7, 14, 7);
            button.FontFamily = Theme.UiFont;
            button.FontSize = 10.5;
            try
            {
                Style style = owner.TryFindResource(styleKey) as Style;
                if (style != null) button.Style = style;
            }
            catch
            {
            }
            return button;
        }
    }
}
