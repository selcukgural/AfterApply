using AfterApply.Domain.Common;
using AfterApply.Domain.EmailIntegrations;

namespace AfterApply.Domain.Applications;

/// <summary>Everything a status change carries besides the new status and its timestamp. Bundled
/// into one record so ChangeStatus keeps a readable signature as provenance grows, and so a caller
/// cannot silently omit the origin.
/// </summary>
/// <param name="Note">The user's own free text — and only that. Never a system-generated sentence:
/// origin and rejection reason are structured fields precisely so the frontend can render them in
/// the viewer's language.</param>
/// <param name="EmailSuggestionId">The suggestion this change came from, so the history row can link
/// back to the email. Null for every non-email origin.</param>
/// <param name="RejectionReasonCategory">Snapshot of the reason the rejection email stated, copied
/// (not joined) onto the history row: history is a record of what was true at the time, and must
/// survive the suggestion being deleted.</param>
public sealed record StatusChangeContext(
    Source Source,
    StatusChangeOrigin Origin,
    string? Note = null,
    Guid? EmailSuggestionId = null,
    RejectionReasonCategory? RejectionReasonCategory = null,
    string? RejectionReasonDetail = null)
{
    public static StatusChangeContext Manual(string? note = null) =>
        new(Source.Manual, StatusChangeOrigin.Manual, note);
}
