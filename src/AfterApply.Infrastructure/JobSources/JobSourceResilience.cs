using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Timeout;

namespace AfterApply.Infrastructure.JobSources;

/// <summary>
/// The Polly pipeline in front of <see cref="LinkedInJobSourceClient"/>, as one function so a unit
/// test can stand up the same pipeline over a fake handler. Order (outermost first): total
/// timeout, retry, circuit breaker, per-attempt timeout — the standard handler's order, with one
/// deliberate difference: <b>a 429 or 403 is not retried and does not trip the breaker.</b> Those
/// are the source saying stop; the client reports them and the sweep stops for a day. What is
/// retried is what a network can cause — a dropped connection, a per-attempt timeout, a 5xx —
/// twice, with exponential backoff and jitter. <c>Retry-After</c> is ignored on purpose: on this
/// site the right reaction to it is not "wait and retry", it is "stop".
/// </summary>
public static class JobSourceResilience
{
    public const int MaxRetryAttempts = 2;

    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> pipeline, JobSourceOptions options)
    {
        pipeline.AddTimeout(TimeSpan.FromSeconds(options.TotalTimeoutSeconds));
        pipeline.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = MaxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(options.RetryBaseDelayMs),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldRetryAfterHeader = false,
            ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome, args.Context.CancellationToken))
        });
        pipeline.AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 4,
            BreakDuration = TimeSpan.FromSeconds(60),
            ShouldHandle = args => ValueTask.FromResult(IsTransient(args.Outcome, args.Context.CancellationToken))
        });
        pipeline.AddTimeout(TimeSpan.FromSeconds(options.AttemptTimeoutSeconds));
    }

    private static bool IsTransient(Outcome<HttpResponseMessage> outcome, CancellationToken cancellationToken) => outcome switch
    {
        { Exception: HttpRequestException or TimeoutRejectedException } => true,
        { Exception: TaskCanceledException } => !cancellationToken.IsCancellationRequested,
        { Result: { } response } => (int)response.StatusCode is >= 500 and <= 599, // not 999: LinkedIn's "request denied"
        _ => false
    };
}
