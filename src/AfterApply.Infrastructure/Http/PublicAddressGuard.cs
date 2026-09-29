using System.Net;
using System.Net.Sockets;

namespace AfterApply.Infrastructure.Http;

/// <summary>
/// The SSRF guard for fetches whose host is not on an allow-list (a company's own website —
/// DECISIONS.md 2026-09-28). Checking the URL is not enough for those: the name can resolve to
/// 127.0.0.1, the cloud metadata address or a private range, and it can resolve differently the
/// second time it is asked. So the check sits in the socket's own connect step: the name is
/// resolved once, every address it resolves to must be public, and the connection is made to one
/// of exactly those addresses — there is no second lookup for a rebinding answer to slip into.
/// </summary>
/// <summary>A connection the guard refused. An answer, not an outage: the handler wraps it in an
/// <see cref="HttpRequestException"/>, and callers look for it underneath so they do not retry it.</summary>
public sealed class NonPublicAddressException(string host)
    : Exception($"Refused to connect to {host}: it does not resolve to public addresses only.");

public static class PublicAddressGuard
{
    /// <summary>Whether a failed request was the guard's refusal rather than a network problem.</summary>
    public static bool IsRefusal(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is NonPublicAddressException)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A handler that connects only to public addresses, never follows a redirect on its
    /// own (the caller re-checks each hop) and takes no cookies.</summary>
    public static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectCallback = ConnectAsync
    };

    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        // All of them, not just the one connected to: a name that also points somewhere private is
        // not a name to trust with any of its answers.
        if (addresses.Length == 0 || !addresses.All(IsPublic))
        {
            throw new NonPublicAddressException(host);
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Whether an address is on the public internet: not loopback, private, link-local
    /// (169.254.169.254 — the metadata server — among it), carrier-grade NAT, multicast,
    /// documentation, benchmarking or reserved space, in either family.</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0                                        // 0.0.0.0/8
                || b[0] == 10                                         // 10.0.0.0/8
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)         // 100.64.0.0/10 carrier-grade NAT
                || b[0] == 127                                        // loopback
                || (b[0] == 169 && b[1] == 254)                       // link-local, metadata
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)          // 172.16.0.0/12
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)            // 192.0.0.0/24 protocol assignments
                || (b[0] == 192 && b[1] == 0 && b[2] == 2)            // TEST-NET-1
                || (b[0] == 192 && b[1] == 168)                       // 192.168.0.0/16
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))        // benchmarking
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)         // TEST-NET-2
                || (b[0] == 203 && b[1] == 0 && b[2] == 113)          // TEST-NET-3
                || b[0] >= 224);                                      // multicast, reserved, broadcast
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (address.Equals(IPAddress.IPv6Loopback) || address.Equals(IPAddress.IPv6None)
                || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
            {
                return false;
            }

            var b = address.GetAddressBytes();
            if ((b[0] & 0xFE) == 0xFC)                                // fc00::/7 unique local
            {
                return false;
            }

            if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0D && b[3] == 0xB8) // 2001:db8::/32 documentation
            {
                return false;
            }

            // ::/96 (IPv4-compatible, "::" itself included), 64:ff9b::/96 (NAT64) and 2002::/16
            // (6to4) carry an IPv4 address: judge that one.
            if (b[..12].All(x => x == 0))
            {
                return IsPublic(new IPAddress(b[12..16]));
            }

            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B && b[4..12].All(x => x == 0))
            {
                return IsPublic(new IPAddress(b[12..16]));
            }

            if (b[0] == 0x20 && b[1] == 0x02)
            {
                return IsPublic(new IPAddress(b[2..6]));
            }

            return true;
        }

        return false;
    }
}
