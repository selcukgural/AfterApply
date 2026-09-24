using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.ClientConfig;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.CandidateExperiences;

public sealed class CandidateExperiencesDisabledProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("CandidateExperiences:Enabled", "false");
    }
}

/// <summary>CandidateExperiences:Enabled=false — the feature deployed dark. Every route is a 404,
/// the public company page reports zero entries, and /api/config says so. Reviews and salaries
/// are unaffected.</summary>
[Collection(IntegrationTestCollection.Name)]
public class CandidateExperiencesDisabledTests(ApiHost<CandidateExperiencesDisabledProfile> host)
    : IClassFixture<ApiHost<CandidateExperiencesDisabledProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Every_Route_Is_Not_Found_And_Config_Reports_The_Flag()
    {
        var client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory.Services,
            new RegisterRequest("dark.experiences@example.com", "P@ssw0rd123!", "Dark", "Launch", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        // Reviews still work, and give us a real company to aim at.
        var resolve = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest("Dark Experience Co"), JsonOptions);
        resolve.EnsureSuccessStatusCode();
        var company = (await resolve.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;

        var request = new CandidateExperienceRequest(4);
        (await client.GetAsync($"/api/companies/public/{company.Slug}/experiences")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/companies/{company.Id}/experiences/me")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsJsonAsync($"/api/companies/{company.Id}/experiences", request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync("/api/candidate-experiences/mine")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PutAsJsonAsync($"/api/candidate-experiences/{Guid.NewGuid()}", request, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/candidate-experiences/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        // The dashboard's ended-processes card goes dark with the feature it invites to.
        (await client.GetAsync("/api/experience-invites")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.PostAsync($"/api/experience-invites/{company.Id}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var page = await client.GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions);
        page!.CandidateExperienceCount.ShouldBe(0);

        // The admin table is dark with the feature, for an admin too.
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Id == auth.User.Id);
            user.IsAdmin = true;
            // A row the feature left behind before going dark: not a contribution while it is off.
            db.CandidateExperiences.Add(CandidateExperience.Create(auth.User.Id, company.Id,
                new CandidateExperienceContent(4, [], [], [], null, null, null, []), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        (await client.GetAsync("/api/admin/candidate-experiences")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.DeleteAsync($"/api/admin/candidate-experiences/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Neither the directory nor the sitemap lists a company on the strength of a dark
        // feature's row, and the author's merged list shows no experience quota.
        var directory = await client.GetFromJsonAsync<PagedResult<CompanyPublicListItemResponse>>("/api/companies/public?q=dark%20experience", JsonOptions);
        directory!.Items.ShouldBeEmpty();
        var slugs = await client.GetFromJsonAsync<IReadOnlyList<ReviewedCompanySlugResponse>>("/api/companies/public/slugs", JsonOptions);
        slugs!.ShouldNotContain(s => s.Slug == company.Slug);
        var mine = await client.GetFromJsonAsync<MyContributionsResponse>("/api/contributions/mine", JsonOptions);
        mine!.Items.ShouldBeEmpty();
        mine.ExperienceQuota.ShouldBeNull();
        mine.SalaryQuota.ShouldNotBeNull();

        var config = await client.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions);
        config!.CandidateExperiences.ShouldNotBeNull();
        config.CandidateExperiences!.Enabled.ShouldBeFalse();
        config.CompanyReviews!.Enabled.ShouldBeTrue();
        config.CompanySalaries!.Enabled.ShouldBeTrue();
    }
}
