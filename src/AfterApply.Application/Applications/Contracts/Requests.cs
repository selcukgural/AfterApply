using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

namespace AfterApply.Application.Applications.Contracts;

public sealed record CreateApplicationRequest(
    string CompanyName,
    string JobTitle,
    string? JobUrl,
    string? Location,
    EmploymentType EmploymentType,
    DateTimeOffset AppliedAt,
    Source? Source,
    string? Notes);

public sealed record UpdateApplicationRequest(
    string JobTitle,
    string? JobUrl,
    string? Location,
    EmploymentType EmploymentType,
    DateTimeOffset AppliedAt,
    string? Notes);

public sealed record ChangeStatusRequest(ApplicationStatus NewStatus, string? Note, DateTimeOffset? ChangedAt, Source? Source = null);

public sealed record CreateEventRequest(
    ApplicationEventType Type,
    DateTimeOffset? OccurredAt,
    Source? Source,
    string? Metadata);

public enum ApplicationListSortBy
{
    AppliedAt,
    CompanyName,
    JobTitle,
    Status,
    UpdatedAt
}

public enum SortDirection
{
    Ascending,
    Descending
}

public sealed record GetApplicationsQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    ApplicationStatus? Status = null,
    ApplicationListSortBy SortBy = ApplicationListSortBy.AppliedAt,
    SortDirection SortDirection = SortDirection.Descending);
