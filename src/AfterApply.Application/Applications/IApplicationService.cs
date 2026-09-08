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
}
