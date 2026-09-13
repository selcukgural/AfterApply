using AfterApply.Domain.Applications;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.Common;
using AfterApply.Domain.Documents;
using AfterApply.Domain.Notifications;
using AfterApply.Domain.Feedback;

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

/// <summary>What the user wrote in the in-app feedback panel, and what came back. The reply
/// address is included because it is theirs; the technical context (page, browser) is not — it is
/// diagnostic metadata about a message they already have in front of them.</summary>
public sealed record FeedbackExportItem(
    Guid Id,
    FeedbackCategory Category,
    FeedbackMood? Mood,
    string Message,
    string? ReplyEmail,
    FeedbackStatus Status,
    string? AdminReply,
    DateTimeOffset SubmittedAt);

/// <summary>What the user wrote about an employer, with the moderation outcome. Public readers
/// never see the author; the author gets the whole row back, because it is theirs.</summary>
public sealed record CompanyReviewExportItem(
    Guid Id,
    string CompanyName,
    EmploymentStatus EmploymentStatus,
    string Title,
    string Pros,
    string Cons,
    int OverallRating,
    int ManagementRating,
    int WorkEnvironmentRating,
    int SalaryAndBenefitsRating,
    int CareerAndDevelopmentRating,
    ReviewModerationStatus Status,
    string? RejectionReason,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A report the user filed against someone else's review — their statement, so it is
/// theirs to read back. The reported review's text is not included: it is another person's.</summary>
public sealed record CompanyReviewReportExportItem(
    Guid Id,
    Guid ReviewId,
    ReviewReportReason Reason,
    string? Note,
    ReviewReportStatus Status,
    ReviewReportResolution? Resolution,
    DateTimeOffset ReportedAt);

public sealed record AccountExportResponse(
    UserProfileResponse Profile,
    IReadOnlyList<ApplicationExportItem> Applications,
    IReadOnlyList<ImportBatchExportItem> ImportBatches,
    IReadOnlyList<ReminderExportItem> Reminders,
    DateTimeOffset ExportedAt,
    IReadOnlyList<CvDocumentExportItem>? CvDocuments = null,
    IReadOnlyList<FeedbackExportItem>? Feedback = null,
    IReadOnlyList<CompanyReviewExportItem>? CompanyReviews = null,
    IReadOnlyList<CompanyReviewReportExportItem>? CompanyReviewReports = null,
    IReadOnlyList<Guid>? HelpfulMarkedReviewIds = null);
