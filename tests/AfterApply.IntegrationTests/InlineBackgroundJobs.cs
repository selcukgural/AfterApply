using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Hangfire;
using Hangfire.Common;
using Hangfire.Server;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AfterApply.IntegrationTests;

/// <summary>
/// What a test host does with a background job instead of handing it to a Hangfire server: it
/// records it, and <see cref="RunAsync" /> performs it when the test asks.
///
/// This replaces the real server for every host <see cref="ApiHost" /> builds, and it is the
/// second half of the fix for this suite's oldest problem. Turning the server off by default
/// (TestContainerCleanup.DisableHangfireServerForTests) removed the 30-second shutdown wait from
/// the hosts that never needed a job to run — but the twelve classes that assert what a job did
/// still opened a real server per host (three per test in EmailSignalTests), then polled the
/// database every 200 ms with a 30–60 s deadline, and slept two seconds flat to prove a job did
/// not run. Each of those servers paid the same shutdown wait on the way down, and each timed-out
/// shutdown left its host alive for the rest of the run (WebApplicationFactory skips
/// <c>_host.Dispose()</c> when <c>StopAsync</c> throws). That is where a 454-test run spent 33
/// minutes and how it accumulated enough live hosts to crash.
///
/// Running the job inline removes the server, the polling and the sleeps in one move, and makes
/// every job-asserting test deterministic: <c>POST …; await host.RunJobsAsync(); assert</c>. A
/// negative — "nothing was enqueued" — is <c>Pending.ShouldBeEmpty()</c> instead of a timed wait.
///
/// What it still exercises: the real <see cref="Job" /> built by Hangfire's expression parser, the
/// production serialiser round-trip (an argument that does not serialise fails at enqueue time
/// here as it would there), the real DI-resolved job target in its own scope, the real job body
/// against the real database. What it does not: Hangfire's queue, retries and filters (none are
/// configured in this codebase), and the server itself — which HangfireServerInTestsTests and
/// PostgresPoolCapTests keep proving with a real one, in isolation.
///
/// Jobs are recorded, not run, at enqueue time on purpose: ApplicationService enqueues company
/// enrichment before its own SaveChangesAsync, so running inline there would observe a row that
/// does not exist yet. Deferring to <see cref="RunAsync" /> keeps the production ordering — the
/// request completes, then the job runs.
/// </summary>
public sealed class InlineJobQueue
{
    private readonly ConcurrentQueue<PendingJob> _pending = new();
    private readonly List<FailedJob> _failed = [];
    private int _nextId;

    /// <summary>Jobs enqueued and not yet performed, oldest first.</summary>
    public IReadOnlyCollection<PendingJob> Pending => _pending;

    /// <summary>Jobs whose last run threw. Hangfire would have marked these Failed and retried;
    /// here a test asserts on them directly.</summary>
    public IReadOnlyList<FailedJob> Failed
    {
        get
        {
            lock (_failed)
            {
                return [.. _failed];
            }
        }
    }

    internal string Add(Job job, IState state, IServiceScopeFactory scopes)
    {
        var id = Interlocked.Increment(ref _nextId).ToString();
        _pending.Enqueue(new PendingJob(id, job, state, scopes));
        return id;
    }

    /// <summary>Forgets everything — between tests, so one test's leftover job cannot run in the
    /// next one's RunAsync.</summary>
    public void Clear()
    {
        _pending.Clear();
        lock (_failed)
        {
            _failed.Clear();
        }
    }

