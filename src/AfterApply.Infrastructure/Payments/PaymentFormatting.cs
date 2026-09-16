using System.Globalization;
using AfterApply.Application.Payments;
using AfterApply.Domain.Payments;

namespace AfterApply.Infrastructure.Payments;

/// <summary>Text for e-mails and PayTR's basket, in the user's locale. The web formats its own
/// numbers; this is only for what leaves the server as prose.</summary>
public static class PaymentFormatting
{
    public static string Amount(long amountMinor, string locale)
    {
        var value = amountMinor / 100m;
        return locale == "en"
            ? "₺" + value.ToString("N2", CultureInfo.GetCultureInfo("en-GB"))
            : value.ToString("N2", CultureInfo.GetCultureInfo("tr-TR")) + " ₺";
    }

    public static string Date(DateTimeOffset at, string locale)
    {
        var local = TimeZoneInfo.ConvertTime(at, MonthlySummary.MerchantTimeZone);
        return local.ToString("d MMMM yyyy", CultureInfo.GetCultureInfo(locale == "en" ? "en-GB" : "tr-TR"));
    }

    public static string PlanName(ProPlan plan, string locale) => (plan, locale) switch
    {
        (ProPlan.Monthly, "en") => "e-kariyerim Pro — 1 month",
        (ProPlan.Yearly, "en") => "e-kariyerim Pro — 1 year",
        (ProPlan.Monthly, _) => "e-kariyerim Pro — 1 ay",
        (ProPlan.Yearly, _) => "e-kariyerim Pro — 1 yıl",
        _ => "e-kariyerim Pro"
    };

    /// <summary>PayTR refuses an e-mail with non-ASCII characters; ours are validated on sign-up
    /// but an internationalised address can still get through an OAuth provider.</summary>
    public static bool IsAsciiEmail(string email) =>
        email.Length is > 0 and <= PaymentOrder.MaxEmailLength && email.All(c => c < 128 && !char.IsControl(c));

    /// <summary>The user's locale for links and e-mails: only the two we ship, defaulting to Turkish.</summary>
    public static string NormalizeLocale(string? locale) => locale == "en" ? "en" : "tr";
}
