namespace AfterApply.Application.EmailIntegrations;

/// <summary>Serves the Gmail content script (extension/gmail-scan.js) the same weights/phrases/
/// domain vocabulary RecruitmentSignalAnalyzer already uses server-side, so the extension's local
/// pre-filter scorer and the backend's LLM-gating logic are tuned from one appsettings.json source
/// of truth — see GET /api/email-forwarding/local-filter-config.</summary>
public interface ILocalFilterConfigService
{
    /// <summary>ETag is a quoted (RFC 7232), hex-encoded SHA256 hash of the response's canonical
    /// JSON — stable for identical config, so the endpoint can 304 a matching If-None-Match without
    /// re-sending the payload.</summary>
    Task<(LocalFilterConfigResponse Config, string ETag)> GetAsync(CancellationToken cancellationToken);
}

/// <summary>Flat projection of EmailIntelligenceOptions + JobBoardDomainsOptions for the extension's
/// local pre-filter — deliberately a separate wire type from those Infrastructure-internal,
/// config-binding-shaped options classes rather than serializing them directly.</summary>
public sealed record LocalFilterConfigResponse(
    int Threshold, LocalFilterWeightsDto Weights, LocalFilterPhrasesDto Phrases, string[] JobBoardDomains);

/// <summary>Field-for-field mirror of EmailIntelligenceWeights — same names on purpose, so the JS
/// scorer's property access lines up 1:1 and stays easy to eyeball-diff against
/// RecruitmentSignalAnalyzer.cs whenever either changes.</summary>
public sealed record LocalFilterWeightsDto(
    int MatchedApplication, int ApplicationPhrase, int InterviewPhrase, int AssessmentPhrase, int OfferPhrase,
    int RecruiterSignal, int KnownJobBoardOrAts, int CalendarLink, int ApplicationLink, int CompanyNameInSubject,
    int Newsletter, int Unsubscribe, int Marketing, int JobAlert, int Digest,
    int ApplicationCap, int InterviewCap, int AssessmentCap, int OfferCap, int RecruiterCap,
    int AtsCap, int CompanyMatchCap, int LinksCap, int NegativeCap);

/// <summary>Field-for-field mirror of EmailIntelligencePhrases — see LocalFilterWeightsDto's doc
/// comment for why the names match exactly.</summary>
public sealed record LocalFilterPhrasesDto(
    string[] Application, string[] Interview, string[] Assessment, string[] Offer, string[] Recruiter,
    string[] RecruiterLocalPartPrefixes, string[] RecruiterLocalPartExact, string[] Newsletter,
    string[] Unsubscribe, string[] Marketing, string[] JobAlert, string[] Digest,
    string[] AtsLinkDomains, string[] CalendarLinkDomains);
