using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Occupations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanySalaries;

public sealed class SalaryPositionProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("CompanySalaries:MinimumEntriesForStats", "3");
        builder.UseSetting("CompanySalaries:PersonalBandMinimumEntries", "5");
        builder.UseSetting("CompanySalaries:CurrentWindowYears", "2");
    }
}

/// <summary>
/// "Where you sit in the band" (contribution loop #9, 2026-09-24): the author's own entry against
/// the company's current band in its currency — hidden below its own, higher threshold, made of
/// the same rows as the company page's band, and nobody else's to read.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SalaryPositionTests(ApiHost<SalaryPositionProfile> host) : IClassFixture<ApiHost<SalaryPositionProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;
    private static readonly int ThisYear = DateTimeOffset.UtcNow.Year;
    private static readonly Guid SoftwareDevelopers = Occupation.IdFor("2512");
    private static readonly Guid SystemsAnalysts = Occupation.IdFor("2511");

    private WebApplicationFactory<Program> _factory => host;

    private int _people;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string? email = null)
    {
        var client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory.Services,
            new RegisterRequest(email ?? $"band.person{++_people}@example.com", "P@ssw0rd123!", "Band", "Tester", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> CompanyAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!.Id;
    }

    private static CompanySalaryRequest Salary(decimal amount, SalaryCurrency currency = SalaryCurrency.TRY, Guid? occupation = null,
        int? periodStart = null, int? periodEnd = null, SalaryEmploymentStatus status = SalaryEmploymentStatus.CurrentEmployee) =>
        new(occupation ?? SoftwareDevelopers, 5, EmploymentType.FullTime, status, amount, currency, false, null,
            periodStart ?? ThisYear - 1, periodEnd);

    private static async Task<MyCompanySalaryResponse> ShareAsync(HttpClient client, Guid companyId, CompanySalaryRequest request)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/salaries", request, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCompanySalaryResponse>(JsonOptions))!;
    }

    /// <summary>Other people's current TRY salaries at the company, one account each.</summary>
    private async Task OthersAsync(Guid companyId, params decimal[] amounts)
    {
        foreach (var amount in amounts)
        {
            await ShareAsync(await RegisterAsync(), companyId, Salary(amount));
        }
    }

    private static async Task<SalaryPositionResponse> PositionAsync(HttpClient client, Guid entryId) =>
        (await client.GetFromJsonAsync<SalaryPositionResponse>($"/api/company-salaries/{entryId}/position", JsonOptions))!;

    [Fact]
    public async Task Below_The_Personal_Threshold_Only_The_Count_Comes_Back()
    {
        var me = await RegisterAsync();
        var companyId = await CompanyAsync(me, "Small Band Co");
        await OthersAsync(companyId, 80_000m, 90_000m, 100_000m);
        var mine = await ShareAsync(me, companyId, Salary(95_000m));

        // Four rows: enough for the company page's band (3), not for a personal position (5) —
        // at that size the range and the median would hand over the others' amounts.
        var position = await PositionAsync(me, mine.Id);
        position.Count.ShouldBe(4);
        position.MinimumEntries.ShouldBe(5);
        position.IncludesOwn.ShouldBeTrue();
        position.MedianMonthlyNet.ShouldBeNull();
        position.MinMonthlyNet.ShouldBeNull();
        position.MaxMonthlyNet.ShouldBeNull();
        position.PercentFromMedian.ShouldBeNull();
        position.CompanyName.ShouldBe("Small Band Co");
        position.MonthlyNetAmount.ShouldBe(95_000m);
    }

    [Fact]
    public async Task At_The_Threshold_The_Band_And_The_Distance_From_The_Median_Appear()
    {
        var me = await RegisterAsync();
        var companyId = await CompanyAsync(me, "Band Co");
        await OthersAsync(companyId, 62_000m, 80_000m, 88_000m, 140_000m);
        var mine = await ShareAsync(me, companyId, Salary(95_000m));

        var position = await PositionAsync(me, mine.Id);
        position.Count.ShouldBe(5);
        position.Currency.ShouldBe(SalaryCurrency.TRY);
        position.MedianMonthlyNet.ShouldBe(88_000m);
        position.MinMonthlyNet.ShouldBe(62_000m);
        position.MaxMonthlyNet.ShouldBe(140_000m);
        position.PercentFromMedian.ShouldBe(8); // 95 000 is 7.95 % above 88 000
        position.WindowYears.ShouldBe(2);
    }

    [Fact]
    public async Task The_Band_Is_The_Company_Pages_Rows_Every_Occupation_This_Currency_Current_Only()
    {
        var me = await RegisterAsync();
        var companyId = await CompanyAsync(me, "Mixed Band Co");
        await OthersAsync(companyId, 70_000m, 80_000m, 90_000m);
        // Another occupation counts (the band is company-wide) …
        await ShareAsync(await RegisterAsync(), companyId, Salary(100_000m, occupation: SystemsAnalysts));
        // … another currency and a salary that ended years ago do not.
        await ShareAsync(await RegisterAsync(), companyId, Salary(5_000m, SalaryCurrency.EUR));
        await ShareAsync(await RegisterAsync(), companyId, Salary(10_000m, periodStart: ThisYear - 8, periodEnd: ThisYear - 6,
            status: SalaryEmploymentStatus.FormerEmployee));
        var mine = await ShareAsync(me, companyId, Salary(110_000m));

        var position = await PositionAsync(me, mine.Id);
        position.Count.ShouldBe(5);
        position.MinMonthlyNet.ShouldBe(70_000m);
        position.MaxMonthlyNet.ShouldBe(110_000m);
        position.MedianMonthlyNet.ShouldBe(90_000m);
        position.PercentFromMedian.ShouldBe(22);
    }

    [Fact]
    public async Task An_Old_Own_Salary_Is_Compared_With_The_Current_Band_It_Is_Not_Part_Of()
    {
        var me = await RegisterAsync();
        var companyId = await CompanyAsync(me, "Former Band Co");
        await OthersAsync(companyId, 60_000m, 70_000m, 80_000m, 90_000m, 100_000m);
        var mine = await ShareAsync(me, companyId, Salary(40_000m, periodStart: ThisYear - 9, periodEnd: ThisYear - 7,
            status: SalaryEmploymentStatus.FormerEmployee));

        var position = await PositionAsync(me, mine.Id);
        position.IncludesOwn.ShouldBeFalse();
        position.Count.ShouldBe(5);
        position.MedianMonthlyNet.ShouldBe(80_000m);
        position.PercentFromMedian.ShouldBe(-50);
    }

    [Fact]
    public async Task Only_The_Author_Can_Read_An_Entrys_Position()
    {
        var me = await RegisterAsync();
        var companyId = await CompanyAsync(me, "Private Band Co");
        var mine = await ShareAsync(me, companyId, Salary(95_000m));

        var stranger = await RegisterAsync();
        (await stranger.GetAsync($"/api/company-salaries/{mine.Id}/position")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await me.GetAsync($"/api/company-salaries/{Guid.NewGuid()}/position")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _factory.CreateClient().GetAsync($"/api/company-salaries/{mine.Id}/position")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
