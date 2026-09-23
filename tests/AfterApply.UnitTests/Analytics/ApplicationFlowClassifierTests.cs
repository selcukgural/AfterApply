using AfterApply.Application.Analytics;
using AfterApply.Application.Analytics.Contracts;
using AfterApply.Domain.Applications;
using Shouldly;

namespace AfterApply.UnitTests.Analytics;

public class ApplicationFlowClassifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    private const int UnansweredAfterDays = 30;

    private static ApplicationFlowItem Item(ApplicationStatus current, int daysAgo = 60, params ApplicationStatus[] history) =>
        new(current, history, Now.AddDays(-daysAgo));

    private static ApplicationFlowCounts Classify(params ApplicationFlowItem[] items) =>
        ApplicationFlowClassifier.Classify(items, Now, UnansweredAfterDays);

    [Fact]
    public void Empty_Input_Is_All_Zeros()
    {
        Classify().ShouldBe(new ApplicationFlowCounts(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void Applied_Past_The_Threshold_Is_Unanswered_And_Before_It_Is_Awaiting()
    {
        var counts = Classify(
            Item(ApplicationStatus.Applied, daysAgo: 30),
            Item(ApplicationStatus.Applied, daysAgo: 29));

        counts.Unanswered.ShouldBe(1);
        counts.AwaitingReply.ShouldBe(1);
    }

    [Fact]
    public void Ghosted_Without_An_Interview_Is_Unanswered_Even_After_A_Screening_Call()
    {
        var counts = Classify(
            Item(ApplicationStatus.Ghosted, daysAgo: 3),
            Item(ApplicationStatus.Ghosted, 60, ApplicationStatus.Applied, ApplicationStatus.Screening));

        counts.Unanswered.ShouldBe(2);
        counts.InScreening.ShouldBe(0);
    }

    [Fact]
    public void First_Column_Outcomes_Without_An_Interview()
    {
        var counts = Classify(
            Item(ApplicationStatus.Rejected, 60, ApplicationStatus.Applied),
            Item(ApplicationStatus.Rejected, 60, ApplicationStatus.Screening),
            Item(ApplicationStatus.Withdrawn),
            Item(ApplicationStatus.Screening, 60, ApplicationStatus.Applied));

        counts.RejectedBeforeInterview.ShouldBe(2);
        counts.WithdrawnBeforeInterview.ShouldBe(1);
        counts.InScreening.ShouldBe(1);
        counts.Interviewed.ShouldBe(0);
    }

    [Fact]
    public void Any_Interview_Status_In_History_Counts_As_Interviewed_Whatever_The_Current_Status()
    {
        var counts = Classify(
            Item(ApplicationStatus.Rejected, 60, ApplicationStatus.TechnicalInterview),
            Item(ApplicationStatus.Ghosted, 60, ApplicationStatus.Interview),
            Item(ApplicationStatus.Withdrawn, 60, ApplicationStatus.FinalInterview),
            Item(ApplicationStatus.Interview));

        counts.Interviewed.ShouldBe(4);
        counts.RejectedAfterInterview.ShouldBe(1);
        counts.SilentAfterInterview.ShouldBe(1);
        counts.WithdrawnAfterInterview.ShouldBe(1);
        counts.InterviewInProgress.ShouldBe(1);
        counts.RejectedBeforeInterview.ShouldBe(0);
        counts.Unanswered.ShouldBe(0);
    }

    [Fact]
    public void An_Offer_Counts_As_Offer_Even_When_Declined_Or_Reached_Without_An_Interview()
    {
        var counts = Classify(
            Item(ApplicationStatus.Accepted, 60, ApplicationStatus.Screening, ApplicationStatus.Offer),
            Item(ApplicationStatus.Withdrawn, 60, ApplicationStatus.Interview, ApplicationStatus.Offer),
            Item(ApplicationStatus.Offer));

        counts.Interviewed.ShouldBe(3);
        counts.Offer.ShouldBe(3);
        counts.WithdrawnAfterInterview.ShouldBe(0);
    }

    [Fact]
    public void Both_Columns_Always_Add_Up()
    {
        var items = Enum.GetValues<ApplicationStatus>()
            .SelectMany(current => Enum.GetValues<ApplicationStatus>()
                .Select(past => Item(current, daysAgo: (int)past * 7, ApplicationStatus.Applied, past)))
            .ToArray();

        var c = Classify(items);

        c.Total.ShouldBe(items.Length);
        (c.Unanswered + c.AwaitingReply + c.RejectedBeforeInterview + c.InScreening + c.WithdrawnBeforeInterview + c.Interviewed)
            .ShouldBe(c.Total);
        (c.Offer + c.InterviewInProgress + c.RejectedAfterInterview + c.SilentAfterInterview + c.WithdrawnAfterInterview)
            .ShouldBe(c.Interviewed);
    }

    [Theory]
    [InlineData("30", FlowPeriod.Last30Days)]
    [InlineData("90", FlowPeriod.Last90Days)]
    [InlineData("all", FlowPeriod.All)]
    [InlineData(" ALL ", FlowPeriod.All)]
    public void Period_Parses_The_Query_Spellings(string value, FlowPeriod expected)
    {
        FlowPeriods.TryParse(value, out var period).ShouldBeTrue();
        period.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("60")]
    [InlineData("Last30Days")]
    public void Period_Refuses_Anything_Else(string? value)
    {
        FlowPeriods.TryParse(value, out _).ShouldBeFalse();
    }

    [Fact]
    public void Period_Start_Counts_Back_From_Now()
    {
        FlowPeriods.StartOf(FlowPeriod.Last30Days, Now).ShouldBe(Now.AddDays(-30));
        FlowPeriods.StartOf(FlowPeriod.Last90Days, Now).ShouldBe(Now.AddDays(-90));
        FlowPeriods.StartOf(FlowPeriod.All, Now).ShouldBeNull();
    }
}
