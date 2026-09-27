using AfterApply.Application.Applications;
using AfterApply.Domain.EmailIntegrations;
using Shouldly;

namespace AfterApply.UnitTests.Applications;

public class RejectionPatternsTests
{
    private const RejectionReasonCategory Experience = RejectionReasonCategory.ExperienceLevelMismatch;
    private const RejectionReasonCategory Location = RejectionReasonCategory.LocationOrRelocation;

    [Fact]
    public void Three_Of_The_Last_Five_Is_A_Pattern()
    {
        var pattern = RejectionPatterns.Find([Experience, Location, Experience, Location, Experience]);

        pattern.ShouldNotBeNull();
        pattern.Category.ShouldBe(Experience);
        pattern.Count.ShouldBe(3);
        pattern.OutOf.ShouldBe(5);
    }

    [Fact]
    public void Two_Is_Anecdote()
    {
        RejectionPatterns.Find([Experience, Location, Experience]).ShouldBeNull();
    }

    [Fact]
    public void Only_The_Newest_Five_Count()
    {
        // The three Experience reasons are older than the five newest.
        RejectionPatterns.Find([Location, Location, RejectionReasonCategory.SkillOrTechStackGap, RejectionReasonCategory.SkillOrTechStackGap,
            RejectionReasonCategory.PositionCancelledOrFilled, Experience, Experience, Experience]).ShouldBeNull();
    }

    [Fact]
    public void Not_Stated_And_Other_Neither_Count_Nor_Take_A_Slot()
    {
        var pattern = RejectionPatterns.Find([RejectionReasonCategory.NotStated, RejectionReasonCategory.Other, Experience,
            RejectionReasonCategory.NotStated, Experience, RejectionReasonCategory.Other, Experience]);

        pattern.ShouldNotBeNull();
        pattern.OutOf.ShouldBe(3);

        RejectionPatterns.Find([RejectionReasonCategory.Other, RejectionReasonCategory.Other, RejectionReasonCategory.Other]).ShouldBeNull();
    }

    [Fact]
    public void Nothing_Is_Nothing()
    {
        RejectionPatterns.Find([]).ShouldBeNull();
    }
}
