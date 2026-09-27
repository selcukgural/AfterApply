namespace AfterApply.Domain.Board;

/// <summary>
/// Sparse ordering keys for cards inside a column: neighbours are <see cref="Gap"/> apart, so a
/// card goes on top, at the bottom or between two others by writing its own row only. When two
/// neighbours have run out of room between them the column is renumbered once
/// (<see cref="Renumbered"/>) and the move retried. At 2^20 per step a column can take about
/// nine trillion inserts on top before a long runs out.
/// </summary>
public static class BoardPositions
{
    public const long Gap = 1L << 20;

    /// <summary>The key for a card placed above everything in a column whose topmost key is
    /// <paramref name="currentTop"/> (null: the column is empty).</summary>
    public static long Top(long? currentTop) => currentTop is { } top ? top - Gap : 0;

    /// <summary>The key for a card placed below everything in a column whose lowest key is
    /// <paramref name="currentBottom"/>.</summary>
    public static long Bottom(long? currentBottom) => currentBottom is { } bottom ? bottom + Gap : 0;

    /// <summary>
    /// The key for a card placed directly below <paramref name="above"/> and directly above
    /// <paramref name="below"/> (either may be null: the top or bottom of the column). Null when
    /// the two are adjacent or out of order and there is no whole number strictly between them —
    /// the caller renumbers the column and asks again.
    /// </summary>
    public static long? Between(long? above, long? below)
    {
        switch (above, below)
        {
            case (null, null):
                return 0;
            case (null, { } b):
                return b - Gap;
            case ({ } a, null):
                return a + Gap;
            case ({ } a, { } b):
                if (b - a < 2)
                {
                    return null;
                }

                return a + (b - a) / 2;
        }
    }

    /// <summary>Evenly spaced keys for a column of <paramref name="count"/> cards, in order.</summary>
    public static IReadOnlyList<long> Renumbered(int count)
    {
        var keys = new long[count];
        for (var i = 0; i < count; i++)
        {
            keys[i] = i * Gap;
        }

        return keys;
    }
}
