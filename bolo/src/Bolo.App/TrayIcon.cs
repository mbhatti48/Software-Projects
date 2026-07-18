using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GdiColor = System.Drawing.Color;

namespace Bolo.App;

/// <summary>
/// System tray icon: the Bolo logo with a state badge (red = recording, amber =
/// transcribing, grey = loading; no badge when idle). Falls back to drawn circles
/// if the logo asset is missing.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _idleIcon;
    private readonly Icon _recordingIcon;
    private readonly Icon _busyIcon;
    private readonly Icon _loadingIcon;

    public event Action? ToggleRequested;
    public event Action? OpenRequested;
    public event Action? CopyLastRequested;
    public event Action? FixLastRequested;
    public event Action? SaveLastRequested;
    public event Action? ExitRequested;

    public TrayIcon()
    {
        _idleIcon = DrawIcon(badge: null);
        _recordingIcon = DrawIcon(GdiColor.FromArgb(0xE0, 0x3C, 0x31));
        _busyIcon = DrawIcon(GdiColor.FromArgb(0xF2, 0xA0, 0x0C));
        _loadingIcon = DrawIcon(GdiColor.FromArgb(0x8A, 0x8A, 0x8A));

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Bolo", null, (_, _) => OpenRequested?.Invoke());
        menu.Items.Add("Start / stop dictation", null, (_, _) => ToggleRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Copy last transcript", null, (_, _) => CopyLastRequested?.Invoke());
        menu.Items.Add("Fix last transcript…", null, (_, _) => FixLastRequested?.Invoke());
        menu.Items.Add("Save last transcript to .txt…", null, (_, _) => SaveLastRequested?.Invoke());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitRequested?.Invoke());

        _icon = new NotifyIcon
        {
            Icon = _loadingIcon,
            Text = "Bolo — loading model…",
            Visible = true,
            ContextMenuStrip = menu,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    public void SetState(DictationState state, string runtime)
    {
        (_icon.Icon, _icon.Text) = state switch
        {
            DictationState.Loading => (_loadingIcon, "Bolo — loading model…"),
            DictationState.Recording => (_recordingIcon, "Bolo — recording (press hotkey to stop)"),
            DictationState.Transcribing => (_busyIcon, "Bolo — transcribing…"),
            _ => (_idleIcon, $"Bolo — ready ({runtime})"),
        };
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info) =>
        _icon.ShowBalloonTip(3000, title, message, icon);

    /// <summary>
    /// Tray icons render at ~16px, where the full logo's lettering turns to mush —
    /// so the tray uses a simplified brand mark: red rounded tile + bold white B.
    /// The full logo stays on the exe/desktop where there's room for it.
    /// </summary>
    private static Icon DrawIcon(GdiColor? badge)
    {
        using var bmp = new Bitmap(32, 32);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        using var tile = new LinearGradientBrush(
            new System.Drawing.Rectangle(1, 1, 30, 30),
            GdiColor.FromArgb(0xE8, 0x35, 0x3C), GdiColor.FromArgb(0xB0, 0x1E, 0x24), 55f);
        using (var path = RoundedRect(1, 1, 30, 30, 9))
            g.FillPath(tile, path);

        using var font = new Font("Segoe UI", 19, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        var size = g.MeasureString("B", font);
        g.DrawString("B", font, System.Drawing.Brushes.White, (32 - size.Width) / 2, (32 - size.Height) / 2 - 1);

        if (badge is not null)
        {
            using var outline = new SolidBrush(GdiColor.White);
            g.FillEllipse(outline, 17, 17, 15, 15);
            using var fill = new SolidBrush(badge.Value);
            g.FillEllipse(fill, 19, 19, 11, 11);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath RoundedRect(int x, int y, int w, int h, int r)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
