namespace AfterApply.Application.JobSearch.Contracts;

/// <summary>
/// <c>GET /api/job-search/jobs</c>. Every optional member is nullable because null means "use the
/// caller's saved preference, or failing that the global default" — the service resolves them
/// (see EffectiveJobSearchSettings); <c>false</c> or <c>All</c> sent explicitly is an override.
/// Comma-separated list parameters arrive as one string (<c>?employmentTypes=FullTime,Intern</c>)
/// and are validated against the enum names.
/// </summary>
public sealed record SearchJobsQuery(
    string Query = "",
    string? Cursor = null,
    int? NumPages = null,
    string? Country = null,
    string? Language = null,
    string? Location = null,
    JobSearchDatePosted? DatePosted = null,
    bool? WorkFromHome = null,
    string? EmploymentTypes = null,
    string? JobRequirements = null,
    int? Radius = null,
    string? ExcludeJobPublishers = null);

/// <summary><c>GET /api/job-search/jobs/details?ids=a,b,c</c>. Ids are the provider's opaque
/// <c>job_id</c> values from a search; each id not already on file costs one credit.</summary>
public sealed record GetJobDetailsQuery(
    string Ids = "",
    string? Country = null,
    string? Language = null);

/// <summary><c>GET /api/job-search/salary</c>.</summary>
public sealed record EstimatedSalaryQuery(
    string JobTitle = "",
    string Location = "",
    JobSearchLocationType LocationType = JobSearchLocationType.Any,
    JobSearchExperienceRange YearsOfExperience = JobSearchExperienceRange.All);

/// <summary><c>GET /api/job-search/company-salary</c>.</summary>
public sealed record CompanyJobSalaryQuery(
    string Company = "",
    string JobTitle = "",
    string? Location = null,
    JobSearchLocationType LocationType = JobSearchLocationType.Any,
    JobSearchExperienceRange YearsOfExperience = JobSearchExperienceRange.All);

/// <summary>The user's own overrides (<c>PUT /api/job-search/settings</c>). Null clears that
/// override back to the global default; the whole row goes when nothing is left.</summary>
public sealed record UpdateJobSearchPreferencesRequest(
    string? DefaultCountry,
    string? DefaultLanguage,
    string? DefaultLocation,
    JobSearchDatePosted? DefaultDatePosted,
    bool? DefaultWorkFromHome);

/// <summary>Admin-only limit overrides for one user (<c>PUT /api/admin/job-search/settings/{userId}</c>).
/// Null clears that override.</summary>
public sealed record UpdateJobSearchLimitsRequest(
    int? PerUserDailyCredits,
    int? MaxPagesPerSearch,
    int? MaxJobIdsPerDetails);
