using AfterApply.Domain.Companies;

namespace AfterApply.Application.EmailIntegrations;

/// <summary>Pre-filtered by the caller to the user's own non-terminal Applications. CompanyWebsiteDomain
/// is the already-parsed domain from Company.Website (null when Website isn't set), and
/// NormalizedCompanyName is CompanyNameNormalizer.Normalize(companyName) — both computed by the
/// Infrastructure caller, not this pure matcher, since they're derived from persisted data.</summary>
public sealed record ApplicationMatchCandidate(Guid ApplicationId, string NormalizedCompanyName, string? CompanyWebsiteDomain);

public static class EmailApplicationMatcher
{
    /// <summary>recipientEmail/ownAccountEmail let a message the user sent themselves (e.g. "I accept
    /// the offer" replies) still match: when senderEmail is the user's own account, the recipient's
    /// domain is checked instead of the sender's, since the sender is never the company in that case.</summary>
    public static Guid? Match(string senderEmail, string senderDisplayName, string recipientEmail,
        string ownAccountEmail, string subject, IReadOnlyList<ApplicationMatchCandidate> candidates)
    {
        var isSelfSent = string.Equals(senderEmail, ownAccountEmail, StringComparison.OrdinalIgnoreCase);
        var domainToCheck = isSelfSent ? recipientEmail : senderEmail;
        var domain = ExtractDomain(domainToCheck);

        if (domain is not null)
        {
            var domainMatch = candidates.FirstOrDefault(c =>
                c.CompanyWebsiteDomain is not null &&
                string.Equals(c.CompanyWebsiteDomain, domain, StringComparison.OrdinalIgnoreCase));

            if (domainMatch is not null)
            {
                return domainMatch.ApplicationId;
            }
        }

        // Self-sent messages have no meaningful "sender display name" (it's the user's own name),
        // so only the subject is useful for the name-fallback in that case.
        var normalizedDisplayName = isSelfSent ? "" : CompanyNameNormalizer.Normalize(senderDisplayName);
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
