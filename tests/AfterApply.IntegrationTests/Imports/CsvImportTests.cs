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
using Testcontainers.Redis;

namespace AfterApply.IntegrationTests.Imports;

public class CsvImportTests : IAsyncLifetime
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

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();
    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString());
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

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

        await _postgres.DisposeAsync();
        await _redis.DisposeAsync();
    }

    private static MultipartFormDataContent BuildCsvUpload(string csvContent, string fileName = "applications.csv")
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task ImportCsv_First_Upload_Creates_New_Applications_And_Reports_Invalid_Row()
    {
        var response = await _client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv));
        response.EnsureSuccessStatusCode();
        var summary = await response.Content.ReadFromJsonAsync<ImportSummaryResponse>(JsonOptions);

        summary.ShouldNotBeNull();
        summary!.TotalRecords.ShouldBe(3);
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
        first.EnsureSuccessStatusCode();

        var second = await _client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv));
        second.EnsureSuccessStatusCode();
        var summary = await second.Content.ReadFromJsonAsync<ImportSummaryResponse>(JsonOptions);

        summary.ShouldNotBeNull();
        summary!.TotalRecords.ShouldBe(3);
        summary.NewApplications.ShouldBe(0);
        summary.DuplicateRecords.ShouldBe(2);
        summary.InvalidRecords.ShouldBe(1);
    }

    [Fact]
    public async Task ImportCsv_Missing_Required_Column_Returns_ValidationProblem()
    {
        var response = await _client.PostAsync("/api/imports/csv", BuildCsvUpload("Notes,Random\nfoo,bar\n"));

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImportCsv_NonCsv_Extension_Returns_ValidationProblem()
    {
        var response = await _client.PostAsync("/api/imports/csv", BuildCsvUpload(SampleCsv, fileName: "applications.txt"));

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }
}
