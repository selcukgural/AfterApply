using AfterApply.Application.Notifications;
using AfterApply.Domain.Applications;
using Shouldly;

namespace AfterApply.UnitTests.Notifications;

public class ReminderCalculationsTests
{
    private static readonly DateTimeOffset AppliedAt = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GetReferenceAt_With_Only_Seed_Row_Returns_AppliedAt()
    {
        (ApplicationStatus? FromStatus, DateTimeOffset ChangedAt)[] history =
        [
            (null, AppliedAt)
        ];

        ReminderCalculations.GetReferenceAt(AppliedAt, history).ShouldBe(AppliedAt);
    }

    [Fact]
    public void GetReferenceAt_With_Real_Transitions_Returns_Latest_ChangedAt_Ignoring_Seed()
    {
        var firstTransition = AppliedAt.AddDays(3);
        var latestTransition = AppliedAt.AddDays(10);

        (ApplicationStatus? FromStatus, DateTimeOffset ChangedAt)[] history =
        [
            (null, AppliedAt),
            (ApplicationStatus.Applied, firstTransition),
            (ApplicationStatus.Screening, latestTransition)
        ];

        ReminderCalculations.GetReferenceAt(AppliedAt, history).ShouldBe(latestTransition);
    }

    [Fact]
    public void GetReferenceAt_Ignores_Transitions_Older_Than_The_Latest()
    {
        var older = AppliedAt.AddDays(5);
        var newest = AppliedAt.AddDays(20);

        (ApplicationStatus? FromStatus, DateTimeOffset ChangedAt)[] history =
        [
            (null, AppliedAt),
            (ApplicationStatus.Screening, newest),
            (ApplicationStatus.Applied, older)
        ];

        ReminderCalculations.GetReferenceAt(AppliedAt, history).ShouldBe(newest);
    }

    [Fact]
    public void DaysElapsed_Computes_Whole_Days()
    {
        var referenceAt = AppliedAt;
        var now = AppliedAt.AddDays(7).AddHours(5);

        ReminderCalculations.DaysElapsed(referenceAt, now).ShouldBe(7);
    }

    [Fact]
    public void DaysElapsed_With_Same_Instant_Returns_Zero()
    {
        ReminderCalculations.DaysElapsed(AppliedAt, AppliedAt).ShouldBe(0);
    }

    [Theory]
    [InlineData(6, 7, false)]
    [InlineData(7, 7, true)]
    [InlineData(8, 7, true)]
    public void IsFollowUpDue_Boundary_At_Threshold(int daysElapsed, int thresholdDays, bool expected)
    {
        ReminderCalculations.IsFollowUpDue(daysElapsed, thresholdDays).ShouldBe(expected);
    }

    [Theory]
    [InlineData(29, 30, false)]
    [InlineData(30, 30, true)]
    [InlineData(31, 30, true)]
    public void IsPossiblyGhosted_Boundary_At_Threshold(int daysElapsed, int thresholdDays, bool expected)
    {
        ReminderCalculations.IsPossiblyGhosted(hasResponded: false, daysElapsed, thresholdDays).ShouldBe(expected);
    }

    [Fact]
    public void IsPossiblyGhosted_Returns_False_When_Application_Has_Responded_Regardless_Of_Days()
    {
        ReminderCalculations.IsPossiblyGhosted(hasResponded: true, daysElapsed: 365, ghostingThresholdDays: 30)
            .ShouldBeFalse();
    }
}
