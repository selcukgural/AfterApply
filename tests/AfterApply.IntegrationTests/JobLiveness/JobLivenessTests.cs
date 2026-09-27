using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.JobLiveness;
using AfterApply.Domain.Applications;
using AfterApply.Infrastructure.JobLiveness;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.JobLiveness;

// The daily liveness check end to end: postings captured through the extension, the real client
// and rules over a stubbed transport, Job.ClosedAt on the shared row, and what the application
// detail says about it. The responses are shaped like the real ones measured on 2026-09-27.
public sealed class JobLivenessProfile : IHostProfile
{
    public PathStubHandler Handler { get; } = new();

    public void Configure(IWebHostBuilder builder)
    {
        // Ships off, as every outbound-fetch flag does; on here so the path under test runs.
        builder.UseSetting("JobLiveness:Enabled", "true");
        builder.UseSetting("JobLiveness:MinDelayMs", "0");
        builder.ConfigureServices(services =>
            services.AddHttpClient(nameof(IJobLivenessClient)).ConfigurePrimaryHttpMessageHandler(() => Handler));
    }

    public void Reset() => Handler.Reset();
}

public class JobLivenessTests(ApiHost<JobLivenessProfile> host) : IClassFixture<ApiHost<JobLivenessProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private const string ClosedLinkedIn = "https://www.linkedin.com/jobs/view/4438199390/";
    private const string OpenLinkedIn = "https://www.linkedin.com/jobs/view/4464874711/";
    private const string ExpiredLinkedIn = "https://www.linkedin.com/jobs/view/4450698564/";
    private const string ClosedKariyerNet = "https://www.kariyer.net/is-ilani/perspective-satis-danismani-4300001";

    private HttpClient _client = null!;
    private PathStubHandler Handler => host.Profile.Handler;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        _client = await SignInAsync("liveness.test@example.com");

        Handler.On("/jobs/view/4438199390/", () => Page("""<figure class="closed-job closed-job__flavor">No longer accepting applications</figure>"""));
        Handler.On("/jobs/view/4464874711/", () => Page("""<figcaption class="num-applicants__caption">Be among the first 25 applicants</figcaption>"""));
        Handler.On("/jobs/view/4450698564/", () => Redirect("https://de.linkedin.com/jobs/systemprogrammierer-stellen?trk=expired_jd_redirect"));
        Handler.On("/is-ilani/ilan-4300001", () => Redirect("https://www.kariyer.net/is-ilani/perspective-satis-danismani-4300001"));
        Handler.On("/is-ilani/perspective-satis-danismani-4300001", () => Page("""job:{closingDateNumeric:"04.12.2025",passiveReason:"Bu ilan başvuruları artık kabul etmiyor."}"""));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Closed_Postings_Get_Their_Close_And_Open_Ones_Only_A_Check()
    {
        var closed = await CaptureAsync(_client, ClosedLinkedIn);
        var open = await CaptureAsync(_client, OpenLinkedIn);
        var expired = await CaptureAsync(_client, ExpiredLinkedIn);
        var kariyer = await CaptureAsync(_client, ClosedKariyerNet);

        await RunAsync();

        var jobs = await host.WithDbAsync(db => db.Jobs.ToDictionaryAsync(j => j.Url!));
        jobs[ClosedLinkedIn].ClosedAt.ShouldNotBeNull();
        jobs[ExpiredLinkedIn].ClosedAt.ShouldNotBeNull();
        jobs[OpenLinkedIn].ClosedAt.ShouldBeNull();
        jobs[OpenLinkedIn].LivenessCheckedAt.ShouldNotBeNull();
        jobs[ClosedKariyerNet].ClosedAt.ShouldBe(new DateTimeOffset(2025, 12, 5, 0, 0, 0, TimeSpan.FromHours(3)));

        // The expired redirect is the verdict itself: its target is never fetched.
        Handler.Requested.ShouldNotContain(uri => uri.Host == "de.linkedin.com");

        (await DetailAsync(closed)).JobClosedAt.ShouldNotBeNull();
        (await DetailAsync(open)).JobClosedAt.ShouldBeNull();
        (await DetailAsync(kariyer)).JobClosedAt.ShouldNotBeNull();
        (await DetailAsync(expired)).JobClosedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Posting_Is_Not_Asked_Again_Before_Its_Interval()
    {
        await CaptureAsync(_client, OpenLinkedIn);
        await RunAsync();
        Handler.Reset();

        await RunAsync();

        Handler.Requested.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Finished_Application_Is_Not_Worth_A_Request()
    {
        var application = await CaptureAsync(_client, OpenLinkedIn);
        var response = await _client.PostAsJsonAsync($"/api/applications/{application}/status",
            new ChangeStatusRequest(ApplicationStatus.Rejected, null, null), JsonOptions);
        response.EnsureSuccessStatusCode();

        await RunAsync();

        Handler.Requested.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_Posting_Saved_For_Later_Is_Checked_Too()
    {
        var response = await _client.PostAsJsonAsync("/api/tracked-jobs/from-extension", Request(ClosedLinkedIn), JsonOptions);
        response.EnsureSuccessStatusCode();

        await RunAsync();

        var job = await host.WithDbAsync(db => db.Jobs.SingleAsync());
        job.ClosedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Site_Saying_Stop_Is_Left_Alone_For_The_Rest_Of_The_Run()
    {
        Handler.On("/jobs/view/4438199390/", () => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        await CaptureAsync(_client, ClosedLinkedIn);
        await CaptureAsync(_client, OpenLinkedIn);
        await CaptureAsync(_client, ClosedKariyerNet);

        await RunAsync();

        Handler.Requested.Count(uri => uri.Host.EndsWith("linkedin.com")).ShouldBe(1);
        Handler.Requested.ShouldContain(uri => uri.Host.EndsWith("kariyer.net"));
        var jobs = await host.WithDbAsync(db => db.Jobs.ToListAsync());
        jobs.Where(j => j.Url!.Contains("linkedin")).ShouldAllBe(j => j.ClosedAt == null);
    }

    [Fact]
    public async Task A_Closed_Posting_Captured_Again_Is_A_New_Posting()
    {
        var first = await CaptureAsync(_client, ClosedLinkedIn);
        await RunAsync();

        // Someone else saves the same posting after it closed: the site put it back up, as far as
        // we are concerned, and that is a new round — the first applicant's row stays closed.
        var other = await SignInAsync("liveness.other@example.com");
        var second = await CaptureAsync(other, ClosedLinkedIn);

        var jobs = await host.WithDbAsync(db => db.Jobs.Where(j => j.Url == ClosedLinkedIn).ToListAsync());
        jobs.Count.ShouldBe(2);
        jobs.Count(j => j.ClosedAt != null).ShouldBe(1);
        (await DetailAsync(first)).JobClosedAt.ShouldNotBeNull();
        (await DetailAsync(second, other)).JobClosedAt.ShouldBeNull();
    }

    private Task RunAsync() =>
        host.WithScopeAsync(services => services.GetRequiredService<IJobLivenessService>().RunAsync(CancellationToken.None));

    private async Task<HttpClient> SignInAsync(string email)
    {
        var client = host.CreateClient();
        var auth = await TestAccounts.RegisterVerifiedAsync(client, host.Services,
            new RegisterRequest(email, "P@ssw0rd123!", "Live", "Ness", true));
        host.Jobs.DiscardWhere(TestAccounts.IsVerificationCodeJob);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static CreateFromExtensionRequest Request(string url) =>
        new("PeopleCert", "DevOps Engineer", url, Location: null, Description: null, PublishedAt: null);

    private static async Task<Guid> CaptureAsync(HttpClient client, string url)
    {
        var response = await client.PostAsJsonAsync("/api/applications/from-extension", Request(url), JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExtensionApplicationResponse>(JsonOptions))!.Application.Id;
    }

    private async Task<ApplicationDetailResponse> DetailAsync(Guid id, HttpClient? client = null) =>
        (await (client ?? _client).GetFromJsonAsync<ApplicationDetailResponse>($"/api/applications/{id}", JsonOptions))!;

    private static HttpResponseMessage Page(string html) => new(HttpStatusCode.OK) { Content = new StringContent(html) };

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri(location) } };
}

/// <summary>Answers by request path; anything unregistered is a 404. Records every request.</summary>
public sealed class PathStubHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpResponseMessage>> _routes = new(StringComparer.Ordinal);
    private readonly List<Uri> _requested = [];

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

    public void On(string path, Func<HttpResponseMessage> response)
    {
        lock (_routes)
        {
            _routes[path] = response;
        }
    }

    public void Reset()
    {
        lock (_requested)
        {
            _requested.Clear();
        }
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        lock (_requested)
        {
            _requested.Add(uri);
        }

        Func<HttpResponseMessage>? route;
        lock (_routes)
        {
            _routes.TryGetValue(uri.AbsolutePath, out route);
        }

        return Task.FromResult(route?.Invoke() ?? new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
