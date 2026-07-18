using System.Text.Json;

namespace Bolo.Core.Storage;

public sealed record TranscriptEntry(
    DateTimeOffset Timestamp,
    string Text,
    double AudioSeconds,
    double TranscribeSeconds,
    string Runtime,
    string TargetApp = "");

/// <summary>
/// Transcript history as one JSON file per day (history/YYYY-MM-DD.json), plain and readable.
/// Only text is ever stored — never audio.
/// </summary>
public sealed class HistoryStore
{
    // Compact on purpose: history is machine-read data (Insights, AI review sessions)
    // and compact JSON is roughly half the size of pretty-printed. Older indented
    // files still deserialize fine.
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public void Append(TranscriptEntry entry)
    {
        BoloPaths.EnsureDirectories();
        var file = FileForDate(entry.Timestamp.LocalDateTime.Date);
        var entries = ReadFile(file);
        entries.Add(entry);
        File.WriteAllText(file, JsonSerializer.Serialize(entries, JsonOptions));
    }

    public TranscriptEntry? GetLatest()
    {
        if (!Directory.Exists(BoloPaths.HistoryDir))
            return null;
        var newestFile = Directory.GetFiles(BoloPaths.HistoryDir, "*.json")
            .OrderDescending()
            .FirstOrDefault();
        return newestFile is null ? null : ReadFile(newestFile).LastOrDefault();
    }

    public sealed record DayEntries(DateTime Date, List<TranscriptEntry> Entries);

    /// <summary>All history, newest day first — used by the History page and Insights.</summary>
    public List<DayEntries> ReadAllDays()
    {
        if (!Directory.Exists(BoloPaths.HistoryDir))
            return [];
        return Directory.GetFiles(BoloPaths.HistoryDir, "*.json")
            .OrderDescending()
            .Select(f => new DayEntries(
                DateTime.TryParse(Path.GetFileNameWithoutExtension(f), out var d) ? d : DateTime.MinValue,
                ReadFile(f)))
            .Where(d => d.Date != DateTime.MinValue)
            .ToList();
    }

    /// <summary>Replaces the text of a specific entry, located by its timestamp.</summary>
    public void UpdateEntryText(DateTimeOffset timestamp, string correctedText)
    {
        var file = FileForDate(timestamp.LocalDateTime.Date);
        if (!File.Exists(file))
            return;
        var entries = ReadFile(file);
        var index = entries.FindIndex(e => e.Timestamp == timestamp);
        if (index < 0)
            return;
        entries[index] = entries[index] with { Text = correctedText };
        File.WriteAllText(file, JsonSerializer.Serialize(entries, JsonOptions));
    }

    /// <summary>Replaces the text of the most recent entry (after a user correction).</summary>
    public void UpdateLatestText(string correctedText)
    {
        var newestFile = Directory.Exists(BoloPaths.HistoryDir)
            ? Directory.GetFiles(BoloPaths.HistoryDir, "*.json").OrderDescending().FirstOrDefault()
            : null;
        if (newestFile is null)
            return;
        var entries = ReadFile(newestFile);
        if (entries.Count == 0)
            return;
        entries[^1] = entries[^1] with { Text = correctedText };
        File.WriteAllText(newestFile, JsonSerializer.Serialize(entries, JsonOptions));
    }

    private static string FileForDate(DateTime date) =>
        Path.Combine(BoloPaths.HistoryDir, $"{date:yyyy-MM-dd}.json");

    private static List<TranscriptEntry> ReadFile(string file)
    {
        if (!File.Exists(file))
            return [];
        try
        {
            var json = File.ReadAllText(file);
            return JsonSerializer.Deserialize<List<TranscriptEntry>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            // A single unreadable day-file must not crash History/Insights or blank every
            // stat — skip it and carry on with the rest of the history.
            BoloLog.Write($"history file unreadable, skipping {Path.GetFileName(file)}: {ex.Message}");
            return [];
        }
    }
}
