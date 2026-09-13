using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.CompanyReviews;

/// <summary>
/// Company reviews end to end: the anonymous read side never sees anything but approved rows,
/// authors own only their own rows, the quota and the one-per-company rule hold, and the
/// moderation surface is admin-only. One host for the class; each test uses its own accounts
/// and its own company names.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CompanyReviewFlowTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(CompanyReviewFlowTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            // Small on purpose so the quota test does not have to write ten reviews.
            builder.UseSetting("CompanyReviews:MaxReviewsPerUser", "3");
            builder.UseSetting("CompanyReviews:MinimumReviewsForScore", "3");
            builder.UseSetting("CompanyReviews:PriorWeight", "5");
        });

        _admin = await RegisterAsync("admin.reviews@ekariyerim.com");
        await SetAdminAsync("admin.reviews@ekariyerim.com", true);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await TestHostDisposal.DisposeQuietlyAsync(_factory);
        }
    }

    private async Task<HttpClient> RegisterAsync(string email)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Review", "Tester", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
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

    private static CreateCompanyReviewRequest Review(int overall = 4, string title = "Honest, slow-moving, fair") => new(
        EmploymentStatus.FormerEmployee, title,
        "Clear expectations, good tooling, colleagues who actually review code.",
        "Decisions take a long time and the salary band lags the market.",
        overall, 4, 4, 3, 4);

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

    private async Task<Guid> ApprovedReviewByAsync(string email, Guid companyId, int overall = 4)
    {
        var author = await RegisterAsync(email);
        var review = await WriteAsync(author, companyId, Review(overall));
        await ApproveAsync(review.Id);
        return review.Id;
    }

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
    public async Task A_Pending_Review_Is_Invisible_On_Every_Public_Route()
    {
        var author = await RegisterAsync("pending.reviews@example.com");
        var company = await ResolveAsync(author, "Pending Visibility Co");
        await WriteAsync(author, company.Id);

        var anonymous = _factory!.CreateClient();

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
    public async Task An_Approved_Review_Is_Public_Without_Its_Author_And_A_Rejected_One_Is_Not()
    {
        var author = await RegisterAsync("approved.reviews@example.com");
        var company = await ResolveAsync(author, "Approved Visibility Co");
        var review = await WriteAsync(author, company.Id);
        await ApproveAsync(review.Id);

        var rejectedAuthor = await RegisterAsync("rejected.reviews@example.com");
        var rejected = await WriteAsync(rejectedAuthor, company.Id, Review(1, "Terrible"));
        (await _admin.PostAsJsonAsync($"/api/admin/company-reviews/{rejected.Id}/reject",
            new RejectCompanyReviewRequest("Names a colleague."), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var anonymous = _factory!.CreateClient();
        var response = await anonymous.GetAsync($"/api/companies/public/{company.Slug}/reviews");
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync();

        // The promise on the privacy page, asserted on the wire rather than on a record type.
        raw.ShouldNotContain("userId", Case.Insensitive);
        raw.ShouldNotContain("@example.com");
        raw.ShouldNotContain("Terrible");

        var page = JsonSerializer.Deserialize<PagedResult<CompanyReviewPublicResponse>>(raw, JsonOptions)!;
        var item = page.Items.ShouldHaveSingleItem();
        item.Title.ShouldBe("Honest, slow-moving, fair");
        item.SubmittedMonth.ShouldBe(DateTimeOffset.UtcNow.ToString("yyyy-MM"));
        item.HelpfulCount.ShouldBe(0);

        var directory = await anonymous.GetFromJsonAsync<PagedResult<CompanyPublicListItemResponse>>(
            "/api/companies/public?q=approved%20visibility", JsonOptions);
        directory!.Items.ShouldHaveSingleItem().ApprovedCount.ShouldBe(1);

        var slugs = await anonymous.GetFromJsonAsync<List<ReviewedCompanySlugResponse>>("/api/companies/public/slugs", JsonOptions);
        slugs!.ShouldContain(s => s.Slug == company.Slug);

        // The author still sees the rejection and its reason on their own list.
        var mine = await rejectedAuthor.GetFromJsonAsync<MyReviewsResponse>("/api/company-reviews/mine", JsonOptions);
        var own = mine!.Items.ShouldHaveSingleItem();
        own.Status.ShouldBe(ReviewModerationStatus.Rejected);
        own.RejectionReason.ShouldBe("Names a colleague.");
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
        await ApprovedReviewByAsync("score0.reviews@example.com", elsewhere.Id, 1);

        await ApproveAsync((await WriteAsync(first, company.Id, Review(5))).Id);
        await ApprovedReviewByAsync("score2.reviews@example.com", company.Id, 5);

        var two = await anonymous.GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions);
        two!.Summary.ApprovedCount.ShouldBe(2);
        two.Summary.Score.ShouldBeNull();
        two.Summary.AverageOverall.ShouldBe(5.0);

        await ApprovedReviewByAsync("score3.reviews@example.com", company.Id, 5);

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
    public async Task Editing_An_Approved_Review_Takes_It_Off_The_Page_Until_It_Is_Approved_Again()
    {
        var author = await RegisterAsync("edit.reviews@example.com");
        var company = await ResolveAsync(author, "Edit Cycle Co");
        var review = await WriteAsync(author, company.Id);
        await ApproveAsync(review.Id);
        var anonymous = _factory!.CreateClient();

        (await anonymous.GetFromJsonAsync<PagedResult<CompanyReviewPublicResponse>>(
            $"/api/companies/public/{company.Slug}/reviews", JsonOptions))!.Items.ShouldHaveSingleItem();

        var update = new UpdateCompanyReviewRequest(EmploymentStatus.CurrentEmployee, "Revised after a year",
            "Things improved: the salary band caught up and reviews are quicker now.",
            "Still too many meetings for the size of the team, honestly.", 4, 4, 4, 4, 4);
        var response = await author.PutAsJsonAsync($"/api/company-reviews/{review.Id}", update, JsonOptions);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions);
        updated!.Status.ShouldBe(ReviewModerationStatus.Pending);
        updated.Title.ShouldBe("Revised after a year");

        (await anonymous.GetFromJsonAsync<PagedResult<CompanyReviewPublicResponse>>(
            $"/api/companies/public/{company.Slug}/reviews", JsonOptions))!.Items.ShouldBeEmpty();
        (await anonymous.GetFromJsonAsync<CompanyPublicResponse>($"/api/companies/public/{company.Slug}", JsonOptions))!
            .Summary.ApprovedCount.ShouldBe(0);
    }

    [Fact]
    public async Task Another_Account_Cannot_Edit_Or_Delete_A_Review_And_Learns_Nothing_From_Trying()
    {
        var author = await RegisterAsync("owner.reviews@example.com");
        var company = await ResolveAsync(author, "Ownership Co");
        var review = await WriteAsync(author, company.Id);
        var other = await RegisterAsync("other.reviews@example.com");

        var update = new UpdateCompanyReviewRequest(EmploymentStatus.Intern, "Hijacked title here",
            "This text is long enough to pass validation for sure.",
            "This text is also long enough to pass validation.", 1, 1, 1, 1, 1);

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
        // Not approved yet: not on any page, so "not found".
        (await reader.PostAsync($"/api/company-reviews/{review.Id}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await ApproveAsync(review.Id);

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
        await ApproveAsync(review.Id);

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

        var anonymous = _factory!.CreateClient();
        (await anonymous.GetFromJsonAsync<PagedResult<CompanyReviewPublicResponse>>(
            $"/api/companies/public/{company.Slug}/reviews", JsonOptions))!.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Dismissed_Report_Leaves_The_Review_Published()
    {
        var author = await RegisterAsync("dismiss.author@example.com");
        var company = await ResolveAsync(author, "Dismissed Report Co");
        var review = await WriteAsync(author, company.Id);
        await ApproveAsync(review.Id);
        var reader = await RegisterAsync("dismiss.reader@example.com");

        var report = await (await reader.PostAsJsonAsync($"/api/company-reviews/{review.Id}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.MisleadingInformation), JsonOptions))
            .Content.ReadFromJsonAsync<ReportCompanyReviewResponse>(JsonOptions);

        (await _admin.PostAsJsonAsync($"/api/admin/company-review-reports/{report!.Id}/resolve",
            new ResolveReviewReportRequest(ReviewReportResolution.Dismissed), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detail = await _admin.GetFromJsonAsync<AdminCompanyReviewResponse>($"/api/admin/company-reviews/{review.Id}", JsonOptions);
        detail!.Status.ShouldBe(ReviewModerationStatus.Approved);

        // Dismissed → the reader may report again if the text still bothers them.
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
        var pendingAlpha = await WriteAsync(a, alpha.Id);
        var b = await RegisterAsync("queue.b@example.com");
        var approvedBeta = await WriteAsync(b, beta.Id);
        await ApproveAsync(approvedBeta.Id);

        var pending = await _admin.GetFromJsonAsync<PagedResult<AdminCompanyReviewListItemResponse>>(
            "/api/admin/company-reviews?status=Pending&company=queue", JsonOptions);
        pending!.Items.ShouldContain(r => r.Id == pendingAlpha.Id);
        pending.Items.ShouldNotContain(r => r.Id == approvedBeta.Id);

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
