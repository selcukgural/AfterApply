using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

namespace AfterApply.Application.EmailIntegrations;

public sealed record EmailClassificationResult(ApplicationStatus? SuggestedStatus, double ConfidenceScore, string MatchedRule);

/// <summary>Zero-cost, zero-latency fast path for the most obvious, lowest-risk phrasings — tried
/// before falling back to the LLM-based IEmailClassificationProvider. Deliberately small: broader
/// coverage now comes from the LLM, not from growing this table (see DECISIONS.md).</summary>
public static class RuleBasedEmailClassifier
{
    private sealed record ClassificationRule(string[] Phrases, ApplicationStatus? TargetStatus, string RuleLabel, double Weight);

    // Data-driven ruleset — add a new phrase/rule as a one-line addition, not a
    // code restructure. Starting rules come directly from spec §10's own examples.
    private static readonly ClassificationRule[] Rules =
    [
        new(["invite you to an interview", "invited to interview", "schedule an interview", "mülakata davet"],
            ApplicationStatus.Interview, "InterviewInvitation", 0.85),
        new(["unfortunately", "moving forward with other candidates", "will not be moving forward", "maalesef", "olumsuz"],
            ApplicationStatus.Rejected, "Rejection", 0.85),
        new(["we will get back to you", "still under review", "will be in touch", "değerlendirme sürecinde"],
            null, "StillWaiting", 0.5),
        // The very first automated reply an ATS sends right after a candidate applies on a
        // company's own career site — no interview/rejection/waiting language yet, just an
        // acknowledgement. Null status (like StillWaiting) rather than ApplicationStatus.Applied:
        // for an already-registered application this is a content-free no-op either way, but null
        // additionally means EmailForwardingService's matched-application branch treats it as no
        // signal at all (see its own hasSignal check) instead of surfacing a pointless
        // "confirm Applied" suggestion for an application that's already sitting at Applied.
        // Still worth passing as a signal for an *unmatched* sender, though — this acknowledgement
        // is the only evidence a "new job" suggestion has to go on. Phrases cover EN/DE/TR since a
        // job board or ATS's confirmation email is rarely in the user's own UI language.
        new(["we have received your application", "thank you for your application",
                "thank you so much for your application", "your application has been received",
                "received your application", "thanks for applying", "thank you for applying",
                "ihre bewerbung", "deine bewerbung", "bewerbung eingegangen", "bewerbung erhalten",
                "bedanken uns für deine bewerbung", "bedanken uns für ihre bewerbung",
                "interesse an einer tätigkeit", "interesse an einer position",
                "başvurunuz alındı", "başvurunuz için teşekkür ederiz", "başvurunuz tarafımıza ulaştı",
                "başvurunuz bize ulaştı"],
            null, "ApplicationReceived", 0.6)
    ];

    public static EmailClassificationResult Classify(string subject, string snippet)
    {
        var text = TurkishTextNormalizer.FoldCase($"{subject} {snippet}");

        ClassificationRule? bestRule = null;
        var bestMatchCount = 0;

        foreach (var rule in Rules)
        {
            var matchCount = rule.Phrases.Count(p => text.Contains(TurkishTextNormalizer.FoldCase(p), StringComparison.Ordinal));
            if (matchCount == 0)
            {
                continue;
            }

            if (bestRule is null || IsBetterMatch(rule, bestRule))
            {
                bestRule = rule;
                bestMatchCount = matchCount;
            }
        }

        if (bestRule is null)
        {
            return new EmailClassificationResult(null, 0, "NoMatch");
        }

        var confidence = Math.Min(0.95, bestRule.Weight + (bestMatchCount - 1) * 0.05);
        return new EmailClassificationResult(bestRule.TargetStatus, confidence, bestRule.RuleLabel);
    }

    private static bool IsBetterMatch(ClassificationRule candidate, ClassificationRule current)
    {
        if (candidate.Weight != current.Weight)
        {
            return candidate.Weight > current.Weight;
        }

        // Tie-break: Rejection wins over a same-weight rule — a false "still
        // interviewing" suggestion is worse than being cautious about a rejection.
        return candidate.RuleLabel == "Rejection" && current.RuleLabel != "Rejection";
    }
}
