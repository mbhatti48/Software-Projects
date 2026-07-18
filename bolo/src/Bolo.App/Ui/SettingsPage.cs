using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

public sealed class SettingsPage : UserControl
{
    private readonly Theme _t;
    private readonly AppSettings _settings;
    private readonly DictationController _controller;
    private readonly App _app;

    public SettingsPage(Theme t, AppSettings settings, DictationController controller, App app)
    {
        _t = t;
        _settings = settings;
        _controller = controller;
        _app = app;

        var panel = new StackPanel();
        panel.Children.Add(UiKit.Title(t, "Settings"));

        panel.Children.Add(Section("Dictation hotkey", BuildHotkeySection()));
        panel.Children.Add(Section("Microphone", BuildMicSection()));
        panel.Children.Add(Section("Text injection", BuildInjectionSection()));
        panel.Children.Add(Section("Model & engine", BuildModelSection()));
        panel.Children.Add(Section("Overlay", BuildOverlaySection()));
        panel.Children.Add(Section("Appearance", BuildAppearanceSection()));
        panel.Children.Add(Section("Reset", BuildResetSection()));

        var version = UiKit.Label(t,
            $"Bolo v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}",
            muted: true);
        version.Margin = new Thickness(0, 10, 0, 0);
        panel.Children.Add(version);

        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    private UIElement Section(string title, UIElement body)
    {
        var header = UiKit.Label(_t, title, muted: true);
        header.FontWeight = FontWeights.SemiBold;
        header.Margin = new Thickness(0, 6, 0, 6);
        var stack = new StackPanel();
        stack.Children.Add(header);
        stack.Children.Add(UiKit.Card(_t, body));
        return stack;
    }

    private UIElement BuildHotkeySection()
    {
        var stack = new StackPanel();

        // Key-capture box. Regular-key combos bind on the keypress; modifier-only
        // combos (Ctrl+Shift, Ctrl+Win) bind by HOLDING them for half a second —
        // no release detection, which is what made Win combos unreliable before.
        var capture = UiKit.Input(_t, _settings.Hotkey, 220);
        capture.IsReadOnly = true;
        capture.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        capture.TextAlignment = TextAlignment.Center;
        capture.FontWeight = FontWeights.SemiBold;
        capture.ToolTip = "Click here, then press a key combo. For modifier-only combos, hold the modifiers together for half a second.";

        var status = UiKit.Label(_t,
            "Click the box, then press a key combo. Modifiers alone (like Ctrl+Shift or Ctrl+Win) work too — hold them together briefly.",
            muted: true);
        status.Margin = new Thickness(0, 6, 0, 0);

        var holdTimer = new System.Windows.Threading.DispatcherTimer
        {
            // Just long enough to distinguish "binding Ctrl+Shift" from someone
            // mid-way through typing Ctrl+Shift+Space.
            Interval = TimeSpan.FromMilliseconds(150),
        };
        var pendingCombo = new List<string>();

        void Bind(string combo)
        {
            holdTimer.Stop();
            var previous = _settings.Hotkey;
            _settings.Hotkey = combo;
            try
            {
                _app.ApplyHotkeySettings();
                _settings.Save();
                capture.Text = combo;
                status.Text = $"Hotkey is now {combo}.";
                Keyboard.ClearFocus();
            }
            catch (Exception ex)
            {
                _settings.Hotkey = previous;
                capture.Text = "press keys…";
                status.Text = $"Couldn't bind {combo}: {ex.Message}";
            }
        }

        // Read physical key state from the OS — WPF's Keyboard.Modifiers omits the
        // Win key entirely (confirmed via capture logging, 2026-07-03).
        static List<string> CurrentModifiers() => HotkeyManager.PhysicalModifiersHeld();

        holdTimer.Tick += (_, _) =>
        {
            holdTimer.Stop();
            // Bind only if the same modifier set is still physically held.
            var held = CurrentModifiers();
            if (pendingCombo.Count >= 2 && held.SequenceEqual(pendingCombo))
            {
                if (pendingCombo.Contains("Win"))
                    HotkeyManager.MaskWinKey(); // keys are still down: cancels the Start menu
                Bind(string.Join("+", pendingCombo));
            }
            else
            {
                Bolo.Core.Storage.BoloLog.Write(
                    $"capture: lock-in aborted (pending: {string.Join("+", pendingCombo)}, held now: {string.Join("+", held)})");
            }
        };

        capture.GotKeyboardFocus += (_, _) =>
        {
            // Release the current global hotkey, otherwise Windows swallows that combo
            // before this box can see it.
            _app.SuspendHotkey();
            capture.Text = "press keys…";
            capture.BorderBrush = _t.Accent;
            pendingCombo.Clear();
            status.Text = "Listening — press a key combo, or hold modifiers together for half a second.";
        };
        capture.LostKeyboardFocus += (_, _) =>
        {
            holdTimer.Stop();
            capture.BorderBrush = _t.CardBorder;
            if (capture.Text is "press keys…" or "" || capture.Text.EndsWith("+…"))
                capture.Text = _settings.Hotkey;
            try { _app.ApplyHotkeySettings(); } catch { /* keep whatever was last bound */ }
        };
        capture.PreviewKeyDown += (_, e) =>
        {
            e.Handled = true; // never let Tab/Space/arrows navigate away mid-capture
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            Bolo.Core.Storage.BoloLog.Write($"capture keydown: {key} (mods: {Keyboard.Modifiers})");

            // Ignore injected/junk keys (incl. the Win-mask dummy, VK 0xFF).
            if (key is Key.None or Key.DeadCharProcessed || KeyInterop.VirtualKeyFromKey(key) >= 0xFE)
                return;

            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
            {
                var parts = CurrentModifiers();
                capture.Text = parts.Count > 0 ? string.Join("+", parts) + "+…" : "press keys…";
                // Held modifiers auto-repeat their keydown every ~30ms; only (re)start
                // the lock-in timer when the combo actually changes, or it never fires.
                if (parts.Count >= 2 && !parts.SequenceEqual(pendingCombo))
                {
                    pendingCombo = parts;
                    status.Text = $"Keep holding to set {string.Join("+", parts)}…";
                    holdTimer.Stop();
                    holdTimer.Start();
                }
                return;
            }

            // A regular key finishes the combo immediately.
            holdTimer.Stop();
            var combo = CurrentModifiers();
            combo.Add(key.ToString());
            Bind(string.Join("+", combo));
        };
        capture.PreviewKeyUp += (_, e) =>
        {
            e.Handled = true;
            // A quick tap of the combo is clear intent — bind on release, instantly.
            if (holdTimer.IsEnabled)
            {
                holdTimer.Stop();
                if (pendingCombo.Count >= 2)
                {
                    var combo = string.Join("+", pendingCombo);
                    pendingCombo.Clear();
                    if (combo.Contains("Win"))
                        HotkeyManager.MaskWinKey();
                    Bind(combo);
                    return;
                }
            }
            // Single modifier released without forming a combo: hint.
            if (capture.Text.EndsWith("+…") && HotkeyManager.PhysicalModifiersHeld().Count == 0)
            {
                capture.Text = "press keys…";
                status.Text = "One modifier alone can't be a hotkey — tap two together (like Ctrl+Win) or add a regular key.";
            }
        };

        var mode = UiKit.Combo(_t, ["Toggle (tap to start / tap to stop)", "Hold-to-talk (hold while speaking)"],
            _settings.HotkeyMode == HotkeyMode.Toggle
                ? "Toggle (tap to start / tap to stop)"
                : "Hold-to-talk (hold while speaking)");
        mode.SelectionChanged += (_, _) =>
        {
            _settings.HotkeyMode = mode.SelectedIndex == 0 ? HotkeyMode.Toggle : HotkeyMode.HoldToTalk;
            _settings.Save();
        };

        var reset = UiKit.ActionButton(_t, "Reset");
        reset.VerticalAlignment = VerticalAlignment.Center;
        reset.ToolTip = "Restore the default hotkey";
        reset.Click += (_, _) => Bind(new AppSettings().Hotkey);

        stack.Children.Add(UiKit.Row(capture, reset));
        stack.Children.Add(status);

        // Alternative to keystroke capture: build the combo from menus. Immune to
        // keyboard-software rebinding weirdness at selection time.
        var pickLabel = UiKit.Label(_t, "Or build the combo manually:", muted: true);
        pickLabel.Margin = new Thickness(0, 12, 0, 4);
        stack.Children.Add(pickLabel);

        CheckBox Mod(string name)
        {
            var box = new CheckBox
            {
                Content = name,
                Foreground = _t.Text,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0),
                IsChecked = _settings.Hotkey.Contains(name, StringComparison.OrdinalIgnoreCase),
            };
            return box;
        }
        var ctrl = Mod("Ctrl");
        var alt = Mod("Alt");
        var shift = Mod("Shift");
        var win = Mod("Win");

        string[] pickableKeys = ["(none)", "Space",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z"];
        var currentKey = _settings.Hotkey.Split('+').LastOrDefault(p =>
            p is not ("Ctrl" or "Alt" or "Shift" or "Win")) ?? "(none)";
        var keyPick = UiKit.Combo(_t, pickableKeys, pickableKeys.Contains(currentKey) ? currentKey : "(none)");
        keyPick.MinWidth = 90;

        var applyPick = UiKit.ActionButton(_t, "Set", primary: true);
        applyPick.Click += (_, _) =>
        {
            var parts = new List<string>();
            if (ctrl.IsChecked == true) parts.Add("Ctrl");
            if (alt.IsChecked == true) parts.Add("Alt");
            if (shift.IsChecked == true) parts.Add("Shift");
            if (win.IsChecked == true) parts.Add("Win");
            var picked = (string)keyPick.SelectedItem;
            if (picked != "(none)")
                parts.Add(picked);

            if (parts.Count == 0 || (parts.Count == 1 && picked == "(none)"))
            {
                status.Text = "Pick at least two modifiers, or one modifier plus a key, or just a function key.";
                return;
            }
            Bind(string.Join("+", parts));
        };

        stack.Children.Add(UiKit.Row(ctrl, alt, shift, win, keyPick, applyPick));
        var modeLabel = UiKit.Label(_t, "Mode", muted: true);
        modeLabel.Margin = new Thickness(0, 10, 0, 4);
        stack.Children.Add(modeLabel);
        stack.Children.Add(mode);
        return stack;
    }

    private UIElement BuildMicSection()
    {
        var devices = new List<string> { "System default" };
        for (var i = 0; i < NAudio.Wave.WaveInEvent.DeviceCount; i++)
            devices.Add(NAudio.Wave.WaveInEvent.GetCapabilities(i).ProductName);

        var selectedIndex = Math.Clamp(_settings.MicDeviceIndex + 1, 0, devices.Count - 1);
        var combo = UiKit.Combo(_t, devices, devices[selectedIndex]);
        combo.SelectionChanged += (_, _) =>
        {
            _settings.MicDeviceIndex = combo.SelectedIndex - 1; // "System default" = -1
            _settings.Save();
        };
        var note = UiKit.Label(_t, "Takes effect on the next dictation.", muted: true);
        note.Margin = new Thickness(0, 6, 0, 0);
        var stack = new StackPanel();
        stack.Children.Add(combo);
        stack.Children.Add(note);
        return stack;
    }

    private UIElement BuildInjectionSection()
    {
        var combo = UiKit.Combo(_t, ["Simulated keystrokes (most compatible)", "Clipboard paste (faster for long text)"],
            _settings.InjectionMode == InjectionMode.Keystrokes
                ? "Simulated keystrokes (most compatible)"
                : "Clipboard paste (faster for long text)");
        combo.SelectionChanged += (_, _) =>
        {
            _settings.InjectionMode = combo.SelectedIndex == 0 ? InjectionMode.Keystrokes : InjectionMode.Paste;
            _settings.Save();
        };
        return combo;
    }

    private UIElement BuildModelSection()
    {
        var stack = new StackPanel();

        var modelsDir = Path.GetDirectoryName(_settings.ModelPath) ?? "";
        var models = Directory.Exists(modelsDir)
            ? Directory.GetFiles(modelsDir, "ggml-*.bin").Select(Path.GetFileName).ToList()!
            : [Path.GetFileName(_settings.ModelPath)];

        var combo = UiKit.Combo(_t, models!, Path.GetFileName(_settings.ModelPath));
        var status = UiKit.Label(_t, $"Loaded on {_controller.LoadedRuntime}.", muted: true);
        status.Margin = new Thickness(0, 6, 0, 0);
        combo.SelectionChanged += async (_, _) =>
        {
            _settings.ModelPath = Path.Combine(modelsDir, (string)combo.SelectedItem);
            _settings.Save();
            status.Text = "Loading model…";
            try
            {
                await _controller.ReloadModelAsync();
                status.Text = $"Loaded on {_controller.LoadedRuntime}.";
            }
            catch (Exception ex)
            {
                status.Text = $"Failed: {ex.Message}";
            }
        };

        var cpu = new CheckBox
        {
            Content = "Force CPU (skip GPU) — takes effect after restarting Bolo",
            IsChecked = _settings.ForceCpu,
            Foreground = _t.TextMuted,
            Margin = new Thickness(0, 10, 0, 0),
        };
        cpu.Checked += (_, _) => { _settings.ForceCpu = true; _settings.Save(); };
        cpu.Unchecked += (_, _) => { _settings.ForceCpu = false; _settings.Save(); };

        stack.Children.Add(combo);
        stack.Children.Add(status);
        stack.Children.Add(cpu);
        return stack;
    }

    private UIElement BuildOverlaySection()
    {
        var stack = new StackPanel();

        var position = UiKit.Combo(_t, Enum.GetNames<OverlayPosition>(), _settings.OverlayPosition.ToString());
        position.SelectionChanged += (_, _) =>
        {
            _settings.OverlayPosition = Enum.Parse<OverlayPosition>((string)position.SelectedItem);
            SaveAndRecreateOverlay();
        };

        var scale = MakeSlider(0.5, 2.5, _settings.OverlayScale, v =>
        {
            _settings.OverlayScale = Math.Round(v, 2);
            SaveAndRecreateOverlay();
        });
        var opacity = MakeSlider(0.3, 1.0, _settings.OverlayOpacity, v =>
        {
            _settings.OverlayOpacity = Math.Round(v, 2);
            SaveAndRecreateOverlay();
        });

        stack.Children.Add(UiKit.Row(UiKit.Label(_t, "Position", muted: true), position));
        var scaleLabel = UiKit.Label(_t, "Size", muted: true);
        scaleLabel.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(scaleLabel);
        stack.Children.Add(scale);
        var opacityLabel = UiKit.Label(_t, "Background opacity", muted: true);
        opacityLabel.Margin = new Thickness(0, 10, 0, 0);
        stack.Children.Add(opacityLabel);
        stack.Children.Add(opacity);

        var preview = UiKit.ActionButton(_t, "Preview overlay (3s)");
        preview.Margin = new Thickness(0, 10, 0, 0);
        preview.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        preview.Click += async (_, _) =>
        {
            _app.PreviewOverlay("This is how your dictation will look.");
            await Task.Delay(3000);
            _app.HideOverlayPreview();
        };
        stack.Children.Add(preview);
        return stack;
    }

    private Slider MakeSlider(double min, double max, double value, Action<double> onChanged)
    {
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            Width = 260,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            IsSnapToTickEnabled = false,
        };
        // Apply on release, not every pixel of drag.
        slider.LostMouseCapture += (_, _) => onChanged(slider.Value);
        return slider;
    }

