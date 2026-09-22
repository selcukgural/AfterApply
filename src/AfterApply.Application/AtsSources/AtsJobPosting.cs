using AfterApply.Domain.Common;

namespace AfterApply.Application.AtsSources;

/// <summary>
/// What one ATS's public posting API tells us about a job, normalised across the five shapes.
/// Every field is optional: these APIs are best-effort backstops for a DOM scrape that already
/// succeeded or already left a field blank, and a missing value must never overwrite a present one
/// (see <c>Job.EnrichFrom</c>).
/// </summary>
public sealed record AtsJobPosting(
    string? Title = null,
    string? Description = null,
    string? DescriptionHtml = null,
    string? Location = null,
    DateTimeOffset? PublishedAt = null,
    EmploymentType? EmploymentType = null);
