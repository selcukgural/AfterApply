using AfterApply.Domain.Common;

namespace AfterApply.Domain.Companies;

public sealed class Company : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    public string? LinkedInUrl { get; private set; }

    /// <summary>The company's kariyer.net profile page (kariyer.net/firma-profil/...), read off
    /// the job posting's own company link. kariyer.net's counterpart to LinkedInUrl, and the only
    /// enrichment source we have for a company that has never appeared in a LinkedIn posting.</summary>
    public string? KariyerNetUrl { get; private set; }

    public string? Industry { get; private set; }

    public string? Country { get; private set; }

    /// <summary>The segment of the company's public page (<c>/companies/{slug}</c>), from
    /// <see cref="CompanySlugGenerator"/>. Nullable in the database on purpose: a NOT NULL column
    /// would fail inserts from the still-running old instances during a Cloud Run rollout. Code
    /// always sets it on create, the migration backfilled every existing row, and the review
    /// service assigns one lazily if it ever meets a null — so a null here is a window of minutes,
    /// not a state the product has to support.</summary>
    public string? Slug { get; private set; }

    private Company()
    {
    }

    public static Company Create(string name, DateTimeOffset now, string? website = null,
        string? linkedInUrl = null, string? industry = null, string? country = null,
        string? kariyerNetUrl = null, string? slug = null)
    {
        return new Company
        {
            Name = name,
            NormalizedName = CompanyNameNormalizer.Normalize(name),
            Slug = slug ?? CompanySlugGenerator.Generate(name),
            Website = website,
            LinkedInUrl = linkedInUrl,
            KariyerNetUrl = kariyerNetUrl,
            Industry = industry,
            Country = country,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>Backfill only: fills a slug the row never got (see <see cref="Slug"/>). Never
    /// replaces one — a slug is a published URL.</summary>
    public void AssignSlug(string slug, DateTimeOffset now)
    {
        if (Slug is not null)
        {
            return;
        }

        Slug = slug;
        Touch(now);
    }

    // Backfill only — a company resolved by exact-name match may predate the extension carrying
    // either profile URL at all. Never overwrites an existing value: whatever is already stored
    // (set manually, or by an earlier import) wins over a later guess. The two are independent: a
    // company can pick up a kariyer.net profile long after it got its LinkedIn one, or vice versa.
    public void SetProfileLinksIfMissing(string? linkedInUrl, string? kariyerNetUrl, DateTimeOffset now)
    {
        var changed = false;

        if (LinkedInUrl is null && linkedInUrl is not null)
        {
            LinkedInUrl = linkedInUrl;
            changed = true;
        }

        if (KariyerNetUrl is null && kariyerNetUrl is not null)
        {
            KariyerNetUrl = kariyerNetUrl;
            changed = true;
        }

        if (changed)
        {
            Touch(now);
        }
    }

    // Fills in only the fields still missing — CompanyEnrichmentService's best-effort fetch of the
    // LinkedIn company page never overwrites a value that already got set some other way (manual
    // entry, a prior successful enrichment).
    public void EnrichFrom(string? website, string? industry, string? country, DateTimeOffset now)
    {
        var changed = false;

        if (Website is null && website is not null)
        {
            Website = website;
            changed = true;
        }

        if (Industry is null && industry is not null)
        {
            Industry = industry;
            changed = true;
        }

        if (Country is null && country is not null)
        {
            Country = country;
            changed = true;
        }

        if (changed)
        {
            Touch(now);
        }
    }
}
