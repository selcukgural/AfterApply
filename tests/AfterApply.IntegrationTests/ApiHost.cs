using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace AfterApply.IntegrationTests;

/// <summary>
/// What a test class needs from the app, and what it owns beyond the host: the fakes, stubs and
/// clock its tests read and mutate. Implemented once per test class (or shared, like
/// <see cref="DefaultProfile" />), handed to <see cref="ApiHost{TProfile}" />, reached through
/// <c>host.Profile</c>.
/// </summary>
public interface IHostProfile
{
    /// <summary>Applied to the fixture's host and to every Variant/Standalone host built from
    /// it — settings, service replacements, stubbed HttpClient handlers.</summary>
    void Configure(IWebHostBuilder builder)
    {
    }

    /// <summary>Called at the start of every test, after the database was emptied: put every
    /// fake back in its starting state, because the same instances serve the whole class.</summary>
    void Reset()
    {
    }

    /// <summary>Runs before the host boots — for a profile that needs its own container (fake
    /// GCS) up first.</summary>
    ValueTask InitializeAsync() => default;

    /// <summary>Runs after the host is disposed.</summary>
    ValueTask DisposeAsync() => default;
}

/// <summary>The profile for a class that needs nothing beyond the app as shipped.</summary>
public sealed class DefaultProfile : IHostProfile;

