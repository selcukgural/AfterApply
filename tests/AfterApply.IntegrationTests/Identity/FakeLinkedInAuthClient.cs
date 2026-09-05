using System.Collections.Concurrent;
using AfterApply.Application.Identity;

namespace AfterApply.IntegrationTests.Identity;

/// <summary>Stands in for LinkedIn's token endpoint: a test registers an identity and gets back a
/// one-time code, exactly the contract the real exchange has (a code is spent on first use, whether
/// or not the sign-in then succeeds).</summary>
public sealed class FakeLinkedInAuthClient : ILinkedInAuthClient
{
    private readonly ConcurrentDictionary<string, LinkedInIdentity> _codes = new();

    public ConcurrentQueue<(string Code, string RedirectUri)> Exchanges { get; } = new();

    public string IssueCode(LinkedInIdentity identity)
    {
        var code = "li-" + Guid.NewGuid().ToString("N");
        _codes[code] = identity;
        return code;
    }

    public Task<LinkedInIdentity?> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken)
    {
        Exchanges.Enqueue((code, redirectUri));
        return Task.FromResult(_codes.TryRemove(code, out var identity) ? identity : null);
    }
}
