using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AfterApply.Infrastructure.Persistence;

/// <summary>
/// The one place the application's Postgres connection string is read and shaped. EF Core and
/// Hangfire both go through here so they end up on the same string and therefore the same Npgsql
/// pool — Npgsql pools per distinct connection string, so a per-instance connection ceiling only
/// holds if every consumer shares one.
/// </summary>
public static class PostgresConnectionString
{
    /// <summary>
    /// <c>Postgres:MaxPoolSize</c>. When set, applied as Npgsql's <c>Maximum Pool Size</c> — the hard
    /// cap on connections one process holds open to the database. It exists because production's
    /// Cloud SQL tier (db-f1-micro) has <c>max_connections = 25</c>, of which the application gets
    /// ~20, and Cloud Run runs several instances of this process at once: the sizing rule is
    /// <c>max-instances × MaxPoolSize ≤ usable slots</c>, and both halves of it live in
    /// <c>.github/workflows/deploy.yml</c> so they can be read together. Unset (local dev, tests)
    /// leaves Npgsql's default of 100, which is fine against a Postgres that is ours alone.
    /// See DECISIONS.md 2026-09-12 "53300 remaining connection slots".
    /// </summary>
    public const string MaxPoolSizeKey = "Postgres:MaxPoolSize";

    private const string PlaceholderForOpenApiGeneration =
        "Host=localhost;Database=openapi-gen;Username=openapi-gen;Password=openapi-gen";

    public static string Resolve(IConfiguration configuration, bool isOpenApiDocumentGeneration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? (isOpenApiDocumentGeneration ? PlaceholderForOpenApiGeneration : null)
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Postgres is not configured. For local dev run " +
                "'dotnet user-secrets set ConnectionStrings:Postgres \"...\" --project src/AfterApply.Api', " +
                "or set ConnectionStrings__Postgres when running via docker-compose.");

        return ApplyMaxPoolSize(connectionString, configuration.GetValue<int?>(MaxPoolSizeKey));
    }

    /// <summary>
    /// Returns <paramref name="connectionString"/> with <c>Maximum Pool Size</c> set to
    /// <paramref name="maxPoolSize"/>. A value already present in the string is overridden: the
    /// setting is the deploy's sizing decision, and the secret that carries the string is not
    /// where anyone would look for it. <c>null</c> returns the string untouched — not a rebuilt
    /// equivalent — so a string nobody asked to change keeps its exact spelling.
    /// </summary>
    public static string ApplyMaxPoolSize(string connectionString, int? maxPoolSize)
    {
        if (maxPoolSize is null)
        {
            return connectionString;
        }

        if (maxPoolSize < 1)
        {
            throw new InvalidOperationException($"{MaxPoolSizeKey} must be at least 1, got {maxPoolSize}.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString) { MaxPoolSize = maxPoolSize.Value };

        // Npgsql requires MinPoolSize ≤ MaxPoolSize and throws at first open otherwise; a cap
        // below a string's declared minimum would surface as a runtime failure far from here.
        if (builder.MinPoolSize > builder.MaxPoolSize)
        {
            builder.MinPoolSize = builder.MaxPoolSize;
        }

        return builder.ConnectionString;
    }
}
