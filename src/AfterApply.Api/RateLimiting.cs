using System.Globalization;
using System.Threading.RateLimiting;
using AfterApply.Api.RateLimits;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.Caching;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using RedisRateLimiting;
using StackExchange.Redis;

namespace AfterApply.Api;

public static class RateLimiting
{
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRateLimiter(_ => { });

        services.Configure<RateLimitingOptions>(configuration.GetSection(RateLimitingOptions.SectionName));

        services.AddOptions<RateLimiterOptions>().Configure<IHostApplicationLifetime, IOptions<RateLimitingOptions>,
            IOptions<CachingOptions>, IConnectionMultiplexer, ILoggerFactory>((options, lifetime, limits, caching, redis, loggerFactory) =>
        {
            // Sizes come from the "RateLimiting" section (RateLimitingOptions holds the defaults) so
            // a bucket can be retuned without a redeploy; the comments on each policy below explain
            // the sizing, not the numbers.
            var sizes = limits.Value;

            // Every window below is counted in Redis, shared by every instance, with the in-memory
            // window as the fallback while Redis is unreachable — see RedisFirstFixedWindowLimiter.
            // One partition = one limiter, built on first sight of the key and evicted once idle.
            var logger = loggerFactory.CreateLogger("RateLimiting");
            var keyPrefix = caching.Value.KeyPrefix;
            RateLimitPartition<string> Partition(string policy, string partitionKey, RateLimitingOptions.FixedWindowPolicy size) =>
                RateLimitPartition.Get(partitionKey, key => (RateLimiter)new RedisFirstFixedWindowLimiter(
                    new RedisFixedWindowRateLimiter<string>(RateLimitPartitionKeys.ForRedis(keyPrefix, policy, key), new RedisFixedWindowRateLimiterOptions
                    {
                        PermitLimit = size.PermitLimit,
                        Window = size.Window,
                        ConnectionMultiplexerFactory = () => redis
                    }),
                    new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = size.PermitLimit,
                        Window = size.Window,
                        QueueLimit = 0
                    }),
                    redisIsConnected: () => redis.IsConnected,
                    // The policy name only: the partition key is an IP or a user id.
                    onFallback: ex => logger.LogWarning(ex, "Rate limit window for {Policy} counted locally: Redis unavailable", policy)));

            // ASP.NET Core's default rejection status is 503 — 429 is the conventional
            // status for rate limiting and what clients are expected to handle.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = (context, _) =>
            {
                // Without this a client has nothing to back off against and the sensible thing for
                // it to do — retry immediately — is the worst thing for us.
                // Two spellings, one header: the in-memory limiter reports MetadataName.RetryAfter
                // (a TimeSpan), the Redis one RateLimitMetadataName.RetryAfter (whole seconds).
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }
                else if (context.Lease.TryGetMetadata(RateLimitMetadataName.RetryAfter, out var retryAfterSeconds))
                {
                    context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                }

