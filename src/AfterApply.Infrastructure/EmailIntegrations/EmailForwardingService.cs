using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AfterApply.Application.Applications;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.Caching;
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
    PaidCallBudget paidCalls,
    IOptions<PaidCallOptions> paidCallOptions,
    ILogger<EmailForwardingService> logger) : IEmailForwardingService
{
    private const string PaidCallFeature = "email-signals";

    /// <summary>Longer than Hangfire's ten retries take (about a day and a half), so a signal is
    /// never purged under a job that is still retrying it.</summary>
    internal static readonly TimeSpan PendingSignalRetention = TimeSpan.FromDays(3);

    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    /// <summary>Reserves one OpenAI call for this account's Gmail scanning (see PaidCallBudget).
    /// Refused, the flow carries on with what the free rules already said.</summary>
    private Task<bool> TryReservePaidCallAsync(Guid userId)
    {
        var budget = paidCallOptions.Value.EmailSignals;
        return paidCalls.TryReserveAsync(PaidCallFeature, budget.GlobalDaily, userId, budget.PerUserDaily);
    }

    public async Task ProcessExtensionSignalAsync(Guid userId, ExtensionEmailSignalRequest request, CancellationToken cancellationToken)
    {
        var connection = await GetOrCreateExtensionConnectionAsync(userId, cancellationToken);

        // `?? []` is belt-and-braces: the endpoint's validator rejects a null LinkDomains before this
        // job is ever enqueued (see ExtensionEmailSignalRequestValidator), so in practice it can't
        // arrive null here. It stays because this method is also reachable as a Hangfire job
        // re-executing an argument payload deserialized from storage, which no validator re-runs.
        var providerMessageId = ComputeIdempotencyKey(request.GmailMessageId);
        try
        {
            await ProcessSignalAsync(connection, request.SenderEmail, request.SenderDisplayName, request.Subject,
                request.Snippet, request.ReceivedAt, request.LinkDomains ?? [], providerMessageId, cancellationToken);
        }
        catch
        {
            // Hangfire retries a failed run; it must not find the message marked as already seen.
            await paidCalls.ForgetSightingAsync(PaidCallFeature, $"{connection.Id:N}:{providerMessageId}");
            throw;
        }
    }

    public async Task<Guid> StageExtensionSignalAsync(Guid userId, ExtensionEmailSignalRequest request, CancellationToken cancellationToken)
    {
        var pending = PendingEmailSignal.Create(userId, JsonSerializer.Serialize(request, PayloadJson), DateTimeOffset.UtcNow);
        dbContext.PendingEmailSignals.Add(pending);
        await dbContext.SaveChangesAsync(cancellationToken);
        return pending.Id;
    }

    public async Task ProcessPendingExtensionSignalAsync(Guid pendingSignalId, CancellationToken cancellationToken)
    {
        var pending = await dbContext.PendingEmailSignals.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == pendingSignalId, cancellationToken);
        if (pending is null)
        {
            return;
        }

        var request = JsonSerializer.Deserialize<ExtensionEmailSignalRequest>(pending.Payload, PayloadJson);
        if (request is not null)
        {
            // A failure throws past the delete, so Hangfire's retry finds the row again.
            await ProcessExtensionSignalAsync(pending.UserId, request, cancellationToken);
        }

        await dbContext.PendingEmailSignals.Where(s => s.Id == pendingSignalId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> PurgeStalePendingSignalsAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - PendingSignalRetention;
        return dbContext.PendingEmailSignals.Where(s => s.CreatedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
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

        // The check above only knows messages that became a suggestion. A message that did not is
        // not written anywhere, so sending it again would pay for the same classification again;
        // remembering every message seen for a month closes that.
        if (!await paidCalls.IsFirstSightingAsync(PaidCallFeature, $"{connection.Id:N}:{providerMessageId}", TimeSpan.FromDays(30)))
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

        // Kept only when it is a mailbox someone could actually write back to — see
        // HrEmailCandidate. Anything else stays reduced to senderDomain above and the full address
        // is never written down.
        var hrEmailCandidate = HrEmailCandidate.From(fromEmail, jobBoardDomainMatcher.IsKnown(senderDomain));

        var classification = await ClassifyAsync(connection.UserId, fromEmail, subject, snippet,
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
                              && await TryReservePaidCallAsync(connection.UserId)
            ? await emailRejectionReasonExtractionProvider.ExtractAsync(subject, snippet, cancellationToken)
            : null;

        if (applicationId is not null)
        {
            var suggestion = EmailSuggestion.Create(
                connection.UserId, connection.Id, applicationId.Value,
                providerMessageId, providerThreadId: null,
                classification.SuggestedStatus, classification.ConfidenceScore, classification.MatchedRule,
                matchResult!.MatchType, senderDomain, receivedAt, now, subject, snippet,
                rejectionReason?.Category, rejectionReason?.Detail, rejectionReason?.Confidence,
                hrEmailCandidate);

            dbContext.EmailSuggestions.Add(suggestion);

            if (suggestion.SuggestedStatus is not null)
            {
                await TryAutoApplyAsync(connection.UserId, suggestion, cancellationToken);
            }

            await SaveSuggestionAsync(cancellationToken);
            return;
        }

        // Unmatched: the job isn't registered in the app yet. Only worth extracting company/job-title
        // detail (an extra LLM call) now that we know the email carries a real status signal — a
        // signal-less unmatched email (newsletter, unrelated mail) was already returned above, same
        // as before this "new job" flow existed (DECISIONS.md "Eşleşmeyen email'ler gösterilmiyor").
        var extraction = await TryReservePaidCallAsync(connection.UserId)
            ? await emailJobExtractionProvider.ExtractAsync(subject, snippet, cancellationToken)
            : null;
        if (extraction is null)
        {
            return; // couldn't confidently read a company name + job title — stay silent, don't guess
        }

        dbContext.EmailSuggestions.Add(EmailSuggestion.CreateForNewJob(
            connection.UserId, connection.Id, providerMessageId,
            classification.SuggestedStatus, classification.ConfidenceScore, classification.MatchedRule,
            senderDomain, receivedAt, now, subject, snippet,
            extraction.CompanyName, extraction.JobTitle, extraction.Location, extraction.Description,
            rejectionReason?.Category, rejectionReason?.Detail, rejectionReason?.Confidence,
            hrEmailCandidate));

        await SaveSuggestionAsync(cancellationToken);
    }

    /// <summary>The (EmailConnectionId, ProviderMessageId) index is what actually makes a message
    /// process once; the AnyAsync check above is the cheap path. The extension can send the same
    /// signal twice (a retry, two tabs), and with several instances both copies can pass that
    /// check before either commits — the loser must end quietly, not as a failed Hangfire job
    /// that retries the classification.</summary>
    private async Task SaveSuggestionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg
                                           && pg.ConstraintName?.Contains("ProviderMessageId", StringComparison.Ordinal) == true)
        {
            // The other copy of this signal already wrote the suggestion.
        }
    }

    public Task<int> GetPendingSuggestionCountAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.EmailSuggestions
            .CountAsync(s => s.UserId == userId && s.Status == EmailSuggestionStatus.Pending, cancellationToken);

    public Task<bool> HasReceivedExtensionSignalAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.EmailConnections
            .AnyAsync(c => c.UserId == userId && c.Provider == EmailProvider.Extension, cancellationToken);

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
                    suggestion.SuggestedStatus.Value, suggestion.EmailReceivedAt,
                    ContextFor(suggestion, StatusChangeOrigin.EmailSuggestionConfirmed),
                    cancellationToken);
            }

            await AttachHrEmailAsync(userId, created.Id, suggestion, cancellationToken);

            suggestion.Confirm(DateTimeOffset.UtcNow);
            await dbContext.SaveChangesAsync(cancellationToken);
            return ConfirmSuggestionResult.Confirmed;
        }

        if (suggestion.SuggestedStatus is null)
        {
            return ConfirmSuggestionResult.NoStatusToConfirm;
        }

        var changed = await ApplyStatusChangeAsync(userId, suggestion, StatusChangeOrigin.EmailSuggestionConfirmed, cancellationToken);

        if (changed is null)
        {
            return ConfirmSuggestionResult.NotFound;
        }

        await AttachHrEmailAsync(userId, suggestion.ApplicationId!.Value, suggestion, cancellationToken);

        suggestion.Confirm(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ConfirmSuggestionResult.Confirmed;
    }

    /// <summary>
    /// Offers the sender's address as the application's HR contact, on the two paths where the
    /// email has actually been accepted as being about this application: the user confirming it,
    /// and auto-apply (which only runs on a DomainMatch). A dismissed suggestion writes nothing —
    /// the user rejecting it is exactly the case where the address might belong to the wrong job.
    /// Fills only when the application has no address yet; see
    /// Application.SetHrEmailFromIncomingEmail.
    /// </summary>
    private Task AttachHrEmailAsync(Guid userId, Guid applicationId, EmailSuggestion suggestion, CancellationToken cancellationToken) =>
        suggestion.SenderEmail is null
            ? Task.CompletedTask
            : applicationService.AttachHrEmailFromIncomingEmailAsync(userId, applicationId, suggestion.SenderEmail, cancellationToken);

    /// <summary>Shared by the manual-confirm and auto-apply paths — the only place that actually
    /// mutates an existing Application's status from a matched suggestion. Caller must already have
    /// checked SuggestedStatus is not null.</summary>
    private Task<ApplicationDetailResponse?> ApplyStatusChangeAsync(
        Guid userId, EmailSuggestion suggestion, StatusChangeOrigin origin, CancellationToken cancellationToken) =>
        applicationService.ChangeStatusAsync(userId, suggestion.ApplicationId!.Value,
            suggestion.SuggestedStatus!.Value, suggestion.EmailReceivedAt,
            ContextFor(suggestion, origin), cancellationToken);

    /// <summary>Carries the suggestion's provenance onto the status history row as structured
    /// fields. The note stays null: what used to be written here as a Turkish sentence ("E-postadan
    /// onaylandı — Ret sebebi: lokasyon/relocation") is now Origin plus the rejection-reason
    /// snapshot, so the UI can render it in whichever language the viewer reads — and Note goes back
    /// to meaning "what the user typed".</summary>
    private static StatusChangeContext ContextFor(EmailSuggestion suggestion, StatusChangeOrigin origin) =>
        new(Source.Email, origin, Note: null, suggestion.Id,
            suggestion.RejectionReasonCategory, suggestion.RejectionReasonDetail);

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

        var changed = await ApplyStatusChangeAsync(userId, suggestion, StatusChangeOrigin.EmailAutoApplied, cancellationToken);
        if (changed is not null)
        {
            await AttachHrEmailAsync(userId, suggestion.ApplicationId!.Value, suggestion, cancellationToken);
            suggestion.AutoApply(DateTimeOffset.UtcNow);
        }
    }

    public async Task<RevertAutoApplyResult> RevertAutoApplyAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await dbContext.EmailSuggestions
            .FirstOrDefaultAsync(s => s.Id == suggestionId && s.UserId == userId, cancellationToken);

        // Only an unattended apply can be taken back. A Confirmed suggestion was the user's own
        // answer to a question they were asked; undoing that is the ordinary status control's job.
        if (suggestion is null || suggestion.Status != EmailSuggestionStatus.AutoApplied
            || suggestion.ApplicationId is not { } applicationId || suggestion.SuggestedStatus is not { } appliedStatus)
        {
            return RevertAutoApplyResult.NotFound;
        }

        // The history row auto-apply wrote is the only record of what the status was before it, so
        // it is also the only thing that can say what to put back.
        var appliedRow = await dbContext.ApplicationStatusHistories
            .Where(h => h.EmailSuggestionId == suggestionId && h.Origin == StatusChangeOrigin.EmailAutoApplied)
            .OrderByDescending(h => h.ChangedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (appliedRow?.FromStatus is not { } previousStatus)
        {
            return RevertAutoApplyResult.NotFound;
        }

        var currentStatus = await dbContext.Applications
            .Where(a => a.Id == applicationId && a.UserId == userId)
            .Select(a => (ApplicationStatus?)a.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentStatus is null)
        {
            return RevertAutoApplyResult.NotFound;
        }

        // Refuse rather than clobber. If the user has since moved the application on themselves,
        // "undo" would quietly throw their own change away.
        if (currentStatus != appliedStatus)
        {
            return RevertAutoApplyResult.StatusMovedOn;
        }

        var reverted = await applicationService.ChangeStatusAsync(userId, applicationId, previousStatus,
            DateTimeOffset.UtcNow, ContextFor(suggestion, StatusChangeOrigin.EmailAutoApplyReverted), cancellationToken);

        if (reverted is null)
        {
            return RevertAutoApplyResult.NotFound;
        }

        // The HR address auto-apply may have attached is deliberately left in place: it came from a
        // real message from that company, it is useful whether or not the status change was right,
        // and silently removing contact details the user may have since relied on would be its own
        // small betrayal.
        suggestion.Revert(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return RevertAutoApplyResult.Reverted;
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

    private static readonly EmailSuggestionStatus[] NotificationStatuses =
        [EmailSuggestionStatus.AutoApplied, EmailSuggestionStatus.Confirmed];

    /// <summary>What the Notifications page is a view over: resolved-by-applying suggestions the user
    /// has not swiped away. Every notification read and write below starts from here so the four
    /// endpoints cannot drift on what counts as "a notification".</summary>
    private IQueryable<EmailSuggestion> Notifications(Guid userId) =>
        dbContext.EmailSuggestions.Where(s =>
            s.UserId == userId && NotificationStatuses.Contains(s.Status) && s.NotificationDismissedAt == null);

    public async Task<PagedResult<EmailNotificationResponse>> GetNotificationsAsync(Guid userId,
        GetNotificationsQuery query, CancellationToken cancellationToken)
    {
        var notifications = Notifications(userId);
        var totalCount = await notifications.CountAsync(cancellationToken);

        // Id as the tie-break so two rows created in the same instant (one signal, one job) cannot
        // swap sides of a page boundary between requests.
        var page = await notifications
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Company/job title come from the application for a matched suggestion; one lookup for the
        // page rather than a join, so the paging above is over suggestions alone.
        var applicationIds = page.Where(s => s.ApplicationId != null).Select(s => s.ApplicationId!.Value).Distinct().ToList();
        var applications = applicationIds.Count == 0
            ? new Dictionary<Guid, (string CompanyName, string JobTitle)>()
            : await dbContext.Applications
                .Where(a => applicationIds.Contains(a.Id))
                .Join(dbContext.Companies, a => a.CompanyId, c => c.Id, (a, c) => new { a.Id, a.JobTitle, CompanyName = c.Name })
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => (x.CompanyName, x.JobTitle), cancellationToken);

        // "New job" suggestions never get ApplicationId back-filled on Confirm (see EmailSuggestion.
        // ApplicationId doc comment — it's a permanent discriminator of the suggestion's original
        // kind), so CompanyName/JobTitle come from the Extracted* fields, not the lookup.
        var items = page.Select(s =>
        {
            var isNewJob = s.ApplicationId is null;
            var (companyName, jobTitle) = isNewJob
                ? (s.ExtractedCompanyName ?? "", s.ExtractedJobTitle ?? "")
                : applications.GetValueOrDefault(s.ApplicationId!.Value, ("", ""));
            return new EmailNotificationResponse(
                s.Id, s.ApplicationId, companyName, jobTitle,
                s.SuggestedStatus, s.Status == EmailSuggestionStatus.AutoApplied,
                IsNewApplicationSuggestion: isNewJob, s.MatchType, s.ConfidenceScore, s.IsRead,
                s.CreatedAt, s.ResolvedAt);
        }).ToList();

        return new PagedResult<EmailNotificationResponse>(items, totalCount, query.Page, query.PageSize);
    }

    public Task<int> GetUnreadNotificationCountAsync(Guid userId, CancellationToken cancellationToken) =>
        Notifications(userId).CountAsync(s => s.Status == EmailSuggestionStatus.AutoApplied && !s.IsRead, cancellationToken);

    public async Task MarkNotificationsReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var unread = await Notifications(userId)
            .Where(s => s.Status == EmailSuggestionStatus.AutoApplied && !s.IsRead)
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

    public async Task<bool> DismissNotificationAsync(Guid userId, Guid suggestionId, CancellationToken cancellationToken)
    {
        // Not Notifications(userId): a row the user already cleared must answer 204 again, not 404 —
        // a swipe that raced its own retry is not an error.
        var suggestion = await dbContext.EmailSuggestions.FirstOrDefaultAsync(s =>
            s.Id == suggestionId && s.UserId == userId && NotificationStatuses.Contains(s.Status), cancellationToken);

        if (suggestion is null)
        {
            return false;
        }

        suggestion.DismissNotification(DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<int> DismissAllNotificationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return Notifications(userId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(s => s.NotificationDismissedAt, now), cancellationToken);
    }

    private async Task<EmailClassificationResult> ClassifyAsync(Guid userId, string senderEmail, string subject, string snippet,
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

        if (!await TryReservePaidCallAsync(userId))
        {
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

    private static string? ExtractDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 && atIndex < email.Length - 1
            ? email[(atIndex + 1)..].Trim().ToLowerInvariant()
            : null;
    }
}
