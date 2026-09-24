using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Decides whether an account email may go out now, and records it when it may. Three checks, per
/// kind: a cooldown per account, a daily count per account, and a daily count for the whole
/// product. Two concurrent requests can both pass before either records — the per-IP auth rate
/// limit keeps that overshoot to a handful, which the ceilings leave room for.
/// </summary>
public sealed class AuthEmailThrottle(AppDbContext dbContext, IOptions<EmailVerificationOptions> options,
    ILogger<AuthEmailThrottle> logger, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public readonly record struct Decision(bool Allowed, DateTimeOffset NextAllowedAt);

    public async Task<Decision> TryReserveAsync(Guid userId, AuthEmailKind kind, CancellationToken cancellationToken)
    {
        var limit = options.Value.LimitFor(kind);
        var now = _timeProvider.GetUtcNow();
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);

        var accountSends = await dbContext.AuthEmailDispatches
            .Where(d => d.UserId == userId && d.Kind == kind && d.SentAt >= dayStart)
            .Select(d => d.SentAt)
            .ToListAsync(cancellationToken);

        if (accountSends.Count > 0)
        {
            var cooldownEnds = accountSends.Max() + TimeSpan.FromSeconds(limit.CooldownSeconds);
            if (cooldownEnds > now)
            {
                return new Decision(false, cooldownEnds);
            }
        }

        if (accountSends.Count >= limit.PerAccountDaily)
        {
            return new Decision(false, dayStart.AddDays(1));
        }

        var globalSends = await dbContext.AuthEmailDispatches
            .CountAsync(d => d.Kind == kind && d.SentAt >= dayStart, cancellationToken);
        if (globalSends >= limit.GlobalDaily)
        {
            // One fixed message per kind rather than the enum as a log value: nothing about the
            // account goes into the line, and a scanner cannot mistake a kind name for a secret.
            if (kind == AuthEmailKind.EmailVerification)
            {
                logger.LogWarning("Daily ceiling of {Limit} verification-code emails reached; not sending more today", limit.GlobalDaily);
            }
            else
            {
                logger.LogWarning("Daily ceiling of {Limit} account-recovery emails reached; not sending more today", limit.GlobalDaily);
            }
            return new Decision(false, dayStart.AddDays(1));
        }

        dbContext.AuthEmailDispatches.Add(AuthEmailDispatch.Create(userId, kind, now));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new Decision(true, now + TimeSpan.FromSeconds(limit.CooldownSeconds));
    }
}
