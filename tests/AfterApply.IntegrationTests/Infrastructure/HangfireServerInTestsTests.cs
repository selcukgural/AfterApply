using System.Security.Cryptography;
using Hangfire;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AfterApply.IntegrationTests.Infrastructure;

/// <summary>
/// The guard for the suite's stability fix: a test host starts no Hangfire background server unless
/// its test asked for one, and can still enqueue.
///
/// Both halves matter. Without the first, every host pays Hangfire's shutdown on disposal —
/// ExpirationManager ignores the shutdown token, so the wait runs to its timeout and the run ends as
/// a TaskCanceledException out of DisposeAsync, a hang, or a crashed test host (see
/// TestContainerCleanup.DisableHangfireServerForTests). Without the second, "no server" would mean
/// endpoints that enqueue start failing and the fix would be worse than the problem: production
/// enqueues through IBackgroundJobClient in the request path, so that has to keep working here.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class HangfireServerInTestsTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private string _postgres = null!;

    public async Task InitializeAsync() =>
        _postgres = await shared.CreateIsolatedDatabaseAsync(nameof(HangfireServerInTestsTests));

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task A_Plain_Test_Host_Runs_No_Background_Server()
    {
        await using var factory = Build();

        HostedServices(factory).ShouldNotContain(
            name => name.Contains("BackgroundJobServer", StringComparison.Ordinal),
            "A test host started a Hangfire server without asking for one. Shutting those down is " +
            "what made this suite hang and crash; see TestContainerCleanup.");
    }

    [Fact]
    public async Task A_Test_That_Asks_For_One_Gets_It()
    {
        // The escape hatch the import, enrichment, email-signal, feedback, password-reset and
        // mailing classes use. If this stops working those classes go green while asserting nothing.
        await using var factory = Build(builder => builder.UseSetting("Hangfire:ServerEnabled", "true"));

        HostedServices(factory).ShouldContain(
            name => name.Contains("BackgroundJobServer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Enqueuing_Still_Works_Without_A_Server()
    {
        // Nothing runs the job — that is the point — but the request path that enqueues it must not
        // fail, because that is production's path.
        await using var factory = Build();
        var jobs = factory.Services.GetRequiredService<IBackgroundJobClient>();

        var jobId = jobs.Enqueue<IHangfireProbe>(probe => probe.DoNothing());

        jobId.ShouldNotBeNullOrWhiteSpace();
    }

    private WebApplicationFactory<Program> Build(Action<Microsoft.AspNetCore.Hosting.IWebHostBuilder>? extra = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            extra?.Invoke(builder);
        });

    private static string[] HostedServices(WebApplicationFactory<Program> factory) =>
        factory.Services.GetServices<IHostedService>()
            .Select(service => service.GetType().Name)
            .ToArray();
}

/// <summary>A job target that exists only so the enqueue above has something real to serialise.</summary>
public interface IHangfireProbe
{
    void DoNothing();
}
