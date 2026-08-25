using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;

namespace AfterApply.Application.EmailIntegrations;

public sealed record EmailClassificationResult(ApplicationStatus? SuggestedStatus, double ConfidenceScore, string MatchedRule);

public static class EmailClassifier
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
            null, "StillWaiting", 0.5)
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
