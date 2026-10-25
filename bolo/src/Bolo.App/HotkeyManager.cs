using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Bolo.App;

/// <summary>
/// System-wide hotkey with two engines:
/// - Combos with a regular key ("F9", "Ctrl+Shift+Space") use RegisterHotKey.
/// - Modifier-only combos ("Ctrl+Shift") use a low-level keyboard hook, since
///   RegisterHotKey cannot express them. Activation waits ~200ms so ordinary
///   shortcuts like Ctrl+Shift+S in other apps don't trigger dictation.
/// </summary>
public sealed class HotkeyManager : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int HotkeyId = 0xB010;
    private const int ModifierOnlyDelayMs = 120;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, IntPtr dwExtraInfo);

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    /// <summary>
    /// Injects a harmless dummy keystroke (VK 0xFF) while the Win key is held. Windows
    /// only opens the Start menu if Win is released with no other key pressed in
    /// between — this cancels that, letting Win participate in hotkey combos.
    /// (Same masking technique AutoHotkey and PowerToys use.)
    /// </summary>
    public static void MaskWinKey()
    {
        keybd_event(0xFF, 0, 0, IntPtr.Zero);
        keybd_event(0xFF, 0, 2 /* KEYEVENTF_KEYUP */, IntPtr.Zero);
    }

    public event Action? Pressed;
    /// <summary>Modifier-only combo just went down (not yet confirmed) — start capturing audio.</summary>
    public event Action? ComboPending;
    /// <summary>The pending combo turned out to be an app shortcut / early release — discard.</summary>
    public event Action? ComboCancelled;

    // RegisterHotKey path
    private HwndSource? _source;
    private uint _mainKeyVk;

    // Hook path (modifier-only)
    private IntPtr _hookHandle;
    private LowLevelKeyboardProc? _hookProc; // field keeps the delegate alive for the native hook
    private int[] _modifierVks = [];
    private bool _comboActive;
    private CancellationTokenSource? _pendingActivation;

    private bool IsModifierOnly => _mainKeyVk == 0;

    /// <summary>True while the bound combo is physically held (hold-to-talk release polling).</summary>
    public bool IsMainKeyDown => IsModifierOnly
        ? AllModifiersHeld()
        : (GetAsyncKeyState((int)_mainKeyVk) & 0x8000) != 0;

    public HotkeyManager(string hotkey)
    {
        var (modifiers, modifierVks, key) = Parse(hotkey);

        if (key != 0)
        {
            _mainKeyVk = key;
            _source = new HwndSource(new HwndSourceParameters("BoloHotkeyWindow")
            {
                Width = 0, Height = 0,
                WindowStyle = 0,
            });
            _source.AddHook(WndProc);
            if (!RegisterHotKey(_source.Handle, HotkeyId, modifiers, key))
                throw new InvalidOperationException(
                    $"Could not register global hotkey '{hotkey}' — another app may already be using it.");
        }
        else if (modifierVks.Length >= 2)
        {
            _modifierVks = modifierVks;
            _hookProc = HookProc;
            _hookHandle = SetWindowsHookEx(13 /* WH_KEYBOARD_LL */, _hookProc, GetModuleHandle(null), 0);
            if (_hookHandle == IntPtr.Zero)
                throw new InvalidOperationException($"Could not install keyboard hook for '{hotkey}'.");
        }
        else
        {
            throw new InvalidOperationException(
                "A hotkey needs either a regular key (like F9 or Ctrl+Shift+Space) or at least two modifiers together (like Ctrl+Shift).");
        }
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

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            var vk = Marshal.ReadInt32(lParam); // first field of KBDLLHOOKSTRUCT
            var isDown = msg is 0x0100 or 0x0104; // WM_KEYDOWN / WM_SYSKEYDOWN
            var isUp = msg is 0x0101 or 0x0105;   // WM_KEYUP / WM_SYSKEYUP
            var isTargetModifier = IsTargetModifier(vk);

            if (isDown)
            {
                // GetAsyncKeyState does NOT yet report the key being processed by this
                // hook callback as down — assume it is, or the combo can never activate.
                if (isTargetModifier && !_comboActive && _pendingActivation is null
                    && AllModifiersHeld(assumeDownVk: NormalizeModifier(vk)))
                {
                    ComboPending?.Invoke();
                    BeginPendingActivation();
                }
                else if (!isTargetModifier && _pendingActivation is { } pending)
                {
                    // A regular key while modifiers are held = an app shortcut, not our combo.
                    // (Local copy: the activation task clears the field from another thread.)
                    pending.Cancel();
                    _pendingActivation = null;
                    ComboCancelled?.Invoke();
                }
            }
            else if (isUp && isTargetModifier)
            {
                if (_pendingActivation is { } pending)
                {
                    pending.Cancel();
                    _pendingActivation = null;
                    ComboCancelled?.Invoke();
                }
                _comboActive = false;
            }
        }
        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private void BeginPendingActivation()
    {
        var cts = new CancellationTokenSource();
        _pendingActivation = cts;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(ModifierOnlyDelayMs, cts.Token);
                if (!cts.IsCancellationRequested && AllModifiersHeld())
                {
                    _comboActive = true;
                    _pendingActivation = null;
                    if (_modifierVks.Contains(0x5B))
                        MaskWinKey(); // keep the Start menu from opening on Win release
                    Pressed?.Invoke();
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    /// <summary>Maps left/right variant VKs to the generic modifier VK, 0 if not a modifier.</summary>
    private static int NormalizeModifier(int vk) => vk switch
    {
        0xA0 or 0xA1 or 0x10 => 0x10, // shift
        0xA2 or 0xA3 or 0x11 => 0x11, // ctrl
        0xA4 or 0xA5 or 0x12 => 0x12, // alt
        0x5B or 0x5C => 0x5B,         // win
        _ => 0,
    };

    private bool IsTargetModifier(int vk)
    {
        var normalized = NormalizeModifier(vk);
        return normalized != 0 && _modifierVks.Contains(normalized);
    }

    private bool AllModifiersHeld(int assumeDownVk = 0) => _modifierVks.All(vk =>
        vk == assumeDownVk
        || (vk == 0x5B
            ? ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0)
            : (GetAsyncKeyState(vk) & 0x8000) != 0));

    private static (uint modifiers, int[] modifierVks, uint key) Parse(string hotkey)
    {
        uint modifiers = 0;
        var modifierVks = new List<int>();
        var key = Key.None;
        foreach (var part in hotkey.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control": modifiers |= 0x0002; modifierVks.Add(0x11); break;
                case "alt": modifiers |= 0x0001; modifierVks.Add(0x12); break;
                case "shift": modifiers |= 0x0004; modifierVks.Add(0x10); break;
                case "win": modifiers |= 0x0008; modifierVks.Add(0x5B); break;
                default:
                    key = (Key)Enum.Parse(typeof(Key), part, ignoreCase: true);
                    break;
            }
        }
        return (modifiers, modifierVks.ToArray(),
            key == Key.None ? 0u : (uint)KeyInterop.VirtualKeyFromKey(key));
    }

    /// <summary>
    /// Physically-held modifiers straight from the OS. WPF's Keyboard.Modifiers does
    /// not report the Windows key, so the capture box must use this instead.
    /// </summary>
    public static List<string> PhysicalModifiersHeld()
    {
        var parts = new List<string>();
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0) parts.Add("Ctrl");
        if ((GetAsyncKeyState(0x12) & 0x8000) != 0) parts.Add("Alt");
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0) parts.Add("Shift");
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0) parts.Add("Win");
        return parts;
    }

    /// <summary>Whether a hotkey string is bindable at all (used by the Settings capture box).</summary>
    public static bool IsValidCombo(string hotkey)
    {
        try
        {
            var (_, modifierVks, key) = Parse(hotkey);
            return key != 0 || modifierVks.Length >= 2;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _pendingActivation?.Cancel();
        if (_source is not null)
        {
            UnregisterHotKey(_source.Handle, HotkeyId);
            _source.Dispose();
            _source = null;
        }
        if (_hookHandle != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }
    }
}
