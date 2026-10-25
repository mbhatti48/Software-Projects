using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using GdiColor = System.Drawing.Color;

namespace Bolo.App;

/// <summary>
/// System tray icon: the simplified Bolo mark (a dark tile with a Cinzel "B" in the accent)
/// whose whole tile changes colour with state, which reads far better at 16 px than a badge:
/// red tile while recording, amber while transcribing, grey while loading. The menu is
/// drawn in the app's dark style instead of the stock WinForms look.
/// </summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _idleIcon;
    private readonly Icon _recordingIcon;
    private readonly Icon _busyIcon;
    private readonly Icon _loadingIcon;
    private readonly ContextMenuStrip _menu;

    private static PrivateFontCollection? _fonts;
    private static IntPtr _fontMemory;

    public event Action? ToggleRequested;
    public event Action? OpenRequested;
    public event Action? CopyLastRequested;
    public event Action? FixLastRequested;
    public event Action? SaveLastRequested;
    public event Action? ExitRequested;
    /// <summary>A plain click on the icon: show the last few transcripts by the tray.</summary>
    public event Action? HistoryRequested;

    public TrayIcon(bool darkTheme, System.Windows.Media.Color accent)
    {
        var gold = GdiColor.FromArgb(accent.R, accent.G, accent.B);
        var size = SystemInformation.SmallIconSize.Width; // 16 at 100 %, 24 at 150 %, 32 at 200 %
        _idleIcon = DrawIcon(size, tile: GdiColor.FromArgb(0x14, 0x14, 0x13), letter: gold);
        _recordingIcon = DrawIcon(size, tile: GdiColor.FromArgb(0xE8, 0x35, 0x3C), letter: GdiColor.White);
        _busyIcon = DrawIcon(size, tile: GdiColor.FromArgb(0xF2, 0xA0, 0x0C), letter: GdiColor.FromArgb(0x17, 0x14, 0x0C));
        _loadingIcon = DrawIcon(size, tile: GdiColor.FromArgb(0x3A, 0x39, 0x35), letter: GdiColor.FromArgb(0xA3, 0xA0, 0x98));

        _menu = new ContextMenuStrip
        {
            Renderer = new MenuRenderer(darkTheme),
            ShowImageMargin = false,
            Font = new Font("Segoe UI Variable Text", 9.5f, GraphicsUnit.Point),
            Padding = new Padding(4),
        };
        var fore = darkTheme ? GdiColor.FromArgb(0xF3, 0xF1, 0xEC) : GdiColor.FromArgb(0x1D, 0x1D, 0x1B);
        ToolStripMenuItem Item(string text, Action onClick)
        {
            var item = new ToolStripMenuItem(text) { ForeColor = fore, Padding = new Padding(6, 4, 6, 4) };
            item.Click += (_, _) => onClick();
            return item;
        }
        var open = Item("Open Bolo", () => OpenRequested?.Invoke());
        open.Font = new Font(_menu.Font, System.Drawing.FontStyle.Bold);
        _menu.Items.Add(open);
        _menu.Items.Add(Item("Start / stop dictation", () => ToggleRequested?.Invoke()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Copy last transcript", () => CopyLastRequested?.Invoke()));
        _menu.Items.Add(Item("Fix last transcript…", () => FixLastRequested?.Invoke()));
        _menu.Items.Add(Item("Save last transcript to .txt…", () => SaveLastRequested?.Invoke()));
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(Item("Exit", () => ExitRequested?.Invoke()));

        _icon = new NotifyIcon
        {
            Icon = _loadingIcon,
            Text = "Bolo: loading the model…",
            Visible = true,
            ContextMenuStrip = _menu,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _icon.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) HistoryRequested?.Invoke(); };
    }

    public void SetState(DictationState state, string runtime)
    {
        (_icon.Icon, _icon.Text) = state switch
        {
            DictationState.Loading => (_loadingIcon, "Bolo: loading the model…"),
            DictationState.Recording => (_recordingIcon, "Bolo: listening (press the hotkey to stop)"),
            DictationState.Transcribing => (_busyIcon, "Bolo: transcribing…"),
            _ => (_idleIcon, $"Bolo: ready ({runtime})"),
        };
    }

    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info) =>
        _icon.ShowBalloonTip(3000, title, message, icon);

    /// <summary>
    /// Draws the mark at the exact tray size: a rounded tile with a hairline and a Cinzel
    /// Bold "B" from the embedded font (Georgia if the resource can't be read).
    /// </summary>
    private static Icon DrawIcon(int size, GdiColor tile, GdiColor letter)
    {
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var radius = size * 0.22f;
        using (var path = RoundedRect(0.5f, 0.5f, size - 1, size - 1, radius))
        {
            using var fill = new SolidBrush(tile);
            g.FillPath(fill, path);
            using var edge = new Pen(GdiColor.FromArgb(0x46, 0xFF, 0xFF, 0xFF), 1f);
            g.DrawPath(edge, path);
        }

        using var font = new Font(TitleFamily(), size * 0.74f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        using var brush = new SolidBrush(letter);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("B", font, brush, new RectangleF(-size * 0.03f, size * 0.02f, size, size), format);

        return Icon.FromHandle(bmp.GetHicon());
    }

    private static System.Drawing.FontFamily TitleFamily()
    {
        if (_fonts is null)
        {
            try
            {
                var info = System.Windows.Application.GetResourceStream(
                    new Uri("pack://application:,,,/Assets/Fonts/Cinzel-Bold.ttf"))!;
                using var stream = info.Stream;
                var bytes = new byte[stream.Length];
                stream.ReadExactly(bytes);
                _fontMemory = Marshal.AllocCoTaskMem(bytes.Length); // must outlive the collection
                Marshal.Copy(bytes, 0, _fontMemory, bytes.Length);
                _fonts = new PrivateFontCollection();
                _fonts.AddMemoryFont(_fontMemory, bytes.Length);
            }
            catch
            {
                _fonts = new PrivateFontCollection();
            }
        }
        return _fonts.Families.Length > 0 ? _fonts.Families[0] : new System.Drawing.FontFamily("Georgia");
    }

    private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
    {
        var path = new GraphicsPath();
        path.AddArc(x, y, r * 2, r * 2, 180, 90);
        path.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
        path.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
        path.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Flat menu in the app's panel colours: no gradients, no image margin, a hairline border.</summary>
    private sealed class MenuRenderer(bool dark) : ToolStripProfessionalRenderer(new MenuColors(dark))
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = new System.Drawing.Rectangle(2, 0, e.Item.Width - 4, e.Item.Height);
            if (e.Item.Selected && e.Item.Enabled)
            {
                using var brush = new SolidBrush(dark ? GdiColor.FromArgb(0x22, 0x21, 0x1F) : GdiColor.FromArgb(0xF6, 0xF5, 0xF1));
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var path = RoundedRect(rect.X, rect.Y, rect.Width, rect.Height, 3);
                e.Graphics.FillPath(brush, path);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using var pen = new Pen(dark ? GdiColor.FromArgb(0x2D, 0x2C, 0x29) : GdiColor.FromArgb(0xE2, 0xDF, 0xD8));
            var y = e.Item.Height / 2;
            e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
        }
    }

    private sealed class MenuColors(bool dark) : ProfessionalColorTable
    {
        private readonly GdiColor _panel = dark ? GdiColor.FromArgb(0x18, 0x18, 0x17) : GdiColor.White;
        private readonly GdiColor _line = dark ? GdiColor.FromArgb(0x3A, 0x39, 0x35) : GdiColor.FromArgb(0xD2, 0xCF, 0xC7);
        public override GdiColor ToolStripDropDownBackground => _panel;
        public override GdiColor ImageMarginGradientBegin => _panel;
        public override GdiColor ImageMarginGradientMiddle => _panel;
        public override GdiColor ImageMarginGradientEnd => _panel;
        public override GdiColor MenuBorder => _line;
        public override GdiColor MenuItemBorder => GdiColor.Transparent;
        public override GdiColor MenuItemSelected => _panel;
        public override GdiColor SeparatorDark => _line;
        public override GdiColor SeparatorLight => _panel;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}
