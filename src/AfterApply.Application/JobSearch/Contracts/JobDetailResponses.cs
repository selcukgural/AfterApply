namespace AfterApply.Application.JobSearch.Contracts;

/// <summary>Answer to <c>GET /api/job-search/jobs/details</c>, in the order the ids were asked
/// for. An id the provider no longer knows is simply absent.</summary>
public sealed record JobSearchJobDetailsResponse(
    IReadOnlyList<JobSearchJobDetailResponse> Jobs,
    JobSearchMeta Meta);

/// <summary>
/// One posting as the details endpoint returns it — every field of the summary plus the fields
/// only <c>job-details</c> carries (highlights, employer reviews, the structured "insights" the
/// provider extracts from the description). Its own type, not a summary with extras bolted on.
/// </summary>
public sealed record JobSearchJobDetailResponse(
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
    string? OnetJobZone,
    JobSearchHighlightsResponse? Highlights,
    IReadOnlyList<JobSearchEmployerReviewResponse> EmployerReviews,
    string? WorkArrangement,
    string? SeniorityLevel,
    int? RequiredExperienceYears,
    JobSearchEducationResponse? EducationRequired,
    bool? VisaSponsorship,
    bool? RelocationRequired,
    bool? RelocationAssistance,
    string? ContractDuration,
    string? StartDate,
    IReadOnlyList<string> RequiredTechnologies,
    IReadOnlyList<string> PreferredTechnologies,
    IReadOnlyList<string> Methodologies,
    string? Industry,
    string? JobFunction,
    bool? HasManagementResponsibilities,
    bool? AiMlInvolved,
    IReadOnlyList<string> BenefitsExtended,
    IReadOnlyList<string> SoftSkills);

public sealed record JobSearchHighlightsResponse(
    IReadOnlyList<string> Qualifications,
    IReadOnlyList<string> Benefits,
    IReadOnlyList<string> Responsibilities);

public sealed record JobSearchEmployerReviewResponse(
    string? Publisher,
    string? EmployerName,
    double? Score,
    double? NumStars,
    int? ReviewCount,
    double? MaxScore,
    string? ReviewsLink);

public sealed record JobSearchEducationResponse(string? Level, string? Field);
