using System.Globalization;
using System.Text.Json.Serialization;
using AfterApply.Api;
using AfterApply.Api.Endpoints;
using AfterApply.Api.ExceptionHandling;
using AfterApply.Application.EmailIntegrations;
using AfterApply.Application.Metrics;
using AfterApply.Application.Notifications;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.EmailIntegrations;
using AfterApply.Infrastructure.Notifications;
using Hangfire;
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

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiRateLimiting();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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
app.MapAnalyticsEndpoints();
app.MapImportEndpoints();
app.MapReminderEndpoints();
app.MapEmailIntegrationEndpoints();
app.MapMatchingEndpoints();
app.MapPersonalAccessTokenEndpoints();
app.MapCompanyIntelligenceEndpoints();
app.MapCompanyEndpoints();

using (var scope = app.Services.CreateScope())
{
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

    var emailOptions = scope.ServiceProvider.GetRequiredService<IOptions<EmailIntegrationOptions>>().Value;
    if (emailOptions.Enabled)
    {
        recurringJobManager.AddOrUpdate<IEmailIntegrationService>(
            "gmail-sync",
            service => service.SyncAllConnectionsAsync(CancellationToken.None),
            emailOptions.SyncCronExpression);
    }
}

app.Run();

public partial class Program;
