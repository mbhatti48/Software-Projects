using System.Text.Json;

namespace Bolo.Core.Learning;

public sealed class VocabCluster
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public List<string> Terms { get; set; } = [];
}

/// <summary>
/// Custom vocabulary grouped into switchable domain clusters (vocabulary.json).
/// Enabled clusters' terms are fed to Whisper as an initial prompt so decoding is
/// biased toward them — text-level biasing, no model retraining.
/// </summary>
public sealed class VocabularyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string FilePath => Path.Combine(Storage.BoloPaths.Root, "vocabulary.json");

    public List<VocabCluster> Clusters { get; private set; } = [];

    public static VocabularyStore Load()
    {
        var store = new VocabularyStore();
        if (File.Exists(FilePath))
        {
            try
            {
                store.Clusters = JsonSerializer.Deserialize<List<VocabCluster>>(
                    File.ReadAllText(FilePath), JsonOptions) ?? [];
            }
            catch (Exception ex)
            {
                // Unreadable vocabulary.json must not crash startup. Start empty and leave
                // the file untouched (no Save here) so nothing is lost to a transient failure.
                Storage.BoloLog.Write($"vocabulary load failed, starting empty (file left untouched): {ex.Message}");
                store.Clusters = [];
            }
        }
        else
        {
            // Minimal starter — onboarding seeds the user's own terms.
            store.Clusters =
            [
                new VocabCluster { Name = "General", Terms = ["Bolo"] },
            ];
            store.Save();
        }
        return store;
    }

    public void Save()
    {
        Storage.BoloPaths.EnsureDirectories();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Clusters, JsonOptions));
    }

    /// <summary>
    /// Whisper prompt built from all enabled clusters. Whisper's prompt window is
    /// limited (~224 tokens), so the term list is capped.
    /// </summary>
    public string BuildPrompt()
    {
        var terms = Clusters.Where(c => c.Enabled).SelectMany(c => c.Terms).Distinct().ToList();
        if (terms.Count == 0)
            return "";

        var prompt = "Glossary: ";
        const int maxChars = 700;
        var included = new List<string>();
        foreach (var term in terms)
        {
            if (prompt.Length + included.Sum(t => t.Length + 2) + term.Length > maxChars)
                break;
            included.Add(term);
        }
        return prompt + string.Join(", ", included) + ".";
    }
}
