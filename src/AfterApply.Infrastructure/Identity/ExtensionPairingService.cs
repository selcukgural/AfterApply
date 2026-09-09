using AfterApply.Application.Common;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Identity;

internal sealed class ExtensionPairingService(
    AppDbContext dbContext,
    ITokenService tokenService,
    IPersonalAccessTokenService personalAccessTokens,
    IOptions<ExtensionPairingOptions> options,
    IOptions<AppOptions> appOptions) : IExtensionPairingService
{
    /// <summary>The name the minted token carries in the settings list. English like every other
    /// stored value, and stable, so a second pairing is recognisable as a second browser rather
    /// than as a mystery.</summary>
    private const string TokenName = "Browser Extension";

    /// <summary>The languages the verification page exists in. Kept here rather than read from the
    /// frontend for the same reason SiteTrafficNormalizer keeps its own copy: this layer does not
    /// depend on the web app.</summary>
    private static readonly HashSet<string> Locales = new(StringComparer.OrdinalIgnoreCase) { "tr", "en" };

    private const string DefaultLocale = "tr";

    public async Task<StartedExtensionPairingResponse> StartAsync(
        StartExtensionPairingRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // Sweeping here rather than from a recurring job: the table only grows when someone starts
        // a pairing, the rows are worthless within minutes, and a Hangfire schedule for a delete
        // this small would be more moving parts than the problem has. Bounded by design — an hour
        // past expiry is well beyond anything a client can still be polling for.
        await dbContext.ExtensionPairingRequests
            .Where(r => r.ExpiresAt < now.AddHours(-1))
            .ExecuteDeleteAsync(cancellationToken);

        var deviceSecret = tokenService.GenerateExtensionPairingSecret();
        var pairing = ExtensionPairingRequest.Create(
            await GenerateUnusedCodeAsync(cancellationToken),
            tokenService.HashExtensionPairingSecret(deviceSecret),
            now,
            options.Value.Lifetime);

        dbContext.ExtensionPairingRequests.Add(pairing);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new StartedExtensionPairingResponse(
            pairing.Code,
            deviceSecret,
            pairing.ExpiresAt,
            VerificationUrl(pairing.Code, request.Locale),
            options.Value.PollIntervalSeconds);
    }

    public async Task<ExtensionPairingPollResponse?> PollAsync(string deviceSecret, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var secretHash = tokenService.HashExtensionPairingSecret(deviceSecret);

        var pairing = await dbContext.ExtensionPairingRequests
            .FirstOrDefaultAsync(r => r.DeviceSecretHash == secretHash, cancellationToken);

        if (pairing is null)
        {
            return null;
        }

        if (pairing.CompletedAt is not null)
        {
            // The token was handed over already. Not an error and not repeatable: an extension that
            // saved it and polled once more (a reload mid-flow) is told the flow is done, without a
            // second token being minted for it.
            return new ExtensionPairingPollResponse(ExtensionPairingStatus.Completed);
        }

        if (pairing.DeniedAt is not null)
        {
            return new ExtensionPairingPollResponse(ExtensionPairingStatus.Denied);
        }

        if (pairing.ExpiresAt <= now)
        {
            return new ExtensionPairingPollResponse(ExtensionPairingStatus.Expired);
        }

        if (pairing.ApprovedByUserId is not { } approvedByUserId)
        {
            return new ExtensionPairingPollResponse(ExtensionPairingStatus.Pending);
        }

        // Everything below is the collection step, and it is where the only credential in this flow
        // comes into existence. One transaction so that "this pairing is spent" and "this token
        // exists" are the same fact: if minting fails — the account is at its token cap — the claim
        // rolls back too and the next poll can try again once a token has been revoked.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // A conditional UPDATE, not a load-modify-save: it takes the row lock and reports whether it
        // was this caller who claimed the pairing, so two polls racing (a duplicated options tab)
        // cannot both mint a token.
        var claimed = await dbContext.ExtensionPairingRequests
            .Where(r => r.Id == pairing.Id && r.CompletedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.CompletedAt, now), cancellationToken);

        if (claimed == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ExtensionPairingPollResponse(ExtensionPairingStatus.Completed);
        }

        CreatedPersonalAccessTokenResponse token;
        try
        {
            token = await personalAccessTokens.CreateAsync(
                approvedByUserId,
                new CreatePersonalAccessTokenRequest(TokenName, PersonalAccessTokenScope.Extension),
                cancellationToken);
        }
        catch (CodedException exception) when (exception.ErrorCode == "PERSONAL_ACCESS_TOKEN_LIMIT_REACHED")
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ExtensionPairingPollResponse(ExtensionPairingStatus.TokenLimitReached);
        }

        await transaction.CommitAsync(cancellationToken);

        return new ExtensionPairingPollResponse(ExtensionPairingStatus.Completed, token.Token, token.ExpiresAt);
    }

    public async Task<ExtensionPairingReviewResponse?> GetForReviewAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = ExtensionPairingCode.Normalize(code);
        if (!ExtensionPairingCode.IsWellFormed(normalized))
        {
            return null;
        }

        var pairing = await dbContext.ExtensionPairingRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Code == normalized, cancellationToken);

        return pairing is null
            ? null
            : new ExtensionPairingReviewResponse(pairing.Code, pairing.ExpiresAt, StatusOf(pairing, DateTimeOffset.UtcNow));
    }

    public async Task<ExtensionPairingStatus?> ApproveAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var (pairing, now) = await LoadForDecisionAsync(code, cancellationToken);
        if (pairing is null)
        {
            return null;
        }

        if (!pairing.IsPendingAt(now))
        {
            return StatusOf(pairing, now);
        }

        pairing.Approve(userId, now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ExtensionPairingStatus.Approved;
    }

    public async Task<ExtensionPairingStatus?> DenyAsync(string code, CancellationToken cancellationToken)
    {
        var (pairing, now) = await LoadForDecisionAsync(code, cancellationToken);
        if (pairing is null)
        {
            return null;
        }

        // Denying takes no ownership check, and cannot: a pending request belongs to nobody yet.
        // That is the point — someone who lands on this page holding a code they did not expect is
        // exactly the person who should be able to shut it down.
        if (!pairing.IsPendingAt(now))
        {
            return StatusOf(pairing, now);
        }

        pairing.Deny(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ExtensionPairingStatus.Denied;
    }

    private async Task<(ExtensionPairingRequest? Pairing, DateTimeOffset Now)> LoadForDecisionAsync(
        string code, CancellationToken cancellationToken)
    {
        var normalized = ExtensionPairingCode.Normalize(code);
        if (!ExtensionPairingCode.IsWellFormed(normalized))
        {
            return (null, DateTimeOffset.UtcNow);
        }

        var pairing = await dbContext.ExtensionPairingRequests
            .FirstOrDefaultAsync(r => r.Code == normalized, cancellationToken);

        return (pairing, DateTimeOffset.UtcNow);
    }

    private static ExtensionPairingStatus StatusOf(ExtensionPairingRequest pairing, DateTimeOffset now)
    {
        if (pairing.CompletedAt is not null)
        {
            return ExtensionPairingStatus.Completed;
        }

        if (pairing.DeniedAt is not null)
        {
            return ExtensionPairingStatus.Denied;
        }

        if (pairing.ExpiresAt <= now)
        {
            return ExtensionPairingStatus.Expired;
        }

        return pairing.ApprovedAt is not null ? ExtensionPairingStatus.Approved : ExtensionPairingStatus.Pending;
    }

    /// <summary>Codes are short enough to collide by chance once enough of them are alive at the
    /// same time, and a collision would point two extensions at one row. Retried rather than left
    /// to the unique index, which would surface as a 500 on a flow whose whole promise is that it
    /// just works.</summary>
    private async Task<string> GenerateUnusedCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = ExtensionPairingCode.Generate();
            var taken = await dbContext.ExtensionPairingRequests
                .AnyAsync(r => r.Code == candidate, cancellationToken);

            if (!taken)
            {
                return candidate;
            }
        }

        // Five collisions in a row means the table is not what this design assumes (it holds
        // minutes of traffic, not a dictionary). Failing loudly beats handing out a duplicate.
        throw new InvalidOperationException("Could not generate an unused extension pairing code.");
    }

    private string VerificationUrl(string code, string? locale)
    {
        var resolved = locale is not null && Locales.Contains(locale) ? locale.ToLowerInvariant() : DefaultLocale;
        return $"{appOptions.Value.WebBaseUrl.TrimEnd('/')}/{resolved}/pair?code={code}";
    }
}
