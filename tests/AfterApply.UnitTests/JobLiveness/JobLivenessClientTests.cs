using System.Net;
using AfterApply.Application.AtsSources;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using AfterApply.Domain.Jobs;
using AfterApply.Infrastructure.AtsSources;
using AfterApply.Infrastructure.JobLiveness;
using AfterApply.Infrastructure.JobSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.UnitTests.JobLiveness;

/// <summary>
/// The client behind the resilience pipeline it ships with, over a scripted handler: which answer
/// means closed, gone, stop or nothing, which address is asked for, and that the redirects that
/// carry the verdict are read without ever being followed.
/// </summary>
public class JobLivenessClientTests
{
    [Fact]
    public async Task LinkedIns_Expired_Redirect_Is_A_Close_And_Is_Not_Followed()
    {
        var handler = new ScriptedHandler(Redirect("https://de.linkedin.com/jobs/systemprogrammierer-stellen?trk=expired_jd_redirect"));

        var result = await Build(handler).CheckAsync(Source.LinkedIn, "4450698564", null, CancellationToken.None);

        result.Kind.ShouldBe(PostingLivenessKind.Closed);
        handler.Requests.Single().RequestUri!.AbsoluteUri.ShouldBe("https://www.linkedin.com/jobs/view/4450698564/");
        handler.Requests.Single().Headers.UserAgent.ToString().ShouldStartWith("EKariyerimJobCheck/1.0");
    }

