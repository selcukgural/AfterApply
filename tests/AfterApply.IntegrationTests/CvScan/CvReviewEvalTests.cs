using System.Security.Cryptography;
using AfterApply.Application.CvScan;
using AfterApply.Infrastructure.CvScan;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace AfterApply.IntegrationTests.CvScan;

/// <summary>
/// The eval for layer B: does the model actually find the weakness that was written into a CV, and
/// does it leave a well-written one alone.
///
/// <b>It does not run by default and must not.</b> It calls a paid API over the real network, which
/// the rest of this suite is built to make impossible (see NoOutboundHttpStartup) — so it is opt-in
/// through an environment variable and does nothing at all otherwise:
///
/// <code>
/// CV_REVIEW_EVAL=1 CvScan__Review__ProjectId=your-project \
///   dotnet test tests/AfterApply.IntegrationTests --filter FullyQualifiedName~CvReviewEval
/// </code>
///
/// Application Default Credentials have to be present (<c>gcloud auth application-default login</c>)
/// and the project needs Vertex AI enabled — see DEPLOYMENT.md.
///
/// What it asserts is deliberately weak, and the output is the point: a model's prose cannot be
/// unit-tested, so this prints every note beside the weakness that was planted and leaves the
/// judgement to a person. The two hard assertions are the ones that would make the feature
/// unshippable rather than merely disappointing — a fabricated quote, and a clean CV covered in
/// complaints.
/// </summary>
public class CvReviewEvalTests(ITestOutputHelper output)
{
    private const string EnvironmentSwitch = "CV_REVIEW_EVAL";

    [Fact]
    public async Task Eval_The_Content_Notes_Against_The_Synthetic_Corpus()
    {
        if (Environment.GetEnvironmentVariable(EnvironmentSwitch) != "1")
        {
            // Returns rather than skipping: this xunit version has no runtime skip, and a test that
            // fails when an optional API key is absent would be worse than one that says why it did
            // nothing. The line lands in the run's output.
            output.WriteLine($"Skipped. Set {EnvironmentSwitch}=1 to run the layer B eval against the real Vertex AI API.");
            return;
        }

        var projectId = Environment.GetEnvironmentVariable("CvScan__Review__ProjectId");
        Assert.False(string.IsNullOrWhiteSpace(projectId),
            "Set CvScan__Review__ProjectId to the GCP project the eval should bill.");

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // No database is touched: only the provider is resolved out of this host. The
            // connection string still has to parse, because the host builds the whole app.
            builder.UseSetting("ConnectionStrings:Postgres", "Host=localhost;Database=unused;Username=unused;Password=unused");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("CvScan:LlmEnabled", "true");
            builder.UseSetting("CvScan:Review:ProjectId", projectId);

            // The one place in this assembly that is allowed to open a socket, and only because
            // this test exists to make a real call. Everything else stays blocked.
            builder.ConfigureServices(services => services
                .AddHttpClient(CvScanOptions.ReviewHttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler()));
        });

        using var scope = factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<ICvReviewProvider>();

        var failures = new List<string>();

        foreach (var testCase in CvReviewEvalCorpus.Cases)
        {
            var notes = await provider.ReviewAsync(new CvReviewRequest(testCase.Text, testCase.Locale),
                CancellationToken.None);

            // Through the same gate production uses, so the eval judges what a reader would see
            // rather than what the model returned.
            var shown = CvReviewNotes.Sanitize(notes, testCase.Text);

            output.WriteLine($"=== {testCase.Name} ({testCase.Locale}) ===");
            output.WriteLine($"expected: {(testCase.Expected.Count == 0 ? "nothing" : string.Join(", ", testCase.Expected))}");
            output.WriteLine($"returned: {notes.Count} note(s), {shown.Count} shown after verification");

            foreach (var note in shown)
            {
                output.WriteLine($"  [{note.Kind}] \"{note.Quote}\" → {note.Suggestion}");
            }

            var fabricated = notes.Count - shown.Count;
            if (fabricated > 0)
            {
                // The failure that would make the feature unshippable: a note quoting something
                // that is not in the document. Production drops these; the eval must still see them.
                failures.Add($"{testCase.Name}: {fabricated} note(s) quoted text that is not in the CV");
                foreach (var note in notes.Where(candidate => shown.All(kept => kept.Quote != candidate.Quote)))
                {
                    output.WriteLine($"  DROPPED (not in CV): [{note.Kind}] \"{note.Quote}\"");
                }
            }

            if (testCase.Expected.Count == 0)
            {
                // A clean CV. One note is a difference of opinion; three is a reviewer nobody will
                // read twice.
                if (shown.Count > 2)
                {
                    failures.Add($"{testCase.Name}: {shown.Count} notes on a CV written to have none");
                }

                continue;
            }

            var missed = testCase.Expected.Where(kind => shown.All(note => note.Kind != kind)).ToList();
            if (missed.Count > 0)
            {
                output.WriteLine($"  MISSED: {string.Join(", ", missed)}");
                failures.Add($"{testCase.Name}: missed {string.Join(", ", missed)}");
            }
        }

        output.WriteLine(failures.Count == 0
            ? "All cases passed."
            : $"{failures.Count} failure(s):\n  {string.Join("\n  ", failures)}");

        // Only the structural failures fail the run. Whether the suggestions are worth reading is a
        // judgement the output above exists to support, not something to assert.
        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }
}
