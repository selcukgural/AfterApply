using AfterApply.Domain.Common;

namespace AfterApply.Application.EmailIntegrations;

/// <summary>One scored category of evidence that an email is recruitment-related. Deliberately
/// carries no raw email content — Category/Source are enough to log a routing decision without ever
/// risking PII in a log line (see RecruitmentSignalAnalyzer's own privacy note).</summary>
public sealed record RecruitmentSignal(string Category, int Weight, string Source);

public sealed record RecruitmentSignalAnalysis(int Score, IReadOnlyCollection<RecruitmentSignal> Signals);

/// <summary>No C# defaults on purpose — every value must come from appsettings.json's
/// EmailIntelligence:Weights section. EmailIntelligenceConfigurationValidator (Infrastructure)
/// fails app startup if any is missing, so a value here is never silently 0/unset at runtime.</summary>
public sealed class EmailIntelligenceWeights
{
    public required int MatchedApplication { get; init; }
    public required int ApplicationPhrase { get; init; }
    public required int InterviewPhrase { get; init; }
    public required int AssessmentPhrase { get; init; }
    public required int OfferPhrase { get; init; }
    public required int RecruiterSignal { get; init; }
    public required int KnownJobBoardOrAts { get; init; }
    public required int CalendarLink { get; init; }
    public required int ApplicationLink { get; init; }
    public required int CompanyNameInSubject { get; init; }

    public required int Newsletter { get; init; }
    public required int Unsubscribe { get; init; }
    public required int Marketing { get; init; }
    public required int JobAlert { get; init; }
    public required int Digest { get; init; }

    public required int ApplicationCap { get; init; }
    public required int InterviewCap { get; init; }
    public required int AssessmentCap { get; init; }
    public required int OfferCap { get; init; }
    public required int RecruiterCap { get; init; }
    public required int AtsCap { get; init; }
    public required int CompanyMatchCap { get; init; }
    public required int LinksCap { get; init; }
    public required int NegativeCap { get; init; }
}

/// <summary>Every phrase/domain vocabulary RecruitmentSignalAnalyzer matches against — fully
/// config-driven (bound from appsettings.json's EmailIntelligence:Phrases section) so tuning which
/// words count as evidence never requires a code change/deploy, only an appsettings edit + restart.
/// No C# defaults on purpose (see EmailIntelligenceWeights) — EmailIntelligenceConfigurationValidator
/// fails app startup if any list here is missing or empty.</summary>
public sealed class EmailIntelligencePhrases
{
    public required string[] Application { get; init; }
    public required string[] Interview { get; init; }
    public required string[] Assessment { get; init; }
    public required string[] Offer { get; init; }

    /// <summary>Recruiter/HR vocabulary matched against the email body/subject.</summary>
    public required string[] Recruiter { get; init; }

    /// <summary>Sender local-part prefixes that suggest a recruiting mailbox (e.g. "careers@",
    /// "jobs-noreply@"). Neutral local-parts (newsletter@, marketing@, sales@, support@, billing@)
    /// are deliberately absent from this list, not negatively weighted either — that would
    /// double-count against the body-level Newsletter/Marketing phrase signals for the same email.</summary>
    public required string[] RecruiterLocalPartPrefixes { get; init; }

    /// <summary>Local parts checked for exact equality rather than prefix (e.g. "hr" alone — a
    /// prefix check would also match unrelated words like "hristo").</summary>
    public required string[] RecruiterLocalPartExact { get; init; }

    public required string[] Newsletter { get; init; }
    public required string[] Unsubscribe { get; init; }
    public required string[] Marketing { get; init; }
    public required string[] JobAlert { get; init; }
    public required string[] Digest { get; init; }
    public required string[] AtsLinkDomains { get; init; }
    public required string[] CalendarLinkDomains { get; init; }
}

/// <summary>No C# defaults anywhere in this options tree, by design — see
/// EmailIntelligenceConfigurationValidator (Infrastructure), which walks this type's own property
/// tree via reflection at startup and fails the app if appsettings.json's EmailIntelligence section
/// is missing so much as one weight or phrase list. `required` here documents that intent and keeps
/// any direct (non-config-bound) construction — e.g. in tests — honest about what must be supplied;
/// the configuration binder itself doesn't enforce `required`, which is exactly why the validator
/// exists.</summary>
public sealed class EmailIntelligenceOptions
{
    public required int LowThreshold { get; init; }
    public required int LlmThreshold { get; init; }
    public required int HighConfidenceThreshold { get; init; }
    public required EmailIntelligenceWeights Weights { get; init; }
    public required EmailIntelligencePhrases Phrases { get; init; }
}

