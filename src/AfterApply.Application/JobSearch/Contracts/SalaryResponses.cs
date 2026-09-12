namespace AfterApply.Application.JobSearch.Contracts;

/// <summary>Answer to <c>GET /api/job-search/salary</c> — JSearch's <c>estimated-salary</c>.</summary>
public sealed record JobSearchSalaryEstimatesResponse(
    IReadOnlyList<JobSearchSalaryEstimateResponse> Estimates,
    JobSearchMeta Meta);

/// <summary>One salary estimate for a title around a location, all 18 provider fields.</summary>
public sealed record JobSearchSalaryEstimateResponse(
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
    int? SalaryCount,
    DateTimeOffset? SalariesUpdatedAt,
    string? PublisherName,
    string? PublisherLink,
    string? Confidence);
