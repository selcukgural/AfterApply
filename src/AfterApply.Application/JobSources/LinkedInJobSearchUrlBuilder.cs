using System.Globalization;
using AfterApply.Domain.JobSources;

namespace AfterApply.Application.JobSources;

/// <summary>
/// The only place a LinkedIn search or posting URL is built. Host and path are constants; the
/// user's words go through <see cref="Uri.EscapeDataString"/> and nowhere else — so a profile
/// cannot steer the fetch anywhere but the two endpoints below (SSRF), and the parameter set is
/// exactly what was verified against the live site on 2026-09-12.
/// </summary>
public static class LinkedInJobSearchUrlBuilder
{
    public const string SearchBase = "https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search";
    public const string PostingBase = "https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/";

    /// <summary>Cards per page the endpoint returns; <c>start</c> steps by this.</summary>
    public const int PageSize = 10;

    public static Uri Search(string keywords, string location, JobSourceTimeWindow window, bool remoteOnly, int start)
    {
        var query = $"?keywords={Uri.EscapeDataString(keywords)}" +
                    $"&location={Uri.EscapeDataString(location)}" +
                    $"&f_TPR={window.ToLinkedInFilter()}" +
                    (remoteOnly ? "&f_WT=2" : string.Empty) +
                    $"&start={start.ToString(CultureInfo.InvariantCulture)}";
        return new Uri(SearchBase + query);
    }

    public static Uri Posting(string externalId)
    {
        if (externalId.Length == 0 || !externalId.All(char.IsAsciiDigit))
        {
            throw new ArgumentException("A LinkedIn posting id is numeric.", nameof(externalId));
        }

        return new Uri(PostingBase + externalId);
    }

    /// <summary>LinkedIn's <c>f_TPR</c> value: <c>r</c> + seconds.</summary>
    public static string ToLinkedInFilter(this JobSourceTimeWindow window) =>
        "r" + ((int)window * 86400).ToString(CultureInfo.InvariantCulture);
}
