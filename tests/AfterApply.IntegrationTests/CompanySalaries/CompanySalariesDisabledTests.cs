using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.ClientConfig;
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

public sealed class CompanySalariesDisabledProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("CompanySalaries:Enabled", "false");
    }
}

/// <summary>CompanySalaries:Enabled=false — the feature deployed dark. Every salary route is a 404,
/// the public company page reports zero entries, and /api/config says so. Reviews are unaffected.</summary>
[Collection(IntegrationTestCollection.Name)]
public class CompanySalariesDisabledTests(ApiHost<CompanySalariesDisabledProfile> host) : IClassFixture<ApiHost<CompanySalariesDisabledProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Every_Route_Is_Not_Found_And_Config_Reports_The_Flag()
    {
        var client = _factory!.CreateClient();
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("dark.salaries@example.com", "P@ssw0rd123!", "Dark", "Launch", true), JsonOptions);
        register.EnsureSuccessStatusCode();
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Reviews still work, and give us a real company to aim at.
        var resolve = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest("Dark Salary Co"), JsonOptions);
        resolve.EnsureSuccessStatusCode();
        var company = (await resolve.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;

        var request = new CompanySalaryRequest(Occupation.IdFor("2512"), 3, EmploymentType.FullTime, SalaryEmploymentStatus.CurrentEmployee,
            50_000m, SalaryCurrency.TRY, false);
        (await client.GetAsync($"/api/companies/{company.Id}/salaries")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/companies/{company.Id}/salaries/me")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/api/companies/{company.Id}/salaries", request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/company-salaries/mine")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/api/company-salaries/{Guid.NewGuid()}", request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/company-salaries/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var page = await client.GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions);
        page!.SalaryCount.ShouldBe(0);

        // The catalogue is generic and stays reachable while salaries are dark.
        (await client.GetAsync("/api/occupations/search?q=soft")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var config = await client.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        config!.CompanySalaries.ShouldNotBeNull();
        config.CompanySalaries!.Enabled.ShouldBeFalse();
        config.CompanyReviews!.Enabled.ShouldBeTrue();
    }
}
