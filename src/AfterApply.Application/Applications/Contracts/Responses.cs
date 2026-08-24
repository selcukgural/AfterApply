using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

namespace AfterApply.Application.Applications.Contracts;

public sealed record ApplicationSummaryResponse(
    Guid Id,
    string CompanyName,
    string JobTitle,
    ApplicationStatus Status,
    DateTimeOffset AppliedAt,
    DateTimeOffset UpdatedAt);

public sealed record ApplicationDetailResponse(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    string JobTitle,
    string? JobUrl,
    string? Location,
    EmploymentType EmploymentType,
    DateTimeOffset AppliedAt,
    ApplicationStatus Status,
    Source Source,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ApplicationEventResponse(
    Guid Id,
    ApplicationEventType Type,
    DateTimeOffset OccurredAt,
    Source Source,
    string? Metadata);
