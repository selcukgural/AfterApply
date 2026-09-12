namespace AfterApply.Domain.JobSources;

/// <summary>Which postings a query surfaced, in the order the source listed them. Composite key.</summary>
public sealed class JobSourceQueryPosting
{
    public Guid QueryId { get; private set; }

    public Guid PostingId { get; private set; }

    public DateTimeOffset FirstSeenAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    /// <summary>Position in the source's result list on the most recent run (0-based). Lower ranks
    /// are the source's own idea of the best match; past the exact-match count LinkedIn pads with
    /// loosely related postings, so rank is what keeps those at the back of the queue.</summary>
    public int Rank { get; private set; }

    private JobSourceQueryPosting()
    {
    }

    public static JobSourceQueryPosting Create(Guid queryId, Guid postingId, int rank, DateTimeOffset now) =>
        new() { QueryId = queryId, PostingId = postingId, Rank = rank, FirstSeenAt = now, LastSeenAt = now };

    public void SeenAgain(int rank, DateTimeOffset now)
    {
        Rank = rank;
        LastSeenAt = now;
    }
}
