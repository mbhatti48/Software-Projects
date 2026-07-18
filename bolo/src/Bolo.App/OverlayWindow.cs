using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Bolo.Core.Storage;

namespace Bolo.App;

/// <summary>
/// Bottom-center floating pill shown while dictating: pulsing dot + live ghost text.
/// Never takes focus (WS_EX_NOACTIVATE) — the target app must stay focused so the
/// final transcript injects into it.
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

    private static readonly Brush RecordingBrush = new SolidColorBrush(Color.FromRgb(0xE0, 0x3C, 0x31));
    private static readonly Brush BusyBrush = new SolidColorBrush(Color.FromRgb(0xF2, 0xA0, 0x0C));

    private readonly Ellipse _dot;
    private readonly TextBlock _status;
    private readonly TextBlock _ghost;
    private readonly AppSettings _settings;

    public OverlayWindow(AppSettings settings)
    {
        _settings = settings;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;

        _dot = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = RecordingBrush,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _status = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _ghost = new TextBlock
        {
            Foreground = new SolidColorBrush(Color.FromArgb(0xAA, 0xFF, 0xFF, 0xFF)),
            FontStyle = FontStyles.Italic,
            FontSize = 13,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 560,
        };

        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        panel.Children.Add(_dot);
        panel.Children.Add(_status);
        panel.Children.Add(_ghost);

        var opacity = (byte)(Math.Clamp(_settings.OverlayOpacity, 0.3, 1.0) * 255);
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(opacity, 0x20, 0x20, 0x24)),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(14, 9, 14, 9),
            Child = panel,
        };
        if (Math.Abs(_settings.OverlayScale - 1.0) > 0.01)
        {
            var scale = Math.Clamp(_settings.OverlayScale, 0.5, 3.0);
            ((Border)Content).LayoutTransform = new ScaleTransform(scale, scale);
        }

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

        var margin = _settings.OverlayEdgeMargin;
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
        _dot.Fill = RecordingBrush;
        _status.Text = "Listening…";
        _ghost.Text = "";
        ((System.Windows.Controls.Border)Content).Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(0xE8, 0x35, 0x3C),
            BlurRadius = 22,
            ShadowDepth = 0,
            Opacity = 0.45,
        };
        StartPulse();
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
        _status.Text = "Finalizing…";
        ((System.Windows.Controls.Border)Content).Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Color.FromRgb(0xF2, 0xA0, 0x0C),
            BlurRadius = 18,
            ShadowDepth = 0,
            Opacity = 0.35,
        };
    }

    public void HideOverlay()
    {
        _dot.BeginAnimation(OpacityProperty, null);
        Hide();
    }

    private void StartPulse()
    {
        _dot.BeginAnimation(OpacityProperty, new DoubleAnimation(1.0, 0.25, TimeSpan.FromMilliseconds(600))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
        });
    }
}
