using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CvScanCorpus;

/// <summary>
/// The calibration sample for outside tools: a fixed, stratified pick of CVs from every local set,
/// copied under neutral names (REF-001.pdf…) so no outside tool sees where a file came from, with
/// a CSV of our own score beside empty columns for theirs.
///
/// It lives in cv-templates/_reference rather than in artifacts/ because the CSV is filled in by
/// hand and cannot be regenerated; the tool refuses to overwrite it. Scans skip folders whose name
/// starts with an underscore, so the copies are never counted twice.
/// </summary>
internal static class ReferenceSample
{
    private sealed record Candidate(string Source, string Path, int Score, string Band, string Findings);

    /// <summary>How many files each source contributes. Real-world sets carry the weight; the
    /// synthetic corpus contributes one file per defect it was built to have.</summary>
    private static readonly (string Source, int Count)[] Quotas =
    [
        ("arefinnomi/pdf", 10), ("arefinnomi/word", 10), ("kaggle", 12), ("canva", 10),
        ("synthetic", 12), ("bazil-suhail", 3), ("marketing", 3)
    ];

    public static async Task<int> BuildAsync(string root, CancellationToken cancellationToken)
    {
        var templates = Path.Combine(root, "cv-templates");
        var target = Path.Combine(templates, "_reference");
        var manifest = Path.Combine(target, "reference.csv");

        if (File.Exists(manifest))
        {
            Console.Error.WriteLine($"{manifest} already exists and may hold scores entered by hand; not overwriting.");
            return 1;
        }

        var candidates = new List<Candidate>();
        candidates.AddRange(await FromScanAsync(root, "kaggle", Path.Combine(templates, "kaggle"), "kaggle", cancellationToken));
        candidates.AddRange((await FromScanAsync(root, "arefinnomi", Path.Combine(templates, "arefinnomi"), "arefinnomi", cancellationToken))
            .Select(candidate => candidate with
            {
                Source = candidate.Path.Contains($"{Path.DirectorySeparatorChar}word{Path.DirectorySeparatorChar}")
                    ? "arefinnomi/word"
                    : "arefinnomi/pdf"
            }));
        candidates.AddRange((await FromScanAsync(root, "cv-templates-without-arefinnomi-without-kaggle", templates, null, cancellationToken))
            .Select(candidate => candidate with
            {
                Source = Path.GetRelativePath(templates, candidate.Path).Split(Path.DirectorySeparatorChar)[0]
            }));
        candidates.AddRange(await FromCorpusAsync(root, cancellationToken));

        var random = new Random(20260925);
        var picked = new List<Candidate>();

        foreach (var (source, count) in Quotas)
        {
            var pool = candidates.Where(candidate => candidate.Source == source).ToList();
            if (pool.Count == 0)
            {
                Console.Error.WriteLine($"no scan results for {source}; run the scans first.");
                return 1;
            }

            picked.AddRange(source == "synthetic" ? OnePerGroup(pool, count, random) : Stratified(pool, count, random));
        }

        Directory.CreateDirectory(target);
        var csv = new StringBuilder();
        csv.AppendLine("ref,source,original,our_score,our_band,our_findings," +
                       "tool_a_score,tool_b_score,tool_c_score," +
                       "parser_quality_codes,parser_name_ok,parser_email_ok,parser_phone_ok," +
                       "parser_positions_found,parser_position_dates_ok,parser_education_found,notes");

        for (var index = 0; index < picked.Count; index++)
        {
            var candidate = picked[index];
            var reference = $"REF-{index + 1:000}{Path.GetExtension(candidate.Path).ToLowerInvariant()}";
            File.Copy(candidate.Path, Path.Combine(target, reference), overwrite: false);

            csv.AppendLine(string.Join(',', reference, candidate.Source,
                Csv(Path.GetRelativePath(root, candidate.Path)), candidate.Score.ToString(CultureInfo.InvariantCulture),
                candidate.Band, Csv(candidate.Findings), "", "", "", "", "", "", "", "", "", "", ""));
        }

        await File.WriteAllTextAsync(manifest, csv.ToString(), cancellationToken);
        Console.WriteLine($"{picked.Count} files in {target}");
        foreach (var group in picked.GroupBy(candidate => (candidate.Source, candidate.Band)).OrderBy(group => group.Key))
        {
            Console.WriteLine($"  {group.Key.Source,-18} {group.Key.Band,-5} {group.Count()}");
        }

        return 0;
    }

    /// <summary>
    /// Spread across our own bands first (a sample of only "good" files would say nothing about
    /// whether a 60 means the same thing elsewhere), then across the findings that decided the
    /// score, so each rule we are unsure of has files an outside tool can vote on.
    /// </summary>
    private static IEnumerable<Candidate> Stratified(List<Candidate> pool, int count, Random random)
    {
        var strata = pool
            .GroupBy(candidate => (candidate.Band, Lead: candidate.Findings.Split(' ')[0].Split('-')[0]))
            .Select(group => new Queue<Candidate>(group.OrderBy(_ => random.Next())))
            .OrderBy(_ => random.Next())
            .ToList();

        var picked = new List<Candidate>();
        while (picked.Count < count && strata.Any(stratum => stratum.Count > 0))
        {
            foreach (var stratum in strata.Where(stratum => stratum.Count > 0))
            {
                picked.Add(stratum.Dequeue());
                if (picked.Count == count)
                {
                    break;
                }
            }
        }

        return picked;
    }

    private static IEnumerable<Candidate> OnePerGroup(List<Candidate> pool, int count, Random random) =>
        pool.GroupBy(candidate => Path.GetFileNameWithoutExtension(candidate.Path)[4..])
            .OrderBy(_ => random.Next())
            .Take(count)
            .Select(group => group.OrderBy(_ => random.Next()).First());

    private static async Task<List<Candidate>> FromScanAsync(string root, string scanName, string sourceRoot,
        string? source, CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "artifacts", "cv-scan", scanName, "scan-results.json");
        if (!File.Exists(path))
        {
            return [];
        }

        var rows = JsonSerializer.Deserialize<List<ScanRow>>(await File.ReadAllTextAsync(path, cancellationToken), Json.Options)!;
        return rows
            .Where(row => row.Score >= 0 && !row.Holdout)
            .Select(row => new Candidate(source ?? "", Path.Combine(sourceRoot, row.File), row.Score,
                Evaluator.Band(row.Score),
                string.Join(' ', row.Findings.OrderByDescending(finding => finding.Cost).Select(finding => $"{finding.Code}-{finding.Cost}"))))
            .Where(candidate => File.Exists(candidate.Path))
            .ToList();
    }

    private static async Task<List<Candidate>> FromCorpusAsync(string root, CancellationToken cancellationToken)
    {
        var corpus = Path.Combine(root, "artifacts", "cv-corpus");
        var path = Path.Combine(corpus, "results.json");
        if (!File.Exists(path))
        {
            return [];
        }

        var rows = JsonSerializer.Deserialize<List<CaseResult>>(await File.ReadAllTextAsync(path, cancellationToken), Json.Options)!;
        return rows
            .Where(row => row.Score >= 0 && !row.Holdout)
            .Select(row => new Candidate("synthetic", Path.Combine(corpus, row.File), row.Score, row.Band,
                row.Evidence is null ? "" : string.Join(' ', row.Found)))
            .ToList();
    }

    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
