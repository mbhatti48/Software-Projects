using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Bolo.App.Ui;

/// <summary>
/// A click on the tray icon: the last three transcripts in a small panel by the tray, each
/// with Copy, and a link to the full window. Closes when it loses focus or on Esc.
/// </summary>
public sealed class TrayPopover : Window
{
    public TrayPopover(Theme t, DictationController controller, Action openMainWindow)
    {
        ModernStyles.Apply(this, t);
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = Theme.BodyFont;
        ResizeMode = ResizeMode.NoResize;

        var stack = new StackPanel { Width = 340 };
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var open = new TextBlock
        {
            Text = "Open Bolo ›",
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = t.AccentInk,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        open.MouseLeftButtonUp += (_, _) => { Close(); openMainWindow(); };
        DockPanel.SetDock(open, Dock.Right);
        head.Children.Add(open);
        var section = UiKit.Section(t, "Recent");
        section.Margin = new Thickness(0);
        head.Children.Add(section);
        stack.Children.Add(head);

        var entries = controller.History.ReadAllDays()
            .SelectMany(d => Enumerable.Reverse(d.Entries))
            .Take(3)
            .ToList();
        if (entries.Count == 0)
        {
            stack.Children.Add(UiKit.Empty(t, "Nothing dictated yet. Your last three transcripts show up here."));
        }
        foreach (var entry in entries)
        {
            var text = UiKit.Label(t, entry.Text);
            text.FontSize = 13;
            text.LineHeight = 18;
            text.MaxHeight = 54;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            var meta = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };
            var copy = UiKit.GlyphButton(t, UiKit.GlyphCopy, "Copy");
            copy.Padding = new Thickness(8, 3, 9, 3);
            copy.FontSize = 12;
            var copied = false;
            copy.Click += (_, _) =>
            {
                Clipboard.SetText(entry.Text);
                if (!copied)
                {
                    copied = true;
                    ((TextBlock)((StackPanel)copy.Content).Children[1]).Text = "Copied";
                }
            };
            DockPanel.SetDock(copy, Dock.Right);
            meta.Children.Add(copy);
            var when = new TextBlock
            {
                Text = entry.Timestamp.Date == DateTime.Today
                    ? entry.Timestamp.ToString("h:mm tt")
                    : entry.Timestamp.ToString("ddd h:mm tt"),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = t.TextMuted,
                VerticalAlignment = VerticalAlignment.Center,
            };
            meta.Children.Add(when);
            var inner = new StackPanel();
            inner.Children.Add(text);
            inner.Children.Add(meta);
            stack.Children.Add(UiKit.Raised(t, inner));
        }

        var card = UiKit.Card(t, stack, padding: 14);
        card.Margin = new Thickness(12);
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 24, ShadowDepth = 6, Opacity = 0.45, Color = Colors.Black };
        Content = card;

        Deactivated += (_, _) => Close();
        KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        SizeChanged += (_, _) =>
        {
            // By the tray: bottom-right of the primary work area (the window carries a 12 px transparent margin).
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth;
            Top = area.Bottom - ActualHeight;
        };
    }
}
