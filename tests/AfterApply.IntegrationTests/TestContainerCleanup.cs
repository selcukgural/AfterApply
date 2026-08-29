using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace AfterApply.IntegrationTests;

internal static class TestContainerCleanup
{
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
