using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AfterApply.Infrastructure.Persistence;

/// <remarks>
/// dotnet ef doesn't run through Api's launchSettings.json, so ASPNETCORE_ENVIRONMENT
/// isn't Development and automatic user-secrets loading never fires — this factory
/// loads user-secrets directly so `dotnet ef migrations add` works against the same
/// local connection string the running app uses. Environment variables are added too
/// (same ConnectionStrings__Postgres convention Program.cs/docker-compose use) so
/// `dotnet ef migrations bundle` can run in CI/Docker builds, which have no user-secrets
/// store — user-secrets is added second so local dev still wins if both are present.
/// </remarks>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets("5a305068-58fc-4c7f-8363-441e0b8b71e1")
            .Build();

        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. Run " +
                "'dotnet user-secrets set ConnectionStrings:Postgres \"...\" --project src/AfterApply.Api' first.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
