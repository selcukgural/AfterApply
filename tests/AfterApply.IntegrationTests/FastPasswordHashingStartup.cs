using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(AfterApply.IntegrationTests.FastPasswordHashingStartup))]

namespace AfterApply.IntegrationTests;

/// <summary>
/// Drops Identity's PBKDF2 iteration count for every host this assembly builds.
/// </summary>
/// <remarks>
/// The production default (100,000 iterations) is a deliberate cost per hash, and a run pays it
/// hundreds of times: every <c>RegisterAsync</c> hashes once on sign-up and again on sign-in, and
/// the suite registers well over 700 accounts. None of the tests is about the hash's strength —
/// they need a password to be stored and checked, which the same V3 format does at any count, since
/// the count is written into the hash itself. Production configures nothing here, so it keeps
/// Identity's default. Loaded the same way as <see cref="NoOutboundHttpStartup" />, so no class has
/// to opt in.
/// </remarks>
public sealed class FastPasswordHashingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) =>
        builder.ConfigureServices(services =>
            services.Configure<PasswordHasherOptions>(options => options.IterationCount = 1));
}