    private UIElement BuildAppearanceSection()
    {
        var stack = new StackPanel();

        var nameLabel = UiKit.Label(_t, "Your name (for the Home greeting — leave empty for none)", muted: true);
        var nameInput = UiKit.Input(_t, _settings.UserName, 240);
        nameInput.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        nameInput.Margin = new Thickness(0, 4, 0, 12);
        nameInput.LostFocus += (_, _) =>
        {
            _settings.UserName = nameInput.Text.Trim();
            _settings.Save();
        };
        stack.Children.Add(nameLabel);
        stack.Children.Add(nameInput);

        var themeLabel = UiKit.Label(_t, "Theme", muted: true);
        themeLabel.Margin = new Thickness(0, 0, 0, 4);
        stack.Children.Add(themeLabel);
        var theme = UiKit.Combo(_t, ["Dark", "Light"], _settings.Theme);
        theme.SelectionChanged += (_, _) =>
        {
            _settings.Theme = (string)theme.SelectedItem;
            _settings.Save();
            _app.ReopenMainWindow("Settings"); // applies immediately
        };
        stack.Children.Add(theme);
        return stack;
    }

    private UIElement BuildResetSection()
    {
        var reset = UiKit.ActionButton(_t, "Reset all settings to defaults");
        reset.Click += (_, _) =>
        {
            if (MessageBox.Show(
                    "Reset hotkey, mode, injection, overlay, theme, and transforms to defaults?\n(Model, microphone, dictionary, and history are kept.)",
                    "Bolo", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            var defaults = new AppSettings();
            _settings.Hotkey = defaults.Hotkey;
            _settings.HotkeyMode = defaults.HotkeyMode;
            _settings.InjectionMode = defaults.InjectionMode;
            _settings.OverlayPosition = defaults.OverlayPosition;
            _settings.OverlayScale = defaults.OverlayScale;
            _settings.OverlayOpacity = defaults.OverlayOpacity;
            _settings.OverlayEdgeMargin = defaults.OverlayEdgeMargin;
            _settings.Theme = defaults.Theme;
            _settings.RemoveFillerWords = defaults.RemoveFillerWords;
            _settings.ConvertSpokenLists = defaults.ConvertSpokenLists;
            _settings.AutoLearn = defaults.AutoLearn;
            _settings.Save();

            try { _app.ApplyHotkeySettings(); } catch { /* old binding stays if default is taken */ }
            _app.RecreateOverlay();
            _app.ReopenMainWindow("Settings");
        };
        return reset;
    }

    private void SaveAndRecreateOverlay()
    {
        _settings.Save();
        _app.RecreateOverlay();
    }
}
