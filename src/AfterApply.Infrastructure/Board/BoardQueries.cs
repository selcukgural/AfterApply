using AfterApply.Domain.Board;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AfterApply.Infrastructure.Board;

/// <summary>The one definition of "the cards in a column", shared by the reads, the moves and
/// <see cref="BoardSync"/> — a column is not stored, so every one of them has to derive it the
/// same way.</summary>
internal static class BoardQueries
{
    public static IQueryable<BoardCard> CardsIn(AppDbContext dbContext, Guid userId, BoardColumn column)
    {
        var cards = dbContext.BoardCards.Where(c => c.UserId == userId);
        if (column == BoardColumn.Saved)
        {
            return cards.Where(c => c.TrackedJobId != null);
        }

        var statuses = BoardColumns.StatusesIn(column);
        return cards.Where(c => c.ApplicationId != null
            && dbContext.Applications.Any(a => a.Id == c.ApplicationId && statuses.Contains(a.Status)));
    }

    public static Task<long?> TopOfAsync(AppDbContext dbContext, Guid userId, BoardColumn column,
        CancellationToken cancellationToken) =>
        CardsIn(dbContext, userId, column).MinAsync(c => (long?)c.Position, cancellationToken);
}
