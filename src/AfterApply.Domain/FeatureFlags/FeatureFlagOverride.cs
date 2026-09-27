namespace AfterApply.Domain.FeatureFlags;

/// <summary>
/// A flag switched on or off at runtime from the admin panel (DECISIONS.md 2026-09-27). One row
/// per flag that has been switched; a flag with no row runs on its deploy default (appsettings.json
/// and the deploy workflow's env vars). Removing the row is "back to the default".
/// </summary>
public sealed class FeatureFlagOverride
{
    /// <summary>The flag's stable name (<c>FeatureFlag</c> member name). Never renamed: a rename
    /// would leave the stored row behind and silently return the flag to its default.</summary>
    public string Key { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>The admin who set it; null once that account is deleted.</summary>
    public Guid? UpdatedByUserId { get; private set; }

    private FeatureFlagOverride()
    {
    }

    public static FeatureFlagOverride Create(string key, bool enabled, Guid adminId, DateTimeOffset now) =>
        new() { Key = key, Enabled = enabled, UpdatedAt = now, UpdatedByUserId = adminId };

    public void Set(bool enabled, Guid adminId, DateTimeOffset now)
    {
        Enabled = enabled;
        UpdatedAt = now;
        UpdatedByUserId = adminId;
    }
}

/// <summary>
/// One switch of a flag, kept for good: who, when, what it became and why. Written only after both
/// confirmation steps passed. The connection it came from is its <see cref="FeatureFlagChangeOrigin"/>.
/// </summary>
public sealed class FeatureFlagChange
{
    public const int MinReasonLength = 5;

    public const int MaxReasonLength = 500;

    public Guid Id { get; private set; }

    public string Key { get; private set; } = string.Empty;

    /// <summary>What the override became; null means it was removed (back to the deploy default).</summary>
    public bool? Enabled { get; private set; }

    /// <summary>Whether the flag was effectively on before and after — the default included, so
    /// the history reads right even when a reset changed nothing visible.</summary>
    public bool WasOn { get; private set; }

    public bool IsOn { get; private set; }

    /// <summary>Why — required at the first confirmation step.</summary>
    public string Reason { get; private set; } = string.Empty;

    public DateTimeOffset ChangedAt { get; private set; }

    public Guid? ChangedByUserId { get; private set; }

    private FeatureFlagChange()
    {
    }

    public static FeatureFlagChange Record(string key, bool? enabled, bool wasOn, bool isOn, string reason,
        Guid adminId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        Key = key,
        Enabled = enabled,
        WasOn = wasOn,
        IsOn = isOn,
        Reason = reason.Trim(),
        ChangedAt = now,
        ChangedByUserId = adminId
    };
}

/// <summary>
/// Which account and which connection confirmed a <see cref="FeatureFlagChange"/>. Kept apart from
/// the change for the rule <c>RequestAudit</c> follows (DECISIONS.md 2026-09-14): the IP is held
/// for an investigation of who changed the product's behaviour, is never returned by an endpoint,
/// shown on a screen or logged, and goes with the account (cascade) — while the change itself
/// stays in the history, with its author forgotten.
/// </summary>
public sealed class FeatureFlagChangeOrigin
{
    /// <summary>An IPv6 address with an embedded IPv4 tail is 45 characters at most.</summary>
    public const int MaxIpAddressLength = 45;

    public Guid ChangeId { get; private set; }

    public Guid UserId { get; private set; }

    public string? IpAddress { get; private set; }

    private FeatureFlagChangeOrigin()
    {
    }

    public static FeatureFlagChangeOrigin For(FeatureFlagChange change, Guid userId, string? ipAddress)
    {
        var trimmed = ipAddress?.Trim();
        return new FeatureFlagChangeOrigin
        {
            ChangeId = change.Id,
            UserId = userId,
            IpAddress = string.IsNullOrEmpty(trimmed) ? null
                : trimmed.Length <= MaxIpAddressLength ? trimmed : trimmed[..MaxIpAddressLength]
        };
    }
}
