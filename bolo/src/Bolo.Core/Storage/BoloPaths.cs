namespace Bolo.Core.Storage;

/// <summary>
/// All Bolo data lives under %APPDATA%\Bolo as human-readable JSON — deliberately outside
/// the Obsidian vault, and structured so a future Claude session can read it directly.
/// </summary>
public static class BoloPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Bolo");

    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string HistoryDir => Path.Combine(Root, "history");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(HistoryDir);
    }
}
