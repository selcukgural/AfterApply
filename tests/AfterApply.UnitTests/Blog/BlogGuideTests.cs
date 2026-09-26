using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Blog.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.Blog;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

/// <summary>The guide as a kind of post (DECISIONS.md 2026-09-26): its settings, and the
/// back-dated first publish the move of the file-based guides relies on.</summary>
public class BlogGuideTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);

    private static BlogDraftContent Content(BlogGuideOptions? guide = null) =>
        new("Rehber", "Özet", """{"type":"doc","content":[]}""", "<p>Metin</p>", BlogSeo.Empty, guide);

    private static BlogPost Guide()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0, BlogPostKind.Guide);
        post.SaveDraft(Content(), 1, T0);
        post.AssignGeneratedSlug("rehber", T0);
        return post;
    }

    [Fact]
    public void A_Post_Is_A_Blog_Post_Unless_Created_As_A_Guide()
    {
        BlogPost.Create(Author, BlogLanguage.Tr, T0).Kind.ShouldBe(BlogPostKind.Blog);
        BlogPost.Create(Author, BlogLanguage.Tr, T0, BlogPostKind.Guide).Kind.ShouldBe(BlogPostKind.Guide);
    }

    [Fact]
    public void Create_Refuses_An_Undefined_Kind()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => BlogPost.Create(Author, BlogLanguage.Tr, T0, (BlogPostKind)7));
    }

    [Fact]
    public void Guide_Settings_Ride_The_Draft_And_Reach_The_Published_Slot_Only_On_Publish()
    {
        var related = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var post = Guide();

        post.SaveDraft(Content(new BlogGuideOptions(true, related)), post.Revision, T0.AddMinutes(1));

        post.DraftGuide.ShouldBe(new BlogGuideOptions(true, related));
        post.Guide.ShouldBe(BlogGuideOptions.Empty);

        post.Publish(T0.AddMinutes(2));

        post.HideRegisterCta.ShouldBeTrue();
        post.RelatedPostIds.ShouldBe(related);
    }

    [Fact]
    public void A_Blog_Post_Holds_No_Guide_Settings()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        Should.Throw<BlogPostContentInvalidException>(() =>
            post.SaveDraft(Content(new BlogGuideOptions(true, [])), 1, T0));
    }

    [Fact]
    public void A_Guide_Cannot_Be_Related_To_Itself()
    {
        var post = Guide();

        Should.Throw<BlogRelatedInvalidException>(() =>
            post.SaveDraft(Content(new BlogGuideOptions(false, [post.Id])), post.Revision, T0));
    }

    [Fact]
    public void More_Than_Two_Or_Repeated_Related_Guides_Are_Refused()
    {
        var post = Guide();
        var id = Guid.NewGuid();

        Should.Throw<BlogPostContentInvalidException>(() =>
            post.SaveDraft(Content(new BlogGuideOptions(false, [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()])), post.Revision, T0));
        Should.Throw<BlogPostContentInvalidException>(() =>
            post.SaveDraft(Content(new BlogGuideOptions(false, [id, id])), post.Revision, T0));
    }

    [Fact]
    public void A_First_Publish_Can_Be_Back_Dated_And_Both_Dates_Take_It()
    {
        var post = Guide();
        var original = new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero);

        post.Publish(T0, original);

        post.PublishedAt.ShouldBe(original);
        post.PublishedUpdatedAt.ShouldBe(original);
        // Not "has unpublished changes": the draft is what was just published.
        post.DraftUpdatedAt.ShouldBe(original);
    }

    [Fact]
    public void A_Back_Date_Is_Refused_In_The_Future_Or_After_The_First_Publish()
    {
        var future = Guide();
        Should.Throw<BlogPublishedAtInvalidException>(() => future.Publish(T0, T0.AddDays(1)));
        future.IsPublished.ShouldBeFalse();

        var live = Guide();
        live.Publish(T0);
        Should.Throw<BlogPublishedAtInvalidException>(() => live.Publish(T0.AddDays(1), T0.AddDays(-30)));
        live.PublishedAt.ShouldBe(T0);
    }

    [Fact]
    public void Guide_Options_Compare_By_Value()
    {
        var id = Guid.NewGuid();

        new BlogGuideOptions(true, [id]).ShouldBe(new BlogGuideOptions(true, new List<Guid> { id }));
        new BlogGuideOptions(true, [id]).ShouldNotBe(new BlogGuideOptions(false, [id]));
    }

    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private static SaveBlogDraftRequest Draft(BlogGuideRequest? guide) =>
        new("Başlık", "Özet", """{"type":"doc","content":[]}""", "<p>x</p>", "tr", null, null, null, 1, Guide: guide);

    [Fact]
    public void The_Validator_Accepts_Up_To_Two_Distinct_Related_Guides()
    {
        var validator = new SaveBlogDraftRequestValidator(new KeyEchoLocalizer());

        validator.Validate(Draft(null)).IsValid.ShouldBeTrue();
        validator.Validate(Draft(new BlogGuideRequest(true, null))).IsValid.ShouldBeTrue();
        validator.Validate(Draft(new BlogGuideRequest(false, [Guid.NewGuid(), Guid.NewGuid()]))).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void The_Validator_Names_The_Related_Field_When_The_List_Is_Wrong()
    {
        var validator = new SaveBlogDraftRequestValidator(new KeyEchoLocalizer());
        var id = Guid.NewGuid();

        foreach (var ids in new[] { new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }, [id, id], [Guid.Empty] })
        {
            validator.Validate(Draft(new BlogGuideRequest(false, ids))).Errors
                .ShouldContain(e => e.PropertyName == "Guide.RelatedPostIds" && e.ErrorMessage == "BLOG_RELATED_INVALID");
        }
    }

    [Fact]
    public void Create_Refuses_An_Undefined_Kind_By_Name()
    {
        var request = new CreateBlogPostRequest("Başlık", null, """{"type":"doc","content":[]}""", "<p>x</p>", "tr", null, null,
            Kind: (BlogPostKind)7);

        new CreateBlogPostRequestValidator(new KeyEchoLocalizer()).Validate(request).Errors
            .ShouldContain(e => e.PropertyName == nameof(CreateBlogPostRequest.Kind));
    }
}
