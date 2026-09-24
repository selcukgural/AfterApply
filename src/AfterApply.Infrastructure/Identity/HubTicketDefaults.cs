namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// The credential a SignalR connection authenticates with. A browser cannot put an Authorization
/// header on a WebSocket handshake, so whatever the hub accepts travels in the URL — and a URL is
/// written into the load balancer's request log, the browser's history and any error report that
/// captures it. The session's access token used to be that value; a hub ticket is what goes there
/// now: signed like an access token but for its own audience (the API's JwtBearer rejects it, and
/// this scheme rejects an access token) and good for <see cref="Lifetime"/>. A ticket lifted from a
/// log is worth, for a minute, a view of its owner's import progress — nothing in the API.
///
/// Not single-use, deliberately: the long-polling and server-sent-events transports present the
/// same credential on every request of one connection, and a spent-once ticket would break the
/// fallbacks that exist for networks where WebSockets do not get through. The client asks for a
/// fresh ticket on every connect and reconnect, and an open connection is not re-authenticated.
/// </summary>
public static class HubTicketDefaults
{
    public const string AuthenticationScheme = "HubTicket";

    public const string Audience = "AfterApply.Hubs";

    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);
}
