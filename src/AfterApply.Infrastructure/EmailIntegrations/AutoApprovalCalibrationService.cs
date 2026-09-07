using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.EmailIntegrations;

internal sealed class AutoApprovalCalibrationService(
    AppDbContext dbContext,
    IOptions<EmailAutoApprovalOptions> options) : IAutoApprovalCalibrationService
{
    /// <summary>
    /// Band edges, coarse at the bottom and fine at the top. The decision this table exists to
    /// support is "0.9 or 0.95?", so the two ends of that argument each get their own row; nobody
    /// is going to auto-apply at 0.55 and the low bands only need to be visible, not precise.
    /// </summary>
    private static readonly double[] Edges = [0.0, 0.5, 0.6, 0.7, 0.8, 0.9, 0.95, 1.0];

    public async Task<AutoApprovalCalibrationResponse> ComputeAsync(CancellationToken cancellationToken)
    {
        // The qualifying rule from TryAutoApplyAsync minus the threshold — deliberately, because
        // the whole question is what a *different* threshold would have done. Kept in sync by hand;
        // there is one assertion for that in the tests.
        var rows = await dbContext.EmailSuggestions
            .Where(s => s.MatchType == EmailApplicationMatchType.DomainMatch && s.MatchedRule.StartsWith("Llm:"))
            .Select(s => new { s.ConfidenceScore, s.Status })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var buckets = new List<AutoApprovalCalibrationBucket>(Edges.Length - 1);

        for (var i = 0; i < Edges.Length - 1; i++)
        {
            var lower = Edges[i];
            var upper = Edges[i + 1];
            var isTopBand = i == Edges.Length - 2;

            // Half-open bands, except the top one, so a perfect 1.0 lands somewhere.
            var inBucket = rows
                .Where(r => r.ConfidenceScore >= lower && (isTopBand ? r.ConfidenceScore <= upper : r.ConfidenceScore < upper))
                .ToList();

            var confirmed = inBucket.Count(r => r.Status == EmailSuggestionStatus.Confirmed);
            var dismissed = inBucket.Count(r => r.Status == EmailSuggestionStatus.Dismissed);
            var autoApplied = inBucket.Count(r => r.Status == EmailSuggestionStatus.AutoApplied);
            var reverted = inBucket.Count(r => r.Status == EmailSuggestionStatus.Reverted);
            var pending = inBucket.Count(r => r.Status == EmailSuggestionStatus.Pending);

            var judged = confirmed + dismissed;
            // A revert is an auto-apply that was taken back, so the denominator is both together —
            // a reverted row no longer counts as AutoApplied once it moves to Reverted.
            var acted = autoApplied + reverted;

            buckets.Add(new AutoApprovalCalibrationBucket(
                lower, upper, inBucket.Count, confirmed, dismissed, autoApplied, reverted, pending,
                AgreementRate: judged == 0 ? null : Rate(confirmed, judged),
                RevertRate: acted == 0 ? null : Rate(reverted, acted)));
        }

        return new AutoApprovalCalibrationResponse(
            options.Value.ConfidenceThreshold,
            options.Value.Enabled,
            options.Value.ShadowModeEnabled,
            rows.Count,
            buckets);
    }

    private static double Rate(int count, int total) => Math.Round(100.0 * count / total, 1);
}
