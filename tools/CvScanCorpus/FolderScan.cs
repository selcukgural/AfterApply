using System.Globalization;
using System.Text;
using System.Text.Json;
using AfterApply.Application.CvScan;
using AfterApply.Domain.Documents;
using AfterApply.Infrastructure.CvScan;
using Microsoft.Extensions.Options;

namespace CvScanCorpus;

internal sealed record ScanRow(string File, string Category, bool Holdout, int Score, IReadOnlyList<ScanFinding> Findings);

internal sealed record ScanFinding(string Code, int Cost, string? Quote, IReadOnlyDictionary<string, double>? Metrics = null);

/// <summary>
/// An unlabelled collection — real CVs from outside, one folder per sector. Nothing here says what
/// each file should score, so the report is about rates: how often each finding fires, per sector,
/// with sample evidence. A finding that fires on most of a set of plainly laid-out CVs is a false
/// alarm until shown otherwise.
///
/// Same holdout rule as the labelled corpus: a stable fifth of the files (by name) is reported as
/// aggregates only, never as examples to tune against.
/// </summary>
internal static partial class FolderScan
{
    public static async Task<int> RunAsync(string sourceDirectory, string outDirectory, bool baseline,
        IReadOnlyCollection<string> excluded, bool quotes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outDirectory);
        var resultsPath = Path.Combine(outDirectory, "scan-results.json");
        var previousPath = Path.Combine(outDirectory, "scan-results.previous.json");
        if (baseline && File.Exists(resultsPath))
        {
            File.Copy(resultsPath, previousPath, overwrite: true);
        }

        var extractor = new CvTextExtractor(Options.Create(new CvScanOptions()));
        var rows = new List<ScanRow>();
        var files = Directory.EnumerateFiles(sourceDirectory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            .Where(path => !excluded.Any(name =>
                Path.GetRelativePath(sourceDirectory, path).Split(Path.DirectorySeparatorChar).Contains(name)))
            // "_reference" and the like: working copies kept beside the sets, never a set themselves.
            .Where(path => !Path.GetRelativePath(sourceDirectory, path).Split(Path.DirectorySeparatorChar)
                .Any(part => part.StartsWith('_')))
            .Order(StringComparer.Ordinal)
            .ToList();

        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var category = Path.GetFileName(Path.GetDirectoryName(path)) ?? "?";
            var holdout = IsHoldout(Path.GetFileName(path));
            var format = path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? CvFileFormat.Pdf : CvFileFormat.Docx;

            try
            {
                await using var stream = File.OpenRead(path);
                var cv = await extractor.ExtractAsync(stream, format, cancellationToken);
                var score = CvScanScoring.Score(CvScanChecks.Run(cv));
                rows.Add(new ScanRow(Path.GetRelativePath(sourceDirectory, path), category, holdout, score.Score,
                    score.Findings.Select(finding => new ScanFinding(finding.Code.ToString(), finding.PointCost,
                        quotes ? Mask(finding.Evidence.FirstOrDefault()?.Quote) : null, finding.Metrics)).ToList()));
            }
            catch (CvExtractionException exception)
            {
                rows.Add(new ScanRow(Path.GetRelativePath(sourceDirectory, path), category, holdout, -1,
                    [new ScanFinding($"Refused:{exception.Failure}", 0, null)]));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Anything else escaped the production extractor: in the product that is a 500 for
                // whoever uploaded the file. Recorded, not thrown, so one file cannot hide the rest.
                rows.Add(new ScanRow(Path.GetRelativePath(sourceDirectory, path), category, holdout, -1,
                    [new ScanFinding($"Crashed:{exception.GetType().Name}", 0, Mask(exception.Message))]));
            }
        }

        var previous = baseline && File.Exists(previousPath)
            ? JsonSerializer.Deserialize<List<ScanRow>>(await File.ReadAllTextAsync(previousPath, cancellationToken), Json.Options)
            : null;

        await File.WriteAllTextAsync(resultsPath, JsonSerializer.Serialize(rows, Json.Options), cancellationToken);
        var reportPath = Path.Combine(outDirectory, "scan-report.md");
        await File.WriteAllTextAsync(reportPath, Report(rows, previous), cancellationToken);

