namespace AfterApply.Domain.Jobs;

/// <summary>What one look at a posting's own page or API said about whether it still takes applications.</summary>
public enum PostingLivenessKind
{
    /// <summary>The posting is up and taking applications.</summary>
    Open,

    /// <summary>The site says so itself — LinkedIn's "no longer accepting applications" or its
    /// expired-posting redirect, a kariyer.net closing date in the past, an ATS "job not found".
    /// One observation is enough.</summary>
    Closed,

    /// <summary>The posting has disappeared without the site saying why: a bare 404, a redirect to
    /// the site's general listing. Could be a transient hiccup, so it takes a second look a while
    /// later before it closes anything.</summary>
    Gone,

    /// <summary>No answer worth acting on — a timeout, a 5xx, a rate limit, a login wall. Never
    /// closes anything.</summary>
    Unknown
}

/// <param name="ClosedOn">When the site says it closed, if it says (kariyer.net's closing date);
/// otherwise the check time is used.</param>
/// <param name="ClosesOn">When an open posting is announced to close (kariyer.net).</param>
/// <param name="SourceSaidStop">The site answered 429/403 or a login wall: the run leaves that
/// site alone for the rest of the day.</param>
public sealed record PostingLiveness(
    PostingLivenessKind Kind,
    DateTimeOffset? ClosedOn = null,
    DateTimeOffset? ClosesOn = null,
    bool SourceSaidStop = false)
{
    public static PostingLiveness Open(DateTimeOffset? closesOn = null) => new(PostingLivenessKind.Open, ClosesOn: closesOn);

    public static PostingLiveness Closed(DateTimeOffset? closedOn = null) => new(PostingLivenessKind.Closed, ClosedOn: closedOn);

    public static readonly PostingLiveness Gone = new(PostingLivenessKind.Gone);

    public static readonly PostingLiveness Unknown = new(PostingLivenessKind.Unknown);

    public static readonly PostingLiveness Stop = new(PostingLivenessKind.Unknown, SourceSaidStop: true);
}
