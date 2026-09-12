using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.JobSearch.Contracts;

namespace AfterApply.Infrastructure.JobSearch;

// The provider's shapes, as it sends them (snake_case, everything nullable). These never leave
// Infrastructure: JobSearchMapper turns them into the Application DTOs, and it is the DTO that is
// stored and returned. A field the provider adds tomorrow is ignored; one it drops becomes null.

/// <summary>The rate-limit headers RapidAPI adds to every response.</summary>
public sealed record JSearchRateLimit(int? Limit, int? Remaining, int? ResetSeconds);

/// <summary>A successful call: the typed data, the provider's request id (for support tickets),
/// what the gateway said about the month's remaining quota, and how many HTTP attempts it took —
/// RapidAPI bills every attempt that reaches it, a timed-out one included (observed 2026-09-12:
/// two 15 s timeouts cost two requests), so the ledger charges per attempt.</summary>
public sealed record JSearchResult<T>(T Data, string? RequestId, JSearchRateLimit RateLimit, int Attempts = 1);

/// <summary>Every <c>search-v2</c> parameter, typed. <see cref="Fields"/> is supported by the
/// client but never set by the service (a projected answer would poison the cache).</summary>
public sealed record JSearchSearchRequest(
    string Query,
    string? Cursor = null,
    int? NumPages = null,
    string? Country = null,
    string? Language = null,
    string? Location = null,
    JobSearchDatePosted? DatePosted = null,
    bool? WorkFromHome = null,
    IReadOnlyList<JobSearchEmploymentType>? EmploymentTypes = null,
    IReadOnlyList<JobSearchJobRequirement>? JobRequirements = null,
    int? Radius = null,
    IReadOnlyList<string>? ExcludeJobPublishers = null,
    IReadOnlyList<string>? Fields = null);

public sealed record JSearchJobDetailsRequest(
    IReadOnlyList<string> JobIds,
    string? Country = null,
    string? Language = null,
    IReadOnlyList<string>? Fields = null);

public sealed record JSearchEstimatedSalaryRequest(
    string JobTitle,
    string Location,
    JobSearchLocationType LocationType = JobSearchLocationType.Any,
    JobSearchExperienceRange YearsOfExperience = JobSearchExperienceRange.All,
    IReadOnlyList<string>? Fields = null);

public sealed record JSearchCompanySalaryRequest(
    string Company,
    string JobTitle,
    string? Location = null,
    JobSearchLocationType LocationType = JobSearchLocationType.Any,
    JobSearchExperienceRange YearsOfExperience = JobSearchExperienceRange.All);

public sealed record JSearchSearchData(
    IReadOnlyList<JSearchJob>? Jobs,
    string? Cursor);

/// <summary>One posting. The search endpoint fills the first 34 members; job-details fills all
/// of them. <c>job_highlights</c> and <c>employer_reviews</c> are kept as raw JSON because the
/// search endpoint sends <c>{}</c>/<c>null</c> where the details endpoint sends real objects.</summary>
public sealed record JSearchJob(
    string? JobId,
    string? JobTitle,
    string? EmployerName,
    string? EmployerLogo,
    string? EmployerWebsite,
    string? JobPublisher,
    string? JobEmploymentType,
    IReadOnlyList<string>? JobEmploymentTypes,
    string? JobApplyLink,
    bool? JobApplyIsDirect,
    IReadOnlyList<JSearchApplyOption>? ApplyOptions,
    string? JobDescription,
    bool? JobIsRemote,
    string? JobPostedAt,
    long? JobPostedAtTimestamp,
    string? JobPostedAtDatetimeUtc,
    string? JobLocation,
    string? JobCity,
    string? JobState,
    string? JobCountry,
    double? JobLatitude,
    double? JobLongitude,
    IReadOnlyList<string>? JobBenefits,
    IReadOnlyList<string>? JobBenefitsStrings,
    string? JobGoogleLink,
    double? JobSalary,
    string? JobSalaryString,
    double? JobMinSalary,
    double? JobMaxSalary,
    string? JobSalaryPeriod,
    JsonElement? JobHighlights,
    string? JobOnetSoc,
    string? JobOnetJobZone,
    JsonElement? EmployerReviews,
    string? WorkArrangement,
    string? SeniorityLevel,
    double? RequiredExperienceYears,
    JSearchEducationRequired? EducationRequired,
    bool? VisaSponsorship,
    bool? RelocationRequired,
    bool? RelocationAssistance,
    string? ContractDuration,
    string? StartDate,
    IReadOnlyList<string>? RequiredTechnologies,
    IReadOnlyList<string>? PreferredTechnologies,
    IReadOnlyList<string>? Methodologies,
    string? Industry,
    string? JobFunction,
    bool? HasManagementResponsibilities,
    bool? AiMlInvolved,
    IReadOnlyList<string>? BenefitsExtended,
    IReadOnlyList<string>? SoftSkills);

