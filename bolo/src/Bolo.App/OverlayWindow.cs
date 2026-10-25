using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Bolo.App.Ui;
using Bolo.Core.Storage;

namespace Bolo.App;

/// <summary>
/// The floating pill shown while dictating: a state dot, a small uppercase label, seven
/// live level bars fed by the microphone, and the ghost text. Dark glass whatever the
/// window theme, because it floats over other apps. Never takes focus (WS_EX_NOACTIVATE):
/// the target app must stay focused so the final transcript injects into it.
/// </summary>
public sealed class OverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExNoactivate = 0x08000000;
    private const int WsExToolwindow = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static readonly Color RecordingColor = Color.FromRgb(0xE8, 0x35, 0x3C);
    private static readonly Color BusyColor = Color.FromRgb(0xF2, 0xA0, 0x0C);
    private static readonly Brush RecordingBrush = Frozen(RecordingColor);
    private static readonly Brush BusyBrush = Frozen(BusyColor);
    private static readonly Brush BarBrush = Frozen(Color.FromRgb(0xD6, 0xD3, 0xCC));

    private readonly Ellipse _dot;
    private readonly TextBlock _status;
    private readonly TextBlock _ghost;
    private readonly Rectangle[] _bars = new Rectangle[7];
    private readonly double[] _barLevels = new double[7];
    private readonly Border _pill;
    private readonly AppSettings _settings;
    private readonly Func<float> _level;
    private readonly DispatcherTimer _meter;
    private readonly TranslateTransform _rise = new(0, 0);
    private bool _hiding;

    public OverlayWindow(AppSettings settings, Func<float> inputLevel)
    {
        _settings = settings;
        _level = inputLevel;
        var t = Theme.FromName("Dark", settings.Accent);
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        FontFamily = Theme.BodyFont;

        _dot = new Ellipse { Width = 9, Height = 9, Fill = RecordingBrush, VerticalAlignment = VerticalAlignment.Center };
        _status = new TextBlock
        {
            Text = "LISTENING",
            Foreground = t.AccentInk,
            FontSize = 11,
            FontWeight = FontWeights.ExtraBold,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var bars = new StackPanel { Orientation = Orientation.Horizontal, Height = 16, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < _bars.Length; i++)
        {
            _bars[i] = new Rectangle { Width = 2, Height = 3, RadiusX = 1, RadiusY = 1, Fill = BarBrush, Margin = new Thickness(0, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center };
            bars.Children.Add(_bars[i]);
        }
        _ghost = new TextBlock
        {
            Foreground = Frozen(Color.FromRgb(0xC9, 0xC6, 0xBF)),
            FontStyle = FontStyles.Italic,
            FontSize = 14,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 520,
            TextTrimming = TextTrimming.None,
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(_dot);
        panel.Children.Add(_status);
        panel.Children.Add(bars);
        panel.Children.Add(_ghost);

        var opacity = (byte)(Math.Clamp(_settings.OverlayOpacity, 0.3, 1.0) * 255);
        _pill = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(opacity, 0x14, 0x14, 0x13)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x1C, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(16, 0, 18, 0),
            Height = 44,
            Margin = new Thickness(24), // room for the glow inside the transparent window
            Child = panel,
            RenderTransform = _rise,
        };
        Content = _pill;
        if (Math.Abs(_settings.OverlayScale - 1.0) > 0.01)
        {
            var scale = Math.Clamp(_settings.OverlayScale, 0.5, 3.0);
            _pill.LayoutTransform = new ScaleTransform(scale, scale);
        }

        _meter = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(50) };
        _meter.Tick += (_, _) => TickMeter();

        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            SetWindowLong(handle, GwlExstyle,
                GetWindowLong(handle, GwlExstyle) | WsExNoactivate | WsExToolwindow);
        };
        SizeChanged += (_, _) => Reposition();
    }

    private void Reposition()
    {
        // Show on the monitor hosting the app being dictated into, not always the
        // primary. Screen works in device pixels; convert to WPF units.
        Rect area;
        try
        {
            var screen = System.Windows.Forms.Screen.FromHandle(GetForegroundWindow());
            var dpi = VisualTreeHelper.GetDpi(this);
            var wa = screen.WorkingArea;
            area = new Rect(wa.Left / dpi.DpiScaleX, wa.Top / dpi.DpiScaleY,
                wa.Width / dpi.DpiScaleX, wa.Height / dpi.DpiScaleY);
        }
        catch
        {
            area = SystemParameters.WorkArea;
        }

        // The window carries a 24 px transparent margin for the glow; the pill itself keeps the configured edge margin.
        var margin = _settings.OverlayEdgeMargin - 24;
        (Left, Top) = _settings.OverlayPosition switch
        {
            OverlayPosition.TopCenter =>
                (area.Left + (area.Width - ActualWidth) / 2, area.Top + margin),
            OverlayPosition.BottomLeft =>
                (area.Left + margin, area.Bottom - ActualHeight - margin),
            OverlayPosition.BottomRight =>
                (area.Right - ActualWidth - margin, area.Bottom - ActualHeight - margin),
            _ => (area.Left + (area.Width - ActualWidth) / 2, area.Bottom - ActualHeight - margin),
        };
    }

    public void ShowRecording()
    {
        _hiding = false;
        _dot.Fill = RecordingBrush;
        _status.Text = "LISTENING";
        _ghost.Text = "";
        SetGlow(RecordingColor, 0.35);
        StartPulse();
        _meter.Start();
        AnimateIn();
        Show();
        Reposition();
    }

    public void SetGhostText(string text)
    {
        // Show the tail so the most recent words are always visible.
        const int maxChars = 90;
        _ghost.Text = text.Length <= maxChars ? text : "…" + text[^maxChars..];
    }

    public void ShowFinalizing()
    {
        _dot.Fill = BusyBrush;
        _dot.BeginAnimation(OpacityProperty, null);
        _dot.Opacity = 1;
        _status.Text = "FINALIZING";
        _meter.Stop();
        foreach (var bar in _bars)
            bar.Height = 3;
        SetGlow(BusyColor, 0.3);
    }

    public void HideOverlay()
    {
        _dot.BeginAnimation(OpacityProperty, null);
        _meter.Stop();
        if (!IsVisible || !SystemParameters.ClientAreaAnimation)
        {
            Hide();
            return;
        }
        // Fade out over 120 ms, then hide for real.
        _hiding = true;
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(120));
        fade.Completed += (_, _) =>
        {
            if (_hiding)
            {
                Hide();
                _pill.Opacity = 1;
                _hiding = false;
            }
        };
        _pill.BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateIn()
    {
        _pill.BeginAnimation(OpacityProperty, null);
        _pill.Opacity = 1;
        if (!SystemParameters.ClientAreaAnimation)
            return;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        _pill.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { EasingFunction = ease });
        _rise.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
    }

    private void SetGlow(Color color, double opacity) =>
        _pill.Effect = new DropShadowEffect { Color = color, BlurRadius = 28, ShadowDepth = 0, Opacity = opacity };

    private void StartPulse()
    {
        _dot.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(650))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        });
    }

    /// <summary>Seven bars follow the live mic level with a little spread and decay, so they move like a meter, not a loop.</summary>
    private void TickMeter()
    {
        var level = Math.Clamp(_level(), 0f, 1f);
        var shaped = Math.Sqrt(level); // quiet speech still visibly moves the bars
        var weights = new[] { 0.45, 0.7, 1.0, 0.85, 0.95, 0.6, 0.4 };
        for (var i = 0; i < _bars.Length; i++)
        {
            var target = 3 + 13 * shaped * weights[i];
            _barLevels[i] = target > _barLevels[i] ? target : _barLevels[i] * 0.78 + target * 0.22;
            _bars[i].Height = Math.Max(3, _barLevels[i]);
        }
    }

    private static Brush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
