using AfterApply.Application.EmailIntegrations;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.EmailIntegrations;

internal sealed class JobBoardDomainMatcher(IOptions<JobBoardDomainsOptions> options) : IJobBoardDomainMatcher
{
    public bool IsKnown(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        foreach (var known in options.Value.Domains)
        {
            if (domain.Equals(known, StringComparison.OrdinalIgnoreCase) ||
                domain.EndsWith("." + known, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
