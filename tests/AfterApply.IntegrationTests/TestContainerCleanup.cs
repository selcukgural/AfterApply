using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AfterApply.IntegrationTests;

internal static class TestContainerCleanup
{
    /// <summary>
    /// Shrinks Hangfire's background server for every WebApplicationFactory this assembly builds.
    /// Set as environment variables rather than per-factory UseSetting calls because ASP.NET's
    /// default configuration reads them automatically — this reaches all ~200 hosts a run creates
    /// without every one of the 17 test classes having to opt in and remember to keep doing so.
    ///
    /// Why it matters: Hangfire's shutdown was the single most destabilising thing in this suite.
    /// WaitForShutdownAsync timing out during a fixture's DisposeAsync showed up as a failed test,
    /// a run that hung until something killed it, or "Test host process crashed" partway through —
    /// three symptoms, one cause. Raising the timeout (see DependencyInjection.AddBackgroundJobs)
    /// had already been tried and only lengthened the wait. One worker is plenty for tests, which
    /// only ever need to observe that a job ran, and it leaves far less to wind down.
    /// </summary>
    [ModuleInitializer]
    public static void ConfigureHangfireForTests()
    {
        Environment.SetEnvironmentVariable("Hangfire__WorkerCount", "1");

        // ShutdownTimeoutSeconds is deliberately left at its 30s default. Lowering it to 5 was
        // tried and is a mistake: the timeout does not reduce the work Hangfire has to do on the
        // way down, it only decides how long we are willing to wait, so a tight value brings back
        // the very TaskCanceledException-out-of-DisposeAsync failures it was meant to help with as
        // soon as the machine is loaded (reproduced immediately under `dotnet test --diag`, whose
        // overhead stretched one test to four minutes). WorkerCount is the setting that actually
        // reduces the work.
    }

    /// <summary>
    /// Turns the rate-limiting middleware off for every host this assembly builds. Same env-var
    /// delivery as ConfigureHangfireForTests, for the same reason. The one test that needs a 429,
    /// AccountManagementTests.Login_Rate_Limit_Rejects_Requests_Beyond_The_Threshold, switches it
    /// back on for its own factory.
    ///
    /// Why: this was the actual cause of the "Test host process crashed" / hung run / Hangfire
    /// shutdown-timeout trio, found from a heap dump of a hung run. WebApplicationFactory hosts are
    /// never garbage-collected once disposed (all 102 built so far were alive at test 47), and each
    /// carried two PartitionedRateLimiters whose 100ms heartbeat timers nothing ever stops — the
    /// middleware never disposes them. ~2,000 timer callbacks a second by mid-run, each of them
    /// disposing idle partitions, starved the thread pool and the JIT lock (200+ threads queued on
    /// one method's compile), which is what a "random" crash or hang partway through looked like
    /// from the outside. The named policies (auth, upload, ...) date from August and gave each host
    /// one such limiter; the OWASP hardening commit (755f7cf) added the global per-user/IP limiter,
    /// a second one whose partitions churn on every request — and that is the day the previously
    /// rare crash became constant.
    /// </summary>
    [ModuleInitializer]
    public static void ConfigureRateLimitingForTests()
    {
        Environment.SetEnvironmentVariable("RateLimiting__Enabled", "false");
    }

    /// <summary>
    /// Reports what killed the process when a run ends in "Test host process crashed".
    ///
    /// An unhandled exception on a background thread terminates a .NET process outright, and the
    /// test runner has nothing to print because no test threw — which is the shape of the
    /// intermittent local crash this suite has. Hangfire runs plenty of such threads and the suite
    /// builds roughly 200 of its servers per run, so a background thread outliving the storage it
    /// captured is a plausible source.
    ///
    /// Writes to a file rather than stderr: vstest does not surface the test host's own stderr in
    /// the `dotnet test` output, so a first version of this that used Console.Error produced a
    /// captured crash with nothing printed at all. AFTERAPPLY_CRASH_LOG names the file; unset means
    /// the whole thing stays off, so this costs nothing in CI or a normal run.
    ///
    /// Diagnostic only: it cannot prevent a crash (the runtime is already tearing the process down
    /// by the time it fires), it only makes one legible.
    /// </summary>
    [ModuleInitializer]
    public static void ReportBackgroundFailures()
    {
        var path = Environment.GetEnvironmentVariable("AFTERAPPLY_CRASH_LOG");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        // Synchronous by necessity: this runs from an UnhandledException/ProcessExit handler, which
        // cannot await — and by then the runtime is already tearing the process down, so a pending
        // async write would never flush.
        void Write(string kind, object? payload)
        {
            try
            {
                File.AppendAllText(path,
                    $"=== {kind} pid={Environment.ProcessId} {DateTime.UtcNow:HH:mm:ss.fff} ==={Environment.NewLine}{payload}{Environment.NewLine}");
            }
            catch
            {
                // Diagnostics must never be the thing that breaks a run.
            }
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write($"UNHANDLED terminating={e.IsTerminating}", e.ExceptionObject);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("UNOBSERVED", e.Exception);
            e.SetObserved();
        };

        // Fires on a clean shutdown. Its absence in the log after a crash is itself evidence: it
        // means the process died in a way that skips managed shutdown entirely (a native fault,
        // a stack overflow, or an outside kill), rather than through anything raising an exception.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Write("PROCESS-EXIT", "clean");
    }

