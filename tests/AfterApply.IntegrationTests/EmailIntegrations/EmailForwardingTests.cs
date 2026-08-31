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
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

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

    private async Task<HttpResponseMessage> SendInboundAsync(string toAddress, string from, string fromName, string subject, string snippet)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/email-forwarding/inbound")
        {
            Content = JsonContent.Create(new { to = toAddress, from, fromName, subject, snippet }, options: JsonOptions)
        };
        request.Headers.Add("X-Webhook-Secret", WebhookSecret);

        var unauthenticatedClient = _factory!.CreateClient();
        return await unauthenticatedClient.SendAsync(request);
    }

    private async Task<string> GetOwnAddressAsync()
    {
        var response = await _client.GetFromJsonAsync<JsonElement>("/api/email-forwarding/address", JsonOptions);
        return response.GetProperty("address").GetString()!;
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
}
