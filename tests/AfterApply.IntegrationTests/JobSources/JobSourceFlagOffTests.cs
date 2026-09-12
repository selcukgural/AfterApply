using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.JobSources;

/// <summary>
/// With JobSources:Enabled at its default (off), the feature does not exist: every route — the
/// user's and the admin's — answers 404 before any service runs, and the recurring sweep returns
/// without touching the network or the database.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class JobSourceFlagOffTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(JobSourceFlagOffTests));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        _admin = _factory.CreateClient();
        var response = await _admin.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("admin.flagoff@ekariyerim.com", "P@ssw0rd123!", "Flag", "Off", true), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.SingleAsync(u => u.Email == "admin.flagoff@ekariyerim.com")).IsAdmin = true;
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Every_Route_Is_A_404_Even_For_An_Admin()
    {
        var userId = Guid.NewGuid();

        (await _admin.GetAsync("/api/job-sources/profile")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _admin.PutAsJsonAsync("/api/job-sources/profile", new UpsertJobSourceProfileRequest(["Dev"], "İstanbul"), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _admin.GetAsync("/api/job-sources/postings")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _admin.GetAsync("/api/admin/job-sources/usage")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _admin.PutAsJsonAsync($"/api/admin/pro/entitlements/{userId}",
            new GrantProEntitlementRequest(DateTimeOffset.UtcNow.AddMonths(1)), JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.UserJobSourceProfiles.AnyAsync()).ShouldBeFalse();
        (await db.ProEntitlements.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task The_Sweep_Is_A_No_Op()
    {
        await using var scope = _factory!.Services.CreateAsyncScope();
        // NoOutboundHttpStartup would throw on any real request; the sweep never makes one.
        await scope.ServiceProvider.GetRequiredService<IJobSourceSweepService>().SweepAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.JobSourceFetches.AnyAsync()).ShouldBeFalse();
    }
}
