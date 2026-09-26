using System.Net.Http.Json;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.IntegrationTests.Infrastructure;

/// <summary>
/// The guard for FastPasswordHashingStartup: a plain host gets the cheap iteration count, and a
/// password hashed with it still verifies. Without this, a renamed startup or a disabled hosting
/// startup would quietly put the suite back on the production cost — about a third of a run's
/// wall clock before 2026-09-26.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class FastPasswordHashingTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public void Test_Hosts_Hash_With_The_Cheap_Iteration_Count()
    {
        host.Services.GetRequiredService<IOptions<PasswordHasherOptions>>().Value.IterationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Sign_In_Still_Checks_The_Password()
    {
        // RegisterAsync signs in with the right password, so that half is already proven.
        var (client, _) = await host.RegisterAsync("fast.hash@example.com");

        var wrong = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("fast.hash@example.com", "not-the-password"), ApiHost.JsonOptions);

        wrong.IsSuccessStatusCode.ShouldBeFalse();
    }
}
