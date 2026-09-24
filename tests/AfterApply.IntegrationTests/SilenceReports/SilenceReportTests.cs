using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.CompanyIntelligence.Contracts;
using AfterApply.Application.SilenceReports;
using AfterApply.Domain.Benchmark;
using AfterApply.Domain.Companies;
using AfterApply.Domain.SilenceReports;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using StackExchange.Redis;

namespace AfterApply.IntegrationTests.SilenceReports;

/// <summary>The shipped floor (5 reports, 2 quarters); company figures stay behind their flag
/// except in the "intelligence" variant.</summary>
public sealed class SilenceReportProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("CompanyIntelligence:HiddenBelow", "2");
    }
}

/// <summary>
/// The anonymous "no reply" report on a company page, end to end (growth item 1.6). Every client
/// here is a stranger with no Authorization header, and each says who it is through
/// X-Forwarded-For — under the in-memory server there is no TCP connection, so this is how the
/// per-address rate limit and repeat block are exercised, as in RequestAuditTests.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SilenceReportTests(ApiHost<SilenceReportProfile> host) : IClassFixture<ApiHost<SilenceReportProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    private WebApplicationFactory<Program> _disabledFactory =>
        host.Variant("disabled", builder => builder.UseSetting("SilenceReports:Enabled", "false"));

    // The suite runs with rate limiting off (TestContainerCleanup); this variant turns it back on
    // with the shipped bucket size.
    private WebApplicationFactory<Program> _limitedFactory =>
        host.Variant("limited", builder => builder.UseSetting("RateLimiting:Enabled", "true"));

    private WebApplicationFactory<Program> _intelligenceFactory =>
        host.Variant("intelligence", builder => builder.UseSetting("CompanyIntelligence:Enabled", "true"));

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static HttpClient Stranger(WebApplicationFactory<Program> factory, string ip)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
        return client;
    }

    private static SubmitSilenceReportRequest Report(
        SilenceStage? stage = SilenceStage.AfterTechnicalInterview, SilenceWait? wait = SilenceWait.OneToTwoMonths,
        bool? promiseGiven = null, string? locale = "tr", string? website = null, BenchmarkSource? source = null) =>
        new(stage, wait, promiseGiven, locale, website, source);

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string slug, SubmitSilenceReportRequest request) =>
        client.PostAsJsonAsync($"/api/companies/public/{slug}/silence-reports", request, JsonOptions);

    private async Task<Company> SeedCompanyAsync(string name)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = Company.Create(name, DateTimeOffset.UtcNow);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        // Reports arrive through the public page, which only a listed company has.
        await TestCompanies.MakeListedAsync(_factory.Services, company.Id);
        return company;
    }

    private async Task<List<SilenceReport>> ReportsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.SilenceReports.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task A_Stranger_Can_Report_And_Only_The_Closed_Answers_Are_Stored()
    {
        var company = await SeedCompanyAsync("Sessiz Teknoloji");
        var client = Stranger(_factory, "203.0.113.10");

        var response = await PostAsync(client, company.Slug!,
            Report(SilenceStage.AfterFinalInterview, SilenceWait.TwoToThreeMonths, promiseGiven: true, source: BenchmarkSource.Eksi));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var stored = (await ReportsAsync()).ShouldHaveSingleItem();
        stored.CompanyId.ShouldBe(company.Id);
        stored.Stage.ShouldBe(SilenceStage.AfterFinalInterview);
        stored.Wait.ShouldBe(SilenceWait.TwoToThreeMonths);
        stored.PromiseGiven.ShouldBe(true);
        stored.Source.ShouldBe(BenchmarkSource.Eksi);
        stored.Locale.ShouldBe("tr");
        stored.SilentSinceMonth.Day.ShouldBe(1);
    }

    [Fact]
    public async Task The_Request_Is_Audited_Without_A_User_And_The_Address_Stays_Out_Of_The_Report()
    {
        const string ip = "203.0.113.11";
        var company = await SeedCompanyAsync("Denetim Yazılım");

        (await PostAsync(Stranger(_factory, ip), company.Slug!, Report())).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.RequestAudits.AsNoTracking()
            .SingleAsync(a => a.Path == $"/api/companies/public/{company.Slug}/silence-reports");
        audit.UserId.ShouldBeNull();
        audit.IpAddress.ShouldBe(ip);

        // The repeat block lives in Redis under a keyed hash: neither the address nor the company
        // id is readable from the key.
        var redis = _factory.Services.GetRequiredService<IConnectionMultiplexer>();
        var database = redis.GetDatabase();
        var keys = redis.GetServers().First().Keys(database.Database, "silence-report:repeat:*").Select(k => k.ToString()).ToList();
        var key = keys.ShouldHaveSingleItem();
        key.ShouldNotContain(ip);
        key.ShouldNotContain(company.Id.ToString());
        (await database.KeyTimeToLiveAsync(key)).ShouldNotBeNull().TotalDays.ShouldBeInRange(29.9, 30.0);
    }

    [Fact]
    public async Task An_Unknown_Company_Is_Not_Found_And_Nothing_Is_Stored()
    {
        var response = await PostAsync(Stranger(_factory, "203.0.113.12"), "boyle-bir-sirket-yok", Report());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReportsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Missing_Answer_Or_A_Filled_Honeypot_Is_Refused()
    {
        var company = await SeedCompanyAsync("Doğrulama AŞ");
        var client = Stranger(_factory, "203.0.113.13");

        (await PostAsync(client, company.Slug!, Report(stage: null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PostAsync(client, company.Slug!, Report(wait: null))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PostAsync(client, company.Slug!, Report(website: "https://spam.example"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ReportsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Same_Connection_Reports_A_Company_Once_Per_Repeat_Window()
    {
        var first = await SeedCompanyAsync("Tekrar Bir");
        var second = await SeedCompanyAsync("Tekrar İki");
        var client = Stranger(_factory, "203.0.113.14");

        (await PostAsync(client, first.Slug!, Report())).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var again = await PostAsync(client, first.Slug!, Report(SilenceStage.AfterHrScreen));
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await again.Content.ReadAsStringAsync()).ShouldNotContain("203.0.113.14");

        // Another company from the same address, and the same company from another address, both pass.
        (await PostAsync(client, second.Slug!, Report())).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await PostAsync(Stranger(_factory, "203.0.113.15"), first.Slug!, Report())).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ReportsAsync()).Count.ShouldBe(3);
    }

    [Fact]
    public async Task One_Address_Is_Rate_Limited_Across_Companies()
    {
        var client = Stranger(_limitedFactory, "203.0.113.16");
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
        {
            var company = await SeedCompanyAsync($"Limit Şirketi {i}");
            statuses.Add((await PostAsync(client, company.Slug!, Report())).StatusCode);
        }

        statuses.Take(5).ShouldAllBe(s => s == HttpStatusCode.NoContent);
        statuses[5].ShouldBe(HttpStatusCode.TooManyRequests);
        (await ReportsAsync()).Count.ShouldBe(5);
    }

    [Fact]
    public async Task With_The_Flag_Off_The_Route_Is_Not_Found_And_The_Config_Says_So()
    {
        var company = await SeedCompanyAsync("Kapalı Bayrak");

        var response = await PostAsync(Stranger(_disabledFactory, "203.0.113.17"), company.Slug!, Report());
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReportsAsync()).ShouldBeEmpty();

        var off = await _disabledFactory.CreateClient().GetFromJsonAsync<JsonElement>("/api/config", JsonOptions);
        off.GetProperty("silenceReports").GetProperty("enabled").GetBoolean().ShouldBeFalse();
        var on = await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/config", JsonOptions);
        on.GetProperty("silenceReports").GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Company_Figures_Show_The_Count_Only_Above_The_Floor_And_Never_While_Their_Flag_Is_Off()
    {
        var company = await SeedCompanyAsync("Eşik Holding");
        var now = DateTimeOffset.UtcNow;

        async Task SeedAsync(int monthsAgo, SilenceStage stage)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SilenceReports.Add(SilenceReport.Create(company.Id, stage, SilenceWait.TwoToFourWeeks, null, "tr", null,
                now.AddMonths(-monthsAgo)));
            await db.SaveChangesAsync();
        }

        async Task<CompanyIntelligenceResponse> ReadAsync()
        {
            var response = await _intelligenceFactory.CreateClient().GetAsync($"/api/company-intelligence/{company.Id}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            return (await response.Content.ReadFromJsonAsync<CompanyIntelligenceResponse>(JsonOptions))!;
        }

        // Five reports, all from one quarter's worth of months: withheld.
        for (var i = 0; i < 5; i++)
        {
            await SeedAsync(1, SilenceStage.AfterTechnicalInterview);
        }
        var sameQuarter = await ReadAsync();
        sameQuarter.SilenceReports.ShouldBeNull();
        sameQuarter.SilenceReportThresholds.ShouldBe(new SilenceReportThresholds(5, 2, 12));

        // One more, from a different quarter: shown, as a count by stage.
        await SeedAsync(7, SilenceStage.AfterApplication);
        var shown = (await ReadAsync()).SilenceReports.ShouldNotBeNull();
        shown.Count.ShouldBe(6);
        shown.ByStage.ShouldBe([
            new SilenceStageCount(SilenceStage.AfterApplication, 1),
            new SilenceStageCount(SilenceStage.AfterTechnicalInterview, 5),
        ]);

        // Older than the window: stored, not counted.
        await SeedAsync(14, SilenceStage.AfterOfferTalk);
        (await ReadAsync()).SilenceReports!.Count.ShouldBe(6);
        (await ReportsAsync()).Count.ShouldBe(7);

        // The same company through the shipped flag (off): no figures at all.
        (await _factory.CreateClient().GetAsync($"/api/company-intelligence/{company.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }
}
