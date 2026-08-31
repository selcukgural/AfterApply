namespace AfterApply.Infrastructure.EmailIntegrations;

public sealed class EmailIntegrationOptions
{
    public bool Enabled { get; init; } = false;

    public string SyncCronExpression { get; init; } = "0 * * * *"; // hourly

    public string FrontendBaseUrl { get; init; } = "http://localhost:3000";

    /// <summary>Safety valve against a burst of matched mail (e.g. a misbehaving sender, or a
    /// recovery run after a long sync gap) turning into an unbounded number of LLM calls in one
    /// sync run. Extra messages beyond this cap are left for the next sync run rather than dropped.</summary>
    public int MaxLlmClassificationsPerSyncRun { get; init; } = 200;
}
