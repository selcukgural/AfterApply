using Microsoft.AspNetCore.Mvc.Testing;

namespace AfterApply.IntegrationTests;

internal static class TestHostDisposal
{
    /// <summary>
    /// Disposes a test host and does not fail the test if a Hangfire server it started refuses to
    /// stop in time.
    /// </summary>
    /// <remarks>
    /// Only for the handful of classes that ask for a background server
    /// (<c>Hangfire:ServerEnabled</c>); every other host has none and disposes cleanly, which is
    /// why this is a helper the exceptions opt into rather than a blanket rule.
    /// <para>
    /// What it swallows and why: Hangfire.PostgreSql's ExpirationManager does not observe the
    /// shutdown token, so <c>BackgroundProcessingServer.WaitForShutdownAsync</c> can run out its
    /// budget and throw <see cref="TaskCanceledException" /> — out of
    /// <c>WebApplicationFactory.DisposeAsync</c>, which means out of a fixture's
    /// <c>DisposeAsync</c>, which xunit reports as the test failing. The test had already passed at
    /// that point; what failed is a third-party shutdown in a process that is about to move on. It
    /// is not a defect this suite can assert away either: neither WorkerCount nor the timeout
    /// changes it (a shorter timeout only throws sooner — see
    /// TestContainerCleanup.ConfigureHangfireForTests).
    /// </para>
    /// <para>
    /// Deliberately narrow: only cancellation from disposal, only for these hosts. Any other
    /// exception still fails the test, so a genuinely broken teardown is still visible.
    /// </para>
    /// </remarks>
    public static async Task DisposeQuietlyAsync<TEntryPoint>(WebApplicationFactory<TEntryPoint>? factory)
        where TEntryPoint : class
    {
        if (factory is null)
        {
            return;
        }

        try
        {
            await factory.DisposeAsync();
        }
        catch (OperationCanceledException)
        {
            // Hangfire's shutdown gave up. Nothing this test can do, and nothing it asserts.
        }
    }
}
