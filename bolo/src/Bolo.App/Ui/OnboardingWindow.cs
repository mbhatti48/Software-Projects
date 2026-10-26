using System.Windows;
using System.Windows.Controls;
using Bolo.Core.Storage;
using NAudio.Wave;

namespace Bolo.App.Ui;

/// <summary>
/// First-run setup: name, mic level check, seed the personal dictionary. Honest about
/// what it does: it seeds vocabulary and checks audio, it does not "learn your accent."
/// </summary>
public sealed class OnboardingWindow : Window
{
    private readonly Theme _t;
    private readonly TextBlock _micResult;
    private readonly TextBox _terms;
    private readonly AppSettings _settings;

    public OnboardingWindow(AppSettings settings, DictationController controller)
    {
        _settings = settings;
        _t = Theme.FromName(settings.Theme, settings.Accent);
        ModernStyles.Apply(this, _t);
        ModernStyles.ApplyTitleBar(this, _t);
        Title = "Welcome to Bolo";
        Width = 640;
        Height = 760;
        MinWidth = 520;
        MinHeight = 560;
        FontFamily = Theme.BodyFont;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = _t.WindowBg;

        var panel = new StackPanel();

        // Hero: the mark, the title, one sentence that states the privacy promise.
        var hero = new DockPanel { Margin = new Thickness(0, 0, 0, 24), LastChildFill = true };
        var logo = MainWindow.LogoImage(56);
        logo.VerticalAlignment = VerticalAlignment.Top;
        DockPanel.SetDock(logo, Dock.Left);
        hero.Children.Add(logo);
        var heroText = new StackPanel { Margin = new Thickness(16, 0, 0, 0) };
        heroText.Children.Add(UiKit.Title(_t, "Welcome to Bolo"));
        var promise = UiKit.Subtitle(_t, "Three quick things before you dictate. Everything stays on this machine: no cloud, no account, ever.");
        promise.Margin = new Thickness(0, 4, 0, 0);
        heroText.Children.Add(promise);
        hero.Children.Add(heroText);
        panel.Children.Add(hero);

        // Step 1: name (optional).
        var nameStack = new StackPanel();
        nameStack.Children.Add(UiKit.Section(_t, "01  What should Bolo call you?"));
        var nameInput = UiKit.Input(_t, settings.UserName, 260);
        nameInput.HorizontalAlignment = HorizontalAlignment.Left;
        nameInput.LostFocus += (_, _) => { settings.UserName = nameInput.Text.Trim(); settings.Save(); };
        nameStack.Children.Add(nameInput);
        var nameNote = UiKit.Label(_t, "Optional. Used for the Home greeting; change it any time in Settings.", muted: true);
        nameNote.FontSize = 13;
        nameNote.Margin = new Thickness(0, 8, 0, 0);
        nameStack.Children.Add(nameNote);
        panel.Children.Add(UiKit.Card(_t, nameStack));

        // Step 2: pick and check the microphone.
        var micStack = new StackPanel();
        micStack.Children.Add(UiKit.Section(_t, "02  Microphone"));
        var devices = new List<string> { "System default" };
        devices.AddRange(MicInfo.DeviceNames());
        var deviceCombo = UiKit.Combo(_t, devices,
            devices[Math.Clamp(MicInfo.Resolve(settings) + 1, 0, devices.Count - 1)]);
        deviceCombo.MinWidth = 320;
        deviceCombo.SelectionChanged += (_, _) => MicInfo.Choose(settings, devices, deviceCombo.SelectedIndex);
        micStack.Children.Add(deviceCombo);

        var micButton = UiKit.ActionButton(_t, "Test the microphone for 2 seconds");
        micButton.HorizontalAlignment = HorizontalAlignment.Left;
        micButton.Margin = new Thickness(0, 10, 0, 0);
        _micResult = UiKit.Label(_t, "Speak normally while the test runs.", muted: true);
        _micResult.FontSize = 13;
        _micResult.Margin = new Thickness(0, 8, 0, 0);
        micButton.Click += async (_, _) => await RunMicTestAsync(micButton);
        micStack.Children.Add(micButton);
        micStack.Children.Add(_micResult);
        panel.Children.Add(UiKit.Card(_t, micStack));

        // Step 3: seed the dictionary.
        var seedStack = new StackPanel();
        seedStack.Children.Add(UiKit.Section(_t, "03  Seed your dictionary"));
        var seedNote = UiKit.Label(_t,
            "Names, jargon and terms Bolo should recognise from day one, separated by commas. You can manage these later on the Dictionary page.",
            muted: true);
        seedNote.FontSize = 13;
        seedStack.Children.Add(seedNote);
        _terms = UiKit.Input(_t);
        _terms.MinHeight = 72;
        _terms.TextWrapping = TextWrapping.Wrap;
        _terms.AcceptsReturn = true;
        _terms.VerticalContentAlignment = VerticalAlignment.Top;
        _terms.Margin = new Thickness(0, 10, 0, 0);
        seedStack.Children.Add(_terms);
        panel.Children.Add(UiKit.Card(_t, seedStack));

        // Step 4: prove the hotkey. The box is an ordinary text box; the global hotkey types
        // into it exactly as it will into any app, so the user leaves knowing it works.
        var tryStack = new StackPanel();
        tryStack.Children.Add(UiKit.Section(_t, "04  Try it"));
        var practice = UiKit.Input(_t);
        practice.MinHeight = 64;
        practice.TextWrapping = TextWrapping.Wrap;
        practice.AcceptsReturn = true;
        practice.VerticalContentAlignment = VerticalAlignment.Top;
        var tryStatus = UiKit.Label(_t, "", muted: true);
        tryStatus.FontSize = 13;
        tryStatus.Margin = new Thickness(0, 8, 0, 0);
        void ShowTryState(DictationState state)
        {
            tryStatus.Text = state switch
            {
                DictationState.Loading => "The speech model is still loading. This box wakes up in a moment.",
                DictationState.Recording => "Listening. Release the keys when you are done.",
                DictationState.Transcribing => "Finalizing…",
                _ when practice.Text.Trim().Length > 0 => "That's it. Bolo typed what you said, and it works the same in any app.",
                _ when controller.LoadedRuntime is "none" or "unknown" => "No speech model is loaded yet, so dictation can't start. Settings explains where the model goes.",
                _ => $"Click in the box, hold {settings.Hotkey}, say something, then release. Esc throws a take away.",
            };
        }
        ShowTryState(controller.State);
        practice.TextChanged += (_, _) => ShowTryState(controller.State);
        Action<DictationState> onTryState = state => Dispatcher.Invoke(() => ShowTryState(state));
        controller.StateChanged += onTryState;
        Closed += (_, _) => controller.StateChanged -= onTryState;
        tryStack.Children.Add(practice);
        tryStack.Children.Add(tryStatus);
        panel.Children.Add(UiKit.Card(_t, tryStack));

        // Bottom bar, always visible: what happens next on the left, Finish on the right.
        var finish = UiKit.PillButton(_t, "Finish setup", "");
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
        var next = UiKit.Label(_t, $"Then hold {settings.Hotkey} in any text field, speak, and release. The words land where your cursor is.", muted: true);
        next.FontSize = 13;
        next.Margin = new Thickness(0, 0, 16, 0);
        var bar = new DockPanel { Margin = new Thickness(32, 12, 32, 24), LastChildFill = true };
        DockPanel.SetDock(finish, Dock.Right);
        bar.Children.Add(finish);
        bar.Children.Add(next);

        var root = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(bar, Dock.Bottom);
        root.Children.Add(bar);
        root.Children.Add(new ScrollViewer
        {
            Content = panel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(32, 30, 32, 0),
        });
        Content = root;
    }

    private async Task RunMicTestAsync(Button button)
    {
        button.IsEnabled = false;
        _micResult.Text = "Listening…";
        float peak = 0;
        using var waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(16000, 16, 1),
            DeviceNumber = MicInfo.Resolve(_settings),
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
                < 0.02f => "Very quiet. Check that the right microphone is set as the Windows default.",
                < 0.15f => $"A bit quiet (peak {peak:P0}). Usable, but consider raising the mic volume.",
                > 0.95f => $"Clipping (peak {peak:P0}). Consider lowering the mic volume.",
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
