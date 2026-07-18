using System.Text.RegularExpressions;

namespace Bolo.Core.Learning;

/// <summary>
/// Rule-based text transforms (the Style page) — deliberately NOT LLM-based, per the
/// project's offline constraint. Conservative by design: better to leave text alone
/// than mangle it.
/// </summary>
public static class Transforms
{
    private static readonly Regex FillerPattern = new(
        @"(?<=^|[\s])(um+|uh+|uhm+|erm+|er)([,.]?\s+|[,.]?$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ListMarkerPattern = new(
        @"\b(firstly|first|secondly|second|thirdly|third|fourthly|fourth|fifthly|fifth|finally|lastly)[,:]\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SpelledAcronymPattern = new(
        @"\b(?<first>[A-Za-z])(?:[\s\-\.]+(?<rest>[A-Za-z])){2,}(?=[\s\.,!?;:]|$)",
        RegexOptions.Compiled);

    /// <summary>
    /// Collapses spelled-out letter sequences ("I R C C" / "I-R-C-C") into acronyms
    /// ("IRCC") and reports what it found so the caller can auto-add them to the
    /// dictionary. Requires at least three letters to avoid false positives.
    /// </summary>
    public static (string Text, List<string> Acronyms) CollapseSpelledAcronyms(string text)
    {
        var found = new List<string>();
        var result = SpelledAcronymPattern.Replace(text, m =>
        {
            var acronym = (m.Groups["first"].Value +
                string.Concat(m.Groups["rest"].Captures.Select(c => c.Value))).ToUpperInvariant();
            found.Add(acronym);
            return acronym;
        });
        return (result, found);
    }

    /// <summary>Removes standalone filler words (um, uh, er…). Never touches real words.</summary>
    public static string RemoveFillers(string text)
    {
        var result = FillerPattern.Replace(text, "");
        // Clean up doubled spaces left behind.
        return Regex.Replace(result, @"  +", " ").Trim();
    }

    /// <summary>
    /// Converts a spoken enumeration ("First, X. Second, Y. Finally, Z.") into a
    /// bulleted list. Only fires when at least two markers are present.
    /// </summary>
    public static string SpokenListToBullets(string text)
    {
        var matches = ListMarkerPattern.Matches(text);
        if (matches.Count < 2)
            return text;

        var result = new System.Text.StringBuilder();
        var lastEnd = 0;
        foreach (Match m in matches)
        {
            if (m.Index > lastEnd)
            {
                var chunk = text[lastEnd..m.Index].Trim().TrimEnd('.');
                if (chunk.Length > 0)
                    result.AppendLine(lastEnd == 0 ? chunk : "- " + chunk);
            }
            lastEnd = m.Index + m.Length;
        }
        var tail = text[lastEnd..].Trim().TrimEnd('.');
        if (tail.Length > 0)
            result.AppendLine("- " + tail);
        return result.ToString().TrimEnd();
    }
}
