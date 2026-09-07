using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Feedback;
using AfterApply.Application.Feedback.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Feedback;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Feedback;

/// <summary>
/// The in-app feedback panel's endpoint, end to end. The GitHub mirror is deliberately left
/// unconfigured for this class — see <see cref="FeedbackGitHubMirrorTests"/> for the other half.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class FeedbackFlowTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private StubGitHubHandler _gitHubHandler = null!;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(FeedbackFlowTests));
        _gitHubHandler = new StubGitHubHandler();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));

            // The mirror is off for this class (TestContainerCleanup turns it off for every host),
            // so nothing here should ever call GitHub. The stub is the belt to that braces: if the
            // switch ever fails, the call lands in a recorder the tests can assert on instead of
            // opening a real issue in the live feedback repository — which is exactly what happened
            // on 2026-09-07, five times, before this was here.
            builder.ConfigureServices(services =>
                services.AddHttpClient(nameof(IGitHubIssueMirror))
                    .ConfigurePrimaryHttpMessageHandler(() => _gitHubHandler));
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _factory!.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Feed", "Back", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private async Task<FeedbackEntry> ReadStoredAsync(Guid id)
    {
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entry = await dbContext.FeedbackEntries.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id);
        entry.ShouldNotBeNull();
        return entry!;
    }

    [Fact]
    public async Task Anonymous_Callers_Cannot_Send_Feedback()
    {
        var client = _factory!.CreateClient();

        var response = await client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Idea, "Let me group by company."), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Submitting_Stores_The_Message_Against_The_Caller_With_Its_Context()
    {
        var client = await AuthenticatedClientAsync("feedback.submit@example.com");
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Macintosh) TestAgent/1.0");

        var response = await client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Bug, "  The status filter forgets my choice.  ",
                FeedbackMood.Struggling, "reply@example.com", "/tr/applications", "tr", "dark"),
            JsonOptions);
        response.EnsureSuccessStatusCode();

        var receipt = await response.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions);
        receipt.ShouldNotBeNull();

        var stored = await ReadStoredAsync(receipt!.Id);
        stored.Category.ShouldBe(FeedbackCategory.Bug);
        stored.Mood.ShouldBe(FeedbackMood.Struggling);
        stored.Message.ShouldBe("The status filter forgets my choice.");
        stored.ReplyEmail.ShouldBe("reply@example.com");
        stored.PagePath.ShouldBe("/tr/applications");
        stored.Locale.ShouldBe("tr");
        stored.Theme.ShouldBe("dark");
        // Read from the header, not the body — the client never asserts this.
        stored.UserAgent.ShouldNotBeNull();
        stored.UserAgent!.ShouldContain("TestAgent/1.0");
        stored.Status.ShouldBe(FeedbackStatus.Received);
        stored.UserId.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task The_Receipt_Says_Nothing_Beyond_That_The_Message_Landed()
    {
        var client = await AuthenticatedClientAsync("feedback.receipt@example.com");

        var response = await client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Question, "Where do I find my CVs?",
                ReplyEmail: "reply@example.com"),
            JsonOptions);
        response.EnsureSuccessStatusCode();

        // The stored row holds a reply address and the message; echoing either back would widen the
        // response for nothing a panel showing "thank you" could use.
        var payload = await response.Content.ReadAsStringAsync();
        payload.ShouldNotContain("reply@example.com");
        payload.ShouldNotContain("Where do I find my CVs?");
    }

    [Fact]
    public async Task A_Query_String_Is_Stripped_From_The_Page_Context()
    {
        var client = await AuthenticatedClientAsync("feedback.pagepath@example.com");

        var response = await client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Idea, "Sorting by company would help.",
                PagePath: "/tr/applications?status=Applied&page=2"),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var receipt = await response.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions);

        var stored = await ReadStoredAsync(receipt!.Id);
        stored.PagePath.ShouldBe("/tr/applications");
    }

    [Fact]
    public async Task Feedback_Is_Not_Mirrored_When_The_GitHub_Bridge_Is_Unconfigured()
    {
        var client = await AuthenticatedClientAsync("feedback.nomirror@example.com");

        var response = await client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Bug, "Nothing should leave the database here."),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var receipt = await response.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions);

        // Asserting a negative about a background job means waiting for the job that should not
        // exist to have had its chance. Without this delay the row is read before Hangfire has run
        // anything, so the assertion passes no matter what the mirror does — which is how five real
        // issues got opened while this test stayed green (2026-09-07).
        await Task.Delay(2000);

        var stored = await ReadStoredAsync(receipt!.Id);
        stored.GitHubIssueNumber.ShouldBeNull();
        stored.MirroredAt.ShouldBeNull();
        // The stronger claim: no request was even attempted.
        _gitHubHandler.Requests.ShouldBeEmpty();
    }

    // One host, every rejection. Written as a Fact looping over the cases rather than a Theory
    // with one case each because xUnit constructs the test class per test method, so a four-case
    // Theory here means four WebApplicationFactories, four databases and four Hangfire servers to
    // start and wind down — the exact volume TestContainerCleanup exists to keep down.
    [Fact]
    public async Task Invalid_Submissions_Are_Rejected()
    {
        var client = await AuthenticatedClientAsync("feedback.invalid@example.com");

        (string Case, SubmitFeedbackRequest Request)[] cases =
        [
            ("nothing written",
                new SubmitFeedbackRequest(FeedbackCategory.Bug, "")),
            ("whitespace only",
                new SubmitFeedbackRequest(FeedbackCategory.Bug, "   ")),
            ("past the column width",
                new SubmitFeedbackRequest(FeedbackCategory.Bug, new string('a', 1001))),
            ("a reply address that is not one",
                new SubmitFeedbackRequest(FeedbackCategory.Bug, "Something broke.", ReplyEmail: "not-an-address")),
            // A whole URL where a path belongs.
            ("an origin instead of a path",
                new SubmitFeedbackRequest(FeedbackCategory.Bug, "Something broke.",
                    PagePath: "https://ekariyerim.com/tr/applications")),
        ];

        foreach (var (name, request) in cases)
        {
            var response = await client.PostAsJsonAsync("/api/feedback", request, JsonOptions);
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, $"case: {name}");
        }
    }

    [Fact]
    public async Task Feedback_Is_In_The_Account_Export_And_Goes_With_The_Account()
    {
        var client = await AuthenticatedClientAsync("feedback.export@example.com");

        var submitResponse = await client.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Question, "Where do I find my CVs?",
                ReplyEmail: "reply@example.com"),
            JsonOptions);
        submitResponse.EnsureSuccessStatusCode();
        var receipt = await submitResponse.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions);

        // What a user wrote is data held about them, so the data-subject export has to carry it.
        var exportResponse = await client.GetAsync("/api/users/me/export");
        exportResponse.EnsureSuccessStatusCode();
        var export = await exportResponse.Content.ReadAsStringAsync();
        export.ShouldContain("Where do I find my CVs?");
        export.ShouldContain("reply@example.com");

        // ...and deleting the account has to take it, which the FeedbackEntries -> Users cascade
        // does (see the CascadeUserOwnedRowsOnAccountDelete migration).
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("P@ssw0rd123!"), options: JsonOptions)
        };
        var deleteResponse = await client.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await dbContext.FeedbackEntries.AnyAsync(f => f.Id == receipt!.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task One_Users_Feedback_Is_Never_Attributed_To_Another()
    {
        var first = await AuthenticatedClientAsync("feedback.owner.a@example.com");
        var second = await AuthenticatedClientAsync("feedback.owner.b@example.com");

        var firstResponse = await first.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Idea, "From the first account."), JsonOptions);
        var secondResponse = await second.PostAsJsonAsync("/api/feedback",
            new SubmitFeedbackRequest(FeedbackCategory.Idea, "From the second account."), JsonOptions);
        firstResponse.EnsureSuccessStatusCode();
        secondResponse.EnsureSuccessStatusCode();

        var firstEntry = await ReadStoredAsync((await firstResponse.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions))!.Id);
        var secondEntry = await ReadStoredAsync((await secondResponse.Content.ReadFromJsonAsync<FeedbackResponse>(JsonOptions))!.Id);

        firstEntry.UserId.ShouldNotBe(secondEntry.UserId);
    }
}
