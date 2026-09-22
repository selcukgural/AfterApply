using AfterApply.Domain.Applications;
using Shouldly;

namespace AfterApply.UnitTests.Domain;

public class ReplyPromisesTests
{
    // The stage the promise was given in began here; "by the 10th" is the promise.
    private static readonly DateTimeOffset Since = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly PromisedBy = new(2026, 10, 10);

    private static DateTimeOffset On(int day, int hour = 12) => new(2026, 10, day, hour, 0, 0, TimeSpan.Zero);

    private static ReplyPromiseEvaluation Evaluate(DateTimeOffset now,
        params (ApplicationStatus ToStatus, DateTimeOffset ChangedAt)[] history) =>
        ReplyPromises.Evaluate(PromisedBy, Since, history, now);

    [Fact]
    public void Before_The_Date_With_Nothing_Happened_It_Is_Pending_And_Counts_Nowhere()
    {
        var result = Evaluate(On(8));

        result.Outcome.ShouldBe(ReplyPromiseOutcome.Pending);
        result.CountsAsKept.ShouldBeFalse();
        result.CountsAsBroken.ShouldBeFalse();
    }

    [Fact]
    public void On_The_Date_Itself_It_Is_Still_Pending()
    {
        Evaluate(On(10, 23)).Outcome.ShouldBe(ReplyPromiseOutcome.Pending);
    }

    [Fact]
    public void A_Move_On_Or_Before_The_Date_Keeps_It()
    {
        Evaluate(On(20), (ApplicationStatus.TechnicalInterview, On(9))).Outcome.ShouldBe(ReplyPromiseOutcome.Kept);
        Evaluate(On(20), (ApplicationStatus.Rejected, On(9))).CountsAsKept.ShouldBeTrue();
    }

    [Fact]
    public void The_Grace_Absorbs_A_Reply_Two_Days_Late()
    {
        Evaluate(On(20), (ApplicationStatus.Offer, On(12, 23))).Outcome.ShouldBe(ReplyPromiseOutcome.Kept);
    }

    [Fact]
    public void A_Reply_After_The_Grace_Is_Late_And_Counts_As_Broken()
    {
        var result = Evaluate(On(20), (ApplicationStatus.Rejected, On(13)));

        result.Outcome.ShouldBe(ReplyPromiseOutcome.Late);
        result.CountsAsBroken.ShouldBeTrue();
        result.CountsAsKept.ShouldBeFalse();
    }

    [Fact]
    public void Past_The_Date_With_No_Move_It_Is_Overdue_But_Only_Broken_Once_The_Grace_Is_Over()
    {
        var insideGrace = Evaluate(On(12));
        insideGrace.Outcome.ShouldBe(ReplyPromiseOutcome.Overdue);
        insideGrace.CountsAsBroken.ShouldBeFalse();

        var pastGrace = Evaluate(On(13));
        pastGrace.Outcome.ShouldBe(ReplyPromiseOutcome.Overdue);
        pastGrace.CountsAsBroken.ShouldBeTrue();
    }

    [Fact]
    public void Transitions_At_Or_Before_The_Stage_Start_Are_Not_An_Answer()
    {
        // The row that opened the stage (and anything before it) happened before the promise.
        var result = Evaluate(On(15),
            (ApplicationStatus.Applied, Since.AddDays(-20)),
            (ApplicationStatus.Interview, Since));

        result.Outcome.ShouldBe(ReplyPromiseOutcome.Overdue);
    }

    [Fact]
    public void Marking_It_Ghosted_Is_Not_The_Company_Answering()
    {
        var result = Evaluate(On(20), (ApplicationStatus.Ghosted, On(11)));

        result.Outcome.ShouldBe(ReplyPromiseOutcome.Overdue);
        result.CountsAsBroken.ShouldBeTrue();
    }

    [Fact]
    public void The_First_Answer_Decides_Even_When_History_Arrives_Out_Of_Order()
    {
        var result = Evaluate(On(30),
            (ApplicationStatus.Offer, On(25)),
            (ApplicationStatus.FinalInterview, On(9)));

        result.Outcome.ShouldBe(ReplyPromiseOutcome.Kept);
    }

    [Fact]
    public void Withdrawing_Before_The_Deadline_Voids_It()
    {
        var result = Evaluate(On(30), (ApplicationStatus.Withdrawn, On(5)));

        result.Outcome.ShouldBe(ReplyPromiseOutcome.Void);
        result.CountsAsKept.ShouldBeFalse();
        result.CountsAsBroken.ShouldBeFalse();
    }

    [Fact]
    public void Withdrawing_After_The_Deadline_Does_Not_Excuse_The_Silence_Before_It()
    {
        var result = Evaluate(On(30), (ApplicationStatus.Withdrawn, On(20)));

        result.Outcome.ShouldBe(ReplyPromiseOutcome.Overdue);
        result.CountsAsBroken.ShouldBeTrue();
    }
}
