using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Documents.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Common;
using AfterApply.Domain.Documents;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.Documents;

[Collection(IntegrationTestCollection.Name)]
public class CvDocumentFlowTests(SharedInfrastructure shared) : IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>A minimal but genuinely well-formed PDF header — the upload path checks the leading
    /// bytes, so a placeholder of arbitrary content would be refused for the wrong reason.</summary>
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n% test fixture\n");

    private static readonly byte[] DocxBytes = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00, 0x08, 0x00];

    private WebApplicationFactory<Program>? _factory;
    private string _storageRoot = string.Empty;

    public async Task InitializeAsync()
    {
        var postgres = await shared.CreateIsolatedDatabaseAsync(nameof(CvDocumentFlowTests));

        // A directory of its own per test, so one test's files can never be mistaken for another's
        // and the assertions about what is on disk mean what they say.
        _storageRoot = Path.Combine(Path.GetTempPath(), "afterapply-cv-tests", Guid.CreateVersion7().ToString("N"));

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", postgres);
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            builder.UseSetting("Storage:LocalRootPath", _storageRoot);
        });
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _factory!.CreateClient();

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest(email, "P@ssw0rd123!", "Cv", "Owner", true), JsonOptions);
        registerResponse.EnsureSuccessStatusCode();
        var auth = await registerResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        auth.ShouldNotBeNull();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        return client;
    }

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName)
    {
        var part = new ByteArrayContent(bytes);
        // Deliberately a Content-Type the server must not believe: it decides the stored type from
        // the extension and the file's own bytes, never from what the client declared.
        part.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        return new MultipartFormDataContent { { part, "file", fileName } };
    }

    private static async Task<CvDocumentResponse> UploadAsync(HttpClient client, byte[] bytes, string fileName)
    {
        using var content = FileContent(bytes, fileName);
        var response = await client.PostAsync("/api/cv-documents", content);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<CvDocumentResponse>(JsonOptions);
        created.ShouldNotBeNull();
        return created!;
    }

    [Fact]
    public async Task Upload_List_Download_And_Delete()
    {
        var client = await AuthenticatedClientAsync("cv.crud@example.com");

        var created = await UploadAsync(client, PdfBytes, "Selcuk-Gural-CV.pdf");
        created.FileName.ShouldBe("Selcuk-Gural-CV.pdf");
        created.Format.ShouldBe(CvFileFormat.Pdf);
        created.SizeBytes.ShouldBe(PdfBytes.Length);

        var list = await client.GetFromJsonAsync<CvDocumentListResponse>("/api/cv-documents", JsonOptions);
        list.ShouldNotBeNull();
        list!.MaxCount.ShouldBe(CvDocument.MaxPerUser);
        list.Items.ShouldContain(item => item.Id == created.Id);

        var download = await client.GetAsync($"/api/cv-documents/{created.Id}/content");
        download.EnsureSuccessStatusCode();
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(PdfBytes);

        var deleteResponse = await client.DeleteAsync($"/api/cv-documents/{created.Id}");
        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterDelete = await client.GetFromJsonAsync<CvDocumentListResponse>("/api/cv-documents", JsonOptions);
        afterDelete!.Items.ShouldBeEmpty();

        var downloadAfterDelete = await client.GetAsync($"/api/cv-documents/{created.Id}/content");
        downloadAfterDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Removes_The_Stored_File_Too()
    {
        var client = await AuthenticatedClientAsync("cv.storage.delete@example.com");

        var created = await UploadAsync(client, PdfBytes, "cv.pdf");

        var storedFiles = Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories);
        storedFiles.Length.ShouldBe(1);

        (await client.DeleteAsync($"/api/cv-documents/{created.Id}")).EnsureSuccessStatusCode();

        Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task Download_Is_Always_An_Attachment_With_The_Stored_Content_Type()
    {
        var client = await AuthenticatedClientAsync("cv.disposition@example.com");

        var created = await UploadAsync(client, PdfBytes, "cv.pdf");

        var download = await client.GetAsync($"/api/cv-documents/{created.Id}/content");

        // "attachment", never "inline": the browser must save the file rather than render it on
        // our own origin.
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        // From the stored format, not from the application/octet-stream the upload declared.
        download.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        download.Headers.CacheControl!.NoStore.ShouldBeTrue();
        download.Headers.CacheControl.Private.ShouldBeTrue();
    }

    [Fact]
    public async Task Upload_Refuses_A_File_Whose_Bytes_Are_Not_The_Format_Its_Name_Claims()
    {
        var client = await AuthenticatedClientAsync("cv.disguised@example.com");

        // "MZ..." — a Windows executable wearing a .pdf name.
        byte[] executable = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, 0x04, 0x00];

        using var content = FileContent(executable, "cv.pdf");
        var response = await client.PostAsync("/api/cv-documents", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Nothing was written: a refused upload must not leave bytes behind.
        Directory.Exists(_storageRoot).ShouldBeFalse();
    }

    [Fact]
    public async Task Upload_Refuses_An_Unsupported_Extension()
    {
        var client = await AuthenticatedClientAsync("cv.badext@example.com");

        using var content = FileContent(PdfBytes, "cv.exe");
        var response = await client.PostAsync("/api/cv-documents", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_Stops_At_The_Per_User_Cap()
    {
        var client = await AuthenticatedClientAsync("cv.cap@example.com");

        for (var index = 0; index < CvDocument.MaxPerUser; index++)
        {
            await UploadAsync(client, PdfBytes, $"cv-{index}.pdf");
        }

        using var content = FileContent(PdfBytes, "one-too-many.pdf");
        var response = await client.PostAsync("/api/cv-documents", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain(CvDocument.MaxPerUser.ToString());

        var list = await client.GetFromJsonAsync<CvDocumentListResponse>("/api/cv-documents", JsonOptions);
        list!.Items.Count.ShouldBe(CvDocument.MaxPerUser);
    }

    [Fact]
    public async Task One_User_Cannot_Reach_Another_Users_Cv()
    {
        var owner = await AuthenticatedClientAsync("cv.owner@example.com");
        var stranger = await AuthenticatedClientAsync("cv.stranger@example.com");

        var created = await UploadAsync(owner, PdfBytes, "private.pdf");

        // Not found, not forbidden — a stranger learns nothing about whether the id exists.
        (await stranger.GetAsync($"/api/cv-documents/{created.Id}/content")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await stranger.DeleteAsync($"/api/cv-documents/{created.Id}")).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/cv-documents/{created.Id}/default", content: null)).StatusCode
            .ShouldBe(HttpStatusCode.NotFound);

        var strangerList = await stranger.GetFromJsonAsync<CvDocumentListResponse>("/api/cv-documents", JsonOptions);
        strangerList!.Items.ShouldBeEmpty();

        // And the owner still has it.
        (await owner.GetAsync($"/api/cv-documents/{created.Id}/content")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Anonymous_Callers_Are_Rejected()
    {
        var owner = await AuthenticatedClientAsync("cv.anon.owner@example.com");
        var created = await UploadAsync(owner, PdfBytes, "cv.pdf");

        var anonymous = _factory!.CreateClient();

        (await anonymous.GetAsync("/api/cv-documents")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync($"/api/cv-documents/{created.Id}/content")).StatusCode
            .ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_First_Upload_Becomes_The_Default_And_Setting_One_Clears_The_Other()
    {
        var client = await AuthenticatedClientAsync("cv.default@example.com");

        var first = await UploadAsync(client, PdfBytes, "first.pdf");
        first.IsDefault.ShouldBeTrue();

        var second = await UploadAsync(client, DocxBytes, "second.docx");
        second.IsDefault.ShouldBeFalse();

        var setDefault = await client.PostAsync($"/api/cv-documents/{second.Id}/default", content: null);
        setDefault.EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<CvDocumentListResponse>("/api/cv-documents", JsonOptions);
        list!.Items.Single(item => item.Id == second.Id).IsDefault.ShouldBeTrue();
        list.Items.Single(item => item.Id == first.Id).IsDefault.ShouldBeFalse();
        // Exactly one, always.
        list.Items.Count(item => item.IsDefault).ShouldBe(1);
    }

    [Fact]
    public async Task Deleting_The_Default_Hands_The_Badge_To_The_Newest_Survivor()
    {
        var client = await AuthenticatedClientAsync("cv.default.delete@example.com");

        var first = await UploadAsync(client, PdfBytes, "first.pdf");
        var second = await UploadAsync(client, PdfBytes, "second.pdf");
        first.IsDefault.ShouldBeTrue();

        (await client.DeleteAsync($"/api/cv-documents/{first.Id}")).EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<CvDocumentListResponse>("/api/cv-documents", JsonOptions);
        list!.Items.Single().Id.ShouldBe(second.Id);
        list.Items.Single().IsDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task An_Application_Records_The_Cv_It_Was_Sent_With()
    {
        var client = await AuthenticatedClientAsync("cv.application@example.com");

        var cv = await UploadAsync(client, PdfBytes, "applied-with.pdf");

        var createResponse = await client.PostAsJsonAsync("/api/applications",
            new CreateApplicationRequest("Storage Co", "Backend Engineer", null, "Remote",
                EmploymentType.FullTime, DateTimeOffset.UtcNow, Source.Manual, null,
                CvDocumentId: cv.Id),
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();

        var application = await createResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);
        application!.CvDocumentId.ShouldBe(cv.Id);
        application.CvDocumentFileName.ShouldBe("applied-with.pdf");

        var list = await client.GetFromJsonAsync<CvDocumentListResponse>("/api/cv-documents", JsonOptions);
        list!.Items.Single().UsedByApplicationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Deleting_A_Cv_Clears_The_Reference_But_Keeps_The_Application()
    {
        var client = await AuthenticatedClientAsync("cv.application.delete@example.com");

        var cv = await UploadAsync(client, PdfBytes, "applied-with.pdf");

        var createResponse = await client.PostAsJsonAsync("/api/applications",
            new CreateApplicationRequest("Storage Co", "Backend Engineer", null, null,
                EmploymentType.FullTime, DateTimeOffset.UtcNow, Source.Manual, null,
                CvDocumentId: cv.Id),
            JsonOptions);
        var application = await createResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        (await client.DeleteAsync($"/api/cv-documents/{cv.Id}")).EnsureSuccessStatusCode();

        var reloaded = await client.GetFromJsonAsync<ApplicationDetailResponse>(
            $"/api/applications/{application!.Id}", JsonOptions);

        reloaded.ShouldNotBeNull();
        reloaded!.JobTitle.ShouldBe("Backend Engineer");
        reloaded.CvDocumentId.ShouldBeNull();
        reloaded.CvDocumentFileName.ShouldBeNull();
    }

    [Fact]
    public async Task An_Application_Cannot_Point_At_Someone_Elses_Cv()
    {
        var owner = await AuthenticatedClientAsync("cv.idor.owner@example.com");
        var stranger = await AuthenticatedClientAsync("cv.idor.stranger@example.com");

        var ownersCv = await UploadAsync(owner, PdfBytes, "not-yours.pdf");

        var createResponse = await stranger.PostAsJsonAsync("/api/applications",
            new CreateApplicationRequest("Storage Co", "Backend Engineer", null, null,
                EmploymentType.FullTime, DateTimeOffset.UtcNow, Source.Manual, null,
                CvDocumentId: ownersCv.Id),
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();

        var application = await createResponse.Content.ReadFromJsonAsync<ApplicationDetailResponse>(JsonOptions);

        // The application is created, but the reference is dropped — and, crucially, the owner's
        // file name never appears in the stranger's response.
        application!.CvDocumentId.ShouldBeNull();
        application.CvDocumentFileName.ShouldBeNull();
    }

    [Fact]
    public async Task Deleting_The_Account_Removes_Every_Cv_Row_And_File()
    {
        var client = await AuthenticatedClientAsync("cv.account.delete@example.com");

        await UploadAsync(client, PdfBytes, "one.pdf");
        await UploadAsync(client, DocxBytes, "two.docx");

        Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories).Length.ShouldBe(2);

        var deleteAccount = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest("P@ssw0rd123!"), options: JsonOptions)
        });
        deleteAccount.EnsureSuccessStatusCode();

        Directory.GetFiles(_storageRoot, "*", SearchOption.AllDirectories).ShouldBeEmpty();
    }

    [Fact]
    public async Task The_Data_Export_Lists_The_Stored_Cvs()
    {
        var client = await AuthenticatedClientAsync("cv.export@example.com");

        await UploadAsync(client, PdfBytes, "exported.pdf");

        var export = await client.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", JsonOptions);

        export.ShouldNotBeNull();
        export!.CvDocuments.ShouldNotBeNull();
        export.CvDocuments!.Single().FileName.ShouldBe("exported.pdf");
    }
}
