using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace AfterApply.Api.RateLimits;

/// <summary>
/// Who a request counts against when it has no account to count against (2026-09-24).
///
/// <list type="bullet">
/// <item>An IPv6 caller is its /64, not its full address. A single host commonly holds a whole /64
/// and can pick a fresh address per request, so a per-address limit would not limit it at all.</item>
/// <item>The web app's server-side rendering reaches the API from the web service's own egress
/// address, which every visitor would share — one crawler could spend everyone's bucket. When a
/// request carries the render key (<c>RateLimiting:ServerRenderKey</c>, a secret both services
/// hold), the visitor address the web app names in <see cref="ClientHeader"/> is used instead.
/// Without the key that header is ignored, so nobody else can claim an address.</item>
/// </list>
/// </summary>
public static class ClientPartition
{
    public const string KeyHeader = "X-Render-Key";
    public const string ClientHeader = "X-Render-Client";

    /// <summary>The address to count the request against — see the type's remarks.</summary>
    public static IPAddress? ClientAddress(HttpContext httpContext, string? serverRenderKey)
    {
        if (!string.IsNullOrEmpty(serverRenderKey)
            && httpContext.Request.Headers.TryGetValue(KeyHeader, out var presented)
            && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(presented.ToString()), Encoding.UTF8.GetBytes(serverRenderKey))
            && IPAddress.TryParse(httpContext.Request.Headers[ClientHeader].ToString(), out var visitor))
        {
            return visitor;
        }

        return httpContext.Connection.RemoteIpAddress;
    }

    /// <summary>The partition key for an address: IPv4 as is, IPv6 as its /64.</summary>
    public static string ForAddress(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }
}
