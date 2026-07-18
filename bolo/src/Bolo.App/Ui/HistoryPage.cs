using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

public sealed class HistoryPage : UserControl
{
    private readonly Theme _theme;
    private readonly DictationController _controller;
    private readonly StackPanel _list = new();

    public HistoryPage(Theme t, DictationController controller)
    {
        _theme = t;
        _controller = controller;

        var panel = new DockPanel();
        var title = UiKit.Title(t, "History");
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);
        panel.Children.Add(new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
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
            _list.Children.Add(UiKit.Label(_theme, "No transcripts yet — hold your hotkey and say something.", muted: true));
            return;
        }

        foreach (var day in days)
        {
            var header = UiKit.Label(_theme, day.Date.ToString("dddd, MMMM d yyyy"), muted: true);
            header.Margin = new Thickness(0, 8, 0, 6);
            header.FontWeight = FontWeights.SemiBold;
            _list.Children.Add(header);

            foreach (var entry in Enumerable.Reverse(day.Entries))
                _list.Children.Add(EntryCard(entry));
        }
    }

    private UIElement EntryCard(TranscriptEntry entry)
    {
        var text = UiKit.Label(_theme, entry.Text);
        var meta = UiKit.Label(_theme,
            $"{entry.Timestamp:h:mm tt}  ·  {entry.AudioSeconds:F0}s audio  ·  {entry.TranscribeSeconds:F1}s on {entry.Runtime}",
            muted: true);
        meta.Margin = new Thickness(0, 6, 0, 8);

        var copy = UiKit.ActionButton(_theme, "Copy");
        copy.Click += (_, _) => Clipboard.SetText(entry.Text);

        var edit = UiKit.ActionButton(_theme, "Edit & learn");
        edit.Click += (_, _) =>
        {
            var dialog = new FixTranscriptWindow(entry.Text) { Owner = Window.GetWindow(this) };
            if (dialog.ShowDialog() == true)
            {
                var learned = _controller.CorrectEntry(entry, dialog.EditedText);
                MessageBox.Show(learned > 0
                    ? $"Learned {learned} correction{(learned == 1 ? "" : "s")}."
                    : "Transcript updated.", "Bolo");
                Refresh();
            }
        };

        var stack = new StackPanel();
        stack.Children.Add(text);
        stack.Children.Add(meta);
        stack.Children.Add(UiKit.Row(copy, edit));
        return UiKit.Card(_theme, stack);
    }
}
