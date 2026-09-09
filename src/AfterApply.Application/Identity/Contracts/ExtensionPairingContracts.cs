namespace AfterApply.Application.Identity.Contracts;

/// <summary>What the extension asks for when the user clicks "Connect". The locale only decides
/// which language the verification page opens in.</summary>
public sealed record StartExtensionPairingRequest(string? Locale = null);

/// <summary>
/// The half the extension keeps (<paramref name="DeviceSecret"/>) and the half the user reads
/// (<paramref name="Code"/>). Splitting them is the whole security model: the code travels through
/// a URL and a pair of eyes, the secret never leaves the extension's storage, and only the holder
/// of the secret can collect the token the approval produces.
/// </summary>
public sealed record StartedExtensionPairingResponse(
    string Code,
    string DeviceSecret,
    DateTimeOffset ExpiresAt,
    string VerificationUrl,
    int PollIntervalSeconds);

public sealed record PollExtensionPairingRequest(string DeviceSecret);

/// <summary>
/// Where a pairing stands, from the extension's side. <see cref="ExtensionPairingStatus.Completed"/>
/// is the only status that carries a token, and it is returned exactly once — the row is marked
/// completed in the same transaction that mints the token.
/// </summary>
public sealed record ExtensionPairingPollResponse(
    ExtensionPairingStatus Status,
    string? Token = null,
    DateTimeOffset? TokenExpiresAt = null);

/// <summary>What the verification page shows before asking the user to confirm: the code they
/// should be comparing against the extension, and how long it stays valid.</summary>
public sealed record ExtensionPairingReviewResponse(
    string Code,
    DateTimeOffset ExpiresAt,
    ExtensionPairingStatus Status);

public enum ExtensionPairingStatus
{
    /// <summary>Waiting for the signed-in user to confirm the code.</summary>
    Pending = 0,

    /// <summary>Confirmed, but the extension has not collected the token yet. Transient: the next
    /// poll turns it into <see cref="Completed"/>.</summary>
    Approved = 1,

    /// <summary>The token has been handed to the extension. Terminal.</summary>
    Completed = 2,

    /// <summary>The user said this was not them. Terminal, and the reason the page offers the
    /// button at all — a pairing request someone else started should be refusable, not just
    /// ignorable.</summary>
    Denied = 3,

    /// <summary>Ran out of time before it was confirmed and collected.</summary>
    Expired = 4,

    /// <summary>Approved, but the account already holds as many active tokens as it may. The row
    /// stays approved: revoking one on the settings page and letting the extension poll again is
    /// enough, no need to start over.</summary>
    TokenLimitReached = 5
}
