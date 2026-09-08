using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Benchmark;
using AfterApply.Application.Benchmark.Contracts;
using AfterApply.Domain.Benchmark;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Benchmark;

/// <summary>
/// The public benchmark, end to end. The threshold is set to three for these tests so the rule that
/// matters — nothing is compared below it — can be crossed in a few requests rather than thirty.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class BenchmarkTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private const int Threshold = 3;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public Task InitializeAsync() => InitialiseAsync();

    private async Task InitialiseAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(BenchmarkTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("Benchmark:MinimumSampleSize", Threshold.ToString());
        });

        // Never given an Authorization header: every test is also an assertion that a stranger can
        // answer this, which is the entire point of the page.
        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private static SubmitBenchmarkRequest Answer(
        int applications = 100, int replies = 20,
        BenchmarkSector sector = BenchmarkSector.SoftwareAndIt,
        BenchmarkPeriod period = BenchmarkPeriod.LastSixMonths,
        BenchmarkSeniority? seniority = null, BenchmarkLocation? location = null,
        string? locale = "tr", string? website = null) =>
        new(applications, replies, sector, period, seniority, location, locale, website);

    private Task<HttpResponseMessage> PostAsync(SubmitBenchmarkRequest request) =>
        _client.PostAsJsonAsync("/api/benchmark/submissions", request, JsonOptions);

    private async Task<BenchmarkResultResponse> SubmitAsync(SubmitBenchmarkRequest request)
    {
        var response = await PostAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<BenchmarkResultResponse>(JsonOptions))!;
    }

    [Fact]
    public async Task A_Stranger_Can_Answer_And_Gets_Their_Own_Rate_Back()
    {
        var result = await SubmitAsync(Answer(applications: 80, replies: 20));

        // Percent, one decimal — the same scale every other rate in this API uses.
        result.YourRate.ShouldBe(25.0);
        result.ApplicationCount.ShouldBe(80);
        result.ReplyCount.ShouldBe(20);
        result.Sector.ShouldBe(BenchmarkSector.SoftwareAndIt);
        result.SampleSize.ShouldBe(1);
        result.TotalSubmissions.ShouldBe(1);
        result.MinimumSampleSize.ShouldBe(Threshold);
        result.Scope.ShouldBe(BenchmarkComparisonScope.None);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.BenchmarkSubmissions.AsNoTracking().SingleAsync();
        stored.ApplicationCount.ShouldBe(80);
        stored.ReplyCount.ShouldBe(20);
        stored.Locale.ShouldBe("tr");
        stored.Seniority.ShouldBeNull();
        stored.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Below_The_Threshold_Nothing_Is_Compared()
    {
        await SubmitAsync(Answer(replies: 10));
        var result = await SubmitAsync(Answer(replies: 30));

        // Two answers in total, so neither the sector nor the overall pool can carry a median.
        result.Scope.ShouldBe(BenchmarkComparisonScope.None);
        result.MedianRate.ShouldBeNull();
        result.ShareBelowYou.ShouldBeNull();
        result.ComparedAgainstCount.ShouldBeNull();

        // The participation counts are still returned — withholding the comparison must not mean
        // withholding the reason to come back.
        result.SampleSize.ShouldBe(2);
        result.TotalSubmissions.ShouldBe(2);
    }

    [Fact]
    public async Task Crossing_The_Threshold_Turns_The_Comparison_On()
    {
        await SubmitAsync(Answer(applications: 100, replies: 10));
        await SubmitAsync(Answer(applications: 100, replies: 20));
        var result = await SubmitAsync(Answer(applications: 100, replies: 60));

        result.Scope.ShouldBe(BenchmarkComparisonScope.Sector);
        result.ComparedAgainstCount.ShouldBe(Threshold);
        result.SampleSize.ShouldBe(Threshold);
        result.MedianRate.ShouldBe(20.0);
        result.YourRate.ShouldBe(60.0);
        // Two of the three answers are lower than this one.
        result.ShareBelowYou.ShouldBe(66.7);
    }

    [Fact]
    public async Task Each_Sector_Reaches_The_Threshold_On_Its_Own()
    {
        // The cell is the sector: filling one must not unlock another.
        for (var i = 0; i < Threshold; i++)
        {
            await SubmitAsync(Answer(sector: BenchmarkSector.SoftwareAndIt));
        }

        var other = await SubmitAsync(Answer(sector: BenchmarkSector.Education));

        // Education has one answer, so it gets no sector median of its own...
        other.SampleSize.ShouldBe(1);
        other.Scope.ShouldNotBe(BenchmarkComparisonScope.Sector);
        // ...but the overall pool has cleared the bar, so the fallback answers instead — drawn
        // from everyone, and the response says so rather than passing it off as Education's.
        other.Scope.ShouldBe(BenchmarkComparisonScope.Overall);
        other.ComparedAgainstCount.ShouldBe(Threshold + 1);
        // The total still counts everyone, which is the number worth sharing.
        other.TotalSubmissions.ShouldBe(Threshold + 1);
    }

    [Fact]
    public async Task More_Replies_Than_Applications_Is_Rejected()
    {
        // The one mistake a person plausibly makes by accident.
        var response = await PostAsync(Answer(applications: 10, replies: 11));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldHaveStoredNothingAsync();
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(2001, 0)]
    public async Task An_Impossible_Application_Count_Is_Rejected(int applications, int replies)
    {
        (await PostAsync(Answer(applications, replies))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldHaveStoredNothingAsync();
    }

    [Fact]
    public async Task A_Missing_Required_Answer_Is_Rejected()
    {
        var response = await _client.PostAsJsonAsync("/api/benchmark/submissions",
            new SubmitBenchmarkRequest(100, 20, null, BenchmarkPeriod.LastSixMonths, null, null, "tr", null),
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldHaveStoredNothingAsync();
    }

    [Fact]
    public async Task A_Filled_Honeypot_Is_Rejected()
    {
        // Anything that walks the form filling every input it finds. This is the defence available:
        // a CAPTCHA is a third-party script, which the CSP forbids and the Cookie Policy denies.
        var response = await PostAsync(Answer(website: "https://example.com"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldHaveStoredNothingAsync();
    }

    [Fact]
    public async Task An_Unknown_Locale_Is_Rejected()
    {
        (await PostAsync(Answer(locale: "de"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PostAsync(Answer(locale: null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ShouldHaveStoredNothingAsync();
    }

    [Fact]
    public async Task The_Optional_Answers_Are_Stored_When_Given()
    {
        await SubmitAsync(Answer(seniority: BenchmarkSeniority.Senior, location: BenchmarkLocation.Izmir));

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.BenchmarkSubmissions.AsNoTracking().SingleAsync();
        stored.Seniority.ShouldBe(BenchmarkSeniority.Senior);
        stored.Location.ShouldBe(BenchmarkLocation.Izmir);
    }

    [Fact]
    public async Task The_Summary_Is_Public_And_Reports_Participation_Per_Sector()
    {
        await SubmitAsync(Answer(sector: BenchmarkSector.SoftwareAndIt));
        await SubmitAsync(Answer(sector: BenchmarkSector.SoftwareAndIt));
        await SubmitAsync(Answer(sector: BenchmarkSector.Education));

        var response = await _client.GetAsync("/api/benchmark/summary");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var summary = (await response.Content.ReadFromJsonAsync<BenchmarkSummaryResponse>(JsonOptions))!;
        summary.TotalSubmissions.ShouldBe(3);
        summary.MinimumSampleSize.ShouldBe(Threshold);
        summary.BySector.ShouldContain(s => s.Sector == BenchmarkSector.SoftwareAndIt && s.Count == 2);
        summary.BySector.ShouldContain(s => s.Sector == BenchmarkSector.Education && s.Count == 1);
    }

    [Fact]
    public async Task Nothing_Stored_Can_Be_Traced_To_Anyone()
    {
        await SubmitAsync(Answer());

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The table's shape is the privacy claim on the page's methodology note, so it is asserted
        // rather than trusted: no user id, and every stored column is a count, a fixed category or
        // the site language.
        var columns = typeof(BenchmarkSubmission)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        columns.ShouldNotContain("UserId");
        columns.ShouldNotContain("IpAddress");
        columns.ShouldNotContain("UserAgent");
        (await db.BenchmarkSubmissions.CountAsync()).ShouldBe(1);
    }

    private async Task ShouldHaveStoredNothingAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.BenchmarkSubmissions.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task A_Sector_That_Can_Stand_Alone_Is_Never_Overridden_By_The_Fallback()
    {
        // The fallback exists for an empty sector, not to replace a real one. Software gets its own
        // three answers at 10%; a much larger crowd in another sector at 90% must not move it.
        for (var i = 0; i < Threshold; i++)
        {
            await SubmitAsync(Answer(applications: 100, replies: 10, sector: BenchmarkSector.SoftwareAndIt));
        }

        for (var i = 0; i < Threshold * 3; i++)
        {
            await SubmitAsync(Answer(applications: 100, replies: 90, sector: BenchmarkSector.FinanceAndInsurance));
        }

        var result = await SubmitAsync(Answer(applications: 100, replies: 10, sector: BenchmarkSector.SoftwareAndIt));

        result.Scope.ShouldBe(BenchmarkComparisonScope.Sector);
        result.MedianRate.ShouldBe(10.0);
        result.ComparedAgainstCount.ShouldBe(Threshold + 1);
    }
}
