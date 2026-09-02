namespace AfterApply.Domain.EmailIntegrations;

/// <summary>How EmailApplicationMatcher.Match found the Application a suggestion is tied to.
/// DomainMatch is the only signal strong enough to gate auto-apply — see
/// EmailForwardingService.TryAutoApplyAsync.</summary>
public enum EmailApplicationMatchType
{
    DomainMatch,
    NameFallbackMatch
}
