using AfterApply.Domain.Common;
using AfterApply.Domain.Jobs;
using Shouldly;

namespace AfterApply.UnitTests.JobLiveness;

/// <summary>How one observation moves a posting: a close is final, a bare "gone" needs a second look.</summary>
public class JobApplyLivenessTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 3, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Confirm = TimeSpan.FromHours(36);

    private static Job NewJob() => Job.Create(Guid.NewGuid(), "Backend Developer", Source.LinkedIn, T0.AddDays(-10), externalId: "4438199390");

    [Fact]
    public void An_Open_Posting_Records_The_Check_And_Its_Announced_Close()
    {
        var job = NewJob();
        var closes = T0.AddDays(10);

        job.ApplyLiveness(PostingLiveness.Open(closes), T0, Confirm).ShouldBeFalse();

        job.ClosedAt.ShouldBeNull();
        job.LivenessCheckedAt.ShouldBe(T0);
        job.ClosesAt.ShouldBe(closes);
    }

    [Fact]
    public void An_Explicit_Close_Closes_At_Once()
    {
        var job = NewJob();

        job.ApplyLiveness(PostingLiveness.Closed(), T0, Confirm).ShouldBeTrue();

        job.ClosedAt.ShouldBe(T0);
    }

    [Fact]
    public void A_Close_The_Site_Dates_Keeps_That_Date_Unless_It_Is_In_The_Future()
    {
        var dated = NewJob();
        dated.ApplyLiveness(PostingLiveness.Closed(T0.AddDays(-4)), T0, Confirm);
        dated.ClosedAt.ShouldBe(T0.AddDays(-4));

        var future = NewJob();
        future.ApplyLiveness(PostingLiveness.Closed(T0.AddDays(2)), T0, Confirm);
        future.ClosedAt.ShouldBe(T0);
    }

    [Fact]
    public void A_Single_Gone_Only_Raises_Suspicion()
    {
        var job = NewJob();

        job.ApplyLiveness(PostingLiveness.Gone, T0, Confirm).ShouldBeFalse();

        job.ClosedAt.ShouldBeNull();
        job.LivenessSuspectedAt.ShouldBe(T0);
    }

    [Fact]
    public void A_Second_Gone_Too_Soon_Does_Not_Close()
    {
        var job = NewJob();
        job.ApplyLiveness(PostingLiveness.Gone, T0, Confirm);

        job.ApplyLiveness(PostingLiveness.Gone, T0.AddHours(12), Confirm).ShouldBeFalse();

        job.ClosedAt.ShouldBeNull();
    }

    [Fact]
    public void A_Second_Gone_After_The_Gap_Closes_Dated_From_The_First()
    {
        var job = NewJob();
        job.ApplyLiveness(PostingLiveness.Gone, T0, Confirm);

        job.ApplyLiveness(PostingLiveness.Gone, T0.AddHours(40), Confirm).ShouldBeTrue();

        job.ClosedAt.ShouldBe(T0);
        job.LivenessSuspectedAt.ShouldBeNull();
    }

    [Fact]
    public void Seeing_It_Up_Again_Clears_The_Suspicion()
    {
        var job = NewJob();
        job.ApplyLiveness(PostingLiveness.Gone, T0, Confirm);
        job.ApplyLiveness(PostingLiveness.Open(), T0.AddHours(20), Confirm);

        job.ApplyLiveness(PostingLiveness.Gone, T0.AddHours(40), Confirm).ShouldBeFalse();

        job.ClosedAt.ShouldBeNull();
        job.LivenessSuspectedAt.ShouldBe(T0.AddHours(40));
    }

    [Fact]
    public void An_Unknown_Answer_Never_Closes_Anything()
    {
        var job = NewJob();
        job.ApplyLiveness(PostingLiveness.Gone, T0, Confirm);

        job.ApplyLiveness(PostingLiveness.Unknown, T0.AddDays(3), Confirm).ShouldBeFalse();

        job.ClosedAt.ShouldBeNull();
        job.LivenessSuspectedAt.ShouldBe(T0);
    }

    [Fact]
    public void A_Closed_Posting_Is_Final()
    {
        var job = NewJob();
        job.ApplyLiveness(PostingLiveness.Closed(), T0, Confirm);

        job.ApplyLiveness(PostingLiveness.Open(), T0.AddDays(5), Confirm).ShouldBeFalse();

        job.ClosedAt.ShouldBe(T0);
        job.LivenessCheckedAt.ShouldBe(T0);
    }
}
