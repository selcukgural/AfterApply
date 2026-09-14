using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.JobSources;

namespace AfterApply.Application.JobSources;

/// <summary>
/// The gate between a model's answer and the database, outside any provider so every provider
/// passes through the same one. The model's output is untrusted text that ends up rendered to
/// the user: control characters go, list items are capped and de-duplicated, the score is clamped,
/// and a summary that is empty makes the whole answer unusable (a score with no reason is a
/// number the user cannot argue with).
/// </summary>
public static class JobFitScores
{
    public static JobFitScoringResult? Sanitize(JobFitScoringResult? result)
    {
        if (result is null)
        {
            return null;
        }

        var summary = Clean(result.Summary);
        if (summary.Length == 0)
        {
            return null;
        }

        return result with
        {
            Score = Math.Clamp(result.Score, 0, 100),
            Summary = Truncate(summary, UserJobSourceDelivery.MaxSummaryLength),
            MatchedCriteria = CleanList(result.MatchedCriteria),
            MissingCriteria = CleanList(result.MissingCriteria),
            RequiredSkills = CleanList(result.RequiredSkills)
        };
    }

    private static IReadOnlyList<string> CleanList(IReadOnlyList<string>? items) => (items ?? [])
        .Select(Clean)
        .Where(i => i.Length > 0)
        .Select(i => Truncate(i, UserJobSourceDelivery.MaxCriterionLength))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Take(UserJobSourceDelivery.MaxCriteriaItems)
        .ToList();

    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value.Where(c => !char.IsControl(c) || c == ' ').ToArray();
        return JobSourceQueryNormalizer.CollapseWhitespace(new string(chars));
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
