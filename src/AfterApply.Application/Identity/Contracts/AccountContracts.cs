using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Notifications;

namespace AfterApply.Application.Identity.Contracts;

/// <summary>Password is required to match only for accounts that have one. An account created
/// through Google sign-in has no password hash, so the client omits it (see
/// IAuthService.DeleteAccountAsync).</summary>
public sealed record DeleteAccountRequest(string? Password);

public sealed record ApplicationEventExportItem(ApplicationEventType Type, DateTimeOffset OccurredAt, Source Source, string? Metadata);

public sealed record StatusHistoryExportItem(ApplicationStatus? FromStatus, ApplicationStatus ToStatus, DateTimeOffset ChangedAt, string? Note);

public sealed record ApplicationExportItem(
    Guid Id,
    string CompanyName,
    string JobTitle,
    ApplicationStatus Status,
    DateTimeOffset AppliedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ApplicationEventExportItem> Events,
    IReadOnlyList<StatusHistoryExportItem> StatusHistory);

public sealed record ImportBatchExportItem(
    Guid Id,
    Source Source,
    string FileName,
    int TotalRecords,
    int NewApplications,
    DateTimeOffset? CompletedAt);

public sealed record ReminderExportItem(
    Guid Id,
    Guid ApplicationId,
    ReminderType Type,
    DateTimeOffset ReferenceAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DismissedAt);

public sealed record AccountExportResponse(
    UserProfileResponse Profile,
    IReadOnlyList<ApplicationExportItem> Applications,
    IReadOnlyList<ImportBatchExportItem> ImportBatches,
    IReadOnlyList<ReminderExportItem> Reminders,
    DateTimeOffset ExportedAt);
