using System.Globalization;
using System.Text;
using System.Text.Json;
using AfterApply.Application.CvScan;
using AfterApply.Domain.Documents;
using AfterApply.Infrastructure.CvScan;
using Microsoft.Extensions.Options;

namespace CvScanCorpus;

internal sealed record CaseResult(
    int Id,
    string File,
    string Group,
    bool Holdout,
    int Score,
    string Band,
    IReadOnlyList<string> Found,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Unexpected,
    bool BandOk,
    string? Evidence)
{
    public bool Pass => Missing.Count == 0 && Unexpected.Count == 0 && BandOk;
}

/// <summary>
/// Scores every case with the production extractor and checks, and compares against the labels.
///
/// The holdout split is the guard against tuning the checks into the corpus: the report details
/// every failing development case, but for holdout cases it prints only counts per group. Fixes are
/// made against what the development half shows; the holdout number says whether they generalise.
/// </summary>
internal static class Evaluator
{
    public static async Task<int> RunAsync(string corpusDirectory, string reportPath, string resultsPath,
        string? baselinePath, CancellationToken cancellationToken)
    {
        var cases = JsonSerializer.Deserialize<List<CorpusCase>>(
            await File.ReadAllTextAsync(Path.Combine(corpusDirectory, "manifest.json"), cancellationToken),
            Json.Options)!;

        var extractor = new CvTextExtractor(Options.Create(new CvScanOptions()));
        var results = new List<CaseResult>();

        foreach (var item in cases)
        {
            await using var file = File.OpenRead(Path.Combine(corpusDirectory, item.FileName));
            var format = item.Format == "pdf" ? CvFileFormat.Pdf : CvFileFormat.Docx;

            CvScanScore score;
            try
            {
                var cv = await extractor.ExtractAsync(file, format, cancellationToken);
                score = CvScanScoring.Score(CvScanChecks.Run(cv));
            }
            catch (CvExtractionException exception)
            {
                results.Add(new CaseResult(item.Id, item.FileName, item.Group, item.Holdout, -1, "refused",
                    [], item.Label.Must, [$"refused:{exception.Failure}"], false, null));
                continue;
            }

            var found = score.Findings.Select(finding => finding.Code.ToString()).Distinct().ToList();
            var band = Band(score.Score);

            results.Add(new CaseResult(item.Id, item.FileName, item.Group, item.Holdout, score.Score, band, found,
                item.Label.Must.Except(found).ToList(),
                found.Except(item.Label.Must).Except(item.Label.May).ToList(),
                item.Label.Bands.Contains(band),
                string.Join(" | ", score.Findings.Select(finding =>
                    $"{finding.Code} -{finding.PointCost} “{finding.Evidence.FirstOrDefault()?.Quote}”"))));
        }

        var baseline = baselinePath is not null && File.Exists(baselinePath)
            ? JsonSerializer.Deserialize<List<CaseResult>>(await File.ReadAllTextAsync(baselinePath, cancellationToken), Json.Options)
            : null;

        await File.WriteAllTextAsync(resultsPath, JsonSerializer.Serialize(results, Json.Options), cancellationToken);
        await File.WriteAllTextAsync(reportPath, Report(cases, results, baseline), cancellationToken);

        var dev = results.Where(result => !result.Holdout).ToList();
        var holdout = results.Where(result => result.Holdout).ToList();
        Console.WriteLine($"dev {dev.Count(result => result.Pass)}/{dev.Count} pass · " +
                          $"holdout {holdout.Count(result => result.Pass)}/{holdout.Count} pass · report {reportPath}");
        return 0;
    }

    public static string Band(int score) => score >= 80 ? "good" : score >= 55 ? "fair" : "poor";

