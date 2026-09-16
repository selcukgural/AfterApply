using AfterApply.Infrastructure.OpenAi;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AfterApply.IntegrationTests.Infrastructure;

/// <summary>
/// The guard for "an integration test never talks to the internet" (see NoOutboundHttpStartup).
///
/// It is a test rather than a comment because the mechanism is invisible from a test class: nothing
/// in a class's own code says its hosts cannot dial out, so the day the hosting-startup variable is
/// renamed, the assembly is renamed, or someone sets ASPNETCORE_PREVENTHOSTINGSTARTUP, every class
/// silently goes back to making real calls. This is a host built the way the plainest test class
/// builds one — nothing configured beyond a database — which is exactly the kind that was reaching
/// LinkedIn.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class NoOutboundHttpTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Theory]
    // The clients that fetch on behalf of user data, which is how the calls got out: a company's
    // LinkedIn/kariyer.net page and a job link's preview are both fetched from a URL a test created.
    [InlineData("ICompanyEnrichmentService")]
    [InlineData("IJobLinkPreviewService")]
    [InlineData("LinkedInJwks")]
    [InlineData("ILinkedInJobSourceClient")]
    [InlineData("vertex-cv-review")]
    [InlineData("vertex-job-fit")]
    [InlineData("IPayTrClient")]
    // And any client at all, including one a future feature adds without touching this file.
    [InlineData("some-client-added-next-year")]
    public async Task Every_Factory_Client_Refuses_To_Leave_The_Machine(string clientName)
    {
        _factory!.Services.GetRequiredService<IServiceProvider>();
        var httpClientFactory = _factory.Services.GetRequiredService<IHttpClientFactory>();
        using var client = httpClientFactory.CreateClient(clientName);

        var attempt = await Should.ThrowAsync<HttpRequestException>(
            () => client.GetAsync("https://www.linkedin.com/company/acme/"));

        attempt.Message.ShouldContain("do not make real outbound HTTP requests");
        attempt.Message.ShouldContain(clientName);
    }

    [Fact]
    public void OpenAi_Is_Left_Unconfigured_So_Its_Own_HttpClient_Has_Nothing_To_Call()
    {
        // The SDK builds its client internally, out of reach of the handler filter, so the key is
        // the lever — and a development machine's user secrets, which this host loads, hold a real
        // one.
        var openAi = _factory!.Services.GetRequiredService<IOptions<OpenAiOptions>>();

        openAi.Value.ApiKey.ShouldBeNullOrEmpty();
    }
}
