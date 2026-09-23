using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Domain.Blog;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.EmailIntegrations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Notifications;

internal sealed class NotificationFeedService(
    AppDbContext dbContext,
    IEmailForwardingService emailForwarding,
    IOptions<EmailForwardingOptions> emailForwardingOptions,
    TimeProvider? timeProvider = null) : INotificationFeedService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// The contribution rows the bell shows: not cleared, and pointing at something still on a
    /// public page. A review or comment taken down by moderation, a salary or experience its author
    /// deleted, a comment under an unpublished post — the row goes quiet rather than linking to a
    /// 404. Every read and write below starts here so they cannot disagree on what is listed.
    /// </summary>
    private IQueryable<ContributionNotification> Contributions(Guid userId) =>
        dbContext.ContributionNotifications.Where(n => n.UserId == userId && n.DismissedAt == null && (
            (n.Type == ContributionNotificationType.ReviewHelpful &&
             dbContext.CompanyReviews.Any(r => r.Id == n.TargetId && r.Status == ReviewModerationStatus.Approved)) ||
            (n.Type == ContributionNotificationType.SalaryHelpful &&
             dbContext.CompanySalaryEntries.Any(s => s.Id == n.TargetId)) ||
            (n.Type == ContributionNotificationType.ExperienceHelpful &&
             dbContext.CandidateExperiences.Any(e => e.Id == n.TargetId)) ||
            (n.Type == ContributionNotificationType.BlogCommentHelpful &&
             dbContext.BlogComments.Any(c => c.Id == n.TargetId && c.Status == BlogCommentStatus.Approved &&
                                             dbContext.BlogPosts.Any(p => p.Id == c.PostId && p.Status == BlogPostStatus.Published)))));

    public async Task<PagedResult<NotificationFeedItemResponse>> ListAsync(Guid userId, GetNotificationFeedQuery query,
        CancellationToken cancellationToken)
    {
        var reach = query.Page * query.PageSize;

        var contributions = Contributions(userId);
        var contributionTotal = await contributions.CountAsync(cancellationToken);
        var contributionRows = await contributions
            .OrderByDescending(n => n.LastEventAt).ThenByDescending(n => n.Id)
            .Take(reach)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var contributionItems = await ToItemsAsync(contributionRows, cancellationToken);

        var emailItems = new List<NotificationFeedItemResponse>();
        var emailTotal = 0;
        if (await ShowsGmailAsync(userId, cancellationToken))
        {
            var emails = await emailForwarding.GetNotificationsAsync(userId, new GetNotificationsQuery(1, reach), cancellationToken);
            emailTotal = emails.TotalCount;
            // Only an auto-applied change was ever "unread" (the Gmail count's own rule): a change
            // the user confirmed themselves is not news to them.
            emailItems.AddRange(emails.Items.Select(e => new NotificationFeedItemResponse(
                e.Id, NotificationFeedKind.Email, e.CreatedAt, e.IsRead || !e.WasAutoApplied, null, e)));
        }

        var page = NotificationFeedPaging.Page(contributionItems, emailItems, query.Page, query.PageSize);
        return new PagedResult<NotificationFeedItemResponse>(page, contributionTotal + emailTotal, query.Page, query.PageSize);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var count = await Contributions(userId).CountAsync(n => n.ReadAt == null, cancellationToken);
        if (await ShowsGmailAsync(userId, cancellationToken))
        {
            count += await emailForwarding.GetUnreadNotificationCountAsync(userId, cancellationToken);
        }

        return count;
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        await dbContext.ContributionNotifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), cancellationToken);

        if (await ShowsGmailAsync(userId, cancellationToken))
        {
            await emailForwarding.MarkNotificationsReadAsync(userId, cancellationToken);
        }
    }

    public async Task<bool> DismissAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        // Not Contributions(userId): clearing a row that was already cleared, or whose target has
        // since gone, is a 204 — a swipe racing its own retry is not an error.
        var notification = await dbContext.ContributionNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (notification is not null)
        {
            notification.Dismiss(_timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        return await ShowsGmailAsync(userId, cancellationToken)
               && await emailForwarding.DismissNotificationAsync(userId, id, cancellationToken);
    }

    public async Task DismissAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        await dbContext.ContributionNotifications
            .Where(n => n.UserId == userId && n.DismissedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.DismissedAt, now), cancellationToken);

        // Hidden Gmail rows stay as they are: "clear all" clears what the user was looking at.
        if (await ShowsGmailAsync(userId, cancellationToken))
        {
            await emailForwarding.DismissAllNotificationsAsync(userId, cancellationToken);
        }
    }

    public async Task<NotificationPreferencesResponse> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => new NotificationPreferencesResponse(
                u.NotifyContributions, u.NotifyReviewHelpful, u.NotifySalaryHelpful,
                u.NotifyExperienceHelpful, u.NotifyBlogCommentHelpful, u.NotifyGmailUpdates))
            .SingleAsync(cancellationToken);

    public async Task<NotificationPreferencesResponse> UpdatePreferencesAsync(Guid userId,
        UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken)
    {
        await dbContext.Users
            .Where(u => u.Id == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.NotifyContributions, request.Contributions)
                .SetProperty(u => u.NotifyReviewHelpful, request.ReviewHelpful)
                .SetProperty(u => u.NotifySalaryHelpful, request.SalaryHelpful)
                .SetProperty(u => u.NotifyExperienceHelpful, request.ExperienceHelpful)
                .SetProperty(u => u.NotifyBlogCommentHelpful, request.BlogCommentHelpful)
                .SetProperty(u => u.NotifyGmailUpdates, request.GmailUpdates), cancellationToken);
        return await GetPreferencesAsync(userId, cancellationToken);
    }

    private async Task<bool> ShowsGmailAsync(Guid userId, CancellationToken cancellationToken) =>
        emailForwardingOptions.Value.Enabled &&
        await dbContext.Users.Where(u => u.Id == userId).Select(u => u.NotifyGmailUpdates).FirstOrDefaultAsync(cancellationToken);

    /// <summary>Company or post for each row: one lookup per kind for the page, so the paging above
    /// stays over notifications alone.</summary>
    private async Task<List<NotificationFeedItemResponse>> ToItemsAsync(List<ContributionNotification> rows,
        CancellationToken cancellationToken)
    {
        List<Guid> Ids(ContributionNotificationType type) => rows.Where(r => r.Type == type).Select(r => r.TargetId).Distinct().ToList();

        var reviewIds = Ids(ContributionNotificationType.ReviewHelpful);
        var salaryIds = Ids(ContributionNotificationType.SalaryHelpful);
        var experienceIds = Ids(ContributionNotificationType.ExperienceHelpful);
        var commentIds = Ids(ContributionNotificationType.BlogCommentHelpful);

        var companies = new Dictionary<Guid, (string Name, string? Slug)>();
        if (reviewIds.Count > 0)
        {
            foreach (var x in await dbContext.CompanyReviews.Where(r => reviewIds.Contains(r.Id))
                         .Join(dbContext.Companies, r => r.CompanyId, c => c.Id, (r, c) => new { r.Id, c.Name, c.Slug })
                         .ToListAsync(cancellationToken))
            {
                companies[x.Id] = (x.Name, x.Slug);
            }
        }

        if (salaryIds.Count > 0)
        {
            foreach (var x in await dbContext.CompanySalaryEntries.Where(s => salaryIds.Contains(s.Id))
                         .Join(dbContext.Companies, s => s.CompanyId, c => c.Id, (s, c) => new { s.Id, c.Name, c.Slug })
                         .ToListAsync(cancellationToken))
            {
                companies[x.Id] = (x.Name, x.Slug);
            }
        }

        if (experienceIds.Count > 0)
        {
            foreach (var x in await dbContext.CandidateExperiences.Where(e => experienceIds.Contains(e.Id))
                         .Join(dbContext.Companies, e => e.CompanyId, c => c.Id, (e, c) => new { e.Id, c.Name, c.Slug })
                         .ToListAsync(cancellationToken))
            {
                companies[x.Id] = (x.Name, x.Slug);
            }
        }

        Dictionary<Guid, (string Title, string? Slug, string Language)> posts = commentIds.Count == 0
            ? new()
            : await dbContext.BlogComments.Where(c => commentIds.Contains(c.Id))
                .Join(dbContext.BlogPosts, c => c.PostId, p => p.Id, (c, p) => new { c.Id, p.Title, p.Slug, p.Language })
                .ToDictionaryAsync(x => x.Id, x => (x.Title, x.Slug, x.Language), cancellationToken);

        return rows.Select(r =>
        {
            var company = companies.GetValueOrDefault(r.TargetId);
            var post = posts.GetValueOrDefault(r.TargetId);
            return new NotificationFeedItemResponse(
                r.Id, NotificationFeedKind.Contribution, r.LastEventAt, r.ReadAt != null,
                new ContributionNotificationResponse(
                    r.Type, r.TargetId, r.Count,
                    company.Name, company.Slug,
                    post.Title, post.Slug, post.Language),
                null);
        }).ToList();
    }
}
