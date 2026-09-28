using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Blog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;
using Shouldly;

namespace AfterApply.IntegrationTests.Identity;

/// <summary>Profile photos and the blog images each in a scratch directory of the class's own.</summary>
public sealed class AvatarProfile() : LocalStorageProfile("avatars")
{
    public string AvatarRoot => Path.Combine(StorageRoot, "avatars");

    public override void Configure(IWebHostBuilder builder)
    {
        base.Configure(builder);
        builder.UseSetting("Storage:AvatarLocalRootPath", AvatarRoot);
        builder.UseSetting("Storage:BlogLocalRootPath", Path.Combine(StorageRoot, "blog-media"));
    }

    public string[] StoredAvatars() =>
        Directory.Exists(AvatarRoot) ? Directory.GetFiles(AvatarRoot, "*", SearchOption.AllDirectories) : [];
}

/// <summary>
/// Profile photos end to end (DECISIONS.md 2026-09-28): only the re-encoded photo is stored and
/// served, at a random address that changes with every upload and when the photo stops being shown;
/// blog comments carry it only when the author chose so; a moderator can take it away; the account
/// takes it with it.
/// </summary>
public class AvatarTests(ApiHost<AvatarProfile> host) : IClassFixture<ApiHost<AvatarProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private const string Doc = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Merhaba"}]}]}""";

    private WebApplicationFactory<Program> _factory => host;

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        host.Profile.Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- helpers --------------------------------------------------------------------------------

    private static byte[] JpegWithGps(int width = 640, int height = 480)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(30, 120, 200));
        image.Metadata.ExifProfile = new ExifProfile();
        image.Metadata.ExifProfile.SetValue(ExifTag.GPSLatitudeRef, "N");
        image.Metadata.ExifProfile.SetValue(ExifTag.GPSLatitude, [new Rational(41, 1), new Rational(0, 1), new Rational(0, 1)]);
        using var stream = new MemoryStream();
        image.Save(stream, new JpegEncoder());
        return stream.ToArray();
    }

    private static async Task<HttpResponseMessage> PutAvatarAsync(HttpClient client, byte[] bytes, string fileName = "me.jpg")
    {
        using var content = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } };
        return await client.PutAsync("/api/users/me/avatar", content);
    }

    private static async Task<UserProfileResponse> UploadAsync(HttpClient client, byte[]? bytes = null)
    {
        var response = await PutAvatarAsync(client, bytes ?? JpegWithGps());
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions))!;
    }

    private static async Task<UserProfileResponse> ShowInCommentsAsync(HttpClient client, bool show)
    {
        var response = await client.PutAsJsonAsync("/api/users/me/avatar/visibility", new UpdateAvatarVisibilityRequest(show), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions))!;
    }

    private async Task<HttpStatusCode> AnonymousGetStatusAsync(string url) =>
        (await _factory.CreateClient().GetAsync(url)).StatusCode;

    private static async Task<string[]> FileErrorsAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions))!;
        return problem.Errors["file"];
    }

    /// <summary>An admin, a published post, and an approved comment on it by <paramref name="reader"/>.</summary>
    private async Task<(HttpClient Admin, BlogCommentResponse Comment)> ApprovedCommentAsync(HttpClient reader)
    {
        var (admin, adminAuth) = await host.RegisterAsync($"admin.{Guid.NewGuid():N}@example.com");
        await host.MakeAdminAsync(adminAuth.User.Id);

        var created = await admin.PostAsJsonAsync("/api/admin/blog/posts",
            new CreateBlogPostRequest("Ghosting Üzerine", null, Doc, "<p>Merhaba</p>", "tr", null, null), JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var post = (await created.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
        (await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/publish", null)).EnsureSuccessStatusCode();

        var posted = await reader.PostAsJsonAsync($"/api/blog/posts/{post.Id}/comments",
            new CreateBlogCommentRequest("Geri dönüş tarihini sormak işe yaradı."), JsonOptions);
        posted.StatusCode.ShouldBe(HttpStatusCode.Created, await posted.Content.ReadAsStringAsync());
        var comment = (await posted.Content.ReadFromJsonAsync<BlogCommentResponse>(JsonOptions))!;
        (await admin.PostAsync($"/api/admin/blog/comments/{comment.Id}/approve", null)).EnsureSuccessStatusCode();
        return (admin, comment);
    }

    private async Task<BlogCommentResponse> PublicCommentAsync(BlogCommentResponse comment)
    {
        var list = (await _factory.CreateClient()
            .GetFromJsonAsync<BlogCommentListResponse>($"/api/blog/public/posts/{comment.PostId}/comments", JsonOptions))!;
        return list.Items.Single(c => c.Id == comment.Id);
    }

    // ---- upload and serve -----------------------------------------------------------------------

    [Fact]
    public async Task An_Upload_Is_Stored_Only_As_A_Clean_256_Webp_At_A_Random_Public_Address()
    {
        var (client, auth) = await host.RegisterAsync("avatar.upload@example.com");

        var profile = await UploadAsync(client);

        profile.AvatarUrl.ShouldNotBeNull().ShouldStartWith("/api/avatars/");
        profile.AvatarUrl.ShouldNotContain(auth.User.Id.ToString());
        profile.ShowAvatarInComments.ShouldBeFalse();
        (await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions))!.AvatarUrl.ShouldBe(profile.AvatarUrl);

        // Readable without a token — a plain <img> sends none — as the re-encoded photo alone.
        var served = await _factory.CreateClient().GetAsync(profile.AvatarUrl);
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        served.Headers.CacheControl!.ToString().ShouldBe("public, max-age=86400");
        served.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        using var photo = Image.Load(await served.Content.ReadAsByteArrayAsync());
        photo.Metadata.DecodedImageFormat.ShouldBe(WebpFormat.Instance);
        photo.Size.ShouldBe(new Size(256, 256));
        photo.Metadata.ExifProfile.ShouldBeNull();

        host.Profile.StoredAvatars().Length.ShouldBe(1);
    }

    [Fact]
    public async Task A_New_Upload_Retires_The_Old_Address_And_The_Old_Bytes()
    {
        var (client, _) = await host.RegisterAsync("avatar.replace@example.com");
        var first = await UploadAsync(client);

        var second = await UploadAsync(client, JpegWithGps(300, 900));

        second.AvatarUrl.ShouldNotBe(first.AvatarUrl);
        (await AnonymousGetStatusAsync(first.AvatarUrl!)).ShouldBe(HttpStatusCode.NotFound);
        (await AnonymousGetStatusAsync(second.AvatarUrl!)).ShouldBe(HttpStatusCode.OK);
        host.Profile.StoredAvatars().Length.ShouldBe(1);
    }

    [Fact]
    public async Task What_Is_Not_A_Jpeg_Png_Or_Webp_Photo_Is_Refused_With_A_Reason_And_Nothing_Is_Stored()
    {
        var (client, _) = await host.RegisterAsync("avatar.refused@example.com");
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");

        using (var gif = new Image<Rgba32>(64, 64))
        using (var stream = new MemoryStream())
        {
            await gif.SaveAsync(stream, new GifEncoder());
            (await FileErrorsAsync(await PutAvatarAsync(client, stream.ToArray(), "me.gif")))
                .ShouldBe(["Only JPEG, PNG and WebP photos can be uploaded."]);
        }

        (await FileErrorsAsync(await PutAvatarAsync(client, "<svg onload=alert(1)>"u8.ToArray(), "me.jpg")))
            .ShouldBe(["Only JPEG, PNG and WebP photos can be uploaded."]);
        (await FileErrorsAsync(await PutAvatarAsync(client, [], "me.jpg"))).ShouldBe(["The photo is empty."]);
        (await FileErrorsAsync(await PutAvatarAsync(client, new byte[5 * 1024 * 1024 + 1], "me.jpg")))
            .ShouldBe(["The photo exceeds the 5 MB size limit."]);

        (await client.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions))!.AvatarUrl.ShouldBeNull();
        host.Profile.StoredAvatars().ShouldBeEmpty();
        (await PutAvatarAsync(_factory.CreateClient(), JpegWithGps())).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Removing_The_Photo_Deletes_It_And_Turns_Comment_Showing_Off()
    {
        var (client, _) = await host.RegisterAsync("avatar.remove@example.com");
        var uploaded = await UploadAsync(client);
        await ShowInCommentsAsync(client, true);

        var response = await client.DeleteAsync("/api/users/me/avatar");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = (await response.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOptions))!;
        profile.AvatarUrl.ShouldBeNull();
        profile.ShowAvatarInComments.ShouldBeFalse();
        (await AnonymousGetStatusAsync(uploaded.AvatarUrl!)).ShouldBe(HttpStatusCode.NotFound);
        host.Profile.StoredAvatars().ShouldBeEmpty();

        // Nothing to remove is not an error.
        (await client.DeleteAsync("/api/users/me/avatar")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---- blog comments ------------------------------------------------------------------------

    [Fact]
    public async Task A_Comment_Carries_The_Photo_Only_While_Its_Author_Chooses_To_Show_It()
    {
        var (reader, _) = await host.RegisterAsync("avatar.comments@example.com", firstName: "Selin", lastName: "Yılmaz");
        var uploaded = await UploadAsync(reader);
        var (_, comment) = await ApprovedCommentAsync(reader);

        // Off by default: a photo uploaded for the menu does not appear on the page.
        (await PublicCommentAsync(comment)).AuthorAvatarUrl.ShouldBeNull();

        var shown = await ShowInCommentsAsync(reader, true);
        shown.AvatarUrl.ShouldBe(uploaded.AvatarUrl);
        var onPage = await PublicCommentAsync(comment);
        onPage.AuthorName.ShouldBe("Selin Y.");
        onPage.AuthorAvatarUrl.ShouldBe(uploaded.AvatarUrl);

        // Turning it off takes back the address readers already loaded, not just future pages.
        var hidden = await ShowInCommentsAsync(reader, false);
        hidden.AvatarUrl.ShouldNotBe(uploaded.AvatarUrl);
        (await PublicCommentAsync(comment)).AuthorAvatarUrl.ShouldBeNull();
        (await AnonymousGetStatusAsync(uploaded.AvatarUrl!)).ShouldBe(HttpStatusCode.NotFound);
        (await AnonymousGetStatusAsync(hidden.AvatarUrl!)).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task A_Moderator_Removes_A_Reported_Photo_And_Closes_The_Photo_Reports_Only()
    {
        var (reader, _) = await host.RegisterAsync("avatar.reported@example.com");
        var uploaded = await UploadAsync(reader);
        await ShowInCommentsAsync(reader, true);
        var (admin, comment) = await ApprovedCommentAsync(reader);

        var (reporterA, _) = await host.RegisterAsync("avatar.reporter.a@example.com");
        var (reporterB, _) = await host.RegisterAsync("avatar.reporter.b@example.com");
        (await reporterA.PostAsJsonAsync($"/api/blog/comments/{comment.Id}/reports",
            new ReportBlogCommentRequest(BlogCommentReportReason.ProfilePhoto, null), JsonOptions)).EnsureSuccessStatusCode();
        (await reporterB.PostAsJsonAsync($"/api/blog/comments/{comment.Id}/reports",
            new ReportBlogCommentRequest(BlogCommentReportReason.Spam, null), JsonOptions)).EnsureSuccessStatusCode();

        var before = (await admin.GetFromJsonAsync<AdminBlogCommentResponse>($"/api/admin/blog/comments/{comment.Id}", JsonOptions))!;
        before.Comment.AuthorAvatarUrl.ShouldBe(uploaded.AvatarUrl);

        // A reader cannot do it.
        (await reporterA.PostAsync($"/api/admin/blog/comments/{comment.Id}/author-avatar/remove", null))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.PostAsync($"/api/admin/blog/comments/{Guid.NewGuid()}/author-avatar/remove", null))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var response = await admin.PostAsync($"/api/admin/blog/comments/{comment.Id}/author-avatar/remove", null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var after = (await response.Content.ReadFromJsonAsync<AdminBlogCommentResponse>(JsonOptions))!;
        after.Comment.AuthorAvatarUrl.ShouldBeNull();
        after.Comment.Status.ShouldBe(BlogCommentStatus.Approved);
        after.Reports.Single(r => r.Reason == BlogCommentReportReason.ProfilePhoto).Status.ShouldBe(BlogCommentReportStatus.ActionTaken);
        after.Reports.Single(r => r.Reason == BlogCommentReportReason.Spam).Status.ShouldBe(BlogCommentReportStatus.Open);

        var profile = (await reader.GetFromJsonAsync<UserProfileResponse>("/api/users/me", JsonOptions))!;
        profile.AvatarUrl.ShouldBeNull();
        profile.ShowAvatarInComments.ShouldBeFalse();
        (await AnonymousGetStatusAsync(uploaded.AvatarUrl!)).ShouldBe(HttpStatusCode.NotFound);
        (await PublicCommentAsync(comment)).AuthorAvatarUrl.ShouldBeNull();
        host.Profile.StoredAvatars().ShouldBeEmpty();
    }

    // ---- the account ----------------------------------------------------------------------------

    [Fact]
    public async Task The_Export_Names_The_Photo_And_Deleting_The_Account_Deletes_It()
    {
        var (client, _) = await host.RegisterAsync("avatar.account@example.com");
        var uploaded = await UploadAsync(client);
        await ShowInCommentsAsync(client, true);

        var export = (await client.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", JsonOptions))!;
        export.Profile.AvatarUrl.ShouldBe(uploaded.AvatarUrl);
        export.Profile.ShowAvatarInComments.ShouldBeTrue();

        var delete = new HttpRequestMessage(HttpMethod.Delete, "/api/users/me")
        {
            Content = JsonContent.Create(new DeleteAccountRequest(ApiHost.DefaultPassword), options: JsonOptions)
        };
        (await client.SendAsync(delete)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await AnonymousGetStatusAsync(uploaded.AvatarUrl!)).ShouldBe(HttpStatusCode.NotFound);
        host.Profile.StoredAvatars().ShouldBeEmpty();
    }

    [Fact]
    public async Task Photo_Writes_Have_Their_Own_Per_Account_Limit()
    {
        // The suite disables rate limiting for every host; this test opts back in on a host of its
        // own (a limiter's fixed windows have no reset).
        await using var limited = host.Standalone(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:AvatarWrite:PermitLimit", "2");
        });
        var (client, _) = await host.RegisterAsync("avatar.limit@example.com", on: limited);

        (await client.PutAsJsonAsync("/api/users/me/avatar/visibility", new UpdateAvatarVisibilityRequest(true), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.DeleteAsync("/api/users/me/avatar")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PutAvatarAsync(client, JpegWithGps())).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }
}
