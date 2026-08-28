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
using AfterApply.Domain.Imports;
using AfterApply.Domain.Notifications;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using DomainReminder = AfterApply.Domain.Notifications.Reminder;

namespace AfterApply.IntegrationTests.Identity;

public class AccountManagementTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private async Task<HttpClient> RegisterAsync(string email, bool consentAccepted = true)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Account", "Test", consentAccepted), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
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

        // Same company name -> resolver reuses the same Company row (Sprint 4/5 dedup) -> proves it survives deletion.
        var userB = await RegisterAsync("userb.delete@example.com");
        var userBApplicationId = await CreateApplicationAsync(userB, "Shared Co");

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

            // Shared Company must survive - user B's application still references it.
            var userBApplication = await db.Applications.SingleAsync(a => a.Id == userBApplicationId);
            (await db.Companies.AnyAsync(c => c.Id == userBApplication.CompanyId)).ShouldBeTrue();
        }

        var profileResponse = await userA.GetAsync("/api/users/me");
        profileResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
    public async Task Login_Rate_Limit_Rejects_Requests_Beyond_The_Threshold()
    {
        var client = _factory!.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest("no-such-user@example.com", "whatever"), JsonOptions);
        }

        lastResponse!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
