using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.CandidateExperiences;
using AfterApply.Infrastructure.CompanyReviews;
using AfterApply.Infrastructure.CompanySalaries;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Companies;

/// <summary>How many different people must point a company at the same profile page before the
/// website read from that page is shown to anyone but them (2026-09-24). Bound from <c>CompanyProfiles</c>.</summary>
public sealed class CompanyProfileOptions
{
    public const string SectionName = "CompanyProfiles";

    public int MinimumConfirmingUsers { get; init; } = 2;
}

/// <summary>
/// The two rules for what a shared company row shows to people other than the ones who created it
/// (2026-09-24), in one place so every read path applies the same ones.
///
/// <list type="bullet">
/// <item><b>Listed.</b> The company table is built from people's own applications, so a company's
/// mere existence says "somebody here applied there". A company is listed — has a public page, can
/// be found by name — once somebody contributed to it (a review that was not rejected, a salary
/// entry, a candidate experience) or once
/// <see cref="CompanyReviewOptions.KnownCompanyMinimumApplicants"/> different people applied to it.
/// A signed-in user also sees the companies they applied to or track themselves.</item>
/// <item><b>Confirmed website.</b> The website is read from a profile page one user's capture
/// pointed at. It is shown publicly only once <see cref="CompanyProfileOptions.MinimumConfirmingUsers"/>
/// different people pointed the company at that same page.</item>
/// </list>
/// </summary>
internal sealed class CompanyVisibility(
    AppDbContext dbContext,
    IOptions<CompanyReviewOptions> reviewOptions,
    IOptions<CompanySalaryOptions> salaryOptions,
    IOptions<CandidateExperienceOptions> experienceOptions,
    IOptions<CompanyProfileOptions> profileOptions)
{
    public IQueryable<Company> Listed(IQueryable<Company> companies)
    {
        var minimumApplicants = reviewOptions.Value.KnownCompanyMinimumApplicants;
        var salariesOn = salaryOptions.Value.Enabled;
        var experiencesOn = experienceOptions.Value.Enabled;

        return companies.Where(c =>
            dbContext.CompanyReviews.Any(r => r.CompanyId == c.Id && r.Status != ReviewModerationStatus.Rejected)
            || (salariesOn && dbContext.CompanySalaryEntries.Any(s => s.CompanyId == c.Id))
            || (experiencesOn && dbContext.CandidateExperiences.Any(e => e.CompanyId == c.Id))
            || dbContext.Applications.Where(a => a.CompanyId == c.Id).Select(a => a.UserId).Distinct().Count() >= minimumApplicants);
    }

    /// <summary><see cref="Listed"/>, plus the companies this user applied to or tracks — they
    /// know those exist already.</summary>
    public IQueryable<Company> VisibleTo(IQueryable<Company> companies, Guid userId)
    {
        var listed = Listed(dbContext.Companies).Select(c => c.Id);
        return companies.Where(c =>
            listed.Contains(c.Id)
            || dbContext.Applications.Any(a => a.CompanyId == c.Id && a.UserId == userId)
            || dbContext.TrackedJobs.Any(t => t.CompanyId == c.Id && t.UserId == userId));
    }

    public async Task<bool> IsListedAsync(Guid companyId, CancellationToken cancellationToken) =>
        await Listed(dbContext.Companies.Where(c => c.Id == companyId)).AnyAsync(cancellationToken);

    /// <summary>Of <paramref name="companyIds"/>, the ones whose website may be shown publicly.</summary>
    public async Task<IReadOnlySet<Guid>> ConfirmedWebsitesAsync(IReadOnlyCollection<Guid> companyIds, CancellationToken cancellationToken)
    {
        if (companyIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var companies = await dbContext.Companies
            .Where(c => companyIds.Contains(c.Id) && c.Website != null)
            .Select(c => new { c.Id, c.LinkedInUrl, c.KariyerNetUrl, c.WebsiteSource })
            .ToListAsync(cancellationToken);
        if (companies.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var ids = companies.Select(c => c.Id).ToList();
        var submissions = await dbContext.CompanyProfileSubmissions
            .Where(s => ids.Contains(s.CompanyId))
            .Select(s => new { s.CompanyId, s.Platform, s.Url, s.UserId })
            .ToListAsync(cancellationToken);
        var byCompany = submissions.ToLookup(s => s.CompanyId);
        var minimum = profileOptions.Value.MinimumConfirmingUsers;

        var confirmed = new HashSet<Guid>();
        foreach (var company in companies)
        {
            // The page the website was read from; a website stored before the source was recorded
            // may have come from either, so either page's confirmations count for it.
            var pages = new List<(Source Platform, string Url)>();
            if (company.LinkedInUrl is not null && company.WebsiteSource is null or Source.LinkedIn)
            {
                pages.Add((Source.LinkedIn, CompanyProfileUrl.Canonical(company.LinkedInUrl)));
            }

            if (company.KariyerNetUrl is not null && company.WebsiteSource is null or Source.KariyerNet)
            {
                pages.Add((Source.KariyerNet, CompanyProfileUrl.Canonical(company.KariyerNetUrl)));
            }

            var confirmingUsers = pages
                .Select(page => byCompany[company.Id]
                    .Where(s => s.Platform == page.Platform && s.Url == page.Url)
                    .Select(s => s.UserId)
                    .Distinct()
                    .Count())
                .DefaultIfEmpty(0)
                .Max();

            if (confirmingUsers >= minimum)
            {
                confirmed.Add(company.Id);
            }
        }

        return confirmed;
    }
}
