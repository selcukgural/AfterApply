namespace AfterApply.Application.JobSearch.Contracts;

/// <summary>Answer to <c>GET /api/job-search/company-salary</c> — JSearch's <c>company-job-salary</c>.</summary>
public sealed record JobSearchCompanySalariesResponse(
    IReadOnlyList<JobSearchCompanySalaryResponse> Salaries,
    JobSearchMeta Meta);

/// <summary>One salary estimate for a title at a named company, all 16 provider fields.</summary>
public sealed record JobSearchCompanySalaryResponse(
    string? Company,
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
    string? Confidence);
