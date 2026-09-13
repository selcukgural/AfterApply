using System.Text;
using System.Text.RegularExpressions;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.Companies;

/// <summary>
/// Turns a company name into the URL segment its public page lives under
/// (<c>/companies/{slug}</c>). Pure: uniqueness against the table is the caller's job
/// (<c>CompanyResolver</c>), which appends <see cref="WithSuffix"/> until a free one turns up.
///
/// The Turkish fold table here is mirrored by the SQL backfill in the <c>AddCompanySlug</c>
/// migration; a unit test pins both to the same inputs so the two never diverge on the letters
/// that matter for this product. Other diacritics (é, ß) fall out as "-" on both sides, which is
/// cosmetic — slugs are stable once assigned, so a later improvement never rewrites a URL.
/// </summary>
public static partial class CompanySlugGenerator
{
    public const int MaxLength = 80;

    /// <summary>What a company whose name has no usable character at all gets — "!!!" happens.</summary>
    public const string Fallback = "company";

    /// <summary>
    /// The static segments the web app mounts under <c>/companies</c>. A company that would slug
    /// to one of these gets a suffix instead, so the router never has to pick between a page and
    /// a company.
    /// </summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "scoring", "search", "public", "new"
    };

    public static string Generate(string name)
    {
        var folded = TurkishTextNormalizer.FoldCase(name.Trim());

        var builder = new StringBuilder(folded.Length);
        foreach (var ch in folded)
        {
            builder.Append(ch switch
            {
                'ç' => 'c',
                'ğ' => 'g',
                'ö' => 'o',
                'ş' => 's',
                'ü' => 'u',
                'â' => 'a',
                'î' => 'i',
                'û' => 'u',
                _ => ch
            });
        }

        var slug = NonAlphanumericRegex().Replace(builder.ToString(), "-").Trim('-');
        if (slug.Length > MaxLength)
        {
            slug = slug[..MaxLength].TrimEnd('-');
        }

        if (slug.Length == 0)
        {
            slug = Fallback;
        }

        return Reserved.Contains(slug) ? WithSuffix(slug, 2) : slug;
    }

    public static string WithSuffix(string baseSlug, int n) => $"{baseSlug}-{n}";

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();
}
