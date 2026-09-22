using AfterApply.Application.AtsSources;
using AfterApply.Domain.Common;
using Shouldly;

namespace AfterApply.UnitTests.AtsSources;

/// <summary>
/// The fixtures are trimmed copies of real responses captured on 2026-09-22, not invented shapes —
/// four of the five publish no schema, so a made-up fixture would only prove the parser matches
/// itself.
/// </summary>
public class AtsJobPostingParserTests
{
    private const string Uuid = "8f2b1c34-1a2b-4c3d-9e8f-0a1b2c3d4e5f";

    [Fact]
    public void Greenhouse_Decodes_The_Doubly_Escaped_Content()
    {
        const string json = """
            {"id":"4512345","title":"Abuse Investigator","location":{"name":"Seattle, San Francisco"},
             "first_published":"2026-09-09T10:50:29-04:00","updated_at":"2026-09-10T13:11:58-04:00",
             "content":"&lt;p&gt;Who we are&lt;/p&gt;&lt;ul&gt;&lt;li&gt;Payments&lt;/li&gt;&lt;/ul&gt;"}
            """;

        var posting = AtsJobPostingParser.Parse(Source.Greenhouse, json, "stripe/4512345");

        posting.ShouldNotBeNull();
        posting.Title.ShouldBe("Abuse Investigator");
        posting.Location.ShouldBe("Seattle, San Francisco");
        // Greenhouse escapes its own markup, so the raw value is entity-encoded HTML.
        posting.DescriptionHtml.ShouldBe("<p>Who we are</p><ul><li>Payments</li></ul>");
        posting.Description.ShouldBe("Who we are\nPayments");
        posting.PublishedAt!.Value.UtcDateTime.ShouldBe(new DateTime(2026, 9, 9, 14, 50, 29, DateTimeKind.Utc));
    }