/// <summary>Decides whether an email that RuleBasedEmailClassifier couldn't definitively classify is
/// still worth an LLM call — the pre-LLM recall layer described in
/// e-kariyerim-pre-llm-email-intelligence-plan.md. Phrase tables (EmailIntelligencePhrases) are
/// deliberately broader/more recall-oriented than RuleBasedEmailClassifier's own — they only ever run
/// after that classifier has already returned "NoMatch" for the same text, so an identical (narrow)
/// phrase list would contribute near-zero signal by construction. isKnownSender-style evidence (known
/// job board/ATS domain, matched an existing Application) is folded in here as just two more weighted
/// categories, not a separate gate.</summary>
public static class RecruitmentSignalAnalyzer
{
    public static RecruitmentSignalAnalysis Analyze(
        string senderEmail, string subject, string snippet, string? senderDomain,
        bool isKnownJobBoardOrAtsDomain, bool hasApplicationMatch,
        IReadOnlyCollection<string> linkDomains, EmailIntelligenceOptions options)
    {
        var weights = options.Weights;
        var phrases = options.Phrases;
        var text = TurkishTextNormalizer.FoldCase($"{subject} {snippet}");
        var signals = new List<RecruitmentSignal>();

        AddPositiveCategory(signals, "Application", "Body", CountMatches(text, phrases.Application), weights.ApplicationPhrase, weights.ApplicationCap);
        AddPositiveCategory(signals, "Interview", "Body", CountMatches(text, phrases.Interview), weights.InterviewPhrase, weights.InterviewCap);
        AddPositiveCategory(signals, "Assessment", "Body", CountMatches(text, phrases.Assessment), weights.AssessmentPhrase, weights.AssessmentCap);
        AddPositiveCategory(signals, "Offer", "Body", CountMatches(text, phrases.Offer), weights.OfferPhrase, weights.OfferCap);

        var recruiterHits = CountMatches(text, phrases.Recruiter) + (HasRecruiterLocalPart(senderEmail, phrases) ? 1 : 0);
        AddPositiveCategory(signals, "Recruiter", "SenderOrBody", recruiterHits, weights.RecruiterSignal, weights.RecruiterCap);

        AddNegativeCategory(signals, "Newsletter", "Body", CountMatches(text, phrases.Newsletter), weights.Newsletter, weights.NegativeCap);
        AddNegativeCategory(signals, "Unsubscribe", "Body", CountMatches(text, phrases.Unsubscribe), weights.Unsubscribe, weights.NegativeCap);
        AddNegativeCategory(signals, "Marketing", "Body", CountMatches(text, phrases.Marketing), weights.Marketing, weights.NegativeCap);
        AddNegativeCategory(signals, "JobAlert", "Body", CountMatches(text, phrases.JobAlert), weights.JobAlert, weights.NegativeCap);
        AddNegativeCategory(signals, "Digest", "Body", CountMatches(text, phrases.Digest), weights.Digest, weights.NegativeCap);

        if (isKnownJobBoardOrAtsDomain)
        {
            signals.Add(new RecruitmentSignal("KnownJobBoardOrAts", Math.Min(weights.AtsCap, weights.KnownJobBoardOrAts), "SenderDomain"));
        }

        if (hasApplicationMatch)
        {
            signals.Add(new RecruitmentSignal("MatchedApplication", Math.Min(weights.CompanyMatchCap, weights.MatchedApplication), "ApplicationMatch"));
        }

        if (SubjectMentionsSenderDomainName(senderDomain, subject))
        {
            signals.Add(new RecruitmentSignal("CompanyNameInSubject", weights.CompanyNameInSubject, "Subject"));
        }

        var atsLinkHits = linkDomains.Count(d => MatchesAnyDomain(d, phrases.AtsLinkDomains));
        var calendarLinkHits = linkDomains.Count(d => MatchesAnyDomain(d, phrases.CalendarLinkDomains));
        var linksRaw = atsLinkHits * weights.ApplicationLink + calendarLinkHits * weights.CalendarLink;
        if (linksRaw > 0)
        {
            signals.Add(new RecruitmentSignal("Links", Math.Min(weights.LinksCap, linksRaw), "Links"));
        }

        var total = signals.Sum(s => s.Weight);
        return new RecruitmentSignalAnalysis(Math.Max(0, total), signals);
    }

    private static void AddPositiveCategory(List<RecruitmentSignal> signals, string category, string source, int hitCount, int weightPerHit, int cap)
    {
        if (hitCount == 0)
        {
            return;
        }

        signals.Add(new RecruitmentSignal(category, Math.Min(cap, hitCount * weightPerHit), source));
    }

    private static void AddNegativeCategory(List<RecruitmentSignal> signals, string category, string source, int hitCount, int weightPerHit, int cap)
    {
        if (hitCount == 0)
        {
            return;
        }

        signals.Add(new RecruitmentSignal(category, Math.Max(cap, hitCount * weightPerHit), source));
    }

    private static int CountMatches(string normalizedText, string[] phrases) =>
        phrases.Count(p => normalizedText.Contains(TurkishTextNormalizer.FoldCase(p), StringComparison.Ordinal));

    private static bool HasRecruiterLocalPart(string senderEmail, EmailIntelligencePhrases phrases)
    {
        var atIndex = senderEmail.IndexOf('@');
        if (atIndex <= 0)
        {
            return false;
        }

        var localPart = TurkishTextNormalizer.FoldCase(senderEmail[..atIndex]);
        return phrases.RecruiterLocalPartPrefixes.Any(prefix => localPart.StartsWith(prefix, StringComparison.Ordinal))
            || phrases.RecruiterLocalPartExact.Any(exact => localPart == exact);
    }

    // Deliberately simple: the registrable label before the sender domain's first dot (e.g. "acme"
    // from "acme.com") appearing in the subject is a weak but free "this looks like real
    // correspondence from that company" signal — no existing Application/candidate list needed.
    private static bool SubjectMentionsSenderDomainName(string? senderDomain, string subject)
    {
        if (string.IsNullOrWhiteSpace(senderDomain))
        {
            return false;
        }

        var label = senderDomain.Split('.')[0];
        return label.Length >= 3 && TurkishTextNormalizer.FoldCase(subject).Contains(TurkishTextNormalizer.FoldCase(label), StringComparison.Ordinal);
    }

    private static bool MatchesAnyDomain(string domain, string[] knownDomains) =>
        knownDomains.Any(known => domain.Equals(known, StringComparison.OrdinalIgnoreCase) ||
                                   domain.EndsWith("." + known, StringComparison.OrdinalIgnoreCase));
}
