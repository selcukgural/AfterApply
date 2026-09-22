using AfterApply.Application.Applications.Contracts;
using AfterApply.Domain.Applications;

namespace AfterApply.Application.Applications;

public interface IApplicationService
{
    Task<PagedResult<ApplicationSummaryResponse>> GetAllAsync(Guid userId, GetApplicationsQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The same applications <see cref="GetAllAsync"/> would return for the same filter, collected
    /// under the company they were sent to. Pages over companies rather than applications, so that a
    /// company's applications can never be split across two pages — which is the whole point of the
    /// view. Each group carries at most a fixed number of rows; see
    /// <see cref="Contracts.CompanyGroupResponse.HasMore"/>.
    /// </summary>
    Task<GroupedApplicationsResponse> GetGroupedByCompanyAsync(Guid userId, GetGroupedApplicationsQuery query, CancellationToken cancellationToken);

    Task<ApplicationSummaryCountsResponse> GetSummaryCountsAsync(Guid userId, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse?> GetByIdAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse> CreateAsync(Guid userId, CreateApplicationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Used by the browser extension's "I Applied" action (spec §11/Sprint 9). Unlike CreateAsync
    /// (a deliberate one-off manual entry, never deduplicated), this resolves the Job via
    /// IJobResolver and, when an application with the same JobUrl already exists for this user,
    /// returns it instead of creating a duplicate (WasDuplicate = true) — the extension button can
    /// be clicked more than once on the same job page without piling up rows.
    /// </summary>
    Task<ExtensionApplicationResponse> CreateFromExtensionAsync(Guid userId, CreateFromExtensionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Records the HR email read off an email that was matched to this application, and only when
    /// the application has none — the user's own entry always wins. Deliberately not reachable
    /// through UpdateApplicationRequest: provenance is decided by the code path that writes the
    /// value, never by a caller claiming it, the same rule ChangeStatusRequest already follows.
    /// </summary>
    Task AttachHrEmailFromIncomingEmailAsync(Guid userId, Guid applicationId, string email, CancellationToken cancellationToken);

    Task<ApplicationDetailResponse?> UpdateAsync(Guid userId, Guid applicationId, UpdateApplicationRequest request, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    /// <summary>The user-initiated path, behind POST /applications/{id}/status. Always records
    /// StatusChangeOrigin.Manual — a caller cannot claim a different provenance.</summary>
    Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId, ChangeStatusRequest request, CancellationToken cancellationToken);

    /// <summary>The internal path for changes the system applies on the user's behalf — email
    /// suggestions and imports — where the caller knows the real origin and passes it explicitly.
    /// Not reachable from the HTTP surface.</summary>
    Task<ApplicationDetailResponse?> ChangeStatusAsync(Guid userId, Guid applicationId, ApplicationStatus newStatus,
        DateTimeOffset changedAt, StatusChangeContext context, CancellationToken cancellationToken);

    /// <summary>Records, moves or clears the company's promised reply date without a status change.
    /// Null when the application is not the user's.</summary>
    Task<ApplicationDetailResponse?> SetReplyPromiseAsync(Guid userId, Guid applicationId, SetReplyPromiseRequest request,
        CancellationToken cancellationToken);

    /// <summary>Newest first. Null when the application is not the user's.</summary>
    Task<IReadOnlyCollection<ApplicationStatusHistoryResponse>?> GetStatusHistoryAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ApplicationEventResponse>?> GetTimelineAsync(Guid userId, Guid applicationId, CancellationToken cancellationToken);

    Task<ApplicationEventResponse?> AddEventAsync(Guid userId, Guid applicationId, CreateEventRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Moves every application in the selection to one status, skipping any that is already there.
    /// Records StatusChangeOrigin.BulkEdit — the caller cannot claim a different one, same rule as
    /// the single-application path.
    /// </summary>
    /// <exception cref="Common.CodedException">The selection resolves to more rows than the
    /// configured ceiling, or the count the user was shown no longer holds.</exception>
    Task<BulkChangeStatusResponse> BulkChangeStatusAsync(Guid userId, BulkChangeStatusRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Puts back what <see cref="BulkChangeStatusAsync"/> did, entry by entry, skipping any
    /// application whose status has moved on since. Appends BulkEditReverted history rather than
    /// deleting the rows it reverses.
    /// </summary>
    Task<UndoBulkStatusResponse> UndoBulkStatusAsync(Guid userId, UndoBulkStatusRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Permanently deletes every application in the selection, together with everything that
    /// cascades from it (events, status history, reminders, matched email suggestions). There is no
    /// recovery path by design — see DECISIONS.md 2026-09-07 on why this product does not soft-delete
    /// personal data.
    /// </summary>
    /// <exception cref="Common.CodedException">The count the user was shown no longer holds; nothing
    /// is deleted in that case.</exception>
    Task<BulkDeleteResponse> BulkDeleteAsync(Guid userId, BulkDeleteRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// The user's applications that are still Applied past the stale horizon with no real status
    /// change inside it — see <see cref="StaleApplicationsSummaryResponse"/>.
    /// </summary>
    Task<StaleApplicationsSummaryResponse> GetStaleSummaryAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves every application <see cref="GetStaleSummaryAsync"/> counts to Ghosted, in one act. The
    /// set is the server's, never the client's, so it is not subject to the bulk operation ceiling;
    /// the response is the same shape as a bulk status change and is undone through
    /// <see cref="UndoGhostAsync"/>.
    /// </summary>
    Task<BulkChangeStatusResponse> GhostStaleApplicationsAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the given applications to Ghosted in one act — the reminders card's bulk "it was
    /// ghosted" answer. Ids are intersected with the caller's own open rows: a foreign, deleted or
    /// already terminal id matches nothing. Not subject to the bulk ceiling for the same reason
    /// <see cref="GhostStaleApplicationsAsync"/> is not: the set is resolved from a server-side
    /// selection (the user's own reminders), never from a client-supplied filter.
    /// </summary>
    Task<BulkChangeStatusResponse> GhostApplicationsAsync(Guid userId, IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken);

    /// <summary>
    /// Undoes <see cref="GhostStaleApplicationsAsync"/> or <see cref="GhostApplicationsAsync"/> with
    /// the same compare-and-set rules as <see cref="UndoBulkStatusAsync"/>, minus the ceiling: both
    /// batches are bounded by the user's own rows, not by anything the client chose.
    /// </summary>
    Task<UndoBulkStatusResponse> UndoGhostAsync(Guid userId, UndoBulkStatusRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// "Not now": stops the stale question until a later import brings in applications the user
    /// has not been asked about.
    /// </summary>
    Task DismissStaleSuggestionAsync(Guid userId, CancellationToken cancellationToken);
}
