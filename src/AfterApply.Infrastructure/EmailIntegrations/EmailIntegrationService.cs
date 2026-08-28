using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Common;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.Identity;
using AfterApply.Infrastructure.Persistence;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Gmail.v1;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AfterApply.Infrastructure.EmailIntegrations;

internal sealed class EmailIntegrationService(
    AppDbContext dbContext,
    IGmailClient gmailClient,
    IDataProtectionProvider dataProtectionProvider,
    IApplicationService applicationService,
    IOptions<GoogleOAuthOptions> googleOptions,
    IOptions<JwtOptions> jwtOptions,
    HybridCache cache) : IEmailIntegrationService
{
    private const string StatePurpose = "gmail-oauth-state";

    private static readonly HybridCacheEntryOptions ConnectionStatusCacheOptions = new()
    {
        Expiration = TimeSpan.FromSeconds(20),
        LocalCacheExpiration = TimeSpan.FromSeconds(20)
    };

    private static string ConnectionStatusCacheKey(Guid userId) => $"email:connection-status:{userId}";

    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("AfterApply.EmailIntegrations.RefreshToken");

    public Task<string> BuildAuthorizationUrlAsync(Guid userId, CancellationToken cancellationToken)
    {
        var opts = googleOptions.Value;
        if (!opts.IsConfigured)
        {
            throw new CodedException("EMAIL_INTEGRATION_OAUTH_NOT_CONFIGURED",
                "Google OAuth is not configured. See README.md 'Gmail Integration Setup'.");
        }

        var state = CreateState(userId, DateTimeOffset.UtcNow);

        var flow = new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = new ClientSecrets { ClientId = opts.ClientId, ClientSecret = opts.ClientSecret },
            Scopes = [GmailService.Scope.GmailReadonly]
        });

        var request = (GoogleAuthorizationCodeRequestUrl)flow.CreateAuthorizationCodeRequest(opts.RedirectUri);
        request.State = state;
        request.AccessType = "offline";
        request.Prompt = "consent";

        return Task.FromResult(request.Build().ToString());
    }

    public async Task<EmailConnectionCallbackResult> HandleCallbackAsync(string code, string state, CancellationToken cancellationToken)
    {
        var userId = await ValidateStateAsync(state);
        if (userId is null)
        {
            return new EmailConnectionCallbackResult(false, "invalid_state");
        }

        var opts = googleOptions.Value;
        GoogleTokenResponse tokenResponse;
        try
        {
            tokenResponse = await gmailClient.ExchangeCodeAsync(code, opts.RedirectUri, cancellationToken);
        }
        catch
        {
            return new EmailConnectionCallbackResult(false, "token_exchange_failed");
        }

        var profile = await gmailClient.GetProfileAsync(new UserCredentialToken(tokenResponse.RefreshToken), cancellationToken);
        var encryptedToken = _protector.Protect(tokenResponse.RefreshToken);
        var now = DateTimeOffset.UtcNow;

        var connection = await dbContext.EmailConnections
            .FirstOrDefaultAsync(c => c.UserId == userId.Value && c.Provider == EmailProvider.Gmail, cancellationToken);

        if (connection is null)
        {
            dbContext.EmailConnections.Add(EmailConnection.Create(
                userId.Value, EmailProvider.Gmail, profile.EmailAddress, encryptedToken,
                GmailService.Scope.GmailReadonly, now));
        }
        else
        {
            connection.Reconnect(encryptedToken, profile.EmailAddress, GmailService.Scope.GmailReadonly, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(ConnectionStatusCacheKey(userId.Value), cancellationToken);

        return new EmailConnectionCallbackResult(true, null);
    }

    public Task<EmailConnectionStatusResponse> GetConnectionStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        return cache.GetOrCreateAsync(
            ConnectionStatusCacheKey(userId),
            userId,
            async (uid, ct) =>
            {
                var connection = await dbContext.EmailConnections
                    .Where(c => c.UserId == uid && c.Provider == EmailProvider.Gmail)
                    .Select(c => new { c.DisconnectedAt, c.ProviderAccountEmail, c.LastSyncedAt, c.LastSyncError })
                    .FirstOrDefaultAsync(ct);

                if (connection is null)
                {
                    return new EmailConnectionStatusResponse(false, null, null, false);
                }

                var connected = connection.DisconnectedAt is null;
                return new EmailConnectionStatusResponse(
                    connected,
                    connected ? connection.ProviderAccountEmail : null,
                    connection.LastSyncedAt,
                    NeedsReattention: connected && connection.LastSyncError is not null);
            },
            ConnectionStatusCacheOptions,
            cancellationToken: cancellationToken).AsTask();
    }

    public async Task<bool> DisconnectAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.EmailConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == EmailProvider.Gmail, cancellationToken);

        if (connection is null || connection.DisconnectedAt is not null)
        {
            return false;
        }

        if (connection.EncryptedRefreshToken is not null)
        {
            try
            {
                await gmailClient.RevokeAsync(_protector.Unprotect(connection.EncryptedRefreshToken), cancellationToken);
            }
            catch
            {
                // Best-effort: local disconnect proceeds even if Google's revoke endpoint is unreachable.
            }
        }

        connection.Disconnect(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        await cache.RemoveAsync(ConnectionStatusCacheKey(userId), cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<EmailSuggestionResponse>> GetPendingSuggestionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        // AsNoTracking matters here (unlike most other read paths in this codebase): the final
        // projection below carries the whole EmailSuggestion entity (`s`) through, not just scalar
        // fields, so EF would otherwise snapshot every row for change tracking it never uses.
        var rows = await dbContext.EmailSuggestions
            .Where(s => s.UserId == userId && s.Status == EmailSuggestionStatus.Pending)
            .Join(dbContext.Applications, s => s.ApplicationId, a => a.Id, (s, a) => new { s, a.JobTitle, a.CompanyId })
            .Join(dbContext.Companies, x => x.CompanyId, c => c.Id, (x, c) => new { x.s, x.JobTitle, CompanyName = c.Name })
            .Join(dbContext.EmailConnections, x => x.s.EmailConnectionId, ec => ec.Id,
                (x, ec) => new { x.s, x.JobTitle, x.CompanyName, ec.EncryptedRefreshToken })
            .OrderByDescending(x => x.s.EmailReceivedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var responses = new List<EmailSuggestionResponse>(rows.Count);

        foreach (var row in rows)
        {
            var (subject, snippet) = ("", "");

            if (row.EncryptedRefreshToken is not null)
            {
                var token = new UserCredentialToken(_protector.Unprotect(row.EncryptedRefreshToken));
                var detail = await gmailClient.GetMessageDetailAsync(token, row.s.ProviderMessageId, cancellationToken);
                if (detail is not null)
                {
                    subject = detail.Subject;
                    snippet = detail.Snippet;
                }
            }

            responses.Add(new EmailSuggestionResponse(
                row.s.Id, row.s.ApplicationId, row.CompanyName, row.JobTitle,
                row.s.SuggestedStatus, row.s.ConfidenceScore, subject, snippet, row.s.EmailReceivedAt));
        }

        return responses;
    }

    public async Task<ConfirmSuggestionResult> ConfirmSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await dbContext.EmailSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.UserId == userId, cancellationToken);

        if (suggestion is null || suggestion.Status != EmailSuggestionStatus.Pending)
        {
            return ConfirmSuggestionResult.NotFound;
        }

        if (suggestion.SuggestedStatus is null)
        {
            return ConfirmSuggestionResult.NoStatusToConfirm;
        }

        var changed = await applicationService.ChangeStatusAsync(userId, suggestion.ApplicationId,
            new ChangeStatusRequest(suggestion.SuggestedStatus.Value, "Gmail'den onaylandı", suggestion.EmailReceivedAt, Source.Email),
            cancellationToken);

        if (changed is null)
        {
            return ConfirmSuggestionResult.NotFound;
        }

        suggestion.Confirm(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ConfirmSuggestionResult.Confirmed;
    }

    public async Task<bool> DismissSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await dbContext.EmailSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.UserId == userId, cancellationToken);

        if (suggestion is null || suggestion.Status != EmailSuggestionStatus.Pending)
        {
            return false;
        }

        suggestion.Dismiss(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> SyncAllConnectionsAsync(CancellationToken cancellationToken)
    {
        var scanStartedAt = DateTimeOffset.UtcNow;

        var connections = await dbContext.EmailConnections
            .Where(c => c.DisconnectedAt == null)
            .ToListAsync(cancellationToken);

        var totalNewSuggestions = 0;

        foreach (var connection in connections)
        {
            try
            {
                totalNewSuggestions += await SyncConnectionAsync(connection, scanStartedAt, cancellationToken);
            }
            catch (Exception ex)
            {
                connection.RecordSyncFailure(ex.Message, scanStartedAt);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return totalNewSuggestions;
    }

    private async Task<int> SyncConnectionAsync(EmailConnection connection, DateTimeOffset scanStartedAt, CancellationToken cancellationToken)
    {
        if (connection.EncryptedRefreshToken is null)
        {
            return 0;
        }

        var token = new UserCredentialToken(_protector.Unprotect(connection.EncryptedRefreshToken));
        var since = connection.LastSyncedAt ?? scanStartedAt.AddDays(-30);
        var messages = await gmailClient.ListMessagesSinceAsync(token, since, cancellationToken);

        if (messages.Count == 0)
        {
            connection.UpdateAfterSync(scanStartedAt);
            return 0;
        }

        var existingMessageIds = await dbContext.EmailSuggestions
            .Where(s => s.EmailConnectionId == connection.Id)
            .Select(s => s.ProviderMessageId)
            .ToListAsync(cancellationToken);
        var existingSet = existingMessageIds.ToHashSet();

        var newMessages = messages.Where(m => !existingSet.Contains(m.MessageId)).ToList();

        if (newMessages.Count == 0)
        {
            connection.UpdateAfterSync(scanStartedAt);
            return 0;
        }

        var candidates = await BuildCandidatesAsync(connection.UserId, cancellationToken);
        var newSuggestions = new List<EmailSuggestion>();

        foreach (var message in newMessages)
        {
            var applicationId = EmailApplicationMatcher.Match(
                message.SenderEmail, message.SenderDisplayName, message.Subject, candidates);

            if (applicationId is null)
            {
                continue; // unmatched emails are not surfaced (product decision)
            }

            var classification = EmailClassifier.Classify(message.Subject, message.Snippet);
            if (classification.MatchedRule == "NoMatch")
            {
                continue; // matched an application, but nothing about the email is classifiable
            }

            newSuggestions.Add(EmailSuggestion.Create(
                connection.UserId, connection.Id, applicationId.Value,
                message.MessageId, message.ThreadId,
                classification.SuggestedStatus, classification.ConfidenceScore, classification.MatchedRule,
                ExtractSenderDomain(message.SenderEmail), message.ReceivedAt, scanStartedAt));
        }

        if (newSuggestions.Count > 0)
        {
            dbContext.EmailSuggestions.AddRange(newSuggestions);
        }

        connection.UpdateAfterSync(scanStartedAt);
        return newSuggestions.Count;
    }

    private async Task<IReadOnlyList<ApplicationMatchCandidate>> BuildCandidatesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.Applications
            .Where(a => a.UserId == userId && !TerminalApplicationStatuses.Values.Contains(a.Status))
            .Join(dbContext.Companies, a => a.CompanyId, c => c.Id, (a, c) => new { a.Id, c.Name, c.Website })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new ApplicationMatchCandidate(
                r.Id, CompanyNameNormalizer.Normalize(r.Name), ExtractDomainFromWebsite(r.Website)))
            .ToList();
    }

    private static string? ExtractDomainFromWebsite(string? website)
    {
        if (string.IsNullOrWhiteSpace(website))
        {
            return null;
        }

        var candidate = website.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
    }

    private static string? ExtractSenderDomain(string senderEmail)
    {
        var atIndex = senderEmail.IndexOf('@');
        return atIndex >= 0 && atIndex < senderEmail.Length - 1
            ? senderEmail[(atIndex + 1)..].Trim().ToLowerInvariant()
            : null;
    }

    private string CreateState(Guid userId, DateTimeOffset now)
    {
        var jwt = jwtOptions.Value;
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKey)), SecurityAlgorithms.HmacSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(10).UtcDateTime,
            SigningCredentials = signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                ["purpose"] = StatePurpose
            }
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private async Task<Guid?> ValidateStateAsync(string state)
    {
        var jwt = jwtOptions.Value;
        var handler = new JsonWebTokenHandler();

        var result = await handler.ValidateTokenAsync(state, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        });

        if (!result.IsValid)
        {
            return null;
        }

        var purpose = result.ClaimsIdentity?.FindFirst("purpose")?.Value;
        if (purpose != StatePurpose)
        {
            return null;
        }

        var sub = result.ClaimsIdentity?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return sub is not null && Guid.TryParse(sub, out var userId) ? userId : null;
    }
}
