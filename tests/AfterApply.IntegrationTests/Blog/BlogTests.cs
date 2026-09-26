using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.ClientConfig;
using AfterApply.Domain.Blog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using AfterApply.Infrastructure.Persistence.Migrations;
using Microsoft.Extensions.DependencyInjection;
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
        // Two per page so paging is testable with three posts — the admin table too.
        builder.UseSetting("Blog:PageSize", "2");
        builder.UseSetting("Blog:AdminPageSize", "2");
        // No grace: an orphan is collected on the very next save, so the test does not wait.
        builder.UseSetting("Blog:OrphanMediaGraceMinutes", "0");
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

    /// <summary>
    /// The slack for comparing a timestamp the API answered from memory with the same one read
    /// back from Postgres. `DateTimeOffset.UtcNow` has 100 ns ticks on Linux (µs-aligned on
    /// macOS, which is why this never showed locally) and timestamptz keeps microseconds, so a
    /// value that made the round trip can be up to 900 ns short of the one that did not. Exact
    /// equality failed three of these on the 2026-09-19 deploy run and passed the same code on
    /// the PR run — a coin toss, not a bug in the dates.
    /// </summary>
    private static readonly TimeSpan StoredClock = TimeSpan.FromMilliseconds(1);

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

    /// <summary>A post from its first draft — a title and nothing else, the least the API accepts.</summary>
    private static async Task<AdminBlogPostResponse> CreateAsync(HttpClient admin, string language = "tr")
    {
        var response = await admin.PostAsJsonAsync("/api/admin/blog/posts", NewPost(language), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
    }

    private static CreateBlogPostRequest NewPost(string language = "tr", string title = "Taslak", string? excerpt = null,
        string html = "", string? slug = null, Guid? translationOf = null) =>
        new(title, excerpt, Doc, html, language, slug, translationOf);

    private static SaveBlogDraftRequest Draft(AdminBlogPostResponse post, string title = "İşe Alım Sürecinde Ghosting",
        string html = "<p>Merhaba</p>", string? slug = null, string? language = null, Guid? translationOf = null,
        Guid? cover = null, int? revision = null, BlogSeoRequest? seo = null) =>
        new(title, "Özet", Doc, html, language ?? post.Language, slug ?? post.Slug, cover ?? post.CoverMediaId,
            translationOf ?? post.TranslationOfPostId, revision ?? post.Revision, seo);

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
        (await user.PostAsJsonAsync("/api/admin/blog/posts", NewPost(), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync("/api/admin/blog/posts")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Create -------------------------------------------------------------------------------

    [Fact]
    public async Task Create_Refuses_A_Draft_With_Nothing_Written_So_An_Abandoned_Editor_Leaves_No_Post()
    {
        var admin = (await RegisterAdminAsync("create.empty@example.com")).Client;
        var before = (await admin.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>("/api/admin/blog/posts", JsonOptions))!.TotalCount;

        var empty = await admin.PostAsJsonAsync("/api/admin/blog/posts", NewPost(title: "", excerpt: "  ", html: "<p></p>"), JsonOptions);

        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await empty.Content.ReadAsStringAsync()).ShouldContain("Write something");
        (await admin.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>("/api/admin/blog/posts", JsonOptions))!
            .TotalCount.ShouldBe(before);
    }

    [Fact]
    public async Task Create_Stores_The_First_Draft_And_Hands_Back_The_Revision_The_Autosave_Continues_From()
    {
        var admin = (await RegisterAdminAsync("create.first@example.com")).Client;

        var response = await admin.PostAsJsonAsync("/api/admin/blog/posts",
            NewPost("en", title: "", excerpt: null, html: "<p>a</p><script>alert(1)</script>", slug: "first-words"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var post = (await response.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
        post.Status.ShouldBe(BlogPostStatus.Draft);
        post.Language.ShouldBe("en");
        post.Slug.ShouldBe("first-words");
        post.DraftTitle.ShouldBe("");
        post.DraftContentHtml.ShouldBe("<p>a</p>");
        post.DraftContentJson.ShouldBe(Doc);
        post.IsMine.ShouldBeTrue();

        // The revision the create returned is the one the first autosave must send.
        var saved = await SaveAsync(admin, post.Id, Draft(post, title: "First words"));
        saved.Revision.ShouldBe(post.Revision + 1);
    }

    [Fact]
    public async Task Create_Refuses_A_Body_The_Sanitizer_Empties()
    {
        var admin = (await RegisterAdminAsync("create.sanitized@example.com")).Client;

        var response = await admin.PostAsJsonAsync("/api/admin/blog/posts", NewPost(title: "", html: "<script>alert(1)</script>"), JsonOptions);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).ShouldContain("Write something");
    }

    [Fact]
    public async Task Create_Applies_The_Same_Rules_As_A_Save_Slug_Taken_And_Bad_Translation()
    {
        var admin = (await RegisterAdminAsync("create.rules@example.com")).Client;
        var existing = await CreateAsync(admin);
        await SaveAsync(admin, existing.Id, Draft(existing, slug: "taken-at-create"));

        var taken = await admin.PostAsJsonAsync("/api/admin/blog/posts", NewPost(slug: "taken-at-create"), JsonOptions);
        taken.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(taken)).ShouldContain("already uses that address");

        // A translation must be in the other language; the existing post is Turkish, so is this one.
        var sameLanguage = await admin.PostAsJsonAsync("/api/admin/blog/posts", NewPost(translationOf: existing.Id), JsonOptions);
        sameLanguage.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var linked = await admin.PostAsJsonAsync("/api/admin/blog/posts", NewPost("en", translationOf: existing.Id), JsonOptions);
        linked.StatusCode.ShouldBe(HttpStatusCode.Created, await linked.Content.ReadAsStringAsync());
        var created = (await linked.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
        created.TranslationOfPostId.ShouldBe(existing.Id);
        (await admin.GetFromJsonAsync<AdminBlogPostResponse>($"/api/admin/blog/posts/{existing.Id}", JsonOptions))!
            .TranslationOfPostId.ShouldBe(created.Id);
    }

    // ---- Drafts and publishing ----------------------------------------------------------------

    [Fact]
    public async Task A_Draft_Is_Saved_With_A_Rising_Revision_And_A_Stale_One_Is_409()
    {
        var (admin, _) = await RegisterAdminAsync("author.blog@example.com");
        var post = await CreateAsync(admin);
        // The row is born at 1 and the first draft (which create carries) is its first save.
        post.Revision.ShouldBe(2);
        post.Status.ShouldBe(BlogPostStatus.Draft);

        var saved = await SaveAsync(admin, post.Id, Draft(post));
        saved.Revision.ShouldBe(3);

        // The same tab, saving again from what it last saw: fine.
        var again = await SaveAsync(admin, post.Id, Draft(post, title: "v3", revision: 3));
        again.Revision.ShouldBe(4);

        // A second tab still holding revision 3: refused, and nothing changed.
        var stale = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{post.Id}/draft", Draft(post, title: "stale", revision: 3), JsonOptions);
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
        updated.PublishedAt!.Value.ShouldBe(published.PublishedAt!.Value, StoredClock);
        updated.PublishedUpdatedAt!.Value.ShouldBeGreaterThan(published.PublishedUpdatedAt!.Value);
        (await (await GetPublicAsync("tr", "ise-alim-surecinde-ghosting")).Content
            .ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!.Title.ShouldBe("Yarım kalmış");

        // And the config flag now says there is a blog.
        (await _factory.CreateClient().GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions))!
            .Blog.ShouldBe(new BlogConfigResponse(true, true));
    }

    [Fact]
    public async Task Preview_Is_The_Draft_In_The_Public_Shape_With_The_Slug_And_Dates_Publish_Would_Use()
    {
        var (admin, _) = await RegisterAdminAsync("preview.blog@example.com");
        var (other, _) = await RegisterAdminAsync("preview.other@example.com");
        var post = await CreateAsync(admin);
        await SaveAsync(admin, post.Id, Draft(post, html: "<p>Taslak</p>"));

        // Before any publish: the draft slot, at the address publish would allocate, dated today.
        var before = await admin.GetAsync($"/api/admin/blog/posts/{post.Id}/preview");
        before.StatusCode.ShouldBe(HttpStatusCode.OK, await before.Content.ReadAsStringAsync());
        var preview = (await before.Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        preview.Id.ShouldBe(post.Id);
        preview.Slug.ShouldBe("ise-alim-surecinde-ghosting");
        preview.Title.ShouldBe("İşe Alım Sürecinde Ghosting");
        preview.Excerpt.ShouldBe("Özet");
        preview.ContentHtml.ShouldBe("<p>Taslak</p>");
        preview.LikeCount.ShouldBe(0);
        preview.LikedByMe.ShouldBeNull();
        preview.Translation.ShouldBeNull();
        preview.PublishedAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
        preview.UpdatedAt.ShouldBeGreaterThanOrEqualTo(preview.PublishedAt);

        // Nothing was written: still a draft, still no slug, still not on the site.
        var untouched = await GetAdminAsync(admin, post.Id);
        untouched.Status.ShouldBe(BlogPostStatus.Draft);
        untouched.Slug.ShouldBeNull();
        (await GetPublicAsync("tr", "ise-alim-surecinde-ghosting")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Another admin's draft is as absent in preview as everywhere else.
        (await other.GetAsync($"/api/admin/blog/posts/{post.Id}/preview")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Published, then edited: the preview shows the pending draft, the public page the live one,
        // and the first publish date is kept while "updated" is now.
        var published = await PublishAsync(admin, post.Id);
        await SaveAsync(admin, post.Id, Draft(published, title: "Yeni başlık", html: "<p>Yeni</p>"));
        var pending = (await admin.GetFromJsonAsync<BlogPostPublicResponse>($"/api/admin/blog/posts/{post.Id}/preview", JsonOptions))!;
        pending.Title.ShouldBe("Yeni başlık");
        pending.ContentHtml.ShouldBe("<p>Yeni</p>");
        pending.Slug.ShouldBe(published.Slug);
        pending.PublishedAt.ShouldBe(published.PublishedAt!.Value, StoredClock);
        pending.UpdatedAt.ShouldBeGreaterThan(published.PublishedUpdatedAt!.Value);
        (await (await GetPublicAsync("tr", published.Slug!)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!
            .Title.ShouldBe("İşe Alım Sürecinde Ghosting");

        // Once published, any admin may preview it — the same rule as editing it.
        (await other.GetAsync($"/api/admin/blog/posts/{post.Id}/preview")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Preview_Links_A_Translation_Only_Once_The_Twin_Is_Published()
    {
        var (admin, _) = await RegisterAdminAsync("preview.twin@example.com");
        var tr = await CreateAsync(admin);
        await SaveAsync(admin, tr.Id, Draft(tr));
        var en = await CreateAsync(admin, "en");
        await SaveAsync(admin, en.Id, Draft(en, title: "Ghosting in Hiring", translationOf: tr.Id));

        (await admin.GetFromJsonAsync<BlogPostPublicResponse>($"/api/admin/blog/posts/{en.Id}/preview", JsonOptions))!
            .Translation.ShouldBeNull();

        await PublishAsync(admin, tr.Id);

        (await admin.GetFromJsonAsync<BlogPostPublicResponse>($"/api/admin/blog/posts/{en.Id}/preview", JsonOptions))!
            .Translation.ShouldBe(new BlogTranslationLink("tr", "ise-alim-surecinde-ghosting"));
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
        back.PublishedAt!.Value.ShouldBe(post.PublishedAt!.Value, StoredClock);
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

    // ---- The admin table (2026-09-20) ----------------------------------------------------------

    private static async Task<PagedResult<AdminBlogPostGroupResponse>> GroupedAsync(HttpClient admin, string query = "")
    {
        var response = await admin.GetAsync($"/api/admin/blog/posts/grouped{query}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PagedResult<AdminBlogPostGroupResponse>>(JsonOptions))!;
    }

    [Fact]
    public async Task Grouped_List_Pairs_A_Post_With_Its_Translation_Sorts_By_The_Later_Touch_And_Pages_By_Pair()
    {
        var (admin, _) = await RegisterAdminAsync("grouped.blog@example.com");
        var tr = await PublishedPostAsync(admin, "Türkçe yazı", "tr");
        var en = await CreateAsync(admin, "en");
        await SaveAsync(admin, en.Id, Draft(en, "English draft", translationOf: tr.Id));
        var loneTr = await CreateAsync(admin, "tr");
        await SaveAsync(admin, loneTr.Id, Draft(loneTr, "Yalnız Türkçe"));
        var loneEn = await PublishedPostAsync(admin, "Lone English", "en");

        // Touching the English side moves the whole pair to the top.
        var enNow = await GetAdminAsync(admin, en.Id);
        await SaveAsync(admin, en.Id, Draft(enNow, "English draft, edited"));

        var page1 = await GroupedAsync(admin);
        page1.TotalCount.ShouldBe(3);
        page1.PageSize.ShouldBe(2);
        page1.Items.Count.ShouldBe(2);
        var pair = page1.Items.ElementAt(0);
        pair.Tr.ShouldNotBeNull().Id.ShouldBe(tr.Id);
        pair.En.ShouldNotBeNull().Id.ShouldBe(en.Id);
        pair.En.Title.ShouldBe("English draft, edited");
        pair.En.TranslationOfPostId.ShouldBe(tr.Id);
        pair.Tr.TranslationOfPostId.ShouldBe(en.Id);
        pair.UpdatedAt.ShouldBe(pair.En.UpdatedAt);
        page1.Items.ElementAt(1).En.ShouldNotBeNull().Id.ShouldBe(loneEn.Id);
        page1.Items.ElementAt(1).Tr.ShouldBeNull();

        var page2 = await GroupedAsync(admin, "?page=2");
        page2.Items.ShouldHaveSingleItem().Tr.ShouldNotBeNull().Id.ShouldBe(loneTr.Id);
        page2.Items.ElementAt(0).En.ShouldBeNull();

        // A status filter keeps the row when either side matches — and still shows both sides.
        var published = await GroupedAsync(admin, "?status=Published");
        published.TotalCount.ShouldBe(2);
        published.Items.ElementAt(0).En.ShouldNotBeNull().Status.ShouldBe(BlogPostStatus.Draft);
        published.Items.Select(g => g.Tr?.Id).ShouldNotContain(loneTr.Id);
        var drafts = await GroupedAsync(admin, "?status=Draft");
        drafts.TotalCount.ShouldBe(2);
        drafts.Items.ElementAt(0).Tr.ShouldNotBeNull().Status.ShouldBe(BlogPostStatus.Published);
        drafts.Items.ElementAt(1).Tr.ShouldNotBeNull().Id.ShouldBe(loneTr.Id);

        (await admin.GetAsync("/api/admin/blog/posts/grouped?status=Nope")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await admin.GetAsync("/api/admin/blog/posts/grouped?page=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Grouped_List_Leaves_Another_Admins_Draft_Translation_Out_But_Says_It_Exists()
    {
        var (author, _) = await RegisterAdminAsync("grouped-author.blog@example.com");
        var (other, _) = await RegisterAdminAsync("grouped-other.blog@example.com");
        var tr = await PublishedPostAsync(author, "Türkçe yazı", "tr");
        var en = await CreateAsync(author, "en");
        await SaveAsync(author, en.Id, Draft(en, "English draft", translationOf: tr.Id));

        var mine = await GroupedAsync(author);
        mine.Items.ShouldHaveSingleItem().En.ShouldNotBeNull().Id.ShouldBe(en.Id);

        // The other admin sees the published half only; the link tells them the draft exists
        // without leaking anything about it beyond its id.
        var theirs = await GroupedAsync(other);
        var row = theirs.Items.ShouldHaveSingleItem();
        row.En.ShouldBeNull();
        row.Tr.ShouldNotBeNull().TranslationOfPostId.ShouldBe(en.Id);
        row.Tr.IsMine.ShouldBeFalse();
        row.UpdatedAt.ShouldBe(row.Tr.UpdatedAt);

        var user = await RegisterUserAsync("grouped-user.blog@example.com");
        (await user.GetAsync("/api/admin/blog/posts/grouped")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _factory.CreateClient().GetAsync("/api/admin/blog/posts/grouped")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Views (2026-09-20) --------------------------------------------------------------------

    [Fact]
    public async Task A_Public_Read_Counts_A_View_That_Reaches_The_Admin_Without_Touching_The_Post()
    {
        var (admin, _) = await RegisterAdminAsync("views.blog@example.com");
        var post = await PublishedPostAsync(admin);
        var before = (await admin.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>("/api/admin/blog/posts", JsonOptions))!
            .Items.Single();
        before.ViewCount.ShouldBe(0);
        before.LikeCount.ShouldBe(0);

        var first = (await (await GetPublicAsync("tr", post.Slug!)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        first.ViewCount.ShouldBe(1);
        var reader = await RegisterUserAsync("viewer.blog@example.com");
        var second = (await (await GetPublicAsync("tr", post.Slug!, reader)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        second.ViewCount.ShouldBe(2);
        second.LikedByMe.ShouldBe(false);

        // The tally is on the admin's side too, and a read is not an edit: the post's own clock
        // and revision stay where they were, and the preview does not count.
        var after = (await admin.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>("/api/admin/blog/posts", JsonOptions))!
            .Items.Single();
        after.ViewCount.ShouldBe(2);
        after.UpdatedAt.ShouldBe(before.UpdatedAt);
        var detail = await GetAdminAsync(admin, post.Id);
        detail.ViewCount.ShouldBe(2);
        detail.Revision.ShouldBe(post.Revision);
        (await GroupedAsync(admin)).Items.Single().Tr.ShouldNotBeNull().ViewCount.ShouldBe(2);
        var preview = await admin.GetFromJsonAsync<BlogPostPublicResponse>($"/api/admin/blog/posts/{post.Id}/preview", JsonOptions);
        preview!.ViewCount.ShouldBe(2);
        (await GetAdminAsync(admin, post.Id)).ViewCount.ShouldBe(2);
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

    // ---- SEO fields (2026-09-21) ------------------------------------------------------------------

    [Fact]
    public async Task Seo_Fields_Are_Saved_With_The_Draft_And_Reach_The_Page_On_Publish()
    {
        var (admin, _) = await RegisterAdminAsync("seo.blog@example.com");
        var post = await CreateAsync(admin);

        // An editor build without the SEO section sends no `seo` at all: stored as none, not refused.
        var first = await SaveAsync(admin, post.Id, Draft(post));
        ShouldBeSeo((await GetAdminAsync(admin, post.Id)).DraftSeo, null, null, [], null);

        var seo = new BlogSeoRequest(" İşe Alımda Ghosting ", "işe alımda ghosting",
            ["mülakat sonrası sessizlik", "Mülakat Sonrası Sessizlik", " ", "İK geri dönüş süresi"], "Soyut gradyan");
        await SaveAsync(admin, post.Id, Draft(post, revision: first.Revision, seo: seo));
        var draft = await GetAdminAsync(admin, post.Id);
        ShouldBeSeo(draft.DraftSeo, "İşe Alımda Ghosting", "işe alımda ghosting", ["mülakat sonrası sessizlik", "İK geri dönüş süresi"], "Soyut gradyan");

        // The preview shows the draft's SEO as publish would put it.
        var preview = (await admin.GetFromJsonAsync<BlogPostPublicResponse>($"/api/admin/blog/posts/{post.Id}/preview", JsonOptions))!;
        preview.SeoTitle.ShouldBe("İşe Alımda Ghosting");
        preview.CoverAlt.ShouldBe("Soyut gradyan");
        preview.Keywords.ShouldBe(["işe alımda ghosting", "mülakat sonrası sessizlik", "İK geri dönüş süresi"]);

        var published = await PublishAsync(admin, post.Id);
        var page = (await (await GetPublicAsync("tr", published.Slug!)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        page.SeoTitle.ShouldBe("İşe Alımda Ghosting");
        page.CoverAlt.ShouldBe("Soyut gradyan");
        page.Keywords.ShouldBe(["işe alımda ghosting", "mülakat sonrası sessizlik", "İK geri dönüş süresi"]);

        // Clearing the draft's fields does not touch the page until the next publish.
        await SaveAsync(admin, post.Id, Draft(post, revision: published.Revision, seo: new BlogSeoRequest("", "", [], "")));
        (await (await GetPublicAsync("tr", published.Slug!)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!
            .SeoTitle.ShouldBe("İşe Alımda Ghosting");
        await PublishAsync(admin, post.Id);
        var cleared = (await (await GetPublicAsync("tr", published.Slug!)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        cleared.SeoTitle.ShouldBeNull();
        cleared.CoverAlt.ShouldBeNull();
        cleared.Keywords.ShouldBeEmpty();
    }

    [Fact]
    public async Task Seo_Fields_Over_The_Caps_Are_A_Validation_Problem_Naming_The_Field()
    {
        var (admin, _) = await RegisterAdminAsync("seo2.blog@example.com");
        var post = await CreateAsync(admin);

        var tooManyKeywords = Enumerable.Repeat("k", BlogSeo.MaxSecondaryKeywords + 1).ToArray();
        var response = await admin.PutAsJsonAsync($"/api/admin/blog/posts/{post.Id}/draft",
            Draft(post, seo: new BlogSeoRequest(new string('t', BlogSeo.MaxSeoTitleLength + 1), null, tooManyKeywords, null)), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Seo.SeoTitle");
        body.ShouldContain("Seo.SecondaryKeywords");
    }

    /// <summary>Member by member: the record's own equality compares the keyword list by reference.</summary>
    private static void ShouldBeSeo(BlogSeoResponse? actual, string? seoTitle, string? primaryKeyword, string[] secondaryKeywords, string? coverAlt)
    {
        actual.ShouldNotBeNull();
        actual.SeoTitle.ShouldBe(seoTitle);
        actual.PrimaryKeyword.ShouldBe(primaryKeyword);
        actual.SecondaryKeywords.ShouldBe(secondaryKeywords);
        actual.CoverAlt.ShouldBe(coverAlt);
    }

    /// <summary>Records what the service sent and answers a fixed proposal — the model is never
    /// reached from a test.</summary>
    private sealed class StubSeoProvider : IBlogSeoSuggestionProvider
    {
        public BlogSeoSuggestionRequest? LastRequest { get; private set; }

        public Task<BlogSeoSuggestionResponse> SuggestAsync(BlogSeoSuggestionRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new BlogSeoSuggestionResponse("İşe Alımda Ghosting", "Başvuruların yüzde sekseni yanıtsız kalıyor.",
                "işe alımda ghosting", ["mülakat sonrası sessizlik"], request.HasCover ? "Soyut gradyan" : null,
                request.LockedSlug is null ? "ise-alimda-ghosting" : null, "Bilgi arayan aday."));
        }
    }

    [Fact]
    public async Task Seo_Suggestion_Sends_The_Draft_As_Text_Answers_Proposals_And_Writes_Nothing()
    {
        var provider = new StubSeoProvider();
        await using var suggesting = host.Standalone(builder =>
            builder.ConfigureTestServices(services => services.AddScoped<IBlogSeoSuggestionProvider>(_ => provider)));
        var (admin, _) = await RegisterAdminAsync("seo3.blog@example.com", suggesting);
        var post = await CreateAsync(admin);
        await SaveAsync(admin, post.Id, Draft(post, html: "<p>Başvuruların <strong>yüzde sekseni</strong> yanıtsız.</p>"));

        var response = await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/seo-suggestions", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var suggestion = (await response.Content.ReadFromJsonAsync<BlogSeoSuggestionResponse>(JsonOptions))!;
        suggestion.SeoTitle.ShouldBe("İşe Alımda Ghosting");
        suggestion.Slug.ShouldBe("ise-alimda-ghosting");
        suggestion.CoverAlt.ShouldBeNull();

        // The body went as text, not markup; no cover, no slug lock on a never-published post.
        provider.LastRequest.ShouldNotBeNull();
        provider.LastRequest.BodyText.ShouldBe("Başvuruların yüzde sekseni yanıtsız.");
        provider.LastRequest.Title.ShouldBe("İşe Alım Sürecinde Ghosting");
        provider.LastRequest.HasCover.ShouldBeFalse();
        provider.LastRequest.LockedSlug.ShouldBeNull();
        provider.LastRequest.Kind.ShouldBe(BlogPostKind.Blog);

        // Proposals only: the draft's fields are as they were.
        ShouldBeSeo((await GetAdminAsync(admin, post.Id)).DraftSeo, null, null, [], null);

        // After publish the slug is locked, and the provider is told so.
        await PublishAsync(admin, post.Id);
        await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/seo-suggestions", null);
        provider.LastRequest!.LockedSlug.ShouldNotBeNull();

        // Another admin sees a published post, so may ask; a stranger's draft is 404; a reader is 403.
        var (otherAdmin, _) = await RegisterAdminAsync("seo4.blog@example.com", suggesting);
        (await otherAdmin.PostAsync($"/api/admin/blog/posts/{post.Id}/seo-suggestions", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var theirDraft = await CreateAsync(otherAdmin);
        (await admin.PostAsync($"/api/admin/blog/posts/{theirDraft.Id}/seo-suggestions", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var reader = await RegisterUserAsync("seo.reader@example.com", suggesting);
        (await reader.PostAsync($"/api/admin/blog/posts/{post.Id}/seo-suggestions", null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // A guide (2026-09-26) gets the same tool, and the model is told it is describing a guide.
        var guideCreated = await admin.PostAsJsonAsync("/api/admin/blog/posts",
            NewPost(title: "Mülakattan sonra teşekkür e-postası", html: "<p>Aynı gün gönder.</p>") with { Kind = BlogPostKind.Guide }, JsonOptions);
        guideCreated.StatusCode.ShouldBe(HttpStatusCode.Created, await guideCreated.Content.ReadAsStringAsync());
        var guide = (await guideCreated.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
        (await admin.PostAsync($"/api/admin/blog/posts/{guide.Id}/seo-suggestions", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        provider.LastRequest!.Kind.ShouldBe(BlogPostKind.Guide);
    }

    [Fact]
    public async Task Seo_Suggestion_Without_A_Configured_Model_Is_A_Coded_400_Not_A_500()
    {
        // The real provider, no project id: the button gets a message, not a crash.
        var (admin, _) = await RegisterAdminAsync("seo5.blog@example.com");
        var post = await CreateAsync(admin);
        await SaveAsync(admin, post.Id, Draft(post));

        var response = await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/seo-suggestions", null);
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!.Detail.ShouldNotBeNullOrWhiteSpace();
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
    public async Task Like_State_Is_Readable_Without_Toggling_And_Is_Per_Account()
    {
        // The page is rendered without the reader's token (LikedByMe null), so the button asks
        // here before its first click. Reading must never flip anything (2026-09-21).
        var (admin, _) = await RegisterAdminAsync("likestate.blog@example.com");
        var post = await PublishedPostAsync(admin);
        var reader = await RegisterUserAsync("reader3.blog@example.com");
        var other = await RegisterUserAsync("reader4.blog@example.com");

        (await _factory.CreateClient().GetAsync($"/api/blog/posts/{post.Id}/like")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await reader.GetFromJsonAsync<BlogLikeToggleResponse>($"/api/blog/posts/{post.Id}/like", JsonOptions))
            .ShouldBe(new BlogLikeToggleResponse(false, 0));

        (await reader.PostAsync($"/api/blog/posts/{post.Id}/like", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Read twice: the answer is the same and the like is still there.
        (await reader.GetFromJsonAsync<BlogLikeToggleResponse>($"/api/blog/posts/{post.Id}/like", JsonOptions))
            .ShouldBe(new BlogLikeToggleResponse(true, 1));
        (await reader.GetFromJsonAsync<BlogLikeToggleResponse>($"/api/blog/posts/{post.Id}/like", JsonOptions))
            .ShouldBe(new BlogLikeToggleResponse(true, 1));

        // Another account sees the count but not the reader's like.
        (await other.GetFromJsonAsync<BlogLikeToggleResponse>($"/api/blog/posts/{post.Id}/like", JsonOptions))
            .ShouldBe(new BlogLikeToggleResponse(false, 1));

        // A draft is not on any page, so it has no state to read.
        var draft = await CreateAsync(admin);
        (await reader.GetAsync($"/api/blog/posts/{draft.Id}/like")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
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
        // The cover's pixel size rides along (the fixture's IHDR says 640×480) — what the web app
        // decides the share image by (2026-09-21); the admin view carries it too.
        (page.CoverWidth, page.CoverHeight).ShouldBe((640, 480));
        var admin = await GetAdminAsync(author, post.Id);
        (admin.CoverWidth, admin.CoverHeight).ShouldBe((640, 480));

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
    public async Task An_Image_Nothing_Points_At_Any_More_Is_Deleted_From_The_Bucket_On_The_Next_Save()
    {
        var (admin, _) = await RegisterAdminAsync("orphan.blog@example.com");
        var post = await CreateAsync(admin);
        var first = await UploadAsync(admin, post.Id, PngBytes);
        var second = await UploadAsync(admin, post.Id, PngBytes);
        var inBody = await UploadAsync(admin, post.Id, PngBytes);
        string PathOf(BlogMediaResponse m) => Path.Combine(host.Profile.BlogMediaRoot, "blog", post.Id.ToString("D"), $"{m.Id:D}.png");

        // Cover = first, body shows the third: both referenced, both kept; the second is nobody's.
        var saved = await SaveAsync(admin, post.Id, Draft(post, html: $"<p>x</p><img src=\"{inBody.Url}\">", cover: first.Id));
        File.Exists(PathOf(first)).ShouldBeTrue();
        File.Exists(PathOf(inBody)).ShouldBeTrue();
        File.Exists(PathOf(second)).ShouldBeFalse();
        (await admin.GetAsync(second.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // Swap the cover: the old one goes, the new one (uploaded now) stays.
        var replacement = await UploadAsync(admin, post.Id, PngBytes);
        var current = await GetAdminAsync(admin, post.Id);
        await SaveAsync(admin, post.Id, Draft(current, html: current.DraftContentHtml, cover: replacement.Id, revision: saved.Revision));
        File.Exists(PathOf(first)).ShouldBeFalse();
        File.Exists(PathOf(replacement)).ShouldBeTrue();

        // Publish, then drop the body image from the draft: it is still on the live page, so it
        // survives the autosave — and goes with the next publish.
        await PublishAsync(admin, post.Id);
        current = await GetAdminAsync(admin, post.Id);
        await SaveAsync(admin, post.Id, Draft(current, html: "<p>no image</p>", cover: replacement.Id));
        File.Exists(PathOf(inBody)).ShouldBeTrue();
        (await _factory.CreateClient().GetAsync(inBody.Url)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await PublishAsync(admin, post.Id);
        File.Exists(PathOf(inBody)).ShouldBeFalse();
        (await _factory.CreateClient().GetAsync(inBody.Url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
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

    // ---- Slug corrections (data migrations) --------------------------------------------------------

    private const string MisspelledSlug = "how-can-i-kep-my-motivation-while-job-searching";
    private const string CorrectedSlug = "how-can-i-keep-my-motivation-while-job-searching";

    /// <summary>Runs one direction of a data migration's SQL against the test database — the same
    /// statements the deploy's migrate step runs, taken from the migration itself.</summary>
    private Task RunMigrationSqlAsync(Migration migration, bool up) =>
        host.WithDbAsync(async db =>
        {
            foreach (var sql in (up ? migration.UpOperations : migration.DownOperations).OfType<SqlOperation>())
            {
                await db.Database.ExecuteSqlRawAsync(sql.Sql);
            }
        });

    private Task<string?> StoredSlugAsync(Guid postId) =>
        host.WithDbAsync(db => db.BlogPosts.Where(p => p.Id == postId).Select(p => p.Slug).SingleAsync());

    [Fact]
    public async Task The_Slug_Fix_Migration_Renames_The_Misspelled_English_Post_And_Back()
    {
        var (admin, _) = await RegisterAdminAsync("slugfix.blog@example.com");
        var post = await CreateAsync(admin, "en");
        await SaveAsync(admin, post.Id, Draft(post, title: "How Can I Keep My Motivation While Job Searching?", slug: MisspelledSlug));
        (await PublishAsync(admin, post.Id)).Slug.ShouldBe(MisspelledSlug);
        // A Turkish post with the same slug is another language's address: left alone.
        var turkish = await CreateAsync(admin, "tr");
        await SaveAsync(admin, turkish.Id, Draft(turkish, slug: MisspelledSlug));
        await PublishAsync(admin, turkish.Id);

        await RunMigrationSqlAsync(new FixEnglishMotivationPostSlug(), up: true);

        (await StoredSlugAsync(post.Id)).ShouldBe(CorrectedSlug);
        (await StoredSlugAsync(turkish.Id)).ShouldBe(MisspelledSlug);
        // Everything hangs off the post id, so the page is the same post at the new address.
        var page = (await (await GetPublicAsync("en", CorrectedSlug)).Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
        page.Id.ShouldBe(post.Id);

        await RunMigrationSqlAsync(new FixEnglishMotivationPostSlug(), up: false);
        (await StoredSlugAsync(post.Id)).ShouldBe(MisspelledSlug);
    }

    [Fact]
    public async Task The_Slug_Fix_Migration_Leaves_The_Row_Alone_When_The_Corrected_Slug_Is_Taken()
    {
        var (admin, _) = await RegisterAdminAsync("slugfix.taken.blog@example.com");
        var misspelled = await CreateAsync(admin, "en");
        await SaveAsync(admin, misspelled.Id, Draft(misspelled, slug: MisspelledSlug));
        await PublishAsync(admin, misspelled.Id);
        var holder = await CreateAsync(admin, "en");
        await SaveAsync(admin, holder.Id, Draft(holder, slug: CorrectedSlug));
        await PublishAsync(admin, holder.Id);

        // No unique-index violation to fail the deploy's migrate step; nothing moves.
        await RunMigrationSqlAsync(new FixEnglishMotivationPostSlug(), up: true);

        (await StoredSlugAsync(misspelled.Id)).ShouldBe(MisspelledSlug);
        (await StoredSlugAsync(holder.Id)).ShouldBe(CorrectedSlug);
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
