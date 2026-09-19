using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.ClientConfig;
using AfterApply.Domain.Blog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.Blog;

/// <summary>Local storage for the blog images, in a directory of the class's own.</summary>
public sealed class BlogProfile() : LocalStorageProfile("blog")
{
    public string BlogMediaRoot => Path.Combine(StorageRoot, "blog-media");

    public override void Configure(IWebHostBuilder builder)
    {
        base.Configure(builder);
        builder.UseSetting("Storage:BlogLocalRootPath", BlogMediaRoot);
        // Two per page so paging is testable with three posts.
        builder.UseSetting("Blog:PageSize", "2");
    }
}

/// <summary>
/// The blog end to end (DECISIONS.md 2026-09-19): admin-only writing, author-only drafts, the
/// two-slot publish model (an autosave never reaches the public page), the revision check,
/// slug rules, public reading that never answers 401, likes, images whose visibility follows
/// their post, the config flag, cache eviction across instances and the feature switch.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class BlogTests(ApiHost<BlogProfile> host) : IClassFixture<ApiHost<BlogProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private const string Doc = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Merhaba"}]}]}""";

    // A PNG header with a 640×480 IHDR — enough for the format and dimension checks; the body
    // is never decoded.
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R',
        0, 0, 0x02, 0x80, 0, 0, 0x01, 0xE0, 8, 6, 0, 0, 0, 0, 0, 0, 0
    ];

    private WebApplicationFactory<Program> _factory => host;

    private WebApplicationFactory<Program> _off => host.Variant("blog-off", b => b.UseSetting("Blog:Enabled", "false"));

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        host.Profile.Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- helpers --------------------------------------------------------------------------------

    private async Task<(HttpClient Client, Guid UserId)> RegisterAdminAsync(string email, WebApplicationFactory<Program>? on = null)
    {
        var (client, auth) = await host.RegisterAsync(email, on: on);
        await host.MakeAdminAsync(auth.User.Id);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return (client, auth.User.Id);
    }

    private async Task<HttpClient> RegisterUserAsync(string email, WebApplicationFactory<Program>? on = null)
    {
        var (client, _) = await host.RegisterAsync(email, on: on);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return client;
    }

    private static async Task<AdminBlogPostResponse> CreateAsync(HttpClient admin, string language = "tr")
    {
        var response = await admin.PostAsJsonAsync("/api/admin/blog/posts", new CreateBlogPostRequest(language), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
    }

    private static SaveBlogDraftRequest Draft(AdminBlogPostResponse post, string title = "İşe Alım Sürecinde Ghosting",
        string html = "<p>Merhaba</p>", string? slug = null, string? language = null, Guid? translationOf = null,
        Guid? cover = null, int? revision = null) =>
        new(title, "Özet", Doc, html, language ?? post.Language, slug ?? post.Slug, cover ?? post.CoverMediaId,
            translationOf ?? post.TranslationOfPostId, revision ?? post.Revision);

    private static async Task<BlogDraftSavedResponse> SaveAsync(HttpClient admin, Guid postId, SaveBlogDraftRequest request)
    {
        var response = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{postId}/draft", request, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BlogDraftSavedResponse>(JsonOptions))!;
    }

    private static async Task<AdminBlogPostResponse> PublishAsync(HttpClient admin, Guid postId)
    {
        var response = await admin.PostAsync($"/api/admin/blog/posts/{postId}/publish", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
    }

    private static async Task<AdminBlogPostResponse> GetAdminAsync(HttpClient admin, Guid postId)
    {
        var response = await admin.GetAsync($"/api/admin/blog/posts/{postId}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
    }

    /// <summary>A published post with title, body and a generated slug.</summary>
    private async Task<AdminBlogPostResponse> PublishedPostAsync(HttpClient admin, string title = "İşe Alım Sürecinde Ghosting",
        string language = "tr")
    {
        var post = await CreateAsync(admin, language);
        await SaveAsync(admin, post.Id, Draft(post, title));
        return await PublishAsync(admin, post.Id);
    }

    private async Task<HttpResponseMessage> GetPublicAsync(string language, string slug, HttpClient? client = null) =>
        await (client ?? _factory.CreateClient()).GetAsync($"/api/blog/public/posts/{language}/{slug}");

    private static async Task<BlogMediaResponse> UploadAsync(HttpClient admin, Guid postId, byte[] bytes, string fileName = "photo.png")
    {
        using var content = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } };
        var response = await admin.PostAsync($"/api/admin/blog/posts/{postId}/media", content);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BlogMediaResponse>(JsonOptions))!;
    }

    private static async Task<string> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        return problem!.Detail ?? string.Empty;
    }

    // ---- Access -------------------------------------------------------------------------------

    [Fact]
    public async Task Admin_Routes_Are_403_For_A_Signed_In_Non_Admin_And_401_Anonymous()
    {
        var user = await RegisterUserAsync("reader.blog@example.com");

        (await user.GetAsync("/api/admin/blog/posts")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await user.PostAsJsonAsync("/api/admin/blog/posts", new CreateBlogPostRequest("tr"), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync("/api/admin/blog/posts")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Drafts and publishing ----------------------------------------------------------------

    [Fact]
    public async Task A_Draft_Is_Saved_With_A_Rising_Revision_And_A_Stale_One_Is_409()
    {
        var (admin, _) = await RegisterAdminAsync("author.blog@example.com");
        var post = await CreateAsync(admin);
        post.Revision.ShouldBe(1);
        post.Status.ShouldBe(BlogPostStatus.Draft);

        var saved = await SaveAsync(admin, post.Id, Draft(post));
        saved.Revision.ShouldBe(2);

        // The same tab, saving again from what it last saw: fine.
        var again = await SaveAsync(admin, post.Id, Draft(post, title: "v3", revision: 2));
        again.Revision.ShouldBe(3);

        // A second tab still holding revision 2: refused, and nothing changed.
        var stale = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{post.Id}/draft", Draft(post, title: "stale", revision: 2), JsonOptions);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await GetAdminAsync(admin, post.Id)).DraftTitle.ShouldBe("v3");

        // Every autosave is a write like any other: it leaves an audit row (no opt-out).
        await host.WithDbAsync(async db =>
            (await db.RequestAudits.CountAsync(a => a.Path == $"/api/admin/blog/posts/{post.Id}/draft" && a.Method == "PUT"))
                .ShouldBe(3));
    }

    [Fact]
    public async Task The_Html_Is_Sanitized_On_Save()
    {
        var (admin, _) = await RegisterAdminAsync("sanitize.blog@example.com");
        var post = await CreateAsync(admin);

        await SaveAsync(admin, post.Id, Draft(post, html: "<p onclick=\"x()\">ok</p><script>alert(1)</script><img src=\"https://evil.example/a.png\">"));

        var stored = await GetAdminAsync(admin, post.Id);
        stored.DraftContentHtml.ShouldBe("<p>ok</p>");
    }

    [Fact]
    public async Task Publish_Copies_The_Draft_Generates_The_Slug_And_A_Later_Autosave_Stays_Private()
    {
        var (admin, _) = await RegisterAdminAsync("publish.blog@example.com");
        var post = await CreateAsync(admin);
        await SaveAsync(admin, post.Id, Draft(post));

        // Not on the site before publish, nor visible through the config flag.
        (await _factory.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions))!
            .Blog.ShouldBe(new BlogConfigResponse(true, false));

        var published = await PublishAsync(admin, post.Id);
        published.Status.ShouldBe(BlogPostStatus.Published);
        published.Slug.ShouldBe("ise-alim-surecinde-ghosting");
        published.PublishedAt.ShouldNotBeNull();
        published.HasUnpublishedChanges.ShouldBeFalse();

        var response = await GetPublicAsync("tr", "ise-alim-surecinde-ghosting");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        page.Title.ShouldBe("İşe Alım Sürecinde Ghosting");
        page.ContentHtml.ShouldBe("<p>Merhaba</p>");
        page.LikeCount.ShouldBe(0);
        page.LikedByMe.ShouldBeNull();

        // Keep typing: the public page does not move until the next publish.
        await SaveAsync(admin, post.Id, Draft(published, title: "Yarım kalmış", html: "<p>Yeni</p>"));
        (await GetAdminAsync(admin, post.Id)).HasUnpublishedChanges.ShouldBeTrue();
        var stillOld = (await (await GetPublicAsync("tr", "ise-alim-surecinde-ghosting")).Content
            .ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        stillOld.Title.ShouldBe("İşe Alım Sürecinde Ghosting");
        stillOld.ContentHtml.ShouldBe("<p>Merhaba</p>");

        var updated = await PublishAsync(admin, post.Id);
        updated.PublishedAt.ShouldBe(published.PublishedAt);
        updated.PublishedUpdatedAt!.Value.ShouldBeGreaterThan(published.PublishedUpdatedAt!.Value);
        (await (await GetPublicAsync("tr", "ise-alim-surecinde-ghosting")).Content
            .ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!.Title.ShouldBe("Yarım kalmış");

        // And the config flag now says there is a blog.
        (await _factory.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions))!
            .Blog.ShouldBe(new BlogConfigResponse(true, true));
    }

    [Fact]
    public async Task Publish_Needs_A_Title_And_A_Body()
    {
        var (admin, _) = await RegisterAdminAsync("incomplete.blog@example.com");
        var post = await CreateAsync(admin);

        var response = await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/publish", null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).ShouldContain("title");
    }

    [Fact]
    public async Task Unpublish_Takes_The_Post_Off_The_Site_And_Republish_Keeps_The_Url()
    {
        var (admin, _) = await RegisterAdminAsync("unpublish.blog@example.com");
        var post = await PublishedPostAsync(admin);

        var response = await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/unpublish", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetPublicAsync("tr", post.Slug!)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _factory.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions))!
            .Blog!.HasPublishedPosts.ShouldBeFalse();

        // Unpublishing twice is a 400, not a silent no-op.
        (await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/unpublish", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var back = await PublishAsync(admin, post.Id);
        back.Slug.ShouldBe(post.Slug);
        back.PublishedAt.ShouldBe(post.PublishedAt);
        (await GetPublicAsync("tr", post.Slug!)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Slug_And_Language_Are_Locked_After_The_First_Publish()
    {
        var (admin, _) = await RegisterAdminAsync("locked.blog@example.com");
        var post = await PublishedPostAsync(admin);

        var slugChange = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{post.Id}/draft", Draft(post, slug: "baska-adres"), JsonOptions);
        slugChange.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var languageChange = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{post.Id}/draft", Draft(post, language: "en"), JsonOptions);
        languageChange.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Sending them back unchanged — or the slug blank — is what the editor does every save.
        await SaveAsync(admin, post.Id, Draft(post));
        await SaveAsync(admin, post.Id, Draft(post, slug: "", revision: post.Revision + 1));
        (await GetAdminAsync(admin, post.Id)).Slug.ShouldBe(post.Slug);
    }

    [Fact]
    public async Task A_Hand_Typed_Slug_Is_Used_And_A_Taken_One_Is_Refused_Per_Language()
    {
        var (admin, _) = await RegisterAdminAsync("slug.blog@example.com");
        var first = await CreateAsync(admin);
        await SaveAsync(admin, first.Id, Draft(first, slug: "ozel-adres"));
        (await PublishAsync(admin, first.Id)).Slug.ShouldBe("ozel-adres");

        var second = await CreateAsync(admin);
        var taken = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{second.Id}/draft", Draft(second, slug: "ozel-adres"), JsonOptions);
        taken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The same slug in the other language is a different URL.
        var english = await CreateAsync(admin, "en");
        await SaveAsync(admin, english.Id, Draft(english, title: "Special", slug: "ozel-adres"));
        (await PublishAsync(admin, english.Id)).Slug.ShouldBe("ozel-adres");

        // Two posts with the same title in one language: the generator suffixes the second.
        var twin = await PublishedPostAsync(admin, title: "İşe Alım Sürecinde Ghosting");
        var twin2 = await PublishedPostAsync(admin, title: "İşe Alım Sürecinde Ghosting");
        twin.Slug.ShouldBe("ise-alim-surecinde-ghosting");
        twin2.Slug.ShouldBe("ise-alim-surecinde-ghosting-2");
    }

    // ---- Visibility between admins -------------------------------------------------------------

    [Fact]
    public async Task Another_Admin_Cannot_See_My_Draft_But_Can_Edit_My_Published_Post()
    {
        var (author, _) = await RegisterAdminAsync("author2.blog@example.com");
        var (other, _) = await RegisterAdminAsync("other.blog@example.com");
        var draft = await CreateAsync(author);
        await SaveAsync(author, draft.Id, Draft(draft));

        // 404 on every admin route, and absent from the list — not 403, which would confirm it exists.
        (await other.GetAsync($"/api/admin/blog/posts/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PutAsJsonAsync($"/api/admin/blog/posts/{draft.Id}/draft", Draft(draft, revision: 2), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/admin/blog/posts/{draft.Id}/publish", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.DeleteAsync($"/api/admin/blog/posts/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var otherList = (await other.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>("/api/admin/blog/posts", JsonOptions))!;
        otherList.Items.ShouldBeEmpty();

        var authorList = (await author.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>("/api/admin/blog/posts", JsonOptions))!;
        authorList.Items.ShouldHaveSingleItem().IsMine.ShouldBeTrue();

        // Once published it is everyone's.
        await PublishAsync(author, draft.Id);
        var seen = await GetAdminAsync(other, draft.Id);
        seen.IsMine.ShouldBeFalse();
        seen.AuthorUserId.ShouldNotBeNull();
        (await other.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>("/api/admin/blog/posts", JsonOptions))!
            .Items.ShouldHaveSingleItem().AuthorEmail.ShouldBe("author2.blog@example.com");
        await SaveAsync(other, draft.Id, Draft(seen, title: "Edited by another admin"));
        (await PublishAsync(other, draft.Id)).PublishedTitle.ShouldBe("Edited by another admin");

        // And unpublished, it is the author's alone again.
        (await other.PostAsync($"/api/admin/blog/posts/{draft.Id}/unpublish", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await other.GetAsync($"/api/admin/blog/posts/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await author.GetAsync($"/api/admin/blog/posts/{draft.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Translations_Link_Both_Ways_And_Must_Be_The_Other_Language()
    {
        var (admin, _) = await RegisterAdminAsync("translate.blog@example.com");
        var tr = await PublishedPostAsync(admin, "Türkçe yazı", "tr");
        var en = await PublishedPostAsync(admin, "English post", "en");
        var tr2 = await PublishedPostAsync(admin, "İkinci Türkçe", "tr");

        var sameLanguage = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{tr.Id}/draft", Draft(tr, translationOf: tr2.Id), JsonOptions);
        sameLanguage.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await SaveAsync(admin, tr.Id, Draft(tr, translationOf: en.Id));
        (await GetAdminAsync(admin, en.Id)).TranslationOfPostId.ShouldBe(tr.Id);

        var page = (await (await GetPublicAsync("en", en.Slug!)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        page.Translation.ShouldBe(new BlogTranslationLink("tr", tr.Slug!));
        var slugs = (await _factory.CreateClient().GetFromJsonAsync<List<BlogSlugResponse>>("/api/blog/public/slugs", JsonOptions))!;
        slugs.Single(s => s.Slug == tr.Slug).Translation.ShouldBe(new BlogTranslationLink("en", en.Slug!));

        // Unlinking one side unlinks the other.
        var trNow = await GetAdminAsync(admin, tr.Id);
        await SaveAsync(admin, tr.Id, Draft(trNow, translationOf: null) with { TranslationOfPostId = null });
        (await GetAdminAsync(admin, en.Id)).TranslationOfPostId.ShouldBeNull();
    }

    // ---- Public reading -------------------------------------------------------------------------

    [Fact]
    public async Task Public_List_Is_Per_Language_Newest_First_And_Paged()
    {
        var (admin, _) = await RegisterAdminAsync("list.blog@example.com");
        var a = await PublishedPostAsync(admin, "Birinci", "tr");
        var b = await PublishedPostAsync(admin, "İkinci", "tr");
        var c = await PublishedPostAsync(admin, "Üçüncü", "tr");
        await PublishedPostAsync(admin, "English", "en");
        var draft = await CreateAsync(admin);
        await SaveAsync(admin, draft.Id, Draft(draft, "Taslak"));

        var anonymous = _factory.CreateClient();
        var page1 = (await anonymous.GetFromJsonAsync<PagedResult<BlogPostListItemResponse>>("/api/blog/public/posts?lang=tr", JsonOptions))!;
        page1.TotalCount.ShouldBe(3);
        page1.PageSize.ShouldBe(2);
        page1.Items.Select(i => i.Slug).ShouldBe([c.Slug, b.Slug]);
        var page2 = (await anonymous.GetFromJsonAsync<PagedResult<BlogPostListItemResponse>>("/api/blog/public/posts?lang=tr&page=2", JsonOptions))!;
        page2.Items.Select(i => i.Slug).ShouldBe([a.Slug]);

        (await anonymous.GetFromJsonAsync<PagedResult<BlogPostListItemResponse>>("/api/blog/public/posts?lang=en", JsonOptions))!
            .TotalCount.ShouldBe(1);
        (await anonymous.GetAsync("/api/blog/public/posts?lang=de")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await anonymous.GetAsync("/api/blog/public/posts")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // The slug feed has the four published posts and not the draft.
        (await anonymous.GetFromJsonAsync<List<BlogSlugResponse>>("/api/blog/public/slugs", JsonOptions))!.Count.ShouldBe(4);
    }

    [Fact]
    public async Task Public_Routes_Never_Answer_401_And_Personalise_Only_With_A_Valid_Token()
    {
        var (admin, _) = await RegisterAdminAsync("public.blog@example.com");
        var post = await PublishedPostAsync(admin);

        var stale = _factory.CreateClient();
        stale.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");
        var response = await GetPublicAsync("tr", post.Slug!, stale);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!.LikedByMe.ShouldBeNull();

        var reader = await RegisterUserAsync("liker.blog@example.com");
        (await (await GetPublicAsync("tr", post.Slug!, reader)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!
            .LikedByMe.ShouldBe(false);

        // The wrong language for a slug is a 404, not a redirect.
        (await GetPublicAsync("en", post.Slug!)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Likes ----------------------------------------------------------------------------------

    [Fact]
    public async Task Likes_Toggle_Per_Account_Need_A_Login_And_Reach_The_Public_Count()
    {
        var (admin, _) = await RegisterAdminAsync("likes.blog@example.com");
        var post = await PublishedPostAsync(admin);
        var reader = await RegisterUserAsync("reader2.blog@example.com");

        (await _factory.CreateClient().PostAsync($"/api/blog/posts/{post.Id}/like", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var on = await reader.PostAsync($"/api/blog/posts/{post.Id}/like", null);
        on.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await on.Content.ReadFromJsonAsync<BlogLikeToggleResponse>(JsonOptions)).ShouldBe(new BlogLikeToggleResponse(true, 1));

        var page = (await (await GetPublicAsync("tr", post.Slug!, reader)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        page.LikeCount.ShouldBe(1);
        page.LikedByMe.ShouldBe(true);
        (await _factory.CreateClient().GetFromJsonAsync<PagedResult<BlogPostListItemResponse>>("/api/blog/public/posts?lang=tr", JsonOptions))!
            .Items.Single().LikeCount.ShouldBe(1);

        var off = await reader.PostAsync($"/api/blog/posts/{post.Id}/like", null);
        (await off.Content.ReadFromJsonAsync<BlogLikeToggleResponse>(JsonOptions)).ShouldBe(new BlogLikeToggleResponse(false, 0));

        // A draft is not likeable — it is not there.
        var draft = await CreateAsync(admin);
        (await reader.PostAsync($"/api/blog/posts/{draft.Id}/like", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Likes_Have_Their_Own_Rate_Limit()
    {
        // The suite disables rate limiting for every host; this test opts back in on a host of
        // its own (a limiter's fixed windows have no reset).
        await using var limited = host.Standalone(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:BlogLike:PermitLimit", "3");
        });
        var (admin, _) = await RegisterAdminAsync("limit.blog@example.com", limited);
        var post = await PublishedPostAsync(admin);
        var reader = await RegisterUserAsync("limited.reader@example.com", limited);

        HttpResponseMessage? last = null;
        for (var i = 0; i < 4; i++)
        {
            last = await reader.PostAsync($"/api/blog/posts/{post.Id}/like", null);
        }

        last!.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // ---- Media ----------------------------------------------------------------------------------

    [Fact]
    public async Task An_Image_Follows_Its_Post_From_Author_Only_To_Public_And_Is_Gone_With_It()
    {
        var (author, _) = await RegisterAdminAsync("media.blog@example.com");
        var (other, _) = await RegisterAdminAsync("media.other@example.com");
        var post = await CreateAsync(author);

        var media = await UploadAsync(author, post.Id, PngBytes);
        media.Url.ShouldBe($"/api/blog/media/{media.Id}");
        media.ContentType.ShouldBe("image/png");
        media.Width.ShouldBe(640);
        media.Height.ShouldBe(480);
        var storedPath = Path.Combine(host.Profile.BlogMediaRoot, "blog", post.Id.ToString("D"), $"{media.Id:D}.png");
        File.Exists(storedPath).ShouldBeTrue();

        // Draft: the author sees it, privately; nobody else does.
        var mine = await author.GetAsync(media.Url);
        mine.StatusCode.ShouldBe(HttpStatusCode.OK);
        mine.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        mine.Headers.CacheControl!.Private.ShouldBeTrue();
        mine.Headers.CacheControl.NoStore.ShouldBeTrue();
        mine.Content.Headers.ContentDisposition.ShouldBeNull(); // inline
        (await _factory.CreateClient().GetAsync(media.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.GetAsync(media.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await other.PostAsync($"/api/admin/blog/posts/{post.Id}/media",
            new MultipartFormDataContent { { new ByteArrayContent(PngBytes), "file", "x.png" } })).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The image survives sanitisation as our own relative path, and can be the cover.
        await SaveAsync(author, post.Id, Draft(post, html: $"<p>x</p><img src=\"http://localhost{media.Url}\" alt=\"a\">", cover: media.Id));
        (await GetAdminAsync(author, post.Id)).DraftContentHtml.ShouldContain($"src=\"{media.Url}\"");

        // Published: public and immutable.
        await PublishAsync(author, post.Id);
        var everyone = await _factory.CreateClient().GetAsync(media.Url);
        everyone.StatusCode.ShouldBe(HttpStatusCode.OK);
        everyone.Headers.CacheControl!.Public.ShouldBeTrue();
        everyone.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromDays(365));
        (await everyone.Content.ReadAsByteArrayAsync()).ShouldBe(PngBytes);
        var page = (await (await GetPublicAsync("tr", "ise-alim-surecinde-ghosting")).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        page.CoverImageUrl.ShouldBe(media.Url);

        // Deleted with the post — rows and bytes.
        (await author.DeleteAsync($"/api/admin/blog/posts/{post.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _factory.CreateClient().GetAsync(media.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        File.Exists(storedPath).ShouldBeFalse();
        (await GetPublicAsync("tr", "ise-alim-surecinde-ghosting")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Uploads_Are_Judged_By_Their_Bytes()
    {
        var (admin, _) = await RegisterAdminAsync("bytes.blog@example.com");
        var post = await CreateAsync(admin);

        // An executable named .png.
        using var exe = new MultipartFormDataContent { { new ByteArrayContent("MZ\x90\0\x03\0\0\0"u8.ToArray()), "file", "photo.png" } };
        var refused = await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/media", exe);
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await refused.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
        problem!.Errors.ShouldContainKey("file");
        problem.Errors["file"].Single().ShouldContain("PNG");

        // An SVG is never an image here.
        using var svg = new MultipartFormDataContent { { new ByteArrayContent("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray()), "file", "logo.svg" } };
        (await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/media", svg)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Over the cap.
        using var big = new MultipartFormDataContent { { new ByteArrayContent(new byte[5 * 1024 * 1024 + 1]), "file", "big.png" } };
        (await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/media", big)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // A PNG named .exe is a PNG.
        (await UploadAsync(admin, post.Id, PngBytes, "photo.exe")).ContentType.ShouldBe("image/png");
        Directory.GetFiles(host.Profile.BlogMediaRoot, "*", SearchOption.AllDirectories).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_Cover_Must_Be_One_Of_The_Posts_Own_Images()
    {
        var (admin, _) = await RegisterAdminAsync("cover.blog@example.com");
        var post = await CreateAsync(admin);
        var otherPost = await CreateAsync(admin);
        var foreign = await UploadAsync(admin, otherPost.Id, PngBytes);

        var response = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{post.Id}/draft", Draft(post, cover: foreign.Id), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Caching across instances ---------------------------------------------------------------

    [Fact]
    public async Task A_Publish_On_One_Instance_Is_Seen_On_Another_At_Once()
    {
        var second = host.Variant("second-instance", _ => { });
        var (admin, _) = await RegisterAdminAsync("cache.blog@example.com");
        var post = await PublishedPostAsync(admin);

        // Warm the second instance's cache with the published page and the flag.
        var reader = second.CreateClient();
        (await reader.GetAsync($"/api/blog/public/posts/tr/{post.Slug}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await reader.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions))!.Blog!.HasPublishedPosts.ShouldBeTrue();

        // Unpublish on the first: the second answers from a fresh read, not from its L1.
        (await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/unpublish", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await reader.GetAsync($"/api/blog/public/posts/tr/{post.Slug}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await reader.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions))!.Blog!.HasPublishedPosts.ShouldBeFalse();
    }

    // ---- Feature switch ---------------------------------------------------------------------------

    [Fact]
    public async Task With_The_Flag_Off_Every_Route_Is_404_And_Config_Says_So()
    {
        var (admin, _) = await RegisterAdminAsync("off.blog@example.com");
        var post = await PublishedPostAsync(admin);
        var media = await UploadAsync(admin, post.Id, PngBytes);

        var offAdmin = _off.CreateClient();
        offAdmin.DefaultRequestHeaders.Authorization = admin.DefaultRequestHeaders.Authorization;
        var offAnonymous = _off.CreateClient();

        (await offAnonymous.GetAsync("/api/blog/public/posts?lang=tr")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await offAnonymous.GetAsync($"/api/blog/public/posts/tr/{post.Slug}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await offAnonymous.GetAsync("/api/blog/public/slugs")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await offAnonymous.GetAsync(media.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await offAdmin.GetAsync("/api/admin/blog/posts")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await offAdmin.PostAsync($"/api/blog/posts/{post.Id}/like", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await offAnonymous.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions))!
            .Blog.ShouldBe(new BlogConfigResponse(false, false));
    }
}
