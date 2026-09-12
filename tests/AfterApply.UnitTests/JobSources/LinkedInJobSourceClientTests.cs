using System.Net;
using AfterApply.Domain.Common;
using AfterApply.Domain.JobSources;
using AfterApply.Infrastructure.JobSources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.UnitTests.JobSources;

/// <summary>
/// The client together with the resilience pipeline it ships behind, over a scripted handler —
/// so what is asserted is the behaviour the sweep actually gets: which answers are retried,
/// which are reported as the source saying stop, and that neither ever throws.
/// </summary>
public class LinkedInJobSourceClientTests
{
    private static readonly JobSourceQuery Query =
        JobSourceQuery.Create(Source.LinkedIn, ".net developer", "türkiye", JobSourceTimeWindow.Week, false, "hash", DateTimeOffset.UtcNow);

    [Fact]
    public async Task A_Search_Page_Comes_Back_Parsed()
    {
        var handler = new ScriptedHandler(Ok(LinkedInJobSourceFixtures.ThreeCards));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 0, CancellationToken.None);

        result.Outcome.ShouldBe(JobSourceFetchOutcome.Ok);
        result.StatusCode.ShouldBe(200);
        result.Value!.Count.ShouldBe(3);
        handler.Requests.Single().RequestUri!.AbsoluteUri
            .ShouldBe("https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords=.net%20developer&location=t%C3%BCrkiye&f_TPR=r604800&start=0");
        handler.Requests.Single().Headers.UserAgent.ToString().ShouldStartWith("EKariyerimJobSource/1.0");
    }

    [Fact]
    public async Task A_Posting_Comes_Back_Parsed()
    {
        var handler = new ScriptedHandler(Ok(LinkedInJobSourceFixtures.Posting(LinkedInJobSourceFixtures.RichDescription)));
        var client = Build(handler);

        var result = await client.GetPostingAsync("4460989029", CancellationToken.None);

        result.IsOk.ShouldBeTrue();
        result.Value!.Description.ShouldStartWith("We enable financial institutions");
        result.Value.Seniority.ShouldBe("Mid-Senior level");
        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/jobs-guest/jobs/api/jobPosting/4460989029");
    }

    [Theory]
    [InlineData(429, JobSourceFetchOutcome.RateLimited)]
    [InlineData(403, JobSourceFetchOutcome.Blocked)]
    [InlineData(999, JobSourceFetchOutcome.Blocked)]
    public async Task Stop_Signals_Are_Reported_Once_And_Never_Retried(int status, JobSourceFetchOutcome expected)
    {
        var handler = new ScriptedHandler(Status((HttpStatusCode)status), Ok("<ul></ul>"));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 0, CancellationToken.None);

        result.Outcome.ShouldBe(expected);
        result.StatusCode.ShouldBe(status);
        result.StopsSweep.ShouldBeTrue();
        // The second scripted answer was never asked for: retrying into a rate limit is how a
        // client gets itself blocked.
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_5xx_Is_Retried_And_The_Retry_Can_Succeed()
    {
        var handler = new ScriptedHandler(Status(HttpStatusCode.ServiceUnavailable), Ok(LinkedInJobSourceFixtures.ThreeCards));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 0, CancellationToken.None);

        result.IsOk.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task A_Dropped_Connection_Is_Retried_Then_Given_Up_As_An_Error()
    {
        var handler = new ScriptedHandler(Throw(), Throw(), Throw(), Ok("<ul></ul>"));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 0, CancellationToken.None);

        result.Outcome.ShouldBe(JobSourceFetchOutcome.Error);
        result.StatusCode.ShouldBeNull();
        result.StopsSweep.ShouldBeFalse();
        handler.Requests.Count.ShouldBe(1 + JobSourceResilience.MaxRetryAttempts);
    }

    [Fact]
    public async Task A_Redirect_Into_The_Login_Wall_Is_A_Block()
    {
        var handler = new ScriptedHandler(Redirect("https://www.linkedin.com/authwall?trk=x"), Ok("<ul></ul>"));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 0, CancellationToken.None);

        result.Outcome.ShouldBe(JobSourceFetchOutcome.Blocked);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_Redirect_Off_Site_Is_Not_Followed()
    {
        var handler = new ScriptedHandler(Redirect("https://evil.example/collect"), Ok("<ul></ul>"));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 0, CancellationToken.None);

        result.Outcome.ShouldBe(JobSourceFetchOutcome.Blocked);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_Redirect_Within_LinkedIn_Is_Followed()
    {
        var handler = new ScriptedHandler(Redirect("https://tr.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search?start=0"),
            Ok(LinkedInJobSourceFixtures.ThreeCards));
        var client = Build(handler);

        var result = await client.SearchAsync(Query, 0, CancellationToken.None);

        result.IsOk.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[1].RequestUri!.Host.ShouldBe("tr.linkedin.com");
    }

    [Fact]
    public async Task An_Unexpected_Status_Is_An_Error_Not_A_Stop()
    {
        var handler = new ScriptedHandler(Status(HttpStatusCode.NotFound));
        var client = Build(handler);

        var result = await client.GetPostingAsync("1", CancellationToken.None);

        result.Outcome.ShouldBe(JobSourceFetchOutcome.Error);
        result.StopsSweep.ShouldBeFalse();
    }

    private static ILinkedInJobSourceClient Build(HttpMessageHandler handler)
    {
        var options = new JobSourceOptions { RetryBaseDelayMs = 1, AttemptTimeoutSeconds = 5, TotalTimeoutSeconds = 20 };
        var services = new ServiceCollection();
        services.AddSingleton(Options.Create(options));
        services.AddHttpClient<ILinkedInJobSourceClient, LinkedInJobSourceClient>()
            .ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddResilienceHandler("test", pipeline => JobSourceResilience.Configure(pipeline, options));
        return services.BuildServiceProvider().GetRequiredService<ILinkedInJobSourceClient>();
    }

    private static Func<HttpResponseMessage> Ok(string body) =>
        () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

    private static Func<HttpResponseMessage> Status(HttpStatusCode status) => () => new HttpResponseMessage(status);

    private static Func<HttpResponseMessage> Redirect(string location) =>
        () => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri(location) } };

    private static Func<HttpResponseMessage> Throw() => () => throw new HttpRequestException("connection reset");

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
