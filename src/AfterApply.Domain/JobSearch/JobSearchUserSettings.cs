using AfterApply.Domain.Common;

namespace AfterApply.Domain.JobSearch;

/// <summary>
/// Per-user overrides for job search. Every column is nullable and null means "use the global
/// value from configuration" — the row only exists while at least one override is set.
///
/// Two groups with two different owners. The <i>preferences</i> (default country, language,
/// location, date filter, remote-only) are the user's own and editable from their settings. The
/// <i>limits</i> (daily credits, pages per search, ids per details call) are the admin's: the
/// daily credit ceiling is what keeps one account from spending the shared monthly quota, so the
/// account it protects against cannot be the one raising it. The two update paths take different
/// request types and touch only their own columns.
/// </summary>
public sealed class JobSearchUserSettings : Entity
{
    public Guid UserId { get; private set; }

    public string? DefaultCountry { get; private set; }

    public string? DefaultLanguage { get; private set; }

    public string? DefaultLocation { get; private set; }

    /// <summary>A <c>JobSearchDatePosted</c> member name; kept as text here so the Domain does
    /// not depend on the Application-layer enum.</summary>
    public string? DefaultDatePosted { get; private set; }

    public bool? DefaultWorkFromHome { get; private set; }

    public int? PerUserDailyCredits { get; private set; }

    public int? MaxPagesPerSearch { get; private set; }

    public int? MaxJobIdsPerDetails { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private JobSearchUserSettings()
    {
    }

    public static JobSearchUserSettings CreateEmpty(Guid userId, DateTimeOffset now) =>
        new() { UserId = userId, UpdatedAt = now };

    public void SetPreferences(string? defaultCountry, string? defaultLanguage, string? defaultLocation,
        string? defaultDatePosted, bool? defaultWorkFromHome, DateTimeOffset now)
    {
        DefaultCountry = Clean(defaultCountry)?.ToLowerInvariant();
        DefaultLanguage = Clean(defaultLanguage)?.ToLowerInvariant();
        DefaultLocation = Clean(defaultLocation);
        DefaultDatePosted = Clean(defaultDatePosted);
        DefaultWorkFromHome = defaultWorkFromHome;
        UpdatedAt = now;
    }

    public void SetLimits(int? perUserDailyCredits, int? maxPagesPerSearch, int? maxJobIdsPerDetails, DateTimeOffset now)
    {
        PerUserDailyCredits = perUserDailyCredits;
        MaxPagesPerSearch = maxPagesPerSearch;
        MaxJobIdsPerDetails = maxJobIdsPerDetails;
        UpdatedAt = now;
    }

    /// <summary>True once every override is null — the row has nothing left to say and is removed
    /// rather than kept as an all-null placeholder.</summary>
    public bool IsEmpty =>
        DefaultCountry is null && DefaultLanguage is null && DefaultLocation is null && DefaultDatePosted is null
        && DefaultWorkFromHome is null && PerUserDailyCredits is null && MaxPagesPerSearch is null
        && MaxJobIdsPerDetails is null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
