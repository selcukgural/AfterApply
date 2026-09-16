using System.Text.RegularExpressions;
using AfterApply.Domain.CompanyReviews;
using Shouldly;

namespace AfterApply.UnitTests.CompanyReviews;

/// <summary>The catalogue is data the whole feature hangs on: a duplicate or malformed key would
/// break the web's message lookup silently. These pin its shape.</summary>
public partial class ReviewStatementCatalogueTests
{
    [GeneratedRegex("^[a-z]+\\.(pos|imp)\\.[a-z0-9_]+$")]
    private static partial Regex KeyShape();

    [Fact]
    public void Every_Key_Is_Unique()
    {
        ReviewStatementCatalogue.All.Select(s => s.Key).ShouldBeUnique();
    }

    [Fact]
    public void Every_Key_Follows_The_Scheme_And_Describes_Its_Own_Category_And_Kind()
    {
        foreach (var statement in ReviewStatementCatalogue.All)
        {
            KeyShape().IsMatch(statement.Key).ShouldBeTrue(statement.Key);
            var segments = statement.Key.Split('.');
            segments[0].ShouldBe(ReviewStatementCatalogue.Prefix(statement.Category), statement.Key);
            segments[1].ShouldBe(statement.Kind == ReviewStatementKind.Liked ? "pos" : "imp", statement.Key);
            statement.Key.Length.ShouldBeLessThanOrEqualTo(CompanyReviewStatementPick.MaxKeyLength);
        }
    }

    [Fact]
    public void Every_Category_Offers_Both_Kinds()
    {
        foreach (var category in Enum.GetValues<ReviewCategory>())
        {
            ReviewStatementCatalogue.For(category, ReviewStatementKind.Liked).ShouldNotBeEmpty(category.ToString());
            ReviewStatementCatalogue.For(category, ReviewStatementKind.Improve).ShouldNotBeEmpty(category.ToString());
        }
    }

    [Fact]
    public void The_Source_Document_Is_Transcribed_In_Full()
    {
        // 103 liked + 103 improvable, from ekariyerim-sirket-degerlendirme-secenekleri.md with
        // "Eğitim ve Gelişim" folded into career growth. A different number means a line was lost.
        ReviewStatementCatalogue.All.Count(s => s.Kind == ReviewStatementKind.Liked).ShouldBe(103);
        ReviewStatementCatalogue.All.Count(s => s.Kind == ReviewStatementKind.Improve).ShouldBe(103);
    }

    [Fact]
    public void Prefixes_Are_Unique_Across_Categories()
    {
        Enum.GetValues<ReviewCategory>().Select(ReviewStatementCatalogue.Prefix).ShouldBeUnique();
    }

    [Fact]
    public void TryGet_Finds_Only_Exact_Keys()
    {
        ReviewStatementCatalogue.TryGet("environment.pos.team_communication", out var found).ShouldBeTrue();
        found.Category.ShouldBe(ReviewCategory.WorkEnvironment);
        found.Kind.ShouldBe(ReviewStatementKind.Liked);

        ReviewStatementCatalogue.TryGet("Environment.pos.team_communication", out _).ShouldBeFalse();
        ReviewStatementCatalogue.TryGet("environment.imp.team_communication ", out _).ShouldBeFalse();
    }
}
