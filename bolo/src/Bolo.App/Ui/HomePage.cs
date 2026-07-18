using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Learning;

namespace Bolo.App.Ui;

/// <summary>Home is a stats dashboard (per the Phase 4 interview), with dictation one click away.</summary>
public sealed class HomePage : UserControl
{
    public HomePage(Theme t, DictationController controller, string userName = "")
    {
        var report = Insights.Compute(controller.History, controller.Corrections);
        var today = report.Days.FirstOrDefault(d => d.Date == DateTime.Today);

        var greeting = DateTime.Now.Hour switch
        {
            < 12 => "Good morning",
            < 18 => "Good afternoon",
            _ => "Good evening",
        };

        var panel = new StackPanel();

        var header = new DockPanel { Margin = new Thickness(0, 0, 0, 16) };
        var title = UiKit.Title(t, string.IsNullOrWhiteSpace(userName) ? greeting : $"{greeting}, {userName}");
        title.Margin = new Thickness(0);
        title.VerticalAlignment = VerticalAlignment.Center;

        var recordButton = UiKit.ActionButton(t,
            controller.State == DictationState.Recording ? "Stop dictation" : "Start dictation", primary: true);
        recordButton.Click += (_, _) =>
        {
            if (controller.State == DictationState.Idle)
                controller.StartDictation();
            else if (controller.State == DictationState.Recording)
                controller.StopDictation();
        };
        // Pages are recreated on every navigation — unhook on unload or handlers
        // accumulate on the controller for the app's lifetime.
        Action<DictationState> onStateChanged = state => Dispatcher.Invoke(() =>
        {
            recordButton.Content = state == DictationState.Recording ? "Stop dictation" : "Start dictation";
            // The glow breathes while recording.
            if (recordButton.Effect is System.Windows.Media.Effects.DropShadowEffect glow)
            {
                if (state == DictationState.Recording)
                {
                    glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
                        new System.Windows.Media.Animation.DoubleAnimation(0.35, 0.85, TimeSpan.FromMilliseconds(700))
                        {
                            AutoReverse = true,
                            RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever,
                        });
                }
                else
                {
                    glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
                    glow.Opacity = 0.35;
                }
            }
        });
        controller.StateChanged += onStateChanged;
        Unloaded += (_, _) => controller.StateChanged -= onStateChanged;
        DockPanel.SetDock(recordButton, Dock.Right);
        recordButton.VerticalAlignment = VerticalAlignment.Center;

        header.Children.Add(recordButton);
        header.Children.Add(title);
        panel.Children.Add(header);

        var statsRow = new UniformGrid { Rows = 1, Columns = 4 };
        statsRow.Children.Add(UiKit.StatTile(t, today?.Words.ToString("N0") ?? "0", "words today"));
        statsRow.Children.Add(UiKit.StatTile(t, report.TotalWords.ToString("N0"), "words all-time"));
        statsRow.Children.Add(UiKit.StatTile(t, report.AverageWpm.ToString("F0"), "average wpm"));
        statsRow.Children.Add(UiKit.StatTile(t, report.DayStreak.ToString(), "day streak"));
        panel.Children.Add(statsRow);

        panel.Children.Add(UiKit.BarChart(t, report.Days, "Words per day (last 14 days)"));

        var engineLine = UiKit.Label(t,
            $"Engine: {controller.LoadedRuntime}  ·  hold your hotkey to dictate from anywhere", muted: true);
        engineLine.Margin = new Thickness(2, 4, 0, 0);
        panel.Children.Add(engineLine);

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
