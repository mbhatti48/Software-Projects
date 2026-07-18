using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Bolo.Core.Learning;

namespace Bolo.App.Ui;

public sealed class InsightsPage : UserControl
{
    public InsightsPage(Theme t, DictationController controller)
    {
        var report = Insights.Compute(controller.History, controller.Corrections);

        var panel = new StackPanel();
        panel.Children.Add(UiKit.Title(t, "Insights"));

        var row1 = new UniformGrid { Rows = 1, Columns = 4 };
        row1.Children.Add(UiKit.StatTile(t, report.TotalWords.ToString("N0"), "words dictated"));
        row1.Children.Add(UiKit.StatTile(t, report.TotalTranscripts.ToString("N0"), "transcripts"));
        row1.Children.Add(UiKit.StatTile(t, report.AverageWpm.ToString("F0"), "avg wpm"));
        row1.Children.Add(UiKit.StatTile(t, FormatMinutes(report.TimeSavedMinutes), "saved vs typing*"));
        panel.Children.Add(row1);

        var row2 = new UniformGrid { Rows = 1, Columns = 4 };
        row2.Children.Add(UiKit.StatTile(t, report.FixesApplied.ToString("N0"), "fixes made by Bolo"));
        row2.Children.Add(UiKit.StatTile(t, $"“{report.MostUsedWord}”", "most used word"));
        row2.Children.Add(UiKit.StatTile(t, $"“{report.MostCorrectedWord}”", "most corrected"));
        row2.Children.Add(UiKit.StatTile(t, $"{report.DayStreak} / {report.LongestStreak}", "streak / longest"));
        panel.Children.Add(row2);

        var phraseStack = new StackPanel();
        phraseStack.Children.Add(UiKit.Label(t, "Catchphrase", muted: true));
        var phrase = new TextBlock
        {
            Text = $"“{report.Catchphrase}”",
            FontSize = 19,
            FontStyle = FontStyles.Italic,
            Foreground = t.Text,
            Margin = new Thickness(0, 6, 0, 0),
        };
        phraseStack.Children.Add(phrase);

        var peakStack = new StackPanel();
        peakStack.Children.Add(UiKit.Label(t, "Peak dictation time", muted: true));
        var peak = new TextBlock
        {
            Text = report.PeakTime,
            FontSize = 19,
            FontWeight = FontWeights.SemiBold,
            Foreground = t.Text,
            Margin = new Thickness(0, 6, 0, 0),
        };
        peakStack.Children.Add(peak);

        var pairRow = new UniformGrid { Rows = 1, Columns = 2 };
        var phraseCard = UiKit.Card(t, phraseStack);
        phraseCard.Margin = new Thickness(0, 0, 12, 12);
        pairRow.Children.Add(phraseCard);
        pairRow.Children.Add(UiKit.Card(t, peakStack));
        panel.Children.Add(pairRow);

        panel.Children.Add(BuildHeatmap(t, report.Days));
        panel.Children.Add(BuildAppBreakdown(t, report));
        panel.Children.Add(UiKit.BarChart(t, report.Days, "Words per day (last 14 days)"));

        var correctionsStack = new StackPanel();
        correctionsStack.Children.Add(UiKit.Label(t, "Most-applied corrections", muted: true));
        if (report.TopCorrections.Count == 0)
        {
            var none = UiKit.Label(t, "None yet — use \"Edit & learn\" in History when Bolo mishears something.");
            none.Margin = new Thickness(0, 8, 0, 0);
            correctionsStack.Children.Add(none);
        }
        else
        {
            foreach (var (wrong, right, times) in report.TopCorrections)
            {
                var line = UiKit.Label(t, $"\"{wrong}\" → \"{right}\"   ({times}×)");
                line.Margin = new Thickness(0, 7, 0, 0);
                correctionsStack.Children.Add(line);
            }
        }
        panel.Children.Add(UiKit.Card(t, correctionsStack));

        var footnote = UiKit.Label(t, "* assumes a 40 wpm typing speed", muted: true);
        footnote.FontSize = 11;
        panel.Children.Add(footnote);

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    /// <summary>GitHub-style activity calendar: last 16 weeks, one cell per day.</summary>
    private static UIElement BuildHeatmap(Theme t, IReadOnlyList<DayStats> days)
    {
        var byDate = days.ToDictionary(d => d.Date, d => d.Words);
        var maxWords = Math.Max(1, days.Count == 0 ? 1 : days.Max(d => d.Words));

        // Grid starts on the Sunday 15 weeks back, ends today.
        var end = DateTime.Today;
        var start = end.AddDays(-(7 * 15) - (int)end.DayOfWeek);

        var weeks = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        for (var weekStart = start; weekStart <= end; weekStart = weekStart.AddDays(7))
        {
            var column = new StackPanel { Margin = new Thickness(0, 0, 3, 0) };
            for (var i = 0; i < 7; i++)
            {
                var date = weekStart.AddDays(i);
                var words = date > end ? -1 : byDate.GetValueOrDefault(date);
                var cell = new Rectangle
                {
                    Width = 13, Height = 13,
                    RadiusX = 3, RadiusY = 3,
                    Margin = new Thickness(0, 0, 0, 3),
                    Fill = words < 0 ? Brushes.Transparent : CellBrush(t, words, maxWords),
                    ToolTip = words < 0 ? null : $"{date:ddd, MMM d}: {Math.Max(0, words):N0} words",
                };
                column.Children.Add(cell);
            }
            weeks.Children.Add(column);
        }

        var stack = new StackPanel();
        stack.Children.Add(UiKit.Label(t, "Activity (last 16 weeks)", muted: true));
        stack.Children.Add(weeks);
        return UiKit.Card(t, stack);
    }

    private static Brush CellBrush(Theme t, int words, int maxWords)
    {
        if (words == 0)
            return t.InputBg;
        var intensity = Math.Clamp((double)words / maxWords, 0.15, 1.0);
        var alpha = (byte)(60 + intensity * 195);
        var brush = new SolidColorBrush(Color.FromArgb(alpha, t.AccentGlow.R, t.AccentGlow.G, t.AccentGlow.B));
        brush.Freeze();
        return brush;
    }

    private static UIElement BuildAppBreakdown(Theme t, InsightsReport report)
    {
        var stack = new StackPanel();
        stack.Children.Add(UiKit.Label(t, "Where you dictate", muted: true));
        if (report.AppBreakdown.Count == 0)
        {
            var none = UiKit.Label(t, "No data yet.");
            none.Margin = new Thickness(0, 8, 0, 0);
            stack.Children.Add(none);
        }
        foreach (var app in report.AppBreakdown)
        {
            var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            var name = UiKit.Label(t, app.App);
            name.Width = 140;
            DockPanel.SetDock(name, Dock.Left);
            var pct = UiKit.Label(t, $"{app.Share:P0}  ·  {app.Words:N0} words", muted: true);
            pct.Margin = new Thickness(10, 0, 0, 0);
            DockPanel.SetDock(pct, Dock.Right);
            var bar = new Border
            {
                Background = t.AccentGradient,
                CornerRadius = new CornerRadius(4),
                Height = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Width = Math.Max(6, 260 * app.Share),
                VerticalAlignment = VerticalAlignment.Center,
            };
            row.Children.Add(name);
            row.Children.Add(pct);
            row.Children.Add(bar);
            stack.Children.Add(row);
        }
        return UiKit.Card(t, stack);
    }

    private static string FormatMinutes(double minutes) =>
        minutes >= 90 ? $"{minutes / 60:F1} h" : $"{minutes:F0} min";
}
