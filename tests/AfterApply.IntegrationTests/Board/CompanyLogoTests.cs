using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Board.Contracts;
using AfterApply.Application.Companies;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Board;

/// <summary>A scripted web: each URL answers with the status, body, type and redirect a test set.
/// Anything unscripted is a 404, and every request is recorded, so "we never asked that host"
/// is assertable.</summary>
public sealed class ScriptedHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode Status, byte[] Body, string? Type, string? Location)> _routes = new();
    private readonly List<Uri> _requested = [];

    public IReadOnlyList<Uri> Requested
    {
        get
        {
            lock (_requested)
            {
                return [.. _requested];
            }
        }
    }

    public void Serve(string url, byte[] body, string? type = null) =>
        _routes[url] = (HttpStatusCode.OK, body, type, null);

    public void Fail(string url, HttpStatusCode status) =>
        _routes[url] = (status, [], null, null);

    public void Redirect(string url, string location) =>
        _routes[url] = (HttpStatusCode.Found, [], null, location);

    public void Reset()
    {
        _routes.Clear();
        lock (_requested)
        {
            _requested.Clear();
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        lock (_requested)
        {
            _requested.Add(uri);
        }

        if (!_routes.TryGetValue(uri.AbsoluteUri, out var route))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }

        var response = new HttpResponseMessage(route.Status) { Content = new ByteArrayContent(route.Body) };
        if (route.Type is not null)
        {
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(route.Type);
        }

        if (route.Location is not null)
        {
            response.Headers.Location = new Uri(route.Location);
        }

        return Task.FromResult(response);
    }
}

public sealed class CompanyLogoProfile : IHostProfile
{
    public ScriptedHttpHandler Web { get; } = new();

    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Board:Enabled"] = "true"
        }));
        builder.ConfigureServices(services =>
            services.AddHttpClient(nameof(ICompanyLogoService)).ConfigurePrimaryHttpMessageHandler(() => Web));
    }

    public void Reset() => Web.Reset();
}

