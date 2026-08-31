namespace AfterApply.Infrastructure.EmailIntegrations;

/// <summary>Config for the curated job board/ATS domain allow-list — see JobBoardDomainMatcher.
/// Deliberately just a flat list, no per-entry metadata: broader coverage comes from the per-user
/// application-domain match (EmailForwardingService.BuildCandidatesAsync), not from growing this
/// list.</summary>
public sealed class JobBoardDomainsOptions
{
    public string[] Domains { get; init; } = [];
}
