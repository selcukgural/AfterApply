using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Mailing;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Email verification by a six-digit code (2026-09-24). The browser that signed up (or signed in to
/// an unverified account) holds a ticket; the inbox receives the code; only the two together
/// verify. That pairing is the point: an address typed in by someone who does not own it can never
/// be verified by them, because the code goes to its owner, and the owner can do nothing with the
/// code, because the ticket stays in the other person's browser.
/// </summary>
public sealed class EmailVerificationService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    AuthEmailThrottle throttle,
    IBackgroundJobClient jobClient,
    IOptions<EmailVerificationOptions> options,
    ILogger<EmailVerificationService> logger,
    TimeProvider? timeProvider = null)
{
    public const string TicketExpired = "AUTH_VERIFICATION_EXPIRED";
    public const string CodeExpired = "AUTH_VERIFICATION_CODE_EXPIRED";
    public const string CodeInvalid = "AUTH_VERIFICATION_CODE_INVALID";

    private readonly EmailVerificationOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Hands the browser a fresh ticket for the account's one challenge and, unless the
    /// throttle says it is too soon, emails a new code. A code already sent keeps working with the
    /// new ticket, so a throttled start is not a dead end.</summary>
    public async Task<EmailVerificationPendingResponse> StartAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var ticket = NewTicket();
        var ticketHash = Sha256Hex(ticket);

        var challenge = await dbContext.EmailVerificationChallenges.SingleOrDefaultAsync(c => c.UserId == user.Id, cancellationToken);
        if (challenge is null)
        {
            challenge = EmailVerificationChallenge.Create(user.Id, ticketHash, now, _options.TicketLifetime);
            dbContext.EmailVerificationChallenges.Add(challenge);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Two sign-ins raced and the other one created the account's challenge first (the
                // index allows one per account): take that one over instead.
                dbContext.Entry(challenge).State = EntityState.Detached;
                challenge = await dbContext.EmailVerificationChallenges.SingleAsync(c => c.UserId == user.Id, cancellationToken);
                challenge.ReplaceTicket(ticketHash, now, _options.TicketLifetime);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            challenge.ReplaceTicket(ticketHash, now, _options.TicketLifetime);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var resendAvailableAt = await SendCodeIfAllowedAsync(user, challenge.Id, cancellationToken);
        return new EmailVerificationPendingResponse(ticket, user.Email!, resendAvailableAt);
    }

    public async Task<(DateTimeOffset? ResendAvailableAt, string? Error)> ResendAsync(string ticket, CancellationToken cancellationToken)
    {
        var challenge = await FindLiveChallengeAsync(ticket, cancellationToken);
        if (challenge is null)
        {
            return (null, TicketExpired);
        }

        var user = await userManager.FindByIdAsync(challenge.UserId.ToString());
        if (user is null)
        {
            return (null, TicketExpired);
        }

        return (await SendCodeIfAllowedAsync(user, challenge.Id, cancellationToken), null);
    }

    /// <summary>The account, now verified, or the error code. The challenge is gone after a
    /// success, so a ticket verifies once.</summary>
    public async Task<(ApplicationUser? User, string? Error)> VerifyAsync(string ticket, string code, CancellationToken cancellationToken)
    {
        var challenge = await FindLiveChallengeAsync(ticket, cancellationToken);
        if (challenge is null)
        {
            return (null, TicketExpired);
        }

        var now = _timeProvider.GetUtcNow();
        if (challenge.CodeHash is null
            || challenge.CodeIssuedAt is null
            || challenge.CodeIssuedAt.Value + _options.CodeLifetime <= now
            || challenge.FailedAttempts >= _options.MaxAttemptsPerCode)
        {
            return (null, CodeExpired);
        }

        var expected = Encoding.ASCII.GetBytes(challenge.CodeHash);
        var actual = Encoding.ASCII.GetBytes(CodeHash(challenge.Id, code));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            challenge.RecordFailedAttempt();
            await dbContext.SaveChangesAsync(cancellationToken);
            return (null, challenge.FailedAttempts >= _options.MaxAttemptsPerCode ? CodeExpired : CodeInvalid);
        }

        var user = await userManager.FindByIdAsync(challenge.UserId.ToString());
        if (user is null)
        {
            return (null, TicketExpired);
        }

        user.EmailConfirmed = true;
        dbContext.EmailVerificationChallenges.Remove(challenge);
        await userManager.UpdateAsync(user);

        logger.LogInformation("User {UserId} verified their email address", user.Id);
        return (user, null);
    }

    /// <summary>Drops the account's pending verification, so no ticket handed out before this point
    /// can finish it. Called whenever the account's credentials are replaced.</summary>
    public Task DiscardAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.EmailVerificationChallenges.Where(c => c.UserId == userId).ExecuteDeleteAsync(cancellationToken);

    private async Task<DateTimeOffset> SendCodeIfAllowedAsync(ApplicationUser user, Guid challengeId, CancellationToken cancellationToken)
    {
        var decision = await throttle.TryReserveAsync(user.Id, AuthEmailKind.EmailVerification, cancellationToken);
        if (decision.Allowed)
        {
            var locale = user.PreferredLanguage is { Length: > 0 } language ? language : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            jobClient.Enqueue<IEmailVerificationCodeSender>(s => s.SendAsync(challengeId, locale, CancellationToken.None));
        }

        return decision.NextAllowedAt;
    }

    private async Task<EmailVerificationChallenge?> FindLiveChallengeAsync(string ticket, CancellationToken cancellationToken)
    {
        var ticketHash = Sha256Hex(ticket);
        var now = _timeProvider.GetUtcNow();
        return await dbContext.EmailVerificationChallenges
            .SingleOrDefaultAsync(c => c.TicketHash == ticketHash && c.ExpiresAt > now, cancellationToken);
    }

    private static string NewTicket() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Salted with the challenge id so equal codes on two accounts hash differently.</summary>
    internal static string CodeHash(Guid challengeId, string code) => Sha256Hex($"{challengeId:N}:{code}");

    private static string Sha256Hex(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

/// <summary>The Hangfire job behind <see cref="EmailVerificationService"/>: mints the code, stores its
/// hash, emails it. A retry mints a new code, which only makes the earlier email's code stale.</summary>
internal sealed class EmailVerificationCodeSender(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IEmailSender emailSender,
    TimeProvider? timeProvider = null) : IEmailVerificationCodeSender
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task SendAsync(Guid challengeId, string locale, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var challenge = await dbContext.EmailVerificationChallenges
            .SingleOrDefaultAsync(c => c.Id == challengeId && c.ExpiresAt > now, cancellationToken);
        if (challenge is null)
        {
            return;
        }

        var user = await userManager.FindByIdAsync(challenge.UserId.ToString());
        if (user?.Email is null || user.EmailConfirmed)
        {
            return;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        challenge.IssueCode(EmailVerificationService.CodeHash(challenge.Id, code), now);
        await dbContext.SaveChangesAsync(cancellationToken);

        await emailSender.SendEmailVerificationCodeAsync(user.Email, code, locale, cancellationToken);
    }
}

/// <summary>See <see cref="IUnverifiedAccountCleanupService"/>. "Never verified" is also "never
/// signed in": an account that got a session before verification existed (it has a refresh-token
/// row) is kept — it verifies at its next sign-in instead of losing its data.</summary>
internal sealed class UnverifiedAccountCleanupService(
    AppDbContext dbContext,
    IOptions<EmailVerificationOptions> options,
    ILogger<UnverifiedAccountCleanupService> logger,
    TimeProvider? timeProvider = null) : IUnverifiedAccountCleanupService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var cutoff = now.AddDays(-options.Value.UnverifiedAccountRetentionDays);

        // Every user-owned table cascades from Users (see DeleteAccountAsync), and an account that
        // never signed in owns nothing but its login rows and its challenge.
        var deleted = await dbContext.Users
            .Where(u => !u.EmailConfirmed && u.CreatedAt < cutoff && !dbContext.RefreshTokens.Any(rt => rt.UserId == u.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.EmailVerificationChallenges.Where(c => c.ExpiresAt < now).ExecuteDeleteAsync(cancellationToken);
        await dbContext.AuthEmailDispatches.Where(d => d.SentAt < now.AddDays(-2)).ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} accounts that never verified their email address", deleted);
        }

        return deleted;
    }
}
