using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bolo.Core.Storage;

public enum InjectionMode
{
    /// <summary>Simulated keystrokes via SendInput — most broadly compatible.</summary>
    Keystrokes,
    /// <summary>Clipboard swap + Ctrl+V — faster for long text; original clipboard is restored.</summary>
    Paste,
}

public enum OverlayPosition { BottomCenter, TopCenter, BottomLeft, BottomRight }

public enum HotkeyMode
{
    /// <summary>Tap to start recording, tap again to stop.</summary>
    Toggle,
    /// <summary>Hold the key to record, release to stop (push-to-talk).</summary>
    HoldToTalk,
}

public sealed class AppSettings
{
    public string ModelPath { get; set; } = "";
    public string Language { get; set; } = "en";
    public bool ForceCpu { get; set; } = false;
    /// <summary>NAudio input device index; -1 = Windows default microphone.</summary>
    public int MicDeviceIndex { get; set; } = -1;
    /// <summary>Spelled-out acronym collapse + "add to dictionary" voice command.</summary>
    public bool AutoLearn { get; set; } = true;
    public string Hotkey { get; set; } = "F9";
    // Hold-to-talk default — matches how the INSTALL guide teaches dictation.
    public HotkeyMode HotkeyMode { get; set; } = HotkeyMode.HoldToTalk;
    public InjectionMode InjectionMode { get; set; } = InjectionMode.Keystrokes;

    // Overlay appearance — a visual editor for these lands in the Settings page (Phase 3).
    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.BottomCenter;
    /// <summary>1.0 = default size; 1.5 = 50% bigger, etc.</summary>
    public double OverlayScale { get; set; } = 1.0;
    /// <summary>Background opacity of the pill, 0.3–1.0.</summary>
    public double OverlayOpacity { get; set; } = 0.9;
    /// <summary>Distance in pixels from the screen edge.</summary>
    public int OverlayEdgeMargin { get; set; } = 48;

    public string Theme { get; set; } = "Dark";

    /// <summary>Optional display name for the Home page greeting. Empty = generic greeting.</summary>
    public string UserName { get; set; } = "";

    // Style / Transforms (rule-based, offline) — opt-in.
    public bool RemoveFillerWords { get; set; } = false;
    public bool ConvertSpokenLists { get; set; } = false;

    public bool OnboardingDone { get; set; } = false;

    /// <summary>
    /// Set when Load() found an existing settings.json it couldn't read and fell back to
    /// safe defaults. Save() refuses to write in this state, so a transient read failure
    /// (e.g. the file briefly locked by another instance) can never overwrite the real
    /// settings on disk. Not persisted.
    /// </summary>
    [JsonIgnore]
    public bool LoadFailed { get; private set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Load()
    {
        if (File.Exists(BoloPaths.SettingsFile))
        {
            try
            {
                var json = File.ReadAllText(BoloPaths.SettingsFile);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                    return loaded;
            }
            catch (Exception ex)
            {
                // The file exists but couldn't be read (locked by another instance,
                // transient I/O, corruption). Never treat this as a first run: that would
                // drop the user into onboarding and let a later save overwrite good
                // settings with defaults. Fall back for this session only and leave the
                // file on disk untouched (see Save()).
                BoloLog.Write($"settings load failed, keeping the file on disk untouched: {ex.Message}");
                return new AppSettings { OnboardingDone = true, LoadFailed = true };
            }
        }
        return new AppSettings();
    }

    public void Save()
    {
        if (LoadFailed)
        {
            BoloLog.Write("settings save skipped — running on fallback defaults after a failed load; not overwriting the file on disk");
            return;
        }
        BoloPaths.EnsureDirectories();
        File.WriteAllText(BoloPaths.SettingsFile, JsonSerializer.Serialize(this, JsonOptions));
    }
}
