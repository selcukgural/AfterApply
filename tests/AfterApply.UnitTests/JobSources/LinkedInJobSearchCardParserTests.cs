using AfterApply.Application.JobSources;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

public class LinkedInJobSearchCardParserTests
{
    [Fact]
    public void Reads_Every_Card_With_Its_Fields()
    {
        var cards = LinkedInJobSearchCardParser.Parse(LinkedInJobSourceFixtures.ThreeCards);

        cards.Count.ShouldBe(3);
        var first = cards[0];
        first.ExternalId.ShouldBe("4460989029");
        first.Title.ShouldBe("Software Developer - .NET");
        first.CompanyName.ShouldBe("Acme Bankacılık Yazılımları");
        first.CompanyProfileUrl.ShouldBe("https://tr.linkedin.com/company/acme-bankacilik");
        first.Location.ShouldBe("Türkiye");
        first.PostedAt.ShouldBe(new DateOnly(2026, 9, 10));
        first.Url.ShouldBe("https://www.linkedin.com/jobs/view/4460989029");
    }

    [Fact]
    public void Decodes_Entities_And_Collapses_Whitespace()
    {
        var cards = LinkedInJobSearchCardParser.Parse(LinkedInJobSourceFixtures.ThreeCards);

        cards[2].CompanyName.ShouldBe("Path & Co");
        cards[2].Location.ShouldBe("Ataşehir, Istanbul, Türkiye");
        cards[1].Title.ShouldBe("Junior–Mid Level C#/.NET Geliştirici");
    }

    [Fact]
    public void Skips_A_Card_Without_An_Id_Or_A_Title()
    {
        var noId = LinkedInJobSourceFixtures.Card("4460989029", "T", "C", "c", "L", "2026-09-10")
            .Replace("data-entity-urn=\"urn:li:jobPosting:4460989029\"", string.Empty);
        var noTitle = LinkedInJobSourceFixtures.Card("1", "", "C", "c", "L", "2026-09-10");
        var good = LinkedInJobSourceFixtures.Card("2", "Kept", "C", "c", "L", "2026-09-10");

        var cards = LinkedInJobSearchCardParser.Parse(LinkedInJobSourceFixtures.SearchPage(noId, noTitle, good));

        cards.Select(c => c.ExternalId).ShouldBe(["2"]);
    }

    [Fact]
    public void Empty_Page_Is_No_Cards()
    {
        // What start >= 100 answers: a bare, empty list.
        LinkedInJobSearchCardParser.Parse("\n\n<ul>\n</ul>\n").ShouldBeEmpty();
        LinkedInJobSearchCardParser.Parse(string.Empty).ShouldBeEmpty();
    }

    [Fact]
    public void Tolerates_A_Missing_Date()
    {
        var card = LinkedInJobSourceFixtures.Card("3", "Title", "Co", "co", "Ankara", "2026-09-10")
            .Replace("datetime=\"2026-09-10\"", "datetime=\"soon\"");
        var cards = LinkedInJobSearchCardParser.Parse(card);

        cards.Single().PostedAt.ShouldBeNull();
        cards.Single().Location.ShouldBe("Ankara");
    }

    [Fact]
    public void Never_Emits_Markup_From_The_Page()
    {
        var hostile = LinkedInJobSourceFixtures.Card("9", "<script>alert(1)</script>Dev", "<b>Evil</b> Corp", "evil", "<i>X</i>", "2026-09-10");
        var card = LinkedInJobSearchCardParser.Parse(hostile).Single();

        card.Title.ShouldBe("alert(1) Dev");
        card.CompanyName.ShouldBe("Evil Corp");
        card.Location.ShouldBe("X");
    }
}
