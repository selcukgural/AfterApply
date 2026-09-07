namespace AfterApply.Application.Feedback.Contracts;

/// <summary>
/// Deliberately just the receipt. The panel only needs to know the message landed; echoing the
/// stored row back — message, context, mirror state — would widen the response for nothing.
/// </summary>
public sealed record FeedbackResponse(Guid Id, DateTimeOffset SubmittedAt);
