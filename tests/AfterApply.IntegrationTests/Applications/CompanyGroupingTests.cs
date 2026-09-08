using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

/// <summary>
/// The company view (GET /api/applications/grouped) and the company narrowing on the flat list that
/// it links out to. The property under test throughout is that the two views never disagree about
/// which applications exist — only about how they are counted and drawn.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CompanyGroupingTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private HttpClient _otherUserClient = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(CompanyGroupingTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        _client = await SignUpAsync("grouping.owner@example.com");
        _otherUserClient = await SignUpAsync("grouping.stranger@example.com");
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task<HttpClient> SignUpAsync(string email)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Group", "Tester", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateApplicationAsync(HttpClient client, string companyName,
        string jobTitle = "Engineer", int appliedDaysAgo = 1)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, jobTitle, null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-appliedDaysAgo), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task ChangeStatusAsync(Guid applicationId, ApplicationStatus status)
    {
        var response = await _client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(status, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private async Task<GroupedApplicationsResponse> GetGroupedAsync(string query = "")
    {
        var response = await _client.GetAsync($"/api/applications/grouped{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GroupedApplicationsResponse>(JsonOptions))!;
    }

    private async Task<PagedResult<ApplicationSummaryResponse>> GetFlatAsync(string query = "")
    {
        var response = await _client.GetAsync($"/api/applications{query}");
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PagedResult<ApplicationSummaryResponse>>(JsonOptions))!;
    }

    [Fact]
    public async Task Collects_Every_Application_Under_Its_Company()
    {
        await CreateApplicationAsync(_client, "Trendyol", "Senior Backend Developer");
        await CreateApplicationAsync(_client, "Trendyol", "Platform Engineer");
        await CreateApplicationAsync(_client, "Getir", "Backend Developer");

        var grouped = await GetGroupedAsync();

        grouped.TotalCount.ShouldBe(2, "the page is made of companies");
        grouped.TotalApplicationCount.ShouldBe(3, "the selection acts on applications");

        var trendyol = grouped.Items.Single(g => g.CompanyName == "Trendyol");
        trendyol.ApplicationCount.ShouldBe(2);
        trendyol.Applications.Count.ShouldBe(2);
        trendyol.HasMore.ShouldBeFalse();
        trendyol.Applications.Select(a => a.JobTitle)
            .ShouldBe(["Senior Backend Developer", "Platform Engineer"], ignoreOrder: true);
        trendyol.Applications.ShouldAllBe(a => a.CompanyId == trendyol.CompanyId);
    }

    [Fact]
    public async Task Reports_The_Status_Breakdown_Of_Each_Company()
    {
        var rejected = await CreateApplicationAsync(_client, "Trendyol", "Platform Engineer");
        await ChangeStatusAsync(rejected, ApplicationStatus.Rejected);
        await CreateApplicationAsync(_client, "Trendyol", "Senior Backend Developer");
        await CreateApplicationAsync(_client, "Trendyol", ".NET Developer");

        var trendyol = (await GetGroupedAsync()).Items.Single();

        trendyol.StatusCounts.Sum(s => s.Count).ShouldBe(3);
        trendyol.StatusCounts.Single(s => s.Status == ApplicationStatus.Applied).Count.ShouldBe(2);
        trendyol.StatusCounts.Single(s => s.Status == ApplicationStatus.Rejected).Count.ShouldBe(1);
        // A status the company holds none of is left out rather than sent as a zero — an empty
        // segment is something the reader has to decode.
        trendyol.StatusCounts.ShouldAllBe(s => s.Count > 0);
    }

    [Fact]
    public async Task Pages_Over_Companies_So_A_Company_Is_Never_Split_Across_Pages()
    {
        // The whole point of the view: three applications at one company on a page of size one still
        // arrive together, which the flat list cannot promise.
        await CreateApplicationAsync(_client, "Trendyol", "One");
        await CreateApplicationAsync(_client, "Trendyol", "Two");
        await CreateApplicationAsync(_client, "Trendyol", "Three");
        await CreateApplicationAsync(_client, "Getir", "Four");

        var firstPage = await GetGroupedAsync("?page=1&pageSize=1&sortBy=ApplicationCount");

        firstPage.Items.Count.ShouldBe(1);
        firstPage.TotalCount.ShouldBe(2, "two companies match");
        firstPage.TotalApplicationCount.ShouldBe(4, "four applications match");
        firstPage.Items.Single().CompanyName.ShouldBe("Trendyol");
        firstPage.Items.Single().Applications.Count.ShouldBe(3);

        var secondPage = await GetGroupedAsync("?page=2&pageSize=1&sortBy=ApplicationCount");
        secondPage.Items.Single().CompanyName.ShouldBe("Getir");
    }

    [Fact]
    public async Task Orders_Groups_By_Company_Name_And_By_Application_Count()
    {
        await CreateApplicationAsync(_client, "Zed Yazılım", "One");
        await CreateApplicationAsync(_client, "Alfa Teknoloji", "Two");
        await CreateApplicationAsync(_client, "Alfa Teknoloji", "Three");

        var byName = await GetGroupedAsync("?sortBy=CompanyName&sortDirection=Ascending");
        byName.Items.Select(g => g.CompanyName).ShouldBe(["Alfa Teknoloji", "Zed Yazılım"]);

        var byCount = await GetGroupedAsync("?sortBy=ApplicationCount&sortDirection=Descending");
        byCount.Items.Select(g => g.CompanyName).ShouldBe(["Alfa Teknoloji", "Zed Yazılım"]);
        byCount.Items.First().ApplicationCount.ShouldBe(2);
    }

    [Fact]
    public async Task Status_Filter_Narrows_The_Rows_And_Drops_Companies_With_Nothing_Left()
    {
        var rejected = await CreateApplicationAsync(_client, "Trendyol", "Platform Engineer");
        await ChangeStatusAsync(rejected, ApplicationStatus.Rejected);
        await CreateApplicationAsync(_client, "Trendyol", "Senior Backend Developer");
        await CreateApplicationAsync(_client, "Getir", "Backend Developer");

        var grouped = await GetGroupedAsync("?status=Rejected");

        grouped.TotalCount.ShouldBe(1, "only one company still has a matching application");
        grouped.TotalApplicationCount.ShouldBe(1);
        var trendyol = grouped.Items.Single();
        trendyol.CompanyName.ShouldBe("Trendyol");
        // The count and the breakdown describe the filtered set, not the company's whole history —
        // otherwise the header would contradict the rows drawn under it.
        trendyol.ApplicationCount.ShouldBe(1);
        trendyol.Applications.Single().Id.ShouldBe(rejected);
    }

    [Fact]
    public async Task Search_Matches_The_Same_Applications_The_Flat_List_Would_Return()
    {
        await CreateApplicationAsync(_client, "Searchable Robotics", "Firmware Engineer");
        await CreateApplicationAsync(_client, "Other Co", "Backend Engineer");

        var grouped = await GetGroupedAsync("?search=Searchable");
        var flat = await GetFlatAsync("?search=Searchable");

        grouped.TotalApplicationCount.ShouldBe(flat.TotalCount);
        grouped.Items.Single().CompanyName.ShouldBe("Searchable Robotics");
    }

    [Fact]
    public async Task Caps_A_Long_Company_And_Says_So_Rather_Than_Sending_Everything()
    {
        const int total = 23;
        for (var i = 1; i <= total; i++)
        {
            await CreateApplicationAsync(_client, "Trendyol", $"Engineer {i}");
        }

        var group = (await GetGroupedAsync()).Items.Single();

        group.ApplicationCount.ShouldBe(total, "the header states what the company really holds");
        group.Applications.Count.ShouldBe(20, "the response carries at most the cap");
        group.HasMore.ShouldBeTrue();

        // ...and the rest are reachable, paged, through the flat list narrowed to the company.
        var remaining = await GetFlatAsync($"?companyId={group.CompanyId}&pageSize=100");
        remaining.TotalCount.ShouldBe(total);
    }

    [Fact]
    public async Task Company_Filter_On_The_Flat_List_Returns_Only_That_Company()
    {
        await CreateApplicationAsync(_client, "Trendyol", "One");
        await CreateApplicationAsync(_client, "Trendyol", "Two");
        await CreateApplicationAsync(_client, "Getir", "Three");

        var grouped = await GetGroupedAsync();
        var trendyolId = grouped.Items.Single(g => g.CompanyName == "Trendyol").CompanyId;

        var filtered = await GetFlatAsync($"?companyId={trendyolId}");

        filtered.TotalCount.ShouldBe(2);
        filtered.Items.ShouldAllBe(i => i.CompanyName == "Trendyol");
    }

    [Fact]
    public async Task All_Matching_Bulk_Selection_Stays_Inside_The_Company_The_User_Was_Looking_At()
    {
        // The company narrowing has to travel with the selection. Without it, "delete everything
        // matching" on a company-filtered screen would resolve to every company's rows.
        await CreateApplicationAsync(_client, "Trendyol", "One");
        await CreateApplicationAsync(_client, "Trendyol", "Two");
        var untouched = await CreateApplicationAsync(_client, "Getir", "Three");

        var trendyolId = (await GetGroupedAsync()).Items.Single(g => g.CompanyName == "Trendyol").CompanyId;

        var response = await _client.PostAsJsonAsync("/api/applications/bulk/delete", new BulkDeleteRequest(
            new BulkSelection(AllMatching: new BulkFilterSelection(CompanyId: trendyolId)), ExpectedCount: 2),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var deleted = await response.Content.ReadFromJsonAsync<BulkDeleteResponse>(JsonOptions);

        deleted!.Deleted.ShouldBe(2);
        var left = await GetFlatAsync();
        left.TotalCount.ShouldBe(1);
        left.Items.Single().Id.ShouldBe(untouched);
    }

    [Fact]
    public async Task Never_Shows_Another_User_A_Company_Of_Mine()
    {
        await CreateApplicationAsync(_client, "Trendyol", "Mine");
        await CreateApplicationAsync(_otherUserClient, "Getir", "Theirs");

        var mine = await GetGroupedAsync();
        mine.Items.Select(g => g.CompanyName).ShouldBe(["Trendyol"]);
        mine.TotalApplicationCount.ShouldBe(1);

        var theirsResponse = await _otherUserClient.GetAsync("/api/applications/grouped");
        theirsResponse.EnsureSuccessStatusCode();
        var theirs = await theirsResponse.Content.ReadFromJsonAsync<GroupedApplicationsResponse>(JsonOptions);
        theirs!.Items.Select(g => g.CompanyName).ShouldBe(["Getir"]);
    }

    [Fact]
    public async Task Refuses_A_Sort_The_Company_View_Does_Not_Have()
    {
        // The flat list's vocabulary reaching this endpoint is a client bug, and the web app guards
        // against it by validating the URL — but the endpoint says no rather than quietly ordering
        // by something else. A 400, not a 500: an unreadable query string is the caller's mistake,
        // and anyone able to edit a URL could otherwise raise a server fault (fixed 2026-09-08 in
        // DomainExceptionHandler; the already-shipped list endpoint had the same hole).
        var response = await _client.GetAsync("/api/applications/grouped?sortBy=JobTitle");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("/api/applications?sortBy=Nonsense")]
    [InlineData("/api/applications?status=Nonsense")]
    [InlineData("/api/applications?companyId=not-a-guid")]
    [InlineData("/api/applications/grouped?status=Nonsense")]
    public async Task Answers_An_Unreadable_Query_String_As_A_Client_Error(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Returns_An_Empty_Page_Rather_Than_Failing_When_Nothing_Matches()
    {
        await CreateApplicationAsync(_client, "Trendyol", "One");

        var grouped = await GetGroupedAsync("?search=nothing-matches-this");

        grouped.Items.ShouldBeEmpty();
        grouped.TotalCount.ShouldBe(0);
        grouped.TotalApplicationCount.ShouldBe(0);
    }
}
