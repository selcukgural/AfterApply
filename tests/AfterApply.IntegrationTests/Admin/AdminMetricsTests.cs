using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Metrics;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Admin;

/// <summary>
/// The internal product-metrics surface: who can read it, and that the daily job leaves exactly one
/// row per day behind. Before 2026-09-07 the job computed these numbers and wrote them only to a log
/// line, so nothing could be compared against last week — see DEVELOPMENT_PLAN.md, K5.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class AdminMetricsTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private const string AdminEmail = "admin.metrics@ekariyerim.com";
    private const string OrdinaryEmail = "ordinary.metrics@example.com";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _adminClient = null!;
    private HttpClient _ordinaryClient = null!;
    private HttpClient _anonymousClient = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(AdminMetricsTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        _adminClient = await CreateAuthenticatedClientAsync(AdminEmail);
        _ordinaryClient = await CreateAuthenticatedClientAsync(OrdinaryEmail);
        _anonymousClient = _factory.CreateClient();

        // Granted the same way production grants it — an UPDATE against the column, with no config
        // and no restart. Note this runs *after* the token above was issued: the flag is read from
        // the database on each request, not carried in the JWT, which is what lets a grant land
        // without the user signing in again.
        await GrantAdminAsync(AdminEmail);
    }

    private async Task GrantAdminAsync(string email)
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.Email == email);
        user.IsAdmin = true;
        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = _factory!.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Metrics", "Test", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task Rejects_An_Unauthenticated_Caller()
    {
        var response = await _anonymousClient.GetAsync("/api/admin/metrics");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Rejects_A_Signed_In_User_Who_Is_Not_On_The_Allowlist()
    {
        var response = await _ordinaryClient.GetAsync("/api/admin/metrics");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Allows_A_User_On_The_Allowlist()
    {
        var response = await _adminClient.GetAsync("/api/admin/metrics");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Revoked_Admin_Loses_Access_On_The_Next_Request()
    {
        // The whole reason this is a column and not config: it changes without a redeploy, and
        // without the affected person having to sign in again.
        var email = "revoked.metrics@example.com";
        var client = await CreateAuthenticatedClientAsync(email);
        await GrantAdminAsync(email);

        (await client.GetAsync("/api/admin/metrics")).StatusCode.ShouldBe(HttpStatusCode.OK);

        await using (var scope = _factory!.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await dbContext.Users.SingleAsync(u => u.Email == email);
            user.IsAdmin = false;
            await dbContext.SaveChangesAsync();
        }

        // Same client, same unexpired token.
        (await client.GetAsync("/api/admin/metrics")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Running_The_Job_Stores_Todays_Numbers()
    {
        await RunSnapshotJobAsync();

        var response = await _adminClient.GetAsync("/api/admin/metrics");
        response.EnsureSuccessStatusCode();
        var days = await response.Content.ReadFromJsonAsync<List<ProductMetricsDayResponse>>(JsonOptions);

        days.ShouldNotBeNull();
        days!.ShouldNotBeEmpty();
        // The two accounts this class registers exist by now, so the numbers are real, not zeroes.
        days[0].TotalUsers.ShouldBeGreaterThanOrEqualTo(2);
        days[0].SnapshotDate.ShouldBe(DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime));
    }

    [Fact]
    public async Task Running_The_Job_Twice_In_A_Day_Overwrites_Rather_Than_Duplicates()
    {
        // The job is scheduled daily, but a redeploy re-registering recurring jobs or a manual
        // trigger would otherwise leave two readings for one day and break every comparison the
        // table exists for.
        await RunSnapshotJobAsync();
        await RunSnapshotJobAsync();

        await using var scope = _factory!.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        var rowsForToday = await dbContext.ProductMetricsDailySnapshots
            .CountAsync(s => s.SnapshotDate == today);

        rowsForToday.ShouldBe(1);
    }

    [Fact]
    public async Task Caps_The_Requested_Window_Rather_Than_Failing_On_A_Silly_Value()
    {
        await RunSnapshotJobAsync();

        var response = await _adminClient.GetAsync("/api/admin/metrics?days=100000");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task RunSnapshotJobAsync()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        var metrics = scope.ServiceProvider.GetRequiredService<IProductMetricsService>();
        await metrics.ComputeSnapshotAsync(CancellationToken.None);
    }
}
