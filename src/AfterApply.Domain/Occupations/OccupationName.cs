using System.Text.RegularExpressions;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.Occupations;

/// <summary>
/// The form both catalogue names are stored in for searching, and the form a typed query is put
/// into before it meets them: Turkish i-variants folded, whitespace collapsed, upper-cased. The
/// same fold <c>CompanySearchService</c> applies to a company query — nothing is stripped, this is
/// matching, not deduplication.
/// </summary>
public static partial class OccupationName
{
    public static string Normalize(string name)
    {
        var folded = TurkishTextNormalizer.FoldCase(name.Trim());
        return CollapseWhitespaceRegex().Replace(folded, " ").ToUpperInvariant();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex CollapseWhitespaceRegex();
}
