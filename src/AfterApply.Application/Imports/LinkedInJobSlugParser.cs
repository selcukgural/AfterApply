using System.Text.RegularExpressions;

namespace AfterApply.Application.Imports;

/// <summary>
/// LinkedIn's canonical job URL encodes the position and company in the slug itself
/// (".../jobs/view/{title-slug}-at-{company-slug}-{numericJobId}") — the short
/// "/jobs/view/{id}/" link a user copies 301-redirects here. Parsing just this redirect target
/// string (see IJobLinkPreviewService) resolves a job-link preview without ever downloading or
/// rendering the page body, which LinkedIn shows a login-wall overlay over for logged-out
/// visitors. Best-effort and lossy by construction — hyphens can't reconstruct original
/// casing/punctuation (e.g. ".NET" comes back as "Net"), and a title genuinely containing the
/// word "at" can split wrong — callers must treat the result as an editable suggestion, never as
/// ground truth. Pure function — no I/O.
/// </summary>
public static partial class LinkedInJobSlugParser
{
    public static (string? Title, string? Company) Parse(string? canonicalJobUrl)
    {
        if (string.IsNullOrWhiteSpace(canonicalJobUrl)
            || !Uri.TryCreate(canonicalJobUrl, UriKind.Absolute, out var uri))
        {
            return (null, null);
        }

        var lastSegment = uri.Segments.Length > 0 ? uri.Segments[^1].TrimEnd('/') : null;
        if (string.IsNullOrEmpty(lastSegment))
        {
            return (null, null);
        }

        var withoutId = TrailingNumericIdRegex().Replace(lastSegment, string.Empty);
        if (withoutId.Length == lastSegment.Length)
        {
            // No numeric id suffix to strip => this isn't a slugified canonical URL (e.g. still
            // the bare "/jobs/view/{id}" form) — nothing to parse.
            return (null, null);
        }

        var atIndex = withoutId.LastIndexOf("-at-", StringComparison.OrdinalIgnoreCase);
        if (atIndex <= 0 || atIndex + 4 >= withoutId.Length)
        {
            return (null, null);
        }

        var title = ToTitleCase(withoutId[..atIndex]);
        var company = ToTitleCase(withoutId[(atIndex + 4)..]);

        return string.IsNullOrEmpty(title) || string.IsNullOrEmpty(company)
            ? (null, null)
            : (title, company);
    }

    private static string ToTitleCase(string slug)
    {
        var words = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    [GeneratedRegex(@"-\d+$")]
    private static partial Regex TrailingNumericIdRegex();
}
