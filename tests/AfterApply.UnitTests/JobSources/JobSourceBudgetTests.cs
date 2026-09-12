using AfterApply.Infrastructure.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class JobSourceBudgetTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-14T04:00:00Z");
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(24);

    [Fact]
    public void Spends_Until_The_Daily_Ceiling()
    {
        JobSourceBudget.CanSpend(199, 200, null, Cooldown, Now).ShouldBeTrue();
        JobSourceBudget.CanSpend(200, 200, null, Cooldown, Now).ShouldBeFalse();
    }

    [Fact]
    public void A_Block_Stops_Spending_For_The_Cooldown_Even_With_Budget_Left()
    {
        var blocked = Now.AddHours(-1);

        JobSourceBudget.CanSpend(0, 200, blocked, Cooldown, Now).ShouldBeFalse();
        JobSourceBudget.IsInCooldown(blocked, Cooldown, Now).ShouldBeTrue();
        JobSourceBudget.CooldownUntil(blocked, Cooldown).ShouldBe(Now.AddHours(23));
    }

    [Fact]
    public void An_Old_Block_No_Longer_Counts()
    {
        var blocked = Now.AddHours(-25);

        JobSourceBudget.CanSpend(0, 200, blocked, Cooldown, Now).ShouldBeTrue();
        JobSourceBudget.IsInCooldown(blocked, Cooldown, Now).ShouldBeFalse();
    }
}
