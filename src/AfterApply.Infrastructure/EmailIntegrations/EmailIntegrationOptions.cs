namespace AfterApply.Infrastructure.EmailIntegrations;

public sealed class EmailIntegrationOptions
{
    public bool Enabled { get; init; } = false;

    public string SyncCronExpression { get; init; } = "0 * * * *"; // hourly

    public string FrontendBaseUrl { get; init; } = "http://localhost:3000";
}
