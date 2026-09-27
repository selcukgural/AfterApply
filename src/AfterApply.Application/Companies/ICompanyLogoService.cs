namespace AfterApply.Application.Companies;

/// <summary>
/// Company logos for the applications board (DECISIONS.md 2026-09-27): fetched server-side from the
/// company's LinkedIn page, kept in our own database, served only to users who applied to (or
/// saved a posting at) that company. Every method is a no-op while the board is off.
/// </summary>
public interface ICompanyLogoService
{
    /// <summary>Background job: looks for the company's logo once (again after a while when none
    /// was found). Never throws for a missing or unreachable page.</summary>
    Task FetchAsync(Guid companyId, CancellationToken cancellationToken);

    /// <summary>Nightly: schedules fetches for companies people applied to that have never been
    /// looked at, spread out so LinkedIn sees a trickle. Returns how many were scheduled.</summary>
    Task<int> ScheduleBackfillAsync(CancellationToken cancellationToken);

    /// <summary>The logo, if there is one and the user has an application or saved posting at the
    /// company; null otherwise (the caller answers 404 either way).</summary>
    Task<CompanyLogoFile?> GetForUserAsync(Guid userId, Guid companyId, CancellationToken cancellationToken);

    /// <summary>Admin: takes a logo down and keeps it from being fetched again. False when there
    /// is no such company.</summary>
    Task<bool> BlockAsync(Guid companyId, CancellationToken cancellationToken);
}

public sealed record CompanyLogoFile(byte[] Content, string ContentType);
