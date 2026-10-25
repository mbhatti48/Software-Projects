using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Learning;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

/// <summary>
/// Home is a stats dashboard (per the Phase 4 interview) with dictation one click away:
/// greeting, four tiles with a line of context each, the 14-day chart, and the three
/// most recent transcripts (Flow keeps history on its home too).
/// </summary>
public sealed class HomePage : UserControl
{
    public HomePage(Theme t, DictationController controller, AppSettings settings, Action<string> navigate)
    {
        var report = Insights.Compute(controller.History, controller.Corrections);
        var today = report.Days.FirstOrDefault(d => d.Date == DateTime.Today);
        var activeDays = report.Days.Where(d => d.Words > 0).ToList();
        var averagePerActiveDay = activeDays.Count > 0 ? activeDays.Average(d => d.Words) : 0;

        var greeting = DateTime.Now.Hour switch
        {
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _ => "Good evening",
        };
        var subtitle = today is { Words: > 0 }
            ? $"{DateTime.Today:dddd d MMMM} · {today.Words:N0} words so far today."
            : $"{DateTime.Today:dddd d MMMM} · Nothing dictated yet today. Hold {settings.Hotkey} in any text field to start.";

        var record = UiKit.PillButton(t, "Start dictation", UiKit.GlyphMic);
        record.Click += (_, _) =>
        {
            if (controller.State == DictationState.Idle)
                controller.StartDictation();
            else if (controller.State == DictationState.Recording)
                controller.StopDictation();
        };
        void Paint(DictationState state) =>
            UiKit.SetPillState(t, record, state == DictationState.Recording,
                state == DictationState.Recording ? "Stop dictation" : "Start dictation");
        Paint(controller.State);
        // Pages are recreated on every navigation: unhook on unload or handlers
        // accumulate on the controller for the app's lifetime.
        Action<DictationState> onStateChanged = state => Dispatcher.Invoke(() => Paint(state));
        controller.StateChanged += onStateChanged;
        Unloaded += (_, _) => controller.StateChanged -= onStateChanged;

        var panel = new StackPanel();
        panel.Children.Add(UiKit.PageHeader(t,
            string.IsNullOrWhiteSpace(settings.UserName) ? greeting : $"{greeting}, {settings.UserName}",
            subtitle, record));

        // ---- tiles
        var todayWords = today?.Words ?? 0;
        string? todayDetail = null;
        var todayBrush = t.TextFaint;
        if (todayWords > 0 && averagePerActiveDay > 0 && activeDays.Count > 1)
        {
            var delta = todayWords / averagePerActiveDay - 1;
            todayDetail = delta >= 0 ? $"+{delta:P0} vs your usual day" : $"{delta:P0} vs your usual day";
            if (delta >= 0) todayBrush = t.Ok;
        }
        var firstDay = report.Days.FirstOrDefault()?.Date;
        var statsRow = new UniformGrid { Rows = 1, Columns = 4 };
        statsRow.Children.Add(UiKit.StatTile(t, todayWords.ToString("N0"), "words today", todayDetail, todayBrush));
        statsRow.Children.Add(UiKit.StatTile(t, report.TotalWords.ToString("N0"), "words all time",
            firstDay is null ? null : $"since {firstDay:MMMM yyyy}"));
        statsRow.Children.Add(UiKit.StatTile(t, report.AverageWpm.ToString("F0"), "average wpm",
            report.AverageWpm > 0 ? $"{report.AverageWpm / 40:F1}× a 40 wpm typist" : null));
        statsRow.Children.Add(UiKit.StatTile(t, report.DayStreak.ToString(), "day streak",
            report.LongestStreak > 0 ? $"longest {report.LongestStreak}" : null));
        panel.Children.Add(statsRow);

        // ---- chart + recent
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.35, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var chart = UiKit.BarChart(t, report.Days, "Words per day",
            $"The chart fills in as you dictate. Hold {settings.Hotkey} and say something.");
        Grid.SetColumn(chart, 0);
        grid.Children.Add(chart);
        var recent = RecentCard(t, controller, settings, navigate);
        Grid.SetColumn(recent, 2);
        grid.Children.Add(recent);
        panel.Children.Add(grid);

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 0, 24) };
    }

    private static UIElement RecentCard(Theme t, DictationController controller, AppSettings settings, Action<string> navigate)
    {
        var stack = new StackPanel();
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
        var link = new TextBlock
        {
            Text = "History ›",
            FontSize = 12,
            Foreground = t.TextFaint,
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
        };
        link.MouseEnter += (_, _) => link.Foreground = t.AccentInk;
        link.MouseLeave += (_, _) => link.Foreground = t.TextFaint;
        link.MouseLeftButtonUp += (_, _) => navigate("History");
        DockPanel.SetDock(link, Dock.Right);
        head.Children.Add(link);
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
            stack.Children.Add(UiKit.Empty(t, $"Nothing here yet. Hold {settings.Hotkey} and say something; it shows up here."));
            return UiKit.Card(t, stack);
        }
        foreach (var entry in entries)
        {
            var text = UiKit.Label(t, entry.Text);
            text.FontSize = 13;
            text.LineHeight = 18;
            text.MaxHeight = 36;
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            var meta = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var time = new TextBlock { Text = entry.Timestamp.ToString("h:mm tt"), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = t.TextMuted };
            meta.Children.Add(time);
            if (!string.IsNullOrEmpty(entry.TargetApp))
                meta.Children.Add(new TextBlock { Text = entry.TargetApp, FontSize = 11, Foreground = t.TextFaint, Margin = new Thickness(10, 0, 0, 0) });
            meta.Children.Add(new TextBlock { Text = $"{entry.AudioSeconds:F1} s", FontSize = 11, Foreground = t.TextFaint, Margin = new Thickness(10, 0, 0, 0) });
            var inner = new StackPanel();
            inner.Children.Add(text);
            inner.Children.Add(meta);
            stack.Children.Add(UiKit.Raised(t, inner));
        }
        return UiKit.Card(t, stack);
    }
}
