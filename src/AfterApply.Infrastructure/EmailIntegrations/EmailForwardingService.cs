using System.Security.Cryptography;
using System.Text;
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

internal sealed class EmailForwardingService(
    AppDbContext dbContext,
    IEmailClassificationProvider emailClassificationProvider,
    IEmailJobExtractionProvider emailJobExtractionProvider,
    IEmailRejectionReasonExtractionProvider emailRejectionReasonExtractionProvider,
    IApplicationService applicationService,
    IJobBoardDomainMatcher jobBoardDomainMatcher,
    IOptions<EmailIntelligenceOptions> intelligenceOptions,
    IOptions<EmailAutoApprovalOptions> autoApprovalOptions,
    ILogger<EmailForwardingService> logger) : IEmailForwardingService
{
    public async Task ProcessExtensionSignalAsync(Guid userId, ExtensionEmailSignalRequest request, CancellationToken cancellationToken)
    {
        var connection = await GetOrCreateExtensionConnectionAsync(userId, cancellationToken);

        // `?? []` is belt-and-braces: the endpoint's validator rejects a null LinkDomains before this
        // job is ever enqueued (see ExtensionEmailSignalRequestValidator), so in practice it can't
        // arrive null here. It stays because this method is also reachable as a Hangfire job
        // re-executing an argument payload deserialized from storage, which no validator re-runs.
        await ProcessSignalAsync(connection, request.SenderEmail, request.SenderDisplayName, request.Subject,
            request.Snippet, request.ReceivedAt, request.LinkDomains ?? [], ComputeIdempotencyKey(request.GmailMessageId),
            cancellationToken);
    }

    private async Task<EmailConnection> GetOrCreateExtensionConnectionAsync(Guid userId, CancellationToken cancellationToken)
    {
        var existing = await dbContext.EmailConnections
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == EmailProvider.Extension, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        var connection = EmailConnection.CreateExtension(userId, DateTimeOffset.UtcNow);
        dbContext.EmailConnections.Add(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        return connection;
    }

    // Runs once ProcessExtensionSignalAsync has resolved the user's EmailConnection: idempotency,
    // matching, classification, auto-apply, and persistence.
    private async Task ProcessSignalAsync(
        EmailConnection connection, string fromEmail, string fromDisplayName, string subject, string snippet,
        DateTimeOffset receivedAt, IReadOnlyList<string> linkDomains, string providerMessageId,
        CancellationToken cancellationToken)
    {
        var alreadyProcessed = await dbContext.EmailSuggestions
            .AnyAsync(s => s.EmailConnectionId == connection.Id && s.ProviderMessageId == providerMessageId, cancellationToken);

        if (alreadyProcessed)
        {
            return;
        }

        var candidates = await BuildCandidatesAsync(connection.UserId, cancellationToken);

        // The original sender is the company; there's no "self-sent" concept here since the user
        // opening the thread themselves already happened before this ever reached us.
        var matchResult = EmailApplicationMatcher.Match(
            fromEmail, fromDisplayName, recipientEmail: "", ownAccountEmail: "", subject, candidates);
        var applicationId = matchResult?.ApplicationId;

        var senderDomain = ExtractDomain(fromEmail);

        // isKnownSender no longer hard-gates the LLM call (see RecruitmentSignalAnalyzer) — kept
        // here purely so the routing log line below can show it alongside the new score-based
        // decision.
        var isKnownSender = applicationId is not null || jobBoardDomainMatcher.IsKnown(senderDomain);

        var classification = await ClassifyAsync(fromEmail, subject, snippet,
            senderDomain, applicationId is not null, linkDomains, isKnownSender, cancellationToken);

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

        // Only worth an extra LLM call when the email actually signals a rejection — see
        // IEmailRejectionReasonExtractionProvider (always returns a result, NotStated included).
        var rejectionReason = classification.SuggestedStatus == ApplicationStatus.Rejected
            ? await emailRejectionReasonExtractionProvider.ExtractAsync(subject, snippet, cancellationToken)
            : null;

        if (applicationId is not null)
        {
            var suggestion = EmailSuggestion.Create(
                connection.UserId, connection.Id, applicationId.Value,
                providerMessageId, providerThreadId: null,
                classification.SuggestedStatus, classification.ConfidenceScore, classification.MatchedRule,
                matchResult!.MatchType, senderDomain, receivedAt, now, subject, snippet,
                rejectionReason?.Category, rejectionReason?.Detail, rejectionReason?.Confidence);

            dbContext.EmailSuggestions.Add(suggestion);

            if (suggestion.SuggestedStatus is not null)
            {
                await TryAutoApplyAsync(connection.UserId, suggestion, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        // Unmatched: the job isn't registered in the app yet. Only worth extracting company/job-title
        // detail (an extra LLM call) now that we know the email carries a real status signal — a
        // signal-less unmatched email (newsletter, unrelated mail) was already returned above, same
        // as before this "new job" flow existed (DECISIONS.md "Eşleşmeyen email'ler gösterilmiyor").
        var extraction = await emailJobExtractionProvider.ExtractAsync(subject, snippet, cancellationToken);
        if (extraction is null)
        {
            return; // couldn't confidently read a company name + job title — stay silent, don't guess
        }

        dbContext.EmailSuggestions.Add(EmailSuggestion.CreateForNewJob(
            connection.UserId, connection.Id, providerMessageId,
            classification.SuggestedStatus, classification.ConfidenceScore, classification.MatchedRule,
            senderDomain, receivedAt, now, subject, snippet,
            extraction.CompanyName, extraction.JobTitle, extraction.Location, extraction.Description,
            rejectionReason?.Category, rejectionReason?.Detail, rejectionReason?.Confidence));

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
            row.s.SuggestedStatus, row.s.ConfidenceScore, row.s.Subject ?? "", row.s.Snippet ?? "", row.s.EmailReceivedAt,
            RejectionReasonCategory: row.s.RejectionReasonCategory, RejectionReasonDetail: row.s.RejectionReasonDetail))
            .ToList();

        // "New job" suggestions (ApplicationId is null) always have Subject/Snippet/Extracted*
        // already persisted.
        var newJobRows = await dbContext.EmailSuggestions
            .Where(s => s.UserId == userId && s.Status == EmailSuggestionStatus.Pending && s.ApplicationId == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        responses.AddRange(newJobRows.Select(s => new EmailSuggestionResponse(
            s.Id, ApplicationId: null, s.ExtractedCompanyName ?? "", s.ExtractedJobTitle ?? "",
            s.SuggestedStatus, s.ConfidenceScore, s.Subject ?? "", s.Snippet ?? "", s.EmailReceivedAt,
            IsNewApplicationSuggestion: true, s.ExtractedLocation, s.ExtractedDescription,
            RejectionReasonCategory: s.RejectionReasonCategory, RejectionReasonDetail: s.RejectionReasonDetail)));
        
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
            // Source.Email so the user can see it was registered from email.
            var created = await applicationService.CreateAsync(userId, new CreateApplicationRequest(
                suggestion.ExtractedCompanyName!, suggestion.ExtractedJobTitle!, JobUrl: null,
                suggestion.ExtractedLocation, EmploymentType.FullTime, suggestion.EmailReceivedAt,
                Source.Email, suggestion.ExtractedDescription), cancellationToken);

            if (suggestion.SuggestedStatus is not null && suggestion.SuggestedStatus != ApplicationStatus.Applied)
            {
                await applicationService.ChangeStatusAsync(userId, created.Id,
                    new ChangeStatusRequest(suggestion.SuggestedStatus.Value,
                        AppendRejectionReason("E-postadan içe aktarıldı", suggestion),
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

        var changed = await ApplyStatusChangeAsync(userId, suggestion, "E-postadan onaylandı", cancellationToken);

        if (changed is null)
        {
            return ConfirmSuggestionResult.NotFound;
        }

        suggestion.Confirm(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ConfirmSuggestionResult.Confirmed;
    }

    /// <summary>Shared by the manual-confirm and auto-apply paths — the only place that actually
    /// mutates an existing Application's status from a matched suggestion. Caller must already have
    /// checked SuggestedStatus is not null.</summary>
    private Task<ApplicationDetailResponse?> ApplyStatusChangeAsync(
        Guid userId, EmailSuggestion suggestion, string noteLabel, CancellationToken cancellationToken) =>
        applicationService.ChangeStatusAsync(userId, suggestion.ApplicationId!.Value,
            new ChangeStatusRequest(suggestion.SuggestedStatus!.Value,
                AppendRejectionReason(noteLabel, suggestion), suggestion.EmailReceivedAt, Source.Email),
            cancellationToken);

    /// <summary>Applies a matched suggestion's status change immediately, without waiting for user
    /// confirmation, when it qualifies for auto-apply — see EmailAutoApprovalOptions. Never called for
    /// "new job" suggestions (MatchType is null there, which already fails the qualifying check).</summary>
    private async Task TryAutoApplyAsync(Guid userId, EmailSuggestion suggestion, CancellationToken cancellationToken)
    {
        var qualifies =
            suggestion.MatchType == EmailApplicationMatchType.DomainMatch &&
            suggestion.MatchedRule.StartsWith("Llm:", StringComparison.Ordinal) &&
            suggestion.ConfidenceScore >= autoApprovalOptions.Value.ConfidenceThreshold;

        if (!qualifies)
        {
            return;
        }

        if (!autoApprovalOptions.Value.Enabled)
        {
            if (autoApprovalOptions.Value.ShadowModeEnabled)
            {
                logger.LogInformation(
                    "Auto-apply shadow mode: would auto-apply suggestion for application {ApplicationId} " +
                    "to status {Status} (confidence={Confidence}, matchType={MatchType}, rule={MatchedRule})",
                    suggestion.ApplicationId, suggestion.SuggestedStatus, suggestion.ConfidenceScore,
                    suggestion.MatchType, suggestion.MatchedRule);
            }

            return;
        }

        var changed = await ApplyStatusChangeAsync(userId, suggestion, "E-postadan otomatik uygulandı", cancellationToken);
        if (changed is not null)
        {
            suggestion.AutoApply(DateTimeOffset.UtcNow);
        }
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

    public async Task<IReadOnlyList<EmailNotificationResponse>> GetNotificationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var resolvedStatuses = new[] { EmailSuggestionStatus.AutoApplied, EmailSuggestionStatus.Confirmed };

        var rows = await dbContext.EmailSuggestions
            .Where(s => s.UserId == userId && resolvedStatuses.Contains(s.Status) && s.ApplicationId != null)
            .Join(dbContext.Applications, s => s.ApplicationId, a => a.Id, (s, a) => new { s, a.JobTitle, a.CompanyId })
            .Join(dbContext.Companies, x => x.CompanyId, c => c.Id, (x, c) => new { x.s, x.JobTitle, CompanyName = c.Name })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var responses = rows.Select(row => new EmailNotificationResponse(
            row.s.Id, row.s.ApplicationId, row.CompanyName, row.JobTitle,
            row.s.SuggestedStatus, row.s.Status == EmailSuggestionStatus.AutoApplied,
            IsNewApplicationSuggestion: false, row.s.MatchType, row.s.ConfidenceScore, row.s.IsRead,
            row.s.CreatedAt, row.s.ResolvedAt))
            .ToList();

        // "New job" suggestions never get ApplicationId back-filled on Confirm (see EmailSuggestion.
        // ApplicationId doc comment — it's a permanent discriminator of the suggestion's original
        // kind), so CompanyName/JobTitle come from the Extracted* fields, not a join.
        var newJobRows = await dbContext.EmailSuggestions
            .Where(s => s.UserId == userId && resolvedStatuses.Contains(s.Status) && s.ApplicationId == null)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        responses.AddRange(newJobRows.Select(s => new EmailNotificationResponse(
            s.Id, ApplicationId: null, s.ExtractedCompanyName ?? "", s.ExtractedJobTitle ?? "",
            s.SuggestedStatus, s.Status == EmailSuggestionStatus.AutoApplied,
            IsNewApplicationSuggestion: true, s.MatchType, s.ConfidenceScore, s.IsRead,
            s.CreatedAt, s.ResolvedAt)));

        return [.. responses.OrderByDescending(r => r.CreatedAt)];
    }

    public Task<int> GetUnreadNotificationCountAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.EmailSuggestions.CountAsync(
            s => s.UserId == userId && s.Status == EmailSuggestionStatus.AutoApplied && !s.IsRead,
            cancellationToken);

    public async Task MarkNotificationsReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var unread = await dbContext.EmailSuggestions
            .Where(s => s.UserId == userId && s.Status == EmailSuggestionStatus.AutoApplied && !s.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var suggestion in unread)
        {
            suggestion.MarkRead(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

    // Gmail's own message id is already opaque/short and carries no PII — hashed anyway purely for
    // a consistent, fixed-length hex ProviderMessageId shape.
    private static string ComputeIdempotencyKey(string gmailMessageId) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(gmailMessageId)));

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

    // NotStated is the expected majority result (see IEmailRejectionReasonExtractionProvider) and
    // carries no detail worth persisting into the status-change note — only append when a real
    // reason was found.
    private static string AppendRejectionReason(string baseNote, EmailSuggestion suggestion)
    {
        if (suggestion.RejectionReasonCategory is null or RejectionReasonCategory.NotStated)
        {
            return baseNote;
        }

        var label = RejectionReasonLabel(suggestion.RejectionReasonCategory.Value);
        return suggestion.RejectionReasonDetail is null
            ? $"{baseNote} — Ret sebebi: {label}"
            : $"{baseNote} — Ret sebebi: {label} ({suggestion.RejectionReasonDetail})";
    }

    private static string RejectionReasonLabel(RejectionReasonCategory category) => category switch
    {
        RejectionReasonCategory.LanguageRequirement => "dil yetkinliği",
        RejectionReasonCategory.LocationOrRelocation => "lokasyon/relocation",
        RejectionReasonCategory.ExperienceLevelMismatch => "deneyim seviyesi",
        RejectionReasonCategory.SalaryExpectationMismatch => "maaş beklentisi",
        RejectionReasonCategory.SkillOrTechStackGap => "teknik yetkinlik eksikliği",
        RejectionReasonCategory.PositionCancelledOrFilled => "pozisyon iptal/doldu",
        RejectionReasonCategory.CultureOrTeamFit => "takım/kültür uyumu",
        _ => "diğer"
    };

    private static string? ExtractDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 && atIndex < email.Length - 1
            ? email[(atIndex + 1)..].Trim().ToLowerInvariant()
            : null;
    }
}
