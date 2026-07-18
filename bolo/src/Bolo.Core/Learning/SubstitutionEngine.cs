using System.Text.RegularExpressions;

namespace Bolo.Core.Learning;

/// <summary>
/// Shared text-substitution logic used by both corrections and snippets, plus the
/// word-diff that harvests correction pairs from a transcript edit.
/// </summary>
public static class SubstitutionEngine
{
    /// <summary>Case-insensitive whole-word replacement.</summary>
    public static string Replace(string text, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(from))
            return text;
        var pattern = $@"\b{Regex.Escape(from)}\b";
        // '$' is special in regex replacements ("$0" would inject the matched trigger) —
        // expansions like "$100" must be treated literally.
        return Regex.Replace(text, pattern, to.Replace("$", "$$"), RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Word-level diff between the original transcript and the user's edited version.
    /// Returns (wrong, right) pairs for each contiguous replaced run. Pure insertions
    /// and deletions are ignored — they're edits of intent, not mishearings.
    /// </summary>
    public static List<(string Wrong, string Right)> ExtractCorrections(string original, string edited)
    {
        var a = Tokenize(original);
        var b = Tokenize(edited);

        // Standard LCS table over words (case-insensitive match).
        var lcs = new int[a.Count + 1, b.Count + 1];
        for (var i = a.Count - 1; i >= 0; i--)
            for (var j = b.Count - 1; j >= 0; j--)
                lcs[i, j] = string.Equals(a[i], b[j], StringComparison.OrdinalIgnoreCase)
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var pairs = new List<(string, string)>();
        var wrongRun = new List<string>();
        var rightRun = new List<string>();
        int x = 0, y = 0;

        void FlushRun()
        {
            if (wrongRun.Count > 0 && rightRun.Count > 0)
                pairs.Add((string.Join(' ', wrongRun), string.Join(' ', rightRun)));
            wrongRun.Clear();
            rightRun.Clear();
        }

        while (x < a.Count && y < b.Count)
        {
            if (string.Equals(a[x], b[y], StringComparison.OrdinalIgnoreCase))
            {
                FlushRun();
                x++;
                y++;
            }
            else if (lcs[x + 1, y] >= lcs[x, y + 1])
            {
                wrongRun.Add(a[x]);
                x++;
            }
            else
            {
                rightRun.Add(b[y]);
                y++;
            }
        }
        wrongRun.AddRange(a.Skip(x));
        rightRun.AddRange(b.Skip(y));
        FlushRun();

        return pairs;
    }

    private static List<string> Tokenize(string text) =>
        text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).ToList();
}
