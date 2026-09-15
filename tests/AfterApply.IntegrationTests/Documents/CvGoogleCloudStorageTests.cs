using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AfterApply.Application.Documents.Contracts;
using AfterApply.Application.Identity.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Testcontainers.FakeGcsServer;

namespace AfterApply.IntegrationTests.Documents;

/// <summary>
/// One fake-gcs-server container for this class, started by the host profile before the host
/// boots (a class fixture cannot depend on another class fixture, so the container lives inside
/// the profile rather than beside it) and stopped after the host is disposed. The host points its
/// storage adapter at it.
/// </summary>
public sealed class FakeGcsProfile : IHostProfile
{
    public const string BucketName = "afterapply-cvs-test";

    // Pinned, like the Postgres image: the parameterless builder is obsolete, and an
    // unpinned emulator is a test that can start failing without anything here changing.
    private readonly FakeGcsServerContainer _container =
        new FakeGcsServerBuilder("fsouza/fake-gcs-server:1.52.2").Build();

    /// <summary>
    /// The JSON API root, in the shape Google's client library expects for
    /// <c>StorageService.BaseUri</c>. The module's connection string already carries the
    /// <c>/storage/v1</c> suffix; appending it unconditionally produced a doubled path and a 404
    /// from every call, so the suffix is added only when it is missing.
    /// </summary>
    public string BaseUri
    {
        get
        {
            var origin = _container.GetConnectionString().TrimEnd('/');
            return origin.EndsWith("/storage/v1", StringComparison.Ordinal)
                ? origin + "/"
                : origin + "/storage/v1/";
        }
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        // fake-gcs-server starts empty and object writes to an unknown bucket fail, so the bucket
        // is created the same way the real one is: once, out of band, before anything uploads.
        using var client = new HttpClient();
        var response = await client.PostAsJsonAsync($"{BaseUri}b?project=afterapply-test",
            new { name = BucketName });
        response.EnsureSuccessStatusCode();
    }

    public void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:Provider", "GoogleCloudStorage");
        builder.UseSetting("Storage:BucketName", BucketName);
        builder.UseSetting("Storage:EmulatorBaseUri", BaseUri);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

/// <summary>
/// Covers the Cloud Storage adapter itself — the one part of the CV feature the rest of the suite
/// never touches, because every other test runs against the filesystem provider. Deliberately a
/// single class with a handful of tests: it is here so the GCS path cannot rot unnoticed, not to
/// re-test the rules CvDocumentFlowTests already covers.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CvGoogleCloudStorageTests(ApiHost<FakeGcsProfile> host)
    : IClassFixture<ApiHost<FakeGcsProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n% gcs fixture\n");

    private WebApplicationFactory<Program> _factory => host;

    public Task InitializeAsync() => host.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _factory.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Gcs", "Owner", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    [Fact]
    public async Task A_Cv_Round_Trips_Through_Cloud_Storage()
    {
        var client = await AuthenticatedClientAsync("gcs.roundtrip@example.com");

        var part = new ByteArrayContent(PdfBytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        using var content = new MultipartFormDataContent
        {
            { part, "file", "cv.pdf" },
            { new StringContent("true"), "consentAccepted" }
        };

        var uploadResponse = await client.PostAsync("/api/cv-documents", content);
        uploadResponse.EnsureSuccessStatusCode();
        var created = await uploadResponse.Content.ReadFromJsonAsync<CvDocumentResponse>(JsonOptions);

        var download = await client.GetAsync($"/api/cv-documents/{created!.Id}/content");
        download.EnsureSuccessStatusCode();
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(PdfBytes);

        (await client.DeleteAsync($"/api/cv-documents/{created.Id}")).EnsureSuccessStatusCode();

        // Gone from the bucket, not just from the table: a second delete of the same object is a
        // no-op, and reading it back must now fail.
        (await client.GetAsync($"/api/cv-documents/{created.Id}/content")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
    }
}
