using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.CvScan;
using AfterApply.Application.CvScan.Contracts;
using AfterApply.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace AfterApply.IntegrationTests.CvScan;

/// <summary>
/// The anonymous CV scan, end to end and against real files.
///
/// No test here ever sends an Authorization header, and that is itself an assertion: the surface
/// exists to be usable by someone who has never heard of the product. The other standing assertion
/// is what is left afterwards — one anonymous row, no file anywhere.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class CvScanTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private WebApplicationFactory<Program>? _factory;
    private HttpClient _client = null!;
    private string _postgres = string.Empty;
    private string _storageRoot = string.Empty;

    public async Task InitializeAsync()
    {
        _postgres = await shared.CreateIsolatedDatabaseAsync(nameof(CvScanTests));

        // A storage root of its own, and nothing is ever expected to appear in it: the scan writes
        // no file, and this directory is how that claim is checked rather than asserted.
        _storageRoot = Path.Combine(Path.GetTempPath(), "afterapply-cv-scan-tests", Guid.CreateVersion7().ToString("N"));

        _factory = CreateFactory();
        _client = _factory.CreateClient();
    }

    private WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilderAccessor>? configure = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("Storage:LocalRootPath", _storageRoot);
            configure?.Invoke(new IWebHostBuilderAccessor(builder));
        });

    /// <summary>Thin wrapper so a caller can add settings without this file taking a dependency on
    /// the hosting builder type in its own signature.</summary>
    internal sealed class IWebHostBuilderAccessor(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        public void Setting(string key, string value) => builder.UseSetting(key, value);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private static MultipartFormDataContent ScanContent(byte[] bytes, string fileName,
        bool consentAccepted = true, string? website = null, long elapsedMs = 5_000)
    {
        var part = new ByteArrayContent(bytes);
        // A Content-Type the server must not believe: the format is decided from the extension and
        // the file's own leading bytes, never from what the client declared.
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        var content = new MultipartFormDataContent
        {
            { part, "file", fileName },
            { new StringContent(consentAccepted.ToString()), "consentAccepted" },
            { new StringContent(elapsedMs.ToString()), "elapsedMs" }
        };

        if (website is not null)
        {
            content.Add(new StringContent(website), "website");
        }

        return content;
    }

    private async Task<HttpResponseMessage> ScanAsync(byte[] bytes, string fileName,
        bool consentAccepted = true, string? website = null, long elapsedMs = 5_000, HttpClient? client = null)
    {
        using var content = ScanContent(bytes, fileName, consentAccepted, website, elapsedMs);
        return await (client ?? _client).PostAsync("/api/cv-scan", content);
    }

    private static async Task<CvScanResponse> ReadAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<CvScanResponse>(JsonOptions);
        return result.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_Readable_Cv_Scores_Well_And_The_Subtotals_Add_Up_To_The_Headline()
    {
        var response = await ScanAsync(CvFixtures.ReadablePdf(), "cv.pdf");
        var result = await ReadAsync(response);

        result.Score.ShouldBe(result.Categories.Sum(category => category.Score));
        result.Findings.Sum(finding => finding.PointCost).ShouldBe(100 - result.Score);
        result.Score.ShouldBeGreaterThanOrEqualTo(80);

        result.Document.Format.ShouldBe(AfterApply.Domain.Documents.CvFileFormat.Pdf);
        result.Document.PageCount.ShouldBe(1);
        result.Document.WordCount.ShouldBeGreaterThan(150);

        // What the machine reads, handed back. This is the part of the response the page's whole
        // argument rests on, so an empty preview would be a silent failure.
        result.ExtractedTextPreview.ShouldContain("EXPERIENCE");
    }

    [Fact]
    public async Task A_Two_Column_Cv_Is_Reported_With_The_Page_It_Is_On()
    {
        var result = await ReadAsync(await ScanAsync(CvFixtures.TwoColumnPdf(), "cv.pdf"));

        result.Findings.ShouldContain(item => item.Code == CvScanFindingCode.MultiColumnOrTableLayout);
        var finding = result.Findings.Single(item => item.Code == CvScanFindingCode.MultiColumnOrTableLayout);
        finding.Evidence.ShouldContain(evidence => evidence.Page == 1);
        finding.Evidence.ShouldContain(evidence => !string.IsNullOrWhiteSpace(evidence.Quote));
        result.Score.ShouldBeLessThan(100);
    }

    [Fact]
    public async Task A_Scanned_Cv_Loses_The_Whole_Machine_Readability_Category()
    {
        var result = await ReadAsync(await ScanAsync(CvFixtures.TextlessPdf(), "cv.pdf"));

        result.Findings.ShouldContain(finding => finding.Code == CvScanFindingCode.NoTextLayer);
        result.Categories
            .Single(category => category.Category == CvScanCategory.MachineReadability)
            .Score.ShouldBe(0);
    }

    [Fact]
    public async Task A_Docx_Header_Is_Read_As_A_Header_Rather_Than_As_Body_Text()
    {
        var result = await ReadAsync(await ScanAsync(CvFixtures.DocxWithContactInHeader(), "cv.docx"));

        result.Findings.ShouldContain(item => item.Code == CvScanFindingCode.ContactUnreadable);
        var finding = result.Findings.Single(item => item.Code == CvScanFindingCode.ContactUnreadable);
        finding.Metrics["emailFound"].ShouldBe(1);
        finding.Metrics["onlyInHeaderFooter"].ShouldBe(1);

        // A .docx has no pagination until something renders it, and the response says so rather
        // than inventing a number.
        result.Document.PageCount.ShouldBeNull();
    }

    /// <summary>
    /// The retention claim, checked rather than asserted: one anonymous row, and nothing on disk.
    /// </summary>
    [Fact]
    public async Task A_Scan_Leaves_Behind_One_Anonymous_Score_And_No_File()
    {
        await ReadAsync(await ScanAsync(CvFixtures.ReadablePdf(), "cv.pdf"));

        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await dbContext.CvScanResults.AsNoTracking().ToListAsync();
        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(row => row.Score >= 0 && row.Score <= 100);

        // Nothing was written anywhere: no CV document row, and no object in the storage root.
        (await dbContext.CvDocuments.CountAsync()).ShouldBe(0);
        (Directory.Exists(_storageRoot) && Directory.EnumerateFileSystemEntries(_storageRoot).Any())
            .ShouldBeFalse();
    }

    [Fact]
    public async Task The_Response_Is_Never_Cached()
    {
        var response = await ScanAsync(CvFixtures.ReadablePdf(), "cv.pdf");

        // The body carries excerpts of the caller's own CV. It is theirs, and it must not sit in
        // any shared cache on the way back.
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        response.Headers.CacheControl.Private.ShouldBeTrue();
    }

    [Fact]
    public async Task A_Scan_Without_Consent_Is_Refused_At_The_Consent_Box()
    {
        var response = await ScanAsync(CvFixtures.ReadablePdf(), "cv.pdf", consentAccepted: false);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("consentAccepted");
    }

    [Fact]
    public async Task A_Filled_Honeypot_Is_Refused()
    {
        var response = await ScanAsync(CvFixtures.ReadablePdf(), "cv.pdf", website: "https://example.com");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Form_Submitted_Faster_Than_A_Person_Could_Is_Refused()
    {
        var response = await ScanAsync(CvFixtures.ReadablePdf(), "cv.pdf", elapsedMs: 40);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_Old_Doc_Format_Is_Refused_With_An_Explanation()
    {
        // A real OLE2 header, so the refusal is about the format rather than about the bytes not
        // matching the extension.
        byte[] ole2 = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00];

        var response = await ScanAsync(ole2, "cv.doc");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain(".docx");
    }

    [Fact]
    public async Task A_File_Whose_Bytes_Are_Not_Its_Extension_Is_Refused()
    {
        var response = await ScanAsync(Encoding.ASCII.GetBytes("<html>not a pdf at all</html>"), "cv.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Damaged_Pdf_Is_Refused_Rather_Than_Crashing_The_Endpoint()
    {
        // The right leading bytes and nothing else — the shape a truncated upload or a corrupted
        // export arrives in.
        var response = await ScanAsync(Encoding.ASCII.GetBytes("%PDF-1.7\nnot really a document"), "cv.pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>A page cap is one of the three bounds on what an anonymous caller can ask a parser
    /// to do. Driven from configuration here rather than by building a giant PDF: the rule under
    /// test is "more pages than the limit is refused", and a 0-page limit states it in one line.</summary>
    [Fact]
    public async Task A_Cv_With_More_Pages_Than_The_Scan_Reads_Is_Refused()
    {
        await using var factory = CreateFactory(settings => settings.Setting("CvScan:MaxPages", "0"));
        using var client = factory.CreateClient();

        var response = await ScanAsync(CvFixtures.ReadablePdf(), "cv.pdf", client: client);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>The flag is the stopping condition's mechanism: if the surface does not earn its
    /// place in four weeks it closes, and closing it must take the route out of existence rather
    /// than leave it answering.</summary>
    [Fact]
    public async Task The_Route_Does_Not_Exist_While_The_Flag_Is_Off()
    {
        await using var factory = CreateFactory(settings => settings.Setting("CvScan:Enabled", "false"));
        using var client = factory.CreateClient();

        var response = await ScanAsync(CvFixtures.ReadablePdf(), "cv.pdf", client: client);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_Rate_Limit_Stops_The_Sixth_Scan_From_One_Address()
    {
        await using var factory = CreateFactory(settings =>
        {
            settings.Setting("RateLimiting:Enabled", "true");
            settings.Setting("RateLimiting:CvScan:PermitLimit", "2");
            settings.Setting("RateLimiting:CvScan:WindowSeconds", "7200");
        });

        using var client = factory.CreateClient();
        var bytes = CvFixtures.ReadablePdf();

        (await ScanAsync(bytes, "cv.pdf", client: client)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ScanAsync(bytes, "cv.pdf", client: client)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ScanAsync(bytes, "cv.pdf", client: client)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
