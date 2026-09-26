using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.Infrastructure;

// The smoke test for the suite's own plumbing: if this fails, every other integration test is
// failing for the same reason and their output is noise. Uses the shared server like everything
// else — starting a container of its own just to prove a connection works would be the exact cost
// SharedInfrastructure exists to remove.
public class PostgresConnectivityTests
{
    [Fact]
    public async Task AppDbContext_Can_Connect_To_Postgres()
    {
        var stores = await (await SharedInfrastructure.GetAsync()).CreateIsolatedStoresAsync(nameof(PostgresConnectivityTests));

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(stores.Postgres)
            .Options;

        await using var db = new AppDbContext(options);

        var canConnect = await db.Database.CanConnectAsync();

        canConnect.ShouldBeTrue();
    }
}
