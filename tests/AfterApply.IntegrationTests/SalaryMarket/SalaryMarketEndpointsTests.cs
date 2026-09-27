using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.ClientConfig;
using AfterApply.Application.SalaryMarket.Contracts;
using Microsoft.AspNetCore.Hosting;
using Shouldly;

namespace AfterApply.IntegrationTests.SalaryMarket;

/// <summary>The flag ships off; this class turns it on and compares with a host where it stays off.
/// The figures are the compiled-in seed, so nothing is seeded and nothing is reset.</summary>
public sealed class SalaryMarketOnProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder) => builder.UseSetting("SalaryMarket:Enabled", "true");
}

public class SalaryMarketEndpointsTests(ApiHost<SalaryMarketOnProfile> host) : IClassFixture<ApiHost<SalaryMarketOnProfile>>
{
    [Fact]
    public async Task The_List_Is_Anonymous_Cacheable_And_Carries_The_Surveys()
    {
        var response = await host.CreateClient().GetAsync("/api/salary-market/occupations");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.ShouldBeTrue();
        response.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromHours(1));
        response.Headers.Vary.ShouldContain("Origin");

        var body = await response.Content.ReadFromJsonAsync<SalaryOccupationsResponse>(ApiHost.JsonOptions);
        body!.MinimumResponses.ShouldBe(15);
        body.Editions.ShouldContain(e => e.Year == 2026 && e.SourceName == "Önceki Yazılımcı");
        var backEnd = body.Occupations.Single(o => o.Slug == "back-end-developer");
        backEnd.LatestYear.ShouldBe(2026);
        backEnd.Latest.Count.ShouldBeGreaterThanOrEqualTo(15);
        backEnd.Trend.Count.ShouldBe(9);
    }

    [Fact]
    public async Task An_Occupation_Page_Has_Every_Year_Split_By_Level_And_Experience()
    {
        var body = await host.CreateClient()
            .GetFromJsonAsync<SalaryOccupationResponse>("/api/salary-market/occupations/back-end-developer", ApiHost.JsonOptions);

        body!.Years.Select(y => y.Year).ShouldBe(Enumerable.Range(2018, 9));
        var latest = body.Years[^1];
        latest.Levels.Select(l => l.Level).ShouldBe(["Junior", "Middle", "Senior"]);
        latest.Experience.Select(e => e.Experience).ShouldBe(["ZeroToTwo", "ThreeToFive", "SixToTen", "TenPlus"]);
        body.Years.SelectMany(y => y.Levels.Select(l => l.Stats).Concat(y.Experience.Select(e => e.Stats)).Append(y.Overall))
            .ShouldAllBe(s => s.Count >= 15);
    }

    [Fact]
    public async Task Nothing_Per_Person_Or_Extreme_Is_On_The_Wire()
    {
        var raw = await host.CreateClient().GetStringAsync("/api/salary-market/occupations/back-end-developer");

        // Only percentiles: no raw lowest/highest answer, no field the surveys asked but the pages do not use.
        foreach (var field in new[] { "\"min\"", "\"max\"", "lowest", "highest", "gender", "city", "technolog", "company" })
        {
            raw.ShouldNotContain(field, Case.Insensitive);
        }
    }

    [Theory]
    [InlineData("plumber")]
    [InlineData("BACK-END-DEVELOPER")]
    public async Task An_Unknown_Occupation_Is_Not_Found(string slug)
    {
        var response = await host.CreateClient().GetAsync($"/api/salary-market/occupations/{slug}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_Config_Says_The_Pages_Are_On()
    {
        var config = await host.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", ApiHost.JsonOptions);

        config!.SalaryMarket!.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task With_The_Flag_At_Its_Shipped_Default_Both_Routes_Are_Not_Found_And_The_Config_Says_Off()
    {
        var off = host.Variant("off", builder => builder.UseSetting("SalaryMarket:Enabled", "false")).CreateClient();

        (await off.GetAsync("/api/salary-market/occupations")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await off.GetAsync("/api/salary-market/occupations/back-end-developer")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var config = await off.GetFromJsonAsync<ClientConfigResponse>("/api/config", ApiHost.JsonOptions);
        config!.SalaryMarket!.Enabled.ShouldBeFalse();
    }
}
