using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

public sealed class HistoryPage : UserControl
{
    private readonly Theme _theme;
    private readonly DictationController _controller;
    private readonly AppSettings _settings;
    private readonly StackPanel _list = new();
    private readonly TextBlock _notice;

    public HistoryPage(Theme t, DictationController controller, AppSettings settings)
    {
        _theme = t;
        _controller = controller;
        _settings = settings;

        var panel = new DockPanel();
        var header = UiKit.PageHeader(t, "History",
            "Every transcript, newest first. Fix a mistake with Edit & learn and Bolo stops making it.");
        DockPanel.SetDock(header, Dock.Top);
        panel.Children.Add(header);

        // Inline notice instead of a message box after a correction is learned.
        _notice = UiKit.Label(t, "", muted: true);
        _notice.Foreground = t.AccentInk;
        _notice.Margin = new Thickness(0, 0, 0, 12);
        _notice.Visibility = Visibility.Collapsed;
        DockPanel.SetDock(_notice, Dock.Top);
        panel.Children.Add(_notice);

        panel.Children.Add(new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(0, 0, 8, 24),
        });
        Content = panel;

        Refresh();
    }

    private void Refresh()
    {
        _list.Children.Clear();
        var days = _controller.History.ReadAllDays();
        if (days.Count == 0)
        {
            _list.Children.Add(UiKit.Empty(_theme,
                $"Nothing here yet. Hold {_settings.Hotkey} in any text field and say something; every transcript lands here, grouped by day."));
            return;
        }

        foreach (var day in days)
        {
            var head = UiKit.Section(_theme, day.Date == DateTime.Today ? "Today" :
                day.Date == DateTime.Today.AddDays(-1) ? "Yesterday" : day.Date.ToString("dddd, d MMMM yyyy"));
            head.Margin = new Thickness(0, 6, 0, 10);
            _list.Children.Add(head);

            foreach (var entry in Enumerable.Reverse(day.Entries))
                _list.Children.Add(EntryCard(entry));
        }
    }

    private UIElement EntryCard(TranscriptEntry entry)
    {
        var text = UiKit.Label(_theme, entry.Text);
        text.Foreground = _theme.Text;

        var meta = new TextBlock
        {
            Text = string.Join("  ·  ", new[]
            {
                entry.Timestamp.ToString("h:mm tt"),
                string.IsNullOrEmpty(entry.TargetApp) ? null : entry.TargetApp,
                $"{entry.AudioSeconds:F0} s of audio",
                $"{entry.TranscribeSeconds:F1} s on {entry.Runtime}",
            }.Where(s => s is not null)),
            FontSize = 12,
            Foreground = _theme.TextFaint,
            Margin = new Thickness(0, 8, 0, 0),
        };

        var copy = UiKit.GlyphButton(_theme, UiKit.GlyphCopy, "Copy");
        copy.Click += (_, _) => Clipboard.SetText(entry.Text);

        var edit = UiKit.GlyphButton(_theme, UiKit.GlyphEdit, "Edit & learn");
        edit.Click += (_, _) =>
        {
            var dialog = new FixTranscriptWindow(entry.Text, _settings) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                var learned = _controller.CorrectEntry(entry, dialog.EditedText);
                _notice.Text = learned > 0
                    ? $"Learned {learned} correction{(learned == 1 ? "" : "s")}. {(learned == 1 ? "It applies" : "They apply")} from the next dictation on."
                    : "Transcript updated. No new correction pattern was found in the edit.";
                _notice.Visibility = Visibility.Visible;
                Refresh();
            }
        };

        var actions = UiKit.Row(copy, edit);
        actions.Margin = new Thickness(0, 12, 0, 0);

        var stack = new StackPanel();
        stack.Children.Add(text);
        stack.Children.Add(meta);
        stack.Children.Add(actions);
        return UiKit.Card(_theme, stack);
    }
}