    /// <summary>
    /// Performs every pending job in enqueue order, including the ones a job enqueues while it
    /// runs (the weekly sweep enqueues one digest per user), until the queue is empty.
    /// </summary>
    /// <remarks>
    /// Each job runs in a fresh DI scope of the host that enqueued it, which is the boundary
    /// Hangfire's AspNetCoreJobActivator draws too. The host matters: a class can run variants of
    /// the app over one database with different configuration, and a job must see the
    /// configuration of the variant whose request enqueued it.
    /// </remarks>
    public async Task RunAsync(int maxJobs = 1_000)
    {
        var performed = 0;
        while (_pending.TryDequeue(out var item))
        {
            if (++performed > maxJobs)
            {
                throw new InvalidOperationException(
                    $"Inline job chain did not terminate after {maxJobs} jobs; a job keeps enqueuing another.");
            }

            await using var scope = item.Scopes.CreateAsyncScope();
            try
            {
                await InvokeAsync(item.Job, scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                lock (_failed)
                {
                    _failed.Add(new FailedJob(item, ex));
                }
            }
        }
    }

    private static async Task InvokeAsync(Job job, IServiceProvider services)
    {
        var target = job.Method.IsStatic ? null : services.GetRequiredService(job.Type);
        var parameters = job.Method.GetParameters();
        var args = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            // Substituted by parameter type, the way CoreBackgroundJobPerformer does: a job
            // declares a token it expects the server to supply, and the expression captured a
            // placeholder for it.
            var type = parameters[i].ParameterType;
            args[i] = type == typeof(CancellationToken) ? CancellationToken.None
                : type == typeof(IJobCancellationToken) ? JobCancellationToken.Null
                : type == typeof(PerformContext)
                    ? throw new NotSupportedException("PerformContext is not available to an inline job.")
                    : job.Args[i];
        }

        object? result;
        try
        {
            result = job.Method.Invoke(target, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }

        switch (result)
        {
            case Task task:
                await task;
                break;
            case ValueTask valueTask:
                await valueTask;
                break;
        }
    }
}

public sealed record PendingJob(string Id, Job Job, IState State, IServiceScopeFactory Scopes)
{
    /// <summary>The job's target type — <c>IEmailSender</c>, <c>IImportService</c> … — for a
    /// test that wants to assert what was queued without running it.</summary>
    public Type TargetType => Job.Type;

    public string MethodName => Job.Method.Name;
}

public sealed record FailedJob(PendingJob Job, Exception Exception);

/// <summary>The <see cref="IBackgroundJobClient" /> every ApiHost registers in place of Hangfire's.
/// Production code only ever calls <c>Enqueue&lt;T&gt;</c> on it (eleven call sites, all
/// interface targets registered in DI); anything else is refused loudly rather than ignored.</summary>
internal sealed class InlineBackgroundJobClient(InlineJobQueue queue, IServiceScopeFactory scopes) : IBackgroundJobClientV2
{
    public JobStorage Storage => JobStorage.Current;

    public string Create(Job job, IState state) => Create(job, state, null);

    public string Create(Job job, IState state, IDictionary<string, object>? parameters)
    {
        // The real client serialises the invocation before it touches storage, so an argument
        // that cannot round-trip through Hangfire's serialiser is an enqueue-time failure in
        // production. Keeping it one here means a test that passes such an argument fails at the
        // same place, not somewhere in a fake.
        var roundTripped = InvocationData.SerializeJob(job).DeserializeJob();

        return state switch
        {
            EnqueuedState or ScheduledState => queue.Add(roundTripped, state, scopes),
            _ => throw new NotSupportedException(
                $"The inline job client does not support the '{state.Name}' state. Add it when production starts using it."),
        };
    }

    public bool ChangeState(string jobId, IState state, string expectedState) =>
        throw new NotSupportedException(
            "The inline job client does not support ChangeState (Delete/Requeue/Reschedule); no production code calls it.");
}

internal static class InlineBackgroundJobs
{
    /// <summary>Swaps Hangfire's client for the inline one. Runs from ConfigureTestServices, which
    /// the test server applies after Program's own registrations.</summary>
    public static void Register(IServiceCollection services, InlineJobQueue queue)
    {
        services.AddSingleton(queue);
        services.RemoveAll<IBackgroundJobClient>();
        services.RemoveAll<IBackgroundJobClientV2>();
        services.AddSingleton<InlineBackgroundJobClient>();
        services.AddSingleton<IBackgroundJobClient>(sp => sp.GetRequiredService<InlineBackgroundJobClient>());
        services.AddSingleton<IBackgroundJobClientV2>(sp => sp.GetRequiredService<InlineBackgroundJobClient>());
    }
}
