using System.Globalization;
using System.Text;

namespace CvScanCorpus;

/// <summary>
/// Reads the hand-filled reference CSV and says how far our score sits from each outside tool:
/// the average gap (are we systematically kinder or harsher?), rank agreement (do we order the
/// same CVs the same way?), band agreement, and the files we disagree on most — the next round's
/// work list. The goal it measures is the user's: stay near the industry average, not be the
/// most lenient or the strictest scanner.
/// </summary>
internal static class ReferenceCompare
{
    public static async Task<int> RunAsync(string root, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "cv-templates", "_reference", "reference.csv");
        if (!File.Exists(path))
        {
            Console.Error.WriteLine("no reference.csv; run `reference` first.");
            return 1;
        }

        var lines = (await File.ReadAllLinesAsync(path, cancellationToken)).Where(line => line.Length > 0).ToList();
        var header = Split(lines[0]);
        var rows = lines.Skip(1).Select(Split).Select(cells => header
            .Select((name, index) => (name, value: index < cells.Count ? cells[index] : ""))
            .ToDictionary(pair => pair.name, pair => pair.value)).ToList();

        // Our side is re-scored now, with the rules as they are today; the CSV's our_score is only
        // what the file scored when it was sampled. A comparison against stale numbers would
        // measure the old scanner.
        var extractor = new AfterApply.Infrastructure.CvScan.CvTextExtractor(
            Microsoft.Extensions.Options.Options.Create(new AfterApply.Infrastructure.CvScan.CvScanOptions()));
        foreach (var row in rows)
        {
            var file = Path.Combine(root, "cv-templates", "_reference", row["ref"]);
            await using var stream = File.OpenRead(file);
            var cv = await extractor.ExtractAsync(stream, file.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                ? AfterApply.Domain.Documents.CvFileFormat.Docx
                : AfterApply.Domain.Documents.CvFileFormat.Pdf, cancellationToken);
            var score = AfterApply.Application.CvScan.CvScanScoring.Score(AfterApply.Application.CvScan.CvScanChecks.Run(cv));
            row["our_score"] = score.Score.ToString(CultureInfo.InvariantCulture);
            row["our_band"] = Evaluator.Band(score.Score);
            row["our_findings"] = string.Join(' ', score.Findings.Select(finding => $"{finding.Code}-{finding.PointCost}"));
        }

        var report = new StringBuilder("# Reference comparison (our scores re-computed with today's rules)\n\n");
        foreach (var tool in header.Where(name => name.StartsWith("tool_", StringComparison.Ordinal)))
        {
            var pairs = rows
                .Where(row => double.TryParse(row[tool], NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                .Select(row => (Ref: row["ref"], Source: row["source"],
                    Ours: double.Parse(row["our_score"], CultureInfo.InvariantCulture),
                    Theirs: double.Parse(row[tool], CultureInfo.InvariantCulture)))
                .ToList();

            // "FAIL": the tool could not read the file at all. Not a score, so not in the averages,
            // but reported beside them with our own score for the same files — a parser failing is
            // the strongest verdict an outside tool can give.
            var failures = rows.Where(row => row[tool].Equals("FAIL", StringComparison.OrdinalIgnoreCase)).ToList();

            if (pairs.Count < 3 && failures.Count == 0)
            {
                continue;
            }

            if (failures.Count > 0)
            {
                report.AppendLine($"## {tool}: could not read {failures.Count} file(s)\n");
                foreach (var row in failures)
                {
                    report.AppendLine($"- {row["ref"]} ({row["source"]}): ours {row["our_score"]} ({row["our_band"]}) — {row["our_findings"]}");
                }

                report.AppendLine();
            }

            if (pairs.Count < 3)
            {
                continue;
            }

            var gap = pairs.Average(pair => pair.Ours - pair.Theirs);
            var bandAgreement = pairs.Count(pair => Evaluator.Band((int)pair.Ours) == Evaluator.Band((int)Math.Round(pair.Theirs)));

            report.AppendLine($"## {tool} ({pairs.Count} files)\n");
            report.AppendLine($"- Mean gap (ours − theirs): **{gap:+0.0;-0.0}** points");
            report.AppendLine($"- Rank agreement (Spearman): **{Spearman(pairs.Select(p => p.Ours).ToList(), pairs.Select(p => p.Theirs).ToList()):0.00}**");
            report.AppendLine($"- Same band: {bandAgreement}/{pairs.Count}");
            report.AppendLine("\n| Source | n | Mean gap |\n|---|---:|---:|");
            foreach (var group in pairs.GroupBy(pair => pair.Source).OrderBy(group => group.Key))
            {
                report.AppendLine($"| {group.Key} | {group.Count()} | {group.Average(pair => pair.Ours - pair.Theirs):+0.0;-0.0} |");
            }

            report.AppendLine("\nLargest disagreements (after removing the mean gap):\n");
            foreach (var pair in pairs.OrderByDescending(pair => Math.Abs(pair.Ours - pair.Theirs - gap)).Take(10))
            {
                var row = rows.First(r => r["ref"] == pair.Ref);
                report.AppendLine($"- {pair.Ref} ({pair.Source}): ours {pair.Ours:0}, theirs {pair.Theirs:0} — {row["our_findings"]}");
            }

            report.AppendLine();
        }

        var parsed = rows.Where(row => row["parser_quality_codes"].Length > 0 || row["parser_name_ok"].Length > 0).ToList();
        if (parsed.Count > 0)
        {
            report.AppendLine($"## Parser ({parsed.Count} files)\n");
            foreach (var field in new[] { "parser_name_ok", "parser_email_ok", "parser_phone_ok", "parser_position_dates_ok" })
            {
                var answered = parsed.Where(row => row[field].Length > 0).ToList();
                report.AppendLine($"- {field}: {answered.Count(row => row[field] is "1" or "yes" or "y")}/{answered.Count}");
            }
        }

        var output = Path.Combine(root, "cv-templates", "_reference", "comparison.md");
        await File.WriteAllTextAsync(output, report.ToString(), cancellationToken);
        Console.WriteLine(report.ToString());
        return 0;
    }

    private static double Spearman(List<double> first, List<double> second)
    {
        var a = Ranks(first);
        var b = Ranks(second);
        var meanA = a.Average();
        var meanB = b.Average();
        var covariance = a.Zip(b, (x, y) => (x - meanA) * (y - meanB)).Sum();
        var deviation = Math.Sqrt(a.Sum(x => (x - meanA) * (x - meanA)) * b.Sum(y => (y - meanB) * (y - meanB)));
        return deviation == 0 ? 0 : covariance / deviation;
    }

    /// <summary>Average ranks, so ties (many 100s) do not invent an order.</summary>
    private static List<double> Ranks(List<double> values)
    {
        var ordered = values.Select((value, index) => (value, index)).OrderBy(pair => pair.value).ToList();
        var ranks = new double[values.Count];
        for (var start = 0; start < ordered.Count;)
        {
            var end = start;
            while (end + 1 < ordered.Count && ordered[end + 1].value.Equals(ordered[start].value))
            {
                end++;
            }

            for (var index = start; index <= end; index++)
            {
                ranks[ordered[index].index] = (start + end) / 2.0 + 1;
            }

            start = end + 1;
        }

        return [.. ranks];
    }

    private static List<string> Split(string line)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (quoted)
            {
                if (character == '"' && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else if (character == '"')
                {
                    quoted = false;
                }
                else
                {
                    current.Append(character);
                }
            }
            else if (character == '"')
            {
                quoted = true;
            }
            else if (character == ',')
            {
                cells.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        cells.Add(current.ToString());
        return cells;
    }
}
