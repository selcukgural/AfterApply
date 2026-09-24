using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;

namespace AfterApply.Application.CompanyReviews.Contracts;

// The author's kinds of contribution — reviews, salary entries, candidate experiences and, since
// 2026-09-20, blog comments — as one list (2026-09-18). Each item carries exactly one of the
// "mine" records, the same shape the per-kind endpoints return, so the cards on the page and the
// edit forms read one type.

public enum ContributionKind
{
    Review,
    Salary,
    Experience,
    BlogComment
}

/// <summary>What the page's filter chips narrow to: blog comments, or everything about companies.</summary>
public enum ContributionFilter
{
    BlogComments,
    Company
}

public sealed record MyContributionResponse(
    ContributionKind Kind,
    DateTimeOffset SubmittedAt,
    MyCompanyReviewResponse? Review,
    MyCompanySalaryResponse? Salary,
    MyCandidateExperienceResponse? Experience,
    MyBlogCommentResponse? BlogComment = null,
    /// <summary>Whether the company pages show the "backed by a tracked application" label on
    /// this row (2026-09-24); always false for a blog comment.</summary>
    bool BackedByApplication = false);

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

public sealed record MyContributionsQuery(int Page = 1, ContributionFilter? Filter = null);
