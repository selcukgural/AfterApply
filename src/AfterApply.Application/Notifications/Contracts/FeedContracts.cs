using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Domain.Notifications;

namespace AfterApply.Application.Notifications.Contracts;

/// <summary>The bell's page. The ceiling on <see cref="Page"/> exists because the feed merges two
/// sources and reads <c>Page * PageSize</c> rows from each to place a page boundary correctly.</summary>
public sealed record GetNotificationFeedQuery(int Page = 1, int PageSize = 10);

public enum NotificationFeedKind
{
    Contribution,
    Email
}

/// <summary>
/// One row of the bell: exactly one of <see cref="Contribution"/> / <see cref="Email"/> is set,
/// as <see cref="Kind"/> says. <see cref="Id"/> is what <c>POST /api/notifications/{id}/dismiss</c>
/// takes for either kind. <see cref="OccurredAt"/> is the row's time as shown — for a contribution,
/// the latest mark folded into it.
/// </summary>
public sealed record NotificationFeedItemResponse(
    Guid Id,
    NotificationFeedKind Kind,
    DateTimeOffset OccurredAt,
    bool IsRead,
    ContributionNotificationResponse? Contribution,
    EmailNotificationResponse? Email);

/// <summary>
/// "Your … was found helpful N times." Only what the author can already see on the public page:
/// the company (for a review, salary or experience) or the blog post (for a comment), and the id of
/// their own contribution to link to. Nothing about who marked it — the row does not know.
/// </summary>
public sealed record ContributionNotificationResponse(
    ContributionNotificationType Type,
    Guid TargetId,
    int Count,
    string? CompanyName,
    string? CompanySlug,
    string? BlogPostTitle,
    string? BlogPostSlug,
    string? BlogPostLanguage);

public sealed record NotificationFeedCountResponse(int UnreadCount);

/// <summary>Account settings › Notifications. <see cref="Contributions"/> is the master switch for
/// the four helpful kinds; each kind keeps its own value while the master is off.</summary>
public sealed record NotificationPreferencesResponse(
    bool Contributions,
    bool ReviewHelpful,
    bool SalaryHelpful,
    bool ExperienceHelpful,
    bool BlogCommentHelpful,
    bool GmailUpdates);

public sealed record UpdateNotificationPreferencesRequest(
    bool Contributions,
    bool ReviewHelpful,
    bool SalaryHelpful,
    bool ExperienceHelpful,
    bool BlogCommentHelpful,
    bool GmailUpdates);
