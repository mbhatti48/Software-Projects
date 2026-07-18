using System.Windows;
using System.Windows.Media;

namespace Bolo.App.Ui;

/// <summary>
/// Shared palette for the main window. Red brand accent (from the Bolo logo), subtle
/// gradients, equal design care in Dark and Light.
/// </summary>
public sealed class Theme
{
    public required Brush WindowBg { get; init; }
    public required Brush SidebarBg { get; init; }
    public required Brush CardBg { get; init; }
    public required Brush CardBorder { get; init; }
    public required Brush Text { get; init; }
    public required Brush TextMuted { get; init; }
    public required Brush Accent { get; init; }
    public required Brush AccentGradient { get; init; }
    public required Color AccentGlow { get; init; }
    public required Brush InputBg { get; init; }

    public static Theme Dark { get; } = new()
    {
        WindowBg = Solid(0x17, 0x17, 0x1A),
        SidebarBg = Solid(0x11, 0x11, 0x14),
        CardBg = Gradient(Color.FromRgb(0x26, 0x26, 0x2B), Color.FromRgb(0x1F, 0x1F, 0x24)),
        CardBorder = Solid(0x2E, 0x2E, 0x34),
        Text = Solid(0xEC, 0xEC, 0xEE),
        TextMuted = Solid(0x90, 0x92, 0xA0),
        Accent = Solid(0xE8, 0x35, 0x3C),
        AccentGradient = Gradient(Color.FromRgb(0xE8, 0x35, 0x3C), Color.FromRgb(0xB0, 0x1E, 0x24)),
        AccentGlow = Color.FromRgb(0xE8, 0x35, 0x3C),
        InputBg = Solid(0x2A, 0x2A, 0x30),
    };

    public static Theme Light { get; } = new()
    {
        WindowBg = Solid(0xF6, 0xF6, 0xF8),
        SidebarBg = Solid(0xEC, 0xEC, 0xF0),
        CardBg = Gradient(Colors.White, Color.FromRgb(0xF8, 0xF8, 0xFA)),
        CardBorder = Solid(0xE2, 0xE2, 0xE8),
        Text = Solid(0x1C, 0x1C, 0x20),
        TextMuted = Solid(0x6E, 0x6E, 0x78),
        Accent = Solid(0xC8, 0x23, 0x2B),
        AccentGradient = Gradient(Color.FromRgb(0xD8, 0x2A, 0x33), Color.FromRgb(0x9E, 0x16, 0x1D)),
        AccentGlow = Color.FromRgb(0xC8, 0x23, 0x2B),
        InputBg = Solid(0xEF, 0xEF, 0xF3),
    };

    public static Theme FromName(string name) =>
        string.Equals(name, "Light", StringComparison.OrdinalIgnoreCase) ? Light : Dark;

    private static Brush Solid(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Brush Gradient(Color from, Color to)
    {
        var brush = new LinearGradientBrush(from, to, new Point(0, 0), new Point(1, 1));
        brush.Freeze();
        return brush;
    }
}
