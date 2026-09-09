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
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Applications;

// A timestamp that arrives with an offset other than UTC — 2026-09-01T09:00:00+03:00 from a client
// in Istanbul, say — used to reach Npgsql unchanged and be refused at write time, which surfaced as
// a 500 from an otherwise valid payload. It is now normalised on the way in (see
// UtcDateTimeOffsetConverter), so these tests write one through the endpoints that take a timestamp
// from a caller and check both that the request succeeds and that the instant survived.
[Collection(IntegrationTestCollection.Name)]
public class NonUtcTimestampTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>09:00 in Istanbul, which is 06:00Z — the same moment, spelled the other way.</summary>
    private static readonly DateTimeOffset IstanbulMorning = new(2026, 9, 1, 9, 0, 0, TimeSpan.FromHours(3));

    private static readonly DateTime ExpectedUtc = new(2026, 9, 1, 6, 0, 0, DateTimeKind.Utc);

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(NonUtcTimestampTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("nonutc.test@example.com", "P@ssw0rd123!", "Non", "Utc", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task Create_Accepts_An_AppliedAt_Carrying_A_Non_Utc_Offset()
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Offset Co", "Backend Engineer", null, "İstanbul", EmploymentType.FullTime,
            IstanbulMorning, null, null), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        created.ShouldNotBeNull();
        created!.AppliedAt.UtcDateTime.ShouldBe(ExpectedUtc);

        // And in the row itself, at offset zero — the shape everything downstream (the list's date
        // filters, the weekly buckets, the timeline) already assumes of a stored timestamp.
        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Applications.AsNoTracking().SingleAsync(a => a.Id == created.Id);
        stored.AppliedAt.Offset.ShouldBe(TimeSpan.Zero);
        stored.AppliedAt.UtcDateTime.ShouldBe(ExpectedUtc);
    }

    [Fact]
    public async Task Status_Change_Accepts_A_ChangedAt_Carrying_A_Non_Utc_Offset()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Offset History Co", "Platform Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-3), null, null), JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        created.ShouldNotBeNull();

        // The other direction from UTC, since the write has to hold for both signs of the offset.
        var newYorkEvening = new DateTimeOffset(2026, 9, 1, 20, 0, 0, TimeSpan.FromHours(-4));
        var statusResponse = await _client.PostAsJsonAsync($"/api/applications/{created!.Id}/status",
            new ChangeStatusRequest(ApplicationStatus.Screening, "Recruiter called", newYorkEvening), JsonOptions);

        statusResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var history = await db.ApplicationStatusHistories.AsNoTracking()
            .Where(h => h.ApplicationId == created.Id && h.ToStatus == ApplicationStatus.Screening)
            .SingleAsync();
        history.ChangedAt.Offset.ShouldBe(TimeSpan.Zero);
        history.ChangedAt.UtcDateTime.ShouldBe(new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Extension_Create_Accepts_A_PublishedAt_Carrying_A_Non_Utc_Offset()
    {
        // Job.PublishedAt is nullable, so this is also the check that the nullable timestamps in the
        // model are normalised too and not left out by the convention. And it is the realistic case:
        // the extension reads a posting date in whatever zone the browser is in.
        var response = await _client.PostAsJsonAsync("/api/applications/from-extension",
            new CreateFromExtensionRequest("Offset Extension Co", "Senior Backend Engineer",
                "https://www.linkedin.com/jobs/view/4449445628/", "İstanbul", "We build things.",
                IstanbulMorning),
            JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        await using var scope = _factory!.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = await db.Jobs.AsNoTracking().SingleAsync(j => j.Title == "Senior Backend Engineer");
        job.PublishedAt.ShouldNotBeNull();
        job.PublishedAt!.Value.Offset.ShouldBe(TimeSpan.Zero);
        job.PublishedAt.Value.UtcDateTime.ShouldBe(ExpectedUtc);
    }
}
