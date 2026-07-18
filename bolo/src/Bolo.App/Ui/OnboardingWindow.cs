using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Storage;
using NAudio.Wave;

namespace Bolo.App.Ui;

/// <summary>
/// First-run setup: mic level check + seed the personal dictionary. Honest about what
/// it does — this seeds vocabulary and checks audio, it does not "learn your accent."
/// </summary>
public sealed class OnboardingWindow : Window
{
    private readonly Theme _t;
    private readonly TextBlock _micResult;
    private readonly TextBox _terms;
    private Bolo.Core.Storage.AppSettings _settings = null!;

    public OnboardingWindow(AppSettings settings, DictationController controller)
    {
        _t = Theme.FromName(settings.Theme);
        ModernStyles.Apply(this, _t);
        ModernStyles.ApplyTitleBar(this, dark: settings.Theme != "Light");
        Title = "Welcome to Bolo";
        Width = 560;
        Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = _t.WindowBg;

        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(UiKit.Title(_t, "Welcome to Bolo"));
        panel.Children.Add(UiKit.Label(_t,
            "A few quick things before you start dictating. Everything stays on this machine — no cloud, ever.",
            muted: true));

        // Step 1 — name (optional).
        var nameHeader = UiKit.Label(_t, "1.  What should Bolo call you?");
        nameHeader.FontWeight = FontWeights.SemiBold;
        nameHeader.Margin = new Thickness(0, 18, 0, 6);
        panel.Children.Add(nameHeader);
        var nameInput = UiKit.Input(_t, settings.UserName, 240);
        nameInput.LostFocus += (_, _) => { settings.UserName = nameInput.Text.Trim(); settings.Save(); };
        panel.Children.Add(nameInput);
        panel.Children.Add(UiKit.Label(_t, "Optional — used for the Home page greeting. Changeable anytime in Settings.", muted: true));

        // Step 2 — pick and check the microphone.
        var micHeader = UiKit.Label(_t, "2.  Microphone");
        micHeader.FontWeight = FontWeights.SemiBold;
        micHeader.Margin = new Thickness(0, 18, 0, 6);
        panel.Children.Add(micHeader);

        var devices = new List<string> { "System default" };
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
            devices.Add(WaveInEvent.GetCapabilities(i).ProductName);
        var deviceCombo = UiKit.Combo(_t, devices,
            devices[Math.Clamp(settings.MicDeviceIndex + 1, 0, devices.Count - 1)]);
        deviceCombo.SelectionChanged += (_, _) =>
        {
            settings.MicDeviceIndex = deviceCombo.SelectedIndex - 1;
            settings.Save();
        };
        deviceCombo.Margin = new Thickness(0, 0, 0, 8);
        panel.Children.Add(deviceCombo);

        _settings = settings;
        var micButton = UiKit.ActionButton(_t, "Test microphone (2s)");
        _micResult = UiKit.Label(_t, "Speak normally while the test runs.", muted: true);
        _micResult.Margin = new Thickness(0, 6, 0, 0);
        micButton.Click += async (_, _) => await RunMicTestAsync(micButton);
        panel.Children.Add(micButton);
        panel.Children.Add(_micResult);

        // Step 3 — seed dictionary.
        var seedHeader = UiKit.Label(_t, "3.  Seed your dictionary");
        seedHeader.FontWeight = FontWeights.SemiBold;
        seedHeader.Margin = new Thickness(0, 18, 0, 6);
        panel.Children.Add(seedHeader);
        panel.Children.Add(UiKit.Label(_t,
            "Names, jargon, and terms Bolo should recognize from day one (comma-separated). You can manage these later on the Dictionary page.",
            muted: true));
        _terms = UiKit.Input(_t);
        _terms.MinHeight = 60;
        _terms.TextWrapping = TextWrapping.Wrap;
        _terms.AcceptsReturn = true;
        _terms.Margin = new Thickness(0, 8, 0, 0);
        panel.Children.Add(_terms);

        var finish = UiKit.ActionButton(_t, "Finish setup", primary: true);
        finish.Margin = new Thickness(0, 20, 0, 0);
        finish.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        finish.Click += (_, _) =>
        {
            var newTerms = _terms.Text
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();
            if (newTerms.Count > 0)
            {
                var general = controller.Vocabulary.Clusters.FirstOrDefault(c => c.Name == "General");
                if (general is null)
                {
                    general = new Bolo.Core.Learning.VocabCluster { Name = "General" };
                    controller.Vocabulary.Clusters.Add(general);
                }
                general.Terms = general.Terms.Union(newTerms).ToList();
                controller.Vocabulary.Save();
                _ = controller.RefreshVocabularyPromptAsync();
            }
            settings.OnboardingDone = true;
            settings.Save();
            Close();
        };
        panel.Children.Add(finish);

        Content = panel;
    }

    private async Task RunMicTestAsync(Button button)
    {
        button.IsEnabled = false;
        _micResult.Text = "Listening…";
        float peak = 0;
        using var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            DeviceNumber = _settings.MicDeviceIndex,
        };
        waveIn.DataAvailable += (_, e) =>
        {
            for (var i = 0; i < e.BytesRecorded; i += 2)
            {
                var sample = Math.Abs(BitConverter.ToInt16(e.Buffer, i) / 32768f);
                if (sample > peak)
                    peak = sample;
            }
        };
        try
        {
            waveIn.StartRecording();
            await Task.Delay(2000);
            waveIn.StopRecording();
            _micResult.Text = peak switch
            {
                < 0.02f => "Very quiet — check that the right microphone is set as Windows default.",
                < 0.15f => $"A bit quiet (peak {peak:P0}) — usable, but consider raising mic volume.",
                > 0.95f => $"Clipping (peak {peak:P0}) — consider lowering mic volume.",
                _ => $"Sounds good (peak {peak:P0}).",
            };
        }
        catch (Exception ex)
        {
            _micResult.Text = $"Mic test failed: {ex.Message}";
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
