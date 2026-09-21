using AfterApply.Application.ResponseRates;
using AfterApply.Domain.Applications;
using AfterApply.Infrastructure.CompanyIntelligence;
using AfterApply.Infrastructure.ResponseRates;
using Shouldly;

namespace AfterApply.UnitTests.ResponseRates;

public class ResponseRateAggregatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();
    private static readonly Guid UserC = Guid.NewGuid();

    private static ResponseRateSample Sample(Guid user, int ageDays, ApplicationStatus status = ApplicationStatus.Applied,
        int? repliedAfterDays = null, bool interview = false, bool offer = false) =>
        new(Guid.NewGuid(), user, status, Now.AddDays(-ageDays),
            repliedAfterDays is null ? null : Now.AddDays(-ageDays).AddDays(repliedAfterDays.Value), interview, offer);

    [Fact]
    public void Rates_Are_Over_The_Mature_Cohort_But_The_Count_Is_The_Whole_Window()
    {
        // Two answered applications forty days old, one unanswered application three days old.
        // The young one has not had time to be answered: it counts toward the total (and so the
        // confidence ladder) but not toward any rate.
        var samples = new[]
        {
            Sample(UserA, 40, ApplicationStatus.Rejected, repliedAfterDays: 5),
            Sample(UserB, 40, ApplicationStatus.Interview, repliedAfterDays: 3, interview: true),
            Sample(UserC, 3)
        };

        var figures = ResponseRateAggregator.Compute(samples, Now, maturityDays: 30);

        figures.TotalApplications.ShouldBe(3);
        figures.MatureApplications.ShouldBe(2);
        figures.ResponseRate.ShouldBe(100.0);
        figures.GhostingRate.ShouldBe(0.0);
        figures.InterviewRate.ShouldBe(50.0);
        figures.ClosureRate.ShouldBe(50.0);
    }

    [Fact]
    public void A_Young_Unanswered_Application_Does_Not_Lower_The_Response_Rate()
    {
        var withYoung = new[] { Sample(UserA, 60, repliedAfterDays: 4), Sample(UserB, 2) };
        var without = new[] { Sample(UserA, 60, repliedAfterDays: 4) };

        ResponseRateAggregator.Compute(withYoung, Now, 30).ResponseRate
            .ShouldBe(ResponseRateAggregator.Compute(without, Now, 30).ResponseRate);
    }

    [Fact]
    public void A_Young_Answered_Application_Still_Counts_In_Reply_Time()
    {
        // A reply that happened is real data however young the application: two days to a
        // first reply on a five-day-old application goes into the median.
        var samples = new[]
        {
            Sample(UserA, 60, ApplicationStatus.Screening, repliedAfterDays: 10),
            Sample(UserB, 5, ApplicationStatus.Screening, repliedAfterDays: 2)
        };

        var figures = ResponseRateAggregator.Compute(samples, Now, 30);

        figures.MedianFirstReplyDays.ShouldBe(6.0);
        figures.AverageFirstReplyDays.ShouldBe(6.0);
        figures.MatureApplications.ShouldBe(1);
    }

    [Fact]
    public void Post_Interview_Silence_Is_The_Ghosted_Share_Of_Those_Who_Reached_An_Interview()
    {
        var samples = new[]
        {
            Sample(UserA, 90, ApplicationStatus.Ghosted, repliedAfterDays: 4, interview: true),
            Sample(UserB, 90, ApplicationStatus.Rejected, repliedAfterDays: 4, interview: true),
            Sample(UserC, 90, ApplicationStatus.Offer, repliedAfterDays: 4, interview: true, offer: true),
            // Ghosted without ever reaching an interview: in the ghosting rate, not in this one.
            Sample(UserA, 90, ApplicationStatus.Ghosted)
        };

        var figures = ResponseRateAggregator.Compute(samples, Now, 30);

        figures.PostInterviewSilenceRate.ShouldBe(33.3);
        figures.GhostingRate.ShouldBe(50.0);
        figures.OfferRate.ShouldBe(25.0);
    }

    [Fact]
    public void Post_Interview_Silence_Is_Null_When_Nobody_Reached_An_Interview()
    {
        var samples = new[] { Sample(UserA, 90, ApplicationStatus.Rejected, repliedAfterDays: 2) };

        ResponseRateAggregator.Compute(samples, Now, 30).PostInterviewSilenceRate.ShouldBeNull();
    }

    [Fact]
    public void Contributors_And_The_Largest_Share_Are_Counted_Per_Person()
    {
        var samples = new[]
        {
            Sample(UserA, 90), Sample(UserA, 90), Sample(UserA, 90),
            Sample(UserB, 90),
            Sample(UserC, 90)
        };

        var figures = ResponseRateAggregator.Compute(samples, Now, 30);

        figures.DistinctContributors.ShouldBe(3);
        figures.MaxContributorShare.ShouldBe(0.6);
    }

    [Fact]
    public void An_Empty_Window_Is_All_Zeroes_And_Nulls_Rather_Than_An_Exception()
    {
        var figures = ResponseRateAggregator.Compute([], Now, 30);

        figures.TotalApplications.ShouldBe(0);
        figures.DistinctContributors.ShouldBe(0);
        figures.MaxContributorShare.ShouldBe(0);
        figures.ResponseRate.ShouldBe(0);
        figures.MedianFirstReplyDays.ShouldBeNull();
        figures.PostInterviewSilenceRate.ShouldBeNull();
    }

    [Fact]
    public void ToSample_Takes_The_Earliest_Reply_And_Flags_The_Stages_Reached()
    {
        var appliedAt = Now.AddDays(-50);
        var history = new[]
        {
            (ApplicationStatus.Interview, appliedAt.AddDays(9)),
            (ApplicationStatus.Screening, appliedAt.AddDays(4)),
            (ApplicationStatus.Offer, appliedAt.AddDays(20)),
            (ApplicationStatus.Accepted, appliedAt.AddDays(25))
        };

        var sample = ResponseRateAggregator.ToSample(Guid.NewGuid(), UserA, ApplicationStatus.Accepted, appliedAt, history);

        sample.FirstRespondedAt.ShouldBe(appliedAt.AddDays(4));
        sample.ReachedInterview.ShouldBeTrue();
        sample.ReachedOffer.ShouldBeTrue();
    }

    [Fact]
    public void ToSample_With_No_History_Is_Unanswered()
    {
        var sample = ResponseRateAggregator.ToSample(Guid.NewGuid(), UserA, ApplicationStatus.Applied, Now.AddDays(-50), []);

        sample.FirstRespondedAt.ShouldBeNull();
        sample.ReachedInterview.ShouldBeFalse();
        sample.ReachedOffer.ShouldBeFalse();
    }

    [Fact]
    public void The_Shipped_Guards_Keep_One_Person_From_Being_A_Company_Or_A_Sector()
    {
        // Fairness settings, asserted like the confidence ladder is: a third of a company's
        // applications, half of a sector's, and thirty days before an application is judged.
        // Tightening any of these hides more and is safe; loosening them is a privacy decision.
        var company = new CompanyIntelligenceOptions();
        var sector = new ResponseRateOptions();

        company.MaturityDays.ShouldBeGreaterThanOrEqualTo(30);
        company.MaxContributorShare.ShouldBeLessThanOrEqualTo(1.0 / 3.0 + 0.001);
        sector.MinimumContributors.ShouldBeGreaterThanOrEqualTo(5);
        sector.MinimumApplications.ShouldBeGreaterThanOrEqualTo(30);
        sector.MaxContributorShare.ShouldBeLessThanOrEqualTo(0.5);
        sector.MaturityDays.ShouldBeGreaterThanOrEqualTo(30);
    }

    [Theory]
    [InlineData("LastThreeMonths", 3)]
    [InlineData("LastSixMonths", 6)]
    [InlineData("LastTwelveMonths", 12)]
    public void Every_Window_Has_A_Month_Count(string period, int months)
    {
        ResponseRatePeriods.MonthsOf(Enum.Parse<AfterApply.Domain.Benchmark.BenchmarkPeriod>(period)).ShouldBe(months);
    }

    [Fact]
    public void The_Open_Ended_Period_Has_No_Window()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ResponseRatePeriods.MonthsOf(AfterApply.Domain.Benchmark.BenchmarkPeriod.Longer));
    }
}
