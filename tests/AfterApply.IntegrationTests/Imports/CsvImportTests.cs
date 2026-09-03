using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Imports.Contracts;
using AfterApply.Domain.Imports;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Imports;

[Collection(IntegrationTestCollection.Name)]
public class CsvImportTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private const string SampleCsv =
        "Company,Title,Applied At,Status,Job URL,Location\n" +
        "TechCo,Backend Developer,2026-01-10,Applied,,Istanbul\n" +
        "DataCo,Data Engineer,2026-01-12,Interview,https://example.com/job/42,Ankara\n" +
        ",QA Engineer,2026-01-13,,,\n";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var stores = await shared.CreateIsolatedStoresAsync(nameof(CsvImportTests));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", stores.Postgres);
            builder.UseSetting("ConnectionStrings:Redis", stores.Redis);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });


        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("imports.test@example.com", "P@ssw0rd123!", "Imports", "Test", true), JsonOptions);
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

    private static MultipartFormDataContent BuildCsvUpload(string csvContent, string fileName = "applications.csv")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        return content;
    }

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
    public async Task ImportCsv_First_Upload_Creates_New_Applications_And_Reports_Invalid_Row()
    {
        var response = await _client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv));
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Accepted);
        var accepted = await response.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);
        accepted.ShouldNotBeNull();

        var summary = await PollUntilTerminalAsync(accepted!.Id);

        summary.Status.ShouldBe(ImportBatchStatus.Completed);
        summary.TotalRecords.ShouldBe(3);
        summary.NewApplications.ShouldBe(2);
        summary.DuplicateRecords.ShouldBe(0);
        summary.InvalidRecords.ShouldBe(1);
        summary.Errors.Count.ShouldBe(1);
        summary.Errors.Single().RowNumber.ShouldBe(3);

        var getResponse = await _client.GetAsync($"/api/imports/{summary.Id}");
        getResponse.EnsureSuccessStatusCode();
        var fetched = await getResponse.Content.ReadFromJsonAsync<ImportSummaryResponse>(JsonOptions);

        fetched.ShouldNotBeNull();
        fetched!.Id.ShouldBe(summary.Id);
        fetched.NewApplications.ShouldBe(2);
        fetched.Errors.Single().ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ImportCsv_Reuploading_Same_File_Is_Idempotent()
    {
        var first = await _client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv));
        first.StatusCode.ShouldBe(System.Net.HttpStatusCode.Accepted);
        var firstAccepted = await first.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);
        await PollUntilTerminalAsync(firstAccepted!.Id);

        var second = await _client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv));
        second.StatusCode.ShouldBe(System.Net.HttpStatusCode.Accepted);
        var secondAccepted = await second.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);
        var summary = await PollUntilTerminalAsync(secondAccepted!.Id);

        summary.Status.ShouldBe(ImportBatchStatus.Completed);
        summary.TotalRecords.ShouldBe(3);
        summary.NewApplications.ShouldBe(0);
        summary.DuplicateRecords.ShouldBe(2);
        summary.InvalidRecords.ShouldBe(1);
    }

    [Fact]
    public async Task ImportCsv_Missing_Required_Column_Batch_Fails()
    {
        var response = await _client.PostAsync("/api/imports/csv", BuildCsvUpload("Notes,Random\nfoo,bar\n"));
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Accepted);
        var accepted = await response.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);

        var summary = await PollUntilTerminalAsync(accepted!.Id);

        summary.Status.ShouldBe(ImportBatchStatus.Failed);
        summary.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ImportCsv_NonCsv_Extension_Returns_ValidationProblem()
    {
        var response = await _client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv, fileName: "applications.txt"));

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }
}
