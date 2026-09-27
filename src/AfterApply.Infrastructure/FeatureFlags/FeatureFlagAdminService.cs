using System.Security.Cryptography;
using System.Text.Json;
using AfterApply.Application.FeatureFlags;
using AfterApply.Application.FeatureFlags.Contracts;
using AfterApply.Domain.FeatureFlags;
using AfterApply.Application.Localization;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace AfterApply.Infrastructure.FeatureFlags;

internal sealed class FeatureFlagAdminService(
    AppDbContext dbContext,
    FeatureFlagCatalog catalog,
    FeatureFlagStore store,
    FeatureFlagChannel channel,
    IDataProtectionProvider dataProtection,
    IStringLocalizer<SharedStrings> localizer,
    ILogger<FeatureFlagAdminService> logger,
    TimeProvider? timeProvider = null) : IFeatureFlagAdminService
{
    /// <summary>How long the second step has after the first. Long enough to read the dialog,
    /// short enough that a token left in a tab is worthless by the time anyone finds it.</summary>
    public static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromMinutes(5);

    private const string ProtectorPurpose = "AfterApply.FeatureFlags.Confirmation.v1";

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private sealed record OverrideRow(string Key, bool Enabled, DateTimeOffset UpdatedAt, string? UpdatedBy);

    /// <summary>
    /// What the token binds: the admin, the flag, the change, the reason, and the override exactly
    /// as it stood at the first step (present or not, value, time). The last part makes the token
    /// single-use and makes a change someone else made in between fail the second step instead of
    /// being silently overwritten.
    /// </summary>
    private sealed record Ticket(
        Guid AdminId,
        string Flag,
        bool? Enabled,
        string Reason,
        bool HadOverride,
        bool? OverrideEnabled,
        long OverrideUpdatedAtTicks,
        string Nonce);

    public async Task<IReadOnlyList<FeatureFlagResponse>> ListAsync(CancellationToken cancellationToken)
    {
        // From the database, not this instance's snapshot: the panel shows what was decided even
        // in the second or two before every instance has re-read it.
        var rows = await ReadOverridesAsync(cancellationToken);
        return Enum.GetValues<FeatureFlag>()
            .Select(flag => ToResponse(flag, rows.GetValueOrDefault(flag.ToString())))
            .ToList();
    }

    public async Task<PrepareFeatureFlagChangeResult> PrepareAsync(FeatureFlag flag, bool? enabled, string reason,
        Guid adminId, CancellationToken cancellationToken)
    {
        var key = flag.ToString();
        var row = (await ReadOverridesAsync(cancellationToken)).GetValueOrDefault(key);
        var current = ToResponse(flag, row);

        if (enabled == true && current.MissingPrerequisite is not null)
        {
            return new PrepareFeatureFlagChangeResult(PrepareFeatureFlagOutcome.MissingPrerequisite, current, null);
        }

        if (row?.Enabled == enabled)
        {
            // Same override already, or no override and a reset asked: nothing to confirm.
            return new PrepareFeatureFlagChangeResult(PrepareFeatureFlagOutcome.Unchanged, current, null);
        }

        var ticket = new Ticket(adminId, key, enabled, reason.Trim(), row is not null, row?.Enabled,
            row?.UpdatedAt.UtcTicks ?? 0, Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
        var expiresAt = _timeProvider.GetUtcNow().Add(ConfirmationLifetime);
        var token = Protector().Protect(JsonSerializer.Serialize(ticket), expiresAt);

        return new PrepareFeatureFlagChangeResult(PrepareFeatureFlagOutcome.Ready, current,
            new PrepareFeatureFlagChangeResponse(current, enabled, enabled ?? current.Default, key, token, expiresAt));
    }

    public async Task<ConfirmFeatureFlagChangeResult> ConfirmAsync(FeatureFlag flag, string token, string phrase,
        Guid adminId, string? ipAddress, CancellationToken cancellationToken)
    {
        var key = flag.ToString();
        if (ReadTicket(token) is not { } ticket || ticket.AdminId != adminId || ticket.Flag != key)
        {
            return new ConfirmFeatureFlagChangeResult(ConfirmFeatureFlagOutcome.InvalidToken, null);
        }

        // Exact, case included: the point is that the admin read which flag this is and typed it.
        if (!string.Equals(phrase.Trim(), key, StringComparison.Ordinal))
        {
            return new ConfirmFeatureFlagChangeResult(ConfirmFeatureFlagOutcome.PhraseMismatch, null);
        }

        if (ticket.Enabled == true && catalog.MissingPrerequisiteOf(flag) is not null)
        {
            return new ConfirmFeatureFlagChangeResult(ConfirmFeatureFlagOutcome.MissingPrerequisite, null);
        }

        var row = await dbContext.FeatureFlagOverrides.FirstOrDefaultAsync(o => o.Key == key, cancellationToken);
        var unchangedSinceFirstStep = ticket.HadOverride
            ? row is not null && row.Enabled == ticket.OverrideEnabled && row.UpdatedAt.UtcTicks == ticket.OverrideUpdatedAtTicks
            : row is null;
        if (!unchangedSinceFirstStep)
        {
            return new ConfirmFeatureFlagChangeResult(ConfirmFeatureFlagOutcome.Stale, null);
        }

        var defaultOn = catalog.DefaultOf(flag);
        var wasOn = row?.Enabled ?? defaultOn;
        var isOn = ticket.Enabled ?? defaultOn;
        var now = _timeProvider.GetUtcNow();

        switch (ticket.Enabled, row)
        {
            case (null, not null):
                dbContext.FeatureFlagOverrides.Remove(row);
                break;
            case ({ } value, null):
                dbContext.FeatureFlagOverrides.Add(FeatureFlagOverride.Create(key, value, adminId, now));
                break;
            case ({ } value, not null):
                row.Set(value, adminId, now);
                break;
        }

        var change = FeatureFlagChange.Record(key, ticket.Enabled, wasOn, isOn, ticket.Reason, adminId, now);
        dbContext.FeatureFlagChanges.Add(change);
        dbContext.FeatureFlagChangeOrigins.Add(FeatureFlagChangeOrigin.For(change, adminId, ipAddress));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Two confirmations raced over an existing override: UpdatedAt is the row's concurrency
            // token, so the later UPDATE/DELETE matched nothing. Same answer as a change made in between.
            return new ConfirmFeatureFlagChangeResult(ConfirmFeatureFlagOutcome.Stale, null);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
        {
            // The same race for a flag that had no override: the other one inserted the row first.
            return new ConfirmFeatureFlagChangeResult(ConfirmFeatureFlagOutcome.Stale, null);
        }

        // Ids and states only — the admin's e-mail and the IP never reach a log line.
        logger.LogWarning("Feature flag {Flag} switched {From} -> {To} (override {Override}) by user {UserId}",
            key, wasOn ? "on" : "off", isOn ? "on" : "off", ticket.Enabled?.ToString() ?? "removed", adminId);

        // This instance at once, the others on the announcement (or their next poll).
        await store.ReloadAsync(cancellationToken);
        await channel.PublishAsync();

        var saved = (await ReadOverridesAsync(cancellationToken)).GetValueOrDefault(key);
        return new ConfirmFeatureFlagChangeResult(ConfirmFeatureFlagOutcome.Changed, ToResponse(flag, saved));
    }

    public async Task<IReadOnlyList<FeatureFlagChangeResponse>> GetHistoryAsync(FeatureFlag? flag, int limit,
        CancellationToken cancellationToken)
    {
        var changes = dbContext.FeatureFlagChanges.AsNoTracking();
        if (flag is { } only)
        {
            var key = only.ToString();
            changes = changes.Where(c => c.Key == key);
        }

        var rows = await changes
            .OrderByDescending(c => c.ChangedAt).ThenByDescending(c => c.Id)
            .Take(limit)
            .Select(c => new
            {
                c.Id,
                c.Key,
                c.Enabled,
                c.WasOn,
                c.IsOn,
                c.Reason,
                c.ChangedAt,
                ChangedBy = dbContext.Users.Where(u => u.Id == c.ChangedByUserId).Select(u => u.Email).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        // A key no member answers to any more is history of a flag that no longer exists.
        return rows
            .Where(r => FeatureFlagNames.TryParse(r.Key, out _))
            .Select(r => new FeatureFlagChangeResponse(r.Id, FeatureFlagNames.ParseKnown(r.Key), r.Enabled, r.WasOn, r.IsOn,
                r.Reason, r.ChangedAt, r.ChangedBy))
            .ToList();
    }

    private ITimeLimitedDataProtector Protector() =>
        dataProtection.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();

    private Ticket? ReadTicket(string token)
    {
        try
        {
            var json = Protector().Unprotect(token, out var expiresAt);
            // The protector checks expiry against the wall clock; this checks it against the one
            // the rest of the service uses, so the two cannot disagree in a test or a skewed host.
            return expiresAt < _timeProvider.GetUtcNow() ? null : JsonSerializer.Deserialize<Ticket>(json);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    private async Task<Dictionary<string, OverrideRow>> ReadOverridesAsync(CancellationToken cancellationToken) =>
        await dbContext.FeatureFlagOverrides.AsNoTracking()
            .Select(o => new OverrideRow(o.Key, o.Enabled, o.UpdatedAt,
                dbContext.Users.Where(u => u.Id == o.UpdatedByUserId).Select(u => u.Email).FirstOrDefault()))
            .ToDictionaryAsync(o => o.Key, cancellationToken);

    private FeatureFlagResponse ToResponse(FeatureFlag flag, OverrideRow? row)
    {
        var defaultOn = catalog.DefaultOf(flag);
        var prefix = FeatureFlagTexts.KeyPrefix(flag);
        var notes = localizer[$"{prefix}_NOTES"];
        return new FeatureFlagResponse(flag, localizer[$"{prefix}_TITLE"], localizer[$"{prefix}_DESCRIPTION"],
            localizer[$"{prefix}_WHEN_OFF"], notes.ResourceNotFound ? null : notes.Value,
            row?.Enabled ?? defaultOn, defaultOn, row?.Enabled, row?.UpdatedAt,
            row?.UpdatedBy, catalog.CouplingsOf(flag), catalog.MissingPrerequisiteOf(flag));
    }
}
