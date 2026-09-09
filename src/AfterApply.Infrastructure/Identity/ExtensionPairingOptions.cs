namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// Sizing for the extension pairing handshake, bound from the <c>ExtensionPairing</c> section.
/// </summary>
public sealed class ExtensionPairingOptions
{
    public const string SectionName = "ExtensionPairing";

    /// <summary>How long a started pairing stays open. Long enough to register an account in the
    /// middle of it — the whole point of the flow is that someone arriving from the Web Store has
    /// no account yet — and short enough that an abandoned code is not sitting there to be
    /// confirmed by a passer-by an hour later.</summary>
    public int LifetimeMinutes { get; init; } = 10;

    /// <summary>What the extension is told to wait between polls. Published rather than hardcoded
    /// in the extension because the extension is a shipped artefact: a build already installed
    /// cannot be re-tuned, but it can be told.</summary>
    public int PollIntervalSeconds { get; init; } = 3;

    public TimeSpan Lifetime => TimeSpan.FromMinutes(LifetimeMinutes);
}
