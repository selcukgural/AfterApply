using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;

namespace AfterApply.Application.CompanyReviews.Contracts;

// The author's three kinds of contribution — reviews, salary entries, candidate experiences — as
// one list (2026-09-18). Each item carries exactly one of the three "mine" records, the same shape
// the per-kind endpoints return, so the cards on the page and the edit forms read one type.

public enum ContributionKind
{
    Review,
    Salary,
    Experience
}

public sealed record MyContributionResponse(
    ContributionKind Kind,
    DateTimeOffset SubmittedAt,
    MyCompanyReviewResponse? Review,
    MyCompanySalaryResponse? Salary,
    MyCandidateExperienceResponse? Experience);

/// <summary>One page, newest first. A quota is null while its feature is off — the page then
/// shows neither the line nor the call to action for that kind.</summary>
public sealed record MyContributionsResponse(
    IReadOnlyList<MyContributionResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ReviewQuotaResponse ReviewQuota,
    SalaryQuotaResponse? SalaryQuota,
    ExperienceQuotaResponse? ExperienceQuota);

public sealed record MyContributionsQuery(int Page = 1);
