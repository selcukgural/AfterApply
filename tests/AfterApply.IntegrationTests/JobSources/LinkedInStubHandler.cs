using System.Net;

namespace AfterApply.IntegrationTests.JobSources;

/// <summary>
/// Plays LinkedIn for the sweep: a search page of ten cards at start=0, a shorter page at
/// start=10 (which is how the sweep learns the list has ended), and a posting page for any id.
/// Only the transport is replaced — the real client, the real pipeline, the real parsers and the
/// real sweep all run. A test can script a status for the next N answers (a 429, say) and can
/// add cards between "weeks".
/// </summary>
internal sealed class LinkedInStubHandler : HttpMessageHandler
{
    private readonly List<Uri> _requested = [];
    private readonly Queue<HttpStatusCode> _scriptedStatuses = new();

    public List<(string Id, string Title, string Company, string Location, string Date)> Cards { get; } =
        Enumerable.Range(1, 13)
            .Select(i => ($"44600000{i:00}", $"Software Developer {i}", i % 2 == 0 ? "Kuzey Teknoloji" : "Acme Bankacılık", "İstanbul", "2026-09-10"))
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

    public int SearchRequests => Requested.Count(u => u.AbsolutePath.EndsWith("/search", StringComparison.Ordinal));

    public int PostingRequests => Requested.Count(u => u.AbsolutePath.Contains("/jobPosting/", StringComparison.Ordinal));

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

        if (uri.AbsolutePath.EndsWith("/search", StringComparison.Ordinal))
        {
            var start = int.Parse(System.Web.HttpUtility.ParseQueryString(uri.Query)["start"] ?? "0");
            var page = Cards.Skip(start).Take(10)
                .Select(c => Card(c.Id, c.Title, c.Company, c.Location, c.Date));
            return Task.FromResult(Html("<ul>\n" + string.Join("\n", page) + "\n</ul>"));
        }

        if (uri.AbsolutePath.Contains("/jobPosting/", StringComparison.Ordinal))
        {
            var id = uri.AbsolutePath[(uri.AbsolutePath.LastIndexOf('/') + 1)..];
            return Task.FromResult(Html(Posting(id)));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static HttpResponseMessage Html(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, System.Text.Encoding.UTF8, "text/html") };

    // Trimmed to what the parsers read; see the unit-test fixtures for the full shape.
    private static string Card(string id, string title, string company, string location, string date) => $"""
        <li>
          <div class="base-card base-search-card job-search-card" data-entity-urn="urn:li:jobPosting:{id}">
            <a class="base-card__full-link" href="https://tr.linkedin.com/jobs/view/slug-{id}?position=1"><span class="sr-only">{title}</span></a>
            <div class="base-search-card__info">
              <h3 class="base-search-card__title">{title}</h3>
              <h4 class="base-search-card__subtitle">
                <a class="hidden-nested-link" href="https://tr.linkedin.com/company/{company.ToLowerInvariant().Replace(' ', '-')}?trk=x">{company}</a>
              </h4>
              <div class="base-search-card__metadata">
                <span class="job-search-card__location">{location}</span>
                <time class="job-search-card__listdate" datetime="{date}">2 gün önce</time>
              </div>
            </div>
          </div>
        </li>
        """;

    private static string Posting(string id) => $"""
        <section class="show-more-less-html">
          <div class="show-more-less-html__markup relative overflow-hidden">
            <p>Description of posting {id}.</p><ul><li>C#</li><li>.NET</li></ul>
          </div>
          <button class="show-more-less-html__button">Show more</button>
        </section>
        <ul class="description__job-criteria-list">
          <li><h3 class="description__job-criteria-subheader">Seniority level</h3><span class="description__job-criteria-text">Mid-Senior level</span></li>
          <li><h3 class="description__job-criteria-subheader">Employment type</h3><span class="description__job-criteria-text">Full-time</span></li>
          <li><h3 class="description__job-criteria-subheader">Job function</h3><span class="description__job-criteria-text">Engineering</span></li>
          <li><h3 class="description__job-criteria-subheader">Industries</h3><span class="description__job-criteria-text">Software</span></li>
        </ul>
        """;
}

/// <summary>A clock the tests can move, so "next week" is a call rather than a wait.</summary>
internal sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
