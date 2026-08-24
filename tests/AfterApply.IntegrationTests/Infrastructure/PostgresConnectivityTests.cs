using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Testcontainers.PostgreSql;

namespace AfterApply.IntegrationTests.Infrastructure;

public class PostgresConnectivityTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task AppDbContext_Can_Connect_To_Postgres()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        await using var db = new AppDbContext(options);

        var canConnect = await db.Database.CanConnectAsync();

        canConnect.ShouldBeTrue();
    }
}
