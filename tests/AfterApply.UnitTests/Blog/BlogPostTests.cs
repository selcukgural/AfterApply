using AfterApply.Domain.Blog;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

public class BlogPostTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly Guid OtherAdmin = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 19, 10, 0, 0, TimeSpan.Zero);

    private static BlogDraftContent Content(string title = "İşe alım", string html = "<p>Merhaba</p>", BlogSeo? seo = null) =>
        new(title, "Özet", """{"type":"doc","content":[]}""", html, seo ?? BlogSeo.Empty);

    private static readonly BlogSeo Seo = new("İşe Alımda Ghosting", "işe alımda ghosting", ["mülakat sonrası sessizlik"], "Soyut gradyan");

    [Fact]
    public void A_New_Post_Is_An_Empty_Draft_At_Revision_One()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        post.Status.ShouldBe(BlogPostStatus.Draft);
        post.Revision.ShouldBe(1);
        post.Slug.ShouldBeNull();
        post.PublishedAt.ShouldBeNull();
        post.IsAuthor(Author).ShouldBeTrue();
        post.IsAuthor(OtherAdmin).ShouldBeFalse();
    }

    [Fact]
    public void Create_Refuses_An_Unknown_Language()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => BlogPost.Create(Author, "de", T0));
    }

    [Fact]
    public void Saving_The_Draft_Bumps_The_Revision_And_Leaves_The_Published_Slot_Alone()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        post.SaveDraft(Content(), expectedRevision: 1, T0.AddSeconds(5));

        post.Revision.ShouldBe(2);
        post.DraftTitle.ShouldBe("İşe alım");
        post.DraftUpdatedAt.ShouldBe(T0.AddSeconds(5));
        post.Title.ShouldBe(string.Empty);
        post.ContentHtml.ShouldBe(string.Empty);
    }

    [Fact]
    public void A_Stale_Revision_Is_Refused_Before_Anything_Changes()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        post.SaveDraft(Content("v1"), 1, T0);

        Should.Throw<BlogPostRevisionConflictException>(() => post.SaveDraft(Content("v2"), 1, T0.AddSeconds(5)));

        post.DraftTitle.ShouldBe("v1");
        post.Revision.ShouldBe(2);
    }

    [Fact]
    public void Over_Long_Content_Is_Refused()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        var content = Content(title: new string('a', BlogPost.MaxTitleLength + 1));

        Should.Throw<BlogPostContentInvalidException>(() => post.SaveDraft(content, 1, T0));
    }

    [Fact]
    public void Publish_Copies_The_Draft_Over_The_Published_Slot()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        post.SaveDraft(Content(), 1, T0);
        post.AssignGeneratedSlug("ise-alim", T0);

        post.Publish(T0.AddMinutes(1));

        post.Status.ShouldBe(BlogPostStatus.Published);
        post.IsPublished.ShouldBeTrue();
        post.Title.ShouldBe("İşe alım");
        post.ContentHtml.ShouldBe("<p>Merhaba</p>");
        post.PublishedContentJson.ShouldBe(post.DraftContentJson);
        post.PublishedAt.ShouldBe(T0.AddMinutes(1));
        post.PublishedUpdatedAt.ShouldBe(T0.AddMinutes(1));
    }

    [Fact]
    public void Publishing_Again_Keeps_The_First_Publish_Date_And_Moves_The_Updated_One()
    {
        var post = PublishedPost();
        post.SaveDraft(Content("v2", "<p>İkinci</p>"), post.Revision, T0.AddHours(1));

        // Between the autosave and the second publish the public slot is still v1.
        post.Title.ShouldBe("İşe alım");

        post.Publish(T0.AddHours(2));

        post.Title.ShouldBe("v2");
        post.ContentHtml.ShouldBe("<p>İkinci</p>");
        post.PublishedAt.ShouldBe(T0.AddMinutes(1));
        post.PublishedUpdatedAt.ShouldBe(T0.AddHours(2));
    }

    [Theory]
    [InlineData("", "<p>x</p>")]
    [InlineData("   ", "<p>x</p>")]
    [InlineData("Title", "")]
    [InlineData("Title", "  ")]
    public void Publish_Needs_A_Title_And_A_Body(string title, string html)
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        post.SaveDraft(Content(title, html), 1, T0);
        post.AssignGeneratedSlug("slug", T0);

        Should.Throw<BlogPostIncompleteException>(() => post.Publish(T0));
        post.Status.ShouldBe(BlogPostStatus.Draft);
    }

    [Fact]
    public void Seo_Fields_Ride_The_Draft_And_Reach_The_Published_Slot_Only_On_Publish()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        post.SaveDraft(Content(seo: Seo), expectedRevision: 1, T0);
        post.DraftSeo.ShouldBe(Seo);
        post.Seo.ShouldBe(BlogSeo.Empty);
        post.Seo.AllKeywords.ShouldBeEmpty();

        post.SetSlug("ise-alim", T0);
        post.Publish(T0.AddMinutes(1));
        post.Seo.ShouldBe(Seo);
        post.Seo.AllKeywords.ShouldBe(["işe alımda ghosting", "mülakat sonrası sessizlik"]);

        // Clearing them in the draft clears the published copy on the next publish, nothing sooner.
        post.SaveDraft(Content(), expectedRevision: 2, T0.AddMinutes(2));
        post.Seo.ShouldBe(Seo);
        post.Publish(T0.AddMinutes(3));
        post.Seo.ShouldBe(BlogSeo.Empty);
    }

    [Fact]
    public void Over_Long_Seo_Fields_Are_Refused()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        var tooLong = new BlogSeo(new string('a', BlogSeo.MaxSeoTitleLength + 1), null, [], null);

        Should.Throw<BlogPostContentInvalidException>(() => post.SaveDraft(Content(seo: tooLong), expectedRevision: 1, T0));
        post.Revision.ShouldBe(1);
    }

    [Fact]
    public void Publish_Needs_A_Slug()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        post.SaveDraft(Content(), 1, T0);

        Should.Throw<InvalidOperationException>(() => post.Publish(T0));
    }

    [Fact]
    public void Unpublish_Keeps_The_Snapshot_And_The_Slug()
    {
        var post = PublishedPost();

        post.Unpublish(T0.AddHours(1));

        post.Status.ShouldBe(BlogPostStatus.Draft);
        post.HasEverBeenPublished.ShouldBeTrue();
        post.Slug.ShouldBe("ise-alim");
        post.Title.ShouldBe("İşe alım");
        post.ContentHtml.ShouldBe("<p>Merhaba</p>");

        post.Publish(T0.AddHours(2));
        post.PublishedAt.ShouldBe(T0.AddMinutes(1), "the first publish date is the post's date");
    }

    [Fact]
    public void Unpublish_Refuses_A_Post_That_Is_Not_On_The_Site()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        Should.Throw<BlogPostNotPublishedException>(() => post.Unpublish(T0));
    }

    [Fact]
    public void Slug_And_Language_Are_Free_Before_The_First_Publish()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        post.SetSlug("my-slug", T0);
        post.SetLanguage(BlogLanguage.En, T0);
        post.SetSlug(null, T0);

        post.Slug.ShouldBeNull();
        post.Language.ShouldBe(BlogLanguage.En);
    }

    [Fact]
    public void Slug_And_Language_Are_Locked_After_The_First_Publish_Even_When_Unpublished()
    {
        var post = PublishedPost();
        post.Unpublish(T0.AddHours(1));

        Should.Throw<BlogPostFieldLockedException>(() => post.SetSlug("other", T0));
        Should.Throw<BlogPostFieldLockedException>(() => post.SetLanguage(BlogLanguage.En, T0));
        Should.Throw<BlogPostFieldLockedException>(() => post.AssignGeneratedSlug("other", T0));

        // Sending the current values back is not a change and is not refused.
        post.SetSlug("ise-alim", T0);
        post.SetLanguage(BlogLanguage.Tr, T0);
    }

    [Fact]
    public void A_Hand_Typed_Slug_Is_Validated()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        Should.Throw<BlogSlugInvalidException>(() => post.SetSlug("İşe Alım", T0));
        Should.Throw<BlogSlugInvalidException>(() => post.SetSlug("media", T0));
    }

    [Fact]
    public void Changing_The_Language_Drops_The_Translation_Link()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        post.SetTranslationOf(Guid.NewGuid(), T0);

        post.SetLanguage(BlogLanguage.En, T0);

        post.TranslationOfPostId.ShouldBeNull();
    }

    [Fact]
    public void A_Post_Cannot_Be_Its_Own_Translation()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        Should.Throw<BlogTranslationInvalidException>(() => post.SetTranslationOf(post.Id, T0));
    }

    [Fact]
    public void Visibility_Is_The_Author_Alone_Until_Published_Then_Every_Admin()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        post.IsEditableBy(Author).ShouldBeTrue();
        post.IsEditableBy(OtherAdmin).ShouldBeFalse();

        post.SaveDraft(Content(), 1, T0);
        post.AssignGeneratedSlug("ise-alim", T0);
        post.Publish(T0);
        post.IsEditableBy(OtherAdmin).ShouldBeTrue();

        post.Unpublish(T0);
        post.IsEditableBy(OtherAdmin).ShouldBeFalse("an unpublished post is the author's again");
    }

    private static BlogPost PublishedPost()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        post.SaveDraft(Content(), 1, T0);
        post.AssignGeneratedSlug("ise-alim", T0);
        post.Publish(T0.AddMinutes(1));
        return post;
    }
}
