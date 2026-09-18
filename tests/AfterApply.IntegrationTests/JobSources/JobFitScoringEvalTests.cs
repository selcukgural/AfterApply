using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AfterApply.Application.CvScan;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.Documents;
using AfterApply.Infrastructure.JobSources;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace AfterApply.IntegrationTests.JobSources;

/// <summary>
/// The model comparison for the fit score: the same CV and the same real postings through each
/// candidate model, side by side — score spread, whether off-field postings land low, what the
/// matched/missing lists say, tokens and latency. The judgement is a person's; the output is the
/// point (see CvReviewEvalTests for the same reasoning).
///
/// <b>It does not run by default and must not.</b> It calls a paid API over the real network:
///
/// <code>
/// JOB_FIT_EVAL=1 JobSources__Scoring__ProjectId=your-project \
///   JOB_FIT_EVAL_CV=/path/to/cv.pdf JOB_FIT_EVAL_POSTINGS=/path/to/postings-dir \
///   JOB_FIT_EVAL_MODELS=gemini-2.5-flash,gemini-2.5-flash-lite JOB_FIT_EVAL_REPORT=/path/to/report.md \
///   dotnet test tests/AfterApply.IntegrationTests --filter FullyQualifiedName~JobFitScoringEval
/// </code>
///
/// A model may carry a label after <c>#</c> (<c>gemini-2.5-flash#run2</c>) so the same model can be
/// run twice for a repeatability check; a model may also carry a thinking budget after <c>@</c>
/// (<c>gemini-2.5-pro@1024</c>) for the models that cannot turn thinking off.
///
/// The postings directory holds one JSON file per posting with the fields the sweep stores
/// (title, companyName, location, description, seniority, employmentType, plus a "tag": "own"
/// for the CV's own profession, "adjacent" for a neighbouring one, "off" for an unrelated one —
/// the sanity assertion at the end compares "off" against "own"). Nothing from the corpus
/// or the CV is committed: both are personal or third-party text and live outside the repo.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JobFitScoringEvalTests(SharedInfrastructure shared, ITestOutputHelper output)
{
    private const string EnvironmentSwitch = "JOB_FIT_EVAL";

    private sealed record Posting(string Source, string Tag, string ExternalId, string Title, string CompanyName, string? Location,
        string Description, string? Seniority, string? EmploymentType);

    private sealed record Row(string Model, Posting Posting, JobFitScoringResult? Result, long Ms, string? Error);

    [Fact]
    public async Task Compare_The_Candidate_Models_On_A_Real_Cv_And_Real_Postings()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentSwitch) != "1")
        {
            output.WriteLine($"Skipped. Set {EnvironmentSwitch}=1 to run the fit-score model comparison against the real Vertex AI API.");
            return;
        }

        var projectId = Environment.GetEnvironmentVariable("JobSources__Scoring__ProjectId");
        var cvPath = Environment.GetEnvironmentVariable("JOB_FIT_EVAL_CV");
        var postingsDir = Environment.GetEnvironmentVariable("JOB_FIT_EVAL_POSTINGS");
        var models = (Environment.GetEnvironmentVariable("JOB_FIT_EVAL_MODELS") ?? "gemini-2.5-flash,gemini-2.5-flash-lite")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var reportPath = Environment.GetEnvironmentVariable("JOB_FIT_EVAL_REPORT");
        Assert.False(string.IsNullOrWhiteSpace(projectId), "Set JobSources__Scoring__ProjectId to the GCP project the eval should bill.");
        Assert.True(File.Exists(cvPath), "Set JOB_FIT_EVAL_CV to a PDF or DOCX.");
        Assert.True(Directory.Exists(postingsDir), "Set JOB_FIT_EVAL_POSTINGS to a directory of posting JSON files.");

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var postings = Directory.GetFiles(postingsDir!, "*.json").OrderBy(f => f)
            .Select(f => JsonSerializer.Deserialize<Posting>(File.ReadAllText(f), jsonOptions)!)
            .ToList();
        Assert.NotEmpty(postings);

        // A real database for the same reason CvReviewEvalTests needs one: the host opens a
        // connection for Hangfire at startup before a single test line runs.
        var stores = await shared.CreateIsolatedStoresAsync(nameof(JobFitScoringEvalTests));
        var rows = new List<Row>();
        string cvText;

        foreach (var model in models)
        {
            var (modelId, thinkingBudget) = ParseModel(model);
            await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                stores.Apply(builder);
                builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
                builder.UseSetting("JobSources:Enabled", "true");
                builder.UseSetting("JobSources:Scoring:ProjectId", projectId);
                builder.UseSetting("JobSources:Scoring:Model", modelId);
                if (thinkingBudget is { } budget)
                {
                    builder.UseSetting("JobSources:Scoring:ThinkingBudget", budget.ToString(CultureInfo.InvariantCulture));
                }
                // The one real socket in this assembly, for the same reason as the CV eval.
                builder.ConfigureServices(services => services
                    .AddHttpClient(JobFitScoringSettings.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => new CvReviewEvalRealNetworkHandler()));
            });

            using var scope = factory.Services.CreateScope();
            var extractor = scope.ServiceProvider.GetRequiredService<ICvTextExtractor>();
            await using (var stream = File.OpenRead(cvPath!))
            {
                var format = cvPath!.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ? CvFileFormat.Docx : CvFileFormat.Pdf;
                cvText = (await extractor.ExtractAsync(stream, format, CancellationToken.None)).Text;
            }

            var provider = scope.ServiceProvider.GetRequiredService<IJobFitScoringProvider>();
            var settings = scope.ServiceProvider.GetRequiredService<IOptions<JobSourceOptions>>().Value.Scoring;
            Assert.Equal(modelId, provider.Model);

            foreach (var posting in postings)
            {
                var request = new JobFitScoringRequest(cvText, posting.Title, posting.CompanyName, posting.Location, posting.Description,
                    posting.Seniority, posting.EmploymentType, "tr");
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var result = JobFitScores.Sanitize(await provider.ScoreAsync(request, CancellationToken.None));
                    rows.Add(new Row(model, posting, result, stopwatch.ElapsedMilliseconds, result is null ? "empty answer" : null));
                }
                catch (JobFitScoringProviderException exception)
                {
                    rows.Add(new Row(model, posting, null, stopwatch.ElapsedMilliseconds, exception.Message));
                }
            }

            output.WriteLine($"{model}: {rows.Count(r => r.Model == model && r.Result is not null)}/{postings.Count} scored, " +
                             $"est. cost {JobFitScoringCost.Estimate(rows.Where(r => r.Model == model).Sum(r => (long)(r.Result?.InputTokens ?? 0)), rows.Where(r => r.Model == model).Sum(r => (long)(r.Result?.OutputTokens ?? 0)), settings)} USD at the configured (2.5-flash) list price");
        }

        var report = BuildReport(models, postings, rows);
        output.WriteLine(report);
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            await File.WriteAllTextAsync(reportPath, report);
        }

        // The two things that would make a model unusable rather than merely worse: it fails to
        // answer, or it cannot tell an off-field posting from a relevant one.
        foreach (var model in models)
        {
            var scored = rows.Where(r => r.Model == model && r.Result is not null).ToList();
            Assert.True(scored.Count >= postings.Count * 0.9, $"{model}: only {scored.Count}/{postings.Count} postings scored");
            var relevant = scored.Where(r => r.Posting.Tag is "own" or "net").Select(r => r.Result!.Score).DefaultIfEmpty().Average();
            var off = scored.Where(r => r.Posting.Tag == "off").Select(r => r.Result!.Score).DefaultIfEmpty(0).Max();
            Assert.True(off < relevant, $"{model}: an off-field posting ({off}) scored at or above the relevant average ({relevant:F0})");
        }
    }

    private static string BuildReport(string[] models, List<Posting> postings, List<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Fit-score model comparison");
        sb.AppendLine();
        sb.AppendLine("| # | tag | source | posting | " + string.Join(" | ", models.SelectMany(m => new[] { $"{m} score", "ms" })) + " |");
        sb.AppendLine("|---|---|---|---|" + string.Concat(models.Select(_ => "---:|---:|")));
        var i = 0;
        foreach (var posting in postings)
        {
            i++;
            var cells = models.Select(m =>
            {
                var row = rows.Single(r => r.Model == m && r.Posting == posting);
                return $"{(row.Result is null ? "—" : row.Result.Score.ToString(CultureInfo.InvariantCulture))} | {row.Ms}";
            });
            sb.AppendLine($"| {i} | {posting.Tag} | {posting.Source} | {posting.Title} @ {posting.CompanyName} | {string.Join(" | ", cells)} |");
        }

        sb.AppendLine();
        foreach (var model in models)
        {
            var mine = rows.Where(r => r.Model == model).ToList();
            var scored = mine.Where(r => r.Result is not null).ToList();
            var byTag = scored.GroupBy(r => r.Posting.Tag)
                .Select(g => $"{g.Key}: avg {g.Average(r => r.Result!.Score):F0} (min {g.Min(r => r.Result!.Score)}, max {g.Max(r => r.Result!.Score)})");
            sb.AppendLine($"## {model}");
            sb.AppendLine($"- scored {scored.Count}/{mine.Count}; {string.Join("; ", byTag)}");
            sb.AppendLine($"- tokens in/out: {scored.Sum(r => r.Result!.InputTokens)} / {scored.Sum(r => r.Result!.OutputTokens)}; " +
                          $"median latency {Median(mine.Select(r => r.Ms))} ms; errors: {mine.Count(r => r.Error is not null)}");
            sb.AppendLine();
            foreach (var row in scored)
            {
                sb.AppendLine($"### {row.Posting.Title} @ {row.Posting.CompanyName} — {row.Result!.Score}");
                sb.AppendLine($"- {row.Result.Summary}");
                sb.AppendLine($"- matched: {string.Join(", ", row.Result.MatchedCriteria)}");
                sb.AppendLine($"- missing: {string.Join(", ", row.Result.MissingCriteria)}");
                sb.AppendLine($"- skills: {string.Join(", ", row.Result.RequiredSkills)}");
                sb.AppendLine();
            }

            foreach (var row in mine.Where(r => r.Error is not null))
            {
                sb.AppendLine($"- ERROR {row.Posting.Title}: {row.Error}");
            }
        }

        return sb.ToString();
    }

    /// <summary>"gemini-2.5-pro@1024#run2" → ("gemini-2.5-pro", 1024); the label after # is the
    /// report column and stays on the caller's string.</summary>
    private static (string ModelId, int? ThinkingBudget) ParseModel(string spec)
    {
        var withoutLabel = spec.Split('#')[0];
        var parts = withoutLabel.Split('@');
        return (parts[0], parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : null);
    }

    private static long Median(IEnumerable<long> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
    }
}
