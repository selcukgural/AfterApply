using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.CandidateExperiences.Contracts;
using AfterApply.Application.CompanySalaries.Contracts;
using AfterApply.Application.Feedback.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Pro;
using AfterApply.Application.TrackedJobs.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Auditing;
using AfterApply.Domain.Common;
using AfterApply.Domain.Companies;
using AfterApply.Domain.CompanyReviews;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.CompanySalaries;
using AfterApply.Domain.Occupations;
using AfterApply.Domain.Feedback;
using AfterApply.Domain.Imports;
using AfterApply.Domain.Notifications;
using AfterApply.Domain.TrackedJobs;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using DomainReminder = AfterApply.Domain.Notifications.Reminder;

namespace AfterApply.IntegrationTests.Identity;

[Collection(IntegrationTestCollection.Name)]
public class AccountManagementTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> RegisterAsync(string email, bool consentAccepted = true)
    {
        var client = _factory!.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, _factory!.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Account", "Test", consentAccepted));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateApplicationAsync(HttpClient client, string companyName)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private static Task<HttpResponseMessage> DeleteAccountAsync(HttpClient client, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(password), options: JsonOptions)
        };
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Register_Without_Consent_Is_Rejected()
    {
        var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("noconsent@example.com", "P@ssw0rd123!", "No", "Consent", false), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>The sign-up form stopped asking for a name on 2026-09-14; the API has to take the
    /// empty strings it now sends and hand them back as such, not fail on the NOT NULL column.</summary>
    [Fact]
    public async Task Register_Without_A_Name_Creates_The_Account_With_Empty_Names()
    {
        var client = _factory!.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("noname@example.com", "P@ssw0rd123!", "", "", true), JsonOptions);
        register.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var pending = await register.Content.ReadFromJsonAsync<EmailVerificationPendingResponse>(JsonOptions);
        pending!.Email.ShouldBe("noname@example.com");

        // Sign-up hands out no tokens until the code comes back; stand in for the inbox and sign in.
        await TestAccounts.MarkVerifiedAsync(_factory.Services, "noname@example.com");
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("noname@example.com", "P@ssw0rd123!"), JsonOptions);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth!.User.FirstName.ShouldBe("");
        auth.User.LastName.ShouldBe("");

        client.DefaultRequestHeaders.Authorization = new("Bearer", auth.AccessToken);
        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions);
        profile!.FirstName.ShouldBe("");
        profile.LastName.ShouldBe("");
    }

    // ── Profile page: name editing and the plan line ────────────────────────────────────────

    [Fact]
    public async Task Update_Profile_Changes_The_Name_And_Keeps_The_Email()
    {
        var client = await RegisterAsync("rename@example.com");

        var response = await client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest("  Ada ", "Lovelace  "), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);
        updated!.FirstName.ShouldBe("Ada");
        updated.LastName.ShouldBe("Lovelace");
        updated.Email.ShouldBe("rename@example.com");

        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions);
        profile!.FirstName.ShouldBe("Ada");
        profile.LastName.ShouldBe("Lovelace");
        profile.Email.ShouldBe("rename@example.com");
    }

    /// <summary>Same rule as sign-up since 2026-09-14: a name is optional, so the profile page can
    /// save one half or clear both without the request bouncing.</summary>
    [Fact]
    public async Task Update_Profile_Accepts_Empty_Names()
    {
        var client = await RegisterAsync("clearname@example.com");

        var response = await client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest("", ""), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions);
        profile!.FirstName.ShouldBe("");
        profile.LastName.ShouldBe("");
    }

    [Fact]
    public async Task Update_Profile_Rejects_A_Name_Over_The_Column_Width()
    {
        var client = await RegisterAsync("longname@example.com");

        var response = await client.PutAsJsonAsync("/api/users/me", new UpdateProfileRequest(new string('a', 101), "Ok"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var profile = await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions);
        profile!.FirstName.ShouldBe("Account");
    }

    [Fact]
    public async Task Plan_Requires_Authentication()
    {
        var client = _factory!.CreateClient();

        var response = await client.GetAsync("/api/users/me/plan");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Plan_Is_Inactive_Without_An_Entitlement()
    {
        var client = await RegisterAsync("noplan@example.com");

        var plan = await client.GetFromJsonAsync<UserPlanResponse>("/api/users/me/plan", JsonOptions);

        plan!.IsActive.ShouldBeFalse();
        plan.ActiveUntil.ShouldBeNull();
    }

    [Fact]
    public async Task Plan_Is_Active_With_Its_End_Date_After_A_Grant()
    {
        var client = await RegisterAsync("proplan@example.com");
        var activeUntil = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(30).Date, TimeSpan.Zero);
        await GrantProAsync("proplan@example.com", activeUntil);

        var plan = await client.GetFromJsonAsync<UserPlanResponse>("/api/users/me/plan", JsonOptions);

        plan!.IsActive.ShouldBeTrue();
        plan.ActiveUntil.ShouldBe(activeUntil);
    }

    /// <summary>The page says "Pro ended on …", so an expired period keeps its end date on the wire
    /// while reading as inactive.</summary>
    [Fact]
    public async Task Plan_Keeps_The_End_Date_Of_An_Expired_Period()
    {
        var client = await RegisterAsync("expired@example.com");
        var endedAt = new DateTimeOffset(DateTimeOffset.UtcNow.AddDays(-10).Date, TimeSpan.Zero);
        await GrantProAsync("expired@example.com", endedAt);

        var plan = await client.GetFromJsonAsync<UserPlanResponse>("/api/users/me/plan", JsonOptions);

        plan!.IsActive.ShouldBeFalse();
        plan.ActiveUntil.ShouldBe(endedAt);
    }

    private async Task GrantProAsync(string email, DateTimeOffset activeUntil)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        await scope.ServiceProvider.GetRequiredService<IProEntitlementService>().GrantAsync(userId, activeUntil, CancellationToken.None);
    }

    [Fact]
    public async Task Register_With_Consent_Persists_ConsentAcceptedAt()
    {
        var beforeRegister = DateTimeOffset.UtcNow;
        var client = await RegisterAsync("consent@example.com");

        var response = await client.GetAsync("/api/users/me");
        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions);

        profile.ShouldNotBeNull();
        profile!.ConsentAcceptedAt.ShouldBeGreaterThanOrEqualTo(beforeRegister);
    }

    [Fact]
    public async Task DeleteAccount_With_Wrong_Password_Rejects_And_Deletes_Nothing()
    {
        var client = await RegisterAsync("wrongpw.delete@example.com");
        await CreateApplicationAsync(client, "Wrong Password Co");

        var deleteResponse = await DeleteAccountAsync(client, "NotTheRightPassword!");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var profileResponse = await client.GetAsync("/api/users/me");
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DeleteAccount_Cascades_Own_Data_But_Preserves_Shared_Companies_And_Other_Users_Data()
    {
        var userA = await RegisterAsync("usera.delete@example.com");
        var applicationId = await CreateApplicationAsync(userA, "Shared Co");
        await userA.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(ApplicationStatus.Screening, "note", null), JsonOptions);

        // TrackedJobs and FeedbackEntries are the tables the old hand-written sweep in
        // DeleteAccountAsync never covered — they are here because they were the bug.
        (await userA.PostAsJsonAsync("/api/tracked-jobs",
            new CreateTrackedJobRequest("Watchlist Co", "Staff Engineer", null, null, null), JsonOptions))
            .EnsureSuccessStatusCode();
        (await userA.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Idea, "Group my applications by company."), JsonOptions))
            .EnsureSuccessStatusCode();

        // Same company name -> resolver reuses the same Company row (Sprint 4/5 dedup) -> proves it survives deletion.
        var userB = await RegisterAsync("userb.delete@example.com");
        var userBApplicationId = await CreateApplicationAsync(userB, "Shared Co");

        // Company reviews in all three roles: A wrote one, A reported B's, A marked B's helpful.
        var sharedCompanyId = await CompanyIdOfApplicationAsync(applicationId);
        var reviewByA = await WriteReviewAsync(userA, sharedCompanyId);
        var reviewByB = await WriteReviewAsync(userB, sharedCompanyId);
        await ApproveReviewAsync(reviewByB);
        (await userA.PostAsync($"/api/company-reviews/{reviewByB}/helpful", null)).EnsureSuccessStatusCode();
        (await userA.PostAsJsonAsync($"/api/company-reviews/{reviewByB}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.Spam), JsonOptions)).EnsureSuccessStatusCode();
        var salaryByA = await ShareSalaryAsync(userA, sharedCompanyId);
        var salaryByB = await ShareSalaryAsync(userB, sharedCompanyId);
        var experienceByA = await ShareExperienceAsync(userA, sharedCompanyId);
        var experienceByB = await ShareExperienceAsync(userB, sharedCompanyId);

        Guid userAId;
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userAApplication = await db.Applications.SingleAsync(a => a.Id == applicationId);
            userAId = userAApplication.UserId;

            db.ImportBatches.Add(ImportBatch.Create(userAId, Source.CsvImport, "seed.csv", DateTimeOffset.UtcNow));
            db.Reminders.Add(DomainReminder.Create(userAId, applicationId, ReminderType.FollowUp,
                DateTimeOffset.UtcNow.AddDays(-8), 8, DateTimeOffset.UtcNow));

            await db.SaveChangesAsync();
        }

        var deleteResponse = await DeleteAccountAsync(userA, "P@ssw0rd123!");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            (await db.Users.AnyAsync(u => u.Id == userAId)).ShouldBeFalse();
            (await db.Applications.AnyAsync(a => a.UserId == userAId)).ShouldBeFalse();
            (await db.ApplicationEvents.AnyAsync(e => e.ApplicationId == applicationId)).ShouldBeFalse();
            (await db.ApplicationStatusHistories.AnyAsync(h => h.ApplicationId == applicationId)).ShouldBeFalse();
            (await db.Reminders.AnyAsync(r => r.UserId == userAId)).ShouldBeFalse();
            (await db.ImportBatches.AnyAsync(b => b.UserId == userAId)).ShouldBeFalse();
            (await db.RefreshTokens.AnyAsync(rt => rt.UserId == userAId)).ShouldBeFalse();
            (await db.TrackedJobs.AnyAsync(t => t.UserId == userAId)).ShouldBeFalse();
            (await db.FeedbackEntries.AnyAsync(f => f.UserId == userAId)).ShouldBeFalse();
            (await db.CompanyReviews.AnyAsync(r => r.Id == reviewByA)).ShouldBeFalse();
            (await db.CompanyReviewCategoryRatings.AnyAsync(c => c.ReviewId == reviewByA)).ShouldBeFalse();
            (await db.CompanyReviewStatementPicks.AnyAsync(c => c.ReviewId == reviewByA)).ShouldBeFalse();
            (await db.CompanyReviewReports.AnyAsync(p => p.ReporterUserId == userAId)).ShouldBeFalse();
            (await db.CompanyReviewHelpfulMarks.AnyAsync(m => m.UserId == userAId)).ShouldBeFalse();
            (await db.CompanySalaryEntries.AnyAsync(e => e.Id == salaryByA)).ShouldBeFalse();
            (await db.CandidateExperiences.AnyAsync(e => e.Id == experienceByA)).ShouldBeFalse();
            (await db.CandidateExperienceCategoryRatings.AnyAsync(c => c.ExperienceId == experienceByA)).ShouldBeFalse();
            (await db.CandidateExperienceStatementPicks.AnyAsync(c => c.ExperienceId == experienceByA)).ShouldBeFalse();
            (await db.CandidateExperienceInterviewTypes.AnyAsync(c => c.ExperienceId == experienceByA)).ShouldBeFalse();
            // B's review, B's salary entry and the company they are about are untouched.
            (await db.CompanyReviews.AnyAsync(r => r.Id == reviewByB)).ShouldBeTrue();
            (await db.CompanySalaryEntries.AnyAsync(e => e.Id == salaryByB)).ShouldBeTrue();
            (await db.CandidateExperiences.AnyAsync(e => e.Id == experienceByB)).ShouldBeTrue();

            // Shared Company must survive - user B's application still references it.
            var userBApplication = await db.Applications.SingleAsync(a => a.Id == userBApplicationId);
            (await db.Companies.AnyAsync(c => c.Id == userBApplication.CompanyId)).ShouldBeTrue();
        }

        var profileResponse = await userA.GetAsync("/api/users/me");
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // The point of the cascade is that "deleting an account deletes its data" stops depending on
    // anyone remembering to add a line to DeleteAccountAsync. That only holds if the constraint is
    // really in the schema, so this asserts the schema rather than the code path: a row that
    // belongs to nobody must be impossible to write in the first place.
    [Fact]
    public async Task A_User_Owned_Row_Cannot_Be_Written_Without_A_User()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Its own Company rather than whatever another test left behind — xUnit makes no promise
        // about the order tests inside a class run in.
        var company = Company.Create("Orphan Check Co", DateTimeOffset.UtcNow);
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        db.TrackedJobs.Add(TrackedJob.Create(Guid.CreateVersion7(), company.Id, "Orphan Engineer",
            null, null, null, DateTimeOffset.UtcNow));

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.CompanyReviews.Add(CompanyReview.CreateLegacy(Guid.CreateVersion7(), company.Id,
            new ReviewContent(EmploymentStatus.Intern, "Orphan review", "Long enough pros for the check.",
                "Long enough cons for the check.", 3, 3, 3, 3, 3), DateTimeOffset.UtcNow));

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        // The request audit is the one table where "nobody" is a legitimate owner (an anonymous
        // scan, a failed sign-in): null passes, a made-up user still cannot.
        db.RequestAudits.Add(RequestAudit.Create(Guid.CreateVersion7(), "POST", "/api/orphan", 200, null, DateTimeOffset.UtcNow));

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();

        db.RequestAudits.Add(RequestAudit.Create(null, "POST", "/api/anonymous", 200, null, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ExportAccountData_Returns_Own_Applications_ImportBatches_And_Reminders()
    {
        var client = await RegisterAsync("export.test@example.com");
        var applicationId = await CreateApplicationAsync(client, "Export Co");
        await client.PostAsJsonAsync($"/api/applications/{applicationId}/status",
            new ChangeStatusRequest(ApplicationStatus.Screening, "note", null), JsonOptions);

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var application = await db.Applications.SingleAsync(a => a.Id == applicationId);

            db.ImportBatches.Add(ImportBatch.Create(application.UserId, Source.CsvImport, "export-seed.csv", DateTimeOffset.UtcNow));
            db.Reminders.Add(DomainReminder.Create(application.UserId, applicationId, ReminderType.FollowUp,
                DateTimeOffset.UtcNow.AddDays(-8), 8, DateTimeOffset.UtcNow));

            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync("/api/users/me/export");
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");

        var export = await response.Content.ReadFromJsonAsync<AccountExportResponse>(JsonOptions);

        export.ShouldNotBeNull();
        export!.Profile.Email.ShouldBe("export.test@example.com");

        var applicationExport = export.Applications.ShouldHaveSingleItem();
        applicationExport.Id.ShouldBe(applicationId);
        applicationExport.CompanyName.ShouldBe("Export Co");
        applicationExport.StatusHistory.ShouldContain(h => h.ToStatus == ApplicationStatus.Screening);
        applicationExport.Events.ShouldContain(e => e.Type == ApplicationEventType.StatusChanged);

        export.ImportBatches.ShouldHaveSingleItem().FileName.ShouldBe("export-seed.csv");
        export.Reminders.ShouldHaveSingleItem().Type.ShouldBe(ReminderType.FollowUp);
    }

    [Fact]
    public async Task ExportAccountData_Includes_Company_Reviews_Reports_And_Helpful_Marks()
    {
        var author = await RegisterAsync("export.reviews@example.com");
        var other = await RegisterAsync("export.reviews.other@example.com");
        var companyId = await CompanyIdOfApplicationAsync(await CreateApplicationAsync(author, "Export Review Co"));

        await WriteReviewAsync(author, companyId);
        var othersReview = await WriteReviewAsync(other, companyId);
        await ApproveReviewAsync(othersReview);
        (await author.PostAsync($"/api/company-reviews/{othersReview}/helpful", null)).EnsureSuccessStatusCode();
        (await author.PostAsJsonAsync($"/api/company-reviews/{othersReview}/reports",
            new ReportCompanyReviewRequest(ReviewReportReason.Advertising), JsonOptions)).EnsureSuccessStatusCode();

        // A legacy row, seeded the way the migration left them: its text must come back to its
        // author even though nobody else sees it any more.
        var legacyCompanyId = await CompanyIdOfApplicationAsync(await CreateApplicationAsync(author, "Export Legacy Co"));
        await SeedLegacyReviewAsync(author, legacyCompanyId);

        var export = await (await author.GetAsync("/api/users/me/export")).Content.ReadFromJsonAsync<AccountExportResponse>(JsonOptions);

        export!.CompanyReviews.ShouldNotBeNull().Count.ShouldBe(2);
        var review = export.CompanyReviews.Single(r => r.CompanyName == "Export Review Co");
        review.Format.ShouldBe(ReviewFormat.Structured);
        review.Status.ShouldBe(ReviewModerationStatus.Approved);
        review.Title.ShouldBeNull();
        review.CategoryRatings.ShouldContain(c => c.Category == ReviewCategory.Management && c.Rating == 3);
        review.LikedStatements.ShouldBe(["environment.pos.team_communication"]);
        review.ImprovableStatements.ShouldBe(["career.imp.promotion_transparency"]);
        var legacy = export.CompanyReviews.Single(r => r.CompanyName == "Export Legacy Co");
        legacy.Format.ShouldBe(ReviewFormat.Legacy);
        legacy.Title.ShouldBe("Fine place, on balance");
        legacy.Pros.ShouldNotBeNullOrEmpty();
        legacy.ManagementRating.ShouldBe(3);
        export.CompanyReviewReports.ShouldNotBeNull().ShouldHaveSingleItem().Reason.ShouldBe(ReviewReportReason.Advertising);
        export.HelpfulMarkedReviewIds.ShouldNotBeNull().ShouldBe([othersReview]);
    }

    [Fact]
    public async Task ExportAccountData_Includes_Salary_Entries_With_The_Exact_Years()
    {
        var author = await RegisterAsync("export.salaries@example.com");
        var companyId = await CompanyIdOfApplicationAsync(await CreateApplicationAsync(author, "Export Salary Co"));
        await ShareSalaryAsync(author, companyId);

        var export = await (await author.GetAsync("/api/users/me/export")).Content.ReadFromJsonAsync<AccountExportResponse>(JsonOptions);

        var entry = export!.CompanySalaries.ShouldNotBeNull().ShouldHaveSingleItem();
        entry.CompanyName.ShouldBe("Export Salary Co");
        entry.OccupationCode.ShouldBe("2512");
        entry.OccupationNameEn.ShouldBe("Software Developers");
        entry.YearsOfExperience.ShouldBe(6);
        entry.MonthlyNetAmount.ShouldBe(95_000m);
        entry.Currency.ShouldBe(SalaryCurrency.TRY);
        entry.AnnualBonusAmount.ShouldBe(120_000m);
    }

    [Fact]
    public async Task ExportAccountData_Includes_Candidate_Experiences_With_Their_Picks()
    {
        var author = await RegisterAsync("export.experiences@example.com");
        var companyId = await CompanyIdOfApplicationAsync(await CreateApplicationAsync(author, "Export Experience Co"));
        await ShareExperienceAsync(author, companyId);

        var export = await (await author.GetAsync("/api/users/me/export")).Content.ReadFromJsonAsync<AccountExportResponse>(JsonOptions);

        var entry = export!.CandidateExperiences.ShouldNotBeNull().ShouldHaveSingleItem();
        entry.CompanyName.ShouldBe("Export Experience Co");
        entry.OverallRating.ShouldBe(4);
        entry.CategoryRatings.ShouldHaveSingleItem().Category.ShouldBe(ExperienceCategory.Communication);
        entry.LikedStatements.ShouldBe(["communication.pos.steps_clear_upfront"]);
        entry.ImprovableStatements.ShouldBe(["outcome.imp.notification"]);
        entry.Outcome.ShouldBe(HiringOutcome.Rejected);
        entry.InterviewTypes.ShouldBe([InterviewType.Video]);
    }

    private async Task<Guid> ShareExperienceAsync(HttpClient client, Guid companyId)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/experiences",
            new CandidateExperienceRequest(4, [new ExperienceCategoryRatingDto(ExperienceCategory.Communication, 5)],
                ["communication.pos.steps_clear_upfront"], ["outcome.imp.notification"], HiringOutcome.Rejected,
                ProcessDuration.TwoToFourWeeks, StageCount.Three, [InterviewType.Video]), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCandidateExperienceResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> ShareSalaryAsync(HttpClient client, Guid companyId)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/salaries",
            new CompanySalaryRequest(Occupation.IdFor("2512"), 6, EmploymentType.FullTime, SalaryEmploymentStatus.CurrentEmployee,
                95_000m, SalaryCurrency.TRY, true, 120_000m, PeriodStartYear: 2024), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<MyCompanySalaryResponse>(JsonOptions))!.Id;
    }

    private async Task<Guid> CompanyIdOfApplicationAsync(Guid applicationId)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Applications.SingleAsync(a => a.Id == applicationId)).CompanyId;
    }

    private static async Task<Guid> WriteReviewAsync(HttpClient client, Guid companyId)
    {
        var response = await client.PostAsJsonAsync($"/api/companies/{companyId}/reviews", new CreateCompanyReviewRequest(
            EmploymentStatus.FormerEmployee, 4,
            [new ReviewCategoryRatingDto(ReviewCategory.Management, 3), new ReviewCategoryRatingDto(ReviewCategory.WorkEnvironment, 4)],
            ["environment.pos.team_communication"],
            ["career.imp.promotion_transparency"]), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MyCompanyReviewResponse>(JsonOptions))!.Id;
    }

    /// <summary>A pre-2026-09-16 row, written the only way one can be written now: straight into
    /// the database, as the migration left them.</summary>
    private async Task SeedLegacyReviewAsync(HttpClient client, Guid companyId)
    {
        var userId = (await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions))!.Id;
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CompanyReviews.Add(CompanyReview.CreateLegacy(userId, companyId, new ReviewContent(
            EmploymentStatus.FormerEmployee, "Fine place, on balance",
            "Good people and a sane on-call rotation for a change.",
            "Promotions depend on who your manager knows upstairs.", 4, 3, 4, 3, 3), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    /// <summary>Approves straight in the database: this class is about the account, not the
    /// moderation surface, which has its own tests. A no-op for a structured review, which is
    /// published on save; kept for the legacy seeds.</summary>
    private async Task ApproveReviewAsync(Guid reviewId)
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var review = await db.CompanyReviews.SingleAsync(r => r.Id == reviewId);
        review.Approve(Guid.CreateVersion7(), DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Login_Rate_Limit_Rejects_Requests_Beyond_The_Threshold()
    {
        // The suite disables rate limiting for every host (TestContainerCleanup); this is the one
        // test that asserts a 429, so it opts back in — on a host of its own, because a limiter's
        // fixed windows have no reset and would carry six failed logins into the next test.
        await using var limited = host.Standalone(builder => builder.UseSetting("RateLimiting:Enabled", "true"));
        var client = limited.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest("no-such-user@example.com", "whatever"), JsonOptions);
        }

        lastResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
