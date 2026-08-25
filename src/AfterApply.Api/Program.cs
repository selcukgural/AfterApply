using System.Text.Json.Serialization;
using AfterApply.Api;
using AfterApply.Api.Endpoints;
using AfterApply.Application.Metrics;
using AfterApply.Application.Notifications;
using AfterApply.Infrastructure;
using AfterApply.Infrastructure.Notifications;
using Hangfire;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiRateLimiting();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(DependencyInjection.CorsPolicyName);

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
}

app.Run();

public partial class Program;
