using System.Globalization;
using System.Text.Json.Serialization;
using AfterApply.Api;
using AfterApply.Api.Endpoints;
using AfterApply.Api.ExceptionHandling;
using AfterApply.Api.Imports;
using AfterApply.Application.Imports;
using AfterApply.Application.Metrics;
using AfterApply.Application.Notifications;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.Notifications;
using Hangfire;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Options;
using Serilog;

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
builder.Services.AddApiRateLimiting();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();
builder.Services.AddSignalR();
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
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapApplicationEndpoints();
app.MapTrackedJobEndpoints();
app.MapAnalyticsEndpoints();
app.MapImportEndpoints();
app.MapReminderEndpoints();
app.MapEmailForwardingEndpoints();
app.MapPersonalAccessTokenEndpoints();
app.MapCompanyIntelligenceEndpoints();
app.MapCompanyEndpoints();
app.MapHub<ImportProgressHub>("/hubs/import-progress");

if (!DependencyInjection.IsOpenApiDocumentGeneration)
{
    using var scope = app.Services.CreateScope();
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var notificationOptions = scope.ServiceProvider.GetRequiredService<IOptions<NotificationOptions>>().Value;

    recurringJobManager.AddOrUpdate<IReminderService>(
        "reminder-scan",
        service => service.ScanAndGenerateRemindersAsync(CancellationToken.None),
        notificationOptions.ScanCronExpression);

    recurringJobManager.AddOrUpdate<IProductMetricsService>(
        "product-metrics-snapshot",
        service => service.ComputeSnapshotAsync(CancellationToken.None),
        Cron.Daily());
}

app.Run();

public partial class Program;
