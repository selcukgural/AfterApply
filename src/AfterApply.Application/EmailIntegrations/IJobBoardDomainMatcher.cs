namespace AfterApply.Application.EmailIntegrations;

/// <summary>Port to the curated job board/ATS domain allow-list (JobBoardDomainsOptions,
/// Infrastructure layer — config-driven, not hardcoded, so the list can be extended without a
/// deploy). Used by EmailForwardingService to decide whether an unmatched sender is trusted
/// enough to justify an LLM classification call.</summary>
public interface IJobBoardDomainMatcher
{
    bool IsKnown(string? domain);
}