/// <summary>
/// Company logos for the board (DECISIONS.md 2026-09-27): fetched from the company's LinkedIn page
/// with the fetch rules an untrusted URL needs, stored by us, and shown only to users who applied
/// there.
/// </summary>
public class CompanyLogoTests(ApiHost<CompanyLogoProfile> host) : IClassFixture<ApiHost<CompanyLogoProfile>>, IAsyncLifetime
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = ApiHost.JsonOptions;

    private const string PageUrl = "https://www.linkedin.com/company/acme-yazilim/";
    private const string LogoUrl = "https://media.licdn.com/dms/image/v2/D4/company-logo_200_200/B4/0/1/acme_logo?e=1&v=beta";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private HttpClient _client = null!;
    private Guid _companyId;

    private ScriptedHttpHandler Web => host.Profile.Web;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        (_client, _) = await host.RegisterAsync("logo.owner@example.com");
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Acme Yazılım", "Backend Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow, null, null), Json);
        response.EnsureSuccessStatusCode();
        _companyId = (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(Json))!.CompanyId;
        await host.WithDbAsync(db => db.Companies.Where(c => c.Id == _companyId)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.LinkedInUrl, PageUrl)));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static byte[] PageNaming(string company, string logoUrl) => System.Text.Encoding.UTF8.GetBytes($"""
        <html><head>
        <meta property="og:title" content="{company} | LinkedIn">
        <meta property="og:image" content="{logoUrl.Replace("&", "&amp;")}">
        </head></html>
        """);

    private Task FetchAsync() => host.WithScopeAsync(services =>
        services.GetRequiredService<ICompanyLogoService>().FetchAsync(_companyId, CancellationToken.None));

    private Task<bool> StoredAsync() =>
        host.WithDbAsync(db => db.CompanyLogos.AnyAsync(l => l.CompanyId == _companyId && l.Content != null));

    [Fact]
    public async Task A_Logo_Found_On_The_Companys_Page_Is_Served_To_Its_Applicant_Only()
    {
        Web.Serve(PageUrl, PageNaming("Acme Yazılım", LogoUrl));
        Web.Serve(LogoUrl, Png, "image/png");

        await FetchAsync();

        var response = await _client.GetAsync($"/api/board/company-logos/{_companyId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).ShouldBe(Png);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.CacheControl!.Private.ShouldBeTrue();
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");

        var board = await _client.GetFromJsonAsync<BoardResponse>("/api/board", Json);
        board!.Columns.SelectMany(c => c.Cards).Single().HasCompanyLogo.ShouldBeTrue();

        var (stranger, _) = await host.RegisterAsync("logo.stranger@example.com");
        (await stranger.GetAsync($"/api/board/company-logos/{_companyId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Page_That_Names_Another_Company_Gives_No_Logo()
    {
        Web.Serve(PageUrl, PageNaming("Başka Holding", LogoUrl));
        Web.Serve(LogoUrl, Png, "image/png");

        await FetchAsync();

        (await StoredAsync()).ShouldBeFalse();
        Web.Requested.ShouldNotContain(u => u.Host == "media.licdn.com");
    }

    [Theory]
    [InlineData("https://static.licdn.com/aero-v1/sc/h/placeholder")]
    [InlineData("https://evil.example/company-logo_200_200/x.png")]
    public async Task An_Image_Anywhere_But_LinkedIns_Media_Host_Is_Never_Fetched(string imageUrl)
    {
        Web.Serve(PageUrl, PageNaming("Acme Yazılım", imageUrl));
        Web.Serve(imageUrl, Png, "image/png");

        await FetchAsync();

        (await StoredAsync()).ShouldBeFalse();
        Web.Requested.Select(u => u.Host).Distinct().ShouldBe(["www.linkedin.com"]);
    }

    [Fact]
    public async Task An_Svg_Or_Oversized_Or_Redirected_Image_Is_Refused()
    {
        Web.Serve(PageUrl, PageNaming("Acme Yazılım", LogoUrl));

        // An SVG that claims to be a PNG: the type comes from the bytes.
        Web.Serve(LogoUrl, "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray(), "image/png");
        await FetchAsync();
        (await StoredAsync()).ShouldBeFalse();

        // Past the cap, even with a valid header.
        await host.WithDbAsync(db => db.CompanyLogos.ExecuteDeleteAsync());
        Web.Serve(LogoUrl, [.. Png, .. new byte[CompanyLogoImage.MaxBytes]], "image/png");
        await FetchAsync();
        (await StoredAsync()).ShouldBeFalse();

        // A redirect off the media host is not followed.
        await host.WithDbAsync(db => db.CompanyLogos.ExecuteDeleteAsync());
        Web.Redirect(LogoUrl, "https://evil.example/logo.png");
        await FetchAsync();
        (await StoredAsync()).ShouldBeFalse();
        Web.Requested.ShouldNotContain(u => u.Host == "evil.example");
    }

    [Fact]
    public async Task A_Company_Looked_At_Is_Not_Fetched_Again_Until_The_Retry_Window()
    {
        await FetchAsync(); // nothing scripted: the page is a 404
        await FetchAsync();

        Web.Requested.Count(u => u.Host == "www.linkedin.com").ShouldBe(1);
        (await host.WithDbAsync(db => db.CompanyLogos.CountAsync(l => l.CompanyId == _companyId))).ShouldBe(1);
    }

    [Theory]
    [InlineData(999)] // LinkedIn's bot wall
    [InlineData(429)]
    [InlineData(503)]
    public async Task Throttling_Or_A_Server_Error_Records_Nothing_So_The_Next_Run_Asks_Again(int status)
    {
        Web.Fail(PageUrl, (HttpStatusCode)status);

        await FetchAsync();

        (await host.WithDbAsync(db => db.CompanyLogos.AnyAsync(l => l.CompanyId == _companyId))).ShouldBeFalse();
        (await ScheduleAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task An_Admin_Block_Takes_The_Logo_Down_For_Good()
    {
        Web.Serve(PageUrl, PageNaming("Acme Yazılım", LogoUrl));
        Web.Serve(LogoUrl, Png, "image/png");
        await FetchAsync();

        var (admin, adminAuth) = await host.RegisterAsync("logo.admin@example.com");
        (await _client.PostAsync($"/api/admin/companies/{_companyId}/logo/block", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await host.MakeAdminAsync(adminAuth.User.Id);
        (await admin.PostAsync($"/api/admin/companies/{_companyId}/logo/block", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.GetAsync($"/api/board/company-logos/{_companyId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var board = await _client.GetFromJsonAsync<BoardResponse>("/api/board", Json);
        board!.Columns.SelectMany(c => c.Cards).Single().HasCompanyLogo.ShouldBeFalse();

        Web.Reset();
        Web.Serve(PageUrl, PageNaming("Acme Yazılım", LogoUrl));
        await FetchAsync();
        Web.Requested.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Nightly_Backfill_Schedules_Only_Tracked_Companies_Not_Yet_Looked_At()
    {
        // A company nobody applied to, with a LinkedIn page: not worth a request.
        await host.WithDbAsync(async db =>
        {
            var untracked = AfterApply.Domain.Companies.Company.Create("Kimsenin Şirketi", DateTimeOffset.UtcNow);
            db.Companies.Add(untracked);
            await db.SaveChangesAsync();
            await db.Companies.Where(c => c.Id == untracked.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LinkedInUrl, "https://www.linkedin.com/company/kimse/"));
        });
        host.Jobs.Clear();

        var scheduled = await ScheduleAsync();

        scheduled.ShouldBe(1);
        Web.Serve(PageUrl, PageNaming("Acme Yazılım", LogoUrl));
        Web.Serve(LogoUrl, Png, "image/png");
        await host.RunJobsAsync();
        (await StoredAsync()).ShouldBeTrue();
        Web.Requested.ShouldNotContain(u => u.AbsolutePath.Contains("kimse"));

        (await ScheduleAsync()).ShouldBe(0);
    }

    private async Task<int> ScheduleAsync()
    {
        var count = 0;
        await host.WithScopeAsync(async services =>
            count = await services.GetRequiredService<ICompanyLogoService>().ScheduleBackfillAsync(CancellationToken.None));
        return count;
    }
}
