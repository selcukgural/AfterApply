using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.CompanyReviews;

namespace AfterApply.Application.CandidateExperiences;

/// <summary>One entry as the aggregate sees it — already loaded, no author, no date.</summary>
public sealed record ExperienceAggregateRow(
    int OverallRating,
    HiringOutcome? Outcome,
    ProcessDuration? Duration,
    StageCount? Stages,
    IReadOnlyList<ExperienceCategoryRating> CategoryRatings,
    IReadOnlyList<(string Key, ReviewStatementKind Kind)> Picks,
    IReadOnlyList<InterviewType> InterviewTypes);

/// <summary>
/// Everything the company page's "candidate experience" tab publishes about a company, kept pure
/// so a unit test can pin each number. The score is the review formula
/// (<see cref="CompanyReviewScoring"/>) with this feature's own prior weight and threshold; the
/// process statistics are counts and ordinal medians over closed lists.
/// </summary>
public static class CandidateExperienceStats
{
    /// <summary>How many "most picked" statements each list shows.</summary>
    public const int TopStatements = 3;

    private static readonly ExperienceCategory[] OptionalCategories =
        Enum.GetValues<ExperienceCategory>().Where(c => c != ExperienceCategory.Overall).ToArray();

    /// <summary>The public date label: <c>2026-Q3</c>. Quarter, not month, because the company
    /// knows whom it interviewed in a given month.</summary>
    public static string Quarter(DateTimeOffset submittedAt) =>
        $"{submittedAt.Year}-Q{(submittedAt.Month - 1) / 3 + 1}";

    /// <summary>The median of an ordinal enum: the middle value once sorted by declaration order,
    /// the lower of the two middles on an even count (bands cannot be averaged). Null on empty.</summary>
    public static TEnum? OrdinalMedian<TEnum>(IReadOnlyList<TEnum> values) where TEnum : struct, Enum
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.OrderBy(v => Convert.ToInt32(v)).ToArray();
        return sorted[(sorted.Length - 1) / 2];
    }

    public static CandidateExperienceSummaryResponse Build(IReadOnlyList<ExperienceAggregateRow> rows, double globalAverage,
        int minimumForStats, int priorWeight)
    {
        var count = rows.Count;
        var distribution = new int[5];
        var sumOverall = 0d;
        foreach (var row in rows)
        {
            distribution[Math.Clamp(row.OverallRating, 1, 5) - 1]++;
            sumOverall += row.OverallRating;
        }

        var averageOverall = CompanyReviewScoring.CategoryAverage(count, sumOverall);
        var score = CompanyReviewScoring.BayesianScore(count, sumOverall, globalAverage, priorWeight, minimumForStats);
        var atThreshold = count > 0 && count >= minimumForStats;

        var categories = OptionalCategories.Select(category =>
        {
            var ratings = rows.SelectMany(r => r.CategoryRatings).Where(r => r.Category == category).ToArray();
            return new ExperienceCategoryAverageResponse(category, ratings.Length,
                CompanyReviewScoring.ThresholdedAverage(ratings.Length, ratings.Sum(r => r.Rating), minimumForStats));
        }).ToArray();

        var topLiked = atThreshold ? Top(rows, ReviewStatementKind.Liked) : [];
        var topImprovable = atThreshold ? Top(rows, ReviewStatementKind.Improve) : [];

        IReadOnlyList<HiringOutcomeCountResponse> outcomes = atThreshold
            ? rows.Where(r => r.Outcome is not null).GroupBy(r => r.Outcome!.Value)
                .OrderBy(g => g.Key).Select(g => new HiringOutcomeCountResponse(g.Key, g.Count())).ToArray()
            : [];
        var typicalDuration = atThreshold ? OrdinalMedian(rows.Where(r => r.Duration is not null).Select(r => r.Duration!.Value).ToArray()) : null;
        var typicalStages = atThreshold ? OrdinalMedian(rows.Where(r => r.Stages is not null).Select(r => r.Stages!.Value).ToArray()) : null;
        IReadOnlyList<InterviewTypeCountResponse> interviewTypes = atThreshold
            ? rows.SelectMany(r => r.InterviewTypes).GroupBy(t => t)
                .OrderBy(g => g.Key).Select(g => new InterviewTypeCountResponse(g.Key, g.Count())).ToArray()
            : [];
        var takeHome = atThreshold ? rows.Count(r => r.InterviewTypes.Contains(InterviewType.TakeHomeAssignment)) : 0;

        return new CandidateExperienceSummaryResponse(count, score, minimumForStats, priorWeight, averageOverall, categories,
            distribution, topLiked, topImprovable, outcomes, typicalDuration, typicalStages, interviewTypes, takeHome);
    }

    /// <summary>Most picked first; ties broken by key so the list is stable between reloads.</summary>
    private static ExperienceStatementCountResponse[] Top(IReadOnlyList<ExperienceAggregateRow> rows, ReviewStatementKind kind) =>
        rows.SelectMany(r => r.Picks).Where(p => p.Kind == kind)
            .GroupBy(p => p.Key, StringComparer.Ordinal)
            .Select(g => new ExperienceStatementCountResponse(g.Key, g.Count()))
            .OrderByDescending(s => s.Count).ThenBy(s => s.Key, StringComparer.Ordinal)
            .Take(TopStatements)
            .ToArray();
}
