namespace AfterApply.Infrastructure.EmailIntegrations;

public sealed class EmailIntegrationOptions
{
    public string SyncCronExpression { get; init; } = "0 * * * *"; // hourly

    public string FrontendBaseUrl { get; init; } = "http://localhost:3000";
}
