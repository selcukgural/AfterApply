using AfterApply.Domain.Matching;
using Shouldly;

namespace AfterApply.UnitTests.Domain;

public class JobMatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

    private static JobMatch CreateMatch(string cvText = "cv-v1", string jobDescription = "job-v1") =>
        JobMatch.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), cvText, jobDescription,
            score: 80, strongMatches: ["C#", ".NET"], missing: ["React"],
            recommendation: JobMatchRecommendation.Apply, now: Now);

    [Fact]
    public void Create_Sets_All_Fields()
    {
        var match = CreateMatch();

        match.Score.ShouldBe(80);
        match.StrongMatches.ShouldBe(["C#", ".NET"]);
        match.Missing.ShouldBe(["React"]);
        match.Recommendation.ShouldBe(JobMatchRecommendation.Apply);
        match.ComputedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("cv-v1", "job-v1", true)]
    [InlineData("cv-v2", "job-v1", false)]
    [InlineData("cv-v1", "job-v2", false)]
    public void MatchesInputs_Compares_Cv_And_JobDescription_Snapshots(string cvText, string jobDescription, bool expected)
    {
        var match = CreateMatch();

        match.MatchesInputs(cvText, jobDescription).ShouldBe(expected);
    }

    [Fact]
    public void Recompute_Overwrites_Previous_Result()
    {
        var match = CreateMatch();
        var later = Now.AddDays(1);

        match.Recompute("cv-v2", "job-v2", score: 40, strongMatches: ["Java"], missing: ["C#", ".NET"],
            recommendation: JobMatchRecommendation.Skip, now: later);

        match.CvTextSnapshot.ShouldBe("cv-v2");
        match.JobDescription.ShouldBe("job-v2");
        match.Score.ShouldBe(40);
        match.StrongMatches.ShouldBe(["Java"]);
        match.Missing.ShouldBe(["C#", ".NET"]);
        match.Recommendation.ShouldBe(JobMatchRecommendation.Skip);
        match.ComputedAt.ShouldBe(later);
        match.MatchesInputs("cv-v2", "job-v2").ShouldBeTrue();
    }
}
