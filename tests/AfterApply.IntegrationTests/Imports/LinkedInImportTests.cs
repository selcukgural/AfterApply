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
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Testcontainers.PostgreSql;

namespace AfterApply.IntegrationTests.Imports;

public class LinkedInImportTests : IAsyncLifetime
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

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", "localhost:6379");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        _client = _factory.CreateClient();
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("linkedin.test@example.com", "P@ssw0rd123!", "LinkedIn", "Test"), JsonOptions);
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

        await _postgres.DisposeAsync();
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

    [Fact]
    public async Task ImportLinkedInZip_First_Upload_Aggregates_Across_Files_And_Dedups_By_ExternalId()
    {
        var response = await _client.PostAsync("/api/imports/linkedin", BuildZipUpload(BuildSampleLinkedInZip()));
        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<ImportSummaryResponse>(JsonOptions);

        summary.ShouldNotBeNull();
        summary!.TotalRecords.ShouldBe(5);
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
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsync("/api/imports/linkedin", BuildZipUpload(BuildSampleLinkedInZip()));
        second.EnsureSuccessStatusCode();
        var summary = await second.Content.ReadFromJsonAsync<ImportSummaryResponse>(JsonOptions);

        summary.ShouldNotBeNull();
        summary!.TotalRecords.ShouldBe(5);
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
}
