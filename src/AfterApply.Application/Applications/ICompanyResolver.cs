namespace AfterApply.Application.Applications;

/// <summary>
/// The company-profile pages a job posting pointed at, carried from wherever that posting was read
/// (the extension's DOM scrape today). Both are backfilled onto the Company only when it has none,
/// and both are later fetched server-side by CompanyEnrichmentService — so a caller-supplied value
/// is always re-validated against its own host allow-list before use, never trusted just because
/// it round-tripped through a client.
/// </summary>
public sealed record CompanyProfileLinks(string? LinkedInUrl = null, string? KariyerNetUrl = null)
{
    public bool HasAny => LinkedInUrl is not null || KariyerNetUrl is not null;
}

public interface ICompanyResolver
{
    Task<Guid> ResolveOrCreateAsync(string companyName, CancellationToken cancellationToken,
        CompanyProfileLinks? profileLinks = null);
}
