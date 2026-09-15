using AfterApply.Infrastructure.Persistence;
using Hangfire.PostgreSql;
using Hangfire.PostgreSql.Factories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AfterApply.IntegrationTests;

/// <summary>
/// One Postgres container for the entire assembly, with the schema built exactly once.
///
/// The cost this removes is easy to undercount. Each test class declared its containers as
/// instance fields, and xunit constructs a fresh instance of a test class for every test method —
/// so those fields ran, and a Postgres container started, and ~29 migrations replayed,
/// once per *test*, not once per class. 104 tests meant on the order of 200 containers. That was
/// the whole of CI's critical path: `dotnet test` was 411s of a 441s backend job in a 7.4-minute
/// pipeline, and locally a class like TrackedJobFlowTests spent 13s on 5 tests, nearly all of it
/// standing infrastructure up and tearing it down again.
///
/// Here the schema is migrated once into a template database and each caller clones it with
/// CREATE DATABASE ... TEMPLATE, which Postgres does as a file copy. Isolation is unchanged —
/// every test still gets a private, empty-but-migrated database, exactly as it did when it had a
/// whole container to itself. Cache isolation no longer needs a hand-out at all: HybridCache runs
/// L1-only now that Redis is gone (DECISIONS.md 2026-09-06), so its store is each
/// WebApplicationFactory's own IMemoryCache and dies with the host — a per-test boundary the
/// shared Redis could only approximate with one numbered database per caller.
///
/// Since 2026-09-15 the clone is per test <em>class</em>, not per test: <see cref="ApiHost" /> takes
/// one in its InitializeAsync and empties it between tests with <see cref="ResetDatabaseAsync" />.
/// The template also carries Hangfire's schema, so a host booting on a clone finds it installed
/// and runs no DDL.
///
/// This deliberately does not touch parallelism. Every class lives in one xunit collection, so they
/// still run strictly one at a time, and xunit.runner.json keeps maxParallelThreads at 1. The
/// DOP&gt;1 experiment that was tried and reverted (DECISIONS.md, 2026-09-01) failed on Hangfire jobs
/// missing their polling deadlines under CPU contention; those deadlines no longer exist (jobs run
/// inline, see InlineBackgroundJobs), so that experiment can be rerun — separately, measured.
/// </summary>
public sealed class SharedInfrastructure : IAsyncLifetime
{
    // Cloned by every caller, never connected to once it is built. Postgres refuses to use a
    // database as a template while anything else holds a connection to it.
    private const string TemplateDatabase = "aa_template";

    // Each test used to get its own server and, with it, its own budget of Postgres' default 100
    // connections. Sharing one server means sharing one budget, and the suite blew through it
    // immediately ("53300: sorry, too many clients already"): a test can run up to three
    // WebApplicationFactories, each with an Npgsql pool and a Hangfire server holding connections
    // for its workers, watchdogs and distributed locks. Headroom is the cheap half of the fix;
    // MaxPoolSize and ClearAllPools below are the half that actually bounds it.
    //
    // Only the flags, no leading "postgres": the image's docker-entrypoint.sh prepends the binary
    // itself whenever the command starts with a dash. Passing it explicitly makes the entrypoint
    // run `postgres postgres -c ...`, which exits with `invalid argument: "postgres"` before the
    // server ever listens.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .WithCommand("-c", "max_connections=300")
        .Build();

    private int _nextDatabaseIndex = -1;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await ExecuteOnAdminDatabaseAsync($"""CREATE DATABASE "{TemplateDatabase}";""");

