using System.Text;
using System.Text.RegularExpressions;
using AfterApply.Domain.Common;

namespace AfterApply.Domain.Blog;

/// <summary>
/// Turns a post title into the URL segment the post lives under (<c>/{lang}/blog/{slug}</c>).
/// Pure: uniqueness within a language is the caller's job, which appends <see cref="WithSuffix"/>
/// until a free one turns up.
///
/// A copy of <c>CompanySlugGenerator</c>'s fold table rather than a reference to it, so the blog
/// module stays free of the companies module (ModuleIsolationTests). A unit test pins the two
/// to the same outputs for the letters that matter, so a fix to one is a fix to both.
/// </summary>
public static partial class BlogSlugGenerator
{
    public const int MaxLength = 100;

    /// <summary>What a title with no usable character at all gets.</summary>
    public const string Fallback = "post";

    /// <summary>
    /// Segments that must never be a post's slug: the ones the API and the web app mount under
    /// <c>/blog</c> today or are likely to tomorrow. A post whose title slugs to one of these gets
    /// a suffix instead, and a hand-typed one is refused by <see cref="IsValid"/>.
    /// </summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.Ordinal)
    {
        "public", "new", "media", "admin", "feed", "rss", "page"
    };

    public static string Generate(string title)
    {
        var folded = TurkishTextNormalizer.FoldCase(title.Trim());

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

    /// <summary>Whether a slug the author typed by hand is one <see cref="Generate"/> could have
    /// produced: lowercase ASCII letters, digits and single dashes, within the length cap, and
    /// not a reserved segment.</summary>
    public static bool IsValid(string? slug) =>
        !string.IsNullOrEmpty(slug)
        && slug.Length <= MaxLength
        && ValidSlugRegex().IsMatch(slug)
        && !Reserved.Contains(slug);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumericRegex();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex ValidSlugRegex();
}
