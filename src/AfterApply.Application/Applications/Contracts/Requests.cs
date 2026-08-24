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

public sealed record ChangeStatusRequest(ApplicationStatus NewStatus, string? Note, DateTimeOffset? ChangedAt);

public sealed record CreateEventRequest(
    ApplicationEventType Type,
    DateTimeOffset? OccurredAt,
    Source? Source,
    string? Metadata);
