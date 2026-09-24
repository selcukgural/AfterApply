using AfterApply.Api.Telemetry;
using Sentry;
using Shouldly;

namespace AfterApply.UnitTests.Telemetry;

public class SentryScrubbingTests
{
    [Theory]
    [InlineData("https://api.example/hubs/import-progress?id=abc&access_token=eyJ.x.y",
        "https://api.example/hubs/import-progress?id=abc&access_token=[Filtered]")]
    [InlineData("?Token=abc&page=2", "?Token=[Filtered]&page=2")]
    [InlineData("code=1&state=2&email=a%40b.com", "code=[Filtered]&state=[Filtered]&email=[Filtered]")]
    [InlineData("/cb#access_token=x&token_type=bearer", "/cb#access_token=[Filtered]&token_type=bearer")]
    [InlineData("/api/applications?search=code", "/api/applications?search=code")]
    [InlineData("/api/x?zipcode=34000", "/api/x?zipcode=34000")]
    public void Only_Sensitive_Parameter_Values_Are_Replaced(string input, string expected)
    {
        SentryScrubbing.ScrubUrl(input).ShouldBe(expected);
    }

    [Fact]
    public void An_Event_Loses_The_Ticket_From_Its_Url_Query_And_Referer()
    {
        var sentryEvent = new SentryEvent();
        sentryEvent.Request.Url = "https://api.example/hubs/import-progress?access_token=t";
        sentryEvent.Request.QueryString = "?access_token=t";
        sentryEvent.Request.Headers["Referer"] = "https://web.example/tr/reset-password?token=r";

        SentryScrubbing.Scrub(sentryEvent);

        sentryEvent.Request.Url.ShouldBe("https://api.example/hubs/import-progress?access_token=[Filtered]");
        sentryEvent.Request.QueryString.ShouldBe("?access_token=[Filtered]");
        sentryEvent.Request.Headers["Referer"].ShouldBe("https://web.example/tr/reset-password?token=[Filtered]");
    }

    [Fact]
    public void A_Http_Breadcrumb_Loses_The_Ticket_From_Its_Url()
    {
        var breadcrumb = new Breadcrumb("GET", "http", new Dictionary<string, string> { ["url"] = "/hubs/x?access_token=t", ["method"] = "GET" });

        var scrubbed = SentryScrubbing.Scrub(breadcrumb);

        scrubbed.Data!["url"].ShouldBe("/hubs/x?access_token=[Filtered]");
        scrubbed.Data["method"].ShouldBe("GET");
    }
}
