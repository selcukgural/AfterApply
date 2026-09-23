using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Notifications;
using AfterApply.Application.Notifications.Contracts;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.Common;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Notifications;
using AfterApply.Domain.Occupations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Notifications;

/// <summary>The blog needs somewhere to keep media even when no test uploads any.</summary>
public sealed class ContributionNotificationProfile() : LocalStorageProfile("contribution-notifications")
{
    public override void Configure(IWebHostBuilder builder)
    {
        base.Configure(builder);
        builder.UseSetting("Storage:BlogLocalRootPath", Path.Combine(StorageRoot, "blog-media"));
    }
}

/// <summary>
/// The bell's contribution rows end to end (DECISIONS.md 2026-09-23): a helpful mark on a review,
/// salary, candidate experience or blog comment tells its author — how many that day, never who —
/// a reader toggling on/off/on is counted once, the settings stop rows from being written, a row
/// is the author's alone, it goes quiet when its target leaves the page, and the account's delete,
/// export and the purge job all know about it. Plus the new salary/experience helpful toggles.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class ContributionNotificationTests(ApiHost<ContributionNotificationProfile> host)
    : IClassFixture<ApiHost<ContributionNotificationProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private const string Doc = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Merhaba"}]}]}""";

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        host.Profile.Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- helpers --------------------------------------------------------------------------------

    private async Task<(HttpClient Client, Guid UserId)> RegisterAsync(string email)
    {
        var (client, auth) = await host.RegisterAsync(email);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return (client, auth.User.Id);
    }

    private static async Task<ResolvedCompanyResponse> ResolveAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/companies/resolve", new ResolveCompanyRequest(name), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ResolvedCompanyResponse>(JsonOptions))!;
    }

    private static async Task<Guid> ReviewAsync(HttpClient author, Guid companyId)
    {
        var response = await author.PostAsJsonAsync($"/api/companies/{companyId}/reviews", new CreateCompanyReviewRequest(
            EmploymentStatus.FormerEmployee, 4, [new ReviewCategoryRatingDto(ReviewCategory.WorkEnvironment, 5)],
            ["environment.pos.team_communication"], []), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions))!.Id;
    }

    private static async Task<Guid> SalaryAsync(HttpClient author, Guid companyId)
    {
        var response = await author.PostAsJsonAsync($"/api/companies/{companyId}/salaries", new CompanySalaryRequest(
            Occupation.IdFor("2512"), 4, EmploymentType.FullTime, SalaryEmploymentStatus.CurrentEmployee,
            95_000m, SalaryCurrency.TRY, false, PeriodStartYear: DateTimeOffset.UtcNow.Year), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MyCompanySalaryResponse>(JsonOptions))!.Id;
    }

    private static async Task<Guid> ExperienceAsync(HttpClient author, Guid companyId)
    {
        var response = await author.PostAsJsonAsync($"/api/companies/{companyId}/experiences", new CandidateExperienceRequest(
            4, [new ExperienceCategoryRatingDto(ExperienceCategory.Communication, 5)], ["communication.pos.steps_clear_upfront"], [],
            HiringOutcome.Rejected, ProcessDuration.TwoToFourWeeks, StageCount.Three, [InterviewType.Video]), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<MyCandidateExperienceResponse>(JsonOptions))!.Id;
    }

    private static async Task<HelpfulToggleResponse> MarkAsync(HttpClient reader, string path)
    {
        var response = await reader.PostAsync(path, null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<HelpfulToggleResponse>(JsonOptions))!;
    }

    private static async Task<List<NotificationFeedItemResponse>> FeedAsync(HttpClient client)
    {
        var page = await client.GetFromJsonAsync<PagedResult<NotificationFeedItemResponse>>("/api/notifications?pageSize=50", JsonOptions);
        return page!.Items.ToList();
    }

    private static async Task<int> UnreadAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<NotificationFeedCountResponse>("/api/notifications/count", JsonOptions))!.UnreadCount;

    private static async Task<NotificationPreferencesResponse> SetPreferencesAsync(HttpClient client, UpdateNotificationPreferencesRequest request)
    {
        var response = await client.PutAsJsonAsync("/api/users/me/notification-preferences", request, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<NotificationPreferencesResponse>(JsonOptions))!;
    }

    private static readonly UpdateNotificationPreferencesRequest AllOn = new(true, true, true, true, true, true);

    // ---- reviews: the whole row lifecycle -------------------------------------------------------

    [Fact]
    public async Task A_Review_Mark_Tells_The_Author_Once_Per_Reader_And_Folds_The_Day_Into_One_Row()
    {
        var (author, _) = await RegisterAsync("review.author@example.com");
        var (reader, _) = await RegisterAsync("review.reader@example.com");
        var (second, _) = await RegisterAsync("review.second@example.com");
        var company = await ResolveAsync(author, "Notify Review Co");
        var reviewId = await ReviewAsync(author, company.Id);

        (await FeedAsync(author)).ShouldBeEmpty();

        await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful");
        var feed = await FeedAsync(author);
        var row = feed.ShouldHaveSingleItem();
        row.Kind.ShouldBe(NotificationFeedKind.Contribution);
        row.IsRead.ShouldBeFalse();
        row.Contribution!.Type.ShouldBe(ContributionNotificationType.ReviewHelpful);
        row.Contribution.TargetId.ShouldBe(reviewId);
        row.Contribution.Count.ShouldBe(1);
        row.Contribution.CompanyName.ShouldBe("Notify Review Co");
        row.Contribution.CompanySlug.ShouldBe(company.Slug);
        (await UnreadAsync(author)).ShouldBe(1);

        // Off, then on again: the same reader, so nothing new for the author.
        (await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful")).Marked.ShouldBeFalse();
        (await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful")).Marked.ShouldBeTrue();
        (await FeedAsync(author)).ShouldHaveSingleItem().Contribution!.Count.ShouldBe(1);

        // Read, then a second reader: the same row grows and is unread again.
        (await author.PostAsync("/api/notifications/read", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UnreadAsync(author)).ShouldBe(0);
        (await FeedAsync(author)).Single().IsRead.ShouldBeTrue();

        await MarkAsync(second, $"/api/company-reviews/{reviewId}/helpful");
        var grown = (await FeedAsync(author)).ShouldHaveSingleItem();
        grown.Id.ShouldBe(row.Id);
        grown.Contribution!.Count.ShouldBe(2);
        grown.IsRead.ShouldBeFalse();
        (await UnreadAsync(author)).ShouldBe(1);

        // The readers are told nothing, and the row holds no trace of who marked it.
        (await FeedAsync(reader)).ShouldBeEmpty();
        var json = await author.GetStringAsync("/api/notifications");
        json.ShouldNotContain("review.reader");
        json.ShouldNotContain((await reader.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions))!.Id.ToString());
    }

    [Fact]
    public async Task A_Row_Is_The_Authors_Alone_And_Clearing_It_Is_Idempotent()
    {
        var (author, _) = await RegisterAsync("dismiss.author@example.com");
        var (reader, _) = await RegisterAsync("dismiss.reader@example.com");
        var company = await ResolveAsync(author, "Dismiss Co");
        var reviewId = await ReviewAsync(author, company.Id);
        await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful");
        var row = (await FeedAsync(author)).Single();

        // Someone else's id is not found — not forbidden, not cleared.
        (await reader.PostAsync($"/api/notifications/{row.Id}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await FeedAsync(author)).ShouldHaveSingleItem();

        (await author.PostAsync($"/api/notifications/{row.Id}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await author.PostAsync($"/api/notifications/{row.Id}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FeedAsync(author)).ShouldBeEmpty();
        (await UnreadAsync(author)).ShouldBe(0);

        (await author.PostAsync($"/api/notifications/{Guid.NewGuid()}/dismiss", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await host.CreateClient().GetAsync("/api/notifications")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- salaries and experiences: the new toggles ----------------------------------------------

    [Fact]
    public async Task Salary_Helpful_Counts_Refuses_The_Author_And_Notifies()
    {
        var (author, _) = await RegisterAsync("salary.author@example.com");
        var (reader, _) = await RegisterAsync("salary.reader@example.com");
        var company = await ResolveAsync(author, "Notify Salary Co");
        var entryId = await SalaryAsync(author, company.Id);

        var own = await author.PostAsync($"/api/company-salaries/{entryId}/helpful", null);
        own.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await own.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!.Detail.ShouldBe("Only another person's salary entry can be marked as helpful.");
        (await reader.PostAsync($"/api/company-salaries/{Guid.NewGuid()}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var marked = await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful");
        marked.Marked.ShouldBeTrue();
        marked.HelpfulCount.ShouldBe(1);

        var list = await reader.GetFromJsonAsync<CompanySalaryPageResponse>($"/api/companies/{company.Id}/salaries", JsonOptions);
        list!.Items.Single().HelpfulCount.ShouldBe(1);
        var state = await reader.GetFromJsonAsync<CompanySalaryViewerStateResponse>($"/api/companies/{company.Id}/salaries/me", JsonOptions);
        state!.HelpfulMarkedEntryIds.ShouldBe([entryId]);

        var row = (await FeedAsync(author)).ShouldHaveSingleItem();
        row.Contribution!.Type.ShouldBe(ContributionNotificationType.SalaryHelpful);
        row.Contribution.CompanyName.ShouldBe("Notify Salary Co");

        // Un-marking drops the count (the cached list too) but not the notification.
        (await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful")).HelpfulCount.ShouldBe(0);
        (await reader.GetFromJsonAsync<CompanySalaryPageResponse>($"/api/companies/{company.Id}/salaries", JsonOptions))!
            .Items.Single().HelpfulCount.ShouldBe(0);
        (await FeedAsync(author)).ShouldHaveSingleItem();

        // The author deletes the entry: the row goes quiet rather than linking to nothing.
        (await author.DeleteAsync($"/api/company-salaries/{entryId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FeedAsync(author)).ShouldBeEmpty();
        (await UnreadAsync(author)).ShouldBe(0);
    }

    [Fact]
    public async Task Experience_Helpful_Counts_On_The_Public_List_Refuses_The_Author_And_Notifies()
    {
        var (author, _) = await RegisterAsync("experience.author@example.com");
        var (reader, _) = await RegisterAsync("experience.reader@example.com");
        var company = await ResolveAsync(author, "Notify Experience Co");
        var experienceId = await ExperienceAsync(author, company.Id);

        var own = await author.PostAsync($"/api/candidate-experiences/{experienceId}/helpful", null);
        own.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await own.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!.Detail.ShouldBe("Only another person's candidate experience can be marked as helpful.");
        (await host.CreateClient().PostAsync($"/api/candidate-experiences/{experienceId}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await MarkAsync(reader, $"/api/candidate-experiences/{experienceId}/helpful")).HelpfulCount.ShouldBe(1);

        var list = await host.CreateClient().GetFromJsonAsync<CandidateExperiencePageResponse>(
            $"/api/companies/public/{company.Slug}/experiences", JsonOptions);
        list!.Items.Single().HelpfulCount.ShouldBe(1);
        var state = await reader.GetFromJsonAsync<CandidateExperienceViewerStateResponse>($"/api/companies/{company.Id}/experiences/me", JsonOptions);
        state!.HelpfulMarkedExperienceIds.ShouldBe([experienceId]);

        var row = (await FeedAsync(author)).ShouldHaveSingleItem();
        row.Contribution!.Type.ShouldBe(ContributionNotificationType.ExperienceHelpful);
        row.Contribution.CompanySlug.ShouldBe(company.Slug);
    }

    // ---- blog comments --------------------------------------------------------------------------

    [Fact]
    public async Task A_Blog_Comment_Mark_Notifies_With_The_Post_But_A_Self_Vote_Does_Not()
    {
        var (admin, adminId) = await RegisterAsync("notify.admin@example.com");
        await host.MakeAdminAsync(adminId);
        var created = await admin.PostAsJsonAsync("/api/admin/blog/posts",
            new CreateBlogPostRequest("Mülakat Sonrası Sessizlik", null, Doc, "<p>Merhaba</p>", "tr", null, null), JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var post = (await created.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
        var published = await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/publish", null);
        published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        post = (await published.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;

        var (author, _) = await RegisterAsync("comment.author@example.com");
        var (reader, _) = await RegisterAsync("comment.reader@example.com");
        var commented = await author.PostAsJsonAsync($"/api/blog/posts/{post.Id}/comments", new CreateBlogCommentRequest("Aynen öyle."), JsonOptions);
        commented.StatusCode.ShouldBe(HttpStatusCode.Created, await commented.Content.ReadAsStringAsync());
        var comment = (await commented.Content.ReadFromJsonAsync<BlogCommentResponse>(JsonOptions))!;
        (await admin.PostAsync($"/api/admin/blog/comments/{comment.Id}/approve", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // The comment's author may vote on it (the blog allows it) but is not told about themselves.
        (await author.PostAsync($"/api/blog/comments/{comment.Id}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await FeedAsync(author)).ShouldBeEmpty();

        (await reader.PostAsync($"/api/blog/comments/{comment.Id}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var row = (await FeedAsync(author)).ShouldHaveSingleItem();
        row.Contribution!.Type.ShouldBe(ContributionNotificationType.BlogCommentHelpful);
        row.Contribution.BlogPostTitle.ShouldBe("Mülakat Sonrası Sessizlik");
        row.Contribution.BlogPostSlug.ShouldBe(post.Slug);
        row.Contribution.BlogPostLanguage.ShouldBe("tr");
        row.Contribution.CompanyName.ShouldBeNull();
    }

    // ---- settings ---------------------------------------------------------------------------------

    [Fact]
    public async Task Settings_Stop_Rows_Being_Written_And_A_Reader_Counted_While_Off_Stays_Counted()
    {
        var (author, _) = await RegisterAsync("settings.author@example.com");
        var (reader, _) = await RegisterAsync("settings.reader@example.com");
        var (later, _) = await RegisterAsync("settings.later@example.com");
        var company = await ResolveAsync(author, "Settings Co");
        var reviewId = await ReviewAsync(author, company.Id);
        var entryId = await SalaryAsync(author, company.Id);

        var defaults = await author.GetFromJsonAsync<NotificationPreferencesResponse>("/api/users/me/notification-preferences", JsonOptions);
        defaults.ShouldBe(new NotificationPreferencesResponse(true, true, true, true, true, true));

        // Salary off: a salary mark writes nothing, a review mark still does.
        var saved = await SetPreferencesAsync(author, AllOn with { SalaryHelpful = false });
        saved.SalaryHelpful.ShouldBeFalse();
        await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful");
        await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful");
        (await FeedAsync(author)).Select(r => r.Contribution!.Type).ShouldBe([ContributionNotificationType.ReviewHelpful]);

        // Back on: the reader who marked while it was off does not arrive now by re-toggling...
        await SetPreferencesAsync(author, AllOn);
        await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful");
        await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful");
        (await FeedAsync(author)).ShouldHaveSingleItem();

        // ...but a new reader does.
        await MarkAsync(later, $"/api/company-salaries/{entryId}/helpful");
        (await FeedAsync(author)).Count.ShouldBe(2);

        // The master switch silences every kind, whatever the kind says.
        (await SetPreferencesAsync(author, AllOn with { Contributions = false })).Contributions.ShouldBeFalse();
        await MarkAsync(later, $"/api/company-reviews/{reviewId}/helpful");
        (await FeedAsync(author)).Single(r => r.Contribution!.Type == ContributionNotificationType.ReviewHelpful).Contribution!.Count.ShouldBe(1);

        // Settings are the caller's own.
        (await reader.GetFromJsonAsync<NotificationPreferencesResponse>("/api/users/me/notification-preferences", JsonOptions))!
            .Contributions.ShouldBeTrue();
        (await host.CreateClient().GetAsync("/api/users/me/notification-preferences")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Clearing_All_Clears_Only_The_Callers_Rows_And_A_Hidden_Review_Goes_Quiet()
    {
        var (author, _) = await RegisterAsync("clear.author@example.com");
        var (other, _) = await RegisterAsync("clear.other@example.com");
        var (reader, _) = await RegisterAsync("clear.reader@example.com");
        var company = await ResolveAsync(author, "Clear Co");
        var reviewId = await ReviewAsync(author, company.Id);
        var otherReviewId = await ReviewAsync(other, company.Id);
        var entryId = await SalaryAsync(author, company.Id);
        await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful");
        await MarkAsync(reader, $"/api/company-reviews/{otherReviewId}/helpful");
        await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful");

        (await author.PostAsync("/api/notifications/dismiss-all", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FeedAsync(author)).ShouldBeEmpty();
        (await FeedAsync(other)).ShouldHaveSingleItem();

        // Taken down by moderation: the other author's row is no longer listed or counted.
        await host.WithDbAsync(async db =>
        {
            var review = await db.CompanyReviews.SingleAsync(r => r.Id == otherReviewId);
            review.Reject(Guid.CreateVersion7(), "spam", DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        });
        (await FeedAsync(other)).ShouldBeEmpty();
        (await UnreadAsync(other)).ShouldBe(0);
    }

    // ---- account: delete, export, purge ------------------------------------------------------------

    [Fact]
    public async Task The_Export_Carries_Rows_Marks_Ledger_And_Settings()
    {
        var (author, _) = await RegisterAsync("export.author@example.com");
        var (reader, _) = await RegisterAsync("export.reader@example.com");
        var company = await ResolveAsync(author, "Export Notify Co");
        var entryId = await SalaryAsync(author, company.Id);
        var experienceId = await ExperienceAsync(author, company.Id);
        await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful");
        await MarkAsync(reader, $"/api/candidate-experiences/{experienceId}/helpful");
        await SetPreferencesAsync(author, AllOn with { GmailUpdates = false });

        var authorExport = await (await author.GetAsync("/api/users/me/export")).Content.ReadFromJsonAsync<AccountExportResponse>(JsonOptions);
        authorExport!.ContributionNotifications!.Select(n => n.Type).OrderBy(t => t)
            .ShouldBe([ContributionNotificationType.SalaryHelpful, ContributionNotificationType.ExperienceHelpful]);
        authorExport.NotificationPreferences!.GmailUpdates.ShouldBeFalse();
        authorExport.HelpfulMarksCounted.ShouldBeEmpty();

        var readerExport = await (await reader.GetAsync("/api/users/me/export")).Content.ReadFromJsonAsync<AccountExportResponse>(JsonOptions);
        readerExport!.HelpfulMarkedSalaryIds.ShouldBe([entryId]);
        readerExport.HelpfulMarkedExperienceIds.ShouldBe([experienceId]);
        readerExport.HelpfulMarksCounted!.Select(m => m.TargetId).OrderBy(id => id).ShouldBe(new[] { entryId, experienceId }.OrderBy(id => id));
        readerExport.ContributionNotifications.ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleting_An_Account_Takes_Its_Rows_Its_Marks_And_Its_Ledger_Entries()
    {
        var (author, authorId) = await RegisterAsync("delete.author@example.com");
        var (reader, readerId) = await RegisterAsync("delete.reader@example.com");
        var company = await ResolveAsync(author, "Delete Notify Co");
        var entryId = await SalaryAsync(author, company.Id);
        var experienceId = await ExperienceAsync(author, company.Id);
        await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful");
        await MarkAsync(reader, $"/api/candidate-experiences/{experienceId}/helpful");

        await DeleteAccountAsync(reader);
        await host.WithDbAsync(async db =>
        {
            (await db.CompanySalaryHelpfulMarks.AnyAsync(m => m.UserId == readerId)).ShouldBeFalse();
            (await db.CandidateExperienceHelpfulMarks.AnyAsync(m => m.UserId == readerId)).ShouldBeFalse();
            (await db.HelpfulNotificationLedger.AnyAsync(e => e.VoterUserId == readerId)).ShouldBeFalse();
            // The author keeps the news: it was theirs, and it never named the reader.
            (await db.ContributionNotifications.CountAsync(n => n.UserId == authorId)).ShouldBe(2);
        });

        await DeleteAccountAsync(author);
        await host.WithDbAsync(async db =>
            (await db.ContributionNotifications.AnyAsync(n => n.UserId == authorId)).ShouldBeFalse());
    }

    [Fact]
    public async Task The_Purge_Drops_Old_Read_Or_Cleared_Rows_And_Old_Ledger_Entries_But_Keeps_Unread_Ones()
    {
        var (author, authorId) = await RegisterAsync("purge.author@example.com");
        var (reader, _) = await RegisterAsync("purge.reader@example.com");
        var company = await ResolveAsync(author, "Purge Co");
        var reviewId = await ReviewAsync(author, company.Id);
        var entryId = await SalaryAsync(author, company.Id);
        var experienceId = await ExperienceAsync(author, company.Id);
        await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful");
        await MarkAsync(reader, $"/api/company-salaries/{entryId}/helpful");
        await MarkAsync(reader, $"/api/candidate-experiences/{experienceId}/helpful");

        var old = DateTimeOffset.UtcNow.AddDays(-120);
        await host.WithDbAsync(async db =>
        {
            // Review: old and read. Salary: old and unread. Experience: read but recent.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "ContributionNotifications" SET "LastEventAt" = {old}, "ReadAt" = {old} WHERE "TargetId" = {reviewId}""");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "ContributionNotifications" SET "LastEventAt" = {old} WHERE "TargetId" = {entryId}""");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "ContributionNotifications" SET "ReadAt" = now() WHERE "TargetId" = {experienceId}""");
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE "HelpfulNotificationLedger" SET "CountedAt" = {old} WHERE "TargetId" = {reviewId}""");
        });

        await host.WithScopeAsync(async services =>
            (await services.GetRequiredService<IContributionNotificationRetentionService>().PurgeAsync(CancellationToken.None)).ShouldBe(2));

        await host.WithDbAsync(async db =>
        {
            (await db.ContributionNotifications.Where(n => n.UserId == authorId).Select(n => n.TargetId).ToListAsync())
                .OrderBy(id => id).ShouldBe(new[] { entryId, experienceId }.OrderBy(id => id));
            (await db.HelpfulNotificationLedger.AnyAsync(e => e.TargetId == reviewId)).ShouldBeFalse();
        });

        // Past the window, the same reader re-marking is news again.
        await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful");
        await MarkAsync(reader, $"/api/company-reviews/{reviewId}/helpful");
        (await FeedAsync(author)).Count(r => r.Contribution!.TargetId == reviewId).ShouldBe(1);
    }

    private static async Task DeleteAccountAsync(HttpClient client)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(ApiHost.DefaultPassword), options: JsonOptions)
        };
        (await client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
