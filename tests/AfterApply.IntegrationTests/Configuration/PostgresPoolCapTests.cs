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
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class PostgresPoolCapTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(PostgresPoolCapTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting(PostgresConnectionString.MaxPoolSizeKey, "3");
            builder.UseSetting("Hangfire:ServerEnabled", "true");
            builder.UseSetting("Hangfire:WorkerCount", "2");
        });
    }

    public Task DisposeAsync() => TestHostDisposal.DisposeQuietlyAsync(_factory);

    [Fact]
    public async Task The_Cap_Reaches_The_DbContext_And_The_Host_Still_Serves()
    {
        using var scope = _factory!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connection = new NpgsqlConnectionStringBuilder(db.Database.GetDbConnection().ConnectionString);
        connection.MaxPoolSize.ShouldBe(3);

        // A real query through the capped pool, and the health check (its own Npgsql probe on the
        // same string) — both go green only if the capped string is a valid one.
        (await db.Users.CountAsync()).ShouldBe(0);
        var health = await _factory.CreateClient().GetAsync("/health");
        health.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void Hangfire_Runs_Under_The_Same_Cap_With_The_Configured_Workers()
    {
        // Resolving the host's own JobStorage (not the process-wide JobStorage.Current, which any
        // earlier host in this process may have set) starts the host and with it the background
        // server; Hangfire then announces the server through the same connection string, which is
        // how it lands in the pool the cap applies to rather than a pool of its own.
        var storage = _factory!.Services.GetRequiredService<JobStorage>();

        var servers = storage.GetMonitoringApi().Servers();

        var server = servers.ShouldHaveSingleItem();
        server.WorkersCount.ShouldBe(2);
    }
}
