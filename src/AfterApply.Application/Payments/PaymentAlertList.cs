using AfterApply.Domain.Payments;

namespace AfterApply.Application.Payments;

/// <summary>The alert list's sort keys, as the query string names them.</summary>
public enum PaymentAlertSortKey
{
    ReceivedAt,
    Outcome,
    Status,
    MerchantOid
}

/// <summary>
/// What the admin's alert list understands: which outcomes count as an alert, and how a sort
/// request from the query string maps onto the table. Unknown input falls back to the default
/// (newest first) rather than a 400 — an admin who mistypes a query string gets the plain list.
/// </summary>
public static class PaymentAlertList
{
    public const int DefaultDays = 30;
    public const int MaxDays = 365;
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    /// <summary>Notifications we rejected, could not match, or applied late. Applied, Duplicate
    /// and RefundRecorded are the normal course of business and stay off the list.</summary>
    public static readonly PaymentNotificationOutcome[] Outcomes =
    [
        PaymentNotificationOutcome.BadHash, PaymentNotificationOutcome.UnknownOrder, PaymentNotificationOutcome.LateApplied,
        PaymentNotificationOutcome.Malformed, PaymentNotificationOutcome.Error
    ];

    public static int ClampDays(int? days) => Math.Clamp(days ?? DefaultDays, 1, MaxDays);

    public static int ClampPageSize(int? pageSize) => Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);

    /// <summary>"receivedAt" | "outcome" | "status" | "merchantOid" (case-insensitive) with
    /// "asc" | "desc". Received-at defaults to descending, the text keys to ascending.</summary>
    public static (PaymentAlertSortKey Key, bool Descending) ParseSort(string? sort, string? direction)
    {
        var key = Enum.TryParse<PaymentAlertSortKey>(sort, ignoreCase: true, out var parsed) ? parsed : PaymentAlertSortKey.ReceivedAt;
        var descending = direction?.Trim().ToLowerInvariant() switch
        {
            "asc" => false,
            "desc" => true,
            _ => key == PaymentAlertSortKey.ReceivedAt
        };
        return (key, descending);
    }

    /// <summary>Applies the sort with received-at, then id, as the tie-breakers, so paging is
    /// stable when many rows share an outcome or a status.</summary>
    public static IOrderedQueryable<PaymentNotification> Sort(IQueryable<PaymentNotification> notifications, PaymentAlertSortKey key, bool descending)
    {
        if (key == PaymentAlertSortKey.ReceivedAt)
        {
            return descending
                ? notifications.OrderByDescending(n => n.ReceivedAt).ThenByDescending(n => n.Id)
                : notifications.OrderBy(n => n.ReceivedAt).ThenBy(n => n.Id);
        }

        // Outcome is stored as its name, so the database orders it alphabetically; ToString() here
        // keeps an in-memory sort on the same footing instead of the enum's declaration order.
        var ordered = key switch
        {
            PaymentAlertSortKey.Outcome => descending ? notifications.OrderByDescending(n => n.Outcome.ToString()) : notifications.OrderBy(n => n.Outcome.ToString()),
            PaymentAlertSortKey.Status => descending ? notifications.OrderByDescending(n => n.Status) : notifications.OrderBy(n => n.Status),
            _ => descending ? notifications.OrderByDescending(n => n.MerchantOid) : notifications.OrderBy(n => n.MerchantOid)
        };
        return ordered.ThenByDescending(n => n.ReceivedAt).ThenByDescending(n => n.Id);
    }
}
