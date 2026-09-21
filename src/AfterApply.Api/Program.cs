using System.Globalization;
using System.Text.Json.Serialization;
using AfterApply.Api;
using AfterApply.Api.Endpoints;
using AfterApply.Api.ExceptionHandling;
using AfterApply.Api.Imports;
using AfterApply.Api.Middleware;
using AfterApply.Application.Auditing;
using AfterApply.Application.Imports;
using AfterApply.Application.JobSources;
using AfterApply.Application.Payments;
using AfterApply.Application.Metrics;
using AfterApply.Application.Notifications;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.Caching;
using AfterApply.Infrastructure.Auditing;
using AfterApply.Infrastructure.JobSources;
using AfterApply.Infrastructure.Payments;
using AfterApply.Infrastructure.Metrics;
using AfterApply.Infrastructure.Notifications;
using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Config-driven, same "stays inert until real values are set" pattern as
// OpenAI/GoogleOAuth: an empty Dsn makes the Sentry SDK disable itself (no
// events sent), it does not throw. See DECISIONS.md "Sprint 13".
builder.WebHost.UseSentry(options =>
{
    options.Dsn = builder.Configuration["Sentry:Dsn"];
    options.Environment = builder.Environment.EnvironmentName;
});

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Cloud Run terminates TLS at its frontend and forwards to the container over plain HTTP, so
// without this the app sees the *proxy's* address as Connection.RemoteIpAddress and "http" as the
// scheme. The consequence that mattered: the IP-partitioned auth rate limiter (RateLimiting.cs)
// collapsed into a single global bucket shared by every caller on earth — five login attempts per
// minute for the whole world, which is a self-inflicted outage as much as a weak control. It also
// meant RefreshToken.CreatedByIp recorded the proxy, making that audit trail worthless. Secondarily
// the scheme is now correct too, so UseHsts/UseHttpsRedirection below see https rather than http.
//
// ForwardLimit stays at its default of 1, which is what makes this spoof-resistant: the middleware
// reads X-Forwarded-For from the RIGHT, and Cloud Run's frontend *appends* the real client IP to
// whatever the caller sent, so the rightmost entry is always the one Google observed, never a
// client-supplied one. KnownNetworks/KnownProxies must be cleared because Cloud Run's internal
// frontend addresses aren't stable or knowable ahead of time; safe here only because the container
// is not directly addressable — nothing but Cloud Run's frontend can reach it.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
// ConfigureHttpJsonOptions above only reaches Minimal API responses; SignalR serializes hub
// payloads with its own options. Without the same converter here, ImportSummaryResponse.Status
// went out over the hub as the enum's ordinal (2) while GET /api/imports/{id} returned
// "Completed" — and useImportProgress compares against the string, so the push that arrived after
// the last poll flipped a finished import to the "failed" branch of the uploader.
builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
    // Redis backplane (DECISIONS.md 2026-09-18 "PR C"): the import job runs on whichever
    // instance's Hangfire worker fetched it, and the uploader's socket is on whichever instance
    // served the page — with several instances those are usually different, and a group send on
    // one never reached a connection on another. The options are bound below rather than here so
    // the backplane shares the cache's multiplexer instead of opening a second connection.
    .AddStackExchangeRedis(_ => { });
builder.Services.AddOptions<Microsoft.AspNetCore.SignalR.StackExchangeRedis.RedisOptions>()
    .Configure<IConnectionMultiplexer, IOptions<CachingOptions>>((options, multiplexer, caching) =>
    {
        options.ConnectionFactory = _ => Task.FromResult(multiplexer);
        // Prefixed like the cache's backplane channel: pub/sub is not scoped by Redis database, so
        // this is what keeps the integration suite's classes (and any two deployments sharing one
        // Redis) from receiving each other's hub messages.
        options.Configuration.ChannelPrefix = RedisChannel.Literal(caching.Value.ChannelPrefix + ":signalr");
    });
builder.Services.AddScoped<IImportProgressNotifier, SignalRImportProgressNotifier>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// First in the pipeline on purpose: everything downstream that reads the client IP or the request
// scheme (rate limiting, HTTPS redirection, HSTS, logging) has to see the rewritten values, not the
// proxy's. See the ForwardedHeadersOptions block above for why this is load-bearing here.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    // Dev runs over plain http://localhost, where an HSTS header would pin the browser to https
    // for the whole localhost origin and break every other local project too.
    app.UseHsts();
}

app.UseHttpsRedirection();

// This API only ever returns JSON — it has no HTML, no scripts, and nothing that should ever be
// framed — so the strictest possible policy is also the correct one. Mainly defense against a
// browser being talked into treating an error body or a reflected value as markup; the real
// CSP that matters for users lives in web/next.config.ts.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors(DependencyInjection.CorsPolicyName);