/// <summary>
/// One API host per test class, shared by all of its tests.
///
/// This is the fix for the suite's growth curve. Every test class here implemented
/// IAsyncLifetime and built its WebApplicationFactory there — and xunit constructs the class once
/// per test method, so a 454-test run booted 500+ hosts (three per test in the worst class). Each
/// boot is the whole of Program: DI, Hangfire storage, six recurring-job writes, a
/// data-protection key, a dozen HttpClients. Disposed hosts were never collected (an
/// appsettings.json reload watcher rooted every one; see TestContainerCleanup), so the process
/// carried all of them, and the run got slower per test as it went: 107 tests took 80 s, 454
/// took 33 minutes and then crashed. A class fixture brings that to one host per class, ~60 per
/// run, which is where the suite was when it was last fast and stable.
///
/// Isolation between tests moves from "a fresh database per test" to <see cref="ResetAsync" />,
/// which every test calls first: the class's database is emptied, its Redis database is flushed,
/// every host's memory cache is cleared, the inline job queue is dropped and the profile's fakes
/// are reset. The database is still a private, migrated clone per class (SharedInfrastructure),
/// and the Redis database and backplane channel are the class's own, so classes cannot see each
/// other at all.
///
/// A subclass of WebApplicationFactory rather than <c>new WebApplicationFactory().WithWebHostBuilder(...)</c>:
/// that idiom leaves the outer factory undisposed, holding the derived one, which is one more
/// reason the old hosts never went away.
/// </summary>
public abstract class ApiHost : WebApplicationFactory<Program>, IAsyncLifetime
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public const string DefaultPassword = "P@ssw0rd123!";

    private readonly SharedInfrastructure _shared;
    private readonly Dictionary<string, WebApplicationFactory<Program>> _variants = new(StringComparer.Ordinal);
    private IsolatedStores? _stores;

    protected ApiHost(SharedInfrastructure shared, IHostProfile profile)
    {
        _shared = shared;
        Profile = profile;
    }

    public IHostProfile Profile { get; }

    /// <summary>Background jobs this class's hosts enqueued. <see cref="RunJobsAsync" /> performs
    /// them; <c>Jobs.Pending</c> / <c>Jobs.Failed</c> are what a test asserts on.</summary>
    public InlineJobQueue Jobs { get; } = new();

    /// <summary>One signing key for the class, shared by every variant, so a token issued by one
    /// host is accepted by another over the same database.</summary>
    public string JwtSigningKey { get; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>The class's own migrated database. Set by InitializeAsync, before the host boots.</summary>
    public string ConnectionString => Stores.Postgres;

    /// <summary>The class's Postgres clone and Redis database. Every Variant/Standalone host built
    /// from this fixture shares them — which is what lets a test write through one host and read
    /// through another with a separate L1, the multi-instance shape production runs in.</summary>
    public IsolatedStores Stores => _stores ?? throw new InvalidOperationException("The fixture has not been initialised yet.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Stores.Apply(builder);
        builder.UseSetting("Jwt:SigningKey", JwtSigningKey);
        builder.ConfigureTestServices(services => InlineBackgroundJobs.Register(services, Jobs));
        Profile.Configure(builder);
    }

    public async Task InitializeAsync()
    {
        await Profile.InitializeAsync();
        _stores = await _shared.CreateIsolatedStoresAsync(GetType().Name);

        // Boot now rather than on the first test's first request, so a host that cannot start
        // fails the fixture — attributed to the class, once — instead of the first test that
        // happens to run.
        _ = Services;
    }

    /// <summary>Every test's first line. Cheap (one TRUNCATE, a few cache clears), and what makes
    /// a class-shared host safe: nothing a previous test wrote, cached or enqueued survives.</summary>
    public async Task ResetAsync()
    {
        await _shared.ResetDatabaseAsync(ConnectionString);

        // After the truncate, never before: CompanyResolver caches company name → id,
        // PersonalAccessTokenService caches token → user, CompanySearchService caches results.
        // A cached id pointing at a truncated row is a foreign-key failure two tests later.
        // L2 first, then every host's L1 — in that order, so a lapsed L1 entry cannot be refilled
        // from a not-yet-flushed L2 in between.
        await _shared.FlushRedisAsync(Stores.RedisDatabase);
        foreach (var host in BuiltHosts())
        {
            ((MemoryCache)host.Services.GetRequiredService<IMemoryCache>()).Clear();
        }

        Jobs.Clear();
        Profile.Reset();
    }

    /// <summary>Performs every job the class's hosts enqueued since the last reset or run.</summary>
    public Task RunJobsAsync() => Jobs.RunAsync();

    /// <summary>
    /// The same app over the same database with different configuration, built once per class on
    /// first use and disposed with the fixture. For the classes that compare "flag on" with "flag
    /// off": the variant shares the database, the job queue and the signing key, so a user
    /// registered on one host can call the other.
    /// </summary>
    public WebApplicationFactory<Program> Variant(string name, Action<IWebHostBuilder> configure)
    {
        if (!_variants.TryGetValue(name, out var variant))
        {
            variant = WithWebHostBuilder(configure);
            _variants[name] = variant;
        }

        return variant;
    }

    /// <summary>
    /// A host for one test only, over the class's database, that the test disposes itself
    /// (<c>await using</c>). The exception, not the rule: for a test whose configuration cannot be
    /// shared with its neighbours — rate limiting on (its fixed windows have no reset), a review
    /// provider that answers differently per test.
    /// </summary>
    public WebApplicationFactory<Program> Standalone(Action<IWebHostBuilder> configure) => WithWebHostBuilder(configure);

    /// <summary>Registers a user on the given host (the fixture's own by default) and returns a
    /// client carrying its bearer token.</summary>
    public async Task<(HttpClient Client, AuthResponse Auth)> RegisterAsync(
        string email, string firstName = "Test", string lastName = "User", bool consentAccepted = true,
        WebApplicationFactory<Program>? on = null)
    {
        var client = (on ?? this).CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, DefaultPassword, firstName, lastName, consentAccepted), JsonOptions);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return (client, auth);
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task WithScopeAsync(Func<IServiceProvider, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    /// <summary>Marks the user admin straight in the database (there is no endpoint for it).</summary>
    public Task MakeAdminAsync(Guid userId) => WithDbAsync(async db =>
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId);
        user.IsAdmin = true;
        await db.SaveChangesAsync();
    });

    private IEnumerable<WebApplicationFactory<Program>> BuiltHosts()
    {
        yield return this;
        foreach (var variant in _variants.Values)
        {
            yield return variant;
        }
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await base.DisposeAsync();
        }
        finally
        {
            await Profile.DisposeAsync();
            await DropDatabaseAsync();
        }
    }

    Task IAsyncLifetime.DisposeAsync() => DisposeAsync().AsTask();

    private async Task DropDatabaseAsync()
    {
        if (_stores is null)
        {
            return;
        }

        try
        {
            // Safe now in a way it was not before (SharedInfrastructure's warning about
            // ClearAllPools): these hosts run no Hangfire server, so no background thread is
            // holding a connector this pulls away. Only this class's pool is cleared, and the
            // database is dropped so the container does not carry ~60 dead clones to the end of
            // the run.
            NpgsqlConnection.ClearPool(new NpgsqlConnection(ConnectionString));
            await _shared.DropDatabaseAsync(_stores.DatabaseName);
        }
        catch (Exception)
        {
            // Best effort. A clone that outlives its class costs disk in the container, not
            // correctness, and it dies with the container anyway.
        }
    }
}

/// <summary>The class fixture form: <c>IClassFixture&lt;ApiHost&lt;MyProfile&gt;&gt;</c>. xunit builds it
/// once per class, resolving <see cref="SharedInfrastructure" /> from the collection.</summary>
public sealed class ApiHost<TProfile>(SharedInfrastructure shared) : ApiHost(shared, new TProfile())
    where TProfile : class, IHostProfile, new()
{
    public new TProfile Profile => (TProfile)base.Profile;
}
