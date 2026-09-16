using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Occupations.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Occupations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanySalaries;

public sealed class CompanySalaryFlowProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        // Small on purpose so the quota test does not have to write ten entries.
        builder.UseSetting("CompanySalaries:MaxEntriesPerUser", "2");
        builder.UseSetting("CompanySalaries:MinimumEntriesForStats", "3");
    }
}

/// <summary>
/// Salary entries end to end: reading needs an account, the reader's row carries the catalogue
/// occupation, a band and a month but never the years or the author, one entry per occupation
/// per company per account, the total quota holds and deleting frees it, other people's rows are
/// 404 to edit, the per-currency figures appear only at the threshold, and the public company page
/// counts entries. Occupations are the seeded catalogue rows (ids derived from their codes).
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CompanySalaryFlowTests(ApiHost<CompanySalaryFlowProfile> host) : IClassFixture<ApiHost<CompanySalaryFlowProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Salary", "Tester", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return client;
    }

    private async Task<ResolvedCompanyResponse> ResolveAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;
    }

    // Seeded catalogue rows: ISCO 2512 "Software Developers", 2513 "Web and Multimedia Developers",
    // 2511 "Systems Analysts", 2521 "Database Designers and Administrators".
    private static readonly Guid SoftwareDevelopers = Occupation.IdFor("2512");
    private static readonly Guid WebDevelopers = Occupation.IdFor("2513");
    private static readonly Guid SystemsAnalysts = Occupation.IdFor("2511");
    private static readonly Guid DatabaseAdmins = Occupation.IdFor("2521");

    private static CompanySalaryRequest Salary(Guid? occupation = null, int years = 6, decimal amount = 95_000m,
        SalaryCurrency currency = SalaryCurrency.TRY, bool hasBonus = true, decimal? bonus = 120_000m) =>
        new(occupation ?? SoftwareDevelopers, years, EmploymentType.FullTime, SalaryEmploymentStatus.CurrentEmployee, amount, currency, hasBonus, bonus);

    private async Task<MyCompanySalaryResponse> ShareAsync(HttpClient client, Guid companyId, CompanySalaryRequest? request = null)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/salaries", request ?? Salary(), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCompanySalaryResponse>(JsonOptions))!;
    }

    private async Task<CompanySalaryPageResponse> ListAsync(HttpClient client, Guid companyId) =>
        (await client.GetFromJsonAsync<CompanySalaryPageResponse>($"/api/companies/{companyId}/salaries", JsonOptions))!;

    // ---- Reading ------------------------------------------------------------------------------

    [Fact]
    public async Task Reading_Requires_An_Account()
    {
        var author = await RegisterAsync("read.salary@example.com");
        var company = await ResolveAsync(author, "Read Salary Co");
        await ShareAsync(author, company.Id);

        var anonymous = _factory!.CreateClient();
        (await anonymous.GetAsync($"/api/companies/{company.Id}/salaries")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/companies/{company.Id}/salaries/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/company-salaries/mine")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task A_Reader_Sees_A_Band_And_A_Month_But_Never_The_Years_Or_The_Author()
    {
        var author = await RegisterAsync("band.author@example.com");
        var reader = await RegisterAsync("band.reader@example.com");
        var company = await ResolveAsync(author, "Band Co");
        await ShareAsync(author, company.Id, Salary(years: 7));

        var raw = await reader.GetStringAsync($"/api/companies/{company.Id}/salaries");
        raw.ShouldNotContain("userId", Case.Insensitive);
        raw.ShouldNotContain("yearsOfExperience", Case.Insensitive);
        raw.ShouldNotContain("jobTitle", Case.Insensitive);
        raw.ShouldNotContain("@example.com");

        var page = JsonSerializer.Deserialize<CompanySalaryPageResponse>(raw, JsonOptions)!;
        var row = page.Items.ShouldHaveSingleItem();
        row.Occupation.Code.ShouldBe("2512");
        row.Occupation.NameEn.ShouldBe("Software Developers");
        row.Occupation.NameTr.ShouldBe("Yazılım geliştiricileri");
        row.ExperienceBand.ShouldBe(ExperienceBand.FiveToNine);
        row.EmploymentType.ShouldBe(EmploymentType.FullTime);
        row.EmploymentStatus.ShouldBe(SalaryEmploymentStatus.CurrentEmployee);
        row.MonthlyNetAmount.ShouldBe(95_000m);
        row.Currency.ShouldBe(SalaryCurrency.TRY);
        row.AnnualBonusAmount.ShouldBe(120_000m);
        row.SubmittedMonth.ShouldBe(DateTimeOffset.UtcNow.ToString("yyyy-MM"));
        page.Total.ShouldBe(1);
        page.MinimumForStats.ShouldBe(3);
    }

    [Fact]
    public async Task Unknown_Company_Is_Not_Found()
    {
        var reader = await RegisterAsync("unknown.company@example.com");

        (await reader.GetAsync($"/api/companies/{Guid.NewGuid()}/salaries")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await reader.GetAsync($"/api/companies/{Guid.NewGuid()}/salaries/me")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await reader.PostAsJsonAsync($"/api/companies/{Guid.NewGuid()}/salaries", Salary(), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Writing rules ------------------------------------------------------------------------

    [Fact]
    public async Task Same_Occupation_Twice_Is_Refused()
    {
        var author = await RegisterAsync("twice.salary@example.com");
        var company = await ResolveAsync(author, "Twice Co");
        await ShareAsync(author, company.Id, Salary(SoftwareDevelopers));

        var again = await author.PostAsJsonAsync($"/api/companies/{company.Id}/salaries", Salary(SoftwareDevelopers, years: 9), JsonOptions);

        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await again.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        problem!.Detail.ShouldContain("already shared a salary for this occupation");
    }

    [Fact]
    public async Task An_Occupation_Outside_The_Catalogue_Is_Refused()
    {
        var author = await RegisterAsync("unknown.occupation@example.com");
        var company = await ResolveAsync(author, "Unknown Occupation Co");

        var response = await author.PostAsJsonAsync($"/api/companies/{company.Id}/salaries", Salary(Guid.NewGuid()), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!.Detail.ShouldContain("Pick an occupation from the list");

        var empty = await author.PostAsJsonAsync($"/api/companies/{company.Id}/salaries", Salary(Guid.Empty), JsonOptions);
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await empty.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions))!.Errors.Keys.ShouldContain("OccupationId");
    }

    [Fact]
    public async Task A_Different_Occupation_At_The_Same_Company_Is_A_Second_Row()
    {
        var author = await RegisterAsync("promotion.salary@example.com");
        var company = await ResolveAsync(author, "Promotion Co");
        await ShareAsync(author, company.Id, Salary(WebDevelopers, years: 3, amount: 60_000m));
        await ShareAsync(author, company.Id, Salary(SoftwareDevelopers, years: 6));

        var mine = await author.GetFromJsonAsync<CompanySalaryViewerStateResponse>($"/api/companies/{company.Id}/salaries/me", JsonOptions);
        mine!.OwnEntries.Count.ShouldBe(2);
        mine.Quota.Used.ShouldBe(2);
        mine.Quota.Limit.ShouldBe(2);
    }

    [Fact]
    public async Task Quota_Counts_Every_Company_And_Deleting_Frees_A_Slot()
    {
        var author = await RegisterAsync("quota.salary@example.com");
        var first = await ResolveAsync(author, "Quota One Co");
        var second = await ResolveAsync(author, "Quota Two Co");
        var third = await ResolveAsync(author, "Quota Three Co");
        var kept = await ShareAsync(author, first.Id);
        await ShareAsync(author, second.Id);

        var refused = await author.PostAsJsonAsync($"/api/companies/{third.Id}/salaries", Salary(), JsonOptions);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!.Detail.ShouldContain("limit of 2 salary entries");

        (await author.DeleteAsync($"/api/company-salaries/{kept.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ShareAsync(author, third.Id);

        var mine = await author.GetFromJsonAsync<MySalariesResponse>("/api/company-salaries/mine", JsonOptions);
        mine!.Items.Select(i => i.CompanyName).ShouldBe(["Quota Three Co", "Quota Two Co"]);
        mine.Quota.Used.ShouldBe(2);
    }

    [Fact]
    public async Task Validation_Refuses_A_Bonus_Answer_Without_An_Amount()
    {
        var author = await RegisterAsync("validation.salary@example.com");
        var company = await ResolveAsync(author, "Validation Co");

        var noAmount = await author.PostAsJsonAsync($"/api/companies/{company.Id}/salaries", Salary(hasBonus: true, bonus: null), JsonOptions);
        noAmount.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await noAmount.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        problem!.Errors.Keys.ShouldContain("AnnualBonusAmount");
    }

    // ---- Ownership ----------------------------------------------------------------------------

    [Fact]
    public async Task Another_Persons_Entry_Is_Not_Found_To_Edit_Or_Delete()
    {
        var author = await RegisterAsync("owner.salary@example.com");
        var other = await RegisterAsync("other.salary@example.com");
        var company = await ResolveAsync(author, "Owner Co");
        var entry = await ShareAsync(author, company.Id);

        (await other.PutAsJsonAsync($"/api/company-salaries/{entry.Id}", Salary(amount: 1m), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/company-salaries/{entry.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Untouched.
        (await ListAsync(other, company.Id)).Items.ShouldHaveSingleItem().MonthlyNetAmount.ShouldBe(95_000m);
    }

    [Fact]
    public async Task Editing_Replaces_The_Figures_And_Keeps_The_Occupation_Rule()
    {
        var author = await RegisterAsync("edit.salary@example.com");
        var company = await ResolveAsync(author, "Edit Co");
        var senior = await ShareAsync(author, company.Id, Salary(SoftwareDevelopers));
        await ShareAsync(author, company.Id, Salary(WebDevelopers, years: 2, amount: 50_000m, hasBonus: false, bonus: null));

        var updated = await author.PutAsJsonAsync($"/api/company-salaries/{senior.Id}",
            Salary(SystemsAnalysts, years: 9, amount: 130_000m, currency: SalaryCurrency.EUR, hasBonus: false, bonus: null), JsonOptions);
        updated.StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = await updated.Content.ReadFromJsonAsync<MyCompanySalaryResponse>(JsonOptions);
        row!.Occupation.Code.ShouldBe("2511");
        row.YearsOfExperience.ShouldBe(9);
        row.Currency.ShouldBe(SalaryCurrency.EUR);
        row.AnnualBonusAmount.ShouldBeNull();

        // Moving onto the other row's occupation collides; an unknown one is refused.
        var collision = await author.PutAsJsonAsync($"/api/company-salaries/{senior.Id}", Salary(WebDevelopers), JsonOptions);
        collision.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var unknown = await author.PutAsJsonAsync($"/api/company-salaries/{senior.Id}", Salary(Guid.NewGuid()), JsonOptions);
        unknown.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Stats --------------------------------------------------------------------------------

    [Fact]
    public async Task Per_Currency_Figures_Appear_Only_At_The_Threshold()
    {
        var first = await RegisterAsync("stats.one@example.com");
        var company = await ResolveAsync(first, "Stats Co");
        await ShareAsync(first, company.Id, Salary(SoftwareDevelopers, amount: 48_000m));
        var second = await RegisterAsync("stats.two@example.com");
        await ShareAsync(second, company.Id, Salary(SoftwareDevelopers, amount: 95_000m));
        // A EUR row does not count toward the TRY threshold.
        await ShareAsync(second, company.Id, Salary(DatabaseAdmins, amount: 4_000m, currency: SalaryCurrency.EUR));

        var under = await ListAsync(first, company.Id);
        var tryUnder = under.Stats.Single(s => s.Currency == SalaryCurrency.TRY);
        tryUnder.Count.ShouldBe(2);
        tryUnder.MedianMonthlyNet.ShouldBeNull();
        tryUnder.MinMonthlyNet.ShouldBeNull();
        under.Stats.Single(s => s.Currency == SalaryCurrency.EUR).Count.ShouldBe(1);

        var third = await RegisterAsync("stats.three@example.com");
        await ShareAsync(third, company.Id, Salary(WebDevelopers, amount: 62_000m));

        var at = await ListAsync(first, company.Id);
        var tryAt = at.Stats.Single(s => s.Currency == SalaryCurrency.TRY);
        tryAt.Count.ShouldBe(3);
        tryAt.MedianMonthlyNet.ShouldBe(62_000m);
        tryAt.MinMonthlyNet.ShouldBe(48_000m);
        tryAt.MaxMonthlyNet.ShouldBe(95_000m);
        at.Items.Count.ShouldBe(4);
        at.Items.First().Occupation.Code.ShouldBe("2513");
    }

    // ---- Public company page --------------------------------------------------------------------

    [Fact]
    public async Task The_Public_Company_Page_Counts_Entries_Without_Exposing_Them()
    {
        var author = await RegisterAsync("count.salary@example.com");
        var company = await ResolveAsync(author, "Count Co");
        await ShareAsync(author, company.Id);

        var anonymous = _factory!.CreateClient();
        var raw = await anonymous.GetStringAsync($"/api/companies/public/{company.Slug}");
        raw.ShouldNotContain("95000");
        var page = JsonSerializer.Deserialize<CompanyPublicResponse>(raw, JsonOptions)!;
        page.SalaryCount.ShouldBe(1);
    }
}
