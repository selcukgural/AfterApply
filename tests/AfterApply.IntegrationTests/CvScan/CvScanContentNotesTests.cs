using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.CvScan;
using AfterApply.Application.CvScan.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.CvScan;

/// <summary>
/// Layer B — the model's content notes — against a stand-in provider.
///
/// The provider is faked on purpose: what is under test is not what a model says, it is that
/// <b>nothing a model says or fails to say can move the score</b>. Every test here scans the same
/// file twice, once with layer B and once without, and asserts the two scores are identical. The
/// quality of the model's output is a separate question, answered by the eval harness rather than
/// by a test that would have to call a paid API to run.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CvScanContentNotesTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private string _postgres = string.Empty;

    public async Task InitializeAsync() => _postgres = await shared.CreateIsolatedDatabaseAsync(nameof(CvScanContentNotesTests));

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>Answers whatever a test tells it to, including by throwing — the failure path
    /// matters more here than the happy one.</summary>
    private sealed class StubReviewProvider(Func<CvReviewRequest, IReadOnlyList<CvContentNote>> respond)
        : ICvReviewProvider
    {
        public int Calls { get; private set; }

        public Task<IReadOnlyList<CvContentNote>> ReviewAsync(CvReviewRequest request,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private WebApplicationFactory<Program> CreateFactory(bool llmEnabled, ICvReviewProvider? provider = null,
        int dailyCeiling = 200) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("CvScan:LlmEnabled", llmEnabled.ToString());
            builder.UseSetting("CvScan:Review:ProjectId", "test-project");
            builder.UseSetting("CvScan:Review:DailyRequestCeiling", dailyCeiling.ToString());

            if (provider is not null)
            {
                // ConfigureTestServices runs after the app's own registrations, so this replaces
                // the Vertex provider rather than racing it.
                builder.ConfigureTestServices(services => services.AddScoped(_ => provider));
            }
        });

    private static async Task<CvScanResponse> ScanAsync(HttpClient client, bool contentNotes)
    {
        var part = new ByteArrayContent(CvFixtures.ReadablePdf());
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var content = new MultipartFormDataContent
        {
            { part, "file", "cv.pdf" },
            { new StringContent("true"), "consentAccepted" },
            { new StringContent(contentNotes.ToString()), "contentNotesRequested" },
            { new StringContent("5000"), "elapsedMs" },
            { new StringContent("en"), "locale" }
        };

        var response = await client.PostAsync("/api/cv-scan", content);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CvScanResponse>(JsonOptions)).ShouldNotBeNull();
    }

    [Fact]
    public async Task With_The_Feature_Off_The_Page_Is_Told_Nothing_About_Notes()
    {
        await using var factory = CreateFactory(llmEnabled: false);
        using var client = factory.CreateClient();

        // Asked for, and still off: a flag beats a checkbox, which is what makes the flag a
        // kill-switch rather than a suggestion.
        var result = await ScanAsync(client, contentNotes: true);

        result.ReviewStatus.ShouldBe(CvReviewStatus.Disabled);
        result.ContentNotes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Without_The_Optional_Consent_No_Model_Is_Called()
    {
        var provider = new StubReviewProvider(_ => throw new InvalidOperationException("must not be called"));
        await using var factory = CreateFactory(llmEnabled: true, provider);
        using var client = factory.CreateClient();

        var result = await ScanAsync(client, contentNotes: false);

        result.ReviewStatus.ShouldBe(CvReviewStatus.NotRequested);
        provider.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task A_Note_Quoting_The_Cv_Is_Returned_And_A_Fabricated_One_Is_Not()
    {
        var provider = new StubReviewProvider(request =>
        [
            // Verbatim from the fixture CV.
            new CvContentNote(CvContentNoteKind.WeakVerb, "Owned the order tracking service",
                "Say what you changed about it."),
            // Not in the document at all — the shape of a model filling in a plausible line.
            new CvContentNote(CvContentNoteKind.UnquantifiedAchievement, "Managed a team of fifty engineers",
                "Add the number of people.")
        ]);

        await using var factory = CreateFactory(llmEnabled: true, provider);
        using var client = factory.CreateClient();

        var withNotes = await ScanAsync(client, contentNotes: true);
        var withoutNotes = await ScanAsync(client, contentNotes: false);

        withNotes.ReviewStatus.ShouldBe(CvReviewStatus.Ready);
        var note = withNotes.ContentNotes.ShouldHaveSingleItem();
        note.Quote.ShouldBe("Owned the order tracking service");

        // The assertion the whole layer rests on.
        withNotes.Score.ShouldBe(withoutNotes.Score);
        withNotes.Findings.Count.ShouldBe(withoutNotes.Findings.Count);
    }

    [Fact]
    public async Task A_Provider_That_Fails_Costs_The_Notes_And_Nothing_Else()
    {
        var provider = new StubReviewProvider(_ => throw new HttpRequestException("Vertex AI is down"));
        await using var factory = CreateFactory(llmEnabled: true, provider);
        using var client = factory.CreateClient();

        var result = await ScanAsync(client, contentNotes: true);

        // Still a 200 with a score: layer A never depended on the model.
        result.ReviewStatus.ShouldBe(CvReviewStatus.Unavailable);
        result.ContentNotes.ShouldBeEmpty();
        result.Score.ShouldBeGreaterThan(0);
        result.Findings.ShouldNotBeNull();
    }

    /// <summary>
    /// The daily ceiling is what bounds a day's spend on an endpoint anyone can call. Reaching it
    /// degrades layer B and leaves layer A alone.
    /// </summary>
    [Fact]
    public async Task Past_The_Daily_Ceiling_The_Notes_Stop_And_The_Score_Does_Not()
    {
        var provider = new StubReviewProvider(_ =>
            [new CvContentNote(CvContentNoteKind.WeakVerb, "Owned the order tracking service", "Rewrite it.")]);

        await using var factory = CreateFactory(llmEnabled: true, provider, dailyCeiling: 1);
        using var client = factory.CreateClient();

        var first = await ScanAsync(client, contentNotes: true);
        var second = await ScanAsync(client, contentNotes: true);

        first.ReviewStatus.ShouldBe(CvReviewStatus.Ready);
        second.ReviewStatus.ShouldBe(CvReviewStatus.Unavailable);
        second.Score.ShouldBe(first.Score);
        provider.Calls.ShouldBe(1);
    }

    /// <summary>
    /// The opt-in is recorded on the anonymous row — it is the only honest measure of whether
    /// anyone wants layer B, and it is what the ceiling counts. Still no identifier of any kind.
    /// </summary>
    [Fact]
    public async Task The_Opt_In_Is_Recorded_Anonymously()
    {
        var provider = new StubReviewProvider(_ => []);
        await using var factory = CreateFactory(llmEnabled: true, provider);
        using var client = factory.CreateClient();

        await ScanAsync(client, contentNotes: true);
        await ScanAsync(client, contentNotes: false);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await dbContext.CvScanResults.AsNoTracking().ToListAsync();

        rows.Count(row => row.ContentNotesRequested).ShouldBe(1);
        rows.Count(row => !row.ContentNotesRequested).ShouldBe(1);
    }
}
