using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanyReviews;

/// <summary>
/// The directory search's second section (2026-09-23): companies that have a page but no
/// contribution, shown to a stranger only once enough different people applied to them. The
/// shipped floor (three people) is kept, so every test builds its applicants through the API.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class KnownCompanySearchTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    private WebApplicationFactory<Program> _reviewsOffFactory =>
        host.Variant("reviews-off", builder => builder.UseSetting("CompanyReviews:Enabled", "false"));

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Known", "Test", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> ApplyAsync(HttpClient client, string companyName)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-10), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!.CompanyId;
    }

    private async Task<List<HttpClient>> ApplicantsAsync(int count, string prefix)
    {
        var clients = new List<HttpClient>();
        for (var i = 0; i < count; i++)
        {
            clients.Add(await RegisterAsync($"{prefix}.{i}@example.com"));
        }
        return clients;
    }

    private async Task<HttpResponseMessage> SearchAsync(string? q, WebApplicationFactory<Program>? factory = null)
    {
        // Never an Authorization header: the section is for strangers.
        var client = (factory ?? _factory).CreateClient();
        return await client.GetAsync(q is null ? "/api/companies/known" : $"/api/companies/known?q={Uri.EscapeDataString(q)}");
    }

    private async Task<List<KnownCompanyResponse>> KnownAsync(string? q)
    {
        var response = await SearchAsync(q);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<KnownCompanyResponse>>(JsonOptions))!;
    }

    [Fact]
    public async Task A_Company_Three_People_Applied_To_Is_Found_By_A_Stranger_With_Name_And_Slug_Only()
    {
        foreach (var applicant in await ApplicantsAsync(3, "known.three"))
        {
            await ApplyAsync(applicant, "Üçlü Teknoloji");
        }

        var found = (await KnownAsync("üçlü")).ShouldHaveSingleItem();
        found.Name.ShouldBe("Üçlü Teknoloji");
        found.Slug.ShouldNotBeNullOrWhiteSpace();

        // Nothing beyond the two fields leaves: no count, no id.
        var raw = await (await SearchAsync("üçlü")).Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(raw);
        document.RootElement[0].EnumerateObject().Select(p => p.Name).ShouldBe(["slug", "name"], ignoreOrder: true);
    }

    [Fact]
    public async Task Two_People_Are_Not_Enough_However_Many_Applications_They_Sent()
    {
        var applicants = await ApplicantsAsync(2, "known.two");
        for (var i = 0; i < 5; i++)
        {
            await ApplyAsync(applicants[0], "İkili Yazılım");
        }
        await ApplyAsync(applicants[1], "İkili Yazılım");

        (await KnownAsync("ikili")).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Company_With_A_Contribution_Belongs_To_The_Directory_Not_This_Section()
    {
        var applicants = await ApplicantsAsync(3, "known.contributed");
        Guid companyId = default;
        foreach (var applicant in applicants)
        {
            companyId = await ApplyAsync(applicant, "Katkılı Holding");
        }

        var review = new CreateCompanyReviewRequest(EmploymentStatus.FormerEmployee, 4,
            [new ReviewCategoryRatingDto(ReviewCategory.WorkEnvironment, 5)],
            ["environment.pos.team_communication"], ["pay.imp.salary_level"]);
        (await applicants[0].PostAsJsonAsync($"/api/companies/{companyId}/reviews", review, JsonOptions)).StatusCode
            .ShouldBe(HttpStatusCode.Created);

        (await KnownAsync("katkılı")).ShouldBeEmpty();
        var directory = await _factory.CreateClient()
            .GetFromJsonAsync<PagedResultProbe>("/api/companies/public?q=katk%C4%B1l%C4%B1", JsonOptions);
        directory!.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task A_Short_Or_Missing_Query_Returns_Nothing()
    {
        foreach (var applicant in await ApplicantsAsync(3, "known.short"))
        {
            await ApplyAsync(applicant, "Ab Teknoloji");
        }

        (await KnownAsync(null)).ShouldBeEmpty();
        (await KnownAsync("a")).ShouldBeEmpty();
        (await KnownAsync("ab")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task At_Most_Six_Are_Returned_In_Name_Order()
    {
        var applicants = await ApplicantsAsync(3, "known.limit");
        for (var i = 1; i <= 7; i++)
        {
            foreach (var applicant in applicants)
            {
                await ApplyAsync(applicant, $"Sınır Şirketi {i}");
            }
        }

        var found = await KnownAsync("sınır");
        found.Count.ShouldBe(6);
        found.Select(f => f.Name).ShouldBe(found.Select(f => f.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task With_Company_Reviews_Off_The_Route_Is_Not_Found()
    {
        (await SearchAsync("herhangi", _reviewsOffFactory)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed record PagedResultProbe(int TotalCount);
}
