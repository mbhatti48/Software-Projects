namespace Bolo.Core.Storage;

/// <summary>Minimal diagnostic log (%APPDATA%\Bolo\bolo.log) — a tray app has no console.</summary>
public static class BoloLog
{
    private static readonly object Gate = new();
    private static string LogFile => Path.Combine(BoloPaths.Root, "bolo.log");

    // Keep the diagnostic log from growing unbounded: cap it and keep the recent tail.
    private const long MaxLogBytes = 1_000_000;
    private const int KeepTailChars = 200_000;

    public static void Write(string message)
    {
        lock (Gate)
        {
            BoloPaths.EnsureDirectories();
            File.AppendAllText(LogFile, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");

            var info = new FileInfo(LogFile);
            if (info.Length > MaxLogBytes)
            {
                var text = File.ReadAllText(LogFile);
                var tail = text[^Math.Min(KeepTailChars, text.Length)..];
                File.WriteAllText(LogFile, $"[log trimmed {DateTime.Now:yyyy-MM-dd HH:mm}]{Environment.NewLine}{tail}");
            }
        }
    }
}
