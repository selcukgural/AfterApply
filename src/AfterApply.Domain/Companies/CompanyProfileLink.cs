using AfterApply.Domain.Common;

namespace AfterApply.Domain.Companies;

/// <summary>
/// One company's page on one platform, beyond the two the product started with.
///
/// <c>Company.LinkedInUrl</c> and <c>Company.KariyerNetUrl</c> stay as columns on purpose and are
/// not migrated here: enrichment, the company detail response, the data export and the
/// review/salary pages all read them directly, and moving them would be a change of its own with
/// no benefit to this one. So the split is: those two columns are the original pair, this table is
/// every platform added since. A reader meeting both should know that is deliberate, not an
/// accident — and that folding the pair in later is the tidy-up, not a bug fix.
///
/// The reason for a table rather than a sixth, seventh, eighth nullable column is the same reason
/// the extension grew an adapter registry: one column per platform stops scaling the moment the
/// list is open-ended.
/// </summary>
public sealed class CompanyProfileLink : AuditableEntity
{
    public Guid CompanyId { get; private set; }

    /// <summary>Which platform's page this is — the same <see cref="Source"/> a posting from that
    /// platform gets as <c>Job.Source</c>, so the two are directly comparable.</summary>
    public Source Platform { get; private set; }

    public string Url { get; private set; } = string.Empty;

    private CompanyProfileLink()
    {
    }

    public static CompanyProfileLink Create(Guid companyId, Source platform, string url, DateTimeOffset now) =>
        new()
        {
            CompanyId = companyId,
            Platform = platform,
            Url = url,
            CreatedAt = now,
            UpdatedAt = now
        };
}
