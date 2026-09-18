using System.Security.Cryptography;
using System.Text;

namespace AfterApply.Api.RateLimits;

/// <summary>
/// What a partition is called in Redis. The policy name is part of it because the library keys a
/// window on the partition alone (<c>rl:fw:{partition}</c>) and seven policies partition by IP —
/// without the name, one address would share a single window across all of them. The caller's
/// own key (a user id or an IP) goes in hashed: an IP is personal data, and while a window lives
/// only as long as its TTL, a key that never carries the address at all needs no such argument.
/// The in-memory fallback keys its own dictionary by the raw partition, as it always did.
/// </summary>
public static class RateLimitPartitionKeys
{
    public static string ForRedis(string keyPrefix, string policy, string partitionKey)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(partitionKey));
        return $"{keyPrefix}{policy}:{Convert.ToHexStringLower(digest.AsSpan(0, 12))}";
    }
}
