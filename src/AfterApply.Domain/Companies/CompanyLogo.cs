namespace AfterApply.Domain.Companies;

/// <summary>
/// A company's logo, fetched from its LinkedIn page and kept here (small: tens of kilobytes), or
/// the record that one was looked for. One row per company:
/// <list type="bullet">
/// <item>content set — the logo;</item>
/// <item>content null — looked for and not found; looked for again after a while;</item>
/// <item><see cref="Blocked"/> — an admin took it down; never fetched again.</item>
/// </list>
/// The logo belongs to a shared company row that one user's capture pointed at, so it is shown
/// only on the private board of users who applied there (DECISIONS.md 2026-09-27).
/// </summary>
public sealed class CompanyLogo
{
    public Guid CompanyId { get; private set; }

    public byte[]? Content { get; private set; }

    public string? ContentType { get; private set; }

    public DateTimeOffset CheckedAt { get; private set; }

    public bool Blocked { get; private set; }

    private CompanyLogo()
    {
    }

    public static CompanyLogo For(Guid companyId) => new() { CompanyId = companyId };

    public void Found(byte[] content, string contentType, DateTimeOffset now)
    {
        Content = content;
        ContentType = contentType;
        CheckedAt = now;
    }

    public void NotFound(DateTimeOffset now)
    {
        Content = null;
        ContentType = null;
        CheckedAt = now;
    }

    public void Block(DateTimeOffset now)
    {
        NotFound(now);
        Blocked = true;
    }
}
