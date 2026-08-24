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
using Testcontainers.PostgreSql;

namespace AfterApply.IntegrationTests.Applications;

public class AuthAndApplicationFlowTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
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
    }

    [Fact]
    public async Task Register_Login_CreateApplication_ChangeStatus_Timeline_EndToEnd()
    {
        var client = _factory!.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("flow.test@example.com", "P@ssw0rd123!", "Flow", "Test"), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var createResponse = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            "Flow Co", "Backend Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-1), null, null), JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        created.ShouldNotBeNull();
        created!.Status.ShouldBe(ApplicationStatus.Applied);

        var statusResponse = await client.PostAsJsonAsync($"/api/applications/{created.Id}/status",
            new ChangeStatusRequest(ApplicationStatus.Screening, "Recruiter reached out", null), JsonOptions);
        statusResponse.EnsureSuccessStatusCode();

        var timelineResponse = await client.GetAsync($"/api/applications/{created.Id}/timeline");
        timelineResponse.EnsureSuccessStatusCode();
        var timeline = await timelineResponse.Content.ReadFromJsonAsync<List<ApplicationEventResponse>>(JsonOptions);

        timeline.ShouldNotBeNull();
        timeline!.ShouldContain(e => e.Type == ApplicationEventType.ApplicationCreated);
        timeline.ShouldContain(e => e.Type == ApplicationEventType.StatusChanged);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_Unauthorized()
    {
        var client = _factory!.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("wrongpw.test@example.com", "P@ssw0rd123!", "Wrong", "Pw"), JsonOptions);

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("wrongpw.test@example.com", "NotTheRightPassword!"), JsonOptions);

        loginResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.Unauthorized);
    }
}
