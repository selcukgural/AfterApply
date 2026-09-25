using CvScanCorpus;

// The CV scan's calibration loop (DECISIONS.md 2026-09-25):
//
//   dotnet run --project tools/CvScanCorpus -- generate            # 100 labelled synthetic CVs
//   dotnet run --project tools/CvScanCorpus -- eval                # score them, write the report
//   dotnet run --project tools/CvScanCorpus -- eval --baseline     # ...and diff against the last run
//   dotnet run --project tools/CvScanCorpus -- reference            # stratified sample for outside tools
//   dotnet run --project tools/CvScanCorpus -- compare              # our scores vs the ones entered by hand
//   dotnet run --project tools/CvScanCorpus -- headings <folder>      # heading-like lines the vocabulary misses
//   dotnet run --project tools/CvScanCorpus -- text <file>          # what the checks read, verbatim
//   dotnet run --project tools/CvScanCorpus -- scan <folder> [--baseline] [--exclude <sub>]... [--no-quotes]
//                                                     # an unlabelled outside set, one folder per sector
//
// Everything lands in artifacts/cv-corpus/ (git-ignored). Generation needs a local Chrome
// (CHROME_PATH to override); evaluation needs nothing but the files.

var arguments = args.ToList();
var root = FindRepositoryRoot();
var corpus = Path.Combine(root, "artifacts", "cv-corpus");
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

switch (arguments.FirstOrDefault())
{
    case "generate":
        await Generator.RunAsync(corpus, cancellation.Token);
        return 0;

    case "eval":
        var results = Path.Combine(corpus, "results.json");
        var previous = Path.Combine(corpus, "results.previous.json");
        if (arguments.Contains("--baseline") && File.Exists(results))
        {
            File.Copy(results, previous, overwrite: true);
        }

        return await Evaluator.RunAsync(corpus, Path.Combine(corpus, "report.md"), results,
            arguments.Contains("--baseline") ? previous : null, cancellation.Token);

    case "text" when arguments.Count > 1:
        var path = Path.GetFullPath(arguments[1]);
        var extractor = new AfterApply.Infrastructure.CvScan.CvTextExtractor(
            Microsoft.Extensions.Options.Options.Create(new AfterApply.Infrastructure.CvScan.CvScanOptions()));
        await using (var stream = File.OpenRead(path))
        {
            var cv = await extractor.ExtractAsync(stream, path.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                ? AfterApply.Domain.Documents.CvFileFormat.Docx
                : AfterApply.Domain.Documents.CvFileFormat.Pdf, cancellation.Token);
            Console.WriteLine(cv.Text);
            Console.WriteLine($"# fonts: {string.Join(", ", cv.Fonts.Where(font => font.GlyphCount >= 30).Select(font => $"{font.Name}@{font.Size}×{font.GlyphCount}"))}");
            Console.WriteLine($"# pages: {string.Join(", ", cv.Pages.Select(page => $"p{page.Number} backtracks={page.Backtracks} images={page.ImageCount}"))}");
            foreach (var finding in AfterApply.Application.CvScan.CvScanScoring
                         .Score(AfterApply.Application.CvScan.CvScanChecks.Run(cv)).Findings)
            {
                Console.WriteLine($"# {finding.Code} -{finding.PointCost} " +
                                  string.Join(", ", finding.Metrics.Select(m => $"{m.Key}={m.Value}")));
            }
        }

        return 0;

    case "compare":
        return await ReferenceCompare.RunAsync(root, cancellation.Token);

    case "reference":
        return await ReferenceSample.BuildAsync(root, cancellation.Token);

    case "headings" when arguments.Count > 1:
    {
        // Vocabulary gaps: every short line that looks like a heading (upper case, or ending in a
        // colon), counted by how many files carry it, split into known and unknown.
        var headingExtractor = new AfterApply.Infrastructure.CvScan.CvTextExtractor(
            Microsoft.Extensions.Options.Options.Create(new AfterApply.Infrastructure.CvScan.CvScanOptions()));
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(arguments[1], "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var file in files)
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var cv = await headingExtractor.ExtractAsync(stream, file.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
                    ? AfterApply.Domain.Documents.CvFileFormat.Docx : AfterApply.Domain.Documents.CvFileFormat.Pdf, cancellation.Token);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var raw in cv.Text.Split('\n'))
                {
                    var line = raw.Trim();
                    var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length is 0 or > 4 || line.Any(char.IsDigit) || line.Length > 40) continue;
                    var letters = line.Where(char.IsLetter).ToList();
                    var looksLikeHeading = line.EndsWith(':') || (letters.Count > 2 && letters.All(char.IsUpper));
                    if (!looksLikeHeading) continue;
                    var folded = AfterApply.Application.CvScan.CvScanVocabulary.Fold(line);
                    if (folded.Length > 0 && seen.Add(folded)) counts[folded] = counts.GetValueOrDefault(folded) + 1;
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
            }
        }

        foreach (var (heading, count) in counts.Where(pair => pair.Value >= files.Count / 100 + 2)
                     .OrderByDescending(pair => pair.Value).Take(80))
        {
            var known = AfterApply.Application.CvScan.CvScanVocabulary.AllHeadings.Contains(heading);
            Console.WriteLine($"{count,5} {(known ? "   " : "NEW")} {heading}");
        }

        return 0;
    }

    case "scan" when arguments.Count > 1:
        var source = Path.GetFullPath(arguments[1]);
        // --exclude <folder> (repeatable) skips a subfolder anywhere under the source;
        // --no-quotes writes rates and file names only — for sets of real people's CVs.
        var excluded = arguments.Select((value, index) => (value, index))
            .Where(pair => pair.value == "--exclude" && pair.index + 1 < arguments.Count)
            .Select(pair => arguments[pair.index + 1]).ToList();
        var outName = new DirectoryInfo(source).Name +
                      string.Concat(excluded.Order().Select(name => "-without-" + name));
        return await FolderScan.RunAsync(source, Path.Combine(root, "artifacts", "cv-scan", outName),
            arguments.Contains("--baseline"), excluded, quotes: !arguments.Contains("--no-quotes"),
            cancellation.Token);

    default:
        Console.Error.WriteLine("usage: CvScanCorpus generate | eval [--baseline] | scan <folder> [--baseline]");
        return 2;
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AfterApply.slnx")))
    {
        directory = directory.Parent;
    }

    return directory?.FullName ?? throw new InvalidOperationException("Run from inside the repository.");
}
