using AfterApply.Domain.Applications;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Domain.Notifications;

namespace AfterApply.Application.Notifications.Contracts;

public sealed record ReminderResponse(
    Guid Id,
    Guid ApplicationId,
    string CompanyName,
    string JobTitle,
    ReminderType Type,
    int DaysElapsed,
    DateTimeOffset CreatedAt,
    // The median number of days this user's own applications took to get a first reply — the
    // n=1 norm a "possibly ghosted" row is measured against ("you usually hear back in 9 days").
    // Null until enough of their applications have been answered to make a median worth showing
    // (ReminderCalculations.UserMedianResponseDays), and the same value on every row of a page.
    int? UserMedianResponseDays = null,
    // The date the company said it would answer by — set on PromiseMissed rows only, so the row can
    // say "they were going to answer by the 10th" rather than just count days.
    DateOnly? PromisedReplyBy = null,
    // Where the application stands now — an InterviewHeld row offers only the stages after it.
    ApplicationStatus? ApplicationStatus = null,
    // The interview an InterviewHeld row asks about; null on every other type.
    DateTimeOffset? InterviewAt = null,
    // Set when the scan held this reminder back from a weekend or public holiday, with the morning
    // it was moved to — so the row can say "moved to today because of the feast" on that day.
    ReminderDeferral? DeferredFor = null,
    DateTimeOffset? DeferredUntil = null,
    // Who the user met, on InterviewHeld rows: the names the thank-you draft greets.
    string? InterviewWith = null);

/// <summary>A due "apply here again" reminder (ReminderType.Reapply): the rejected application,
/// when it was rejected, the reason the company gave if one was recorded, and where the company
/// lists its openings — whichever of its LinkedIn page and website is known; the slug for the
/// company's page here when neither is.</summary>
public sealed record ReapplyReminderResponse(
    Guid Id,
    Guid ApplicationId,
    string CompanyName,
    string JobTitle,
    DateTimeOffset RejectedAt,
    RejectionReasonCategory? RejectionReason,
    string? CompanyLinkedInUrl,
    string? CompanyWebsite,
    string? CompanySlug);

/// <summary>What an answer to "how did the interview go?" changed, so the row's undo can put exactly
/// that back: the status move (both ends) and the reply date it recorded, each null when the answer
/// did not do it.</summary>
public sealed record InterviewOutcomeResponse(
    ApplicationStatus? FromStatus,
    ApplicationStatus? ToStatus,
    DateOnly? PromisedReplyBy);

/// <summary>One interview still to come, for the top of the reminders card. Only what the row and
/// the "add to calendar" file need; the meeting link is never stored, so it is never here.</summary>
public sealed record UpcomingInterviewResponse(
    Guid ApplicationId,
    string CompanyName,
    string JobTitle,
    ApplicationStatus Status,
    DateTimeOffset InterviewAt,
    InterviewFormat Format,
    string? InterviewWith = null);

/// <summary>How many reminders a bulk answer actually closed. Ids that were not the caller's, or
/// were already closed, simply do not count — an id in a request body is a claim, not proof.</summary>
public sealed record BulkReminderResponse(int Affected);

/// <summary>Where the user's break stands. <c>None</c>: no break. <c>Paused</c>: reminders are
/// hidden until <see cref="ReminderPauseResponse.PausedUntil"/>. <c>Returned</c>: the break has ended
/// and the return question has not been answered yet.</summary>
public enum ReminderPauseState
{
    None,
    Paused,
    Returned
}

/// <param name="SilencedCount">Applications whose "possibly ghosted" reminder was raised during the
/// break and is still open — the number the return question offers to close. Zero outside
/// <see cref="ReminderPauseState.Returned"/>.</param>
public sealed record ReminderPauseResponse(
    ReminderPauseState State,
    DateTimeOffset? PausedFrom,
    DateTimeOffset? PausedUntil,
    int SilencedCount);
