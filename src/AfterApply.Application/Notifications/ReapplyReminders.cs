namespace AfterApply.Application.Notifications;

/// <summary>
/// The "remind me to apply here again" choice on a rejected application (canvas "İnce dokunuşlar
/// — Paket 6", 4A): which lengths are offered, and the morning the reminder shows.
/// </summary>
public static class ReapplyReminders
{
    /// <summary>Three, six or twelve months — the waits companies commonly ask of a rejected
    /// candidate. Nothing else is accepted.</summary>
    public static readonly IReadOnlySet<int> AllowedMonths = new HashSet<int> { 3, 6, 12 };

    /// <summary>
    /// 09:00 Istanbul on the same day <paramref name="months"/> later, moved on to the next working
    /// morning when that day is a weekend or (for a company in Türkiye or of unknown country) a
    /// public holiday — the same days the follow-ups keep clear of (BusinessCalendar).
    /// </summary>
    public static DateTimeOffset DueAt(DateTimeOffset now, int months, bool turkishHolidays)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, BusinessCalendar.SiteTimeZone).DateTime);
        var local = today.AddMonths(months).ToDateTime(BusinessCalendar.ShowAt);
        var due = new DateTimeOffset(local, BusinessCalendar.SiteTimeZone.GetUtcOffset(local));
        return BusinessCalendar.DeferralFor(due, turkishHolidays)?.Until ?? due;
    }
}
