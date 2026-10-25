using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Bolo.Core.Storage;

namespace Bolo.App.Ui;

/// <summary>
/// Verification tool, not a user feature: <c>Bolo.exe --screenshots &lt;folder&gt;</c> renders every
/// page in dark and light, the overlay pill and the onboarding window to PNG files, then exits.
/// It takes no hotkey, opens no microphone, loads no model and does not touch the tray or the
/// single-instance mutex, so it can run beside a live Bolo without interfering with it.
/// </summary>
internal static class Screenshots
{
    public static async Task RunAsync(string folder)
    {
        Directory.CreateDirectory(folder);
        var settings = AppSettings.Load();
        var controller = new DictationController(settings);
        var app = (App)Application.Current;

        foreach (var (theme, accent) in new[] { ("Dark", "Gold"), ("Light", "Gold"), ("Dark", "Red") })
        {
            var variant = Clone(settings);
            variant.Theme = theme;
            variant.Accent = accent;
            var window = new MainWindow(variant, controller, app) { Left = -4000, Top = 0, WindowStartupLocation = WindowStartupLocation.Manual };
            window.Show();
            foreach (var page in new[] { "Home", "History", "Dictionary", "Snippets", "Insights", "Style", "Settings" })
            {
                window.Navigate(page);
                await Settle();
                Save(window, Path.Combine(folder, $"{theme.ToLower()}-{accent.ToLower()}-{page.ToLower()}.png"));
            }
            window.ForceClose();
        }

        var onboarding = new OnboardingWindow(settings, controller) { Left = -4000, Top = 0, Height = 1000, WindowStartupLocation = WindowStartupLocation.Manual }; // tall enough to show every step
        onboarding.Show();
        await Settle();
        Save(onboarding, Path.Combine(folder, "onboarding.png"));
        onboarding.Close();

        var overlay = new OverlayWindow(settings, () => 0.55f);
        overlay.ShowRecording();
        overlay.SetGhostText("and then the final text is typed straight into whatever has focus");
        await Settle(400);
        Save(overlay, Path.Combine(folder, "overlay-listening.png"));
        overlay.ShowFinalizing();
        await Settle(200);
        Save(overlay, Path.Combine(folder, "overlay-finalizing.png"));
        overlay.Close();

        var popover = new TrayPopover(Theme.FromName(settings.Theme, settings.Accent), controller, () => { }) { Topmost = false };
        popover.Show();
        await Settle(300);
        Save(popover, Path.Combine(folder, "tray-popover.png"));
        popover.Close();
    }

    private static AppSettings Clone(AppSettings s) => new()
    {
        ModelPath = s.ModelPath, Language = s.Language, ForceCpu = s.ForceCpu, MicDeviceIndex = s.MicDeviceIndex,
        AutoLearn = s.AutoLearn, Hotkey = s.Hotkey, HotkeyMode = s.HotkeyMode, InjectionMode = s.InjectionMode,
        OverlayPosition = s.OverlayPosition, OverlayScale = s.OverlayScale, OverlayOpacity = s.OverlayOpacity,
        OverlayEdgeMargin = s.OverlayEdgeMargin, Theme = s.Theme, Accent = s.Accent, UserName = s.UserName,
        RemoveFillerWords = s.RemoveFillerWords, ConvertSpokenLists = s.ConvertSpokenLists, OnboardingDone = true,
    };

    /// <summary>Let layout, the entrance animation and the chart growth finish before capturing.</summary>
    private static async Task Settle(int ms = 700)
    {
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);
        await Task.Delay(ms);
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
    }

    /// <summary>
    /// Renders the window's content 1:1 at 96 dpi: the background first, then the content
    /// visual itself. (A VisualBrush would scale by the glow effects' bleed and shift
    /// everything a few pixels, which misled the first pass of this tool.)
    /// </summary>
    private static void Save(Window window, string path)
    {
        var visual = (FrameworkElement)window.Content;
        var width = (int)Math.Ceiling(visual.ActualWidth);
        var height = (int)Math.Ceiling(visual.ActualHeight);
        if (width == 0 || height == 0)
            return;
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        if (window.Background is { } bg)
        {
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
                dc.DrawRectangle(bg, null, new Rect(0, 0, width, height));
            bitmap.Render(dv);
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
