using AfterApply.Infrastructure.Persistence;
using Hangfire;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AfterApply.IntegrationTests;

/// <summary>
/// Gives each test host a Hangfire storage over its own database, instead of whatever
/// <see cref="JobStorage.Current" /> holds at the moment it is resolved.
/// </summary>
/// <remarks>
/// Hangfire.AspNetCore registers <see cref="JobStorage" /> as "configure the global configuration,
/// then return <c>JobStorage.Current</c>". In production there is one host per process, so that is
/// the host's own storage. With test classes booting in parallel it is a race: host A sets
/// Current, host B sets it again, and A's <c>IRecurringJobManager</c> — which Program uses at start-up
/// to write the recurring jobs — ends up writing into B's clone, possibly while B's class is
/// dropping that database. Registering the storage per host takes the global out of the path. The
/// global configuration is still resolved first so the serializer settings Program asks for are
/// applied exactly as before.
/// </remarks>
internal static class HostOwnJobStorage
{
    public static void Register(IServiceCollection services)
    {
        services.RemoveAll<JobStorage>();
        services.AddSingleton<JobStorage>(provider =>
        {
            _ = provider.GetRequiredService<IGlobalConfiguration>();

            // The same resolved string Program's DbContext uses, so the storage shares that pool.
            var connectionString = PostgresConnectionString.Resolve(
                provider.GetRequiredService<IConfiguration>(), isOpenApiDocumentGeneration: false);
            var options = new PostgreSqlStorageOptions { UseSlidingInvisibilityTimeout = true };
            return new PostgreSqlStorage(new NpgsqlConnectionFactory(connectionString, options), options);
        });
    }
}
