using System.Runtime.CompilerServices;
using Hangfire;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Infrastructure;

/// <summary>
/// The tripwires for the two things that made this suite's run time grow faster than its test
/// count: hosts that never went away, and a host per test.
///
/// The 2026-09-03 heap dump found every disposed test host still alive — 102 of them at test 47 —
/// each keeping its timers, threads and Npgsql pool. The cause was the configuration reload
/// watcher (TestContainerCleanup.DoNotWatchConfigurationFiles); the fix is only worth anything if
/// it stays fixed, so the first test builds a host, disposes it, and demands the garbage collector
/// can actually take it.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class HostLifecycleTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>
{
    [Fact]
    public async Task A_Disposed_Host_Is_Collectable()
    {
        var reference = await BuildUseAndDisposeAsync();

        // The last host to boot is rooted by process-wide statics regardless (Serilog's
        // Log.Logger, Hangfire's GlobalConfiguration/JobStorage.Current), so a second, throwaway
        // host takes that place before the first one is checked.
        await using (var successor = host.Standalone(_ => { }))
        {
            _ = successor.Services;
        }

        Collect();

        reference.IsAlive.ShouldBeFalse(
            "A disposed test host is still reachable. Something roots WebApplicationFactory hosts " +
            "again — every run will carry all of them, and its run time will grow superlinearly " +
            "with its test count (DECISIONS.md 2026-09-03 and 2026-09-15).");
    }

    [Fact]
    public async Task The_Class_Fixture_Host_Uses_The_Inline_Job_Client()
    {
        // The default for every ApiHost: jobs are recorded and run by the test, never by a
        // Hangfire server. If this stops holding, job-asserting tests go back to polling.
        await host.ResetAsync();

        host.Services.GetRequiredService<IBackgroundJobClient>().ShouldBeOfType<InlineBackgroundJobClient>();
        host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>()
            .Select(service => service.GetType().Name)
            .ShouldNotContain(name => name.Contains("BackgroundJobServer", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Reset_Empties_The_Database_And_The_Job_Queue_Between_Tests()
    {
        await host.ResetAsync();
        var (_, auth) = await host.RegisterAsync("lifecycle.reset@example.com");
        host.Services.GetRequiredService<IBackgroundJobClient>().Enqueue<IHangfireProbe>(probe => probe.DoNothing());
        host.Jobs.Pending.Count.ShouldBe(1);

        await host.ResetAsync();

        (await host.WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .AnyAsync(db.Users, u => u.Id == auth.User.Id))).ShouldBeFalse();
        host.Jobs.Pending.ShouldBeEmpty();
        // The seeded templates and the key ring survive the reset — the host is still usable.
        (await host.WithDbAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
            .AnyAsync(db.EmailTemplates))).ShouldBeTrue();
        (await host.RegisterAsync("lifecycle.after@example.com")).Client.ShouldNotBeNull();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task<WeakReference> BuildUseAndDisposeAsync()
    {
        // A factory nobody else holds: not Standalone (the fixture keeps every derived factory in
        // its own list until it is disposed) and not the WithWebHostBuilder idiom (the outer
        // factory is never disposed). The reference is to the host's service provider, which is
        // what all the leaked hosts were.
        await using var factory = new PlainFactory(host.ConnectionString, host.JwtSigningKey);
        var reference = new WeakReference(factory.Services);
        (await factory.CreateClient().GetAsync("/health")).EnsureSuccessStatusCode();
        return reference;
    }

    private sealed class PlainFactory(string postgres, string signingKey) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", signingKey);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}
