using System.Text.RegularExpressions;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.Companies;

public static partial class CompanyNameNormalizer
{
    private static readonly string[] LegalSuffixes =
    [
        "a.ş.", "as", "anonim şirketi",
        "ltd. şti.", "ltd.şti.", "limited şirketi", "ltd",
        "inc.", "inc", "llc", "corp.", "corp", "co.", "co", "gmbh"
    ];

    public static string Normalize(string name)
    {
        var normalized = TurkishTextNormalizer.FoldCase(name.Trim());
        normalized = CollapseWhitespaceRegex().Replace(normalized, " ");

        foreach (var suffix in LegalSuffixes)
        {
            if (normalized.EndsWith(" " + suffix, StringComparison.Ordinal))
            {
                normalized = normalized[..^(suffix.Length + 1)].TrimEnd();
            }
        }

        normalized = TrailingPunctuationRegex().Replace(normalized, "");

        return normalized.ToUpperInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();

    [GeneratedRegex(@"[.,\s]+$")]
    private static partial Regex TrailingPunctuationRegex();
}
