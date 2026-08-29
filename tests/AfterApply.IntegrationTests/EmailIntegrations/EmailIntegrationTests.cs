using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.EmailIntegrations.Contracts;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.EmailIntegrations;

// No real Gmail credentials are used here (per the Phase 9 plan's own product decision) — a
// FakeGmailClient is registered in place of the real GmailClient. GmailClient itself (the real
// Google.Apis.Gmail.v1-backed implementation) is exercised manually once real OAuth credentials
// exist; this suite covers everything else: OAuth state signing/validation, the sync job's
// idempotency, and the confirm/dismiss/disconnect workflow.
public class EmailIntegrationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private readonly FakeGmailClient _fakeGmailClient = new();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    // Same connection strings, "EmailIntegrations:Enabled" left at its real appsettings.json
    // default (false) — used to assert every endpoint 404s while the flag is off, same
    // two-factory pattern as CompanyIntelligenceTests/MatchingTests.
    private WebApplicationFactory<Program>? _disabledFactory;
    private HttpClient _disabledClient = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("GoogleOAuth:ClientId", "test-client-id");
            builder.UseSetting("GoogleOAuth:ClientSecret", "test-client-secret");
            builder.UseSetting("EmailIntegrations:Enabled", "true");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IGmailClient>(_fakeGmailClient);
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("email-integration.test@example.com", "P@ssw0rd123!", "Email", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        _disabledFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IGmailClient>(_fakeGmailClient);
            });
        });

        _disabledClient = _disabledFactory.CreateClient();
        var disabledRegisterResponse = await _disabledClient.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("email-integration.disabled@example.com", "P@ssw0rd123!", "Email", "Test", true), JsonOptions);
        disabledRegisterResponse.EnsureSuccessStatusCode();
        var disabledAuth = await disabledRegisterResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _disabledClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", disabledAuth!.AccessToken);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (_disabledFactory is not null)
        {
            await _disabledFactory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task Connect_Returns_NotFound_When_Flag_Disabled()
    {
        var response = await _disabledClient.GetAsync("/api/email-integrations/gmail/connect");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Status_Returns_NotFound_When_Flag_Disabled()
    {
        var response = await _disabledClient.GetAsync("/api/email-integrations/gmail/status");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Suggestions_Returns_NotFound_When_Flag_Disabled()
    {
        var response = await _disabledClient.GetAsync("/api/email-integrations/suggestions");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Callback_Returns_NotFound_When_Flag_Disabled()
    {
        var noRedirectClient = _disabledFactory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await noRedirectClient.GetAsync("/api/email-integrations/gmail/callback?code=abc&state=whatever");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<Guid> CreateApplicationAsync(string companyName)
    {
        var response = await _client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-5), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }

    private async Task<string> GetValidStateAsync()
    {
        var connectResponse = await _client.GetAsync("/api/email-integrations/gmail/connect");
        connectResponse.EnsureSuccessStatusCode();
        var body = await connectResponse.Content.ReadFromJsonAsync<JsonElement>();
        var authorizationUrl = body.GetProperty("authorizationUrl").GetString()!;
        var query = HttpUtility.ParseQueryString(new Uri(authorizationUrl).Query);
        return query["state"]!;
    }

    private HttpClient CreateNoRedirectClient() =>
        _factory!.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Callback_With_Valid_State_Creates_Connection_With_Encrypted_Token()
    {
        var state = await GetValidStateAsync();
        _fakeGmailClient.Profile = new GmailProfile("me@gmail.com");
        _fakeGmailClient.TokenResponse = new GoogleTokenResponse("raw-refresh-token", "access-token", DateTimeOffset.UtcNow.AddHours(1));

        var noRedirectClient = CreateNoRedirectClient();
        var response = await noRedirectClient.GetAsync($"/api/email-integrations/gmail/callback?code=abc&state={Uri.EscapeDataString(state)}");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldContain("emailIntegration=success");

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = await db.EmailConnections.SingleAsync();

        connection.ProviderAccountEmail.ShouldBe("me@gmail.com");
        connection.EncryptedRefreshToken.ShouldNotBeNull();
        connection.EncryptedRefreshToken.ShouldNotBe("raw-refresh-token");
        connection.DisconnectedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Callback_With_Tampered_State_Is_Rejected()
    {
        var state = await GetValidStateAsync();
        var tamperedState = state[..^1] + (state[^1] == 'a' ? 'b' : 'a');

        var noRedirectClient = CreateNoRedirectClient();
        var response = await noRedirectClient.GetAsync($"/api/email-integrations/gmail/callback?code=abc&state={Uri.EscapeDataString(tamperedState)}");

        response.Headers.Location!.ToString().ShouldContain("emailIntegration=error");

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailConnections.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Callback_With_Wrong_Purpose_Token_Is_Rejected()
    {
        using var scope = _factory!.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var (accessToken, _) = tokenService.CreateAccessToken(Guid.NewGuid(), "someone@example.com");

        var noRedirectClient = CreateNoRedirectClient();
        var response = await noRedirectClient.GetAsync($"/api/email-integrations/gmail/callback?code=abc&state={Uri.EscapeDataString(accessToken)}");

        response.Headers.Location!.ToString().ShouldContain("emailIntegration=error");
    }

    [Fact]
    public async Task SyncAndGenerateSuggestions_Run_Twice_Does_Not_Create_Duplicate_Suggestions()
    {
        var state = await GetValidStateAsync();
        _fakeGmailClient.TokenResponse = new GoogleTokenResponse("rt", "at", DateTimeOffset.UtcNow.AddHours(1));
        var noRedirectClient = CreateNoRedirectClient();
        await noRedirectClient.GetAsync($"/api/email-integrations/gmail/callback?code=abc&state={Uri.EscapeDataString(state)}");

        var applicationId = await CreateApplicationAsync("Acme Corp");

        _fakeGmailClient.Messages.Add(new GmailMessageSummary(
            "msg-1", "thread-1", "recruiter@acme.com", "Acme Corp Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.", DateTimeOffset.UtcNow.AddHours(-1)));

        using var scope = _factory!.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmailIntegrationService>();

        var first = await service.SyncAllConnectionsAsync(CancellationToken.None);
        var second = await service.SyncAllConnectionsAsync(CancellationToken.None);

        first.ShouldBe(1);
        second.ShouldBe(0);

        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestions = await db.EmailSuggestions.Where(s => s.ApplicationId == applicationId).ToListAsync();
        suggestions.Count.ShouldBe(1);
        suggestions[0].SuggestedStatus.ShouldBe(ApplicationStatus.Interview);
    }

    [Fact]
    public async Task GetPendingSuggestions_Returns_Subject_And_Snippet_Fetched_Live_From_Gmail()
    {
        var state = await GetValidStateAsync();
        var noRedirectClient = CreateNoRedirectClient();
        await noRedirectClient.GetAsync($"/api/email-integrations/gmail/callback?code=abc&state={Uri.EscapeDataString(state)}");

        var applicationId = await CreateApplicationAsync("Acme Corp");

        _fakeGmailClient.Messages.Add(new GmailMessageSummary(
            "msg-1", "thread-1", "recruiter@acme.com", "Acme Corp Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.", DateTimeOffset.UtcNow.AddHours(-1)));
        _fakeGmailClient.MessageDetails["msg-1"] = new GmailMessageDetail("msg-1", "Live Subject", "Live Snippet");

        using (var scope = _factory!.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmailIntegrationService>();
            await service.SyncAllConnectionsAsync(CancellationToken.None);
        }

        var response = await _client.GetAsync("/api/email-integrations/suggestions");
        response.EnsureSuccessStatusCode();
        var suggestions = await response.Content.ReadFromJsonAsync<List<EmailSuggestionResponse>>(JsonOptions);

        var suggestion = suggestions.ShouldHaveSingleItem();
        suggestion.ApplicationId.ShouldBe(applicationId);
        suggestion.Subject.ShouldBe("Live Subject");
        suggestion.Snippet.ShouldBe("Live Snippet");
    }

    [Fact]
    public async Task ConfirmSuggestion_Changes_Application_Status_With_Source_Email()
    {
        var state = await GetValidStateAsync();
        var noRedirectClient = CreateNoRedirectClient();
        await noRedirectClient.GetAsync($"/api/email-integrations/gmail/callback?code=abc&state={Uri.EscapeDataString(state)}");

        var applicationId = await CreateApplicationAsync("Acme Corp");

        _fakeGmailClient.Messages.Add(new GmailMessageSummary(
            "msg-1", "thread-1", "recruiter@acme.com", "Acme Corp Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.", DateTimeOffset.UtcNow.AddHours(-1)));

        Guid suggestionId;
        using (var scope = _factory!.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmailIntegrationService>();
            await service.SyncAllConnectionsAsync(CancellationToken.None);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            suggestionId = (await db.EmailSuggestions.SingleAsync()).Id;
        }

        var confirmResponse = await _client.PostAsync($"/api/email-integrations/suggestions/{suggestionId}/confirm", null);
        confirmResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detailResponse = await _client.GetAsync($"/api/applications/{applicationId}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        detail!.Status.ShouldBe(ApplicationStatus.Interview);

        var timelineResponse = await _client.GetAsync($"/api/applications/{applicationId}/timeline");
        var timeline = await timelineResponse.Content.ReadFromJsonAsync<List<ApplicationEventResponse>>(JsonOptions);
        timeline!.ShouldContain(e => e.Type == ApplicationEventType.StatusChanged && e.Source == Source.Email);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await verifyDb.EmailSuggestions.SingleAsync();
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Confirmed);
    }

    [Fact]
    public async Task DismissSuggestion_Does_Not_Change_Application_Status()
    {
        var state = await GetValidStateAsync();
        var noRedirectClient = CreateNoRedirectClient();
        await noRedirectClient.GetAsync($"/api/email-integrations/gmail/callback?code=abc&state={Uri.EscapeDataString(state)}");

        var applicationId = await CreateApplicationAsync("Acme Corp");

        _fakeGmailClient.Messages.Add(new GmailMessageSummary(
            "msg-1", "thread-1", "recruiter@acme.com", "Acme Corp Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.", DateTimeOffset.UtcNow.AddHours(-1)));

        Guid suggestionId;
        using (var scope = _factory!.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmailIntegrationService>();
            await service.SyncAllConnectionsAsync(CancellationToken.None);
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            suggestionId = (await db.EmailSuggestions.SingleAsync()).Id;
        }

        var dismissResponse = await _client.PostAsync($"/api/email-integrations/suggestions/{suggestionId}/dismiss", null);
        dismissResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detailResponse = await _client.GetAsync($"/api/applications/{applicationId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        detail!.Status.ShouldBe(ApplicationStatus.Applied);

        var pendingResponse = await _client.GetAsync("/api/email-integrations/suggestions");
        var pending = await pendingResponse.Content.ReadFromJsonAsync<List<EmailSuggestionResponse>>(JsonOptions);
        pending.ShouldBeEmpty();
    }

    [Fact]
    public async Task Disconnect_Keeps_Row_And_Existing_Suggestions_But_Stops_Future_Syncing()
    {
        var state = await GetValidStateAsync();
        var noRedirectClient = CreateNoRedirectClient();
        await noRedirectClient.GetAsync($"/api/email-integrations/gmail/callback?code=abc&state={Uri.EscapeDataString(state)}");

        var applicationId = await CreateApplicationAsync("Acme Corp");
        _fakeGmailClient.Messages.Add(new GmailMessageSummary(
            "msg-1", "thread-1", "recruiter@acme.com", "Acme Corp Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.", DateTimeOffset.UtcNow.AddHours(-1)));

        using (var scope = _factory!.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmailIntegrationService>();
            await service.SyncAllConnectionsAsync(CancellationToken.None);
        }

        var disconnectResponse = await _client.PostAsync("/api/email-integrations/gmail/disconnect", null);
        disconnectResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        _fakeGmailClient.RevokedTokens.ShouldNotBeEmpty();

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var connection = await db.EmailConnections.SingleAsync();
            connection.DisconnectedAt.ShouldNotBeNull();
            connection.EncryptedRefreshToken.ShouldBeNull();

            (await db.EmailSuggestions.CountAsync(s => s.ApplicationId == applicationId)).ShouldBe(1);
        }

        // A subsequent sync must not touch the disconnected connection.
        _fakeGmailClient.Messages.Add(new GmailMessageSummary(
            "msg-2", "thread-2", "recruiter@acme.com", "Acme Corp Recruiting",
            "Update", "Unfortunately we have decided to move forward with other candidates.", DateTimeOffset.UtcNow));

        using (var scope = _factory!.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEmailIntegrationService>();
            var count = await service.SyncAllConnectionsAsync(CancellationToken.None);
            count.ShouldBe(0);
        }

        var statusResponse = await _client.GetAsync("/api/email-integrations/gmail/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<EmailConnectionStatusResponse>(JsonOptions);
        status!.Connected.ShouldBeFalse();
    }
}
