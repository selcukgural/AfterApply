using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.Configuration;

public sealed class SiteRootProfile : IHostProfile
{
    public const string WebBaseUrl = "https://ekariyerim.example";

    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("App:WebBaseUrl", WebBaseUrl);
    }
}

/// <summary>
/// The API host's root and robots.txt (2026-09-16): the web app names the API host in a preconnect
/// hint on every page, so crawlers fetch its root and Search Console reported it as a 404. The root
/// sends them to the web app permanently and robots.txt keeps them from probing further; neither
/// is part of the OpenAPI contract.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class SiteRootTests(ApiHost<SiteRootProfile> host) : IClassFixture<ApiHost<SiteRootProfile>>, IAsyncLifetime
{
    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private HttpClient CreateClient() => host.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task The_Root_Is_A_Permanent_Redirect_To_The_Web_App()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.MovedPermanently);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location.ShouldBe(new Uri(SiteRootProfile.WebBaseUrl));
    }

    [Fact]
    public async Task Robots_Forbids_Indexing_The_API_Host()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/robots.txt");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        (await response.Content.ReadAsStringAsync()).ShouldBe("User-agent: *\nDisallow: /\n");
    }

    [Fact]
    public async Task Neither_Address_Is_In_The_OpenApi_Document()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = await response.Content.ReadAsStringAsync();
        document.ShouldNotContain("\"/\":");
        document.ShouldNotContain("/robots.txt");
    }
}
