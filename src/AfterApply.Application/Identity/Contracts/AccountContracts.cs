using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Documents;
using AfterApply.Domain.Notifications;

namespace AfterApply.Application.Identity.Contracts;

/// <summary>Password is required to match only for accounts that have one. An account created
/// through Google sign-in has no password hash, so the client omits it (see
/// IAuthService.DeleteAccountAsync).</summary>
public sealed record DeleteAccountRequest(string? Password);

public sealed record ApplicationEventExportItem(ApplicationEventType Type, DateTimeOffset OccurredAt, Source Source, string? Metadata);

public sealed record StatusHistoryExportItem(ApplicationStatus? FromStatus, ApplicationStatus ToStatus, DateTimeOffset ChangedAt, string? Note, StatusChangeOrigin Origin);

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

/// <summary>Metadata only. The CV files themselves are not inlined into the export — they are
/// already downloadable one by one from the CV page, and base64-ing up to ten documents into a
/// JSON body would make the export unusable for the thing it is for (reading what is held about
/// you). This row is what tells the reader which files exist.</summary>
public sealed record CvDocumentExportItem(
    Guid Id,
    string FileName,
    CvFileFormat Format,
    long SizeBytes,
    bool IsDefault,
    DateTimeOffset UploadedAt);

public sealed record AccountExportResponse(
    UserProfileResponse Profile,
    IReadOnlyList<ApplicationExportItem> Applications,
    IReadOnlyList<ImportBatchExportItem> ImportBatches,
    IReadOnlyList<ReminderExportItem> Reminders,
    DateTimeOffset ExportedAt,
    IReadOnlyList<CvDocumentExportItem>? CvDocuments = null);
