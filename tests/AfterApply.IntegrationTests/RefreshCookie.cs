using System.Net.Http.Json;
using AfterApply.Api.Endpoints;

namespace AfterApply.IntegrationTests;

/// <summary>
/// The refresh-token cookie as a browser would handle it. The test server speaks plain http, where
/// an HttpClient's cookie container won't send a <c>Secure</c> cookie back, so the value is read off
/// <c>Set-Cookie</c> and presented in a <c>Cookie</c> header by hand.
/// </summary>
public static class RefreshCookie
{
    /// <summary>The raw token. ASP.NET URL-encodes a cookie value on the way out (and decodes it on
    /// the way in), so the header carries <c>%2B</c> where the token has <c>+</c>.</summary>
    public static string? From(HttpResponseMessage response) =>
        SetCookieHeader(response)?.Split(';')[0].Split('=', 2)[1] is { Length: > 0 } value ? Uri.UnescapeDataString(value) : null;

    /// <summary>The whole <c>Set-Cookie</c> line for the refresh cookie, attributes included.</summary>
    public static string? SetCookieHeader(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(v => v.StartsWith(RefreshTokenCookie.Name + "=", StringComparison.Ordinal))
            : null;

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken, string? origin = null) =>
        SendWithCookieAsync(client, "/api/auth/refresh", refreshToken, origin);

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string refreshToken, string? origin = null) =>
        SendWithCookieAsync(client, "/api/auth/logout", refreshToken, origin);

    private static Task<HttpResponseMessage> SendWithCookieAsync(HttpClient client, string path, string refreshToken, string? origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
        request.Headers.Add("Cookie", $"{RefreshTokenCookie.Name}={Uri.EscapeDataString(refreshToken)}");
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }
        return client.SendAsync(request);
    }
}
