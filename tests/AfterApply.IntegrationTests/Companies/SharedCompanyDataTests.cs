using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Companies.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.SilenceReports;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Domain.SilenceReports;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.Companies;

/// <summary>
/// What one user's data may say to others through the shared company and job rows (2026-09-24).
/// A company's existence says "somebody here applied there", so it has a public page and can be
/// found by name only once it is listed; a job's description as one user's capture read it is
/// that user's alone.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SharedCompanyDataTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Company_One_Person_Applied_To_Has_No_Public_Page_Until_Three_Have()
    {
        var (first, _) = await host.RegisterAsync("first@example.com");
        var slug = await ApplyAsync(first, "Initech Yazılım");
        using var anonymous = host.CreateClient();

        (await anonymous.GetAsync($"/api/companies/public/{slug}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"/api/companies/public/{slug}/experiences")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"/api/companies/public/{slug}/reviews")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        foreach (var email in new[] { "second@example.com", "third@example.com" })
        {
            var (client, _) = await host.RegisterAsync(email);
            await ApplyAsync(client, "Initech Yazılım");
        }

        (await anonymous.GetAsync($"/api/companies/public/{slug}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_Unlisted_Company_Takes_No_Anonymous_Silence_Report()
    {
        var (owner, _) = await host.RegisterAsync("owner@example.com");
        var slug = await ApplyAsync(owner, "Quiet Firm");

        var response = await host.CreateClient().PostAsJsonAsync($"/api/companies/public/{slug}/silence-reports",
            new SubmitSilenceReportRequest(SilenceStage.AfterApplication, SilenceWait.OneToTwoMonths, false, "tr", null), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Search_Finds_A_Company_Only_For_Those_Who_May_Know_It_Exists()
    {
        var (applicant, _) = await host.RegisterAsync("applicant@example.com");
        var (stranger, _) = await host.RegisterAsync("stranger@example.com");
        var slug = await ApplyAsync(applicant, "Umbrella Kozmetik");

        (await SearchAsync(applicant, "Umbrella")).ShouldContain(c => c.Name == "Umbrella Kozmetik");
        (await SearchAsync(stranger, "Umbrella")).ShouldBeEmpty();

        (await applicant.GetAsync($"/api/companies/by-slug/{slug}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await stranger.GetAsync($"/api/companies/by-slug/{slug}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Typed_Percent_Sign_Is_Not_A_Wildcard()
    {
        var (applicant, _) = await host.RegisterAsync("wildcard@example.com");
        await ApplyAsync(applicant, "Stark Endüstri");

        (await SearchAsync(applicant, "%%")).ShouldBeEmpty();
    }

    // The same posting is one shared Job row; the description each person's capture read is theirs.
    [Fact]
    public async Task Each_Capture_Of_The_Same_Posting_Keeps_Its_Own_Description()
    {
        const string jobUrl = "https://www.linkedin.com/jobs/view/9100000001/";
        var (first, _) = await host.RegisterAsync("first.capture@example.com");
        var (second, _) = await host.RegisterAsync("second.capture@example.com");
        var (third, _) = await host.RegisterAsync("third.capture@example.com");

        var a = await CaptureAsync(first, jobUrl, "<p>Apply via WhatsApp only</p>");
        var b = await CaptureAsync(second, jobUrl, "<p>The real description</p>");
        var c = await CaptureAsync(third, jobUrl, descriptionHtml: null);

        var jobIds = await host.WithDbAsync(db => db.Applications
            .Where(x => x.Id == a.Id || x.Id == b.Id || x.Id == c.Id).Select(x => x.JobId).Distinct().ToListAsync());
        jobIds.Count.ShouldBe(1);

        (await DetailAsync(first, a.Id)).JobDescriptionHtml.ShouldBe("<p>Apply via WhatsApp only</p>");
        (await DetailAsync(second, b.Id)).JobDescriptionHtml.ShouldBe("<p>The real description</p>");
        (await DetailAsync(third, c.Id)).JobDescriptionHtml.ShouldBeNull();
        (await host.WithDbAsync(db => db.Jobs.SingleAsync(j => j.Id == jobIds[0]))).DescriptionHtml.ShouldBeNull();
    }

    [Fact]
    public async Task The_Export_Carries_The_Profile_Pages_The_Account_Pointed_At()
    {
        var (client, _) = await host.RegisterAsync("exporter@example.com");
        await CaptureAsync(client, "https://www.linkedin.com/jobs/view/9100000002/", null,
            companyLinkedInUrl: "https://www.linkedin.com/company/hooli/");

        var export = await client.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", JsonOptions);

        var submission = export!.CompanyProfileSubmissions.ShouldHaveSingleItem();
        submission.Platform.ShouldBe(nameof(Source.LinkedIn));
        submission.Url.ShouldBe("https://linkedin.com/company/hooli");
        (await host.WithDbAsync(db => db.CompanyProfileSubmissions.CountAsync())).ShouldBe(1);
    }

    private async Task<string> ApplyAsync(HttpClient client, string companyName)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime, DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = (await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions))!;
        return created.CompanySlug!;
    }

    private static async Task<IReadOnlyList<CompanySearchResultResponse>> SearchAsync(HttpClient client, string q) =>
        (await client.GetFromJsonAsync<List<CompanySearchResultResponse>>($"/api/companies/search?q={Uri.EscapeDataString(q)}", JsonOptions))!;

    private static async Task<ApplicationDetailResponse> CaptureAsync(HttpClient client, string jobUrl, string? descriptionHtml,
        string? companyLinkedInUrl = null)
    {
        var response = await client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Hooli", "Engineer", jobUrl, "Istanbul", null, null, descriptionHtml,
                CompanyLinkedInUrl: companyLinkedInUrl),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions))!.Application;
    }

    private static async Task<ApplicationDetailResponse> DetailAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<ApplicationDetailResponse>($"/api/applications/{id}", JsonOptions))!;
}
