namespace AfterApply.Domain.Companies;

/// <summary>
/// A company's logo — from the company's own website, or failing that its LinkedIn page — kept
/// here (small: tens of kilobytes), or the record that one was looked for. One row per company:
/// <list type="bullet">
/// <item>content set — the logo;</item>
/// <item>content null — looked for; looked for again at <see cref="NextCheckAt"/>;</item>
/// <item><see cref="Blocked"/> — an admin took it down; never fetched again.</item>
/// </list>
/// The logo belongs to a shared company row that one user's capture pointed at, so it is shown
/// only on the private board of users who applied there (DECISIONS.md 2026-09-27).
/// </summary>
public sealed class CompanyLogo
{
    /// <summary>The longest a company waits between looks, whatever went wrong.</summary>
    public static readonly TimeSpan MaxRecheckDelay = TimeSpan.FromDays(30);

    public Guid CompanyId { get; private set; }

    public byte[]? Content { get; private set; }

    public string? ContentType { get; private set; }

    public DateTimeOffset CheckedAt { get; private set; }

    /// <summary>When the company is due to be looked at again; null once there is a logo (or a block).</summary>
    public DateTimeOffset? NextCheckAt { get; private set; }

    /// <summary>Looks in a row that ended in "not now" (LinkedIn's bot wall, a timeout, a server
    /// error) rather than an answer; each one doubles the wait before the next.</summary>
    public int DeferCount { get; private set; }

    public bool Blocked { get; private set; }

    private CompanyLogo()
    {
    }

    public static CompanyLogo For(Guid companyId) => new() { CompanyId = companyId };

    public bool IsDue(DateTimeOffset now) => !Blocked && Content is null && (NextCheckAt is null || NextCheckAt <= now);

    public void Found(byte[] content, string contentType, DateTimeOffset now)
    {
        Content = content;
        ContentType = contentType;
        CheckedAt = now;
        NextCheckAt = null;
        DeferCount = 0;
    }

    /// <summary>A real answer — no logo there — so the next look is a month away.</summary>
    public void NotFound(DateTimeOffset now)
    {
        Content = null;
        ContentType = null;
        CheckedAt = now;
        NextCheckAt = now + MaxRecheckDelay;
        DeferCount = 0;
    }

    /// <summary>No answer at all. The next look is 1, 2, 4, 8… days away, capped at a month: a
    /// site that throttles is not asked every night, and one that was briefly down is soon asked
    /// again.</summary>
    public void Deferred(DateTimeOffset now)
    {
        DeferCount++;
        CheckedAt = now;
        var delay = TimeSpan.FromDays(Math.Pow(2, Math.Min(DeferCount - 1, 5)));
        NextCheckAt = now + (delay < MaxRecheckDelay ? delay : MaxRecheckDelay);
    }

    public void Block(DateTimeOffset now)
    {
        NotFound(now);
        NextCheckAt = null;
        Blocked = true;
    }
}