                // The path only: a caller's address is recorded in RequestAudits and nowhere else
                // (CLAUDE.md, "Request audit"), log lines included.
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");
                logger.LogWarning("Rate limit exceeded on {Path}", context.HttpContext.Request.Path);
                return ValueTask.CompletedTask;
            };

            // Backstop over every endpoint, including the ones with no named policy of their own.
            // Before this existed, only auth/upload/extension-signal were bounded at all, so an
            // authenticated caller could hammer anything else — /api/users/me/export (loads the
            // account's entire history), /api/companies/search (a trigram scan per keystroke) —
            // as fast as the network allowed. Sized to be invisible to real use: the busiest screen
            // fires well under a dozen requests, and the polling badges are on multi-second timers.
            //
            // Partitioned by user where there is one and by IP otherwise, the same split the named
            // policies below use. This runs after UseAuthentication (see Program.cs's pipeline
            // order), so the sub claim is already available here.
            // See ClientPartition: an IPv6 caller counts as its /64, and a server-side render counts
            // as the visitor it names when it carries the render key.
            string IpPartitionKey(HttpContext httpContext) =>
                ClientPartition.ForAddress(ClientPartition.ClientAddress(httpContext, sizes.ServerRenderKey));

            // The authenticated user where there is one, the caller's address otherwise. That address
            // is the real client's only because UseForwardedHeaders runs first (Program.cs).
            string PartitionKey(HttpContext httpContext) =>
                httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? IpPartitionKey(httpContext);

            var globalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                Partition("global", PartitionKey(httpContext), sizes.Global));
            options.GlobalLimiter = globalLimiter;

            // The limiter is ours, so its lifetime is ours too. Nothing else ever disposes it:
            // RateLimitingMiddleware only reads options.GlobalLimiter, and a PartitionedRateLimiter
            // runs a 100ms heartbeat timer (idle-partition cleanup, token replenishment) from
            // construction until Dispose. In production that is one timer per process and nobody
            // would notice. In the integration suite, which builds a host per test and whose hosts
            // are never collected once disposed, the leaked heartbeats were the root cause of the
            // "random" test-host crashes and hangs — see DECISIONS.md, "Yerel test-host çökmesi:
            // kök neden bulundu".
            //
            // ApplicationStopped, not Stopping: a request still draining during graceful shutdown
            // must not hit a disposed limiter. The hook is on the lifetime rather than on
            // ServiceProvider disposal because the limiter is held by options, not registered as a
            // service.
            lifetime.ApplicationStopped.Register(globalLimiter.Dispose);

            // IP-based: auth endpoints are called before the caller is authenticated.
            options.AddPolicy(DependencyInjection.AuthRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.AuthRateLimitPolicy, IpPartitionKey(httpContext), sizes.Auth));

            // User-based: upload endpoints already require auth, so this is more precise
            // than IP-based (avoids penalizing legitimate users sharing a NAT'd IP).
            options.AddPolicy(DependencyInjection.UploadRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.UploadRateLimitPolicy, PartitionKey(httpContext), sizes.Upload));

            // User-based, same idiom as UploadRateLimitPolicy. A backstop against a buggy/looping
            // Gmail content script, not the primary control: the extension's own client-side dedup
            // of already-submitted thread ids is what normally keeps volume low, since a user only
            // opens so many emails per session.
            options.AddPolicy(DependencyInjection.ExtensionSignalRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.ExtensionSignalRateLimitPolicy, PartitionKey(httpContext), sizes.ExtensionSignal));

            // Tighter than the global limit because this endpoint is the only one that makes an
            // outbound request to a third party (JobLinkPreviewService fetches the pasted URL from
            // linkedin.com/kariyer.net). Left at the global limit it would let one account point
            // a few hundred requests a minute at someone else's servers over our IP — the kind of
            // amplification that gets an egress address blocked. A human pastes one link at a time.
            options.AddPolicy(DependencyInjection.LinkPreviewRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.LinkPreviewRateLimitPolicy, PartitionKey(httpContext), sizes.LinkPreview));

            // IP-based and anonymous, same idiom as the site-traffic policy above. PartitionKey
            // is not used here for the same reason: it would start partitioning a signed-in
            // visitor by their user id, and a benchmark answer is not supposed to be attributable.
            options.AddPolicy(DependencyInjection.BenchmarkRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.BenchmarkRateLimitPolicy, IpPartitionKey(httpContext), sizes.Benchmark));

            // IP-based and anonymous, like the benchmark: a silence report must not be attributable
            // to an account, so a signed-in visitor is partitioned by address like anyone else.
            options.AddPolicy(DependencyInjection.SilenceReportRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.SilenceReportRateLimitPolicy, IpPartitionKey(httpContext), sizes.SilenceReport));

            // IP-based and anonymous, like the benchmark policy above and for the same reason: the
            // scan is answerable without an account, and partitioning a signed-in visitor by user
            // id would attach a CV scan to a person, which is precisely what this surface does not
            // do. The bucket is the tightest of the anonymous ones because a scan costs a parser
            // run — see RateLimitingOptions.CvScan.
            options.AddPolicy(DependencyInjection.CvScanRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CvScanRateLimitPolicy, IpPartitionKey(httpContext), sizes.CvScan));

            // User-based. Free-text that a human writes and, when the GitHub mirror is on, that
            // leaves our infrastructure — so it is bounded far tighter than the global backstop,
            // which would happily let one account file three hundred issues a minute.
            // IP-based: this endpoint is anonymous, so there is no user to partition by — and
            // deliberately so. PartitionKey would fall back to the IP anyway for a signed-out
            // visitor, but calling it here would silently start partitioning signed-in visitors by
            // their user id, which is the one thing this feature must not do with traffic data.
            options.AddPolicy(DependencyInjection.SiteTrafficRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.SiteTrafficRateLimitPolicy, IpPartitionKey(httpContext), sizes.SiteTraffic));

            // Both halves of the extension pairing flow are anonymous and therefore IP-partitioned,
            // like the two policies above. They are split because they answer to different callers:
            // "start" is a person clicking Connect, "poll" is a timer in a page that stays open for
            // as long as the pairing does. One bucket sized for the timer would leave the start
            // endpoint effectively unbounded.
            options.AddPolicy(DependencyInjection.ExtensionPairingStartRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.ExtensionPairingStartRateLimitPolicy, IpPartitionKey(httpContext), sizes.ExtensionPairingStart));

            options.AddPolicy(DependencyInjection.ExtensionPairingPollRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.ExtensionPairingPollRateLimitPolicy, IpPartitionKey(httpContext), sizes.ExtensionPairingPoll));

            options.AddPolicy(DependencyInjection.FeedbackRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.FeedbackRateLimitPolicy, PartitionKey(httpContext), sizes.Feedback));

            options.AddPolicy(DependencyInjection.PaymentCheckoutRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.PaymentCheckoutRateLimitPolicy, PartitionKey(httpContext), sizes.PaymentCheckout));

            // User-based, all three: the review routes require auth, and the thing being bounded
            // is what one account can write onto public pages. Sizing in RateLimitingOptions.
            options.AddPolicy(DependencyInjection.CompanyReviewWriteRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CompanyReviewWriteRateLimitPolicy, PartitionKey(httpContext), sizes.CompanyReviewWrite));

            options.AddPolicy(DependencyInjection.CompanyReviewReportRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CompanyReviewReportRateLimitPolicy, PartitionKey(httpContext), sizes.CompanyReviewReport));

            options.AddPolicy(DependencyInjection.CompanyReviewHelpfulRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CompanyReviewHelpfulRateLimitPolicy, PartitionKey(httpContext), sizes.CompanyReviewHelpful));

            options.AddPolicy(DependencyInjection.CompanySalaryHelpfulRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CompanySalaryHelpfulRateLimitPolicy, PartitionKey(httpContext), sizes.CompanySalaryHelpful));

            options.AddPolicy(DependencyInjection.CandidateExperienceHelpfulRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CandidateExperienceHelpfulRateLimitPolicy, PartitionKey(httpContext), sizes.CandidateExperienceHelpful));

            options.AddPolicy(DependencyInjection.CompanySalaryWriteRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CompanySalaryWriteRateLimitPolicy, PartitionKey(httpContext), sizes.CompanySalaryWrite));

            options.AddPolicy(DependencyInjection.CandidateExperienceWriteRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CandidateExperienceWriteRateLimitPolicy, PartitionKey(httpContext), sizes.CandidateExperienceWrite));

            options.AddPolicy(DependencyInjection.BlogLikeRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.BlogLikeRateLimitPolicy, PartitionKey(httpContext), sizes.BlogLike));

            options.AddPolicy(DependencyInjection.BlogCommentWriteRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.BlogCommentWriteRateLimitPolicy, PartitionKey(httpContext), sizes.BlogCommentWrite));

            options.AddPolicy(DependencyInjection.BlogCommentReportRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.BlogCommentReportRateLimitPolicy, PartitionKey(httpContext), sizes.BlogCommentReport));

            options.AddPolicy(DependencyInjection.BlogCommentHelpfulRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.BlogCommentHelpfulRateLimitPolicy, PartitionKey(httpContext), sizes.BlogCommentHelpful));

            options.AddPolicy(DependencyInjection.BlogSeoSuggestRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.BlogSeoSuggestRateLimitPolicy, PartitionKey(httpContext), sizes.BlogSeoSuggest));

            // IP-based and anonymous, like the benchmark policy: the directory is readable without
            // an account, and a signed-in reader's browsing is not something to key to their id.
            options.AddPolicy(DependencyInjection.CompanyPublicSearchRateLimitPolicy, httpContext =>
                Partition(DependencyInjection.CompanyPublicSearchRateLimitPolicy, IpPartitionKey(httpContext), sizes.CompanyPublicSearch));
        });

        return services;
    }
}
