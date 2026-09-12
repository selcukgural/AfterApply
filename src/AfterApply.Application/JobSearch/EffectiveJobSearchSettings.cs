using AfterApply.Application.JobSearch.Contracts;
using AfterApply.Domain.JobSearch;

namespace AfterApply.Application.JobSearch;

/// <summary>The global values from configuration, in the shape the resolver and the settings
/// endpoint share. Built by Infrastructure from JobSearchOptions.</summary>
public sealed record JobSearchGlobalDefaults(
    string DefaultCountry,
    int MaxPagesPerSearch,
    int MaxJobIdsPerDetails,
    int PerUserDailyCredits);

/// <summary>
/// What applies to one user's job search once their overrides and the global defaults are
/// merged: field by field, the user's value when set, the global one otherwise.
///
/// <see cref="Language"/> has no global fallback by design. JSearch picks the country's primary
/// language when none is sent and returns nothing at all when the two disagree, so the only
/// language ever sent is one the user asked for — the title of a Turkish posting being in English
/// must not make it disappear.
/// </summary>
public sealed record EffectiveJobSearchSettings(
    string Country,
    string? Language,
    string? Location,
    JobSearchDatePosted DatePosted,
    bool WorkFromHome,
    int PerUserDailyCredits,
    int MaxPagesPerSearch,
    int MaxJobIdsPerDetails)
{
    public static EffectiveJobSearchSettings Resolve(JobSearchUserSettings? user, JobSearchGlobalDefaults global)
    {
        var country = Clean(user?.DefaultCountry) ?? Clean(global.DefaultCountry) ?? "tr";
        var datePosted = user?.DefaultDatePosted is { } stored
                         && Enum.TryParse<JobSearchDatePosted>(stored, ignoreCase: true, out var parsed)
            ? parsed
            : JobSearchDatePosted.All;

        return new EffectiveJobSearchSettings(
            country.ToLowerInvariant(),
            Clean(user?.DefaultLanguage)?.ToLowerInvariant(),
            Clean(user?.DefaultLocation),
            datePosted,
            user?.DefaultWorkFromHome ?? false,
            user?.PerUserDailyCredits ?? global.PerUserDailyCredits,
            user?.MaxPagesPerSearch ?? global.MaxPagesPerSearch,
            user?.MaxJobIdsPerDetails ?? global.MaxJobIdsPerDetails);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
