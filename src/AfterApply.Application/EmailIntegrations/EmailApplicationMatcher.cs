using AfterApply.Domain.Companies;

namespace AfterApply.Application.EmailIntegrations;

/// <summary>Pre-filtered by the caller to the user's own non-terminal Applications. CompanyWebsiteDomain
/// is the already-parsed domain from Company.Website (null when Website isn't set), and
/// NormalizedCompanyName is CompanyNameNormalizer.Normalize(companyName) — both computed by the
/// Infrastructure caller, not this pure matcher, since they're derived from persisted data.</summary>
public sealed record ApplicationMatchCandidate(Guid ApplicationId, string NormalizedCompanyName, string? CompanyWebsiteDomain);

public static class EmailApplicationMatcher
{
    public static Guid? Match(string senderEmail, string senderDisplayName, string subject,
        IReadOnlyList<ApplicationMatchCandidate> candidates)
    {
        var senderDomain = ExtractDomain(senderEmail);

        if (senderDomain is not null)
        {
            var domainMatch = candidates.FirstOrDefault(c =>
                c.CompanyWebsiteDomain is not null &&
                string.Equals(c.CompanyWebsiteDomain, senderDomain, StringComparison.OrdinalIgnoreCase));

            if (domainMatch is not null)
            {
                return domainMatch.ApplicationId;
            }
        }

        var normalizedDisplayName = CompanyNameNormalizer.Normalize(senderDisplayName);
        var normalizedSubject = CompanyNameNormalizer.Normalize(subject);

        var nameMatch = candidates.FirstOrDefault(c =>
            !string.IsNullOrWhiteSpace(c.NormalizedCompanyName) &&
            (normalizedDisplayName.Contains(c.NormalizedCompanyName, StringComparison.Ordinal) ||
             normalizedSubject.Contains(c.NormalizedCompanyName, StringComparison.Ordinal)));

        return nameMatch?.ApplicationId;
    }

    private static string? ExtractDomain(string email)
    {
        var atIndex = email.IndexOf('@');
        return atIndex >= 0 && atIndex < email.Length - 1 ? email[(atIndex + 1)..].Trim().ToLowerInvariant() : null;
    }
}
