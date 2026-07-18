using System.Text.Json;

namespace Bolo.Core.Learning;

public sealed class CorrectionPair
{
    public string Wrong { get; set; } = "";
    public string Right { get; set; } = "";
    public int TimesApplied { get; set; }
    public int TimesReinforced { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset LastUsed { get; set; } = DateTimeOffset.Now;
}

/// <summary>
/// The correction feedback loop (corrections.json): (wrong → right) pairs harvested
/// from transcript edits, auto-applied to future transcripts. Plain JSON so the
/// periodic Claude review session can read and prune it. Decay of stale pairs is
/// deliberately left to that review — no automatic deletion.
/// </summary>
public sealed class CorrectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string FilePath => Path.Combine(Storage.BoloPaths.Root, "corrections.json");

    public List<CorrectionPair> Pairs { get; private set; } = [];

    public static CorrectionStore Load()
    {
        var store = new CorrectionStore();
        if (File.Exists(FilePath))
            store.Pairs = JsonSerializer.Deserialize<List<CorrectionPair>>(
                File.ReadAllText(FilePath), JsonOptions) ?? [];
        return store;
    }

    public void Save()
    {
        Storage.BoloPaths.EnsureDirectories();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Pairs, JsonOptions));
    }

    /// <summary>Adds a new pair, or reinforces the existing one if already known.</summary>
    public void AddOrReinforce(string wrong, string right)
    {
        var existing = Pairs.FirstOrDefault(p =>
            string.Equals(p.Wrong, wrong, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(p.Right, right, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.TimesReinforced++;
            existing.LastUsed = DateTimeOffset.Now;
        }
        else
        {
            Pairs.Add(new CorrectionPair { Wrong = wrong, Right = right });
        }
    }

    /// <summary>Applies all known corrections to a transcript, tracking usage.</summary>
    public string Apply(string text)
    {
        var applied = false;
        foreach (var pair in Pairs)
        {
            var result = SubstitutionEngine.Replace(text, pair.Wrong, pair.Right);
            if (!ReferenceEquals(result, text) && result != text)
            {
                pair.TimesApplied++;
                pair.LastUsed = DateTimeOffset.Now;
                applied = true;
            }
            text = result;
        }
        if (applied)
            Save();
        return text;
    }
}
