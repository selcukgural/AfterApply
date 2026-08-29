using AfterApply.Domain.Common;

namespace AfterApply.Domain.Companies;

public sealed class Company : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Website { get; private set; }

    public string? LinkedInUrl { get; private set; }

    public string? Industry { get; private set; }

    public string? Country { get; private set; }

    private Company()
    {
    }

    public static Company Create(string name, DateTimeOffset now, string? website = null,
        string? linkedInUrl = null, string? industry = null, string? country = null)
    {
        return new Company
        {
            Name = name,
            NormalizedName = CompanyNameNormalizer.Normalize(name),
            Website = website,
            LinkedInUrl = linkedInUrl,
            Industry = industry,
            Country = country,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    // Backfill only — a company resolved by exact-name match may predate the extension carrying
    // a LinkedIn URL at all. Never overwrites an existing value: whatever is already stored (set
    // manually, or by an earlier import) wins over a later guess.
    public void SetLinkedInUrlIfMissing(string linkedInUrl, DateTimeOffset now)
    {
        if (LinkedInUrl is not null)
        {
            return;
        }

        LinkedInUrl = linkedInUrl;
        Touch(now);
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
