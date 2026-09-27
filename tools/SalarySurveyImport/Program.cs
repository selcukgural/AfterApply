using System.Text;
using AfterApply.Application.SalaryMarket;
using AfterApply.Application.SalaryMarket.Import;
using AfterApply.Infrastructure.SalaryMarket;
using SalarySurveyImport;

// The public salary pages' seed, rebuilt from the surveys' published files (DECISIONS.md 2026-09-27):
//
//   dotnet run --project tools/SalarySurveyImport -- fetch    # download every edition into artifacts/salary-surveys/
//   dotnet run --project tools/SalarySurveyImport -- build    # normalise, pool, aggregate, write the seed + a report
//
// The downloads are the surveys' raw answers and stay in artifacts/ (git-ignored). `build` writes
// only aggregates, and only cells with at least SalaryMarket:MinimumResponses answers, into
// src/AfterApply.Infrastructure/SalaryMarket/Seed/ — review that diff, then commit it.

const int MinimumResponses = 15; // SalaryMarketOptions.MinimumResponses; the service applies it again on load.

var arguments = args.ToList();
var root = FindRepositoryRoot();
var downloads = Path.Combine(root, "artifacts", "salary-surveys");
var seed = Path.Combine(root, "src", "AfterApply.Infrastructure", "SalaryMarket", "Seed");
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

switch (arguments.FirstOrDefault())
{
    case "fetch":
        await FetchAsync(cancellation.Token);
        return 0;
    case "build":
        await BuildAsync(cancellation.Token);
        return 0;
    default:
        Console.Error.WriteLine("usage: fetch | build");
        return 1;
}

async Task FetchAsync(CancellationToken cancellationToken)
{
    Directory.CreateDirectory(downloads);
    using var http = new HttpClient();
    http.DefaultRequestHeaders.UserAgent.ParseAdd("e-kariyerim-salary-survey-import/1.0");
    foreach (var edition in SurveyEditions.All)
    {
        var target = Path.Combine(downloads, edition.FileName);
        await using (var response = await http.GetStreamAsync(edition.DownloadUrl, cancellationToken))
        await using (var file = File.Create(target))
        {
            await response.CopyToAsync(file, cancellationToken);
        }

        Console.WriteLine($"{edition.Year} {edition.SourceCode}: {new FileInfo(target).Length:N0} bytes");
    }
}

async Task BuildAsync(CancellationToken cancellationToken)
{
    var normalized = new List<NormalizedResponse>();
    var editions = new List<SalarySurveyEdition>();
    var report = new StringBuilder();
    var unmapped = new Dictionary<string, int>(StringComparer.Ordinal);

    foreach (var edition in SurveyEditions.All)
    {
        var path = Path.Combine(downloads, edition.FileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Run `fetch` first: {path} is missing.");
        }

        var answers = await SurveyReader.ReadAsync(edition, path, cancellationToken);
        var exclusions = new Dictionary<SurveyExclusion, int>();
        var used = 0;
        foreach (var answer in answers)
        {
            var (response, exclusion) = SalarySurveyNormalizer.Normalize(answer);
            if (response is not null)
            {
                normalized.Add(response);
                used++;
            }
            else
            {
                exclusions[exclusion!.Value] = exclusions.GetValueOrDefault(exclusion.Value) + 1;
                if (exclusion == SurveyExclusion.UnknownPosition)
                {
                    var label = answer.Position?.Trim() ?? "(boş)";
                    unmapped[label] = unmapped.GetValueOrDefault(label) + 1;
                }
            }
        }

        editions.Add(new SalarySurveyEdition(edition.Year, edition.SourceCode, edition.SourceName, edition.SourceUrl,
            edition.PublishedMonth, answers.Count, used));
        report.AppendLine($"{edition.Year} {edition.SourceCode}: {answers.Count} yanıt, {used} kullanıldı; " +
                          string.Join(", ", exclusions.OrderBy(e => e.Key).Select(e => $"{e.Key} {e.Value}")));
    }

    var cells = SalaryMarketAggregator.Aggregate(normalized, MinimumResponses);
    Directory.CreateDirectory(seed);
    await using (var writer = new StreamWriter(Path.Combine(seed, SalaryMarketSeed.EditionsFile), false, new UTF8Encoding(false)))
    {
        SalaryMarketSeed.WriteEditions(writer, editions);
    }

    await using (var writer = new StreamWriter(Path.Combine(seed, SalaryMarketSeed.CellsFile), false, new UTF8Encoding(false)))
    {
        SalaryMarketSeed.WriteCells(writer, cells);
    }

    report.AppendLine();
    report.AppendLine($"{cells.Count} hücre yazıldı (eşik {MinimumResponses}).");
    foreach (var group in SalaryMarketGroups.All)
    {
        var years = cells.Where(c => c.Group == group.Slug && c.Dimension == SalaryMarketDimension.All).Select(c => c.Year).ToList();
        report.AppendLine($"  {group.Slug}: {(years.Count == 0 ? "yayınlanacak yıl yok" : string.Join(" ", years))}");
    }

    report.AppendLine();
    report.AppendLine("Eşlenmeyen pozisyonlar (en sık 25):");
    foreach (var (label, count) in unmapped.OrderByDescending(u => u.Value).Take(25))
    {
        report.AppendLine($"  {count,5}  {label}");
    }

    // The report names raw labels, so it goes next to the downloads, not into the repository.
    var reportPath = Path.Combine(downloads, "build-report.txt");
    await File.WriteAllTextAsync(reportPath, report.ToString(), cancellationToken);
    Console.Write(report.ToString());
    Console.WriteLine($"Rapor: {reportPath}");
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