        var dev = rows.Where(row => !row.Holdout).ToList();
        Console.WriteLine($"{rows.Count} files · dev mean {dev.Average(row => row.Score):0.0} · " +
                          $"holdout mean {rows.Where(row => row.Holdout).Average(row => row.Score):0.0} · report {reportPath}");
        return 0;
    }

    /// <summary>
    /// E-mail addresses and phone numbers out of every quote, always. Outside sets can hold real
    /// people's CVs, and a report is something that gets read, pasted and shared; the finding's
    /// point survives without them.
    /// </summary>
    private static string? Mask(string? quote) => quote is null
        ? null
        : Phone().Replace(Email().Replace(quote, "<email>"), "<phone>");

    [System.Text.RegularExpressions.GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]+")]
    private static partial System.Text.RegularExpressions.Regex Email();

    [System.Text.RegularExpressions.GeneratedRegex(@"\+?\d[\d\s().-]{7,}\d")]
    private static partial System.Text.RegularExpressions.Regex Phone();

    /// <summary>A stable fifth, decided by the file name alone so it never moves between runs.</summary>
    private static bool IsHoldout(string name)
    {
        var hash = 0u;
        foreach (var character in name)
        {
            hash = hash * 31 + character;
        }

        return hash % 5 == 0;
    }

    private static string Report(List<ScanRow> rows, List<ScanRow>? previous)
    {
        var md = new StringBuilder();
        var dev = rows.Where(row => !row.Holdout).ToList();
        var holdout = rows.Where(row => row.Holdout).ToList();

        md.AppendLine("# External CV set scan").AppendLine();
        md.AppendLine($"{rows.Count} files, {dev.Count} development / {holdout.Count} holdout.").AppendLine();

        md.AppendLine("## Score distribution").AppendLine();
        md.AppendLine("| Band | Dev | Holdout |").AppendLine("|---|---:|---:|");
        foreach (var (name, low, high) in new[] { ("good (80+)", 80, 101), ("fair (55-79)", 55, 80), ("poor (<55)", 0, 55), ("refused or crashed", -1, 0) })
        {
            md.AppendLine($"| {name} | {Share(dev, row => row.Score >= low && row.Score < high)} | " +
                          $"{Share(holdout, row => row.Score >= low && row.Score < high)} |");
        }

        md.AppendLine().AppendLine("## How often each finding fires").AppendLine();
        md.AppendLine("| Finding | Dev | Holdout | Mean cost when it fires |").AppendLine("|---|---:|---:|---:|");
        foreach (var code in rows.SelectMany(row => row.Findings).Select(finding => finding.Code).Distinct().Order())
        {
            var costs = rows.SelectMany(row => row.Findings).Where(finding => finding.Code == code).Select(finding => finding.Cost).ToList();
            md.AppendLine($"| {code} | {Share(dev, row => row.Findings.Any(f => f.Code == code))} | " +
                          $"{Share(holdout, row => row.Findings.Any(f => f.Code == code))} | {costs.Average():0.0} |");
        }

        md.AppendLine().AppendLine("## Per category (dev)").AppendLine();
        var codes = rows.SelectMany(row => row.Findings).Select(finding => finding.Code).Distinct().Order().ToList();
        md.AppendLine("| Category | n | Mean score | " + string.Join(" | ", codes.Select(Short)) + " |");
        md.AppendLine("|---|---:|---:|" + string.Concat(codes.Select(_ => "---:|")));
        foreach (var group in dev.GroupBy(row => row.Category).OrderBy(group => group.Key))
        {
            md.AppendLine($"| {group.Key} | {group.Count()} | {group.Average(row => row.Score):0} | " +
                          string.Join(" | ", codes.Select(code =>
                              Share(group.ToList(), row => row.Findings.Any(f => f.Code == code)))) + " |");
        }

        md.AppendLine().AppendLine("## Sample evidence (dev, up to 8 per finding)").AppendLine();
        foreach (var code in codes)
        {
            md.AppendLine($"### {code}");
            foreach (var row in dev.Where(row => row.Findings.Any(f => f.Code == code)).Take(8))
            {
                var finding = row.Findings.First(f => f.Code == code);
                var metrics = finding.Metrics is null ? string.Empty
                    : " " + string.Join(", ", finding.Metrics.Select(m => $"{m.Key}={m.Value.ToString(CultureInfo.InvariantCulture)}"));
                md.AppendLine($"- `{row.File}` ({row.Score}): “{finding.Quote}”{metrics}");
            }

            // Which of a finding's reasons carry it: the share of firings in which each metric is non-zero.
            var firings = dev.SelectMany(row => row.Findings).Where(f => f.Code == code && f.Metrics is not null).ToList();
            if (firings.Count > 0)
            {
                md.AppendLine().AppendLine("Metric non-zero in: " + string.Join(", ",
                    firings.SelectMany(f => f.Metrics!.Keys).Distinct().Select(key =>
                        $"{key} {(double)firings.Count(f => f.Metrics!.TryGetValue(key, out var v) && v != 0) / firings.Count:0%}")));
            }

            md.AppendLine();
        }

        md.AppendLine("## Lowest-scoring dev files").AppendLine();
        foreach (var row in dev.OrderBy(row => row.Score).Take(15))
        {
            md.AppendLine($"- `{row.File}` {row.Score}: {string.Join(", ", row.Findings.Select(f => $"{f.Code} -{f.Cost}"))}");
        }

        if (previous is not null)
        {
            var before = previous.ToDictionary(row => row.File);
            var changed = rows.Where(row => before.TryGetValue(row.File, out var b) && b.Score != row.Score).ToList();
            md.AppendLine().AppendLine("## Changes since baseline").AppendLine();
            md.AppendLine($"{changed.Count} files changed score. Dev mean {previous.Where(r => !r.Holdout).Average(r => r.Score):0.0} → " +
                          $"{dev.Average(r => r.Score):0.0}; holdout mean {previous.Where(r => r.Holdout).Average(r => r.Score):0.0} → " +
                          $"{holdout.Average(r => r.Score):0.0}.");
        }

        return md.ToString();
    }

    private static string Short(string code) => new(code.Where(char.IsUpper).ToArray());

    private static string Share(List<ScanRow> rows, Func<ScanRow, bool> predicate) =>
        rows.Count == 0 ? "–" : ((double)rows.Count(predicate) / rows.Count).ToString("0%", CultureInfo.InvariantCulture);
}
