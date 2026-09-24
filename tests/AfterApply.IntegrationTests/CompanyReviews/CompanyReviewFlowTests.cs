using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanyReviews;

public sealed class CompanyReviewFlowProfile : IHostProfile
{
    public void Configure(IWebHostBuilder builder)
    {
        // Small on purpose so the quota test does not have to write ten reviews.
        builder.UseSetting("CompanyReviews:MaxReviewsPerUser", "3");
        builder.UseSetting("CompanyReviews:MinimumReviewsForScore", "3");
        builder.UseSetting("CompanyReviews:PriorWeight", "5");
    }
}

/// <summary>
/// Company reviews end to end: a structured review is public the moment it is saved and carries
/// no free text on the wire, legacy rows keep their ratings but lose their text publicly, the
/// anonymous read side never sees a pending or rejected row, authors own only their own rows,
/// the quota and the one-per-company rule hold, and the moderation surface is admin-only. One
/// host for the class; each test uses its own accounts and its own company names.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CompanyReviewFlowTests(ApiHost<CompanyReviewFlowProfile> host) : IClassFixture<ApiHost<CompanyReviewFlowProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _admin = await RegisterAsync("admin.reviews@ekariyerim.com");
        await SetAdminAsync("admin.reviews@ekariyerim.com", true);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory!.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory!.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Review", "Tester", true));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        // The coded errors come back as localized ProblemDetails text (DomainExceptionHandler);
        // asking for English keeps the assertions readable.
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return client;
    }

    private async Task SetAdminAsync(string email, bool isAdmin)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.IsAdmin = isAdmin;
        await db.SaveChangesAsync();
    }

    private const string LikedKey = "environment.pos.team_communication";
    private const string ImprovableKey = "pay.imp.salary_level";

    private static CreateCompanyReviewRequest Review(int overall = 4,
        IReadOnlyList<ReviewCategoryRatingDto>? categories = null,
        IReadOnlyList<string>? liked = null, IReadOnlyList<string>? improvable = null) => new(
        EmploymentStatus.FormerEmployee, overall,
        categories ?? [new ReviewCategoryRatingDto(ReviewCategory.WorkEnvironment, 5), new ReviewCategoryRatingDto(ReviewCategory.Pay, 2)],
        liked ?? [LikedKey],
        improvable ?? [ImprovableKey]);

    private static UpdateCompanyReviewRequest Update(int overall = 4, IReadOnlyList<string>? liked = null) => new(
        EmploymentStatus.CurrentEmployee, overall,
        [new ReviewCategoryRatingDto(ReviewCategory.Management, 4)],
        liked ?? ["management.pos.feedback_culture"],
        []);

    private static ReviewContent LegacyContent(int overall = 4, string title = "Honest, slow-moving, fair") => new(
        EmploymentStatus.FormerEmployee, title,
        "Clear expectations, good tooling, colleagues who actually review code.",
        "Decisions take a long time and the salary band lags the market.",
        overall, 4, 4, 3, 4);

    /// <summary>A pre-2026-09-16 row, the only way one can come into being now: straight into
    /// the database, exactly as the migration left them.</summary>
    private async Task<Guid> SeedLegacyReviewAsync(HttpClient author, Guid companyId, bool approved, int overall = 4,
        string title = "Honest, slow-moving, fair")
    {
        var userId = (await author.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions))!.Id;
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var review = CompanyReview.CreateLegacy(userId, companyId, LegacyContent(overall, title), DateTimeOffset.UtcNow);
        if (approved)
        {
            review.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        }

        db.CompanyReviews.Add(review);
        await db.SaveChangesAsync();
        return review.Id;
    }

    private async Task<ResolvedCompanyResponse> ResolveAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;
    }

    private async Task<MyCompanyReviewResponse> WriteAsync(HttpClient client, Guid companyId, CreateCompanyReviewRequest? request = null)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/reviews", request ?? Review(), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions))!;
    }

    private async Task ApproveAsync(Guid reviewId)
    {
        var response = await _admin.PostAsync($"/api/admin/company-reviews/{reviewId}/approve", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>A structured review is published on save; no approval step.</summary>
    private async Task<Guid> PublishedReviewByAsync(string email, Guid companyId, int overall = 4, IReadOnlyList<string>? liked = null)
    {
        var author = await RegisterAsync(email);
        var review = await WriteAsync(author, companyId, Review(overall, liked: liked));
        return review.Id;
    }

    private async Task<PagedResult<CompanyReviewPublicResponse>> PublicReviewsAsync(string slug) =>
        (await _factory!.CreateClient().GetFromJsonAsync<PagedResult<CompanyReviewPublicResponse>>(
            $"/api/companies/public/{slug}/reviews", JsonOptions))!;

    private async Task<CompanyReviewSummaryResponse> SummaryAsync(string slug) =>
        (await _factory!.CreateClient().GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{slug}", JsonOptions))!.Summary;

    // ---- Public visibility ------------------------------------------------------------------

    [Fact]
    public async Task Resolve_Creates_A_Company_With_A_Slug_And_Reuses_It_On_A_Spelling_Variant()
    {
        var user = await RegisterAsync("resolve.reviews@example.com");

        var first = await ResolveAsync(user, "Türk Telekom A.Ş.");
        var second = await ResolveAsync(user, "türk telekom");

        // The slug keeps the legal suffix (the SQL backfill does too); the *lookup* is what strips
        // it, which is why the second spelling lands on the same row.
        first.Slug.ShouldBe("turk-telekom-a-s");
        second.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task A_Pending_Legacy_Review_Is_Invisible_On_Every_Public_Route_And_Still_Waits_In_The_Queue()
    {
        var author = await RegisterAsync("pending.reviews@example.com");
        var company = await ResolveAsync(author, "Pending Visibility Co");
        await SeedLegacyReviewAsync(author, company.Id, approved: false);

        var anonymous = _factory!.CreateClient();

        // The switch to structured reviews did not decide these for the moderator.
        (await _admin.GetFromJsonAsync<ModerationCountsResponse>("/api/admin/company-reviews/counts", JsonOptions))!
            .PendingReviews.ShouldBe(1);
        (await author.GetFromJsonAsync<MyReviewsResponse>("/api/company-reviews/mine", JsonOptions))!
            .Items.ShouldHaveSingleItem().Status.ShouldBe(ReviewModerationStatus.Pending);

        var profile = await anonymous.GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions);
        profile!.Summary.ApprovedCount.ShouldBe(0);
        profile.Summary.Score.ShouldBeNull();

        var reviews = await anonymous.GetFromJsonAsync<PagedResult<CompanyReviewPublicResponse>>(
            $"/api/companies/public/{company.Slug}/reviews", JsonOptions);
        reviews!.Items.ShouldBeEmpty();

        var directory = await anonymous.GetFromJsonAsync<PagedResult<CompanyPublicListItemResponse>>(
            "/api/companies/public?q=Pending%20Visibility", JsonOptions);
        directory!.Items.ShouldBeEmpty();

        var slugs = await anonymous.GetFromJsonAsync<List<ReviewedCompanySlugResponse>>("/api/companies/public/slugs", JsonOptions);
        slugs!.ShouldNotContain(s => s.Slug == company.Slug);
    }

    [Fact]
    public async Task A_Structured_Review_Is_Public_At_Once_Without_Its_Author_And_A_Rejected_One_Is_Not()
    {
        var author = await RegisterAsync("approved.reviews@example.com");
        var company = await ResolveAsync(author, "Approved Visibility Co");
        var review = await WriteAsync(author, company.Id);
        review.Status.ShouldBe(ReviewModerationStatus.Approved);
        review.ModeratedAt.ShouldBeNull();
        review.Format.ShouldBe(ReviewFormat.Structured);

        var rejectedAuthor = await RegisterAsync("rejected.reviews@example.com");
        var rejected = await WriteAsync(rejectedAuthor, company.Id, Review(1, liked: ["general.pos.growth_opportunities"]));
        (await _admin.PostAsJsonAsync($"/api/admin/company-reviews/{rejected.Id}/reject",
            new RejectCompanyReviewRequest("Looks like a coordinated pattern."), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var anonymous = _factory!.CreateClient();
        var response = await anonymous.GetAsync($"/api/companies/public/{company.Slug}/reviews");
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();

        // The promises on the privacy page, asserted on the wire rather than on a record type:
        // no author, and no free-text field at all.
        raw.ShouldNotContain("userId", Case.Insensitive);
        raw.ShouldNotContain("@example.com");
        raw.ShouldNotContain("\"title\"", Case.Insensitive);
        raw.ShouldNotContain("\"pros\"", Case.Insensitive);
        raw.ShouldNotContain("\"cons\"", Case.Insensitive);
        raw.ShouldNotContain("growth_opportunities");

        var page = JsonSerializer.Deserialize<PagedResult<CompanyReviewPublicResponse>>(raw, JsonOptions)!;
        var item = page.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(review.Id);
        item.Format.ShouldBe(ReviewFormat.Structured);
        item.OverallRating.ShouldBe(4);
        item.CategoryRatings.ShouldBe([
            new ReviewCategoryRatingDto(ReviewCategory.WorkEnvironment, 5), new ReviewCategoryRatingDto(ReviewCategory.Pay, 2)]);
        item.LegacySalaryAndBenefitsRating.ShouldBeNull();
        item.LikedStatements.ShouldBe([LikedKey]);
        item.ImprovableStatements.ShouldBe([ImprovableKey]);
        item.SubmittedMonth.ShouldBe(DateTimeOffset.UtcNow.ToString("yyyy-MM"));
        item.HelpfulCount.ShouldBe(0);

        var directory = await anonymous.GetFromJsonAsync<PagedResult<CompanyPublicListItemResponse>>(
            "/api/companies/public?q=approved%20visibility", JsonOptions);
        directory!.Items.ShouldHaveSingleItem().ApprovedCount.ShouldBe(1);

        // Auto-published rows carry no ModeratedAt; the sitemap must still list the company.
        var slugs = await anonymous.GetFromJsonAsync<List<ReviewedCompanySlugResponse>>("/api/companies/public/slugs", JsonOptions);
        slugs!.ShouldContain(s => s.Slug == company.Slug);

        // The author still sees the rejection and its reason on their own list.
        var mine = await rejectedAuthor.GetFromJsonAsync<MyReviewsResponse>("/api/company-reviews/mine", JsonOptions);
        var own = mine!.Items.ShouldHaveSingleItem();
        own.Status.ShouldBe(ReviewModerationStatus.Rejected);
        own.RejectionReason.ShouldBe("Looks like a coordinated pattern.");
    }

    [Fact]
    public async Task A_Legacy_Review_Keeps_Its_Ratings_Publicly_But_Its_Text_Only_For_Its_Author()
    {
        var author = await RegisterAsync("legacy.reviews@example.com");
        var company = await ResolveAsync(author, "Legacy Visibility Co");
        var id = await SeedLegacyReviewAsync(author, company.Id, approved: true, title: "Honest, slow-moving, fair");

        var response = await _factory!.CreateClient().GetAsync($"/api/companies/public/{company.Slug}/reviews");
        var raw = await response.Content.ReadAsStringAsync();
        raw.ShouldNotContain("Honest, slow-moving");
        raw.ShouldNotContain("colleagues who actually");

        var item = JsonSerializer.Deserialize<PagedResult<CompanyReviewPublicResponse>>(raw, JsonOptions)!.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(id);
        item.Format.ShouldBe(ReviewFormat.Legacy);
        item.OverallRating.ShouldBe(4);
        // Management 4, WorkEnvironment 4, Career 4 map onto current categories; SalaryAndBenefits
        // maps onto none and travels separately.
        item.CategoryRatings.Select(c => c.Category).ShouldBe(
            [ReviewCategory.WorkEnvironment, ReviewCategory.Management, ReviewCategory.CareerGrowth]);
        item.LegacySalaryAndBenefitsRating.ShouldBe(3);
        item.LikedStatements.ShouldBeEmpty();

        var mine = (await author.GetFromJsonAsync<MyReviewsResponse>("/api/company-reviews/mine", JsonOptions))!.Items.ShouldHaveSingleItem();
        mine.Format.ShouldBe(ReviewFormat.Legacy);
        mine.Title.ShouldBe("Honest, slow-moving, fair");
        mine.Pros.ShouldNotBeNullOrEmpty();

        // The legacy ratings feed the category averages once enough people rated the category.
        await PublishedReviewByAsync("legacy2.reviews@example.com", company.Id);
        await PublishedReviewByAsync("legacy3.reviews@example.com", company.Id);
        var summary = await SummaryAsync(company.Slug);
        summary.ApprovedCount.ShouldBe(3);
        // Work environment: legacy 4 + structured 5 + 5.
        summary.Categories.Single(c => c.Category == ReviewCategory.WorkEnvironment).ShouldSatisfyAllConditions(
            c => c.Count.ShouldBe(3), c => c.Average.ShouldBe(4.7));
        // Pay: two structured votes only — under the minimum, so a count but no number.
        summary.Categories.Single(c => c.Category == ReviewCategory.Pay).ShouldSatisfyAllConditions(
            c => c.Count.ShouldBe(2), c => c.Average.ShouldBeNull());
        // Management: the legacy row alone.
        summary.Categories.Single(c => c.Category == ReviewCategory.Management).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Editing_A_Legacy_Review_Converts_It_And_Hides_Nothing_From_Its_Author()
    {
        var author = await RegisterAsync("convert.reviews@example.com");
        var company = await ResolveAsync(author, "Conversion Co");
        var id = await SeedLegacyReviewAsync(author, company.Id, approved: true);

        var response = await author.PutAsJsonAsync($"/api/company-reviews/{id}", Update(overall: 3), JsonOptions);
        response.EnsureSuccessStatusCode();
        var updated = (await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions))!;

        updated.Format.ShouldBe(ReviewFormat.Structured);
        updated.Status.ShouldBe(ReviewModerationStatus.Approved);
        updated.OverallRating.ShouldBe(3);
        updated.CategoryRatings.ShouldBe([new ReviewCategoryRatingDto(ReviewCategory.Management, 4)]);
        updated.LikedStatements.ShouldBe(["management.pos.feedback_culture"]);
        // Still stored, still the author's to read; not on the public wire.
        updated.Title.ShouldBe("Honest, slow-moving, fair");

        var item = (await PublicReviewsAsync(company.Slug)).Items.ShouldHaveSingleItem();
        item.Format.ShouldBe(ReviewFormat.Structured);
        item.CategoryRatings.ShouldBe([new ReviewCategoryRatingDto(ReviewCategory.Management, 4)]);
        item.LegacySalaryAndBenefitsRating.ShouldBeNull();
    }

    [Fact]
    public async Task The_Validator_Refuses_Too_Many_Unknown_And_Mislisted_Statements()
    {
        var author = await RegisterAsync("invalid.reviews@example.com");
        var company = await ResolveAsync(author, "Validation Co");
        var six = ReviewStatementCatalogue.For(ReviewCategory.CareerGrowth, ReviewStatementKind.Liked).Take(6).Select(x => x.Key).ToList();

        async Task<HttpResponseMessage> Post(CreateCompanyReviewRequest request) =>
            await author.PostAsJsonAsync($"/api/companies/{company.Id}/reviews", request, JsonOptions);

        var tooMany = await Post(Review(liked: six));
        tooMany.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tooMany.Content.ReadAsStringAsync()).ShouldContain("at most 5");

        (await Post(Review(liked: ["career.pos.does_not_exist"]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Post(Review(improvable: [LikedKey]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Post(Review(categories: [new ReviewCategoryRatingDto(ReviewCategory.Overall, 4)]))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await Post(Review(overall: 0))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Nothing was stored along the way.
        (await author.GetFromJsonAsync<MyReviewsResponse>("/api/company-reviews/mine", JsonOptions))!.Items.ShouldBeEmpty();

        // The bare minimum is a rating and a relationship.
        (await Post(new CreateCompanyReviewRequest(EmploymentStatus.Intern, 3))).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task The_Summary_Counts_Picks_Once_Three_People_Have_Spoken()
    {
        var first = await RegisterAsync("top1.reviews@example.com");
        var company = await ResolveAsync(first, "Top Statements Co");
        await WriteAsync(first, company.Id, Review(liked: [LikedKey, "environment.pos.motivating"]));
        await PublishedReviewByAsync("top2.reviews@example.com", company.Id, liked: [LikedKey]);

        var two = await SummaryAsync(company.Slug);
        two.ApprovedCount.ShouldBe(2);
        two.TopLiked.ShouldBeEmpty();
        two.Categories.Count.ShouldBe(10);
        two.Categories.ShouldAllBe(c => c.Average == null);

        await PublishedReviewByAsync("top3.reviews@example.com", company.Id, liked: [LikedKey, "environment.pos.motivating"]);

        var three = await SummaryAsync(company.Slug);
        three.TopLiked.ShouldBe([new ReviewStatementCountResponse(LikedKey, 3), new ReviewStatementCountResponse("environment.pos.motivating", 2)]);
        three.TopImprovable.ShouldBe([new ReviewStatementCountResponse(ImprovableKey, 3)]);
        three.Categories.Single(c => c.Category == ReviewCategory.WorkEnvironment).Average.ShouldBe(5.0);
        three.Categories.Single(c => c.Category == ReviewCategory.Pay).Average.ShouldBe(2.0);
        three.Categories.Single(c => c.Category == ReviewCategory.Onboarding).ShouldSatisfyAllConditions(
            c => c.Count.ShouldBe(0), c => c.Average.ShouldBeNull());
    }

    [Fact]
    public async Task The_Score_Appears_At_The_Minimum_And_Follows_The_Published_Formula()
    {
        var first = await RegisterAsync("score1.reviews@example.com");
        var company = await ResolveAsync(first, "Score Threshold Co");
        var anonymous = _factory!.CreateClient();

        // The prior is the site-wide mean, so make sure it is below 5 regardless of which other
        // tests have run: one approved 1-star review somewhere else.
        var elsewhere = await ResolveAsync(first, "Score Anchor Co");
        await PublishedReviewByAsync("score0.reviews@example.com", elsewhere.Id, 1);

        await WriteAsync(first, company.Id, Review(5));
        await PublishedReviewByAsync("score2.reviews@example.com", company.Id, 5);

        var two = await anonymous.GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions);
        two!.Summary.ApprovedCount.ShouldBe(2);
        two.Summary.Score.ShouldBeNull();
        two.Summary.AverageOverall.ShouldBe(5.0);

        await PublishedReviewByAsync("score3.reviews@example.com", company.Id, 5);

        var three = await anonymous.GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions);
        three!.Summary.ApprovedCount.ShouldBe(3);
        three.Summary.Score.ShouldNotBeNull();
        // Pulled toward the site-wide mean by the prior: three perfect reviews are not a 5.0.
        three.Summary.Score!.Value.ShouldBeLessThan(5.0);
        three.Summary.Score.Value.ShouldBeGreaterThan(3.0);
        three.Summary.Distribution[4].ShouldBe(3);
        three.Summary.MinimumForScore.ShouldBe(3);
        three.Summary.PriorWeight.ShouldBe(5);
    }

    [Fact]
    public async Task Unknown_Slugs_Are_404_And_The_Viewer_State_Needs_A_Sign_In()
    {
        var anonymous = _factory!.CreateClient();

        (await anonymous.GetAsync("/api/companies/public/no-such-company")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync("/api/companies/public/no-such-company/reviews")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"/api/companies/{Guid.CreateVersion7()}/reviews/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync("/api/company-reviews/mine")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Lifecycle and ownership -----------------------------------------------------------

    [Fact]
    public async Task One_Review_Per_Company_Per_Account()
    {
        var author = await RegisterAsync("once.reviews@example.com");
        var company = await ResolveAsync(author, "One Voice Co");
        await WriteAsync(author, company.Id);

        var second = await author.PostAsJsonAsync($"/api/companies/{company.Id}/reviews", Review(), JsonOptions);

        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await second.Content.ReadAsStringAsync()).ShouldContain("already reviewed this company");
    }

    [Fact]
    public async Task Editing_A_Published_Review_Changes_The_Page_At_Once()
    {
        var author = await RegisterAsync("edit.reviews@example.com");
        var company = await ResolveAsync(author, "Edit Cycle Co");
        var review = await WriteAsync(author, company.Id);

        (await PublicReviewsAsync(company.Slug)).Items.ShouldHaveSingleItem().OverallRating.ShouldBe(4);
        (await SummaryAsync(company.Slug)).AverageOverall.ShouldBe(4.0);

        var response = await author.PutAsJsonAsync($"/api/company-reviews/{review.Id}", Update(overall: 2), JsonOptions);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions);
        updated!.Status.ShouldBe(ReviewModerationStatus.Approved);
        updated.EmploymentStatus.ShouldBe(EmploymentStatus.CurrentEmployee);

        // Still one review, now with the new content; the cached aggregate was evicted.
        var item = (await PublicReviewsAsync(company.Slug)).Items.ShouldHaveSingleItem();
        item.OverallRating.ShouldBe(2);
        item.LikedStatements.ShouldBe(["management.pos.feedback_culture"]);
        item.ImprovableStatements.ShouldBeEmpty();
        (await SummaryAsync(company.Slug)).AverageOverall.ShouldBe(2.0);

        // The old child rows are gone, not merely shadowed.
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CompanyReviewStatementPicks.CountAsync(p => p.ReviewId == review.Id)).ShouldBe(1);
        (await db.CompanyReviewCategoryRatings.CountAsync(p => p.ReviewId == review.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task Another_Account_Cannot_Edit_Or_Delete_A_Review_And_Learns_Nothing_From_Trying()
    {
        var author = await RegisterAsync("owner.reviews@example.com");
        var company = await ResolveAsync(author, "Ownership Co");
        var review = await WriteAsync(author, company.Id);
        var other = await RegisterAsync("other.reviews@example.com");

        var update = Update(overall: 1);

        (await other.PutAsJsonAsync($"/api/company-reviews/{review.Id}", update, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/company-reviews/{review.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        (await author.DeleteAsync($"/api/company-reviews/{review.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await author.GetFromJsonAsync<MyReviewsResponse>("/api/company-reviews/mine", JsonOptions))!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Quota_Counts_Every_Status_And_An_Admin_Override_Changes_It_On_The_Next_Request()
    {
        var author = await RegisterAsync("quota.reviews@example.com");
        var one = await ResolveAsync(author, "Quota One Co");
        var two = await ResolveAsync(author, "Quota Two Co");
        var three = await ResolveAsync(author, "Quota Three Co");
        var four = await ResolveAsync(author, "Quota Four Co");

        await WriteAsync(author, one.Id);
        await WriteAsync(author, two.Id);
        await WriteAsync(author, three.Id);

        var mine = await author.GetFromJsonAsync<MyReviewsResponse>("/api/company-reviews/mine", JsonOptions);
        mine!.Quota.Used.ShouldBe(3);
        mine.Quota.Limit.ShouldBe(3);

        var refused = await author.PostAsJsonAsync($"/api/companies/{four.Id}/reviews", Review(), JsonOptions);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await refused.Content.ReadAsStringAsync()).ShouldContain("limit of 3 company reviews");

        var userId = (await author.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions))!.Id;
        var raised = await _admin.PutAsJsonAsync($"/api/admin/users/{userId}/review-quota", new SetReviewQuotaRequest(4), JsonOptions);
        raised.EnsureSuccessStatusCode();
        (await raised.Content.ReadFromJsonAsync<UserReviewQuotaResponse>(JsonOptions))!.EffectiveLimit.ShouldBe(4);

        await WriteAsync(author, four.Id);

        // Zero is the "this account is done" setting.
        (await _admin.PutAsJsonAsync($"/api/admin/users/{userId}/review-quota", new SetReviewQuotaRequest(0), JsonOptions)).EnsureSuccessStatusCode();
        var five = await ResolveAsync(author, "Quota Five Co");
        (await author.PostAsJsonAsync($"/api/companies/{five.Id}/reviews", Review(), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Helpful and reports ---------------------------------------------------------------

    [Fact]
    public async Task Helpful_Toggles_On_And_Off_And_Never_On_Ones_Own_Review()
    {
        var author = await RegisterAsync("helpful.author@example.com");
        var company = await ResolveAsync(author, "Helpful Co");
        var review = await WriteAsync(author, company.Id);

        var reader = await RegisterAsync("helpful.reader@example.com");
        // A pending (legacy) review is not on any page, so "not found".
        var pendingAuthor = await RegisterAsync("helpful.pending@example.com");
        var pending = await SeedLegacyReviewAsync(pendingAuthor, company.Id, approved: false);
        (await reader.PostAsync($"/api/company-reviews/{pending}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var on = await (await reader.PostAsync($"/api/company-reviews/{review.Id}/helpful", null)).Content.ReadFromJsonAsync<HelpfulToggleResponse>(JsonOptions);
        on!.Marked.ShouldBeTrue();
        on.HelpfulCount.ShouldBe(1);

        var state = await reader.GetFromJsonAsync<CompanyReviewViewerStateResponse>($"/api/companies/{company.Id}/reviews/me", JsonOptions);
        state!.OwnReview.ShouldBeNull();
        state.HelpfulMarkedReviewIds.ShouldBe([review.Id]);

        var off = await (await reader.PostAsync($"/api/company-reviews/{review.Id}/helpful", null)).Content.ReadFromJsonAsync<HelpfulToggleResponse>(JsonOptions);
        off!.Marked.ShouldBeFalse();
        off.HelpfulCount.ShouldBe(0);

        var own = await author.PostAsync($"/api/company-reviews/{review.Id}/helpful", null);
        own.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await own.Content.ReadAsStringAsync()).ShouldContain("marked as helpful");
    }

    [Fact]
    public async Task A_Report_Resolved_As_Removed_Rejects_The_Review_And_Closes_Its_Sibling_Reports()
    {
        var author = await RegisterAsync("report.author@example.com");
        var company = await ResolveAsync(author, "Reported Co");
        var review = await WriteAsync(author, company.Id);

        var readerA = await RegisterAsync("report.a@example.com");
        var readerB = await RegisterAsync("report.b@example.com");

        var first = await readerA.PostAsJsonAsync($"/api/company-reviews/{review.Id}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.PersonalInformation, "Names the team lead."), JsonOptions);
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        var firstReport = await first.Content.ReadFromJsonAsync<ReportCompanyReviewResponse>(JsonOptions);

        // One open report per reader.
        var again = await readerA.PostAsJsonAsync($"/api/company-reviews/{review.Id}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.Spam), JsonOptions);
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await again.Content.ReadAsStringAsync()).ShouldContain("open report");

        // "Other" without a note is a validation error, not a stored report.
        (await readerB.PostAsJsonAsync($"/api/company-reviews/{review.Id}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.Other), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await readerB.PostAsJsonAsync($"/api/company-reviews/{review.Id}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.Other, "Reads like an advert."), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Created);

        // Own review cannot be reported.
        (await author.PostAsJsonAsync($"/api/company-reviews/{review.Id}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.Spam), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var counts = await _admin.GetFromJsonAsync<ModerationCountsResponse>("/api/admin/company-reviews/counts", JsonOptions);
        counts!.OpenReports.ShouldBe(2);

        var open = await _admin.GetFromJsonAsync<PagedResult<AdminReviewReportResponse>>("/api/admin/company-review-reports", JsonOptions);
        open!.Items.Count.ShouldBe(2);
        open.Items.ShouldContain(r => r.ReporterEmail == "report.a@example.com");

        var resolve = await _admin.PostAsJsonAsync($"/api/admin/company-review-reports/{firstReport!.Id}/resolve",
            new ResolveReviewReportRequest(ReviewReportResolution.Removed, "Identifies a colleague by name."), JsonOptions);
        resolve.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _admin.GetFromJsonAsync<ModerationCountsResponse>("/api/admin/company-reviews/counts", JsonOptions))!.OpenReports.ShouldBe(0);

        var detail = await _admin.GetFromJsonAsync<AdminCompanyReviewResponse>($"/api/admin/company-reviews/{review.Id}", JsonOptions);
        detail!.Status.ShouldBe(ReviewModerationStatus.Rejected);
        detail.RejectionReason.ShouldBe("Identifies a colleague by name.");
        detail.AuthorEmail.ShouldBe("report.author@example.com");
        detail.Reports.Count.ShouldBe(2);
        detail.Reports.ShouldAllBe(r => r.Status == ReviewReportStatus.Resolved && r.Resolution == ReviewReportResolution.Removed);

        // Its only review removed, the company is not listed any more and has no public page
        // (CompanyVisibility, 2026-09-24) — let alone the removed review on it.
        (await _factory!.CreateClient().GetAsync($"/api/companies/public/{company.Slug}/reviews"))
            .StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);

        // The author cannot undo a removal by saving again: the edit lands in the human queue.
        var edited = await (await author.PutAsJsonAsync($"/api/company-reviews/{review.Id}", Update(), JsonOptions))
            .Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions);
        edited!.Status.ShouldBe(ReviewModerationStatus.Pending);
        edited.RejectionReason.ShouldBeNull();
        (await PublicReviewsAsync(company.Slug)).Items.ShouldBeEmpty();
        (await _admin.GetFromJsonAsync<ModerationCountsResponse>("/api/admin/company-reviews/counts", JsonOptions))!.PendingReviews.ShouldBe(1);

        await ApproveAsync(review.Id);
        (await PublicReviewsAsync(company.Slug)).Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_Dismissed_Report_Leaves_The_Review_Published()
    {
        var author = await RegisterAsync("dismiss.author@example.com");
        var company = await ResolveAsync(author, "Dismissed Report Co");
        var review = await WriteAsync(author, company.Id);
        var reader = await RegisterAsync("dismiss.reader@example.com");

        var report = await (await reader.PostAsJsonAsync($"/api/company-reviews/{review.Id}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.MisleadingInformation), JsonOptions))
            .Content.ReadFromJsonAsync<ReportCompanyReviewResponse>(JsonOptions);

        (await _admin.PostAsJsonAsync($"/api/admin/company-review-reports/{report!.Id}/resolve",
            new ResolveReviewReportRequest(ReviewReportResolution.Dismissed), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await _admin.GetFromJsonAsync<AdminCompanyReviewResponse>($"/api/admin/company-reviews/{review.Id}", JsonOptions);
        detail!.Status.ShouldBe(ReviewModerationStatus.Approved);

        // Dismissed → the reader may report again if the review still bothers them.
        (await reader.PostAsJsonAsync($"/api/company-reviews/{review.Id}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.Spam), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    // ---- Admin access and queue ------------------------------------------------------------

    [Fact]
    public async Task Every_Admin_Route_Is_401_Anonymous_And_403_For_An_Ordinary_User()
    {
        var ordinary = await RegisterAsync("ordinary.reviews@example.com");
        var anonymous = _factory!.CreateClient();
        var id = Guid.CreateVersion7();

        var routes = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, "/api/admin/company-reviews", null),
            (HttpMethod.Get, "/api/admin/company-reviews/counts", null),
            (HttpMethod.Get, $"/api/admin/company-reviews/{id}", null),
            (HttpMethod.Post, $"/api/admin/company-reviews/{id}/approve", null),
            (HttpMethod.Post, $"/api/admin/company-reviews/{id}/reject", new RejectCompanyReviewRequest("nope")),
            (HttpMethod.Get, "/api/admin/company-review-reports", null),
            (HttpMethod.Post, $"/api/admin/company-review-reports/{id}/resolve", new ResolveReviewReportRequest(ReviewReportResolution.Dismissed)),
            (HttpMethod.Put, $"/api/admin/users/{id}/review-quota", new SetReviewQuotaRequest(1)),
            (HttpMethod.Get, "/api/admin/company-salaries", null),
            (HttpMethod.Delete, $"/api/admin/company-salaries/{id}", null),
            (HttpMethod.Get, "/api/admin/candidate-experiences", null),
            (HttpMethod.Delete, $"/api/admin/candidate-experiences/{id}", null),
        };

        foreach (var (method, path, body) in routes)
        {
            (await Send(anonymous, method, path, body)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized, path);
            (await Send(ordinary, method, path, body)).StatusCode.ShouldBe(HttpStatusCode.Forbidden, path);
        }

        static Task<HttpResponseMessage> Send(HttpClient client, HttpMethod method, string path, object? body)
        {
            var request = new HttpRequestMessage(method, path);
            if (body is not null || method == HttpMethod.Post || method == HttpMethod.Put)
            {
                request.Content = JsonContent.Create(body ?? new { }, options: JsonOptions);
            }

            return client.SendAsync(request);
        }
    }

    [Fact]
    public async Task The_Queue_Filters_By_Status_Company_And_Date()
    {
        var a = await RegisterAsync("queue.a@example.com");
        var alpha = await ResolveAsync(a, "Queue Alpha Co");
        var beta = await ResolveAsync(a, "Queue Beta Co");
        var pendingAlpha = await SeedLegacyReviewAsync(a, alpha.Id, approved: false);
        var b = await RegisterAsync("queue.b@example.com");
        var approvedBeta = await WriteAsync(b, beta.Id);

        var pending = await _admin.GetFromJsonAsync<PagedResult<AdminCompanyReviewListItemResponse>>(
            "/api/admin/company-reviews?status=Pending&company=queue", JsonOptions);
        var pendingRow = pending!.Items.ShouldHaveSingleItem();
        pendingRow.Id.ShouldBe(pendingAlpha);
        pendingRow.Format.ShouldBe(ReviewFormat.Legacy);
        pendingRow.Title.ShouldBe("Honest, slow-moving, fair");
        pending.Items.ShouldNotContain(r => r.Id == approvedBeta.Id);

        // The admin detail carries both shapes: the structured picks, or the legacy text.
        var detail = await _admin.GetFromJsonAsync<AdminCompanyReviewResponse>($"/api/admin/company-reviews/{approvedBeta.Id}", JsonOptions);
        detail!.Format.ShouldBe(ReviewFormat.Structured);
        detail.Title.ShouldBeNull();
        detail.LikedStatements.ShouldBe([LikedKey]);
        detail.AuthorEmail.ShouldBe("queue.b@example.com");

        var beta_ = await _admin.GetFromJsonAsync<PagedResult<AdminCompanyReviewListItemResponse>>(
            "/api/admin/company-reviews?company=queue%20beta", JsonOptions);
        beta_!.Items.ShouldHaveSingleItem().Id.ShouldBe(approvedBeta.Id);

        var tomorrow = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var none = await _admin.GetFromJsonAsync<PagedResult<AdminCompanyReviewListItemResponse>>(
            $"/api/admin/company-reviews?company=queue&from={tomorrow}", JsonOptions);
        none!.Items.ShouldBeEmpty();

        var inverted = await _admin.GetAsync($"/api/admin/company-reviews?from={tomorrow}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"))}");
        inverted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var counts = await _admin.GetFromJsonAsync<ModerationCountsResponse>("/api/admin/company-reviews/counts", JsonOptions);
        counts!.PendingReviews.ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Revoking_Admin_Lands_On_The_Next_Request()
    {
        var email = "revoked.reviews@example.com";
        var client = await RegisterAsync(email);
        await SetAdminAsync(email, true);
        (await client.GetAsync("/api/admin/company-reviews/counts")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await SetAdminAsync(email, false);

        (await client.GetAsync("/api/admin/company-reviews/counts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
