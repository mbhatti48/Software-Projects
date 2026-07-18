using Bolo.Core.Storage;

namespace Bolo.Core.Learning;

public sealed record DayStats(DateTime Date, int Transcripts, int Words, double AudioSeconds);

public sealed record AppUsage(string App, int Words, double Share);

public sealed record InsightsReport(
    int TotalTranscripts,
    int TotalWords,
    double AverageWpm,
    int DayStreak,
    int LongestStreak,
    int FixesApplied,
    string MostUsedWord,
    string MostCorrectedWord,
    string Catchphrase,
    string PeakTime,
    double TimeSavedMinutes,
    List<AppUsage> AppBreakdown,
    List<DayStats> Days,
    List<(string Wrong, string Right, int TimesApplied)> TopCorrections);

/// <summary>
/// Usage trends computed purely from data already on disk (history + corrections).
/// No new data collection beyond the local history — just views over the log.
/// </summary>
public static class Insights
{
    // Words too common to be an interesting "most used word".
    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "to", "of", "in", "on", "is", "it", "i",
        "that", "this", "for", "with", "you", "be", "are", "was", "so", "at", "as",
        "but", "have", "has", "not", "we", "my", "me", "do", "can", "if", "its", "it's",
    };

    /// <summary>Typing baseline used for the "time saved" estimate.</summary>
    private const double TypingWpm = 40;

    public static InsightsReport Compute(HistoryStore history, CorrectionStore corrections)
    {
        var allDays = history.ReadAllDays();
        var allEntries = allDays.SelectMany(d => d.Entries).ToList();

        var days = allDays
            .Select(d => new DayStats(
                d.Date,
                d.Entries.Count,
                d.Entries.Sum(e => CountWords(e.Text)),
                d.Entries.Sum(e => e.AudioSeconds)))
            .OrderBy(d => d.Date)
            .ToList();

        var totalWords = days.Sum(d => d.Words);
        var totalAudio = days.Sum(d => d.AudioSeconds);

        var (currentStreak, longestStreak) = Streaks(days);

        // Word frequencies (stopwords excluded so the result is interesting).
        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var trigramCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in allEntries)
        {
            var words = Tokenize(entry.Text);
            foreach (var word in words)
            {
                if (!Stopwords.Contains(word) && word.Length > 1)
                    wordCounts[word] = wordCounts.GetValueOrDefault(word) + 1;
            }
            for (var i = 0; i + 2 < words.Count; i++)
            {
                if (Stopwords.Contains(words[i]) && Stopwords.Contains(words[i + 2]))
                    continue; // trigrams of pure glue words aren't a catchphrase
                var trigram = $"{words[i]} {words[i + 1]} {words[i + 2]}";
                trigramCounts[trigram] = trigramCounts.GetValueOrDefault(trigram) + 1;
            }
        }
        var mostUsedWord = wordCounts.OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "—";
        var catchphrase = trigramCounts.Where(kv => kv.Value >= 3)
            .OrderByDescending(kv => kv.Value).FirstOrDefault().Key ?? "—";

        // Peak dictation slot: weekday + hour with the most words.
        var peak = allEntries
            .GroupBy(e => (e.Timestamp.LocalDateTime.DayOfWeek, e.Timestamp.LocalDateTime.Hour))
            .OrderByDescending(g => g.Sum(e => CountWords(e.Text)))
            .FirstOrDefault();
        var peakTime = peak is null
            ? "—"
            : $"{peak.Key.DayOfWeek} at {DateTime.Today.AddHours(peak.Key.Hour):h tt}".Replace("AM", "a.m.").Replace("PM", "p.m.");

        // Per-app breakdown (entries recorded before app tracking show as "unknown").
        var appGroups = allEntries
            .GroupBy(e => string.IsNullOrEmpty(e.TargetApp) ? "unknown" : e.TargetApp)
            .Select(g => (App: g.Key, Words: g.Sum(e => CountWords(e.Text))))
            .OrderByDescending(g => g.Words)
            .Take(6)
            .ToList();
        var appBreakdown = appGroups
            .Select(g => new AppUsage(g.App, g.Words, totalWords > 0 ? (double)g.Words / totalWords : 0))
            .ToList();

        var mostCorrected = corrections.Pairs.OrderByDescending(p => p.TimesApplied).FirstOrDefault();
        var timeSaved = Math.Max(0, totalWords / TypingWpm - totalAudio / 60.0);

        return new InsightsReport(
            days.Sum(d => d.Transcripts),
            totalWords,
            totalAudio > 0 ? totalWords / (totalAudio / 60.0) : 0,
            currentStreak,
            longestStreak,
            corrections.Pairs.Sum(p => p.TimesApplied),
            mostUsedWord,
            mostCorrected is null || mostCorrected.TimesApplied == 0 ? "—" : mostCorrected.Wrong,
            catchphrase,
            peakTime,
            timeSaved,
            appBreakdown,
            days,
            corrections.Pairs.Where(p => p.TimesApplied > 0)
                .OrderByDescending(p => p.TimesApplied)
                .Take(10)
                .Select(p => (p.Wrong, p.Right, p.TimesApplied))
                .ToList());
    }

    private static (int Current, int Longest) Streaks(List<DayStats> days)
    {
        var dates = days.Where(d => d.Transcripts > 0).Select(d => d.Date).ToHashSet();
        var current = 0;
        var cursor = dates.Contains(DateTime.Today) ? DateTime.Today : DateTime.Today.AddDays(-1);
        while (dates.Contains(cursor))
        {
            current++;
            cursor = cursor.AddDays(-1);
        }

        var longest = 0;
        foreach (var date in dates)
        {
            if (dates.Contains(date.AddDays(-1)))
                continue; // not a streak start
            var length = 1;
            while (dates.Contains(date.AddDays(length)))
                length++;
            longest = Math.Max(longest, length);
        }
        return (current, longest);
    }

    private static List<string> Tokenize(string text) => text
        .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
        .Select(w => w.Trim('.', ',', '!', '?', ';', ':', '"', '(', ')', '‘', '’', '“', '”').ToLowerInvariant())
        .Where(w => w.Length > 0)
        .ToList();

    private static int CountWords(string text) =>
        text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
}
