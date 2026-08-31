using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.EmailIntegrations;

internal sealed partial class EmailForwardingService(
    AppDbContext dbContext,
    IEmailClassificationProvider emailClassificationProvider,
    IEmailJobExtractionProvider emailJobExtractionProvider,
    IApplicationService applicationService,
    IJobBoardDomainMatcher jobBoardDomainMatcher,
    IOptions<EmailForwardingOptions> options,
    IOptions<EmailIntelligenceOptions> intelligenceOptions,
    ILogger<EmailForwardingService> logger) : IEmailForwardingService
{
    private const string TokenChars = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const int TokenLength = 12;

    // Gmail's real sender/subject for its own forwarding-confirmation email — narrow allowlist
    // (both must match) so this can never misfire on a real recruiter email. Verified against a
    // live confirmation email (2026-08-31): From is exactly forwarding-noreply@google.com; Subject
    // is literally "(Gmail Forwarding Confirmation - Receive Mail from <address>" — Gmail's own
    // subject starts with an unmatched "(" (confirmed via base64-dumped wrangler tail output, not a
    // logging/encoding artifact), so this checks Contains rather than StartsWith.
    private const string GmailConfirmationSenderEmail = "forwarding-noreply@google.com";
    private const string GmailConfirmationSubjectMarker = "Gmail Forwarding Confirmation";
    
    private static readonly Regex GmailConfirmationCodeRegex = GmailConfirmationCode_Regex();
    private static readonly Regex GmailConfirmationLinkRegex = GmailConfirmationLink_Regex();

    public async Task<InboundAddressResponse> GetOrCreateInboundAddressAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.EmailConnections
            .Where(c => c.UserId == userId && c.Provider == EmailProvider.Forwarding)
            .Select(c => new
            {
                c.ProviderAccountEmail, c.GmailConfirmationCode, c.GmailConfirmationLink, c.GmailConfirmationReceivedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return new InboundAddressResponse(existing.ProviderAccountEmail, existing.GmailConfirmationCode,
                existing.GmailConfirmationLink, existing.GmailConfirmationReceivedAt);
        }

        var domain = options.Value.Domain;
        var token = RandomNumberGenerator.GetString(TokenChars, TokenLength);
        var address = $"{token}@{domain}";
        var now = DateTimeOffset.UtcNow;

        dbContext.EmailConnections.Add(EmailConnection.CreateForwarding(userId, token, address, now));
        await dbContext.SaveChangesAsync(cancellationToken);

        return new InboundAddressResponse(address, GmailConfirmationCode: null, GmailConfirmationLink: null,
            GmailConfirmationReceivedAt: null);
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

        if (IsGmailForwardingConfirmation(request.FromEmail, request.Subject))
        {
            connection.SetGmailConfirmation(
                ExtractGmailConfirmationCode(request.Snippet),
                ExtractGmailConfirmationLink(request.Snippet),
                request.ReceivedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
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

        var senderDomain = ExtractDomain(request.FromEmail);

        // isKnownSender no longer hard-gates the LLM call (see RecruitmentSignalAnalyzer) — kept
        // here purely so the routing log line below can show it alongside the new score-based
        // decision.
        var isKnownSender = applicationId is not null || jobBoardDomainMatcher.IsKnown(senderDomain);

        var classification = await ClassifyAsync(request.FromEmail, request.Subject, request.Snippet,
            senderDomain, applicationId is not null, request.LinkDomains, isKnownSender, cancellationToken);

        // ApplicationReceived only counts as a signal for an *unmatched* sender — a "we got your
        // application" acknowledgement about an application we already have on file is content-free
        // (the app is already sitting at Applied), so it shouldn't produce a "confirm Applied"
        // suggestion nobody asked for. See RuleBasedEmailClassifier's own comment on this rule.
        var hasSignal = classification.SuggestedStatus is not null
            || classification.MatchedRule == "StillWaiting"
            || (classification.MatchedRule == "ApplicationReceived" && applicationId is null);

        if (!hasSignal)
        {
            return; // nothing about the email is classifiable — matched or not, there's no signal to act on
        }

        var now = DateTimeOffset.UtcNow;

        if (applicationId is not null)
        {
            dbContext.EmailSuggestions.Add(EmailSuggestion.Create(
                connection.UserId, connection.Id, applicationId.Value,
                providerMessageId, providerThreadId: null,
                classification.SuggestedStatus, classification.ConfidenceScore, classification.MatchedRule,
                senderDomain, request.ReceivedAt, now, request.Subject, request.Snippet));

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Unmatched: the job isn't registered in the app yet. Only worth extracting company/job-title
        // detail (an extra LLM call) now that we know the email carries a real status signal — a
        // signal-less unmatched email (newsletter, unrelated mail) was already returned above, same
        // as before this "new job" flow existed (DECISIONS.md "Eşleşmeyen email'ler gösterilmiyor").
        var extraction = await emailJobExtractionProvider.ExtractAsync(request.Subject, request.Snippet, cancellationToken);
        if (extraction is null)
        {
            return; // couldn't confidently read a company name + job title — stay silent, don't guess
        }

        dbContext.EmailSuggestions.Add(EmailSuggestion.CreateForNewJob(
            connection.UserId, connection.Id, providerMessageId,
            classification.SuggestedStatus, classification.ConfidenceScore, classification.MatchedRule,
            senderDomain, request.ReceivedAt, now, request.Subject, request.Snippet,
            extraction.CompanyName, extraction.JobTitle, extraction.Location, extraction.Description));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetPendingSuggestionCountAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.EmailSuggestions
            .CountAsync(s => s.UserId == userId && s.Status == EmailSuggestionStatus.Pending, cancellationToken);

    public async Task<IReadOnlyList<EmailSuggestionResponse>> GetPendingSuggestionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await dbContext.EmailSuggestions
            .Where(s => s.UserId == userId && s.Status == EmailSuggestionStatus.Pending && s.ApplicationId != null)
            .Join(dbContext.Applications, s => s.ApplicationId, a => a.Id, (s, a) => new { s, a.JobTitle, a.CompanyId })
            .Join(dbContext.Companies, x => x.CompanyId, c => c.Id, (x, c) => new { x.s, x.JobTitle, CompanyName = c.Name })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var responses = rows.Select(row => new EmailSuggestionResponse(
            row.s.Id, row.s.ApplicationId, row.CompanyName, row.JobTitle,
            row.s.SuggestedStatus, row.s.ConfidenceScore, row.s.Subject ?? "", row.s.Snippet ?? "", row.s.EmailReceivedAt))
            .ToList();

        // "New job" suggestions (ApplicationId is null) always come from this Forwarding path, so
        // Subject/Snippet/Extracted* are always already persisted.
        var newJobRows = await dbContext.EmailSuggestions
            .Where(s => s.UserId == userId && s.Status == EmailSuggestionStatus.Pending && s.ApplicationId == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        responses.AddRange(newJobRows.Select(s => new EmailSuggestionResponse(
            s.Id, ApplicationId: null, s.ExtractedCompanyName ?? "", s.ExtractedJobTitle ?? "",
            s.SuggestedStatus, s.ConfidenceScore, s.Subject ?? "", s.Snippet ?? "", s.EmailReceivedAt,
            IsNewApplicationSuggestion: true, s.ExtractedLocation, s.ExtractedDescription)));
        
        return [.. responses.OrderByDescending(r => r.EmailReceivedAt)];
    }

    public async Task<ConfirmSuggestionResult> ConfirmSuggestionAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await dbContext.EmailSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.UserId == userId, cancellationToken);

        if (suggestion is null || suggestion.Status != EmailSuggestionStatus.Pending)
        {
            return ConfirmSuggestionResult.NotFound;
        }

        if (suggestion.ApplicationId is null)
        {
            // "New job" suggestion: the Application (and its Company, via CreateAsync's own
            // ICompanyResolver call) doesn't exist yet — confirming creates it now, tagged
            // Source.Email so the user can see it was registered from a forwarded email.
            var created = await applicationService.CreateAsync(userId, new CreateApplicationRequest(
                suggestion.ExtractedCompanyName!, suggestion.ExtractedJobTitle!, JobUrl: null,
                suggestion.ExtractedLocation, EmploymentType.FullTime, suggestion.EmailReceivedAt,
                Source.Email, suggestion.ExtractedDescription), cancellationToken);

            if (suggestion.SuggestedStatus is not null && suggestion.SuggestedStatus != ApplicationStatus.Applied)
            {
                await applicationService.ChangeStatusAsync(userId, created.Id,
                    new ChangeStatusRequest(suggestion.SuggestedStatus.Value, "E-postadan içe aktarıldı",
                        suggestion.EmailReceivedAt, Source.Email),
                    cancellationToken);
            }

            suggestion.Confirm(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ConfirmSuggestionResult.Confirmed;
        }

        if (suggestion.SuggestedStatus is null)
        {
            return ConfirmSuggestionResult.NoStatusToConfirm;
        }

        var changed = await applicationService.ChangeStatusAsync(userId, suggestion.ApplicationId.Value,
            new ChangeStatusRequest(suggestion.SuggestedStatus.Value, "E-postadan onaylandı", suggestion.EmailReceivedAt, Source.Email),
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

    public async Task<bool> DismissGmailConfirmationAsync(Guid userId, CancellationToken cancellationToken)
    {
        var connection = await dbContext.EmailConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == EmailProvider.Forwarding, cancellationToken);

        if (connection is null || connection.GmailConfirmationCode is null && connection.GmailConfirmationLink is null)
        {
            return false;
        }

        connection.ClearGmailConfirmation();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<EmailClassificationResult> ClassifyAsync(string senderEmail, string subject, string snippet,
        string? senderDomain, bool hasApplicationMatch, IReadOnlyList<string> linkDomains, bool isKnownSender,
        CancellationToken cancellationToken)
    {
        var classification = RuleBasedEmailClassifier.Classify(subject, snippet);
        if (classification.MatchedRule != "NoMatch")
        {
            return classification;
        }

        var intelligence = intelligenceOptions.Value;
        var analysis = RecruitmentSignalAnalyzer.Analyze(senderEmail, subject, snippet, senderDomain,
            jobBoardDomainMatcher.IsKnown(senderDomain), hasApplicationMatch, linkDomains, intelligence);

        var bucket = analysis.Score switch
        {
            var s when s < intelligence.LowThreshold => "ClearlyIrrelevant",
            var s when s < intelligence.LlmThreshold => "Weak",
            var s when s < intelligence.HighConfidenceThreshold => "Possible",
            _ => "Strong"
        };

        logger.LogInformation(
            "Email intelligence routing: score={Score} bucket={Bucket} categories={Categories} isKnownSender={IsKnownSender}",
            analysis.Score, bucket, string.Join(",", analysis.Signals.Select(s => s.Category)), isKnownSender);

        if (analysis.Score < intelligence.LlmThreshold)
        {
            logger.LogDebug("Skipping LLM classification: recruitment signal score is below the LLM threshold.");
            return classification;
        }

        return await emailClassificationProvider.ClassifyAsync(subject, snippet, cancellationToken);
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

    private static bool IsGmailForwardingConfirmation(string fromEmail, string subject) =>
        string.Equals(fromEmail.Trim(), GmailConfirmationSenderEmail, StringComparison.OrdinalIgnoreCase) &&
        subject.Contains(GmailConfirmationSubjectMarker, StringComparison.OrdinalIgnoreCase);

    private static string? ExtractGmailConfirmationCode(string snippet)
    {
        var match = GmailConfirmationCodeRegex.Match(snippet);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? ExtractGmailConfirmationLink(string snippet)
    {
        var match = GmailConfirmationLinkRegex.Match(snippet);
        return match.Success ? match.Value : null;
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

    [GeneratedRegex(@"\b(\d{6,8})\b", RegexOptions.Compiled)]
    private static partial Regex GmailConfirmationCode_Regex();
    [GeneratedRegex(@"https?://\S*google\.com\S*", RegexOptions.Compiled)]
    private static partial Regex GmailConfirmationLink_Regex();
}
