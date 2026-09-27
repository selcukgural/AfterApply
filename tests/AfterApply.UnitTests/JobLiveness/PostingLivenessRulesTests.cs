using AfterApply.Application.JobLiveness;
using AfterApply.Domain.Jobs;
using Shouldly;

namespace AfterApply.UnitTests.JobLiveness;

/// <summary>
/// The closed signals measured on live and closed postings on 2026-09-27. The fixtures are trimmed
/// from those real responses, so a site changing its markup shows up here first.
/// </summary>
public class PostingLivenessRulesTests
{
    // linkedin.com/jobs/view/4438199390 — closed, still shown (trimmed).
    internal const string LinkedInClosedPage = """
        <p>See who PeopleCert has hired for this role</p> <!----> </div> </a>
        <figure class="closed-job closed-job__flavor topcard__flavor-row">
          <span class="closed-job__icon closed-job__icon--error-pebble lazy-load"></span>
          <figcaption class="closed-job__flavor--closed">No longer accepting applications</figcaption>
        </figure>
        """;

    // linkedin.com/jobs/view/4464874711 — open (trimmed).
    internal const string LinkedInOpenPage = """
        <figure class="num-applicants__figure topcard__flavor--metadata topcard__flavor--bullet">
          <span class="num-applicants__icon num-applicants__icon--notify-pebble lazy-load"></span>
          <figcaption class="num-applicants__caption">Be among the first 25 applicants</figcaption>
        </figure>
        """;

    // kariyer.net state blob — note passiveReason: present on open postings too, never a signal.
    internal static string KariyerNetPage(string closingDate) =>
        "<script>window.__NUXT__=(function(a,b){return {job:{versionId:g,closingDateNumeric:\"" + closingDate
        + "\",lastPublishDateNumeric:\"05.11.2025\",jobApplicationViewDayWithText:\"Şirket başvuruları 15+ gün önce inceledi.\","
        + "passiveReason:\"Bu ilan başvuruları artık kabul etmiyor.\",jobBenefits:[]}}}(1,2))</script>";

    private static readonly DateTimeOffset Now = new(2026, 9, 27, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_LinkedIn_Page_With_The_Closed_Figure_Is_Closed()
    {
        PostingLivenessRules.ReadLinkedInPage(LinkedInClosedPage).Kind.ShouldBe(PostingLivenessKind.Closed);
    }

    [Fact]
    public void A_LinkedIn_Page_Without_It_Is_Open()
    {
        PostingLivenessRules.ReadLinkedInPage(LinkedInOpenPage).Kind.ShouldBe(PostingLivenessKind.Open);
    }

    [Fact]
    public void The_LinkedIn_Closed_Class_Is_Matched_Not_Its_Words()
    {
        // The Turkish page says "Artık başvuru kabul etmiyor" around the same figure.
        PostingLivenessRules.ReadLinkedInPage("""<figure class="closed-job closed-job__flavor">Artık başvuru kabul etmiyor</figure>""")
            .Kind.ShouldBe(PostingLivenessKind.Closed);
        PostingLivenessRules.ReadLinkedInPage("<p>No longer accepting applications</p>")
            .Kind.ShouldBe(PostingLivenessKind.Open);
        PostingLivenessRules.ReadLinkedInPage("""<div class="closed-jobs-carousel">""")
            .Kind.ShouldBe(PostingLivenessKind.Open);
    }

    [Theory]
    [InlineData("https://de.linkedin.com/jobs/systemprogrammierer-stellen?trk=expired_jd_redirect", true)]
    [InlineData("https://nl.linkedin.com/jobs/ontwikkelaar-jobs?position=1&trk=expired_jd_redirect", true)]
    [InlineData("https://www.linkedin.com/jobs/view/senior-dev-at-acme-4438199390", false)]
    [InlineData("https://evil.example/jobs?trk=expired_jd_redirect", false)]
    [InlineData("http://www.linkedin.com/jobs?trk=expired_jd_redirect", false)]
    public void Only_LinkedIns_Own_Expired_Redirect_Counts(string location, bool expected)
    {
        PostingLivenessRules.IsLinkedInExpiredRedirect(new Uri(location)).ShouldBe(expected);
    }

    [Fact]
    public void A_KariyerNet_Closing_Date_In_The_Past_Closes_It_From_The_Next_Istanbul_Day()
    {
        var result = PostingLivenessRules.ReadKariyerNetPage(KariyerNetPage("04.12.2025"), Now);

        result.Kind.ShouldBe(PostingLivenessKind.Closed);
        result.ClosedOn.ShouldBe(new DateTimeOffset(2025, 12, 5, 0, 0, 0, TimeSpan.FromHours(3)));
    }

    [Fact]
    public void A_KariyerNet_Closing_Date_Ahead_Is_Open_With_Its_Announced_Close()
    {
        var result = PostingLivenessRules.ReadKariyerNetPage(KariyerNetPage("14.10.2026"), Now);

        result.Kind.ShouldBe(PostingLivenessKind.Open);
        result.ClosesOn.ShouldBe(new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.FromHours(3)));
    }

    [Fact]
    public void The_Closing_Day_Itself_Still_Takes_Applications()
    {
        // 27 Sep in Istanbul, 12:00 local.
        PostingLivenessRules.ReadKariyerNetPage(KariyerNetPage("27.09.2026"), Now).Kind.ShouldBe(PostingLivenessKind.Open);
    }

    [Fact]
    public void A_KariyerNet_Page_Without_A_Closing_Date_Is_Taken_As_Open()
    {
        PostingLivenessRules.ReadKariyerNetPage("<html>passiveReason:\"Bu ilan başvuruları artık kabul etmiyor.\"</html>", Now)
            .Kind.ShouldBe(PostingLivenessKind.Open);
    }

    [Theory]
    [InlineData("https://www.kariyer.net/is-ilanlari", true)]
    [InlineData("https://www.kariyer.net/is-ilanlari/", true)]
    [InlineData("https://www.kariyer.net/is-ilani/acme-yazilim-gelistirici-4557832", false)]
    [InlineData("https://www.kariyer.net/is-ilanlari?kw=backend", true)]
    [InlineData("https://other.example/is-ilanlari", false)]
    public void Only_The_General_Listing_Redirect_Means_Removed(string location, bool expected)
    {
        PostingLivenessRules.IsKariyerNetRemovedRedirect(new Uri(location)).ShouldBe(expected);
    }
}
