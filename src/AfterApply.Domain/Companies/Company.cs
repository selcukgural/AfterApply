using AfterApply.Domain.Common;

namespace AfterApply.Domain.Companies;

public sealed class Company : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    /// <summary>Which profile page <see cref="Website"/> was read from (LinkedIn or KariyerNet), so
    /// the public page can show it only once enough different people pointed at that same page
    /// (2026-09-24). Null for a website stored before this was recorded.</summary>
    public Source? WebsiteSource { get; private set; }

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

    private readonly List<CompanyProfileLink> _profileLinks = [];

    /// <summary>Pages on platforms beyond the original LinkedIn/kariyer.net pair — see
    /// <see cref="CompanyProfileLink"/> for why those two are still columns. Only loaded when a
    /// query asks for it; nothing on the hot application-creation path reads it.</summary>
    public IReadOnlyCollection<CompanyProfileLink> ProfileLinks => _profileLinks;

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

    /// <summary>
    /// Records the company's page on a platform it has none stored for yet. Same fill-if-missing
    /// rule as <see cref="SetProfileLinksIfMissing"/>: whatever is already there wins over a later
    /// guess, because these arrive from a page scrape and the first one is no more likely to be
    /// wrong than the second.
    ///
    /// Returns the new link, or null when there was already one for that platform. The caller gets
    /// it back rather than just a bool because the persistence layer has to insert it explicitly:
    /// <see cref="Entity.Id"/> is assigned in the constructor, so EF sees a child with a non-default
    /// key appear in a tracked collection and marks it Modified — an UPDATE against a row that does
    /// not exist yet. Adding it to its own DbSet is what makes it an INSERT.
    /// </summary>
    public CompanyProfileLink? AddProfileLinkIfMissing(Source platform, string? url, DateTimeOffset now)
    {
        if (url is null || _profileLinks.Any(link => link.Platform == platform))
        {
            return null;
        }

        var link = CompanyProfileLink.Create(Id, platform, url, now);
        _profileLinks.Add(link);
        Touch(now);
        return link;
    }

    // Fills in only the fields still missing — CompanyEnrichmentService's best-effort fetch of the
    // LinkedIn company page never overwrites a value that already got set some other way (manual
    // entry, a prior successful enrichment).
    public void EnrichFrom(Source source, string? website, string? industry, string? country, DateTimeOffset now)
    {
        var changed = false;

        if (Website is null && website is not null)
        {
            Website = website;
            WebsiteSource = source;
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

    /// <summary>
    /// Drops a profile link whose page turned out to name a different company (2026-09-24). The
    /// link came from one user's capture; clearing it lets a later capture that points at the right
    /// page fill the slot, where keeping it would lock the wrong page in for good.
    /// </summary>
    public void ClearProfileLink(Source platform, DateTimeOffset now)
    {
        switch (platform)
        {
            case Source.LinkedIn when LinkedInUrl is not null:
                LinkedInUrl = null;
                break;
            case Source.KariyerNet when KariyerNetUrl is not null:
                KariyerNetUrl = null;
                break;
            default:
                return;
        }

        Touch(now);
    }
}