public sealed record JSearchApplyOption(string? Publisher, string? ApplyLink, bool? IsDirect);

public sealed record JSearchEducationRequired(string? Level, string? Field);

public sealed record JSearchEmployerReview(
    string? Publisher,
    string? EmployerName,
    double? Score,
    double? NumStars,
    long? ReviewCount,
    double? MaxScore,
    string? ReviewsLink);

public sealed record JSearchSalaryEstimate(
    string? Location,
    string? JobTitle,
    double? MinSalary,
    double? MaxSalary,
    double? MedianSalary,
    double? MinBaseSalary,
    double? MaxBaseSalary,
    double? MedianBaseSalary,
    double? MinAdditionalPay,
    double? MaxAdditionalPay,
    double? MedianAdditionalPay,
    string? SalaryPeriod,
    string? SalaryCurrency,
    long? SalaryCount,
    string? SalariesUpdatedAt,
    string? PublisherName,
    string? PublisherLink,
    string? Confidence);

public sealed record JSearchCompanySalary(
    string? Location,
    string? JobTitle,
    string? Company,
    double? MinSalary,
    double? MaxSalary,
    double? MedianSalary,
    double? MinBaseSalary,
    double? MaxBaseSalary,
    double? MedianBaseSalary,
    double? MinAdditionalPay,
    double? MaxAdditionalPay,
    double? MedianAdditionalPay,
    string? SalaryPeriod,
    string? SalaryCurrency,
    string? Confidence,
    long? SalaryCount);

/// <summary>The exact strings the provider accepts for each typed parameter.</summary>
public static class JSearchWireValues
{
    public static string ToWire(this JobSearchDatePosted value) => value switch
    {
        JobSearchDatePosted.All => "all",
        JobSearchDatePosted.Today => "today",
        JobSearchDatePosted.ThreeDays => "3days",
        JobSearchDatePosted.Week => "week",
        JobSearchDatePosted.Month => "month",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToWire(this JobSearchEmploymentType value) => value switch
    {
        JobSearchEmploymentType.FullTime => "FULLTIME",
        JobSearchEmploymentType.Contractor => "CONTRACTOR",
        JobSearchEmploymentType.PartTime => "PARTTIME",
        JobSearchEmploymentType.Intern => "INTERN",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToWire(this JobSearchJobRequirement value) => value switch
    {
        JobSearchJobRequirement.Under3YearsExperience => "under_3_years_experience",
        JobSearchJobRequirement.MoreThan3YearsExperience => "more_than_3_years_experience",
        JobSearchJobRequirement.NoExperience => "no_experience",
        JobSearchJobRequirement.NoDegree => "no_degree",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToWire(this JobSearchLocationType value) => value switch
    {
        JobSearchLocationType.Any => "ANY",
        JobSearchLocationType.City => "CITY",
        JobSearchLocationType.State => "STATE",
        JobSearchLocationType.Country => "COUNTRY",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    public static string ToWire(this JobSearchExperienceRange value) => value switch
    {
        JobSearchExperienceRange.All => "ALL",
        JobSearchExperienceRange.LessThanOne => "LESS_THAN_ONE",
        JobSearchExperienceRange.OneToThree => "ONE_TO_THREE",
        JobSearchExperienceRange.FourToSix => "FOUR_TO_SIX",
        JobSearchExperienceRange.SevenToNine => "SEVEN_TO_NINE",
        JobSearchExperienceRange.TenToFourteen => "TEN_TO_FOURTEEN",
        JobSearchExperienceRange.AboveFifteen => "ABOVE_FIFTEEN",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)
    };

    /// <summary>Reads the provider's JSON: snake_case names, numbers that sometimes arrive as
    /// strings, and no failure on a member we do not model.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true
    };
}
