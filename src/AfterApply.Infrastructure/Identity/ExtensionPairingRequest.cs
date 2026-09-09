namespace AfterApply.Infrastructure.Identity;

/// <summary>
/// One in-flight "connect this extension to my account" handshake. Lives for minutes, not days:
/// it exists only to carry a person's confirmation from the browser they are signed into back to
/// the extension that started it.
///
/// Two fields do the work, and they are stored differently on purpose. <see cref="Code"/> is
/// plaintext because it is an identifier the user is meant to read and compare — knowing it is not
/// enough to obtain anything. <see cref="DeviceSecretHash"/> is a hash because the secret behind it
/// is what actually collects the token, exactly like every other credential in this schema
/// (RefreshToken, PersonalAccessToken): a database copy must not be usable.
///
/// No token value is ever stored here. The personal access token is minted at collection time
/// (<see cref="Complete"/>), so an approval nobody picks up leaves no credential behind and there
/// is no window in which a usable secret sits at rest in this table.
/// </summary>
public sealed class ExtensionPairingRequest
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public string Code { get; private set; } = string.Empty;

    public string DeviceSecretHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Who confirmed it — the account the minted token will belong to. Null until then.
    /// </summary>
    public Guid? ApprovedByUserId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }

    public DateTimeOffset? DeniedAt { get; private set; }

    /// <summary>When the extension collected its token. Terminal: set once, and the row answers
    /// every later poll with "completed" and no token.</summary>
    public DateTimeOffset? CompletedAt { get; private set; }

    private ExtensionPairingRequest()
    {
    }

    public static ExtensionPairingRequest Create(string code, string deviceSecretHash, DateTimeOffset now, TimeSpan lifetime)
    {
        return new ExtensionPairingRequest
        {
            Code = code,
            DeviceSecretHash = deviceSecretHash,
            CreatedAt = now,
            ExpiresAt = now + lifetime
        };
    }

    /// <summary>Still waiting for a decision, and still in time to receive one.</summary>
    public bool IsPendingAt(DateTimeOffset now) =>
        ApprovedAt is null && DeniedAt is null && CompletedAt is null && ExpiresAt > now;

    /// <summary>Approved and still collectable. Expiry applies to collection too: an approval left
    /// uncollected past the deadline dies with the request rather than becoming a standing
    /// permission to mint a token later.</summary>
    public bool IsCollectableAt(DateTimeOffset now) =>
        ApprovedAt is not null && CompletedAt is null && DeniedAt is null && ExpiresAt > now;

    public void Approve(Guid userId, DateTimeOffset now)
    {
        ApprovedByUserId = userId;
        ApprovedAt = now;
    }

    public void Deny(DateTimeOffset now)
    {
        DeniedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        CompletedAt = now;
    }
}
