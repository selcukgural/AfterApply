using AfterApply.Domain.EmailIntegrations;

namespace AfterApply.Application.Applications;

/// <summary>
/// "Of your last five reasoned rejections, three said the same thing" — the line the rejected
/// application's page shows (canvas "İnce dokunuşlar — Paket 2", 4A). Pure: the caller hands in
/// the user's rejection reasons, newest first, one per application.
/// </summary>
public static class RejectionPatterns
{
    /// <summary>How many of the most recent reasoned rejections are looked at.</summary>
    public const int Window = 5;

    /// <summary>A reason has to recur this often to be called a pattern; fewer is anecdote.</summary>
    public const int MinimumRepeat = 3;

    public sealed record Pattern(RejectionReasonCategory Category, int Count, int OutOf);

    /// <summary>
    /// "Not stated" and "other" say nothing a person could act on, so they are neither counted
    /// nor allowed to fill the window.
    /// </summary>
    public static Pattern? Find(IEnumerable<RejectionReasonCategory> newestFirst)
    {
        var window = newestFirst
            .Where(c => c is not RejectionReasonCategory.NotStated and not RejectionReasonCategory.Other)
            .Take(Window)
            .ToList();

        var top = window
            .GroupBy(c => c)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .FirstOrDefault();

        return top is not null && top.Count >= MinimumRepeat ? new Pattern(top.Category, top.Count, window.Count) : null;
    }
}
