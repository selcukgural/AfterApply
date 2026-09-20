using AfterApply.Domain.Applications;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.CompanySalaries;
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
    DateTimeOffset UploadedAt,
    // The last ATS-readability scan of this file, when there was one: the score and when it was
    // measured. The full report (findings, excerpts) is readable on the CV page and is derived
    // from the file above, so the export carries the number rather than a second copy of the CV's
    // own lines.
    int? ScanScore,
    DateTimeOffset? ScannedAt);

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

/// <summary>What the user said about an employer, with the moderation outcome. Public readers
/// never see the author; the author gets the whole row back, because it is theirs — including,
/// on a legacy row, the free text that is no longer shown to anyone else.</summary>
public sealed record CompanyReviewExportItem(
    Guid Id,
    string CompanyName,
    ReviewFormat Format,
    EmploymentStatus EmploymentStatus,
    int OverallRating,
    IReadOnlyList<ReviewCategoryRatingDto> CategoryRatings,
    IReadOnlyList<string> LikedStatements,
    IReadOnlyList<string> ImprovableStatements,
    string? Title,
    string? Pros,
    string? Cons,
    int? ManagementRating,
    int? WorkEnvironmentRating,
    int? SalaryAndBenefitsRating,
    int? CareerAndDevelopmentRating,
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

/// <summary>The author's copy of a salary entry: every column, including the exact years that
/// readers only ever see as a band.</summary>
public sealed record CompanySalaryExportItem(
    Guid Id,
    string CompanyName,
    string OccupationCode,
    string OccupationNameTr,
    string OccupationNameEn,
    int YearsOfExperience,
    EmploymentType EmploymentType,
    SalaryEmploymentStatus EmploymentStatus,
    decimal MonthlyNetAmount,
    SalaryCurrency Currency,
    decimal? AnnualBonusAmount,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt,
    int? PeriodStartYear = null,
    int? PeriodEndYear = null);

/// <summary>The author's copy of a candidate experience: every column, including the exact
/// dates that readers only ever see as a quarter.</summary>
public sealed record CandidateExperienceExportItem(
    Guid Id,
    string CompanyName,
    int OverallRating,
    IReadOnlyList<ExperienceCategoryRatingDto> CategoryRatings,
    IReadOnlyList<string> LikedStatements,
    IReadOnlyList<string> ImprovableStatements,
    HiringOutcome? Outcome,
    ProcessDuration? Duration,
    StageCount? Stages,
    IReadOnlyList<InterviewType> InterviewTypes,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

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
    IReadOnlyList<Guid>? HelpfulMarkedReviewIds = null,
    IReadOnlyList<CompanySalaryExportItem>? CompanySalaries = null,
    IReadOnlyList<PaymentOrderExportItem>? Payments = null,
    ProEntitlementExportItem? ProEntitlement = null,
    IReadOnlyList<CandidateExperienceExportItem>? CandidateExperiences = null,
    IReadOnlyList<BlogCommentExportItem>? BlogComments = null);

/// <summary>The author's copy of a blog comment (2026-09-20): the text, the post it is on, its
/// status and its dates. Reports it received and who found it helpful are other readers' data.</summary>
public sealed record BlogCommentExportItem(
    Guid Id,
    string PostTitle,
    string PostLanguage,
    string? PostSlug,
    Guid? ParentCommentId,
    string Content,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt);

/// <summary>A Pro purchase as the user sees it: what they bought, what they typed for the
/// invoice, what happened. Provider internals (merchant ids, tokens, hashes) stay out.</summary>
public sealed record PaymentOrderExportItem(
    Guid Id,
    string Plan,
    long AmountMinor,
    long? TotalAmountMinor,
    string Currency,
    string Status,
    string BillingName,
    string BillingAddress,
    string BillingPhone,
    string TermsVersion,
    DateTimeOffset TermsAcceptedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt,
    int? FailedReasonCode,
    long RefundedAmountMinor,
    DateTimeOffset? RefundRequestedAt,
    string? RefundReason,
    DateTimeOffset? RefundedAt);

public sealed record ProEntitlementExportItem(DateTimeOffset ActiveUntil, string Source, DateTimeOffset GrantedAt, DateTimeOffset? RevokedAt);
