using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Imports.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.Imports;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Imports;

[Collection(IntegrationTestCollection.Name)]
public class LinkedInImportTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private const string JobApplicationsCsv =
        "Company,Title,Applied At,Status,Job URL\n" +
        "TechCo,Backend Developer,2026-01-10,Applied,https://www.linkedin.com/jobs/view/1111111111/\n" +
        "DataCo,Data Engineer,2026-01-12,Interview,https://www.linkedin.com/jobs/view/2222222222/\n";

    // Row 1 shares LinkedIn job id 1111111111 with JobApplicationsCsv's first row (different URL
    // query string) — proves tier-0 Source+ExternalId dedup, not just JobUrl exact match.
    private const string JobApplicationsCsv1 =
        "Company,Title,Applied At,Status,Job URL\n" +
        "TechCo,Backend Developer,2026-01-10,Applied,https://www.linkedin.com/jobs/view/1111111111/?refId=abc\n" +
        "NoUrlCo,QA Engineer,2026-01-13,Applied,\n" +
        ",Something,2026-01-14,Applied,\n";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var stores = await shared.CreateIsolatedStoresAsync(nameof(LinkedInImportTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });


        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("linkedin.test@example.com", "P@ssw0rd123!", "LinkedIn", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

    }

    private static byte[] BuildZip(params (string EntryName, string Content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entryName, content) in entries)
            {
                var entry = archive.CreateEntry(entryName);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream, Encoding.UTF8);
                writer.Write(content);
            }
        }

        return ms.ToArray();
    }

    private static MultipartFormDataContent BuildZipUpload(byte[] zipBytes, string fileName = "linkedin_export.zip")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(zipBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    private static byte[] BuildSampleLinkedInZip() => BuildZip(
        ("Jobs/Job Applications.csv", JobApplicationsCsv),
        ("Jobs/Job Applications_1.csv", JobApplicationsCsv1));

    // Processing runs out-of-request via a Hangfire job (see ImportEndpoints.cs) — the POST only
    // ever returns 202 + the batch id now. Every test that needs the final summary polls this
    // endpoint until the batch leaves Pending/Processing, same as a real client would (or
    // /hubs/import-progress, which isn't exercised here).
    private async Task<ImportSummaryResponse> PollUntilTerminalAsync(Guid batchId)
    {
        // 60s, not a tighter number: under concurrent test-class load (each with its own
        // in-process Hangfire server competing for CPU) a trivial import can legitimately take
        // much longer than it would in isolation — a too-tight timeout here doesn't just fail the
        // assertion, it risks the fixture disposing its containers out from under a Hangfire job
        // that's still actually running, which was observed to crash the whole test host.
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var response = await _client.GetAsync($"/api/imports/{batchId}");
            response.EnsureSuccessStatusCode();
            var summary = await response.Content.ReadFromJsonAsync<ImportSummaryResponse>(JsonOptions);
            summary.ShouldNotBeNull();

            if (summary!.Status is ImportBatchStatus.Completed or ImportBatchStatus.Failed)
            {
                return summary;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Import batch {batchId} did not reach a terminal status within 60s.");
    }

    [Fact]
    public async Task ImportLinkedInZip_First_Upload_Aggregates_Across_Files_And_Dedups_By_ExternalId()
    {
        var response = await _client.PostAsync("/api/imports/linkedin", BuildZipUpload(BuildSampleLinkedInZip()));
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var accepted = await response.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);
        accepted.ShouldNotBeNull();

        var summary = await PollUntilTerminalAsync(accepted!.Id);

        summary.Status.ShouldBe(ImportBatchStatus.Completed);
        summary.TotalRecords.ShouldBe(5);
        summary.NewApplications.ShouldBe(3);
        summary.DuplicateRecords.ShouldBe(1);
        summary.InvalidRecords.ShouldBe(1);

        var getResponse = await _client.GetAsync($"/api/imports/{summary.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<ImportSummaryResponse>(JsonOptions);
        fetched.ShouldNotBeNull();
        fetched!.NewApplications.ShouldBe(3);
    }

    [Fact]
    public async Task ImportLinkedInZip_Reuploading_Same_Zip_Is_Idempotent()
    {
        var first = await _client.PostAsync("/api/imports/linkedin", BuildZipUpload(BuildSampleLinkedInZip()));
        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var firstAccepted = await first.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);
        await PollUntilTerminalAsync(firstAccepted!.Id);

        var second = await _client.PostAsync("/api/imports/linkedin", BuildZipUpload(BuildSampleLinkedInZip()));
        second.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var secondAccepted = await second.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);
        var summary = await PollUntilTerminalAsync(secondAccepted!.Id);

        summary.Status.ShouldBe(ImportBatchStatus.Completed);
        summary.TotalRecords.ShouldBe(5);
        summary.NewApplications.ShouldBe(0);
        summary.DuplicateRecords.ShouldBe(4);
        summary.InvalidRecords.ShouldBe(1);
    }

    [Fact]
    public async Task ImportLinkedInZip_No_Matching_Files_Returns_ValidationProblem()
    {
        var zip = BuildZip(("Random.csv", "A,B\n1,2\n"));

        var response = await _client.PostAsync("/api/imports/linkedin", BuildZipUpload(zip));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImportLinkedInZip_NonZip_Extension_Returns_ValidationProblem()
    {
        var response = await _client.PostAsync("/api/imports/linkedin",
            BuildZipUpload(BuildSampleLinkedInZip(), fileName: "linkedin_export.rar"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImportLinkedInZip_TooManyEntries_Returns_ValidationProblem()
    {
        var entries = Enumerable.Range(0, 501).Select(i => ($"unrelated-{i}.txt", "x")).ToArray();
        var zip = BuildZip(entries);

        var response = await _client.PostAsync("/api/imports/linkedin", BuildZipUpload(zip));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // The import uploader reads progress.status off the hub push and compares it to the strings
    // "Completed"/"Failed". SignalR serializes hub payloads with its own options, not the ones
    // ConfigureHttpJsonOptions sets for the REST endpoints — so an enum went out over the socket
    // as its ordinal while GET /api/imports/{id} returned the name, and the push that landed after
    // the last poll turned a finished import into "Yükleme başarısız oldu" on screen.
    [Fact]
    public void ImportProgressHub_Serializes_Enums_As_Strings()
    {
        var protocol = _factory!.Services.GetServices<IHubProtocol>().OfType<JsonHubProtocol>().Single();
        var summary = new ImportSummaryResponse(
            Guid.NewGuid(), Source.LinkedInImport, "linkedin_export.zip", ImportBatchStatus.Completed,
            ProcessedRows: 5, TotalRows: 5, TotalRecords: 5, NewApplications: 5,
            DuplicateRecords: 0, InvalidRecords: 0, CompletedAt: DateTimeOffset.UtcNow,
            ErrorMessage: null, Errors: []);

        var payload = protocol.GetMessageBytes(new InvocationMessage("importStatusChanged", [summary]));
        var json = Encoding.UTF8.GetString(payload.ToArray());

        json.ShouldContain("\"status\":\"Completed\"");
        json.ShouldContain("\"source\":\"LinkedInImport\"");
    }
}
