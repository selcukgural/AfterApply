using AfterApply.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AfterApply.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var postgresConnectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Postgres \"...\" --project src/AfterApply.Api', " +
                "or set ConnectionStrings__Postgres when running via docker-compose.");

        var redisConnectionString = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Redis is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Redis \"...\" --project src/AfterApply.Api', " +
                "or set ConnectionStrings__Redis when running via docker-compose.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(postgresConnectionString));

        services.AddHealthChecks()
            .AddNpgSql(postgresConnectionString, name: "postgres")
            .AddRedis(redisConnectionString, name: "redis");

        return services;
    }
}