    private static string Report(List<CorpusCase> cases, List<CaseResult> results, List<CaseResult>? baseline)
    {
        var md = new StringBuilder();
        var dev = results.Where(result => !result.Holdout).ToList();
        var holdout = results.Where(result => result.Holdout).ToList();

        md.AppendLine("# CV scan corpus report").AppendLine();
        md.AppendLine($"- Development: **{dev.Count(r => r.Pass)}/{dev.Count}** cases pass");
        md.AppendLine($"- Holdout: **{holdout.Count(r => r.Pass)}/{holdout.Count}** cases pass");
        md.AppendLine($"- Band agreement: dev {dev.Count(r => r.BandOk)}/{dev.Count}, holdout {holdout.Count(r => r.BandOk)}/{holdout.Count}");
        md.AppendLine();

        md.AppendLine("## Per finding (all cases)").AppendLine();
        md.AppendLine("| Finding | Expected | Found when expected | Missed | False alarm | Precision | Recall |");
        md.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (var code in Enum.GetNames<CvScanFindingCode>())
        {
            var expected = cases.Count(item => item.Label.Must.Contains(code));
            var tp = results.Count(r => cases.Single(c => c.Id == r.Id).Label.Must.Contains(code) && r.Found.Contains(code));
            var fn = results.Count(r => r.Missing.Contains(code));
            var fp = results.Count(r => r.Unexpected.Contains(code));
            md.AppendLine($"| {code} | {expected} | {tp} | {fn} | {fp} | {Ratio(tp, tp + fp)} | {Ratio(tp, tp + fn)} |");
        }

        md.AppendLine().AppendLine("## Per group").AppendLine();
        md.AppendLine("| Group | n | Pass | Score min / mean / max | Bands expected |");
        md.AppendLine("|---|---:|---:|---|---|");
        foreach (var group in results.GroupBy(r => r.Group))
        {
            var scores = group.Select(r => r.Score).ToList();
            var bands = string.Join("/", cases.First(c => c.Group == group.Key).Label.Bands);
            md.AppendLine($"| {group.Key} | {group.Count()} | {group.Count(r => r.Pass)} | " +
                          $"{scores.Min()} / {scores.Average():0} / {scores.Max()} | {bands} |");
        }

        md.AppendLine().AppendLine("## Failing development cases").AppendLine();
        foreach (var result in dev.Where(r => !r.Pass))
        {
            var label = cases.Single(c => c.Id == result.Id).Label;
            md.AppendLine($"### {result.File} — {result.Score} ({result.Band})");
            md.AppendLine($"- Why: {label.Why}");
            if (result.Missing.Count > 0) md.AppendLine($"- Missed: {string.Join(", ", result.Missing)}");
            if (result.Unexpected.Count > 0) md.AppendLine($"- False alarm: {string.Join(", ", result.Unexpected)}");
            if (!result.BandOk) md.AppendLine($"- Band {result.Band}, expected {string.Join("/", label.Bands)}");
            md.AppendLine($"- Findings: {result.Evidence}");
            md.AppendLine();
        }

        md.AppendLine("## Failing holdout cases (counts only)").AppendLine();
        foreach (var group in holdout.Where(r => !r.Pass).GroupBy(r => r.Group))
        {
            md.AppendLine($"- {group.Key}: {group.Count()}");
        }

        if (baseline is not null)
        {
            md.AppendLine().AppendLine("## Changes since baseline").AppendLine();
            var before = baseline.ToDictionary(r => r.Id);
            var changed = results.Where(r => before.TryGetValue(r.Id, out var b) && (b.Score != r.Score || b.Pass != r.Pass)).ToList();
            md.AppendLine($"{changed.Count} cases changed; pass {baseline.Count(r => r.Pass)} → {results.Count(r => r.Pass)}.").AppendLine();
            foreach (var result in changed.Where(r => !r.Holdout))
            {
                var b = before[result.Id];
                md.AppendLine($"- {result.File}: {b.Score} → {result.Score}" +
                              (b.Pass == result.Pass ? string.Empty : result.Pass ? " (now passes)" : " (now FAILS)"));
            }
        }

        return md.ToString();
    }

    private static string Ratio(int numerator, int denominator) =>
        denominator == 0 ? "–" : ((double)numerator / denominator).ToString("0%", CultureInfo.InvariantCulture);
}
