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
using AfterApply.Application.Applications.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
            50_000m, SalaryCurrency.TRY, false, PeriodStartYear: 2024);
        (await client.GetAsync($"/api/companies/{company.Id}/salaries")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/companies/{company.Id}/salaries/me")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/api/companies/{company.Id}/salaries", request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/company-salaries/mine")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/api/company-salaries/{Guid.NewGuid()}", request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/company-salaries/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var page = await client.GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions);
        page!.SalaryCount.ShouldBe(0);

        // The admin table is dark with the feature, for an admin too.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == auth.User.Id);
            user.IsAdmin = true;
            // A row the feature left behind before going dark: not a contribution while it is off.
            db.CompanySalaryEntries.Add(CompanySalaryEntry.Create(auth.User.Id, company.Id,
                new SalaryContent(Occupation.IdFor("2512"), 3, EmploymentType.FullTime, SalaryEmploymentStatus.CurrentEmployee,
                    50_000m, SalaryCurrency.TRY, null, DateTimeOffset.UtcNow.Year, null), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        (await client.GetAsync("/api/admin/company-salaries")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/admin/company-salaries/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The directory does not list a company on the strength of a dark feature's row, and the
        // author's merged list neither shows the row nor a salary quota.
        var directory = await client.GetFromJsonAsync<PagedResult<CompanyPublicListItemResponse>>("/api/companies/public?q=dark%20salary", JsonOptions);
        directory!.Items.ShouldBeEmpty();
        var mine = await client.GetFromJsonAsync<MyContributionsResponse>("/api/contributions/mine", JsonOptions);
        mine!.Items.ShouldBeEmpty();
        mine.SalaryQuota.ShouldBeNull();
        mine.ExperienceQuota.ShouldNotBeNull();

        // The catalogue is generic and stays reachable while salaries are dark.
        (await client.GetAsync("/api/occupations/search?q=soft")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var config = await client.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        config!.CompanySalaries.ShouldNotBeNull();
        config.CompanySalaries!.Enabled.ShouldBeFalse();
        config.CompanyReviews!.Enabled.ShouldBeTrue();
    }
}
