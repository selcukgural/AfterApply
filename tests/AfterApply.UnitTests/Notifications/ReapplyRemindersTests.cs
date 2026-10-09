using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Applications.Validators;
using AfterApply.Application.Notifications;
using Shouldly;

namespace AfterApply.UnitTests.Notifications;

/// <summary>The morning an "apply here again" reminder shows: N months on, 09:00 Istanbul, never on
/// a day the follow-ups keep clear of.</summary>
public class ReapplyRemindersTests
{
    private static readonly TimeSpan Istanbul = TimeSpan.FromHours(3);

    private static DateTimeOffset At(int y, int m, int d, int h) => new(y, m, d, h, 0, 0, Istanbul);

    [Fact]
    public void Lands_At_Nine_On_The_Same_Day_Months_Later()
    {
        // Friday 9 October 2026 + 6 months = Friday 9 April 2027.
        ReapplyReminders.DueAt(At(2026, 10, 9, 22), 6, turkishHolidays: true).ShouldBe(At(2027, 4, 9, 9));
    }

    [Fact]
    public void A_Weekend_Moves_To_Monday_Morning()
    {
        // + 3 months = Saturday 9 January 2027.
        ReapplyReminders.DueAt(At(2026, 10, 9, 10), 3, turkishHolidays: true).ShouldBe(At(2027, 1, 11, 9));
    }

    [Fact]
    public void A_Turkish_Holiday_Moves_On_Only_For_A_Company_That_Keeps_It()
    {
        // + 3 months = Friday 23 April 2027, National Sovereignty Day.
        ReapplyReminders.DueAt(At(2027, 1, 23, 10), 3, turkishHolidays: true).ShouldBe(At(2027, 4, 26, 9));
        ReapplyReminders.DueAt(At(2027, 1, 23, 10), 3, turkishHolidays: false).ShouldBe(At(2027, 4, 23, 9));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(6, true)]
    [InlineData(12, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(24, false)]
    [InlineData(-6, false)]
    public void Only_The_Offered_Lengths_Are_Accepted(int months, bool valid)
    {
        new SetReapplyReminderRequestValidator().Validate(new SetReapplyReminderRequest(months)).IsValid.ShouldBe(valid);
    }
}
