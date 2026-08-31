using System.Security.Cryptography;
using System.Text;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Companies;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.EmailIntegrations;

internal sealed class EmailForwardingService(
    AppDbContext dbContext,
    IEmailClassificationProvider emailClassificationProvider,
    IOptions<EmailForwardingOptions> options,
    ILogger<EmailForwardingService> logger) : IEmailForwardingService
{
    private const string TokenChars = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int TokenLength = 12;

    public async Task<string> GetOrCreateInboundAddressAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.EmailConnections
            .Where(c => c.UserId == userId && c.Provider == EmailProvider.Forwarding)
            .Select(c => c.ProviderAccountEmail)
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var domain = options.Value.Domain;
        var token = RandomNumberGenerator.GetString(TokenChars, TokenLength);
        var address = $"{token}@{domain}";
        var now = DateTimeOffset.UtcNow;

        dbContext.EmailConnections.Add(EmailConnection.CreateForwarding(userId, token, address, now));
        await dbContext.SaveChangesAsync(cancellationToken);

        return address;
    }

    public async Task ProcessInboundEmailAsync(InboundEmailRequest request, CancellationToken cancellationToken)
    {
        var token = ExtractLocalPart(request.ToAddress);
        var connection = token is null
            ? null
            : await dbContext.EmailConnections
                .FirstOrDefaultAsync(c => c.Provider == EmailProvider.Forwarding && c.InboundToken == token, cancellationToken);

        if (connection is null)
        {
            logger.LogWarning("Inbound email received for an unrecognized forwarding address.");
            return;
        }

        var providerMessageId = ComputeIdempotencyKey(request);
        var alreadyProcessed = await dbContext.EmailSuggestions
            .AnyAsync(s => s.EmailConnectionId == connection.Id && s.ProviderMessageId == providerMessageId, cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        var candidates = await BuildCandidatesAsync(connection.UserId, cancellationToken);

        // Forwarded mail's original sender is the company; there's no "self-sent" concept here since
        // the forwarding hop (the user's own filter) already happened before this ever reached us.
        var applicationId = EmailApplicationMatcher.Match(
            request.FromEmail, request.FromDisplayName, recipientEmail: "", ownAccountEmail: "",
            request.Subject, candidates);

        if (applicationId is null)
        {
            return; // unmatched emails are not surfaced (product decision, same as the Gmail path)
        }

        var classification = RuleBasedEmailClassifier.Classify(request.Subject, request.Snippet);
        if (classification.MatchedRule == "NoMatch")
        {
            classification = await emailClassificationProvider.ClassifyAsync(request.Subject, request.Snippet, cancellationToken);
        }

        if (classification.SuggestedStatus is null && classification.MatchedRule != "StillWaiting")
        {
            return; // matched an application, but nothing about the email is classifiable
        }

        var senderDomain = ExtractDomain(request.FromEmail);
        var now = DateTimeOffset.UtcNow;

        dbContext.EmailSuggestions.Add(EmailSuggestion.Create(
            connection.UserId, connection.Id, applicationId.Value,
            providerMessageId, providerThreadId: null,
            classification.SuggestedStatus, classification.ConfidenceScore, classification.MatchedRule,
            senderDomain, request.ReceivedAt, now, request.Subject, request.Snippet));

        await dbContext.SaveChangesAsync(cancellationToken);
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

    private static string? ExtractLocalPart(string address)
    {
        var atIndex = address.IndexOf('@');
        return atIndex > 0 ? address[..atIndex].Trim().ToLowerInvariant() : null;
    }

    private static string ComputeIdempotencyKey(InboundEmailRequest request)
    {
        var raw = $"{request.ToAddress}|{request.FromEmail}|{request.Subject}|{request.ReceivedAt:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash);
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

    private static string? ExtractDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 && atIndex < email.Length - 1
            ? email[(atIndex + 1)..].Trim().ToLowerInvariant()
            : null;
    }
}