var supportedCultures = new[] { new CultureInfo("tr"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("tr"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseExceptionHandler();

app.UseAuthentication();
app.UseAuthorization();

// RateLimiting:Enabled exists for the integration suite and defaults to on; nothing in a deployed
// configuration sets it. The suite turns it off for every host it builds (see
// TestContainerCleanup.ConfigureRateLimitingForTests) and back on only in the one test that asserts
// a 429, because the middleware's own endpoint limiter — the one RateLimitingMiddleware builds
// around the named policies — cannot be disposed from outside and keeps a 100ms heartbeat alive
// for as long as the host object lives, which for a test host is the rest of the run.
//
// Leaving the middleware out is enough: endpoints keep their RequireRateLimiting metadata, and
// routing does not check for an unhandled rate-limiting policy the way it does for authorization.
if (app.Services.GetRequiredService<IOptions<RateLimitingOptions>>().Value.Enabled)
{
    app.UseRateLimiter();
}

// After the rate limiter and before any endpoint: every write under /api leaves a row with the
// caller's IP (RequestAudits). See the middleware for what is and is not recorded.
app.UseMiddleware<RequestAuditMiddleware>();

app.MapHealthChecks("/health");
app.MapSiteRootEndpoints();
app.MapClientConfigEndpoints();
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapApplicationEndpoints();
app.MapTrackedJobEndpoints();
app.MapAnalyticsEndpoints();
app.MapImportEndpoints();
app.MapCvDocumentEndpoints();
app.MapReminderEndpoints();
app.MapEmailForwardingEndpoints();
app.MapPersonalAccessTokenEndpoints();
app.MapExtensionPairingEndpoints();
app.MapCompanyIntelligenceEndpoints();
app.MapResponseRateEndpoints();
app.MapCompanyEndpoints();
app.MapCompanyReviewEndpoints();
app.MapAdminCompanyReviewEndpoints();
app.MapAdminContributionEndpoints();
app.MapCompanySalaryEndpoints();
app.MapCandidateExperienceEndpoints();
app.MapOccupationEndpoints();
app.MapFeedbackEndpoints();
app.MapSiteTrafficEndpoints();
app.MapBenchmarkEndpoints();
app.MapSiteStatsEndpoints();
app.MapCvScanEndpoints();
app.MapAdminEndpoints();
app.MapBlogEndpoints();
app.MapAdminBlogEndpoints();
app.MapBlogCommentEndpoints();
app.MapJobSourceEndpoints();
app.MapPaymentEndpoints();
app.MapHub<ImportProgressHub>("/hubs/import-progress");

if (!DependencyInjection.IsOpenApiDocumentGeneration)
{
    using var scope = app.Services.CreateScope();
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var notificationOptions = scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>().Value;
    var metricsOptions = scope.ServiceProvider.GetRequiredService<IOptions<ProductMetricsOptions>>().Value;
    var jobSourceOptions = scope.ServiceProvider.GetRequiredService<IOptions<JobSourceOptions>>().Value;

    recurringJobManager.AddOrUpdate<IReminderService>(
        "reminder-scan",
        service => service.ScanAndGenerateRemindersAsync(CancellationToken.None),
        notificationOptions.ScanCronExpression);

    recurringJobManager.AddOrUpdate<IProductMetricsService>(
        "product-metrics-snapshot",
        service => service.ComputeSnapshotAsync(CancellationToken.None),
        metricsOptions.SnapshotCronExpression);

    var requestAuditOptions = scope.ServiceProvider.GetRequiredService<IOptions<RequestAuditOptions>>().Value;
    recurringJobManager.AddOrUpdate<IRequestAuditRetentionService>(
        "request-audit-purge",
        service => service.PurgeAnonymousAsync(CancellationToken.None),
        requestAuditOptions.PurgeCronExpression);
    // Registered whether or not JobSources:Enabled is on — the sweep checks the flag itself and
    // returns at once while it is off, so turning the feature on needs no redeploy for the schedule.
    recurringJobManager.AddOrUpdate<IJobSourceSweepService>(
        "job-source-sweep",
        service => service.SweepAsync(CancellationToken.None),
        jobSourceOptions.Cron);

    // Payments: close pending PayTR orders whose window passed, and remind users whose prepaid
    // Pro period is about to end. Both are no-ops on an empty table, so they run regardless of
    // PayTr:Enabled.
    var payTrOptions = scope.ServiceProvider.GetRequiredService<IOptions<PayTrOptions>>().Value;
    recurringJobManager.AddOrUpdate<IPaymentMaintenanceService>(
        "payment-order-expiry",
        service => service.ExpirePendingOrdersAsync(CancellationToken.None),
        payTrOptions.OrderExpiryCron);
    recurringJobManager.AddOrUpdate<IPaymentMaintenanceService>(
        "pro-expiry-reminder",
        service => service.SendExpiryRemindersAsync(CancellationToken.None),
        payTrOptions.ExpiryReminderCron);
}

app.Run();

public partial class Program;
