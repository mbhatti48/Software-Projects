using System.IO;
using System.Threading;
using System.Windows;
using Bolo.Core.Storage;

namespace Bolo.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private static bool _ownsSingleInstanceMutex;
    private const string ActivationSignalName = "BoloActivateSignal";
    private static EventWaitHandle? _activationSignal;

    private AppSettings _settings = null!;
    private DictationController _controller = null!;
    private TrayIcon _tray = null!;
    private OverlayWindow _overlay = null!;
    private HotkeyManager? _hotkey;
    private EscapeCancel? _esc;
    private Ui.MainWindow? _mainWindow;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Verification tool: render every screen to PNG and exit (no hotkey, mic, model, tray or mutex).
        if (e.Args is ["--screenshots", var shotDir])
        {
            await Ui.Screenshots.RunAsync(shotDir);
            Shutdown();
            return;
        }

        // Startup breadcrumbs: if a launch ever wedges into a fake first-run again, these
        // three lines pin down exactly where it diverged — process, data root, whether we
        // won the single-instance mutex, and what settings load actually returned.
        BoloLog.Write($"OnStartup entered (pid {Environment.ProcessId}, data root: {BoloPaths.Root})");

        _singleInstanceMutex = new Mutex(true, "BoloDictationApp", out var isNewInstance);
        _ownsSingleInstanceMutex = isNewInstance;
        BoloLog.Write($"single-instance check: isNewInstance={isNewInstance}");
        if (!isNewInstance)
        {
            // Launching again = "open Bolo": signal the running instance to show its
            // window, then exit quietly.
            try
            {
                EventWaitHandle.OpenExisting(ActivationSignalName).Set();
            }
            catch (WaitHandleCannotBeOpenedException) { /* races app shutdown — nothing to do */ }
            Shutdown();
            return;
        }

        _activationSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationSignalName);
        var activationWatcher = new Thread(() =>
        {
            while (_activationSignal.WaitOne())
                Dispatcher.Invoke(ShowMainWindow);
        })
        { IsBackground = true };
        activationWatcher.Start();

        BoloPaths.EnsureDirectories();
        _settings = AppSettings.Load();
        BoloLog.Write($"settings loaded: onboardingDone={_settings.OnboardingDone}, " +
            $"settingsFileExists={File.Exists(BoloPaths.SettingsFile)}, loadFailed={_settings.LoadFailed}");
        if (string.IsNullOrEmpty(_settings.ModelPath) || !File.Exists(_settings.ModelPath))
        {
            // Look for a model next to the exe (distributed layout) first, then the
            // development location. Prefer turbo when several are present.
            string[] modelDirs =
            [
                Path.Combine(AppContext.BaseDirectory, "models"),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\models")),
            ];
            var found = modelDirs
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "ggml-*.bin"))
                .OrderByDescending(f => f.Contains("turbo", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            if (found is not null)
            {
                _settings.ModelPath = found;
                _settings.Save();
            }
        }

        _controller = new DictationController(_settings);
        _tray = NewTray();
        _overlay = new OverlayWindow(_settings, () => _controller.InputLevel);
        _esc = new EscapeCancel();
        _esc.Pressed += () => _controller.CancelDictation();

        BoloLog.Write($"--- Bolo starting (hotkey: {_settings.Hotkey}, model: {_settings.ModelPath}) ---");

        _controller.StateChanged += state => Dispatcher.Invoke(() =>
        {
            BoloLog.Write($"state -> {state}");
            // Esc is claimed only while recording (EscapeCancel).
            if (state == DictationState.Recording) _esc?.Arm(); else _esc?.Disarm();
            _tray.SetState(state, _controller.LoadedRuntime);
            switch (state)
            {
                case DictationState.Recording:
                    _overlay.ShowRecording();
                    break;
                case DictationState.Transcribing:
                    _overlay.ShowFinalizing();
                    break;
                default:
                    _overlay.HideOverlay();
                    break;
            }
        });
        _controller.PartialTranscript += text => Dispatcher.Invoke(() =>
            _overlay.SetGhostText(text));
        _controller.TranscriptReady += text => Dispatcher.Invoke(() =>
        {
            BoloLog.Write($"transcript ready ({text.Length} chars), injecting via {_settings.InjectionMode}");
            TextInjector.Inject(text, _settings.InjectionMode);
        });
        _controller.Error += message => Dispatcher.Invoke(() =>
        {
            BoloLog.Write($"ERROR: {message}");
            _tray.ShowBalloon("Bolo error", message, System.Windows.Forms.ToolTipIcon.Error);
        });
        _controller.Notice += message => Dispatcher.Invoke(() =>
            _tray.ShowBalloon("Bolo", message));

        try
        {
            ApplyHotkeySettings();
        }
        catch (Exception ex)
        {
            BoloLog.Write($"HOTKEY FAILED: {ex.Message}");
            _tray.ShowBalloon("Bolo hotkey problem", ex.Message, System.Windows.Forms.ToolTipIcon.Warning);
        }

        try
        {
            await _controller.LoadModelAsync();
            BoloLog.Write($"model loaded on {_controller.LoadedRuntime}");
            _tray.ShowBalloon("Bolo ready",
                $"Model loaded on {_controller.LoadedRuntime}. Press {_settings.Hotkey} to dictate.");
        }
        catch (Exception ex)
        {
            BoloLog.Write($"MODEL LOAD FAILED: {ex}");
            _tray.ShowBalloon("Bolo failed to load model", ex.Message, System.Windows.Forms.ToolTipIcon.Error);
        }

        if (!_settings.OnboardingDone)
            new Ui.OnboardingWindow(_settings, _controller).Show();
    }

    /// <summary>
    /// Releases the global hotkey while the Settings capture box has focus — otherwise
    /// Windows intercepts the bound combo before the box can see it.
    /// </summary>
    internal void SuspendHotkey()
    {
        _hotkey?.Dispose();
        _hotkey = null;
        BoloLog.Write("hotkey suspended (capture box focused)");
    }

    /// <summary>(Re)registers the global hotkey from settings — also used by the Settings page.</summary>
    internal void ApplyHotkeySettings()
    {
        _hotkey?.Dispose();
        _hotkey = null;
        _hotkey = new HotkeyManager(_settings.Hotkey);
        _hotkey.ComboPending += () => _controller.PrewarmCapture();
        _hotkey.ComboCancelled += () => _controller.CancelPrewarm();
        _hotkey.Pressed += () =>
        {
            if (_settings.HotkeyMode == HotkeyMode.HoldToTalk)
            {
                // Auto-repeat fires this continuously while held.
                if (_controller.State == DictationState.Idle)
                {
                    BoloLog.Write("hotkey: hold started");
                    _controller.StartDictation();
                    _holdWatcherActive = true;
                    _ = WatchHoldReleaseAsync();
                }
                else if (_controller.State == DictationState.Recording && !_holdWatcherActive)
                {
                    // Recording was started from the window/tray — let the hotkey stop it
                    // rather than ignoring the user (stuck-recording fix).
                    BoloLog.Write("hotkey: stopping UI-started recording");
                    _controller.StopDictation();
                }
            }
            else
            {
                _controller.Toggle();
                // If the press was debounced away, a prewarmed capture (modifier-only
                // combos) would keep recording silently and prepend stale audio to the
                // next dictation — discard it.
                if (_controller.State == DictationState.Idle)
                    _controller.CancelPrewarm();
            }
        };
        BoloLog.Write($"hotkey registered OK ({_settings.Hotkey}, mode: {_settings.HotkeyMode})");
    }

    private bool _holdWatcherActive;

    /// <summary>Rebuilds the overlay after its settings change.</summary>
    internal void RecreateOverlay()
    {
        var old = _overlay;
        _overlay = new OverlayWindow(_settings, () => _controller.InputLevel);
        old.Close();
    }

    private TrayIcon NewTray()
    {
        var theme = Ui.Theme.FromName(_settings.Theme, _settings.Accent);
        var tray = new TrayIcon(theme.IsDark, theme.ColorOf(theme.Accent));
        tray.ToggleRequested += () => _controller.Toggle();
        tray.OpenRequested += ShowMainWindow;
        tray.CopyLastRequested += CopyLastTranscript;
        tray.FixLastRequested += FixLastTranscript;
        tray.SaveLastRequested += SaveLastTranscript;
        tray.ExitRequested += Shutdown;
        tray.HistoryRequested += ShowTrayPopover;
        return tray;
    }

    /// <summary>Theme or accent changed: rebuild the window, the tray mark and the overlay in the new colours.</summary>
    internal void ApplyAppearance(string page)
    {
        var oldTray = _tray;
        _tray = NewTray();
        _tray.SetState(_controller.State, _controller.LoadedRuntime);
        oldTray.Dispose();
        RecreateOverlay();
        ReopenMainWindow(page);
    }

    internal void PreviewOverlay(string sampleText)
    {
        _overlay.ShowRecording();
        _overlay.SetGhostText(sampleText);
    }

    internal void HideOverlayPreview()
    {
        if (_controller.State != DictationState.Recording)
            _overlay.HideOverlay();
    }

    private void ShowTrayPopover()
    {
        var theme = Ui.Theme.FromName(_settings.Theme, _settings.Accent);
        var popover = new Ui.TrayPopover(theme, _controller, ShowMainWindow);
        popover.Show();
        popover.Activate();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
            _mainWindow = new Ui.MainWindow(_settings, _controller, this);
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    /// <summary>Rebuilds the main window (theme change / settings reset) and returns to a page.</summary>
    internal void ReopenMainWindow(string page)
    {
        _mainWindow?.ForceClose();
        _mainWindow = new Ui.MainWindow(_settings, _controller, this);
        _mainWindow.Show();
        _mainWindow.Navigate(page);
        _mainWindow.Activate();
    }

    /// <summary>Hold-to-talk: polls until the hotkey's main key is released, then stops.</summary>
    private async Task WatchHoldReleaseAsync()
    {
        try
        {
            while (_hotkey is not null && _hotkey.IsMainKeyDown)
                await Task.Delay(30);
            BoloLog.Write("hotkey released (hold-to-talk)");
        }
        catch (Exception ex)
        {
            BoloLog.Write($"hold watcher error: {ex.Message}");
        }
        finally
        {
            // Whatever happened, never leave a recording stuck.
            _holdWatcherActive = false;
            _controller.StopDictation();
        }
    }

    private void CopyLastTranscript()
    {
        var last = _controller.LatestTranscript();
        if (last is null)
        {
            _tray.ShowBalloon("Bolo", "No transcripts yet.");
            return;
        }
        Clipboard.SetText(last.Text);
        _tray.ShowBalloon("Bolo", "Last transcript copied to clipboard.");
    }

    private void FixLastTranscript()
    {
        var last = _controller.LatestTranscript();
        if (last is null)
        {
            _tray.ShowBalloon("Bolo", "No transcripts yet.");
            return;
        }
        var dialog = new FixTranscriptWindow(last.Text, _settings);
        if (dialog.ShowDialog() == true)
        {
            var learned = _controller.CorrectLastTranscript(dialog.EditedText);
            BoloLog.Write($"fix dialog: learned {learned} correction pair(s)");
            _tray.ShowBalloon("Bolo", learned > 0
                ? $"Learned {learned} correction{(learned == 1 ? "" : "s")} — they'll apply automatically from now on."
                : "Transcript updated (no correction patterns extracted).");
        }
    }

    private void SaveLastTranscript()
    {
        var last = _controller.LatestTranscript();
        if (last is null)
        {
            _tray.ShowBalloon("Bolo", "No transcripts yet.");
            return;
        }
        var dialog = new SaveFileDialog
        {
            Filter = "Text file (*.txt)|*.txt",
            FileName = $"bolo-transcript-{last.Timestamp:yyyy-MM-dd-HHmm}.txt",
        };
        if (dialog.ShowDialog() == true)
            File.WriteAllText(dialog.FileName, last.Text);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _esc?.Dispose();
        _overlay?.Close();
        _tray?.Dispose();
        _controller?.Dispose();
        // A second instance never owned the mutex; releasing it there throws
        // ApplicationException on the way out (v1.2.0 fix).
        if (_ownsSingleInstanceMutex)
            _singleInstanceMutex?.ReleaseMutex();
        base.OnExit(e);
    }
}