    [Fact]
    public async Task A_LinkedIn_Canonical_Redirect_Is_Followed_To_The_Page()
    {
        var handler = new ScriptedHandler(
            Redirect("https://www.linkedin.com/jobs/view/devops-engineer-at-peoplecert-4438199390"),
            Ok(PostingLivenessRulesTests.LinkedInClosedPage));

        var result = await Build(handler).CheckAsync(Source.LinkedInImport, "4438199390", null, CancellationToken.None);

        result.Kind.ShouldBe(PostingLivenessKind.Closed);
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_Open_LinkedIn_Page_Is_Open()
    {
        var handler = new ScriptedHandler(Ok(PostingLivenessRulesTests.LinkedInOpenPage));

        (await Build(handler).CheckAsync(Source.LinkedIn, "4464874711", null, CancellationToken.None)).Kind
            .ShouldBe(PostingLivenessKind.Open);
    }

    [Theory]
    [InlineData(404, PostingLivenessKind.Gone, false)]
    [InlineData(410, PostingLivenessKind.Gone, false)]
    [InlineData(429, PostingLivenessKind.Unknown, true)]
    [InlineData(403, PostingLivenessKind.Unknown, true)]
    [InlineData(999, PostingLivenessKind.Unknown, true)]
    public async Task Status_Codes_Map_To_Gone_Or_Stop(int status, PostingLivenessKind kind, bool stop)
    {
        var handler = new ScriptedHandler(Status((HttpStatusCode)status));

        var result = await Build(handler).CheckAsync(Source.LinkedIn, "4450698564", null, CancellationToken.None);

        result.Kind.ShouldBe(kind);
        result.SourceSaidStop.ShouldBe(stop);
    }

    [Fact]
    public async Task A_Login_Wall_Is_Stop_And_An_Off_Site_Redirect_Is_Nothing()
    {
        var wall = await Build(new ScriptedHandler(Redirect("https://www.linkedin.com/authwall?trk=x")))
            .CheckAsync(Source.LinkedIn, "1", null, CancellationToken.None);
        wall.SourceSaidStop.ShouldBeTrue();

        var offSite = await Build(new ScriptedHandler(Redirect("https://evil.example/jobs/1")))
            .CheckAsync(Source.LinkedIn, "1", null, CancellationToken.None);
        offSite.Kind.ShouldBe(PostingLivenessKind.Unknown);
        offSite.SourceSaidStop.ShouldBeFalse();
    }

    [Fact]
    public async Task A_Server_Error_Or_A_Dropped_Connection_Is_Nothing()
    {
        (await Build(new ScriptedHandler(Status(HttpStatusCode.ServiceUnavailable))).CheckAsync(Source.LinkedIn, "1", null, CancellationToken.None))
            .Kind.ShouldBe(PostingLivenessKind.Unknown);
        (await Build(new ScriptedHandler(Throw())).CheckAsync(Source.LinkedIn, "1", null, CancellationToken.None))
            .Kind.ShouldBe(PostingLivenessKind.Unknown);
    }

    [Fact]
    public async Task KariyerNet_Is_Asked_By_Id_And_Its_Canonical_Redirect_Followed()
    {
        var handler = new ScriptedHandler(
            Redirect("https://www.kariyer.net/is-ilani/perspective-satis-danismani-mall-of-istanbul-4300001"),
            Ok(PostingLivenessRulesTests.KariyerNetPage("04.12.2025")));

        var result = await Build(handler).CheckAsync(Source.KariyerNet, "4300001", "https://www.kariyer.net/whatever", CancellationToken.None);

        result.Kind.ShouldBe(PostingLivenessKind.Closed);
        handler.Requests[0].RequestUri!.AbsoluteUri.ShouldBe("https://www.kariyer.net/is-ilani/ilan-4300001");
    }

    [Fact]
    public async Task KariyerNets_Redirect_To_The_Listing_Is_Only_Gone()
    {
        var handler = new ScriptedHandler(Redirect("https://www.kariyer.net/is-ilanlari"));

        (await Build(handler).CheckAsync(Source.KariyerNet, "4450003", null, CancellationToken.None)).Kind
            .ShouldBe(PostingLivenessKind.Gone);
        handler.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("4450698564/../../evil")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task An_Id_That_Is_Not_A_Number_Is_Never_Requested(string id)
    {
        var handler = new ScriptedHandler(Ok("x"));

        (await Build(handler).CheckAsync(Source.LinkedIn, id, null, CancellationToken.None)).Kind.ShouldBe(PostingLivenessKind.Unknown);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Source.Greenhouse, 404, PostingLivenessKind.Closed)]
    [InlineData(Source.Lever, 404, PostingLivenessKind.Closed)]
    [InlineData(Source.Workable, 404, PostingLivenessKind.Gone)]
    [InlineData(Source.Ashby, 410, PostingLivenessKind.Gone)]
    [InlineData(Source.Greenhouse, 500, PostingLivenessKind.Unknown)]
    public async Task An_Ats_Not_Found_Closes_Only_Where_It_Was_Measured(Source source, int status, PostingLivenessKind kind)
    {
        var ats = new FakeAtsClient(new JobSourceFetchResult<AtsJobPosting>(JobSourceFetchOutcome.Error, status, 5, null));

        var result = await Build(new ScriptedHandler(), ats).CheckAsync(source, "acme/123", "https://boards.greenhouse.io/acme/jobs/123", CancellationToken.None);

        result.Kind.ShouldBe(kind);
    }

    [Fact]
    public async Task An_Ats_Answer_Is_Open_And_A_Rate_Limit_Is_Stop()
    {
        var open = await Build(new ScriptedHandler(), new FakeAtsClient(new JobSourceFetchResult<AtsJobPosting>(JobSourceFetchOutcome.Ok, 200, 5, null)))
            .CheckAsync(Source.Greenhouse, "acme/1", "https://boards.greenhouse.io/acme/jobs/1", CancellationToken.None);
        open.Kind.ShouldBe(PostingLivenessKind.Open);

        var limited = await Build(new ScriptedHandler(), new FakeAtsClient(new JobSourceFetchResult<AtsJobPosting>(JobSourceFetchOutcome.RateLimited, 429, 5, null)))
            .CheckAsync(Source.Greenhouse, "acme/1", "https://boards.greenhouse.io/acme/jobs/1", CancellationToken.None);
        limited.SourceSaidStop.ShouldBeTrue();
    }

    private static IJobLivenessClient Build(HttpMessageHandler handler, IAtsJobClient? ats = null)
    {
        var sourceOptions = new JobSourceOptions { RetryBaseDelayMs = 1, AttemptTimeoutSeconds = 5, TotalTimeoutSeconds = 20 };
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(new JobLivenessOptions()));
        services.AddSingleton(ats ?? new FakeAtsClient(new JobSourceFetchResult<AtsJobPosting>(JobSourceFetchOutcome.Ok, 200, 1, null)));
        services.AddHttpClient<IJobLivenessClient, JobLivenessClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddResilienceHandler("test", pipeline => JobSourceResilience.Configure(pipeline, sourceOptions));
        return services.BuildServiceProvider().GetRequiredService<IJobLivenessClient>();
    }

    private static Func<HttpResponseMessage> Ok(string body) =>
        () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static Func<HttpResponseMessage> Status(HttpStatusCode status) => () => new HttpResponseMessage(status);

    private static Func<HttpResponseMessage> Redirect(string location) =>
        () => new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri(location) } };

    private static Func<HttpResponseMessage> Throw() => () => throw new HttpRequestException("connection reset");

    private sealed class FakeAtsClient(JobSourceFetchResult<AtsJobPosting> result) : IAtsJobClient
    {
        public Task<JobSourceFetchResult<AtsJobPosting>> GetPostingAsync(Source source, string jobUrl, string externalId,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ScriptedHandler(params Func<HttpResponseMessage>[] script) : HttpMessageHandler
    {
        private int _index;

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var step = script[Math.Min(_index++, script.Length - 1)];
            return Task.FromResult(step());
        }
    }
}
