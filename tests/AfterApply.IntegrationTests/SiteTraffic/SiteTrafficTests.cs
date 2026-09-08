using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.SiteTraffic.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.SiteTraffic;

/// <summary>
/// The public visit counter, end to end. Two things are being proved here that a unit test cannot:
/// that the endpoint takes no credentials at all, and that a repeat of the same visit lands on the
/// same row instead of a second one — the ON CONFLICT upsert and the unique index agreeing.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SiteTrafficTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public Task InitializeAsync() => InitialiseAsync();

    private async Task InitialiseAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(SiteTrafficTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        // No Authorization header is ever set on this client, on purpose: every test below is also
        // an assertion that the counter works for a visitor who has no account.
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

    private Task<HttpResponseMessage> ReportAsync(string? eventName, string? path, string? referrer = null) =>
        _client.PostAsJsonAsync("/api/site-traffic/events",
            new RecordSiteTrafficEventRequest(eventName, path, referrer), JsonOptions);

    private async Task<List<(string Event, string Path, string Locale, string ReferrerHost, int Count)>> RowsAsync()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.SiteTrafficDailyCounters.AsNoTracking().ToListAsync();
        return rows.Select(r => (r.Event.ToString(), r.Path, r.Locale, r.ReferrerHost, r.Count)).ToList();
    }

    [Fact]
    public async Task An_Anonymous_Visit_To_A_Public_Page_Is_Counted()
    {
        var response = await ReportAsync("page_view", "/tr/guide/how-many-applications",
            "https://www.google.com/search?q=kac+basvuru");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var rows = await RowsAsync();
        rows.ShouldHaveSingleItem();
        rows[0].Event.ShouldBe("PageView");
        rows[0].Path.ShouldBe("/guide/how-many-applications");
        rows[0].Locale.ShouldBe("tr");
        // The search term never made it into storage; only the host did.
        rows[0].ReferrerHost.ShouldBe("google.com");
        rows[0].Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_Same_Visit_Twice_Increments_Rather_Than_Duplicating()
    {
        await ReportAsync("page_view", "/en/help/import");
        await ReportAsync("page_view", "/en/help/import");
        await ReportAsync("page_view", "/en/help/import");

        var rows = await RowsAsync();
        rows.ShouldHaveSingleItem();
        rows[0].Count.ShouldBe(3);
    }

    [Fact]
    public async Task Concurrent_Reports_Of_The_Same_Page_All_Land_On_One_Row()
    {
        // The reason RecordAsync is an ON CONFLICT upsert instead of load-then-increment: this is
        // the counter's normal traffic pattern, not an edge case.
        await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => ReportAsync("page_view", "/tr/cookies")));

        var rows = await RowsAsync();
        rows.ShouldHaveSingleItem();
        rows[0].Count.ShouldBe(12);
    }

    [Fact]
    public async Task Different_Languages_Are_Counted_Separately()
    {
        await ReportAsync("page_view", "/tr/privacy");
        await ReportAsync("page_view", "/en/privacy");

        var rows = await RowsAsync();
        rows.Count.ShouldBe(2);
        rows.Select(r => r.Locale).OrderBy(l => l).ShouldBe(["en", "tr"]);
    }

    [Theory]
    [InlineData("/tr/dashboard")]
    [InlineData("/tr/applications/0192e5c1-6f3a-7c2b-9a11-4f0d2e8b5a77")]
    [InlineData("/tr/auth/google/callback?code=4/0AbCd")]
    [InlineData("/tr/settings")]
    [InlineData("scroll_depth-is-not-a-path")]
    public async Task A_Report_That_Is_Not_A_Public_Page_Is_Accepted_And_Stored_Nowhere(string path)
    {
        var response = await ReportAsync("page_view", path);

        // 204 rather than 400: the caller is told nothing about what the allowlist contains.
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await RowsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Injection_Shaped_Input_Cannot_Reach_The_Table()
    {
        // Two independent defences are being exercised at once. The allowlist rejects these values
        // long before SQL — a path must match a fixed set or a kebab-case slug, a referrer host a
        // bounded charset — and the write itself is a parameterised ExecuteSqlInterpolatedAsync, so
        // even a value that got through would travel as a parameter rather than as SQL text.
        var payloads = new[]
        {
            "/tr/guide/x'); DROP TABLE \"SiteTrafficDailyCounters\"; --",
            "/tr/'; DELETE FROM \"Users\"; --",
            "/tr/guide/x\" OR \"1\"=\"1"
        };

        foreach (var payload in payloads)
        {
            (await ReportAsync("page_view", payload)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await ReportAsync("page_view", "/tr", "https://evil.example/'); DROP TABLE \"Users\"; --"))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        // The tables are still there and hold exactly what they should: the three path payloads were
        // dropped, and the three referrer reports counted one legitimate /tr page view each.
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.CountAsync()).ShouldBe(0);

        var rows = await RowsAsync();
        rows.ShouldHaveSingleItem();
        rows[0].Path.ShouldBe("/");
        rows[0].Count.ShouldBe(3);

        // The referrer is the more interesting half. "evil.example" is a perfectly ordinary host,
        // so it survives — the payload was in the referring URL's *path*, and the path is precisely
        // what ResolveReferrerHost throws away. Stored is the host and nothing else.
        rows[0].ReferrerHost.ShouldBe("evil.example");
        rows[0].ReferrerHost.ShouldNotContain("DROP");
        rows[0].ReferrerHost.ShouldNotContain(";");
    }

    [Fact]
    public async Task An_Oversized_Body_Is_Rejected_Outright()
    {
        // Length is the one thing worth a 400: a 5,000-character path is not a browser.
        var response = await ReportAsync("page_view", "/tr/guide/" + new string('a', 5000));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await RowsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Missing_Event_Or_Path_Is_Rejected()
    {
        (await ReportAsync(null, "/tr")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReportAsync("page_view", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_Funnel_Events_Are_Stored_Under_Their_Own_Names()
    {
        await ReportAsync("cta_get_started", "/tr");
        await ReportAsync("register_started", "/tr/register");
        await ReportAsync("register_completed", "/tr/register");

        var rows = await RowsAsync();
        rows.Select(r => r.Event).OrderBy(e => e)
            .ShouldBe(["CtaGetStarted", "RegisterCompleted", "RegisterStarted"]);
    }

    [Fact]
    public async Task Reading_The_Counts_Back_Needs_An_Admin()
    {
        (await _client.GetAsync("/api/admin/site-traffic")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var signedIn = _factory!.CreateClient();
        var registration = await signedIn.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("traffic.reader@example.com", "P@ssw0rd123!", "Traffic", "Reader", true),
            JsonOptions);
        registration.EnsureSuccessStatusCode();
        var auth = await registration.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        signedIn.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        (await signedIn.GetAsync("/api/admin/site-traffic")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        signedIn.Dispose();
    }
}
