using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.ClientConfig;
using AfterApply.Domain.Blog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace AfterApply.IntegrationTests.Blog;

/// <summary>
/// The guide as a kind of blog post (DECISIONS.md 2026-09-26): written in the same editor, read
/// through the same routes with <c>kind=Guide</c>, kept apart from the blog everywhere a reader
/// or an admin table could mix them. Likes and the view tally work; comments do not exist.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class GuideTests(ApiHost<BlogProfile> host) : IClassFixture<ApiHost<BlogProfile>>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = ApiHost.JsonOptions;

    private const string Doc = """{"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Merhaba"}]}]}""";

    private WebApplicationFactory<Program> _off => host.Variant("blog-off", b => b.UseSetting("Blog:Enabled", "false"));

    public async Task InitializeAsync()
    {
        await host.ResetAsync();
        host.Profile.Reset();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- helpers --------------------------------------------------------------------------------

    private async Task<HttpClient> RegisterAdminAsync(string email)
    {
        var (client, auth) = await host.RegisterAsync(email);
        await host.MakeAdminAsync(auth.User.Id);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return client;
    }

    private static async Task<AdminBlogPostResponse> CreateAsync(HttpClient admin, BlogPostKind kind, string language = "tr",
        string title = "Taslak", Guid? translationOf = null)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/blog/posts",
            new CreateBlogPostRequest(title, null, Doc, "", language, null, translationOf, Kind: kind), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
    }

    private static SaveBlogDraftRequest Draft(AdminBlogPostResponse post, string title, string? slug = null,
        BlogGuideRequest? guide = null, Guid? translationOf = null) =>
        new(title, "Özet", Doc, "<p>Merhaba</p>", post.Language, slug ?? post.Slug, post.CoverMediaId,
            translationOf ?? post.TranslationOfPostId, post.Revision, Guide: guide);

    private static async Task<HttpResponseMessage> SaveRawAsync(HttpClient admin, Guid postId, SaveBlogDraftRequest request) =>
        await admin.PutAsJsonAsync($"/api/admin/blog/posts/{postId}/draft", request, JsonOptions);

    private static async Task SaveAsync(HttpClient admin, Guid postId, SaveBlogDraftRequest request)
    {
        var response = await SaveRawAsync(admin, postId, request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task<AdminBlogPostResponse> GetAdminAsync(HttpClient admin, Guid postId) =>
        (await admin.GetFromJsonAsync<AdminBlogPostResponse>($"/api/admin/blog/posts/{postId}", JsonOptions))!;

    private static async Task<AdminBlogPostResponse> PublishAsync(HttpClient admin, Guid postId, PublishBlogPostRequest? body = null)
    {
        var response = body is null
            ? await admin.PostAsync($"/api/admin/blog/posts/{postId}/publish", null)
            : await admin.PostAsJsonAsync($"/api/admin/blog/posts/{postId}/publish", body, JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
    }

    /// <summary>A published post of the kind, with a hand-typed slug when one is given.</summary>
    private static async Task<AdminBlogPostResponse> PublishedAsync(HttpClient admin, BlogPostKind kind, string title,
        string language = "tr", string? slug = null, BlogGuideRequest? guide = null)
    {
        var post = await CreateAsync(admin, kind, language);
        await SaveAsync(admin, post.Id, Draft(post, title, slug, guide));
        return await PublishAsync(admin, post.Id);
    }

    private static async Task<string> ProblemDetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions))!.Detail ?? string.Empty;

    private async Task<BlogPostPublicResponse?> PublicGuideAsync(string language, string slug)
    {
        var response = await host.CreateClient().GetAsync($"/api/blog/public/posts/{language}/{slug}?kind=Guide");
        return response.StatusCode == HttpStatusCode.NotFound
            ? null
            : (await response.Content.ReadFromJsonAsync<BlogPostPublicResponse>(JsonOptions))!;
    }

    // ---- Two sections -----------------------------------------------------------------------------

    [Fact]
    public async Task A_Guide_And_A_Blog_Post_Can_Share_A_Slug_And_Never_Show_In_Each_Others_Section()
    {
        var admin = await RegisterAdminAsync("sections.guide@example.com");
        var guide = await PublishedAsync(admin, BlogPostKind.Guide, "Rehber yazısı", slug: "ayni-adres");
        var post = await PublishedAsync(admin, BlogPostKind.Blog, "Blog yazısı", slug: "ayni-adres");
        guide.Kind.ShouldBe(BlogPostKind.Guide);
        post.Kind.ShouldBe(BlogPostKind.Blog);

        var anonymous = host.CreateClient();
        var asBlog = (await anonymous.GetFromJsonAsync<BlogPostPublicResponse>("/api/blog/public/posts/tr/ayni-adres", JsonOptions))!;
        asBlog.Id.ShouldBe(post.Id);
        asBlog.Kind.ShouldBe(BlogPostKind.Blog);
        (await PublicGuideAsync("tr", "ayni-adres"))!.Id.ShouldBe(guide.Id);

        var blogList = (await anonymous.GetFromJsonAsync<PagedResult<BlogPostListItemResponse>>("/api/blog/public/posts?lang=tr", JsonOptions))!;
        blogList.Items.Select(i => i.Id).ShouldBe([post.Id]);
        var guideList = (await anonymous.GetFromJsonAsync<PagedResult<BlogPostListItemResponse>>(
            "/api/blog/public/posts?lang=tr&kind=Guide", JsonOptions))!;
        guideList.Items.Select(i => i.Id).ShouldBe([guide.Id]);

        (await anonymous.GetFromJsonAsync<List<BlogSlugResponse>>("/api/blog/public/slugs", JsonOptions))!.Count.ShouldBe(1);
        (await anonymous.GetFromJsonAsync<List<BlogSlugResponse>>("/api/blog/public/slugs?kind=Guide", JsonOptions))!
            .Single().Slug.ShouldBe("ayni-adres");
    }

    [Fact]
    public async Task A_Guide_Slug_Under_The_Blog_Is_Not_Found_And_Guides_Alone_Do_Not_Light_Up_The_Blog_Link()
    {
        var admin = await RegisterAdminAsync("nolink.guide@example.com");
        var guide = await PublishedAsync(admin, BlogPostKind.Guide, "Yalnızca rehber");

        var anonymous = host.CreateClient();
        (await anonymous.GetAsync($"/api/blog/public/posts/tr/{guide.Slug}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetAsync($"/api/blog/public/posts/tr/{guide.Slug}?kind=7")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await anonymous.GetFromJsonAsync<ClientConfigResponse>("/api/config", JsonOptions))!.Blog.HasPublishedPosts.ShouldBeFalse();
    }

    [Fact]
    public async Task The_Admin_Tables_Hold_One_Kind_Each_And_The_Blog_Is_The_Default()
    {
        var admin = await RegisterAdminAsync("tables.guide@example.com");
        var guide = await CreateAsync(admin, BlogPostKind.Guide, title: "Rehber taslağı");
        var post = await CreateAsync(admin, BlogPostKind.Blog, title: "Blog taslağı");

        var blogRows = (await admin.GetFromJsonAsync<PagedResult<AdminBlogPostGroupResponse>>("/api/admin/blog/posts/grouped", JsonOptions))!;
        blogRows.Items.Select(r => r.Tr!.Id).ShouldBe([post.Id]);
        var guideRows = (await admin.GetFromJsonAsync<PagedResult<AdminBlogPostGroupResponse>>(
            "/api/admin/blog/posts/grouped?kind=Guide", JsonOptions))!;
        guideRows.Items.Select(r => r.Tr!.Id).ShouldBe([guide.Id]);

        var flat = (await admin.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>(
            "/api/admin/blog/posts?kind=Guide", JsonOptions))!;
        flat.Items.Select(i => i.Id).ShouldBe([guide.Id]);
        (await GetAdminAsync(admin, guide.Id)).Kind.ShouldBe(BlogPostKind.Guide);
    }

    [Fact]
    public async Task A_Translation_Link_Stays_Within_Its_Kind()
    {
        var admin = await RegisterAdminAsync("translation.guide@example.com");
        var blogTr = await CreateAsync(admin, BlogPostKind.Blog);
        var guideTr = await CreateAsync(admin, BlogPostKind.Guide);

        var crossed = await admin.PostAsJsonAsync("/api/admin/blog/posts",
            new CreateBlogPostRequest("English guide", null, Doc, "", "en", null, blogTr.Id, Kind: BlogPostKind.Guide), JsonOptions);
        crossed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var guideEn = await CreateAsync(admin, BlogPostKind.Guide, "en", "English guide", guideTr.Id);
        guideEn.TranslationOfPostId.ShouldBe(guideTr.Id);
        (await GetAdminAsync(admin, guideTr.Id)).TranslationOfPostId.ShouldBe(guideEn.Id);
    }

    // ---- The guide's own settings -------------------------------------------------------------------

    [Fact]
    public async Task Related_Guides_Must_Be_Other_Guides_In_The_Same_Language_And_Show_Only_Once_Published()
    {
        var admin = await RegisterAdminAsync("related.guide@example.com");
        var live = await PublishedAsync(admin, BlogPostKind.Guide, "Yayında rehber");
        var ownDraft = await CreateAsync(admin, BlogPostKind.Guide, title: "Taslak rehber");
        await SaveAsync(admin, ownDraft.Id, Draft(ownDraft, "Taslak rehber"));
        var blogPost = await PublishedAsync(admin, BlogPostKind.Blog, "Blog yazısı");
        var english = await PublishedAsync(admin, BlogPostKind.Guide, "English guide", "en");
        var guide = await CreateAsync(admin, BlogPostKind.Guide, title: "Ana rehber");

        foreach (var wrong in new[] { blogPost.Id, english.Id, guide.Id, Guid.NewGuid() })
        {
            var refused = await SaveRawAsync(admin, guide.Id, Draft(guide, "Ana rehber", guide: new BlogGuideRequest(false, [wrong])));
            refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await ProblemDetailAsync(refused)).ShouldContain("same language");
        }

        await SaveAsync(admin, guide.Id, Draft(guide, "Ana rehber", guide: new BlogGuideRequest(false, [ownDraft.Id, live.Id])));
        (await GetAdminAsync(admin, guide.Id)).DraftGuide!.RelatedPostIds.ShouldBe([ownDraft.Id, live.Id]);
        var published = await PublishAsync(admin, guide.Id);

        // The draft is not on the site, so only the live one is linked — until the draft goes up,
        // which drops the cache and puts it back in the author's order.
        (await PublicGuideAsync("tr", published.Slug!))!.Related!.Select(r => r.Slug).ShouldBe([live.Slug]);
        var nowLive = await PublishAsync(admin, ownDraft.Id);
        var related = (await PublicGuideAsync("tr", published.Slug!))!.Related!;
        related.Select(r => r.Slug).ShouldBe([nowLive.Slug, live.Slug]);
        related[0].Title.ShouldBe("Taslak rehber");
    }

    [Fact]
    public async Task Hiding_The_Register_Box_Reaches_The_Page_Only_On_Publish_And_The_Preview_Shows_It_First()
    {
        var admin = await RegisterAdminAsync("cta.guide@example.com");
        var guide = await PublishedAsync(admin, BlogPostKind.Guide, "Değerlendirme yazmak");
        (await PublicGuideAsync("tr", guide.Slug!))!.HideRegisterCta.ShouldBeFalse();

        await SaveAsync(admin, guide.Id, Draft(guide, "Değerlendirme yazmak", guide: new BlogGuideRequest(true, [])));

        (await PublicGuideAsync("tr", guide.Slug!))!.HideRegisterCta.ShouldBeFalse();
        var preview = (await admin.GetFromJsonAsync<BlogPostPublicResponse>($"/api/admin/blog/posts/{guide.Id}/preview", JsonOptions))!;
        preview.HideRegisterCta.ShouldBeTrue();
        preview.Kind.ShouldBe(BlogPostKind.Guide);

        await PublishAsync(admin, guide.Id);
        (await PublicGuideAsync("tr", guide.Slug!))!.HideRegisterCta.ShouldBeTrue();
    }

    [Fact]
    public async Task Guide_Settings_Sent_For_A_Blog_Post_Are_Not_Stored()
    {
        var admin = await RegisterAdminAsync("blogsettings.guide@example.com");
        var post = await CreateAsync(admin, BlogPostKind.Blog);

        await SaveAsync(admin, post.Id, Draft(post, "Blog", guide: new BlogGuideRequest(true, [Guid.NewGuid()])));

        (await GetAdminAsync(admin, post.Id)).DraftGuide.ShouldBe(new BlogGuideResponse(false, []), new GuideResponseComparer());
    }

    // ---- Reader side --------------------------------------------------------------------------------

    [Fact]
    public async Task A_Guide_Counts_Views_And_Takes_Likes_But_Has_No_Comments()
    {
        var admin = await RegisterAdminAsync("reader.guide@example.com");
        var guide = await PublishedAsync(admin, BlogPostKind.Guide, "Okunan rehber");

        (await PublicGuideAsync("tr", guide.Slug!))!.ViewCount.ShouldBe(1);
        (await PublicGuideAsync("tr", guide.Slug!))!.ViewCount.ShouldBe(2);

        var (reader, _) = await host.RegisterAsync("reader.of.guide@example.com");
        var like = await reader.PostAsync($"/api/blog/posts/{guide.Id}/like", null);
        like.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await like.Content.ReadFromJsonAsync<BlogLikeToggleResponse>(JsonOptions))!.LikeCount.ShouldBe(1);

        (await host.CreateClient().GetAsync($"/api/blog/public/posts/{guide.Id}/comments")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await reader.PostAsJsonAsync($"/api/blog/posts/{guide.Id}/comments", new CreateBlogCommentRequest("Güzel yazı"), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Back-dated first publish -------------------------------------------------------------------

    [Fact]
    public async Task A_First_Publish_Can_Keep_An_Earlier_Date_But_Not_A_Later_One_Or_A_Second_Time()
    {
        var admin = await RegisterAdminAsync("backdate.guide@example.com");
        var guide = await CreateAsync(admin, BlogPostKind.Guide);
        await SaveAsync(admin, guide.Id, Draft(guide, "Eski rehber"));
        var original = new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);

        var future = await admin.PostAsJsonAsync($"/api/admin/blog/posts/{guide.Id}/publish",
            new PublishBlogPostRequest(DateTimeOffset.UtcNow.AddDays(1)), JsonOptions);
        future.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var published = await PublishAsync(admin, guide.Id, new PublishBlogPostRequest(original));
        published.PublishedAt.ShouldBe(original);
        published.PublishedUpdatedAt.ShouldBe(original);
        published.HasUnpublishedChanges.ShouldBeFalse();
        var row = (await admin.GetFromJsonAsync<PagedResult<AdminBlogPostListItemResponse>>("/api/admin/blog/posts?kind=Guide", JsonOptions))!
            .Items.Single();
        row.HasUnpublishedChanges.ShouldBeFalse();
        (await PublicGuideAsync("tr", published.Slug!))!.PublishedAt.ShouldBe(original);

        var again = await admin.PostAsJsonAsync($"/api/admin/blog/posts/{guide.Id}/publish",
            new PublishBlogPostRequest(original.AddDays(-1)), JsonOptions);
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemDetailAsync(again)).ShouldContain("first publish");
    }

    // ---- Feature switch -------------------------------------------------------------------------------

    [Fact]
    public async Task With_The_Blog_Flag_Off_The_Guide_Routes_Are_404_Too()
    {
        var admin = await RegisterAdminAsync("off.guide@example.com");
        var guide = await PublishedAsync(admin, BlogPostKind.Guide, "Kapalı rehber");

        var off = _off.CreateClient();
        (await off.GetAsync($"/api/blog/public/posts/tr/{guide.Slug}?kind=Guide")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await off.GetAsync("/api/blog/public/slugs?kind=Guide")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private sealed class GuideResponseComparer : IEqualityComparer<BlogGuideResponse?>
    {
        public bool Equals(BlogGuideResponse? x, BlogGuideResponse? y) =>
            x is not null && y is not null && x.HideRegisterCta == y.HideRegisterCta && x.RelatedPostIds.SequenceEqual(y.RelatedPostIds);

        public int GetHashCode(BlogGuideResponse? obj) => 0;
    }
}
