using System.Text.RegularExpressions;
using AfterApply.Domain.CandidateExperiences;
using AfterApply.Domain.CompanyReviews;
using Shouldly;

namespace AfterApply.UnitTests.CandidateExperiences;

/// <summary>The catalogue is data the whole feature hangs on: a duplicate or malformed key would
/// break the web's message lookup silently. These pin its shape.</summary>
public partial class ExperienceStatementCatalogueTests
{
    [GeneratedRegex("^[a-z]+\\.(pos|imp)\\.[a-z0-9_]+$")]
    private static partial Regex KeyShape();

    [Fact]
    public void Every_Key_Is_Unique()
    {
        ExperienceStatementCatalogue.All.Select(s => s.Key).ShouldBeUnique();
    }

    [Fact]
    public void Every_Key_Follows_The_Scheme_And_Describes_Its_Own_Category_And_Kind()
    {
        foreach (var statement in ExperienceStatementCatalogue.All)
        {
            KeyShape().IsMatch(statement.Key).ShouldBeTrue(statement.Key);
            var segments = statement.Key.Split('.');
            segments[0].ShouldBe(ExperienceStatementCatalogue.Prefix(statement.Category), statement.Key);
            segments[1].ShouldBe(statement.Kind == ReviewStatementKind.Liked ? "pos" : "imp", statement.Key);
            statement.Key.Length.ShouldBeLessThanOrEqualTo(CandidateExperienceStatementPick.MaxKeyLength);
        }
    }

    [Fact]
    public void Every_Category_Offers_Both_Kinds()
    {
        foreach (var category in Enum.GetValues<ExperienceCategory>())
        {
            ExperienceStatementCatalogue.For(category, ReviewStatementKind.Liked).ShouldNotBeEmpty(category.ToString());
            ExperienceStatementCatalogue.For(category, ReviewStatementKind.Improve).ShouldNotBeEmpty(category.ToString());
        }
    }

    [Fact]
    public void Prefixes_Are_Unique_Per_Category()
    {
        Enum.GetValues<ExperienceCategory>().Select(ExperienceStatementCatalogue.Prefix).ShouldBeUnique();
    }

    [Fact]
    public void No_Key_Collides_With_The_Review_Catalogue()
    {
        // The two vocabularies live in one message catalogue under different namespaces, but a
        // shared key would still be a trap for anyone grepping; keep the prefixes disjoint too.
        var reviewKeys = ReviewStatementCatalogue.All.Select(s => s.Key).ToHashSet(StringComparer.Ordinal);
        ExperienceStatementCatalogue.All.ShouldAllBe(s => !reviewKeys.Contains(s.Key));

        var reviewPrefixes = Enum.GetValues<ReviewCategory>().Select(ReviewStatementCatalogue.Prefix)
            .Where(p => p != "general").ToHashSet(StringComparer.Ordinal);
        Enum.GetValues<ExperienceCategory>().Where(c => c != ExperienceCategory.Overall)
            .ShouldAllBe(c => !reviewPrefixes.Contains(ExperienceStatementCatalogue.Prefix(c)));
    }

    [Fact]
    public void TryGet_Finds_A_Known_Key_And_Not_An_Unknown_One()
    {
        ExperienceStatementCatalogue.TryGet("outcome.imp.notification", out var statement).ShouldBeTrue();
        statement.Category.ShouldBe(ExperienceCategory.OutcomeCommunication);
        statement.Kind.ShouldBe(ReviewStatementKind.Improve);
        ExperienceStatementCatalogue.TryGet("outcome.imp.nope", out _).ShouldBeFalse();
    }

    [Fact]
    public void Ordinal_Enums_Declare_Their_Values_In_Time_Order()
    {
        // The company page's "typical" figures are ordinal medians over declaration order.
        Enum.GetValues<ProcessDuration>().ShouldBe([ProcessDuration.UnderOneWeek, ProcessDuration.OneToTwoWeeks,
            ProcessDuration.TwoToFourWeeks, ProcessDuration.OneToTwoMonths, ProcessDuration.OverTwoMonths]);
        Enum.GetValues<StageCount>().ShouldBe([StageCount.One, StageCount.Two, StageCount.Three, StageCount.Four, StageCount.FivePlus]);
    }
}
