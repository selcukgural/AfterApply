using System.Net;

namespace AfterApply.IntegrationTests.JobSources;

/// <summary>
/// Plays kariyer.net for the sweep the way the site behaved on 2026-09-14: the first request to
/// <c>/is-ilanlari/{city}?kw=…</c> is a 301 onto the same path with the city ids, the page then
/// lists the cards with a pager, and a posting is a 301 from its bare id onto the slug URL. Only
/// the transport is replaced — the real client, pipeline, parsers and sweep run. Cards are
/// synthetic; the markup is the site's.
/// </summary>
internal sealed class KariyerNetStubHandler : HttpMessageHandler
{
    private readonly List<Uri> _requested = [];
    private readonly Queue<HttpStatusCode> _scriptedStatuses = new();

    public List<(string Id, string Slug, string Title, string Company, string Location, string WorkModel, string Date)> Cards { get; } =
        Enumerable.Range(1, 4)
            .Select(i => ($"45500000{i:00}", $"firma-{i}-net-gelistirici", $"Kariyer .NET Geliştirici {i}",
                i % 2 == 0 ? "Marmara Yazılım" : "Anadolu Bilişim", "İstanbul", i == 2 ? "Uzaktan" : "İş Yerinde", $"{i} gün"))
            .ToList();

    public IReadOnlyList<Uri> Requested
    {
        get
        {
            lock (_requested)
            {
                return [.. _requested];
            }
        }
    }

    /// <summary>Search pages served (the 301 hop is not counted).</summary>
    public int SearchRequests => Requested.Count(u => u.AbsolutePath.StartsWith("/is-ilanlari/", StringComparison.Ordinal) && u.Query.Contains("ct="));

    /// <summary>Postings asked for by bare id (the slug hop after the 301 is not counted).</summary>
    public int PostingRequests => Requested.Count(u => u.AbsolutePath.StartsWith("/is-ilani/", StringComparison.Ordinal)
                                                       && !u.AbsolutePath[(u.AbsolutePath.LastIndexOf('/') + 1)..].Contains('-'));

    public void AnswerNextWith(HttpStatusCode status, int times = 1)
    {
        for (var i = 0; i < times; i++)
        {
            _scriptedStatuses.Enqueue(status);
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        lock (_requested)
        {
            _requested.Add(uri);
        }

        if (_scriptedStatuses.TryDequeue(out var scripted))
        {
            return Task.FromResult(new HttpResponseMessage(scripted));
        }

        if (uri.AbsolutePath.StartsWith("/is-ilanlari/", StringComparison.Ordinal))
        {
            if (!uri.Query.Contains("ct="))
            {
                var separator = uri.Query.Length == 0 ? "?" : "&";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MovedPermanently)
                {
                    Headers = { Location = new Uri(uri.GetLeftPart(UriPartial.Path) + uri.Query + separator + "ct=34,82") }
                });
            }

            var body = string.Join("\n", Cards.Select(c => Card(c.Id, c.Slug, c.Title, c.Company, c.Location, c.WorkModel, c.Date))) +
                       "<div data-test=\"pagination\"><a href=\"/is-ilanlari/istanbul?ct=34%2C82&amp;kw=x&amp;cp=1\">1</a></div>";
            return Task.FromResult(Html(body));
        }

        if (uri.AbsolutePath.StartsWith("/is-ilani/", StringComparison.Ordinal))
        {
            var id = uri.AbsolutePath[(uri.AbsolutePath.LastIndexOf('/') + 1)..];
            if (!id.Contains('-'))
            {
                var card = Cards.FirstOrDefault(c => c.Id == id);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.MovedPermanently)
                {
                    Headers = { Location = new Uri($"https://www.kariyer.net/is-ilani/{card.Slug ?? "x"}-{id}") }
                });
            }

            return Task.FromResult(Html(Posting(id[(id.LastIndexOf('-') + 1)..])));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage Html(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "text/html") };

    private static string Card(string id, string slug, string title, string company, string location, string workModel, string date) => $"""
        <div class="job-list-card-item"><div><a href="/is-ilani/{slug}-{id}" target="_blank" data-test="ad-card-item" class="k-ad-card radius"><div data-test="ad-card-top" class="card-top"><div data-test="title-wrapper" class="title-wrapper"><div class="title-left"><span data-test="ad-card-title" class="k-ad-card-title multiline">{title}</span></div> <div data-test="subtitle-section" class="subtitle"><span data-test="subtitle">{company}</span></div> <div data-test="job-detail" class="job-detail"><span data-test="location" class="location">{location}</span> <span class="dot"></span> <span data-test="work-model" class="work-model">{workModel}</span></div></div></div> <div class="card-footer-wrapper"><div data-test="ad-date" class="ad-date"><span data-test="ad-date-item-date-other" class="date date-other"><!----> {date}</span></div></div></a></div></div>
        """;

    private static string Posting(string id) => $"""
        <html><body><div lastPublishDate="10.09.2026" class="job-container"><div data-test="qualifications-and-job-description" class="job-detail-qualifications"><p>kariyer.net ilanı {id} açıklaması.</p><ul><li>C#</li><li>.NET</li></ul></div> <div class="aligment-container"><section><div class="alignment-list"><div data-test="alignment-list-title" class="alignment-title"><span class="bullet">•</span> Tecrübe </div> <div data-test="alignment-list-value" class="alignment-value"> En az 3 yıl tecrübeli </div></div></section></div></div></body></html>
        """;
}
