using System.Text.Json;

namespace Bolo.Core.Learning;

public sealed class Snippet
{
    public string Trigger { get; set; } = "";
    public string Expansion { get; set; } = "";
}

/// <summary>
/// Trigger-phrase → expansion text (snippets.json), e.g. "my email" → the real address.
/// Reuses the correction substitution engine — not a separate subsystem.
/// </summary>
public sealed class SnippetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static string FilePath => Path.Combine(Storage.BoloPaths.Root, "snippets.json");

    public List<Snippet> Snippets { get; private set; } = [];

    public static SnippetStore Load()
    {
        var store = new SnippetStore();
        if (File.Exists(FilePath))
            store.Snippets = JsonSerializer.Deserialize<List<Snippet>>(
                File.ReadAllText(FilePath), JsonOptions) ?? [];
        else
            store.Save(); // create the empty file so it's discoverable/hand-editable
        return store;
    }

    public void Save()
    {
        Storage.BoloPaths.EnsureDirectories();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Snippets, JsonOptions));
    }

    public string Apply(string text)
    {
        foreach (var snippet in Snippets)
            text = SubstitutionEngine.Replace(text, snippet.Trigger, snippet.Expansion);
        return text;
    }
}