        // Pooling=false so no connection outlives this block: a pooled one left open would make
        // every later CREATE DATABASE ... TEMPLATE fail with "source database is being accessed by
        // other users", and the failure would land in whichever test class happened to clone first.
        var templateConnectionString = ConnectionStringFor(TemplateDatabase, pooling: false);
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(templateConnectionString).Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.MigrateAsync();
        }

        // Hangfire.PostgreSql installs its schema (~20 scripts, under an advisory lock) the first
        // time a storage is constructed against a database — which, with a clone per class, would
        // be once per class at host boot. Constructing one storage against the template here means
        // every clone already has the schema and the per-host check finds nothing to do. Its
        // connection is unpooled and closed by the time this returns, so the template stays
        // connection-free for CREATE DATABASE ... TEMPLATE.
        _ = new PostgreSqlStorage(
            new NpgsqlConnectionFactory(templateConnectionString, new PostgreSqlStorageOptions()),
            new PostgreSqlStorageOptions());

        NpgsqlConnection.ClearAllPools();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
    }

    /// <summary>Hands out an already-migrated Postgres database, as the connection string a
    /// WebApplicationFactory needs. The name is only used to make the database recognisable in
    /// psql; uniqueness comes from the counter.</summary>
    public async Task<string> CreateIsolatedDatabaseAsync(string name) =>
        (await CreateDatabaseAsync(name)).ConnectionString;

    /// <summary>The same, also returning the database name so the caller can drop it.</summary>
    public async Task<(string ConnectionString, string DatabaseName)> CreateDatabaseAsync(string name)
    {
        // Deliberately NOT calling NpgsqlConnection.ClearAllPools() here. It looks like the obvious
        // way to reclaim the pool each finished test leaves behind — every test uses a different
        // database, so every test gets its own pool, and nothing releases it — but it reaches
        // across the whole process, and the previous test's Hangfire server is not necessarily done
        // shutting down when the next test starts (BackgroundProcessingServer.WaitForShutdownAsync
        // has a 30s budget). Pulling connectors out from under those background threads crashed the
        // test host outright: the run aborted at test 45 of 104 with "Test host process crashed",
        // immediately after a Hangfire shutdown timeout. MaxPoolSize and ConnectionIdleLifetime on
        // the connection string bound the same growth without touching anyone else's connections.
        var databaseIndex = Interlocked.Increment(ref _nextDatabaseIndex);
        var databaseName = $"aa_{databaseIndex}_{Sanitize(name)}";

        await ExecuteOnAdminDatabaseAsync(
            $"""CREATE DATABASE "{databaseName}" TEMPLATE "{TemplateDatabase}";""");

        return (ConnectionStringFor(databaseName, pooling: true), databaseName);
    }

    /// <summary>Drops a clone a class is done with. FORCE closes any straggling session first.</summary>
    public Task DropDatabaseAsync(string databaseName) =>
        ExecuteOnAdminDatabaseAsync($"""DROP DATABASE IF EXISTS "{databaseName}" WITH (FORCE);""");

    // Built once per process: every clone has the same schema, so the table list is the same.
    private static string? _resetSql;

    /// <summary>
    /// Empties a clone between two tests of the same class — every table in <c>public</c> in one
    /// TRUNCATE ... CASCADE, except the three that must survive: the migration history, the
    /// data-protection key ring the running host already loaded (a host whose keys vanish cannot
    /// read the tokens it issued), and EmailTemplates, the schema's only seeded table
    /// (EmailTemplateConfiguration.HasData) which the app only ever reads. Hangfire's tables live
    /// in their own schema and are not touched; they hold the recurring-job definitions the host
    /// wrote at boot. Sequences keep counting, which no test depends on.
    /// </summary>
    public async Task ResetDatabaseAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        if (_resetSql is null)
        {
            await using var list = new NpgsqlCommand(
                """
                SELECT string_agg(format('%I.%I', schemaname, tablename), ', ')
                FROM pg_tables
                WHERE schemaname = 'public'
                  AND tablename NOT IN ('__EFMigrationsHistory', 'DataProtectionKeys', 'EmailTemplates');
                """, connection);
            var tables = (string)(await list.ExecuteScalarAsync())!;
            _resetSql = $"TRUNCATE TABLE {tables} CASCADE;";
        }

        await using var truncate = new NpgsqlCommand(_resetSql, connection);
        await truncate.ExecuteNonQueryAsync();
    }

    private async Task ExecuteOnAdminDatabaseAsync(string sql)
    {
        // CREATE DATABASE can't run inside a transaction or against the database being copied, so
        // it goes through the container's own maintenance database on a bare connection — built
        // through the same helper as the rest so it also skips GSS negotiation (see below).
        await using var connection = new NpgsqlConnection(AdminConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private string AdminConnectionString() =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Pooling = false,
            GssEncryptionMode = GssEncryptionMode.Disable
        }.ConnectionString;

    private string ConnectionStringFor(string database, bool pooling) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString())
        {
            Database = database,
            Pooling = pooling,
            // Left at Npgsql's default of 100. Capping it looked reasonable — one HTTP client at a
            // time hardly needs a hundred connections — and it deadlocked the suite: a test can run
            // three WebApplicationFactories, each starting a Hangfire server that opens
            // min(ProcessorCount * 5, 20) workers, so ~60 threads compete for the pool of the one
            // connection string they share. At MaxPoolSize=10 they spent their time blocking,
            // timing out and retrying; EmailSignalTests sat on a single test for eleven minutes at
            // 155% CPU with Postgres completely idle. The pool size is not the right lever here;
            // ConnectionIdleLifetime below is, because the problem was only ever pools that never
            // drained, never pools that were too large while in use.
            MaxPoolSize = 100,
            // The last of this suite's hangs, and the least obvious. A run would stop dead with the
            // database idle and no CPU burning, and a stack of the stuck process put the blame
            // exactly here: Hangfire.PostgreSql's ExpirationManager opening a connection →
            // NpgsqlConnector.SetupEncryption → TryNegotiateGssEncryption → GSSEncrypt, waiting
            // forever. Npgsql negotiates GSS encryption by default and this machine has no KDC to
            // answer, so the call never returns and the wait has no timeout in front of it (connect
            // timeouts start after negotiation). A container-local Postgres over loopback has
            // nothing to gain from Kerberos, so it is turned off here; production is untouched.
            GssEncryptionMode = GssEncryptionMode.Disable,
            // These two are what reclaim a finished test's pool, and they are the reason
            // CreateIsolatedDatabaseAsync does not call ClearAllPools (see the comment there — doing
            // that crashed the test host). Every test connects to its own database and so builds
            // its own pool, and by default those pools would sit on idle connections for 300s while
            // 100+ more tests each opened another one; the shared server ran out
            // ("53300: sorry, too many clients already") around the EmailSignalTests block, which
            // runs three app hosts per test. Shrinking both the idle lifetime and the interval the
            // pruner runs at means a pool drains within a couple of seconds of its test finishing,
            // on its own, without reaching into connections another test is still using.
            // Npgsql rejects the connection string outright if the lifetime is below the interval.
            ConnectionIdleLifetime = 2,
            ConnectionPruningInterval = 1
        }.ConnectionString;

    /// <summary>Postgres identifiers are case-folded unless quoted and cap at 63 bytes; keeping the
    /// name lowercase and alphanumeric means it reads the same in psql as it does here.</summary>
    private static string Sanitize(string name) =>
        new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

/// <summary>
/// Every integration test class belongs to this collection, which is what makes the container
/// above start once for the whole assembly rather than once per test. It also means the classes run
/// strictly sequentially — the behaviour maxParallelThreads=1 already gave, now expressed
/// structurally as well.
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<SharedInfrastructure>
{
    public const string Name = "integration";
}
