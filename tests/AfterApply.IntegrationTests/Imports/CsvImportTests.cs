using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Imports.Contracts;
using AfterApply.Domain.Applications;
using AfterApply.Domain.Common;
using AfterApply.Domain.Imports;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.Imports;

[Collection(IntegrationTestCollection.Name)]
public class CsvImportTests(ApiHost<DefaultProfile> host) : IClassFixture<ApiHost<DefaultProfile>>, IAsyncLifetime
{
    private const string SampleCsv =
        "Company,Title,Applied At,Status,Job URL,Location\n" +
        "TechCo,Backend Developer,2026-01-10,Applied,,Istanbul\n" +
        "DataCo,Data Engineer,2026-01-12,Interview,https://example.com/job/42,Ankara\n" +
        ",QA Engineer,2026-01-13,,,\n";

    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private WebApplicationFactory<Program> _factory => host;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("imports.test@example.com", "P@ssw0rd123!", "Imports", "Test", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static MultipartFormDataContent BuildCsvUpload(string csvContent, string fileName = "applications.csv")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    // Processing runs out-of-request via a background job (see ImportEndpoints.cs) — the POST only
    // ever returns 202 + the batch id. The job runs here, inline, and the summary is read once;
    // a real client would poll this endpoint (or /hubs/import-progress, not exercised here).
    private async Task<ImportSummaryResponse> PollUntilTerminalAsync(Guid batchId)
    {
        await host.RunJobsAsync();

        var response = await _client.GetAsync($"/api/imports/{batchId}");
        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<ImportSummaryResponse>(JsonOptions);
        summary.ShouldNotBeNull();
        summary.Status.ShouldBeOneOf(ImportBatchStatus.Completed, ImportBatchStatus.Failed);
        return summary;
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

    /// <summary>The progress hub sits on the Redis backplane, and a group send while Redis is
    /// unreachable throws. That must cost the uploader a push, not the import: the notifier
    /// swallows it, the job completes, and the poll endpoint still tells the truth.</summary>
    [Fact]
    public async Task ImportCsv_Completes_When_The_Progress_Push_Cannot_Reach_Redis()
    {
        await using var withoutRedis = host.Standalone(builder =>
        {
            builder.UseSetting("ConnectionStrings:Redis", "localhost:1,abortConnect=false,connectTimeout=300,syncTimeout=300");
            builder.UseSetting("Redis:WaitForBackplaneSubscribe", "false");
        });
        var (client, _) = await host.RegisterAsync("imports.noredis@example.com", on: withoutRedis);

        var response = await client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv));
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Accepted);
        var accepted = await response.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);

        await host.RunJobsAsync();
        host.Jobs.Failed.ShouldBeEmpty("a lost progress push must not fail the import job");

        var summary = await client.GetFromJsonAsync<ImportSummaryResponse>($"/api/imports/{accepted!.Id}", JsonOptions);
        summary!.Status.ShouldBe(ImportBatchStatus.Completed);
        summary.NewApplications.ShouldBe(2);
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

    [Fact]
    public async Task ImportCsv_Records_Import_Origin_On_Both_History_Rows()
    {
        // DataCo's row imports at Interview, so it produces two history rows: the seed "→ Applied"
        // that Create() writes, and the transition ChangeStatus writes. Neither is a manual change
        // and the status history has to say so on both.
        var response = await _client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv));
        var accepted = await response.Content.ReadFromJsonAsync<ImportAcceptedResponse>(JsonOptions);
        await PollUntilTerminalAsync(accepted!.Id);

        Guid applicationId;
        using (var scope = _factory!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            applicationId = (await db.Applications.SingleAsync(a => a.JobTitle == "Data Engineer")).Id;
        }

        // Read through the endpoint, not the DbSet: both rows carry the CSV's applied date, so this
        // is also the assertion that the newest-first ordering survives that tie.
        var historyResponse = await _client.GetAsync($"/api/applications/{applicationId}/status-history");
        historyResponse.EnsureSuccessStatusCode();
        var history = await historyResponse.Content.ReadFromJsonAsync<List<ApplicationStatusHistoryResponse>>(JsonOptions);

        history!.Count.ShouldBe(2);
        history.ShouldAllBe(h => h.Origin == StatusChangeOrigin.Import);
        history.ShouldAllBe(h => h.Source == Source.CsvImport);
        history.ShouldAllBe(h => h.Note == null);
        history[0].ToStatus.ShouldBe(ApplicationStatus.Interview);
        history[1].ToStatus.ShouldBe(ApplicationStatus.Applied);
        history[1].FromStatus.ShouldBeNull();
        history[0].ChangedAt.ShouldBe(history[1].ChangedAt);
    }
}
