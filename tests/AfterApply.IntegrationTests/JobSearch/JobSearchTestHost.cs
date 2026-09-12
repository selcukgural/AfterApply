using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.JobSearch;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AfterApply.IntegrationTests.JobSearch;

/// <summary>
/// One host per test class with the stub provider wired in and the feature switched on. Turning
/// it on here only: TestContainerCleanup forces it off process-wide through environment
/// variables, which sit above user secrets, so a developer's live RapidAPI key can never reach a
/// test host. The in-memory source added here is appended last and beats those variables.
/// </summary>
internal sealed class JobSearchTestHost : IAsyncDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public WebApplicationFactory<Program> Factory { get; }

    public StubJSearchHandler Handler { get; }

    private JobSearchTestHost(WebApplicationFactory<Program> factory, StubJSearchHandler handler)
    {
        Factory = factory;
        Handler = handler;
    }

    public static async Task<JobSearchTestHost> StartAsync(SharedInfrastructure shared, string databaseName,
        IDictionary<string, string?>? settings = null, bool enabled = true, string? apiKey = "test-rapidapi-key")
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(databaseName);
        var handler = new StubJSearchHandler();

        var configuration = new Dictionary<string, string?>
        {
            ["JobSearch:Enabled"] = enabled ? "true" : "false",
            ["JobSearch:ApiKey"] = apiKey,
            ["JobSearch:RetryDelayMilliseconds"] = "0"
        };
        if (settings is not null)
        {
            foreach (var (key, value) in settings)
            {
                configuration[key] = value;
            }
        }

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(configuration));
            // The typed client's name is the interface's short name, so re-registering it here
            // appends to the same named options and replaces the primary handler.
            builder.ConfigureServices(services =>
                services.AddHttpClient(nameof(IJSearchClient)).ConfigurePrimaryHttpMessageHandler(() => handler));
        });

        return new JobSearchTestHost(factory, handler);
    }

    public async Task<HttpClient> RegisterAsync(string email)
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Job", "Search", true), Json);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        // The API's default culture is Turkish; the assertions below read the English text, and
        // one test sends "tr" explicitly to pin the other half.
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return client;
    }

    public async Task<Guid> UserIdAsync(string email)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Users.SingleAsync(u => u.Email == email)).Id;
    }

    public async Task SetAdminAsync(string email)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.IsAdmin = true;
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await query(db);
    }

    public async ValueTask DisposeAsync() => await TestHostDisposal.DisposeQuietlyAsync(Factory);
}
