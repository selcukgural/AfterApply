using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AfterApply.Application.EmailIntegrations;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.EmailIntegrations;

internal sealed class LocalFilterConfigService(
    IOptions<EmailIntelligenceOptions> intelligenceOptions,
    IOptions<JobBoardDomainsOptions> jobBoardDomainsOptions) : ILocalFilterConfigService
{
    // No caching: IOptions<T> reads are cheap and this config only changes on a redeploy anyway —
    // recomputing per call keeps this trivially correct instead of needing invalidation logic.
    public Task<(LocalFilterConfigResponse Config, string ETag)> GetAsync(CancellationToken cancellationToken)
    {
        var intelligence = intelligenceOptions.Value;
        var weights = intelligence.Weights;
        var phrases = intelligence.Phrases;

        var config = new LocalFilterConfigResponse(
            intelligence.LocalPrefilterThreshold,
            new LocalFilterWeightsDto(
                weights.MatchedApplication, weights.ApplicationPhrase, weights.InterviewPhrase, weights.AssessmentPhrase,
                weights.OfferPhrase, weights.RecruiterSignal, weights.KnownJobBoardOrAts, weights.CalendarLink,
                weights.ApplicationLink, weights.CompanyNameInSubject, weights.Newsletter, weights.Unsubscribe,
                weights.Marketing, weights.JobAlert, weights.Digest, weights.ApplicationCap, weights.InterviewCap,
                weights.AssessmentCap, weights.OfferCap, weights.RecruiterCap, weights.AtsCap, weights.CompanyMatchCap,
                weights.LinksCap, weights.NegativeCap),
            new LocalFilterPhrasesDto(
                phrases.Application, phrases.Interview, phrases.Assessment, phrases.Offer, phrases.Recruiter,
                phrases.RecruiterLocalPartPrefixes, phrases.RecruiterLocalPartExact, phrases.Newsletter,
                phrases.Unsubscribe, phrases.Marketing, phrases.JobAlert, phrases.Digest,
                phrases.AtsLinkDomains, phrases.CalendarLinkDomains),
            jobBoardDomainsOptions.Value.Domains);

        return Task.FromResult((config, ComputeETag(config)));
    }

    // Same SHA256-hex idiom EmailForwardingService.ComputeIdempotencyKey already uses, applied to
    // the response's canonical JSON instead of pipe-joined fields — quoted per RFC 7232.
    private static string ComputeETag(LocalFilterConfigResponse config)
    {
        var json = JsonSerializer.Serialize(config);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return $"\"{Convert.ToHexString(hash)}\"";
    }
}
