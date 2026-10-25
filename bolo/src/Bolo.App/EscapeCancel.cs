using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace Bolo.App;

/// <summary>
/// Esc cancels a recording. The key is claimed system-wide only while Bolo is recording
/// (armed on the Recording state, released on any other), so outside a take Esc belongs
/// to whatever app has focus as usual. Claiming it, rather than watching it, keeps the
/// focused app from also acting on the same press.
/// </summary>
public sealed class EscapeCancel : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xB011;
    private const uint VkEscape = 0x1B;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private bool _armed;

    public event Action? Pressed;

    public EscapeCancel()
    {
        _source = new HwndSource(new HwndSourceParameters("BoloEscapeWindow") { Width = 0, Height = 0, WindowStyle = 0 });
        _source.AddHook(WndProc);
    }

    public void Arm()
    {
        if (_armed)
            return;
        _armed = RegisterHotKey(_source.Handle, HotkeyId, 0, VkEscape);
        if (!_armed)
            Bolo.Core.Storage.BoloLog.Write("Esc could not be claimed for cancel (another app owns it)");
    }

    public void Disarm()
    {
        if (!_armed)
            return;
        UnregisterHotKey(_source.Handle, HotkeyId);
        _armed = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            Pressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Disarm();
        _source.Dispose();
    }
}
