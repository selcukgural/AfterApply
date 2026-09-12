namespace AfterApply.Application.JobSearch.Contracts;

/// <summary>Where an answer came from and what it cost. <see cref="CreditsCharged"/> is what was
/// written to the ledger for this call — zero when everything was served from our own tables.</summary>
public sealed record JobSearchMeta(bool FromCache, DateTimeOffset FetchedAt, int CreditsCharged);

/// <summary>Answer to <c>GET /api/job-search/jobs</c>. <see cref="NextCursor"/> is passed back as
/// <c>cursor</c> to fetch the following page — one page per credit.</summary>
public sealed record JobSearchResultsResponse(
    IReadOnlyList<JobSearchJobSummaryResponse> Jobs,
    string? NextCursor,
    JobSearchMeta Meta);

/// <summary>
/// One posting as the search endpoint returns it — every field JSearch's <c>search-v2</c> carries,
/// in the product's own names. The details endpoint returns the wider
/// <see cref="JobSearchJobDetailResponse"/>; the two are separate types on purpose, so a summary
/// is never mistaken for a record that has the detail-only fields.
/// </summary>
public sealed record JobSearchJobSummaryResponse(
    string JobId,
    string? Title,
    string? EmployerName,
    string? EmployerLogo,
    string? EmployerWebsite,
    string? Publisher,
    string? EmploymentType,
    IReadOnlyList<string> EmploymentTypes,
    string? ApplyLink,
    bool? ApplyIsDirect,
    IReadOnlyList<JobSearchApplyOptionResponse> ApplyOptions,
    string? Description,
    bool? IsRemote,
    string? PostedAtText,
    long? PostedAtTimestamp,
    DateTimeOffset? PostedAtUtc,
    string? Location,
    string? City,
    string? State,
    string? Country,
    double? Latitude,
    double? Longitude,
    IReadOnlyList<string> Benefits,
    IReadOnlyList<string> BenefitLabels,
    string? GoogleLink,
    JobSearchSalaryResponse? Salary,
    string? OnetSoc,
    string? OnetJobZone);

public sealed record JobSearchApplyOptionResponse(string? Publisher, string? ApplyLink, bool? IsDirect);

/// <summary>JSearch's five salary fields on a posting, grouped. Null as a whole when the posting
/// states no pay at all (most do not).</summary>
public sealed record JobSearchSalaryResponse(double? Value, string? Text, double? Min, double? Max, string? Period);
