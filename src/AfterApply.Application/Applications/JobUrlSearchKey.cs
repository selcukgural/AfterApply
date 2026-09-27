using System.Text.RegularExpressions;

namespace AfterApply.Application.Applications;

/// <summary>
/// Turns a job-posting address pasted into the applications search box into the part of it that
/// identifies the posting, so "paste the link, find the application" works even though the copied
/// address rarely matches the stored one character for character: the address bar adds tracking
/// parameters, a trailing slash comes and goes, "www." may or may not be there, and LinkedIn shows
/// the same job as <c>/jobs/view/&lt;id&gt;</c> or as <c>?currentJobId=&lt;id&gt;</c> on a search page.
/// </summary>
/// <remarks>
/// The key is matched as a substring of the stored URL (query and fragment dropped on this side
/// only), always inside the caller's own applications. Anything that does not read as an http(s)
/// address yields no key and the search stays the ordinary title/company text search.
/// </remarks>
public static partial class JobUrlSearchKey
{
    public static bool TryCreate(string? search, out string key)
    {
        key = string.Empty;
        var text = search?.Trim();
        if (string.IsNullOrEmpty(text) || text.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            // "linkedin.com/jobs/view/123" copied without its scheme still reads as an address.
            if (!text.Contains('/') || !Uri.TryCreate("https://" + text, UriKind.Absolute, out uri) || !uri.Host.Contains('.'))
            {
                return false;
            }
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        if (host == "linkedin.com" || host.EndsWith(".linkedin.com", StringComparison.Ordinal))
        {
            var jobId = LinkedInJobId(uri);
            if (jobId is not null)
            {
                key = $"linkedin.com/jobs/view/{jobId}";
                return true;
            }
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        key = host + path;
        return true;
    }

    private static string? LinkedInJobId(Uri uri)
    {
        var view = LinkedInViewPath().Match(uri.AbsolutePath);
        if (view.Success)
        {
            return view.Groups[1].Value;
        }

        var current = LinkedInCurrentJobId().Match(uri.Query);
        return current.Success ? current.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^/jobs/view/(?:[^/]*-)?(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkedInViewPath();

    [GeneratedRegex(@"[?&]currentJobId=(\d+)(?:&|$)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkedInCurrentJobId();
}
