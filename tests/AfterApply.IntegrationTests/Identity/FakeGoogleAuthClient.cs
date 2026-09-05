using System.Collections.Concurrent;
using AfterApply.Application.Identity;

namespace AfterApply.IntegrationTests.Identity;

/// <summary>Stands in for Google's token endpoint: a test registers an identity and gets back a
/// one-time code, exactly the contract the real exchange has (a code is spent on first use, whether
/// or not the sign-in then succeeds).</summary>
public sealed class FakeGoogleAuthClient : IGoogleAuthClient
{
    private readonly ConcurrentDictionary<string, GoogleIdentity> _codes = new();

    public ConcurrentQueue<(string Code, string CodeVerifier, string RedirectUri)> Exchanges { get; } = new();

    public string IssueCode(GoogleIdentity identity)
    {
        var code = "4/" + Guid.NewGuid().ToString("N");
        _codes[code] = identity;
        return code;
    }

    public Task<GoogleIdentity?> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken cancellationToken)
    {
        Exchanges.Enqueue((code, codeVerifier, redirectUri));
        return Task.FromResult(_codes.TryRemove(code, out var identity) ? identity : null);
    }
}
