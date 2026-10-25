using System.Windows;
using System.Windows.Media;

namespace Bolo.App.Ui;

/// <summary>
/// The v1.2 token set: warm near-black layers (window, chrome, panel, raised), hairline
/// borders instead of drop shadows, one accent (gold by default, the red brand is the
/// alternative), red reserved for "live microphone". Light and dark are both first-class.
/// Fonts: Cinzel for titles and the wordmark, Nunito for everything else, Segoe Fluent
/// Icons for glyphs (Segoe MDL2 Assets on Windows 10). Spacing is a 4 px grid.
/// </summary>
public sealed class Theme
{
    // ---- surfaces (base → chrome → panel → raised), lines and text
    public required Brush WindowBg { get; init; }
    public required Brush SidebarBg { get; init; }   // chrome
    public required Brush CardBg { get; init; }      // panel
    public required Brush RaiseBg { get; init; }     // raised rows, hover
    public required Brush CardBorder { get; init; }  // hairline
    public required Brush Line2 { get; init; }       // stronger hairline (inputs, kbd)
    public required Brush Text { get; init; }        // ink
    public required Brush TextBody { get; init; }
    public required Brush TextMuted { get; init; }
    public required Brush TextFaint { get; init; }
    public required Brush InputBg { get; init; }

    // ---- the accent and the state colours
    public required Brush Accent { get; init; }
    public required Brush AccentInk { get; init; }   // accent light enough to read as text on dark
    public required Brush AccentSoft { get; init; }  // 14% accent wash (active nav, chips)
    public required Brush AccentGradient { get; init; }
    public required Color AccentGlow { get; init; }
    public required Brush OnAccent { get; init; }    // text on an accent-filled button
    public required Brush Recording { get; init; }   // the one meaning of red
    public required Color RecordingGlow { get; init; }
    public required Brush Busy { get; init; }        // amber: finalizing
    public required Brush Ok { get; init; }

    public bool IsDark { get; init; } = true;
    public string AccentName { get; init; } = "Gold";

    // ---- type and shape
    public static readonly FontFamily TitleFont = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Cinzel");
    public static readonly FontFamily BodyFont = new(new Uri("pack://application:,,,/"), "./Assets/Fonts/#Nunito");
    public static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public const double Radius = 4;
    public const double RadiusSmall = 3;

    private static readonly Color GoldDark = Color.FromRgb(0xC8, 0xA2, 0x4A);
    private static readonly Color GoldInkDark = Color.FromRgb(0xE2, 0xC4, 0x7C);
    private static readonly Color GoldLight = Color.FromRgb(0xB8, 0x91, 0x2F);
    private static readonly Color GoldInkLight = Color.FromRgb(0x7A, 0x5C, 0x14);
    private static readonly Color Red = Color.FromRgb(0xE8, 0x35, 0x3C);
    private static readonly Color RedInkDark = Color.FromRgb(0xFF, 0x8A, 0x80);
    private static readonly Color RedLight = Color.FromRgb(0xC8, 0x23, 0x2B);
    private static readonly Color Amber = Color.FromRgb(0xF2, 0xA0, 0x0C);

    /// <summary>Theme name "Dark" or "Light"; accent "Gold" (default) or "Red".</summary>
    public static Theme FromName(string name, string accent = "Gold")
    {
        var light = string.Equals(name, "Light", StringComparison.OrdinalIgnoreCase);
        var red = string.Equals(accent, "Red", StringComparison.OrdinalIgnoreCase);
        var a = light ? (red ? RedLight : GoldLight) : (red ? Red : GoldDark);
        var aInk = light ? (red ? RedLight : GoldInkLight) : (red ? RedInkDark : GoldInkDark);
        var onAccent = red || light ? Colors.White : Color.FromRgb(0x17, 0x14, 0x0C);
        return light ? Light(a, aInk, onAccent, red) : Dark(a, aInk, onAccent, red);
    }

    private static Theme Dark(Color a, Color aInk, Color onAccent, bool red) => new()
    {
        WindowBg = Solid(0x0E, 0x0E, 0x0D),
        SidebarBg = Solid(0x14, 0x14, 0x13),
        CardBg = Solid(0x18, 0x18, 0x17),
        RaiseBg = Solid(0x22, 0x21, 0x1F),
        CardBorder = Solid(0x2D, 0x2C, 0x29),
        Line2 = Solid(0x3A, 0x39, 0x35),
        Text = Solid(0xF3, 0xF1, 0xEC),
        TextBody = Solid(0xD6, 0xD3, 0xCC),
        TextMuted = Solid(0xA3, 0xA0, 0x98),
        TextFaint = Solid(0x75, 0x73, 0x6C),
        InputBg = Solid(0x0E, 0x0E, 0x0D),
        Accent = Freeze(new SolidColorBrush(a)),
        AccentInk = Freeze(new SolidColorBrush(aInk)),
        AccentSoft = Freeze(new SolidColorBrush(Color.FromArgb(0x24, a.R, a.G, a.B))),
        AccentGradient = Gradient(aInk, a),
        AccentGlow = a,
        OnAccent = Freeze(new SolidColorBrush(onAccent)),
        Recording = Freeze(new SolidColorBrush(Red)),
        RecordingGlow = Red,
        Busy = Freeze(new SolidColorBrush(Amber)),
        Ok = Solid(0x6F, 0xBF, 0x73),
        IsDark = true,
        AccentName = red ? "Red" : "Gold",
    };

    private static Theme Light(Color a, Color aInk, Color onAccent, bool red) => new()
    {
        WindowBg = Solid(0xF3, 0xF1, 0xEC),
        SidebarBg = Solid(0xEB, 0xE8, 0xE1),
        CardBg = Solid(0xFF, 0xFF, 0xFF),
        RaiseBg = Solid(0xF6, 0xF5, 0xF1),
        CardBorder = Solid(0xE2, 0xDF, 0xD8),
        Line2 = Solid(0xD2, 0xCF, 0xC7),
        Text = Solid(0x1D, 0x1D, 0x1B),
        TextBody = Solid(0x3A, 0x3A, 0x37),
        TextMuted = Solid(0x66, 0x64, 0x5F),
        TextFaint = Solid(0x8E, 0x8B, 0x84),
        InputBg = Solid(0xFF, 0xFF, 0xFF),
        Accent = Freeze(new SolidColorBrush(a)),
        AccentInk = Freeze(new SolidColorBrush(aInk)),
        AccentSoft = Freeze(new SolidColorBrush(Color.FromArgb(0x1F, a.R, a.G, a.B))),
        AccentGradient = Gradient(a, aInk),
        AccentGlow = a,
        OnAccent = Freeze(new SolidColorBrush(onAccent)),
        Recording = Freeze(new SolidColorBrush(RedLight)),
        RecordingGlow = RedLight,
        Busy = Freeze(new SolidColorBrush(Amber)),
        Ok = Solid(0x3E, 0x8E, 0x45),
        IsDark = false,
        AccentName = red ? "Red" : "Gold",
    };

    public Color ColorOf(Brush brush) => brush is SolidColorBrush s ? s.Color : Colors.Transparent;

    private static Brush Solid(byte r, byte g, byte b) => Freeze(new SolidColorBrush(Color.FromRgb(r, g, b)));

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Brush Gradient(Color from, Color to) =>
        Freeze(new LinearGradientBrush(from, to, new Point(0, 0), new Point(0, 1)));
}
