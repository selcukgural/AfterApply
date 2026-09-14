using AfterApply.Application.JobSources;
using AfterApply.Application.JobSources.Contracts;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class JobFitScoresTests
{
    private static JobFitScoringResult Result(int score = 72, string summary = "You match the core stack.",
        IReadOnlyList<string>? matched = null, IReadOnlyList<string>? missing = null, IReadOnlyList<string>? skills = null) =>
        new(score, summary, matched ?? ["C#"], missing ?? ["Kubernetes"], skills ?? ["C#", "Kubernetes"], 1200, 300);

    [Fact]
    public void Null_Stays_Null_And_An_Empty_Summary_Makes_The_Answer_Unusable()
    {
        JobFitScores.Sanitize(null).ShouldBeNull();
        JobFitScores.Sanitize(Result(summary: "  \n ")).ShouldBeNull();
    }

    [Theory]
    [InlineData(-10, 0)]
    [InlineData(250, 100)]
    [InlineData(64, 64)]
    public void Score_Is_Clamped(int given, int expected) =>
        JobFitScores.Sanitize(Result(score: given))!.Score.ShouldBe(expected);

    [Fact]
    public void Control_Characters_Go_And_Whitespace_Collapses()
    {
        var result = JobFitScores.Sanitize(Result(summary: "You match\n\n  the   stack.", matched: ["C#\u001b[31m", " .NET  8 "]))!;

        result.Summary.ShouldBe("You match the stack.");
        result.MatchedCriteria.ShouldBe(["C#[31m", ".NET 8"]);
    }

    [Fact]
    public void Lists_Are_Deduplicated_Case_Insensitively_Capped_And_Emptied_Of_Blanks()
    {
        var items = Enumerable.Range(0, 20).Select(i => $"Skill {i}").Concat(["skill 1", "", "  "]).ToList();

        var result = JobFitScores.Sanitize(Result(skills: items, matched: ["Docker", "docker", "DOCKER"]))!;

        result.RequiredSkills.Count.ShouldBe(UserJobSourceDelivery.MaxCriteriaItems);
        result.MatchedCriteria.ShouldBe(["Docker"]);
    }

    [Fact]
    public void Long_Text_Is_Truncated_To_What_The_Row_Holds()
    {
        var result = JobFitScores.Sanitize(Result(summary: new string('a', 2000), matched: [new string('b', 500)]))!;

        result.Summary.Length.ShouldBe(UserJobSourceDelivery.MaxSummaryLength);
        result.MatchedCriteria[0].Length.ShouldBe(UserJobSourceDelivery.MaxCriterionLength);
    }

    [Fact]
    public void Tokens_Pass_Through_Untouched()
    {
        var result = JobFitScores.Sanitize(Result())!;
        (result.InputTokens, result.OutputTokens).ShouldBe((1200, 300));
    }

    [Fact]
    public void Cost_Is_Tokens_At_List_Price()
    {
        var settings = new JobFitScoringSettings { InputUsdPerMillionTokens = 0.30m, OutputUsdPerMillionTokens = 2.50m };

        // 2M input = $0.60, 400k output = $1.00
        JobFitScoringCost.Estimate(2_000_000, 400_000, settings).ShouldBe(1.60m);
        JobFitScoringCost.Estimate(0, 0, settings).ShouldBe(0m);
    }
}
