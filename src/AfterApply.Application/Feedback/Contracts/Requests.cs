using AfterApply.Domain.Feedback;

namespace AfterApply.Application.Feedback.Contracts;

/// <summary>
/// What the in-app panel sends. The context fields (<paramref name="PagePath"/>,
/// <paramref name="Locale"/>, <paramref name="Theme"/>) are collected by the client and are
/// disclosed to the user in the panel itself before they send — see the note under the message box.
/// The user agent is not here on purpose: it is read from the request header instead.
/// </summary>
public sealed record SubmitFeedbackRequest(
    FeedbackCategory Category,
    string Message,
    FeedbackMood? Mood = null,
    string? ReplyEmail = null,
    string? PagePath = null,
    string? Locale = null,
    string? Theme = null);
