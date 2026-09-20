using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AfterApply.Application.Applications.Contracts;
using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.CompanyReviews.Contracts;
using AfterApply.Application.Identity.Contracts;
using AfterApply.Domain.Blog;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace AfterApply.IntegrationTests.Blog;

/// <summary>Two root comments a page, so "load more" is testable with three.</summary>
public sealed class BlogCommentProfile() : LocalStorageProfile("blog-comments")
{
    public override void Configure(IWebHostBuilder builder)
    {
        base.Configure(builder);
        builder.UseSetting("Storage:BlogLocalRootPath", Path.Combine(StorageRoot, "blog-media"));
        builder.UseSetting("Blog:CommentPageSize", "2");
    }
}

/// <summary>
/// Reader comments end to end (DECISIONS.md 2026-09-20): who may write, moderation before the
/// page, the author-only pending view and edit window, one-level replies, the helpful toggle,
/// reports, the admin queue, the contributions list, the export and the rate limit.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public class BlogCommentTests(ApiHost<BlogCommentProfile> host) : IClassFixture<ApiHost<BlogCommentProfile>>, IAsyncLifetime
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

    private async Task<(HttpClient Client, Guid UserId)> RegisterAdminAsync(string email, WebApplicationFactory<Program>? on = null)
    {
        var (client, auth) = await host.RegisterAsync(email, on: on);
        await host.MakeAdminAsync(auth.User.Id);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return (client, auth.User.Id);
    }

    private async Task<(HttpClient Client, Guid UserId)> RegisterReaderAsync(string email, string firstName = "Selin", string lastName = "Yılmaz",
        WebApplicationFactory<Program>? on = null)
    {
        var (client, auth) = await host.RegisterAsync(email, firstName: firstName, lastName: lastName, on: on);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return (client, auth.User.Id);
    }

    /// <summary>A published Turkish post to comment on.</summary>
    private static async Task<AdminBlogPostResponse> PublishedPostAsync(HttpClient admin, string title = "İşe Alım Sürecinde Ghosting")
    {
        var created = await admin.PostAsJsonAsync("/api/admin/blog/posts",
            new CreateBlogPostRequest(title, null, Doc, "<p>Merhaba</p>", "tr", null, null), JsonOptions);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var post = (await created.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
        var published = await admin.PostAsync($"/api/admin/blog/posts/{post.Id}/publish", null);
        published.StatusCode.ShouldBe(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        return (await published.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
    }

    private static async Task<HttpResponseMessage> PostCommentAsync(HttpClient client, Guid postId, string content) =>
        await client.PostAsJsonAsync($"/api/blog/posts/{postId}/comments", new CreateBlogCommentRequest(content), JsonOptions);

    private static async Task<BlogCommentResponse> CommentAsync(HttpClient client, Guid postId, string content)
    {
        var response = await PostCommentAsync(client, postId, content);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BlogCommentResponse>(JsonOptions))!;
    }

    private static async Task<BlogCommentResponse> ReplyAsync(HttpClient client, Guid parentId, string content)
    {
        var response = await client.PostAsJsonAsync($"/api/blog/comments/{parentId}/replies", new CreateBlogCommentRequest(content), JsonOptions);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BlogCommentResponse>(JsonOptions))!;
    }

    private static async Task<AdminBlogCommentResponse> ApproveAsync(HttpClient admin, Guid commentId)
    {
        var response = await admin.PostAsync($"/api/admin/blog/comments/{commentId}/approve", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AdminBlogCommentResponse>(JsonOptions))!;
    }

    private async Task<BlogCommentListResponse> PublicListAsync(Guid postId, HttpClient? client = null, int page = 1)
    {
        var response = await (client ?? _factory.CreateClient()).GetAsync($"/api/blog/public/posts/{postId}/comments?page={page}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<BlogCommentListResponse>(JsonOptions))!;
    }

    private static async Task<string> ProblemCodeAsync(HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(JsonOptions);
        return problem!.Detail ?? string.Empty;
    }

    // ---- writing and reading --------------------------------------------------------------------

    [Fact]
    public async Task Anyone_Reads_Only_A_Signed_In_Reader_Writes_And_Nothing_Reaches_The_Page_Before_Approval()
    {
        var (admin, _) = await RegisterAdminAsync("comments.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var (reader, _) = await RegisterReaderAsync("comments.reader@example.com");
        var anonymous = _factory.CreateClient();

        (await PostCommentAsync(anonymous, post.Id, "Ben de benzer bir süreç yaşadım.")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The page is public, and a stale token is ignored rather than answered with a 401.
        var stale = _factory.CreateClient();
        stale.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-token");
        (await stale.GetAsync($"/api/blog/public/posts/{post.Id}/comments")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var comment = await CommentAsync(reader, post.Id, "  Ben de benzer bir süreç yaşadım.  ");
        comment.Status.ShouldBe(BlogCommentStatus.Pending);
        comment.Content.ShouldBe("Ben de benzer bir süreç yaşadım.");
        comment.AuthorName.ShouldBe("Selin Y.");
        comment.IsMine.ShouldBeTrue();
        comment.ParentCommentId.ShouldBeNull();

        // Nobody else sees it; the author sees it on the page, marked pending.
        var forEveryone = await PublicListAsync(post.Id);
        forEveryone.Items.ShouldBeEmpty();
        forEveryone.TotalCount.ShouldBe(0);
        var forAuthor = await PublicListAsync(post.Id, reader);
        var mine = forAuthor.Items.ShouldHaveSingleItem();
        mine.Status.ShouldBe(BlogCommentStatus.Pending);
        mine.IsMine.ShouldBeTrue();
        mine.HelpfulByMe.ShouldBe(false);
        forAuthor.TotalCount.ShouldBe(0, "the count is of approved comments");

        var approved = await ApproveAsync(admin, comment.Id);
        approved.Comment.Status.ShouldBe(BlogCommentStatus.Approved);
        var onPage = await PublicListAsync(post.Id);
        var shown = onPage.Items.ShouldHaveSingleItem();
        shown.AuthorName.ShouldBe("Selin Y.");
        shown.IsMine.ShouldBeFalse();
        shown.HelpfulByMe.ShouldBeNull("an anonymous reader has no vote to report");
        onPage.TotalCount.ShouldBe(1);

        // Rejected: gone from the page, and the author's own view no longer carries it either.
        (await admin.PostAsync($"/api/admin/blog/comments/{comment.Id}/reject", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PublicListAsync(post.Id)).Items.ShouldBeEmpty();
        (await PublicListAsync(post.Id, reader)).Items.ShouldBeEmpty();

        // An admin's own comment skips the queue.
        (await CommentAsync(admin, post.Id, "Teşekkürler, paylaştığınız için.")).Status.ShouldBe(BlogCommentStatus.Approved);
        (await PublicListAsync(post.Id)).Items.ShouldHaveSingleItem().AuthorName.ShouldBe("Test U.");

        // An account with no name is "a reader": the page gets null, never the address.
        var (nameless, _) = await RegisterReaderAsync("comments.nameless@example.com", "", "");
        (await CommentAsync(nameless, post.Id, "İsimsiz hesabın yorumu, bekliyor.")).AuthorName.ShouldBeNull();
    }

    [Fact]
    public async Task The_Text_Is_Validated_Kept_As_Plain_Text_And_Never_Accepted_Twice()
    {
        var (admin, _) = await RegisterAdminAsync("comments.validate.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var (reader, _) = await RegisterReaderAsync("comments.validate@example.com");

        (await PostCommentAsync(reader, post.Id, "")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PostCommentAsync(reader, post.Id, "kısa")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await PostCommentAsync(reader, post.Id, new string('a', BlogComment.MaxContentLength + 1))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        // Markup is text here: stored as typed, escaped by whoever renders it.
        var html = await CommentAsync(reader, post.Id, "<script>alert(1)</script> <b>kalın</b> yazı");
        html.Content.ShouldBe("<script>alert(1)</script> <b>kalın</b> yazı");

        var again = await PostCommentAsync(reader, post.Id, "<script>alert(1)</script> <b>kalın</b> yazı ");
        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(again)).ShouldContain("already");

        // A comment on a draft, or on a post that does not exist, is 404.
        var draft = await admin.PostAsJsonAsync("/api/admin/blog/posts",
            new CreateBlogPostRequest("Taslak", null, Doc, "", "tr", null, null), JsonOptions);
        var draftPost = (await draft.Content.ReadFromJsonAsync<AdminBlogPostResponse>(JsonOptions))!;
        (await PostCommentAsync(reader, draftPost.Id, "Taslağa yorum yazmaya çalışıyorum.")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await PostCommentAsync(reader, Guid.NewGuid(), "Olmayan yazıya yorum yazıyorum.")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _factory.CreateClient().GetAsync($"/api/blog/public/posts/{draftPost.Id}/comments")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task The_Author_Edits_A_Pending_Comment_Only_And_Nobody_Else_Can_Touch_It()
    {
        var (admin, _) = await RegisterAdminAsync("comments.edit.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var (author, _) = await RegisterReaderAsync("comments.edit@example.com");
        var (other, _) = await RegisterReaderAsync("comments.edit.other@example.com", "Mehmet", "Kaya");
        var comment = await CommentAsync(author, post.Id, "İlk hâli, on karakterden uzun.");

        var edited = await author.PutAsJsonAsync($"/api/blog/comments/{comment.Id}", new EditBlogCommentRequest("Düzeltilmiş hâli, yine uzun."), JsonOptions);
        edited.StatusCode.ShouldBe(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
        var body = (await edited.Content.ReadFromJsonAsync<BlogCommentResponse>(JsonOptions))!;
        body.Content.ShouldBe("Düzeltilmiş hâli, yine uzun.");
        body.EditedAt.ShouldNotBeNull();
        body.Status.ShouldBe(BlogCommentStatus.Pending);

        // Someone else's comment is not found — not forbidden, which would confirm it exists.
        (await other.PutAsJsonAsync($"/api/blog/comments/{comment.Id}", new EditBlogCommentRequest("Başkasının yorumunu değiştiriyorum."), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await author.PutAsJsonAsync($"/api/blog/comments/{comment.Id}", new EditBlogCommentRequest("kısa"), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        await ApproveAsync(admin, comment.Id);
        var locked = await author.PutAsJsonAsync($"/api/blog/comments/{comment.Id}", new EditBlogCommentRequest("Onaylandıktan sonra düzenliyorum."), JsonOptions);
        locked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(locked)).ShouldContain("waiting for approval");

        // Nothing deletes a comment: no route at all.
        (await author.DeleteAsync($"/api/blog/comments/{comment.Id}")).StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    // ---- replies --------------------------------------------------------------------------------

    [Fact]
    public async Task Replies_Sit_Under_Their_Root_And_Never_Nest_Deeper()
    {
        var (admin, _) = await RegisterAdminAsync("comments.reply.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var (selin, _) = await RegisterReaderAsync("comments.reply.selin@example.com");
        var (mehmet, _) = await RegisterReaderAsync("comments.reply.mehmet@example.com", "Mehmet", "Kaya");

        var root = await CommentAsync(selin, post.Id, "Ben de benzer bir süreç yaşadım.");
        // A pending comment cannot be answered — it is not on the page.
        (await mehmet.PostAsJsonAsync($"/api/blog/comments/{root.Id}/replies", new CreateBlogCommentRequest("Henüz onaylanmamış yoruma yanıt."), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ApproveAsync(admin, root.Id);

        var reply = await ReplyAsync(mehmet, root.Id, "Benim deneyimim de benzerdi.");
        reply.ParentCommentId.ShouldBe(root.Id);
        reply.Status.ShouldBe(BlogCommentStatus.Pending);
        await ApproveAsync(admin, reply.Id);

        // A reply to the reply answers the root: one level, always.
        var replyToReply = await ReplyAsync(selin, reply.Id, "Evet, tam olarak öyle oldu.");
        replyToReply.ParentCommentId.ShouldBe(root.Id);
        await ApproveAsync(admin, replyToReply.Id);

        var page = await PublicListAsync(post.Id);
        var shownRoot = page.Items.ShouldHaveSingleItem();
        shownRoot.Replies.Count.ShouldBe(2);
        shownRoot.Replies.Select(r => r.AuthorName).ShouldBe(["Mehmet K.", "Selin Y."], "oldest reply first");
        shownRoot.Replies.ShouldAllBe(r => r.ParentCommentId == root.Id && r.Replies.Count == 0);
        page.TotalCount.ShouldBe(3);
        page.TotalRootCount.ShouldBe(1);

        // Rejecting the root takes the whole thread off the page.
        (await admin.PostAsync($"/api/admin/blog/comments/{root.Id}/reject", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PublicListAsync(post.Id)).Items.ShouldBeEmpty();
        (await mehmet.PostAsJsonAsync($"/api/blog/comments/{reply.Id}/replies", new CreateBlogCommentRequest("Kökü reddedilmiş yoruma yanıt."), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Root_Comments_Are_Paged_Newest_First_With_Every_Reply_On_The_Page()
    {
        var (admin, _) = await RegisterAdminAsync("comments.page.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var first = await CommentAsync(admin, post.Id, "Birinci kök yorum, admin yazdı.");
        var second = await CommentAsync(admin, post.Id, "İkinci kök yorum, admin yazdı.");
        var third = await CommentAsync(admin, post.Id, "Üçüncü kök yorum, admin yazdı.");
        await ReplyAsync(admin, first.Id, "Birinciye yanıt, admin yazdı.");

        var page1 = await PublicListAsync(post.Id);
        page1.PageSize.ShouldBe(2);
        page1.TotalRootCount.ShouldBe(3);
        page1.TotalCount.ShouldBe(4);
        page1.Items.Select(c => c.Id).ShouldBe([third.Id, second.Id]);
        var page2 = await PublicListAsync(post.Id, page: 2);
        var last = page2.Items.ShouldHaveSingleItem();
        last.Id.ShouldBe(first.Id);
        last.Replies.ShouldHaveSingleItem();

        (await _factory.CreateClient().GetAsync($"/api/blog/public/posts/{post.Id}/comments?page=0")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- helpful and reports ----------------------------------------------------------------------

    [Fact]
    public async Task Helpful_Toggles_Once_Per_Account_And_Only_On_The_Page()
    {
        var (admin, _) = await RegisterAdminAsync("comments.helpful.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var (reader, _) = await RegisterReaderAsync("comments.helpful@example.com");
        var (other, _) = await RegisterReaderAsync("comments.helpful.other@example.com", "Ayşe", "Demir");
        var pending = await CommentAsync(reader, post.Id, "Henüz onaylanmamış bir yorum.");

        (await _factory.CreateClient().PostAsync($"/api/blog/comments/{pending.Id}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await other.PostAsync($"/api/blog/comments/{pending.Id}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await ApproveAsync(admin, pending.Id);
        var on = await other.PostAsync($"/api/blog/comments/{pending.Id}/helpful", null);
        on.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await on.Content.ReadFromJsonAsync<BlogCommentHelpfulResponse>(JsonOptions)).ShouldBe(new BlogCommentHelpfulResponse(true, 1));
        (await reader.PostAsync($"/api/blog/comments/{pending.Id}/helpful", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var seenByOther = (await PublicListAsync(post.Id, other)).Items.Single();
        seenByOther.HelpfulCount.ShouldBe(2);
        seenByOther.HelpfulByMe.ShouldBe(true);

        // One row per account, whatever the database says about a race: the count stays 2.
        (await host.WithDbAsync(db => db.BlogCommentHelpfulVotes.CountAsync(v => v.CommentId == pending.Id))).ShouldBe(2);

        var off = await other.PostAsync($"/api/blog/comments/{pending.Id}/helpful", null);
        (await off.Content.ReadFromJsonAsync<BlogCommentHelpfulResponse>(JsonOptions)).ShouldBe(new BlogCommentHelpfulResponse(false, 1));
        (await PublicListAsync(post.Id, other)).Items.Single().HelpfulByMe.ShouldBe(false);
    }

    [Fact]
    public async Task A_Report_Reaches_The_Admin_Once_Per_Reader_And_Closes_With_The_Decision()
    {
        var (admin, _) = await RegisterAdminAsync("comments.report.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var (reader, _) = await RegisterReaderAsync("comments.report@example.com");
        var (spammer, _) = await RegisterReaderAsync("comments.report.spammer@example.com", "Burak", "Tan");
        var comment = await CommentAsync(spammer, post.Id, "En iyi CV şablonları burada, tıklayın!");

        (await reader.PostAsJsonAsync($"/api/blog/comments/{comment.Id}/reports", new ReportBlogCommentRequest(BlogCommentReportReason.Spam, null), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound, "a pending comment is not on the page to report");
        await ApproveAsync(admin, comment.Id);

        (await _factory.CreateClient().PostAsJsonAsync($"/api/blog/comments/{comment.Id}/reports", new ReportBlogCommentRequest(BlogCommentReportReason.Spam, null), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await reader.PostAsJsonAsync($"/api/blog/comments/{comment.Id}/reports", new ReportBlogCommentRequest(BlogCommentReportReason.Other, null), JsonOptions))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var first = await reader.PostAsJsonAsync($"/api/blog/comments/{comment.Id}/reports", new ReportBlogCommentRequest(BlogCommentReportReason.Advertising, "Düpedüz reklam."), JsonOptions);
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var firstBody = (await first.Content.ReadFromJsonAsync<BlogCommentReportResponse>(JsonOptions))!;
        firstBody.AlreadyReported.ShouldBeFalse();
        var second = await reader.PostAsJsonAsync($"/api/blog/comments/{comment.Id}/reports", new ReportBlogCommentRequest(BlogCommentReportReason.Spam, null), JsonOptions);
        var secondBody = (await second.Content.ReadFromJsonAsync<BlogCommentReportResponse>(JsonOptions))!;
        secondBody.AlreadyReported.ShouldBeTrue();
        secondBody.Id.ShouldBe(firstBody.Id);

        // The admin sees it in the reported filter, on the comment, and in the tab counts.
        var reported = (await admin.GetFromJsonAsync<PagedResult<AdminBlogCommentListItemResponse>>("/api/admin/blog/comments?reported=true", JsonOptions))!;
        reported.Items.ShouldHaveSingleItem().OpenReportCount.ShouldBe(1);
        var detail = (await admin.GetFromJsonAsync<AdminBlogCommentResponse>($"/api/admin/blog/comments/{comment.Id}", JsonOptions))!;
        detail.Reports.ShouldHaveSingleItem().Note.ShouldBe("Düpedüz reklam.");
        detail.Comment.AuthorEmail.ShouldBe("comments.report.spammer@example.com");

        // Rejecting closes the report as action taken; dismissing would have kept the comment.
        var rejected = await admin.PostAsync($"/api/admin/blog/comments/{comment.Id}/reject", null);
        var rejectedBody = (await rejected.Content.ReadFromJsonAsync<AdminBlogCommentResponse>(JsonOptions))!;
        rejectedBody.Comment.Status.ShouldBe(BlogCommentStatus.Rejected);
        rejectedBody.Reports.Single().Status.ShouldBe(BlogCommentReportStatus.ActionTaken);
        (await admin.GetFromJsonAsync<PagedResult<AdminBlogCommentListItemResponse>>("/api/admin/blog/comments?reported=true", JsonOptions))!
            .Items.ShouldBeEmpty();

        var other = await CommentAsync(admin, post.Id, "Yazıyla ilgili bir katkı, sorun yok.");
        await reader.PostAsJsonAsync($"/api/blog/comments/{other.Id}/reports", new ReportBlogCommentRequest(BlogCommentReportReason.Inappropriate, null), JsonOptions);
        var dismissed = (await (await admin.PostAsync($"/api/admin/blog/comments/{other.Id}/reports/dismiss", null))
            .Content.ReadFromJsonAsync<AdminBlogCommentResponse>(JsonOptions))!;
        dismissed.Comment.Status.ShouldBe(BlogCommentStatus.Approved);
        dismissed.Reports.Single().Status.ShouldBe(BlogCommentReportStatus.Dismissed);
    }

    // ---- admin ----------------------------------------------------------------------------------

    [Fact]
    public async Task Only_An_Admin_Moderates_And_The_Queue_Counts_What_Waits()
    {
        var (admin, _) = await RegisterAdminAsync("comments.mod.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var (reader, _) = await RegisterReaderAsync("comments.mod.reader@example.com");
        var comment = await CommentAsync(reader, post.Id, "Onay bekleyen bir yorum.");

        foreach (var route in new[] { $"/api/admin/blog/comments/{comment.Id}/approve", $"/api/admin/blog/comments/{comment.Id}/reject" })
        {
            (await reader.PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            (await _factory.CreateClient().PostAsync(route, null)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await reader.GetAsync("/api/admin/blog/comments")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var counts = (await admin.GetFromJsonAsync<ModerationCountsResponse>("/api/admin/company-reviews/counts", JsonOptions))!;
        counts.PendingComments.ShouldBe(1);

        var pending = (await admin.GetFromJsonAsync<PagedResult<AdminBlogCommentListItemResponse>>("/api/admin/blog/comments?status=Pending", JsonOptions))!;
        var row = pending.Items.ShouldHaveSingleItem();
        row.PostTitle.ShouldBe("İşe Alım Sürecinde Ghosting");
        row.AuthorName.ShouldBe("Selin Y.");
        row.AuthorEmail.ShouldBe("comments.mod.reader@example.com");

        await ApproveAsync(admin, comment.Id);
        (await admin.GetFromJsonAsync<ModerationCountsResponse>("/api/admin/company-reviews/counts", JsonOptions))!.PendingComments.ShouldBe(0);
        (await admin.GetFromJsonAsync<PagedResult<AdminBlogCommentListItemResponse>>("/api/admin/blog/comments?status=Pending", JsonOptions))!.Items.ShouldBeEmpty();
        (await admin.GetFromJsonAsync<PagedResult<AdminBlogCommentListItemResponse>>("/api/admin/blog/comments", JsonOptions))!.TotalCount.ShouldBe(1);
        (await admin.GetAsync($"/api/admin/blog/comments/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.GetAsync("/api/admin/blog/comments?status=Nope")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- the reader's own list, the export, the limit -----------------------------------------------

    [Fact]
    public async Task The_Reader_Sees_Their_Own_Comments_In_Every_Status_And_In_The_Export()
    {
        var (admin, _) = await RegisterAdminAsync("comments.mine.admin@example.com");
        var post = await PublishedPostAsync(admin);
        var (reader, _) = await RegisterReaderAsync("comments.mine@example.com");
        var approved = await CommentAsync(reader, post.Id, "Onaylanacak olan yorumum.");
        await ApproveAsync(admin, approved.Id);
        var adminReply = await ReplyAsync(admin, approved.Id, "Admin yanıtı, hemen yayında.");
        var rejected = await CommentAsync(reader, post.Id, "Reddedilecek olan yorumum.");
        (await admin.PostAsync($"/api/admin/blog/comments/{rejected.Id}/reject", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var pending = await ReplyAsync(reader, adminReply.Id, "Admin yanıtına benim yanıtım, bekliyor.");

        var mine = (await reader.GetFromJsonAsync<PagedResult<MyBlogCommentResponse>>("/api/blog/comments/mine", JsonOptions))!;
        mine.TotalCount.ShouldBe(3);
        mine.Items.Select(c => c.Status).ShouldBe([BlogCommentStatus.Pending, BlogCommentStatus.Rejected, BlogCommentStatus.Approved]);
        var first = mine.Items.First();
        first.Id.ShouldBe(pending.Id);
        first.ParentCommentId.ShouldBe(approved.Id, "a reply to a reply answers the root");
        first.ParentAuthorName.ShouldBe("Selin Y.");
        first.PostTitle.ShouldBe("İşe Alım Sürecinde Ghosting");
        first.PostSlug.ShouldBe(post.Slug);
        mine.Items.Last().ReplyCount.ShouldBe(1, "the approved admin reply; the pending one does not count");

        (await _factory.CreateClient().GetAsync("/api/blog/comments/mine")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The contributions page lists them as a kind of their own, and its chips filter on it.
        var contributions = (await reader.GetFromJsonAsync<MyContributionsResponse>("/api/contributions/mine", JsonOptions))!;
        contributions.TotalCount.ShouldBe(3);
        contributions.Items.ShouldAllBe(i => i.Kind == ContributionKind.BlogComment && i.BlogComment != null);
        contributions.Items.First().BlogComment!.Id.ShouldBe(pending.Id);
        (await reader.GetFromJsonAsync<MyContributionsResponse>("/api/contributions/mine?filter=Company", JsonOptions))!.TotalCount.ShouldBe(0);
        (await reader.GetFromJsonAsync<MyContributionsResponse>("/api/contributions/mine?filter=BlogComments", JsonOptions))!.TotalCount.ShouldBe(3);
        (await reader.GetAsync("/api/contributions/mine?filter=Nope")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var export = (await reader.GetFromJsonAsync<AccountExportResponse>("/api/users/me/export", JsonOptions))!;
        export.BlogComments.ShouldNotBeNull().Count.ShouldBe(3);
        export.BlogComments.ShouldAllBe(c => c.PostTitle == "İşe Alım Sürecinde Ghosting");
    }

    [Fact]
    public async Task Writing_Has_Its_Own_Limit_And_The_Account_Takes_Its_Comments_With_It()
    {
        // The suite disables rate limiting for every host; this test opts back in on a host of
        // its own (a limiter's fixed windows have no reset). Three writes, so no loop of ten.
        await using var limited = host.Standalone(builder =>
        {
            builder.UseSetting("RateLimiting:Enabled", "true");
            builder.UseSetting("RateLimiting:BlogCommentWrite:PermitLimit", "3");
        });
        var (admin, _) = await RegisterAdminAsync("comments.limit.admin@example.com", limited);
        var post = await PublishedPostAsync(admin);
        var (reader, readerId) = await RegisterReaderAsync("comments.limit@example.com", on: limited);

        await CommentAsync(reader, post.Id, "Birinci yorum, sınırın içinde.");
        await CommentAsync(reader, post.Id, "İkinci yorum, sınırın içinde.");
        await CommentAsync(reader, post.Id, "Üçüncü yorum, sınırın içinde.");
        (await PostCommentAsync(reader, post.Id, "Dördüncü yorum, sınırın dışında.")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        // Reading is not bounded by the write limit.
        (await reader.GetFromJsonAsync<BlogCommentListResponse>($"/api/blog/public/posts/{post.Id}/comments", JsonOptions))!.Items.Count.ShouldBe(2);

        (await host.WithDbAsync(db => db.BlogComments.CountAsync(c => c.UserId == readerId))).ShouldBe(3);
        await host.WithDbAsync(async db =>
        {
            db.Users.Remove(await db.Users.SingleAsync(u => u.Id == readerId));
            await db.SaveChangesAsync();
        });
        (await host.WithDbAsync(db => db.BlogComments.CountAsync(c => c.UserId == readerId))).ShouldBe(0, "the comments cascade with the account");
    }
}
