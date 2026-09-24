using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.ResponseRates.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Benchmark;
using AfterApply.Domain.Common;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.ResponseRates;

/// <summary>Thresholds lowered so a handful of seeded accounts can cross them; the cache is one
/// second so a test sees what it just seeded. The flag stays at its shipped default (on).</summary>
public sealed class SectorResponseRatesProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("ResponseRates:MinimumContributors", "2");
        builder.UseSetting("ResponseRates:MinimumApplications", "3");
        builder.UseSetting("ResponseRates:CacheSeconds", "1");
    }
}

[Collection(IntegrationTestCollection.Name)]
public class SectorResponseRatesTests(ApiHost<SectorResponseRatesProfile> host)
    : IClassFixture<ApiHost<SectorResponseRatesProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;
    private static readonly DateTimeOffset Old = DateTimeOffset.UtcNow.AddDays(-45);

    // Three accounts, because the share guard (half of a sector) means two people can only ever
    // clear it with an even split — a third makes the seeds read naturally.
    private HttpClient _userA = null!;
    private HttpClient _userB = null!;
    private HttpClient _userC = null!;
    private HttpClient _anonymous = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        (_userA, _) = await host.RegisterAsync("rr.a@example.com");
        (_userB, _) = await host.RegisterAsync("rr.b@example.com");
        (_userC, _) = await host.RegisterAsync("rr.c@example.com");
        _anonymous = host.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<(Guid ApplicationId, Guid CompanyId)> ApplyAsync(HttpClient client, string company, DateTimeOffset appliedAt)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            company, "Engineer", null, null, EmploymentType.FullTime, appliedAt, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return (created!.Id, created.CompanyId);
    }

    private static async Task ChangeStatusAsync(HttpClient client, Guid applicationId, ApplicationStatus status, DateTimeOffset at)
    {
        var response = await client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, at), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task SetIndustryAsync(Guid companyId, string industry)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = await db.Companies.SingleAsync(c => c.Id == companyId);
        company.EnrichFrom(Source.LinkedIn, null, industry, null, DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    private async Task<SectorResponseRatesResponse> GetTableAsync(string query = "")
    {
        // The service caches for a second; wait it out so the table reflects what was just seeded.
        await Task.Delay(1100);
        var response = await _anonymous.GetAsync($"/api/response-rates/sectors{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.Public.ShouldBeTrue();
        return (await response.Content.ReadFromJsonAsync<SectorResponseRatesResponse>(JsonOptions))!;
    }

    private static SectorResponseRateRow Row(SectorResponseRatesResponse table, BenchmarkSector sector) =>
        table.Sectors.Single(r => r.Sector == sector);

    [Fact]
    public async Task A_Sector_Above_The_Threshold_Is_A_Row_With_Figures_And_The_Rest_Are_Names_Only()
    {
        var (app1, software) = await ApplyAsync(_userA, "Sector Soft Co", Old);
        await ChangeStatusAsync(_userA, app1, ApplicationStatus.Rejected, Old.AddDays(4));
        var (app2, _) = await ApplyAsync(_userB, "Sector Soft Co", Old);
        await ChangeStatusAsync(_userB, app2, ApplicationStatus.Interview, Old.AddDays(8));
        await ApplyAsync(_userC, "Sector Soft Co", Old);
        await SetIndustryAsync(software, "Software Development");

        var table = await GetTableAsync();

        table.Period.ShouldBe(BenchmarkPeriod.LastTwelveMonths);
        table.Sectors.Count.ShouldBe(Enum.GetValues<BenchmarkSector>().Length - 1); // every sector but Other
        table.Sectors.ShouldNotContain(r => r.Sector == BenchmarkSector.Other);
        table.Thresholds.MinimumContributors.ShouldBe(2);
        table.Thresholds.MinimumApplications.ShouldBe(3);
        table.Thresholds.MaturityDays.ShouldBe(30);

        var row = Row(table, BenchmarkSector.SoftwareAndIt);
        row.Figures.ShouldNotBeNull();
        row.Figures!.Applications.ShouldBe(3);
        row.Figures.Contributors.ShouldBe(3);
        row.Figures.ResponseRate.ShouldBe(66.7);
        row.Figures.MedianFirstReplyDays.ShouldBe(6.0);
        row.Figures.InterviewRate.ShouldBe(33.3);

        // Nothing was seeded in finance: a row by name, and nothing else about it.
        Row(table, BenchmarkSector.FinanceAndInsurance).Figures.ShouldBeNull();
    }

    [Fact]
    public async Task Too_Few_Contributors_Leaves_The_Row_Empty_Even_With_Enough_Applications()
    {
        var (_, bank) = await ApplyAsync(_userA, "Lonely Bank", Old);
        await ApplyAsync(_userA, "Lonely Bank", Old);
        await ApplyAsync(_userA, "Lonely Bank", Old);
        await SetIndustryAsync(bank, "Banking");

        var table = await GetTableAsync();

        // Three applications clear the count floor; one person does not clear the people floor,
        // and the row must not even say how many applications it holds.
        Row(table, BenchmarkSector.FinanceAndInsurance).Figures.ShouldBeNull();
    }

    [Fact]
    public async Task One_Person_Being_Most_Of_A_Sector_Hides_It()
    {
        // Two contributors and four applications clear both floors, but three of the four are one
        // person's: 0.75 is over the sector's half.
        var (_, telco) = await ApplyAsync(_userA, "Dominant Telco", Old);
        await ApplyAsync(_userA, "Dominant Telco", Old);
        await ApplyAsync(_userA, "Dominant Telco", Old);
        await ApplyAsync(_userB, "Dominant Telco", Old);
        await SetIndustryAsync(telco, "Telecommunications");

        var table = await GetTableAsync();

        Row(table, BenchmarkSector.Telecom).Figures.ShouldBeNull();
    }

    [Fact]
    public async Task Companies_Without_A_Readable_Industry_Are_Counted_But_In_No_Row()
    {
        await ApplyAsync(_userA, "Mystery Holding", Old);
        await ApplyAsync(_userB, "Mystery Holding", Old);

        var table = await GetTableAsync();

        table.UnclassifiedApplications.ShouldBe(2);
        table.Sectors.ShouldAllBe(r => r.Figures == null);
    }

    [Fact]
    public async Task The_Three_Month_Window_Leaves_Out_Older_Applications()
    {
        var (_, retail) = await ApplyAsync(_userA, "Window Retail", Old);
        await ApplyAsync(_userB, "Window Retail", Old);
        await ApplyAsync(_userC, "Window Retail", Old);
        await ApplyAsync(_userA, "Window Retail", DateTimeOffset.UtcNow.AddMonths(-5));
        await SetIndustryAsync(retail, "Retail");

        var twelve = await GetTableAsync("?period=LastTwelveMonths");
        var three = await GetTableAsync("?period=LastThreeMonths");

        Row(twelve, BenchmarkSector.EcommerceAndRetail).Figures!.Applications.ShouldBe(4);
        Row(three, BenchmarkSector.EcommerceAndRetail).Figures!.Applications.ShouldBe(3);
        three.Period.ShouldBe(BenchmarkPeriod.LastThreeMonths);
        (three.WindowEnd - three.WindowStart).TotalDays.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task The_Open_Ended_Period_Is_Refused()
    {
        var response = await _anonymous.GetAsync("/api/response-rates/sectors?period=Longer");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Young_Applications_Are_In_The_Count_But_Not_In_The_Rates()
    {
        var (app1, health) = await ApplyAsync(_userA, "Young Pharma", Old);
        await ChangeStatusAsync(_userA, app1, ApplicationStatus.Screening, Old.AddDays(3));
        var (app2, _) = await ApplyAsync(_userB, "Young Pharma", Old);
        await ChangeStatusAsync(_userB, app2, ApplicationStatus.Rejected, Old.AddDays(5));
        await ApplyAsync(_userC, "Young Pharma", DateTimeOffset.UtcNow.AddDays(-2));
        await SetIndustryAsync(health, "Pharmaceutical Manufacturing");

        var table = await GetTableAsync();

        var row = Row(table, BenchmarkSector.HealthAndPharma).Figures;
        row.ShouldNotBeNull();
        row!.Applications.ShouldBe(3);
        row.ResponseRate.ShouldBe(100.0);
    }

    [Fact]
    public async Task Flag_Off_Is_Not_Found()
    {
        var off = host.Variant("off", builder => builder.UseSetting("ResponseRates:Enabled", "false"));

        var response = await off.CreateClient().GetAsync("/api/response-rates/sectors");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
    private static async Task PromiseThenRejectAsync(HttpClient client, Guid applicationId, int answeredAfterDays,
        RejectionNotice notice)
    {
        // Promised in the Applied stage for ten days after applying; answered on the given day.
        var promise = await client.PutAsJsonAsync($"/api/applications/{applicationId}/reply-promise",
            new SetReplyPromiseRequest(DateOnly.FromDateTime(Old.AddDays(10).UtcDateTime)), JsonOptions);
        promise.EnsureSuccessStatusCode();
        var status = await client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(ApplicationStatus.Rejected, null, Old.AddDays(answeredAfterDays), null, notice), JsonOptions);
        status.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Promise_Keeping_And_Rejection_Notice_Show_Once_Five_Answers_From_Three_People_Are_In()
    {
        var (a1, company) = await ApplyAsync(_userA, "Promise Sector Co", Old);
        var (a2, _) = await ApplyAsync(_userA, "Promise Sector Co", Old);
        var (b1, _) = await ApplyAsync(_userB, "Promise Sector Co", Old);
        var (b2, _) = await ApplyAsync(_userB, "Promise Sector Co", Old);
        var (c1, _) = await ApplyAsync(_userC, "Promise Sector Co", Old);
        await SetIndustryAsync(company, "Software Development");

        await PromiseThenRejectAsync(_userA, a1, 5, RejectionNotice.CompanyNotified);
        await PromiseThenRejectAsync(_userA, a2, 9, RejectionNotice.CompanyNotified);
        await PromiseThenRejectAsync(_userB, b1, 20, RejectionNotice.SeenOnPortal);
        await PromiseThenRejectAsync(_userB, b2, 12, RejectionNotice.CompanyNotified);

        // Four answers: the row is open, the two new rates are not.
        var four = Row(await GetTableAsync(), BenchmarkSector.SoftwareAndIt).Figures!;
        four.PromiseKeptRate.ShouldBeNull();
        four.RejectionNoticeRate.ShouldBeNull();

        await PromiseThenRejectAsync(_userC, c1, 25, RejectionNotice.OtherOrInferred);

        var five = Row(await GetTableAsync(), BenchmarkSector.SoftwareAndIt).Figures!;
        five.PromiseKeptRate.ShouldBe(60.0);   // days 5, 9, 12 kept (grace to 12); 20 and 25 late
        five.RejectionNoticeRate.ShouldBe(60.0);
    }
}
