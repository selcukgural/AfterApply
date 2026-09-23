using AfterApply.Domain.Common;

namespace AfterApply.Domain.CompanySalaries;

/// <summary>A reader's "helpful" on a salary entry — the <c>CompanyReviewHelpfulMark</c> shape. One
/// per reader per entry (unique index); toggling off deletes the row, so the count is a plain COUNT.</summary>
public sealed class CompanySalaryHelpfulMark : Entity
{
    public Guid EntryId { get; private set; }

    public Guid UserId { get; private set; }

    public DateTimeOffset MarkedAt { get; private set; }

    private CompanySalaryHelpfulMark()
    {
    }

    public static CompanySalaryHelpfulMark Create(Guid entryId, Guid userId, DateTimeOffset now) =>
        new() { EntryId = entryId, UserId = userId, MarkedAt = now };
}
