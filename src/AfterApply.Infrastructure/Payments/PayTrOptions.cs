using Microsoft.Extensions.Options;

namespace AfterApply.Infrastructure.Payments;

/// <summary>
/// PayTR iFrame API settings. The three merchant values are secrets (user-secrets locally,
/// Secret Manager in prod — see DEPLOYMENT.md §15); the rest is plain configuration. Unlike most
/// feature options this one is validated on start when <see cref="Enabled"/> is true: a checkout
/// that silently sends a zero amount or an empty key is worse than a service that refuses to boot.
/// </summary>
public sealed class PayTrOptions
{
    public const string SectionName = "PayTr";

    /// <summary>Master switch for the checkout surface (<c>/api/payments/*</c> and the web's
    /// price/button). The notification endpoint ignores it and only needs the secrets, so a late
    /// notification after the switch is turned off is still applied.</summary>
    public bool Enabled { get; init; }

    public string? MerchantId { get; init; }

    public string? MerchantKey { get; init; }

    public string? MerchantSalt { get; init; }

    /// <summary>Sent as PayTR's <c>test_mode</c>: the merchant is live but this transaction is a
    /// test (test cards, no money moves). Left on until the first real notification round-trips in
    /// production, then turned off.</summary>
    public bool TestMode { get; init; }

    /// <summary>PayTR's <c>debug_on</c>: return a reason with a failed get-token instead of the
    /// generic code 99. Always on — the reason goes to our log, never to the user.</summary>
    public bool DebugOn { get; init; } = true;

    /// <summary>PayTR's <c>timeout_limit</c>, minutes the payment page stays open. Also how long
    /// we keep an order pending before the expiry job closes it (plus <see cref="PendingGraceMinutes"/>).</summary>
    public int TimeoutLimitMinutes { get; init; } = 30;

    /// <summary>Extra minutes past the window before a pending order is marked expired, so a
    /// notification that is merely late does not race the job.</summary>
    public int PendingGraceMinutes { get; init; } = 15;

    public string Currency { get; init; } = "TL";

    /// <summary>Which revision of the distance-sales terms the checkout checkbox refers to;
    /// stamped on the order.</summary>
    public string TermsVersion { get; init; } = "2026-09";

    public PayTrPlanPrices Plans { get; init; } = new();

    /// <summary>PayTR rejects private addresses as <c>user_ip</c>, so a developer on localhost
    /// puts their public IP here. Read only when the host environment is Development.</summary>
    public string? DevUserIpOverride { get; init; }

    /// <summary>Days before the entitlement end at which the "your Pro period is ending" e-mail goes out.</summary>
    public int ExpiryReminderDays { get; init; } = 3;

    /// <summary>When the expiry reminder job runs (server time is UTC; 06:00 UTC is 09:00 in Türkiye).</summary>
    public string ExpiryReminderCron { get; init; } = "0 6 * * *";

    public string OrderExpiryCron { get; init; } = "*/10 * * * *";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(MerchantId) &&
        !string.IsNullOrWhiteSpace(MerchantKey) &&
        !string.IsNullOrWhiteSpace(MerchantSalt);

    public TimeSpan TimeoutLimit => TimeSpan.FromMinutes(TimeoutLimitMinutes);
}

/// <summary>Prices in kuruş (amount × 100, as PayTR takes them), KDV included. Zero means "not for sale".</summary>
public sealed class PayTrPlanPrices
{
    public PayTrPlanPrice Monthly { get; init; } = new();

    public PayTrPlanPrice Yearly { get; init; } = new();
}

public sealed class PayTrPlanPrice
{
    public long AmountMinor { get; init; }
}

public sealed class PayTrOptionsValidator : IValidateOptions<PayTrOptions>
{
    public ValidateOptionsResult Validate(string? name, PayTrOptions options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();
        if (!options.IsConfigured)
        {
            failures.Add("PayTr:MerchantId, PayTr:MerchantKey and PayTr:MerchantSalt must all be set while PayTr:Enabled is true");
        }

        if (options.Plans.Monthly.AmountMinor <= 0)
        {
            failures.Add("PayTr:Plans:Monthly:AmountMinor must be a positive number of kuruş");
        }

        if (options.Plans.Yearly.AmountMinor <= 0)
        {
            failures.Add("PayTr:Plans:Yearly:AmountMinor must be a positive number of kuruş");
        }

        if (options.TimeoutLimitMinutes <= 0)
        {
            failures.Add("PayTr:TimeoutLimitMinutes must be positive");
        }

        if (options.Currency is not ("TL" or "TRY"))
        {
            failures.Add("PayTr:Currency must be TL (only Turkish lira is sold)");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
