using System.Runtime.InteropServices;
using System.Windows;
using Bolo.Core.Storage;

namespace Bolo.App;

/// <summary>
/// Types text into whatever window currently has focus, either as simulated Unicode
/// keystrokes (SendInput) or as a clipboard swap + Ctrl+V with clipboard restore.
/// </summary>
public static class TextInjector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public KeyboardInput Ki;
        // KEYBDINPUT is the largest member of the INPUT union on x64 except MOUSEINPUT;
        // pad to the full union size.
        private readonly long _padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private const uint InputKeyboard = 1;
    private const uint KeyEventFUnicode = 0x0004;
    private const uint KeyEventFKeyUp = 0x0002;
    private const ushort VkReturn = 0x0D;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    public static void Inject(string text, InjectionMode mode)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (mode == InjectionMode.Paste)
            InjectViaPaste(text);
        else
            InjectViaKeystrokes(text);
    }

    private static void InjectViaKeystrokes(string text)
    {
        var inputs = new List<Input>(text.Length * 2);
        foreach (var ch in text)
        {
            if (ch == '\r')
                continue;
            if (ch == '\n')
            {
                inputs.Add(KeyDown(VkReturn));
                inputs.Add(KeyUp(VkReturn));
                continue;
            }
            inputs.Add(UnicodeDown(ch));
            inputs.Add(UnicodeUp(ch));
        }
        // Send in chunks — very large single SendInput calls can be dropped by some apps.
        const int chunkSize = 512;
        for (var i = 0; i < inputs.Count; i += chunkSize)
        {
            var chunk = inputs.Skip(i).Take(chunkSize).ToArray();
            SendInput((uint)chunk.Length, chunk, Marshal.SizeOf<Input>());
        }
    }

    private static void InjectViaPaste(string text)
    {
        string? previousClipboard = null;
        try
        {
            if (Clipboard.ContainsText())
                previousClipboard = Clipboard.GetText();
        }
        catch (COMException) { /* clipboard busy — proceed without restore */ }

        Clipboard.SetText(text);
        Input[] paste =
        [
            KeyDown(VkControl), KeyDown(VkV), KeyUp(VkV), KeyUp(VkControl),
        ];
        SendInput((uint)paste.Length, paste, Marshal.SizeOf<Input>());

        if (previousClipboard is not null)
        {
            // Give the target app a moment to read the clipboard before restoring it.
            var restore = previousClipboard;
            Task.Delay(500).ContinueWith(_ =>
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    try { Clipboard.SetText(restore); }
                    catch (COMException) { /* clipboard busy — skip restore */ }
                }));
        }
    }

    private static Input UnicodeDown(char ch) => new()
    {
        Type = InputKeyboard,
        Ki = new KeyboardInput { Scan = ch, Flags = KeyEventFUnicode },
    };

    private static Input UnicodeUp(char ch) => new()
    {
        Type = InputKeyboard,
        Ki = new KeyboardInput { Scan = ch, Flags = KeyEventFUnicode | KeyEventFKeyUp },
    };

    private static Input KeyDown(ushort vk) => new()
    {
        Type = InputKeyboard,
        Ki = new KeyboardInput { Vk = vk },
    };

    private static Input KeyUp(ushort vk) => new()
    {
        Type = InputKeyboard,
        Ki = new KeyboardInput { Vk = vk, Flags = KeyEventFKeyUp },
    };
}
