using AfterApply.Application.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class WeekKeyTests
{
    [Theory]
    [InlineData("2026-09-14T04:00:00Z", 202638)] // Monday 04:00, the scheduled run
    [InlineData("2026-09-20T23:59:59Z", 202638)] // Sunday of the same ISO week
    [InlineData("2026-09-21T00:00:00Z", 202639)]
    [InlineData("2027-01-01T12:00:00Z", 202653)] // 1 Jan 2027 is a Friday: still ISO week 53 of 2026
    [InlineData("2024-12-30T12:00:00Z", 202501)] // ...and 30 Dec 2024 is already week 1 of 2025
    public void Follows_Iso_8601(string moment, int expected)
    {
        WeekKey.From(DateTimeOffset.Parse(moment)).ShouldBe(expected);
    }

    [Fact]
    public void Same_Week_In_Another_Offset_Is_The_Same_Key()
    {
        // The key is UTC-based, so a run at 04:00 UTC and a viewer at UTC+3 agree on the week.
        WeekKey.From(DateTimeOffset.Parse("2026-09-14T04:00:00+03:00")).ShouldBe(202638);
    }

    [Theory]
    [InlineData(202638, true)]
    [InlineData(202600, false)]
    [InlineData(202654, false)]
    [InlineData(20263, false)]
    public void Validates_The_Shape(int key, bool valid)
    {
        WeekKey.IsValid(key).ShouldBe(valid);
    }
}
