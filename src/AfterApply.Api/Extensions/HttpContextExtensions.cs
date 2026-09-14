namespace AfterApply.Api.Extensions;

public static class HttpContextExtensions
{
    /// <summary>The real client's address — but only because UseForwardedHeaders runs first in the
    /// pipeline (Program.cs); behind Cloud Run's frontend the raw connection is the proxy's.</summary>
    public static string? GetClientIpAddress(this HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString();
}