    // Ryuk (Testcontainers' automatic resource-reaper sidecar) cannot start under this machine's
    // rootless podman setup — it fails trying to bind-mount the podman API socket file ("operation
    // not supported"), so TESTCONTAINERS_RYUK_DISABLED=true is set for every local run (see
    // README.md / DECISIONS.md). Without Ryuk, nothing else ever removes a test class's
    // Postgres/Redis containers if a run crashes or gets killed mid-test (Ctrl+C, an agent timing
    // out, a forced kill) — they pile up silently across sessions. Observed 2026-08-29: five
    // interrupted runs left 79 orphaned containers running (some for 7+ hours), which starved the
    // podman VM badly enough that a clean run went from under a minute to 35+ minutes. Manually
    // remembering to check `podman ps -a` before every run (the previous guidance) clearly doesn't
    // hold up across sessions, so this prunes any container carrying Testcontainers' own
    // "org.testcontainers=true" label before this run creates its own — every run now starts from
    // a guaranteed-clean slate regardless of how the previous one ended. Runs once, synchronously,
    // the instant this test assembly loads (before any fixture/container is created); a few ms
    // when there's nothing to clean up.
    //
    // Deliberately scoped to only ever touch containers carrying that exact label — never anything
    // else on the shared podman VM (e.g. the unrelated "fluentflow" project's containers).
    // Assumes a single-developer local machine: two `dotnet test` invocations racing at the exact
    // same instant could theoretically prune each other's just-started containers, which is an
    // accepted tradeoff here, not a concern on a shared CI runner (which uses real Docker, where
    // Ryuk works and this is a no-op).
    [ModuleInitializer]
    public static void PruneOrphanedContainers()
    {
        if (Environment.GetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED") != "true")
        {
            return; // Ryuk is active elsewhere (e.g. CI's real Docker) — it already handles this.
        }

        try
        {
            using var list = Process.Start(new ProcessStartInfo("podman", "ps -aq --filter label=org.testcontainers=true")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            if (list is null)
            {
                return;
            }

            // Synchronous by necessity: a [ModuleInitializer] cannot be async, and nothing may run
            // before it completes.
            var output = list.StandardOutput.ReadToEnd();
            list.WaitForExit(5_000);

            var ids = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (ids.Length == 0)
            {
                return;
            }

            using var remove = Process.Start(new ProcessStartInfo("podman", $"rm -f {string.Join(' ', ids)}")
            {
                UseShellExecute = false,
            });
            remove?.WaitForExit(15_000);

            // `podman rm` drops the container but not its anonymous volume (that's normally
            // Ryuk's job too) — 5 interrupted runs left 372 orphaned volumes (~11GB) behind
            // alongside the 79 orphaned containers, which was very plausibly contributing to the
            // test host crashing partway through a run (not just the containers competing for
            // CPU). `volume prune` only ever removes volumes not referenced by any container
            // (running or stopped), so this can never touch a volume a live container — including
            // another project's, like "fluentflow" on this same shared VM — still depends on.
            using var pruneVolumes = Process.Start(new ProcessStartInfo("podman", "volume prune -f")
            {
                UseShellExecute = false,
            });
            pruneVolumes?.WaitForExit(15_000);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // podman CLI not on PATH, or some other environment quirk — best-effort only, never
            // block the test run over a cleanup step.
        }
    }
}
