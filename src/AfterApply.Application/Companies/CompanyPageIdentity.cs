using System.Text.RegularExpressions;
using AfterApply.Domain.Companies;

namespace AfterApply.Application.Companies;

/// <summary>
/// Whether a fetched profile page is the company's own (2026-09-24). A profile URL arrives from one
/// user's capture and the company row is shared, so before anything on that page — website,
/// sector, country — is written onto the company, the name the page gives must be the company's
/// name.
///
/// Company names now arrive from any job site the extension reads (a posting's schema.org
/// <c>hiringOrganization</c> as often as LinkedIn's own card), so the same firm turns up as
/// "Trendyol", "TRENDYOL TEKNOLOJİ A.Ş." or "Trendyol | Careers". The comparison is on words:
/// both names go through <see cref="CompanyNameNormalizer"/>, punctuation becomes a word break, and
/// the names match when their words are equal or one's words are the first words of the other's.
/// That last case only counts when the shorter name is long enough to say something on its own —
/// a two-letter name has to match exactly.
/// </summary>
public static partial class CompanyPageIdentity
{
    private const int MinimumPrefixLength = 4;

    public static bool Matches(string companyName, string? pageName)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            return false;
        }

        var company = Words(companyName);
        var page = Words(pageName);
        if (company.Length == 0 || page.Length == 0)
        {
            return false;
        }

        var (shorter, longer) = company.Length <= page.Length ? (company, page) : (page, company);
        if (!longer.Take(shorter.Length).SequenceEqual(shorter))
        {
            return false;
        }

        return shorter.Length == longer.Length || string.Concat(shorter).Length >= MinimumPrefixLength;
    }

    private static string[] Words(string name) =>
        NonWordRegex().Split(CompanyNameNormalizer.Normalize(name))
            .Where(word => word.Length > 0)
            .ToArray();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex NonWordRegex();
}
