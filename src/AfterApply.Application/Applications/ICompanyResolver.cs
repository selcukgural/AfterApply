using AfterApply.Domain.Common;

namespace AfterApply.Application.Applications;

/// <summary>
/// The company-profile pages a job posting pointed at, carried from wherever that posting was read
/// (the extension's DOM scrape today). Both are backfilled onto the Company only when it has none,
/// and both are later fetched server-side by CompanyEnrichmentService — so a caller-supplied value
/// is always re-validated against its own host allow-list before use, never trusted just because
/// it round-tripped through a client.
/// </summary>
/// <param name="AtsUrl">The company's board on the ATS that hosted the posting, with
/// <paramref name="AtsPlatform"/> saying which one. Unlike the pair above this does not land in a
/// column on Companies — it becomes a <c>CompanyProfileLink</c> row, because the set of platforms
/// is open-ended (see that entity). Both are set together or not at all.</param>
public sealed record CompanyProfileLinks(
    string? LinkedInUrl = null,
    string? KariyerNetUrl = null,
    string? AtsUrl = null,
    Source? AtsPlatform = null)
{
    public bool HasAny => LinkedInUrl is not null || KariyerNetUrl is not null || HasAts;

    public bool HasAts => AtsUrl is not null && AtsPlatform is not null;
}

public interface ICompanyResolver
{
    Task<Guid> ResolveOrCreateAsync(string companyName, CancellationToken cancellationToken,
        CompanyProfileLinks? profileLinks = null);
}
