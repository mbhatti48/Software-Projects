using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bolo.Core.Learning;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

public sealed class InsightsPage : UserControl
{
    public InsightsPage(Theme t, DictationController controller, AppSettings settings)
    {
        var report = Insights.Compute(controller.History, controller.Corrections);

        var panel = new StackPanel();
        panel.Children.Add(UiKit.PageHeader(t, "Insights",
            "Everything here is computed from your own history on this machine. Nothing is sent anywhere."));

        if (report.TotalTranscripts == 0)
        {
            panel.Children.Add(UiKit.Card(t, UiKit.Empty(t,
                $"No data yet. Insights appear after your first dictation: hold {settings.Hotkey} and say something.")));
            Content = panel;
            return;
        }

        var row1 = new UniformGrid { Rows = 1, Columns = 4 };
        row1.Children.Add(UiKit.StatTile(t, report.TotalWords.ToString("N0"), "words dictated"));
        row1.Children.Add(UiKit.StatTile(t, report.TotalTranscripts.ToString("N0"), "transcripts"));
        row1.Children.Add(UiKit.StatTile(t, report.AverageWpm.ToString("F0"), "average wpm"));
        row1.Children.Add(UiKit.StatTile(t, FormatMinutes(report.TimeSavedMinutes), "saved vs typing", "against a 40 wpm typist"));
        panel.Children.Add(row1);

        var row2 = new UniformGrid { Rows = 1, Columns = 4 };
        row2.Children.Add(UiKit.StatTile(t, report.FixesApplied.ToString("N0"), "fixes made by Bolo"));
        row2.Children.Add(UiKit.StatTile(t, Quoted(report.MostUsedWord), "most used word"));
        row2.Children.Add(UiKit.StatTile(t, Quoted(report.MostCorrectedWord), "most corrected"));
        row2.Children.Add(UiKit.StatTile(t, $"{report.DayStreak} / {report.LongestStreak}", "streak / longest"));
        panel.Children.Add(row2);

        var phraseStack = new StackPanel();
        phraseStack.Children.Add(UiKit.Section(t, "Catchphrase"));
        phraseStack.Children.Add(new TextBlock
        {
            Text = $"“{report.Catchphrase}”",
            FontFamily = Theme.TitleFont,
            FontSize = 18,
            Foreground = t.Text,
            TextWrapping = TextWrapping.Wrap,
        });

        var peakStack = new StackPanel();
        peakStack.Children.Add(UiKit.Section(t, "Peak dictation time"));
        peakStack.Children.Add(new TextBlock
        {
            Text = report.PeakTime,
            FontSize = 18,
            FontWeight = FontWeights.ExtraBold,
            Foreground = t.Text,
        });

        var pairRow = new UniformGrid { Rows = 1, Columns = 2 };
        var phraseCard = UiKit.Card(t, phraseStack);
        phraseCard.Margin = new Thickness(0, 0, 12, 12);
        pairRow.Children.Add(phraseCard);
        pairRow.Children.Add(UiKit.Card(t, peakStack));
        panel.Children.Add(pairRow);

        panel.Children.Add(BuildHeatmap(t, report.Days));
        panel.Children.Add(BuildAppBreakdown(t, report));
        panel.Children.Add(UiKit.BarChart(t, report.Days, "Words per day", "No days with dictation yet."));

        var correctionsStack = new StackPanel();
        correctionsStack.Children.Add(UiKit.Section(t, "Most-applied corrections"));
        if (report.TopCorrections.Count == 0)
        {
            correctionsStack.Children.Add(UiKit.Empty(t,
                "None yet. When Bolo mishears a word, fix it with Edit & learn in History and the fix shows up here as it gets reused."));
        }
        else
        {
            foreach (var (wrong, right, times) in report.TopCorrections)
            {
                var line = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
                var count = UiKit.Label(t, $"{times}×", muted: true);
                count.FontSize = 12;
                DockPanel.SetDock(count, Dock.Right);
                line.Children.Add(count);
                var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 14, Foreground = t.TextBody };
                text.Inlines.Add(new System.Windows.Documents.Run(wrong) { Foreground = t.TextMuted, TextDecorations = TextDecorations.Strikethrough });
                text.Inlines.Add(new System.Windows.Documents.Run("   →   ") { Foreground = t.TextFaint });
                text.Inlines.Add(new System.Windows.Documents.Run(right) { Foreground = t.Text, FontWeight = FontWeights.Bold });
                line.Children.Add(text);
                correctionsStack.Children.Add(line);
            }
        }
        panel.Children.Add(UiKit.Card(t, correctionsStack));

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 8, 24) };
    }

    /// <summary>GitHub-style activity calendar: last 16 weeks, one cell per day.</summary>
    private static UIElement BuildHeatmap(Theme t, IReadOnlyList<DayStats> days)
    {
        var byDate = days.ToDictionary(d => d.Date, d => d.Words);
        var maxWords = Math.Max(1, days.Count == 0 ? 1 : days.Max(d => d.Words));

        // Grid starts on the Sunday 15 weeks back, ends today.
        var end = DateTime.Today;
        var start = end.AddDays(-(7 * 15) - (int)end.DayOfWeek);

        var weeks = new StackPanel { Orientation = Orientation.Horizontal };
        for (var weekStart = start; weekStart <= end; weekStart = weekStart.AddDays(7))
        {
            var column = new StackPanel { Margin = new Thickness(0, 0, 3, 0) };
            for (var i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                var words = date > end ? -1 : byDate.GetValueOrDefault(date);
                column.Children.Add(new Rectangle
                {
                    Width = 13, Height = 13,
                    RadiusX = 3, RadiusY = 3,
                    Margin = new Thickness(0, 0, 0, 3),
                    Fill = words < 0 ? Brushes.Transparent : CellBrush(t, words, maxWords),
                    ToolTip = words < 0 ? null : $"{date:ddd, MMM d}: {Math.Max(0, words):N0} words",
                });
            }
            weeks.Children.Add(column);
        }

        var stack = new StackPanel();
        stack.Children.Add(UiKit.Section(t, "Activity, last 16 weeks"));
        stack.Children.Add(weeks);
        return UiKit.Card(t, stack);
    }

    private static Brush CellBrush(Theme t, int words, int maxWords)
    {
        if (words == 0)
            return t.RaiseBg;
        var intensity = Math.Clamp((double)words / maxWords, 0.15, 1.0);
        var alpha = (byte)(60 + intensity * 195);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, t.AccentGlow.R, t.AccentGlow.G, t.AccentGlow.B));
        brush.Freeze();
        return brush;
    }

    private static UIElement BuildAppBreakdown(Theme t, InsightsReport report)
    {
        var stack = new StackPanel();
        stack.Children.Add(UiKit.Section(t, "Where you dictate"));
        if (report.AppBreakdown.Count == 0)
            stack.Children.Add(UiKit.Empty(t, "Bolo notes which app each dictation went into; the split appears here."));
        foreach (var app in report.AppBreakdown)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var name = UiKit.Label(t, app.App);
            name.Width = 150;
            DockPanel.SetDock(name, Dock.Left);
            var pct = UiKit.Label(t, $"{app.Share:P0}  ·  {app.Words:N0} words", muted: true);
            pct.FontSize = 12;
            pct.Margin = new Thickness(12, 0, 0, 0);
            DockPanel.SetDock(pct, Dock.Right);
            var track = new Border { Background = t.RaiseBg, CornerRadius = new CornerRadius(3), Height = 8, VerticalAlignment = VerticalAlignment.Center };
            var bar = new Border
            {
                Background = t.AccentGradient,
                CornerRadius = new CornerRadius(3),
                Height = 8,
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = Math.Max(6, 260 * app.Share),
            };
            var bars = new Grid { Width = 260, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            bars.Children.Add(track);
            bars.Children.Add(bar);
            row.Children.Add(name);
            row.Children.Add(pct);
            row.Children.Add(bars);
            stack.Children.Add(row);
        }
        return UiKit.Card(t, stack);
    }

    private static string Quoted(string word) => word == "—" ? "—" : $"“{word}”";

    private static string FormatMinutes(double minutes) =>
        minutes >= 90 ? $"{minutes / 60:F1} h" : $"{minutes:F0} min";
}
