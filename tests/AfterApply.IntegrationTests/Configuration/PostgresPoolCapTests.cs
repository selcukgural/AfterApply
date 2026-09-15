using System.Net;
using System.Security.Cryptography;
using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace AfterApply.IntegrationTests.Configuration;

/// <summary>
/// Production runs with <c>Postgres:MaxPoolSize</c> set (deploy.yml), because the db-f1-micro's
/// 25 connection slots were being exhausted by several Cloud Run instances each holding a
/// full-size Npgsql pool plus ten Hangfire workers (DECISIONS.md 2026-09-12). This host runs the
/// same way — cap on, background server on, two workers — and checks the cap actually reaches
/// the DbContext's connection and that the API and Hangfire both still work under it.
///
/// One of the two classes (with HangfireServerInTestsTests) that still starts a real Hangfire
/// server; everything else runs jobs inline. Built raw rather than through ApiHost for that
/// reason, and per test: a server-carrying host is exactly what must not be shared.
///
/// The cap is production's 5, not the 3 an earlier version used: with 3, two workers holding
/// their fetch connections left one slot for the heartbeat, watchdog, schedulers,
/// ExpirationManager, the recurring-job writes and the test's own query — a pool starvation that
/// turned every step into a chain of 15-second Npgsql timeouts and hung one run for 40 minutes
/// (2026-09-15). That test was also the suite's only synchronous <c>void</c> fact starting a host,
/// blocking xunit's single worker thread inside <c>host.Start()</c>; it is async now, and its
/// storage read is bounded so a stall fails in seconds with a message rather than never.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PostgresPoolCapTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private CappedFactory? _factory;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(PostgresPoolCapTests));
        _factory = new CappedFactory(postgres);
    }

    public Task DisposeAsync() => TestHostDisposal.DisposeQuietlyAsync(_factory);

    private sealed class CappedFactory(string postgres) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            // deploy.yml: Postgres__MaxPoolSize=5, Hangfire__WorkerCount=2.
            builder.UseSetting(PostgresConnectionString.MaxPoolSizeKey, "5");
            builder.UseSetting("Hangfire:ServerEnabled", "true");
            builder.UseSetting("Hangfire:WorkerCount", "2");
        }
    }

    [Fact]
    public async Task The_Cap_Reaches_The_DbContext_And_The_Host_Still_Serves()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);
        connection.MaxPoolSize.ShouldBe(5);

        // A real query through the capped pool, and the health check (its own Npgsql probe on the
        // same string) — both go green only if the capped string is a valid one.
        (await db.Users.CountAsync()).ShouldBe(0);
        var health = await _factory.CreateClient().GetAsync("/health");
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Hangfire_Runs_Under_The_Same_Cap_With_The_Configured_Workers()
    {
        // Resolving the host's own JobStorage (not the process-wide JobStorage.Current, which any
        // earlier host in this process may have set) starts the host and with it the background
        // server; Hangfire then announces the server through the same connection string, which is
        // how it lands in the pool the cap applies to rather than a pool of its own.
        var storage = _factory!.Services.GetRequiredService<JobStorage>();

        // The server announces itself from its own thread once the host is up, so the row can lag
        // the start by a moment; read until it is there. Bounded, and the storage API is
        // synchronous: a pool stall must fail this test in 20 s with a message, not stall the run
        // until --blame-hang kills it. This is the one legitimate poll left in the suite — it waits
        // on a real Hangfire server, which only this class and HangfireServerInTestsTests run.
        var deadline = DateTime.UtcNow.AddSeconds(20);
        IList<Hangfire.Storage.Monitoring.ServerDto> servers;
        while (true)
        {
            servers = await Task.Run(() => storage.GetMonitoringApi().Servers()).WaitAsync(deadline - DateTime.UtcNow);
            if (servers.Count > 0 || DateTime.UtcNow >= deadline)
            {
                break;
            }

            await Task.Delay(100);
        }

        var server = servers.ShouldHaveSingleItem("the Hangfire server did not announce itself within 20 s");
        server.WorkersCount.ShouldBe(2);
    }
}
