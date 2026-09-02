using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.Identity;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.EmailIntegrations;
using AfterApply.Infrastructure.EmailIntegrations;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.EmailIntegrations;

// Covers the Cloudflare-forwarding ingestion path: GET /api/email-forwarding/address,
// POST /api/email-forwarding/inbound (Worker-authenticated, not user-authenticated), and the
// provider-agnostic suggestion-review routes (GET/confirm/dismiss suggestions).
public class EmailForwardingTests : IAsyncLifetime
{
    private const string WebhookSecret = "test-webhook-secret";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private readonly FakeEmailClassificationProvider _fakeClassificationProvider = new();
    private readonly FakeEmailJobExtractionProvider _fakeExtractionProvider = new();
    private readonly FakeEmailRejectionReasonExtractionProvider _fakeRejectionReasonProvider = new();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    private WebApplicationFactory<Program>? _disabledFactory;
    private HttpClient _disabledClient = null!;

    // EmailAutoApproval:Enabled=true, ShadowModeEnabled=false — a separate factory (same Postgres/
    // Redis containers) so the default suite (_factory) can stay in the shipped shadow-mode-first
    // default without every test having to override it.
    private WebApplicationFactory<Program>? _autoApplyFactory;
    private HttpClient _autoApplyClient = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("EmailForwarding:Enabled", "true");
            builder.UseSetting("EmailForwarding:WebhookSecret", WebhookSecret);
            // Explicit, not relying on appsettings.json's own curated list — this suite's
            // "known job board domain" tests must stay deterministic regardless of what that list
            // contains in production.
            builder.UseSetting("JobBoardDomains:Domains:0", "linkedin.com");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IEmailClassificationProvider>(_fakeClassificationProvider);
                services.AddSingleton<IEmailJobExtractionProvider>(_fakeExtractionProvider);
                services.AddSingleton<IEmailRejectionReasonExtractionProvider>(_fakeRejectionReasonProvider);
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("email-forwarding.test@example.com", "P@ssw0rd123!", "Forward", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        _disabledFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            // Explicit, not just relying on appsettings.json's own default — this test must exercise
            // "flag off" regardless of what the app ships as its default (EmailForwarding:Enabled is
            // now true there, since the feature is live).
            builder.UseSetting("EmailForwarding:Enabled", "false");
        });

        _disabledClient = _disabledFactory.CreateClient();
        var disabledRegisterResponse = await _disabledClient.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("email-forwarding.disabled@example.com", "P@ssw0rd123!", "Forward", "Test", true), JsonOptions);
        disabledRegisterResponse.EnsureSuccessStatusCode();
        var disabledAuth = await disabledRegisterResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _disabledClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", disabledAuth!.AccessToken);

        _autoApplyFactory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("EmailForwarding:Enabled", "true");
            builder.UseSetting("EmailForwarding:WebhookSecret", WebhookSecret);
            builder.UseSetting("JobBoardDomains:Domains:0", "linkedin.com");
            builder.UseSetting("EmailAutoApproval:Enabled", "true");
            builder.UseSetting("EmailAutoApproval:ShadowModeEnabled", "false");
            builder.UseSetting("EmailAutoApproval:ConfidenceThreshold", "0.9");

            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IEmailClassificationProvider>(_fakeClassificationProvider);
                services.AddSingleton<IEmailJobExtractionProvider>(_fakeExtractionProvider);
                services.AddSingleton<IEmailRejectionReasonExtractionProvider>(_fakeRejectionReasonProvider);
            });
        });

        _autoApplyClient = _autoApplyFactory.CreateClient();
        var autoApplyRegisterResponse = await _autoApplyClient.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("email-forwarding.autoapply@example.com", "P@ssw0rd123!", "Forward", "Test", true), JsonOptions);
        autoApplyRegisterResponse.EnsureSuccessStatusCode();
        var autoApplyAuth = await autoApplyRegisterResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _autoApplyClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", autoApplyAuth!.AccessToken);
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

        if (_autoApplyFactory is not null)
        {
            await _autoApplyFactory.DisposeAsync();
        }

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    [Fact]
    public async Task Address_Returns_NotFound_When_Flag_Disabled()
    {
        var response = await _disabledClient.GetAsync("/api/email-forwarding/address");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Suggestions_Returns_NotFound_When_Flag_Disabled()
    {
        var response = await _disabledClient.GetAsync("/api/email-forwarding/suggestions");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SuggestionCount_Returns_NotFound_When_Flag_Disabled()
    {
        var response = await _disabledClient.GetAsync("/api/email-forwarding/suggestions/count");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SuggestionCount_Reflects_Pending_Suggestions_And_Drops_After_Confirm()
    {
        var address = await GetOwnAddressAsync();
        await CreateApplicationAsync("Acme Count Test");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/email-forwarding/inbound")
        {
            Content = JsonContent.Create(new
            {
                to = address, from = "recruiter@acme-count-test.com", fromName = "Acme Count Test Recruiting",
                subject = "Interview invitation", snippet = "We'd like to invite you to an interview."
            }, options: JsonOptions)
        };
        request.Headers.Add("X-Webhook-Secret", WebhookSecret);

        var unauthenticatedClient = _factory!.CreateClient();
        (await unauthenticatedClient.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WaitForHangfireIdleAsync();

        var countAfterInbound = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions/count", JsonOptions);
        countAfterInbound.GetProperty("count").GetInt32().ShouldBe(1);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestionId = suggestionsResponse.EnumerateArray().Single().GetProperty("id").GetString();
        (await _client.PostAsync($"/api/email-forwarding/suggestions/{suggestionId}/confirm", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var countAfterConfirm = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions/count", JsonOptions);
        countAfterConfirm.GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetAddress_Creates_Connection_And_Is_Idempotent()
    {
        var first = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/address", JsonOptions);
        var second = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/address", JsonOptions);

        var firstAddress = first.GetProperty("address").GetString();
        var secondAddress = second.GetProperty("address").GetString();

        firstAddress.ShouldNotBeNullOrWhiteSpace();
        firstAddress.ShouldEndWith("@application.ekariyerim.com");
        firstAddress.ShouldBe(secondAddress);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailConnections.CountAsync(c => c.Provider == EmailProvider.Forwarding)).ShouldBe(1);
    }

    [Fact]
    public async Task Inbound_With_Wrong_Secret_Is_Rejected()
    {
        var address = await GetOwnAddressAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/email-forwarding/inbound")
        {
            Content = JsonContent.Create(new
            {
                to = address, from = "recruiter@acme-test.com", fromName = "Acme Test Recruiting",
                subject = "Interview invitation", snippet = "We'd like to invite you to an interview."
            }, options: JsonOptions)
        };
        request.Headers.Add("X-Webhook-Secret", "wrong-secret");

        var unauthenticatedClient = _factory!.CreateClient();
        var response = await unauthenticatedClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Inbound_With_Unknown_Token_Is_A_NoOp()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/email-forwarding/inbound")
        {
            Content = JsonContent.Create(new
            {
                to = "nonexistent-token@application.ekariyerim.com", from = "recruiter@acme-test.com",
                fromName = "Acme Test Recruiting", subject = "Interview invitation",
                snippet = "We'd like to invite you to an interview."
            }, options: JsonOptions)
        };
        request.Headers.Add("X-Webhook-Secret", WebhookSecret);

        var unauthenticatedClient = _factory!.CreateClient();
        var response = await unauthenticatedClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WaitForHangfireIdleAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Inbound_With_Matching_Rule_Creates_Suggestion_With_Persisted_Content()
    {
        var address = await GetOwnAddressAsync();
        await CreateApplicationAsync("Acme Test");
        // A real retry re-delivers the identical payload, including the same Date-header-derived
        // receivedAt — fixed here so the idempotency key matches on replay below.
        var receivedAt = DateTimeOffset.UtcNow;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/email-forwarding/inbound")
        {
            Content = JsonContent.Create(new
            {
                to = address, from = "recruiter@acme-test.com", fromName = "Acme Test Recruiting",
                subject = "Interview invitation", snippet = "We'd like to invite you to an interview.",
                receivedAt
            }, options: JsonOptions)
        };
        request.Headers.Add("X-Webhook-Secret", WebhookSecret);

        var unauthenticatedClient = _factory!.CreateClient();
        var response = await unauthenticatedClient.SendAsync(request);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WaitForHangfireIdleAsync();

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestions = suggestionsResponse.EnumerateArray().ToList();

        suggestions.Count.ShouldBe(1);
        suggestions[0].GetProperty("suggestedStatus").GetString().ShouldBe(nameof(ApplicationStatus.Interview));
        suggestions[0].GetProperty("subject").GetString().ShouldBe("Interview invitation");
        suggestions[0].GetProperty("snippet").GetString().ShouldBe("We'd like to invite you to an interview.");

        // Re-delivering the same webhook payload (Cloudflare retry) must not double-create.
        var replay = await unauthenticatedClient.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/email-forwarding/inbound")
        {
            Content = JsonContent.Create(new
            {
                to = address, from = "recruiter@acme-test.com", fromName = "Acme Test Recruiting",
                subject = "Interview invitation", snippet = "We'd like to invite you to an interview.",
                receivedAt
            }, options: JsonOptions),
            Headers = { { "X-Webhook-Secret", WebhookSecret } }
        });
        replay.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await WaitForHangfireIdleAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ConfirmSuggestion_For_Existing_Application_Changes_Status_With_Source_Email()
    {
        var address = await GetOwnAddressAsync();
        // Display name carries the company name as a literal prefix — EmailApplicationMatcher's
        // fallback path is substring-containment (normalizedDisplayName.Contains(companyName)), and
        // this company has no website domain for the matcher's primary domain-match path.
        var applicationId = await CreateApplicationAsync("Acme Confirm Test");

        var inboundResponse = await SendInboundAsync(address, "recruiter@acme-confirm-test.com", "Acme Confirm Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.");
        inboundResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestionId = suggestionsResponse.EnumerateArray().Single().GetProperty("id").GetGuid();

        var confirmResponse = await _client.PostAsync($"/api/email-forwarding/suggestions/{suggestionId}/confirm", null);
        confirmResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detailResponse = await _client.GetAsync($"/api/applications/{applicationId}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        detail!.Status.ShouldBe(ApplicationStatus.Interview);

        var timelineResponse = await _client.GetAsync($"/api/applications/{applicationId}/timeline");
        var timeline = await timelineResponse.Content.ReadFromJsonAsync<List<ApplicationEventResponse>>(JsonOptions);
        timeline!.ShouldContain(e => e.Type == ApplicationEventType.StatusChanged && e.Source == Source.Email);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await db.EmailSuggestions.SingleAsync();
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Confirmed);
    }

    [Fact]
    public async Task Inbound_Rejection_Runs_Rejection_Reason_Extraction_And_Persists_Category()
    {
        var address = await GetOwnAddressAsync();
        await CreateApplicationAsync("Acme Reason Test");
        _fakeRejectionReasonProvider.Result = new EmailRejectionReasonExtractionResult(
            RejectionReasonCategory.LanguageRequirement, "This role requires Dutch at C1 level", 0.9);

        // "unfortunately" is one of RuleBasedEmailClassifier's own Rejection phrases, so this never
        // needs the classification LLM — only the (fake) rejection-reason provider is exercised here.
        var inboundResponse = await SendInboundAsync(address, "recruiter@acme-reason-test.com", "Acme Reason Test Recruiting",
            "Application update", "Unfortunately, we have decided not to move forward with your application.");
        inboundResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeRejectionReasonProvider.CallCount.ShouldBe(1);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestion = suggestionsResponse.EnumerateArray().Single();
        suggestion.GetProperty("suggestedStatus").GetString().ShouldBe(nameof(ApplicationStatus.Rejected));
        suggestion.GetProperty("rejectionReasonCategory").GetString().ShouldBe(nameof(RejectionReasonCategory.LanguageRequirement));
        suggestion.GetProperty("rejectionReasonDetail").GetString().ShouldBe("This role requires Dutch at C1 level");
    }

    [Fact]
    public async Task Inbound_NonRejection_Skips_Rejection_Reason_Extraction()
    {
        var address = await GetOwnAddressAsync();
        await CreateApplicationAsync("Acme No Reason Test");

        var inboundResponse = await SendInboundAsync(address, "recruiter@acme-no-reason-test.com", "Acme No Reason Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.");
        inboundResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeRejectionReasonProvider.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ConfirmSuggestion_For_Rejected_With_Stated_Reason_Appends_Reason_To_StatusHistory_Note()
    {
        var address = await GetOwnAddressAsync();
        var applicationId = await CreateApplicationAsync("Acme Confirm Reason Test");
        _fakeRejectionReasonProvider.Result = new EmailRejectionReasonExtractionResult(
            RejectionReasonCategory.SalaryExpectationMismatch, "Compensation expectations exceed the budgeted range", 0.85);

        var inboundResponse = await SendInboundAsync(address, "recruiter@acme-confirm-reason-test.com",
            "Acme Confirm Reason Test Recruiting", "Application update",
            "Unfortunately, we have decided not to move forward with your application.");
        inboundResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestionId = suggestionsResponse.EnumerateArray().Single().GetProperty("id").GetGuid();

        var confirmResponse = await _client.PostAsync($"/api/email-forwarding/suggestions/{suggestionId}/confirm", null);
        confirmResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var history = await db.ApplicationStatusHistories
            .Where(h => h.ApplicationId == applicationId && h.ToStatus == ApplicationStatus.Rejected)
            .SingleAsync();
        history.Note.ShouldNotBeNull();
        history.Note.ShouldContain("Compensation expectations exceed the budgeted range");
    }

    [Fact]
    public async Task DismissSuggestion_Does_Not_Change_Application_Status()
    {
        var address = await GetOwnAddressAsync();
        // Same fix as ConfirmSuggestion_For_Existing_Application_Changes_Status_With_Source_Email
        // above — display name must carry the company name as a literal prefix.
        var applicationId = await CreateApplicationAsync("Acme Dismiss Test");

        var inboundResponse = await SendInboundAsync(address, "recruiter@acme-dismiss-test.com", "Acme Dismiss Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.");
        inboundResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestionId = suggestionsResponse.EnumerateArray().Single().GetProperty("id").GetGuid();

        var dismissResponse = await _client.PostAsync($"/api/email-forwarding/suggestions/{suggestionId}/dismiss", null);
        dismissResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var detailResponse = await _client.GetAsync($"/api/applications/{applicationId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        detail!.Status.ShouldBe(ApplicationStatus.Applied);

        var pendingResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        pendingResponse.EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task Inbound_Unmatched_Without_Signal_Skips_Extraction_Entirely()
    {
        var address = await GetOwnAddressAsync();
        // No existing Application for this sender's company, and text that matches none of
        // RuleBasedEmailClassifier's phrases — falls through to the (faked) LLM classifier, which
        // returns NoSignal by default. Extraction must never even be attempted in that case.
        _fakeClassificationProvider.Result = new EmailClassificationResult(null, 0, "Llm:NoSignal");

        var response = await SendInboundAsync(address, "unrelated@newsletter-test.com", "Newsletter Co",
            "Weekly Newsletter", "Check out our latest blog posts.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeExtractionProvider.CallCount.ShouldBe(0);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Inbound_Unmatched_With_Signal_But_Extraction_Not_Confident_Is_NoOp()
    {
        var address = await GetOwnAddressAsync();
        // No CreateApplicationAsync call — this sender matches no existing Application. Interview
        // phrasing is a RuleBasedEmailClassifier hit, so classification never touches the fake LLM.
        _fakeExtractionProvider.Result = null;

        var response = await SendInboundAsync(address, "recruiter@unregistered-test.com", "Unregistered Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeExtractionProvider.CallCount.ShouldBe(1);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Inbound_Unmatched_With_Signal_And_Confident_Extraction_Creates_NewJob_Suggestion()
    {
        var address = await GetOwnAddressAsync();
        _fakeExtractionProvider.Result = new EmailJobExtractionResult(
            "Unregistered Test", "Backend Engineer", "Istanbul", "Build and maintain backend services.");

        var response = await SendInboundAsync(address, "recruiter@unregistered-test.com", "Unregistered Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestions = suggestionsResponse.EnumerateArray().ToList();

        var suggestion = suggestions.ShouldHaveSingleItem();
        suggestion.GetProperty("applicationId").ValueKind.ShouldBe(JsonValueKind.Null);
        suggestion.GetProperty("isNewApplicationSuggestion").GetBoolean().ShouldBeTrue();
        suggestion.GetProperty("companyName").GetString().ShouldBe("Unregistered Test");
        suggestion.GetProperty("jobTitle").GetString().ShouldBe("Backend Engineer");
        suggestion.GetProperty("location").GetString().ShouldBe("Istanbul");
        suggestion.GetProperty("suggestedStatus").GetString().ShouldBe(nameof(ApplicationStatus.Interview));
    }

    [Fact]
    public async Task Inbound_Unmatched_ApplicationReceived_Acknowledgement_Creates_NewJob_Suggestion_Without_Calling_Llm()
    {
        var address = await GetOwnAddressAsync();
        // Real-world case (2026-08-31): applying directly on a company's own career site produces
        // no existing Application and no known-job-board domain — a plain "thanks for applying"
        // ATS acknowledgement used to be silently dropped here. RuleBasedEmailClassifier's
        // ApplicationReceived rule must catch it without ever reaching the (faked) LLM classifier.
        _fakeExtractionProvider.Result = new EmailJobExtractionResult("Abacus Medicine Group", "Senior Engineer", null, null);

        var response = await SendInboundAsync(address, "victor@teamtailor-mail.com", "Abacus Medicine Group",
            "We have received your application!",
            "Dear Selçuk Thank you so much for your application! At Abacus Medicine Group, our employees are our biggest asset.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeClassificationProvider.CallCount.ShouldBe(0);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestion = suggestionsResponse.EnumerateArray().ShouldHaveSingleItem();
        suggestion.GetProperty("isNewApplicationSuggestion").GetBoolean().ShouldBeTrue();
        suggestion.GetProperty("companyName").GetString().ShouldBe("Abacus Medicine Group");
        suggestion.GetProperty("suggestedStatus").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Inbound_Matched_ApplicationReceived_Acknowledgement_Is_NoOp()
    {
        var address = await GetOwnAddressAsync();
        // Display name carries the company name as a literal prefix — same
        // EmailApplicationMatcher fallback convention used elsewhere in this file.
        await CreateApplicationAsync("Acme Ack Test");

        var response = await SendInboundAsync(address, "no-reply@acme-ack-test.com", "Acme Ack Test Recruiting",
            "We have received your application!", "Thank you so much for your application!");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Nothing to extract for a matched application either way, but this pins down that the
        // acknowledgement is treated as no signal at all here, not as a pointless "confirm Applied"
        // suggestion for an application that's already sitting at Applied.
        _fakeExtractionProvider.CallCount.ShouldBe(0);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Inbound_Unknown_Domain_Unmatched_RuleBased_Signal_Creates_Suggestion_Without_Calling_Llm()
    {
        var address = await GetOwnAddressAsync();
        // No CreateApplicationAsync call, and acme-unknown-test.com is neither an existing
        // application's company domain nor on JobBoardDomains — "Interview invitation" is still a
        // RuleBasedEmailClassifier hit, so the LLM classifier must never be reached.
        _fakeExtractionProvider.Result = new EmailJobExtractionResult("Acme Unknown", "Engineer", null, null);

        var response = await SendInboundAsync(address, "recruiter@acme-unknown-test.com", "Acme Unknown Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeClassificationProvider.CallCount.ShouldBe(0);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        suggestionsResponse.EnumerateArray().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Inbound_Unknown_Domain_Unmatched_NonRuleText_Never_Calls_Llm()
    {
        var address = await GetOwnAddressAsync();
        // Unrecognized domain, no application match, and text that doesn't hit
        // RuleBasedEmailClassifier's phrase table — the LLM classifier must be skipped entirely,
        // not just its result ignored, since avoiding this call is the actual cost/privacy point
        // of the allow-list (see DECISIONS.md / plan for the domain allow-list feature).
        var response = await SendInboundAsync(address, "someone@random-personal-test.com", "Someone",
            "Weekend plans", "Are we still on for Saturday?");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeClassificationProvider.CallCount.ShouldBe(0);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Inbound_KnownJobBoardDomain_Unmatched_NonRuleText_Still_Calls_Llm()
    {
        var address = await GetOwnAddressAsync();
        // linkedin.com is on the static JobBoardDomains list — even with no application match and
        // text outside the rule table, the LLM classifier should still be given a chance.
        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.9, "Llm:Interview");
        _fakeExtractionProvider.Result = new EmailJobExtractionResult("New Co Via LinkedIn", "Engineer", null, null);

        var response = await SendInboundAsync(address, "jobs-noreply@linkedin.com", "LinkedIn",
            "Your application status changed", "There's an update on your recent application.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeClassificationProvider.CallCount.ShouldBe(1);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        suggestionsResponse.EnumerateArray().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Inbound_SubdomainOfKnownJobBoardDomain_Unmatched_NonRuleText_Still_Calls_Llm()
    {
        var address = await GetOwnAddressAsync();
        // "linkedin.com" is the configured JobBoardDomains:Domains entry (see InitializeAsync) —
        // a job-board vendor subdomain must still match via IJobBoardDomainMatcher's suffix check.
        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.9, "Llm:Interview");
        _fakeExtractionProvider.Result = new EmailJobExtractionResult("New Co Via LinkedIn", "Engineer", null, null);

        var response = await SendInboundAsync(address, "jobs-noreply@notifications.linkedin.com", "LinkedIn",
            "Your application status changed", "There's an update on your recent application.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeClassificationProvider.CallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Inbound_Unknown_Domain_Unmatched_Paraphrased_Interview_Text_Still_Calls_Llm()
    {
        var address = await GetOwnAddressAsync();
        // Neither an existing Application match nor a known job-board/ATS domain, and the phrasing
        // deliberately avoids RuleBasedEmailClassifier's narrow curated phrases ("invite you to an
        // interview", etc.) — this is exactly the false-negative gap RecruitmentSignalAnalyzer closes:
        // a recruiter-ish sender local-part plus enough broader interview vocabulary to clear
        // EmailIntelligence:LlmThreshold.
        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.8, "Llm:Interview");
        _fakeExtractionProvider.Result = new EmailJobExtractionResult("New Small Co", "Engineer", null, null);

        var response = await SendInboundAsync(address, "talent@new-small-co-test.com", "New Small Co Talent Team",
            "Next steps for your interview process",
            "We'd love to invite you for a technical interview and a phone screen with our team next week.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeClassificationProvider.CallCount.ShouldBe(1);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        suggestionsResponse.EnumerateArray().Count().ShouldBe(1);
    }

    [Fact]
    public async Task Inbound_Unknown_Domain_Unmatched_Newsletter_Text_Never_Calls_Llm()
    {
        var address = await GetOwnAddressAsync();
        // Same unknown-domain/unmatched shape as the paraphrased-interview case above, but the text
        // is squarely negative-signal (newsletter/job-alert) — the analyzer must keep the score below
        // LlmThreshold so this still never reaches the (faked) LLM classifier, same as before the
        // hard isKnownSender gate was removed.
        var response = await SendInboundAsync(address, "updates@some-jobboard-test.com", "Some Job Board",
            "10 jobs you may be interested in", "Here are jobs matching your profile this week. Unsubscribe anytime.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeClassificationProvider.CallCount.ShouldBe(0);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Inbound_MatchedApplicationDomain_NonRuleText_Still_Calls_Llm()
    {
        var address = await GetOwnAddressAsync();
        var applicationId = await CreateApplicationAsync("Website Match Co");

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var application = await db.Applications.SingleAsync(a => a.Id == applicationId);
            var company = await db.Companies.SingleAsync(c => c.Id == application.CompanyId);
            company.EnrichFrom("https://website-match-test.com", industry: null, country: null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.9, "Llm:Interview");

        // Display name deliberately does NOT carry the company name, so only the domain match
        // (against the just-enriched Company.Website) can find this application — proves the
        // allow-list's per-user half stays in sync with enrichment automatically, no extra plumbing.
        var response = await SendInboundAsync(address, "hr@website-match-test.com", "Recruiting Team",
            "Update on your recent application", "There's news about your application.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        _fakeClassificationProvider.CallCount.ShouldBe(1);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestion = suggestionsResponse.EnumerateArray().Single();
        suggestion.GetProperty("applicationId").GetGuid().ShouldBe(applicationId);
    }

    [Fact]
    public async Task ConfirmSuggestion_For_NewJob_Suggestion_Creates_Application_Tagged_SourceEmail()
    {
        var address = await GetOwnAddressAsync();
        _fakeExtractionProvider.Result = new EmailJobExtractionResult(
            "New Co", "Senior Developer", "Remote", "A great new role.");

        var inboundResponse = await SendInboundAsync(address, "recruiter@new-co-test.com", "New Co Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.");
        inboundResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestionId = suggestionsResponse.EnumerateArray().Single().GetProperty("id").GetGuid();

        var confirmResponse = await _client.PostAsync($"/api/email-forwarding/suggestions/{suggestionId}/confirm", null);
        confirmResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var application = await db.Applications.SingleAsync(a => a.JobTitle == "Senior Developer");
        application.Source.ShouldBe(Source.Email);
        application.Status.ShouldBe(ApplicationStatus.Interview);
        application.Location.ShouldBe("Remote");
        application.Notes.ShouldBe("A great new role.");

        var company = await db.Companies.SingleAsync(c => c.Id == application.CompanyId);
        company.Name.ShouldBe("New Co");
    }

    [Fact]
    public async Task ConfirmSuggestion_For_NewJob_Suggestion_With_StillWaiting_Signal_Creates_Application_At_Applied_Status()
    {
        var address = await GetOwnAddressAsync();
        _fakeExtractionProvider.Result = new EmailJobExtractionResult("Still Waiting Co", "QA Engineer", null, null);

        // "still under review" is RuleBasedEmailClassifier's StillWaiting phrase — SuggestedStatus
        // stays null, MatchedRule is "StillWaiting", which is still treated as a signal.
        var inboundResponse = await SendInboundAsync(address, "recruiter@still-waiting-test.com", "Still Waiting Recruiting",
            "Application update", "Your application is still under review.");
        inboundResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestionId = suggestionsResponse.EnumerateArray().Single().GetProperty("id").GetGuid();

        var confirmResponse = await _client.PostAsync($"/api/email-forwarding/suggestions/{suggestionId}/confirm", null);
        confirmResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var application = await db.Applications.SingleAsync(a => a.JobTitle == "QA Engineer");
        application.Source.ShouldBe(Source.Email);
        application.Status.ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Inbound_GmailForwardingConfirmation_Is_Stored_On_Connection_Not_As_Suggestion()
    {
        var address = await GetOwnAddressAsync();

        var response = await SendInboundAsync(address, "forwarding-noreply@google.com", "Gmail Team",
            $"(Gmail Forwarding Confirmation - Receive Mail from {address}",
            "Confirmation code: 482913. Or click here to confirm: https://mail-settings.google.com/mail/vf-abc123");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var addressResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/address", JsonOptions);
        addressResponse.GetProperty("gmailConfirmationCode").GetString().ShouldBe("482913");
        addressResponse.GetProperty("gmailConfirmationLink").GetString().ShouldStartWith("https://mail-settings.google.com");
        addressResponse.GetProperty("gmailConfirmationReceivedAt").ValueKind.ShouldNotBe(JsonValueKind.Null);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Inbound_GmailForwardingConfirmation_Resend_Overwrites_Previous_Values()
    {
        var address = await GetOwnAddressAsync();

        await SendInboundAsync(address, "forwarding-noreply@google.com", "Gmail Team",
            $"(Gmail Forwarding Confirmation - Receive Mail from {address}",
            "Confirmation code: 111111. Or click here to confirm: https://mail-settings.google.com/mail/vf-first");
        var resend = await SendInboundAsync(address, "forwarding-noreply@google.com", "Gmail Team",
            $"(Gmail Forwarding Confirmation - Receive Mail from {address}",
            "Confirmation code: 222222. Or click here to confirm: https://mail-settings.google.com/mail/vf-second");
        resend.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var addressResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/address", JsonOptions);
        addressResponse.GetProperty("gmailConfirmationCode").GetString().ShouldBe("222222");
        addressResponse.GetProperty("gmailConfirmationLink").GetString().ShouldEndWith("vf-second");
    }

    [Fact]
    public async Task DismissGmailConfirmation_Clears_Fields_And_404s_When_Nothing_Pending()
    {
        var address = await GetOwnAddressAsync();
        await SendInboundAsync(address, "forwarding-noreply@google.com", "Gmail Team",
            $"(Gmail Forwarding Confirmation - Receive Mail from {address}",
            "Confirmation code: 333333. Or click here to confirm: https://mail-settings.google.com/mail/vf-third");

        var dismissResponse = await _client.PostAsync("/api/email-forwarding/gmail-confirmation/dismiss", null);
        dismissResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var addressResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/address", JsonOptions);
        addressResponse.GetProperty("gmailConfirmationCode").ValueKind.ShouldBe(JsonValueKind.Null);
        addressResponse.GetProperty("gmailConfirmationLink").ValueKind.ShouldBe(JsonValueKind.Null);

        var secondDismiss = await _client.PostAsync("/api/email-forwarding/gmail-confirmation/dismiss", null);
        secondDismiss.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Inbound_From_UnrelatedGoogleSender_Is_Not_Treated_As_Confirmation()
    {
        var address = await GetOwnAddressAsync();
        // Display name carries the company name as a literal prefix (matches
        // EmailApplicationMatcher's substring-containment fallback — same convention as
        // Inbound_With_Matching_Rule_Creates_Suggestion_With_Persisted_Content above), since this
        // company has no website domain for the matcher's primary domain-match path.
        await CreateApplicationAsync("Acme Google Sender Test");

        // Same @google.com domain as the real confirmation sender, but neither the exact address
        // nor the subject prefix match — must fall through to normal classification, not be
        // swallowed by the confirmation-detection allowlist.
        var response = await SendInboundAsync(address, "recruiter@google.com", "Acme Google Sender Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.");
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        suggestionsResponse.EnumerateArray().Count().ShouldBe(1);

        var addressResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/address", JsonOptions);
        addressResponse.GetProperty("gmailConfirmationCode").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Inbound_DomainMatch_Llm_HighConfidence_With_AutoApproval_Enabled_AutoApplies()
    {
        var address = await GetOwnAddressAsync(_autoApplyClient);
        var applicationId = await CreateApplicationAsync("Auto Apply Domain Co", _autoApplyClient);

        using (var scope = _autoApplyFactory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var applicationForEnrichment = await db.Applications.SingleAsync(a => a.Id == applicationId);
            var company = await db.Companies.SingleAsync(c => c.Id == applicationForEnrichment.CompanyId);
            company.EnrichFrom("https://auto-apply-domain-test.com", industry: null, country: null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.95, "Llm:Interview");

        // Called directly against this factory's own DI scope rather than through the HTTP inbound
        // endpoint + Hangfire: _factory/_disabledFactory/_autoApplyFactory all point at the same
        // Postgres-backed Hangfire storage (see WaitForHangfireIdleAsync's own comment), so a job
        // enqueued via one factory's client isn't guaranteed to be picked up by that same factory's
        // background server — it can be processed under a different factory's EmailAutoApproval
        // config. A direct call removes that nondeterminism for tests where which factory's config
        // actually governs the decision is the entire point.
        await ProcessInboundDirectlyAsync(_autoApplyFactory!, address, "hr@auto-apply-domain-test.com",
            "Recruiting Team", "Update on your recent application", "There's news about your application.");

        using var assertScope = _autoApplyFactory!.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await assertDb.EmailSuggestions.SingleAsync(s => s.ApplicationId == applicationId);
        suggestion.Status.ShouldBe(EmailSuggestionStatus.AutoApplied);
        suggestion.ResolvedAt.ShouldNotBeNull();

        var application = await assertDb.Applications.SingleAsync(a => a.Id == applicationId);
        application.Status.ShouldBe(ApplicationStatus.Interview);
    }

    [Fact]
    public async Task Inbound_RuleBased_Classification_Never_AutoApplies_Even_When_Enabled()
    {
        var address = await GetOwnAddressAsync(_autoApplyClient);
        var applicationId = await CreateApplicationAsync("Auto Apply Rule Co", _autoApplyClient);

        using (var scope = _autoApplyFactory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var applicationForEnrichment = await db.Applications.SingleAsync(a => a.Id == applicationId);
            var company = await db.Companies.SingleAsync(c => c.Id == applicationForEnrichment.CompanyId);
            company.EnrichFrom("https://auto-apply-rule-test.com", industry: null, country: null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        // Deliberately no _fakeClassificationProvider.Result set — "Interview invitation" matches
        // RuleBasedEmailClassifier directly, so the LLM is never even called. Rule-based confidence
        // is a hand-tuned weight, never a calibrated probability, so it must never qualify for
        // auto-apply regardless of EmailAutoApproval:Enabled.
        await ProcessInboundDirectlyAsync(_autoApplyFactory!, address, "hr@auto-apply-rule-test.com",
            "Recruiting Team", "Interview invitation", "We'd like to invite you to an interview.");

        _fakeClassificationProvider.CallCount.ShouldBe(0);

        using var assertScope = _autoApplyFactory!.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await assertDb.EmailSuggestions.SingleAsync(s => s.ApplicationId == applicationId);
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Pending);

        var application = await assertDb.Applications.SingleAsync(a => a.Id == applicationId);
        application.Status.ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Inbound_NameFallbackMatch_Never_AutoApplies_Even_When_Enabled()
    {
        var address = await GetOwnAddressAsync(_autoApplyClient);
        // No website enrichment — only the display-name/subject substring fallback can find this,
        // which is exactly the weak match type auto-apply must never trust.
        var applicationId = await CreateApplicationAsync("Auto Apply Fallback Co", _autoApplyClient);

        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.95, "Llm:Interview");

        await ProcessInboundDirectlyAsync(_autoApplyFactory!, address, "hr@some-ats-test.com",
            "Auto Apply Fallback Co Recruiting", "Update on your recent application", "There's news about your application.");

        using var scope = _autoApplyFactory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await db.EmailSuggestions.SingleAsync(s => s.ApplicationId == applicationId);
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Pending);
        suggestion.MatchType.ShouldBe(EmailApplicationMatchType.NameFallbackMatch);
    }

    [Fact]
    public async Task Inbound_ConfidenceBelowThreshold_Never_AutoApplies_Even_When_Enabled()
    {
        var address = await GetOwnAddressAsync(_autoApplyClient);
        var applicationId = await CreateApplicationAsync("Auto Apply LowConf Co", _autoApplyClient);

        using (var scope = _autoApplyFactory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var application = await db.Applications.SingleAsync(a => a.Id == applicationId);
            var company = await db.Companies.SingleAsync(c => c.Id == application.CompanyId);
            company.EnrichFrom("https://auto-apply-lowconf-test.com", industry: null, country: null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        // Below the factory's configured EmailAutoApproval:ConfidenceThreshold of 0.9.
        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.6, "Llm:Interview");

        await ProcessInboundDirectlyAsync(_autoApplyFactory!, address, "hr@auto-apply-lowconf-test.com",
            "Recruiting Team", "Update on your recent application", "There's news about your application.");

        using var assertScope = _autoApplyFactory!.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await assertDb.EmailSuggestions.SingleAsync(s => s.ApplicationId == applicationId);
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Pending);
    }

    [Fact]
    public async Task Inbound_DomainMatch_Llm_HighConfidence_In_ShadowMode_Stays_Pending()
    {
        // Default _factory ships with the app's default EmailAutoApproval settings
        // (Enabled=false, ShadowModeEnabled=true) — qualifying suggestions must only be logged,
        // never actually applied.
        var address = await GetOwnAddressAsync();
        var applicationId = await CreateApplicationAsync("Shadow Mode Co");

        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var applicationForEnrichment = await db.Applications.SingleAsync(a => a.Id == applicationId);
            var company = await db.Companies.SingleAsync(c => c.Id == applicationForEnrichment.CompanyId);
            company.EnrichFrom("https://shadow-mode-test.com", industry: null, country: null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.95, "Llm:Interview");

        await ProcessInboundDirectlyAsync(_factory!, address, "hr@shadow-mode-test.com",
            "Recruiting Team", "Update on your recent application", "There's news about your application.");

        using var assertScope = _factory!.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await assertDb.EmailSuggestions.SingleAsync(s => s.ApplicationId == applicationId);
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Pending);

        var application = await assertDb.Applications.SingleAsync(a => a.Id == applicationId);
        application.Status.ShouldBe(ApplicationStatus.Applied);
    }

    [Fact]
    public async Task Inbound_NewJob_Suggestion_Never_AutoApplies_Even_When_Enabled()
    {
        var address = await GetOwnAddressAsync(_autoApplyClient);
        // No CreateApplicationAsync — unmatched sender, so MatchType stays null on the resulting
        // suggestion, which already fails the auto-apply qualifying check on its own.
        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.99, "Llm:Interview");
        _fakeExtractionProvider.Result = new EmailJobExtractionResult("Auto Apply New Job Co", "Engineer", null, null);

        // Snippet is deliberately worded to hit Application + Recruiter + Interview
        // RecruitmentSignalAnalyzer categories (score ~80, well clear of EmailIntelligence:
        // LlmThreshold=50) without matching any RuleBasedEmailClassifier phrase — this must reach the
        // LLM fake above for the test to actually exercise "MatchedRule=Llm:... + high confidence
        // still doesn't auto-apply an unmatched suggestion", not just "no signal was found at all".
        await ProcessInboundDirectlyAsync(_autoApplyFactory!, address, "hr@auto-apply-newjob-test.com",
            "Recruiting Team", "Update on your recent application",
            "There's news about your application. Our talent acquisition team wanted to share an update on the interview scheduled for your role.");

        using var scope = _autoApplyFactory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await db.EmailSuggestions.SingleAsync(s => s.ApplicationId == null);
        suggestion.Status.ShouldBe(EmailSuggestionStatus.Pending);
        suggestion.MatchType.ShouldBeNull();
    }

    [Fact]
    public async Task Notifications_Endpoint_Lists_AutoApplied_And_Confirmed_Reflects_Unread_Count_And_MarkRead()
    {
        // One qualifying suggestion — auto-applied.
        var autoAppliedAppId = await CreateApplicationAsync("Notifications Auto Co", _autoApplyClient);
        using (var scope = _autoApplyFactory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var applicationForEnrichment = await db.Applications.SingleAsync(a => a.Id == autoAppliedAppId);
            var company = await db.Companies.SingleAsync(c => c.Id == applicationForEnrichment.CompanyId);
            company.EnrichFrom("https://notifications-auto-test.com", industry: null, country: null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.95, "Llm:Interview");
        var autoAddress = await GetOwnAddressAsync(_autoApplyClient);
        await ProcessInboundDirectlyAsync(_autoApplyFactory!, autoAddress, "hr@notifications-auto-test.com",
            "Recruiting Team", "Update on your recent application", "There's news about your application.");

        // A second suggestion that stays Pending (rule-based, never auto-applies), then gets
        // manually confirmed by the user.
        var confirmedAppId = await CreateApplicationAsync("Notifications Confirmed Co", _autoApplyClient);
        await ProcessInboundDirectlyAsync(_autoApplyFactory!, autoAddress, "recruiter@notifications-confirmed-test.com",
            "Notifications Confirmed Co Recruiting", "Interview invitation", "We'd like to invite you to an interview.");

        var pendingSuggestions = await _autoApplyClient.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var pendingId = pendingSuggestions.EnumerateArray()
            .Single(s => s.GetProperty("applicationId").GetGuid() == confirmedAppId).GetProperty("id").GetString();
        (await _autoApplyClient.PostAsync($"/api/email-forwarding/suggestions/{pendingId}/confirm", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // A third suggestion that gets dismissed — must never show up as a notification.
        var dismissedAppId = await CreateApplicationAsync("Notifications Dismissed Co", _autoApplyClient);
        await ProcessInboundDirectlyAsync(_autoApplyFactory!, autoAddress, "recruiter@notifications-dismissed-test.com",
            "Notifications Dismissed Co Recruiting", "Interview invitation", "We'd like to invite you to an interview.");
        var pendingSuggestions2 = await _autoApplyClient.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var dismissedId = pendingSuggestions2.EnumerateArray()
            .Single(s => s.GetProperty("applicationId").GetGuid() == dismissedAppId).GetProperty("id").GetString();
        (await _autoApplyClient.PostAsync($"/api/email-forwarding/suggestions/{dismissedId}/dismiss", null))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var notifications = await _autoApplyClient.GetFromJsonAsync<JsonElement>("/api/email-forwarding/notifications", JsonOptions);
        var notificationList = notifications.EnumerateArray().ToList();
        notificationList.Count.ShouldBe(2);
        notificationList.ShouldContain(n => n.GetProperty("applicationId").GetGuid() == autoAppliedAppId && n.GetProperty("wasAutoApplied").GetBoolean());
        notificationList.ShouldContain(n => n.GetProperty("applicationId").GetGuid() == confirmedAppId && !n.GetProperty("wasAutoApplied").GetBoolean());

        var countBeforeRead = await _autoApplyClient.GetFromJsonAsync<JsonElement>("/api/email-forwarding/notifications/count", JsonOptions);
        countBeforeRead.GetProperty("unreadCount").GetInt32().ShouldBe(1);

        (await _autoApplyClient.PostAsync("/api/email-forwarding/notifications/read", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var countAfterRead = await _autoApplyClient.GetFromJsonAsync<JsonElement>("/api/email-forwarding/notifications/count", JsonOptions);
        countAfterRead.GetProperty("unreadCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task ExtensionSignal_Requires_Auth()
    {
        var unauthenticatedClient = _factory!.CreateClient();
        var response = await unauthenticatedClient.PostAsJsonAsync("/api/email-forwarding/extension-signal", new
        {
            senderEmail = "recruiter@ext-auth-test.com", senderDisplayName = "Ext Auth Test Recruiting",
            subject = "Interview invitation", snippet = "We'd like to invite you to an interview.",
            receivedAt = DateTimeOffset.UtcNow, linkDomains = Array.Empty<string>(), gmailMessageId = "thread-auth-1"
        }, JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExtensionSignal_Lazily_Creates_Extension_Connection_And_Suggestion()
    {
        var applicationId = await CreateApplicationAsync("Ext Signal Test");

        await SendExtensionSignalAsync("recruiter@ext-signal-test.com", "Ext Signal Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.", "thread-ext-1");

        var suggestionsResponse = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/suggestions", JsonOptions);
        var suggestion = suggestionsResponse.EnumerateArray().Single();
        suggestion.GetProperty("applicationId").GetGuid().ShouldBe(applicationId);

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailConnections.CountAsync(c => c.Provider == EmailProvider.Extension)).ShouldBe(1);
    }

    [Fact]
    public async Task ExtensionSignal_Is_Idempotent_On_Repeated_GmailMessageId()
    {
        await CreateApplicationAsync("Ext Idempotent Test");

        await SendExtensionSignalAsync("recruiter@ext-idempotent-test.com", "Ext Idempotent Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.", "thread-ext-dup");
        await SendExtensionSignalAsync("recruiter@ext-idempotent-test.com", "Ext Idempotent Test Recruiting",
            "Interview invitation", "We'd like to invite you to an interview.", "thread-ext-dup");

        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.EmailSuggestions.CountAsync(s => s.Subject == "Interview invitation")).ShouldBe(1);
    }

    [Fact]
    public async Task ExtensionSignal_Auto_Applies_Like_Forwarding_Path_When_Confidence_Qualifies()
    {
        var applicationId = await CreateApplicationAsync("Ext AutoApply Test", _autoApplyClient);
        Guid userId;
        using (var scope = _autoApplyFactory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var application = await db.Applications.SingleAsync(a => a.Id == applicationId);
            userId = application.UserId;
            var company = await db.Companies.SingleAsync(c => c.Id == application.CompanyId);
            company.EnrichFrom("https://ext-autoapply-test.com", industry: null, country: null, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        _fakeClassificationProvider.Result = new EmailClassificationResult(ApplicationStatus.Interview, 0.95, "Llm:Interview");

        // Direct in-process call, not SendExtensionSignalAsync's HTTP+Hangfire path — same reason
        // ProcessInboundDirectlyAsync exists (see its own comment): Hangfire storage is shared across
        // this class's three factories, so an enqueued job isn't guaranteed to be processed by
        // _autoApplyFactory's own background server/config, and this test's whole point is that
        // config's auto-apply behavior.
        await ProcessExtensionSignalDirectlyAsync(_autoApplyFactory!, userId, "hr@ext-autoapply-test.com", "Ext AutoApply Recruiting",
            "Update on your recent application", "There's news about your application.", "thread-ext-autoapply");

        using var verifyScope = _autoApplyFactory!.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suggestion = await verifyDb.EmailSuggestions.SingleAsync(s => s.ApplicationId == applicationId);
        suggestion.Status.ShouldBe(EmailSuggestionStatus.AutoApplied);
        suggestion.MatchType.ShouldBe(EmailApplicationMatchType.DomainMatch);
    }

    // Calls IEmailForwardingService.ProcessInboundEmailAsync directly within a given factory's own
    // DI scope, instead of POSTing to /inbound and waiting on Hangfire. _factory/_disabledFactory/
    // _autoApplyFactory all share the same Postgres-backed Hangfire storage (see
    // WaitForHangfireIdleAsync's own comment about JobStorage.Current being a shared static), so a
    // job enqueued via one factory's client is not guaranteed to be processed by that same factory's
    // background server. Tests where the whole point is "which factory's config governs this
    // decision" need that determinism; SendInboundAsync stays fine for tests that only care about
    // the HTTP contract or where the outcome doesn't depend on which factory's config wins.
    private static async Task ProcessInboundDirectlyAsync(WebApplicationFactory<Program> factory,
        string toAddress, string from, string fromName, string subject, string snippet)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmailForwardingService>();
        await service.ProcessInboundEmailAsync(
            new InboundEmailRequest(toAddress, from, fromName, subject, snippet, DateTimeOffset.UtcNow, Array.Empty<string>()),
            CancellationToken.None);
    }

    // Same determinism reasoning as ProcessInboundDirectlyAsync above, for the extension-signal path.
    private static async Task ProcessExtensionSignalDirectlyAsync(WebApplicationFactory<Program> factory,
        Guid userId, string senderEmail, string senderDisplayName, string subject, string snippet, string gmailMessageId)
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmailForwardingService>();
        await service.ProcessExtensionSignalAsync(userId,
            new ExtensionEmailSignalRequest(senderEmail, senderDisplayName, subject, snippet, DateTimeOffset.UtcNow,
                Array.Empty<string>(), gmailMessageId),
            CancellationToken.None);
    }

    private async Task<HttpResponseMessage> SendInboundAsync(string toAddress, string from, string fromName, string subject, string snippet,
        WebApplicationFactory<Program>? factory = null)
    {
        factory ??= _factory;

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/email-forwarding/inbound")
        {
            Content = JsonContent.Create(new { to = toAddress, from, fromName, subject, snippet }, options: JsonOptions)
        };
        request.Headers.Add("X-Webhook-Secret", WebhookSecret);

        var unauthenticatedClient = factory!.CreateClient();
        var response = await unauthenticatedClient.SendAsync(request);
        await WaitForHangfireIdleAsync(factory);
        return response;
    }

    // Also processed out-of-request via Hangfire (see /extension-signal's own comment) — same
    // wait-for-idle requirement as SendInboundAsync above, for the same reason.
    private async Task<HttpResponseMessage> SendExtensionSignalAsync(string senderEmail, string senderDisplayName,
        string subject, string snippet, string gmailMessageId, HttpClient? client = null, WebApplicationFactory<Program>? factory = null)
    {
        client ??= _client;
        factory ??= _factory;

        var response = await client.PostAsJsonAsync("/api/email-forwarding/extension-signal", new
        {
            senderEmail, senderDisplayName, subject, snippet,
            receivedAt = DateTimeOffset.UtcNow, linkDomains = Array.Empty<string>(), gmailMessageId
        }, JsonOptions);
        await WaitForHangfireIdleAsync(factory);
        return response;
    }

    // Processing runs out-of-request via a Hangfire job now (see EmailForwardingEndpoints.cs) —
    // the POST only ever enqueues and returns 204 immediately. Every assertion that depends on
    // classification/extraction side effects (including a "this must NOT have happened" assertion,
    // where there's nothing else to poll for) must wait for that job to actually finish first.
    // Queried straight from Hangfire's own Postgres tables (same _postgres container AppDbContext
    // uses) rather than resolved JobStorage/IMonitoringApi from DI: this test class spins up a
    // second factory (_disabledFactory) whose own AddHangfire call sets the same process-wide
    // JobStorage.Current static, so resolving JobStorage from _factory's container isn't guaranteed
    // to actually be _factory's own storage.
    private async Task WaitForHangfireIdleAsync(WebApplicationFactory<Program>? factory = null)
    {
        factory ??= _factory;

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = factory!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pending = await db.Database
                .SqlQueryRaw<int>("SELECT COUNT(*)::int AS \"Value\" FROM hangfire.job WHERE statename IN ('Enqueued', 'Processing')")
                .SingleAsync();

            if (pending == 0)
            {
                return;
            }

            await Task.Delay(50);
        }

        throw new TimeoutException("Hangfire did not finish processing the enqueued inbound-email job within 30s.");
    }

    private async Task<string> GetOwnAddressAsync(HttpClient? client = null)
    {
        client ??= _client;
        var response = await client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/address", JsonOptions);
        return response.GetProperty("address").GetString()!;
    }

    private async Task<Guid> CreateApplicationAsync(string companyName, HttpClient? client = null)
    {
        client ??= _client;
        var response = await client.PostAsJsonAsync("/api/applications", new CreateApplicationRequest(
            companyName, "Engineer", null, null, EmploymentType.FullTime,
            DateTimeOffset.UtcNow.AddDays(-5), null, null), JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        return created!.Id;
    }
}
