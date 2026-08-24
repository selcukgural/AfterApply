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
}
