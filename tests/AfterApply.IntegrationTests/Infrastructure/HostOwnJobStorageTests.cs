using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Shouldly;

namespace AfterApply.IntegrationTests.Infrastructure;

/// <summary>
/// The guard for HostOwnJobStorage: a host's recurring jobs land in its own database. With classes
/// booting in parallel, Hangfire's default registration (JobStorage.Current) could hand a host
/// another class's storage, and its start-up writes would go to that class's database instead.
/// </summary>
public class HostOwnJobStorageTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_Hosts_Recurring_Jobs_Are_In_Its_Own_Database()
    {
        host.Services.GetRequiredService<JobStorage>().ShouldBeOfType<PostgreSqlStorage>();
        host.Services.GetRequiredService<JobStorage>().ShouldNotBeSameAs(JobStorage.Current);

        await using var connection = new NpgsqlConnection(host.ConnectionString);
        await connection.OpenAsync();
        await using var count = new NpgsqlCommand(
            "SELECT count(*) FROM hangfire.set WHERE key = 'recurring-jobs';", connection);

        ((long)(await count.ExecuteScalarAsync())!).ShouldBeGreaterThan(0);
    }
}
