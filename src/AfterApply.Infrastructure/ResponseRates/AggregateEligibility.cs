using AfterApply.Domain.Applications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.ResponseRates;

/// <summary>Which applications the cross-user figures are built from (DECISIONS.md 2026-10-01).
/// Bound from <c>AggregateEligibility</c>.</summary>
public sealed class AggregateEligibilityOptions
{
    public const string SectionName = "AggregateEligibility";

    /// <summary>
    /// The most days an application may have been entered after the day it was sent. A row typed in
    /// within a week of applying was tracked as it happened, and its later status changes are what
    /// the company did. A LinkedIn export or a CSV carries months of past applications whose
    /// statuses were filled in from memory, all on one day — a record of the user's recollection,
    /// not of the company. Measured on the row itself (CreatedAt against AppliedAt), so it holds
    /// the same for a manual entry, the extension, an email suggestion and an import, and moving
    /// AppliedAt back later takes the row out again.
    /// </summary>
    public int FreshEntryWindowDays { get; init; } = 7;

    /// <summary>
    /// An application still in play only counts while its owner still uses the product — has had
    /// a session within this many days (a refresh token issued, the same signal the job-source
    /// sweep uses). Someone who stopped tracking leaves every open application sitting at Applied,
    /// and read as data that is "the company never answered", which is the one error this figure
    /// must not make about a named company. A finished application (Rejected, Accepted, Ghosted,
    /// Withdrawn) keeps counting: its outcome was recorded.
    /// </summary>
    public int ActiveTrackerWindowDays { get; init; } = 90;
}

/// <summary>
/// The one predicate every read that aggregates across accounts applies, so the sector table, the
/// company tab and the "is this company listed" count never disagree on whose data is in them:
/// <list type="bullet">
/// <item>the owner has not turned contribution off in settings (<see cref="Identity.ApplicationUser.ExcludeFromAggregates"/>);</item>
/// <item>the row was entered as it happened (<see cref="AggregateEligibilityOptions.FreshEntryWindowDays"/>);</item>
/// <item>the row is finished, or its owner is still tracking (<see cref="AggregateEligibilityOptions.ActiveTrackerWindowDays"/>).</item>
/// </list>
/// <see cref="ContributingUserIds"/> is the first rule alone, for counts of people rather than of
/// applications.
/// </summary>
internal sealed class AggregateEligibility(AppDbContext dbContext, IOptions<AggregateEligibilityOptions> options)
{
    public IQueryable<Guid> ContributingUserIds() =>
        dbContext.Users.Where(u => !u.ExcludeFromAggregates).Select(u => u.Id);

    public IQueryable<Domain.Applications.Application> Eligible(
        IQueryable<Domain.Applications.Application> applications, DateTimeOffset now)
    {
        var opts = options.Value;
        var freshWindow = TimeSpan.FromDays(opts.FreshEntryWindowDays);
        var activeSince = now.AddDays(-opts.ActiveTrackerWindowDays);
        var contributing = ContributingUserIds();

        return applications.Where(a =>
            contributing.Contains(a.UserId)
            && a.CreatedAt - a.AppliedAt <= freshWindow
            && (TerminalApplicationStatuses.Values.Contains(a.Status)
                || dbContext.RefreshTokens.Any(t => t.UserId == a.UserId && t.CreatedAt >= activeSince)));
    }
}