    [Fact]
    public void Lever_Reads_The_Plain_Description_And_The_Epoch_Timestamp()
    {
        const string json = """
            {"text":"Staff Engineer","description":"<div>Build <b>things</b>.</div>",
             "descriptionPlain":"Build things.","createdAt":1565990241800,
             "categories":{"location":"Baltimore, MD","commitment":"Full-time"},"workplaceType":"remote"}
            """;

        var posting = AtsJobPostingParser.Parse(Source.Lever, json, $"acme/{Uuid}");

        posting.ShouldNotBeNull();
        posting.Title.ShouldBe("Staff Engineer");
        posting.Description.ShouldBe("Build things.");
        posting.Location.ShouldBe("Baltimore, MD");
        posting.EmploymentType.ShouldBe(EmploymentType.FullTime);
        posting.PublishedAt.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1565990241800));
    }

    [Fact]
    public void Ashby_Picks_The_One_Posting_Out_Of_The_Whole_Board()
    {
        var json = $$"""
            {"jobs":[
              {"id":"11111111-1111-1111-1111-111111111111","title":"Other Role","location":"Remote"},
              {"id":"{{Uuid}}","title":"Engineering Manager - EU","location":"Remote - European Union",
               "descriptionHtml":"<p>Lead the team.</p>","descriptionPlain":"Lead the team.",
               "employmentType":"FullTime","publishedAt":"2026-09-01T00:00:00Z"}]}
            """;

        var posting = AtsJobPostingParser.Parse(Source.Ashby, json, $"acme/{Uuid}");

        posting.ShouldNotBeNull();
        posting.Title.ShouldBe("Engineering Manager - EU");
        posting.Location.ShouldBe("Remote - European Union");
        posting.EmploymentType.ShouldBe(EmploymentType.FullTime);
    }

    [Fact]
    public void Workable_Picks_The_Row_Out_Of_The_Board_And_Builds_A_Location_A_Person_Would_Write()
    {
        // Shapes taken from a live board (blueground, 2026-09-22): the row carries city, state and
        // country separately and no single location string.
        const string json = """
            {"name":"acme","jobs":[
              {"shortcode":"9999999999","title":"Other Role","city":"Berlin","country":"Germany"},
              {"shortcode":"186545F8C1","title":"Client Experience Coordinator",
               "description":"<h3>About</h3><p>Join <strong>us</strong>.</p>",
               "city":"Athens","state":"Attica","country":"Greece",
               "employment_type":"Full-time","published_on":"2026-02-12","created_at":"2025-09-30"}]}
            """;

        var posting = AtsJobPostingParser.Parse(Source.Workable, json, "acme/186545F8C1");

        posting.ShouldNotBeNull();
        posting.Title.ShouldBe("Client Experience Coordinator");
        posting.DescriptionHtml.ShouldBe("<h3>About</h3><p>Join <strong>us</strong>.</p>");
        posting.Description.ShouldContain("Join us.");
        // Not "Athens, Attica, Greece": the region says nothing the city does not.
        posting.Location.ShouldBe("Athens, Greece");
        posting.EmploymentType.ShouldBe(EmploymentType.FullTime);
        posting.PublishedAt!.Value.ToString("yyyy-MM-dd").ShouldBe("2026-02-12");
    }

    [Fact]
    public void Workable_Falls_Back_To_The_Region_Only_When_There_Is_No_City()
    {
        const string json = """
            {"jobs":[{"shortcode":"A1B2C3D4E5","title":"Remote Role","state":"Attica","country":"Greece"}]}
            """;

        AtsJobPostingParser.Parse(Source.Workable, json, "acme/A1B2C3D4E5")!.Location.ShouldBe("Attica, Greece");
    }

    [Fact]
    public void A_Workable_Board_That_No_Longer_Lists_The_Posting_Yields_Nothing()
    {
        const string json = """{"jobs":[{"shortcode":"9999999999","title":"Other Role"}]}""";

        AtsJobPostingParser.Parse(Source.Workable, json, "acme/A1B2C3D4E5").ShouldBeNull();
    }

    [Fact]
    public void An_Ashby_Board_That_No_Longer_Lists_The_Posting_Yields_Nothing()
    {
        // A filled or unpublished job. Returning nothing is right: we do not want a closed
        // posting's fields rewritten from whichever row happened to be first.
        const string json = """{"jobs":[{"id":"11111111-1111-1111-1111-111111111111","title":"Other"}]}""";

        AtsJobPostingParser.Parse(Source.Ashby, json, $"acme/{Uuid}").ShouldBeNull();
    }

    [Fact]
    public void SmartRecruiters_Joins_The_Job_Ad_Sections()
    {
        const string json = """
            {"name":"Manufacturing Engineer","location":{"fullLocation":"Charleston, SC, United States"},
             "releasedDate":"2026-09-22T13:02:58.799Z","typeOfEmployment":{"id":"permanent","label":"Full-time"},
             "jobAd":{"sections":{"companyDescription":{"text":"<p>About Bosch.</p>"},
                                  "jobDescription":{"text":"<p>Build engines.</p>"},
                                  "qualifications":{"text":"<p>Five years.</p>"}}}}
            """;

        var posting = AtsJobPostingParser.Parse(Source.SmartRecruiters, json, "Acme/743999123456789");

        posting.ShouldNotBeNull();
        posting.Title.ShouldBe("Manufacturing Engineer");
        posting.Location.ShouldBe("Charleston, SC, United States");
        posting.EmploymentType.ShouldBe(EmploymentType.FullTime);
        posting.Description.ShouldBe("About Bosch.\n\nBuild engines.\n\nFive years.");
    }

    [Fact]
    public void Workday_Reads_The_Nested_Posting_Info()
    {
        const string json = """
            {"jobPostingInfo":{"title":"Senior Engineering Vendor Manager","location":"China, Shenzhen",
             "startDate":"2026-09-22","timeType":"Full time","jobReqId":"JR2026166",
             "jobDescription":"<p>NVIDIA&#39;s NPI team is expanding.</p>"}}
            """;

        var posting = AtsJobPostingParser.Parse(Source.Workday, json, "nvidia/JR2026166");

        posting.ShouldNotBeNull();
        posting.Title.ShouldBe("Senior Engineering Vendor Manager");
        posting.Location.ShouldBe("China, Shenzhen");
        posting.EmploymentType.ShouldBe(EmploymentType.FullTime);
        // The entity is decoded on the way to plain text; the HTML is kept as served.
        posting.Description.ShouldBe("NVIDIA's NPI team is expanding.");
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("[]")]
    // The shapes a renamed or retyped field produces — these APIs change without warning.
    [InlineData("""{"title":42,"content":null,"location":"a string, not an object"}""")]
    public void A_Response_That_Stopped_Making_Sense_Yields_Nothing_Rather_Than_Throwing(string json)
    {
        foreach (var source in new[] { Source.Greenhouse, Source.Lever, Source.Ashby, Source.SmartRecruiters, Source.Workday })
        {
            var posting = AtsJobPostingParser.Parse(source, json, "acme/1");

            // Either nothing at all, or a posting with nothing in it — never an exception, and
            // never a field invented out of the wrong type.
            (posting is null || posting == new AtsJobPosting()).ShouldBeTrue($"{source} on {json}");
        }
    }

    [Theory]
    [InlineData("Full-time", EmploymentType.FullTime)]
    [InlineData("Full time", EmploymentType.FullTime)]
    [InlineData("FullTime", EmploymentType.FullTime)]
    [InlineData("permanent", EmploymentType.FullTime)]
    [InlineData("Part-time", EmploymentType.PartTime)]
    [InlineData("Contract", EmploymentType.Contract)]
    [InlineData("Internship", EmploymentType.Internship)]
    [InlineData("Temporary", EmploymentType.Temporary)]
    // Anything unrecognised stays null: a wrong employment type on someone's own application
    // record is worse than a blank one.
    [InlineData("Flexible", null)]
    [InlineData("", null)]
    public void Employment_Type_Is_Mapped_Or_Left_Alone(string value, EmploymentType? expected)
    {
        var json = $$"""{"text":"x","categories":{"commitment":"{{value}}"} }""";

        AtsJobPostingParser.Parse(Source.Lever, json, "acme/1")!.EmploymentType.ShouldBe(expected);
    }
}
