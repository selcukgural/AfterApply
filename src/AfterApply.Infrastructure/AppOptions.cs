namespace AfterApply.Infrastructure;

/// <summary>General app-level config that isn't specific to one feature area.</summary>
public sealed class AppOptions
{
    /// <summary>Public URL of the web app (no trailing slash), used to build links embedded in
    /// outbound email — e.g. the password reset link. Same value as Cors:AllowedOrigins in
    /// practice, kept separate because CORS is a list and link-building needs one canonical URL.</summary>
    public string WebBaseUrl { get; init; } = "http://localhost:3000";
}
