using System.Net;
using System.Net.Http.Json;
using AfterApply.Application.SiteTraffic.Contracts;
using Shouldly;

namespace AfterApply.IntegrationTests.Blog;

/// <summary>A reader's view as the web app reports it since 2026-09-29: the visit counter's page
/// view, posted by the browser without credentials. That report is what a post's tally counts.</summary>
internal static class ReaderViews
{
    public const string BrowserUserAgent =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";

    public const string CrawlerUserAgent = "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    public static async Task ReportAsync(HttpClient anonymous, string path, string userAgent = BrowserUserAgent)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/site-traffic/events");
        request.Content = JsonContent.Create(new RecordSiteTrafficEventRequest("page_view", path, null), options: ApiHost.JsonOptions);
        request.Headers.UserAgent.ParseAdd(userAgent);
        (await anonymous.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
