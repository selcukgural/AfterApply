using AfterApply.Application.Blog.Contracts;
using AfterApply.Application.Blog.Validators;
using AfterApply.Application.Localization;
using AfterApply.Domain.Blog;
using Microsoft.Extensions.Localization;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

public class BlogValidatorTests
{
    private sealed class KeyEchoLocalizer : IStringLocalizer<SharedStrings>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private const string Doc = """{"type":"doc","content":[{"type":"paragraph"}]}""";

    private static SaveBlogDraftRequest Draft(string title = "Başlık", string? excerpt = "Özet", string json = Doc,
        string html = "<p>x</p>", string language = "tr", string? slug = null, int revision = 1) =>
        new(title, excerpt, json, html, language, slug, null, null, revision);

    [Fact]
    public void Accepts_A_Complete_Draft()
    {
        new SaveBlogDraftRequestValidator(new KeyEchoLocalizer()).Validate(Draft()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Accepts_An_Empty_Draft_The_Editor_Autosaves_Before_Any_Typing()
    {
        // A blank title and body are a valid draft; only publish insists on them.
        var result = new SaveBlogDraftRequestValidator(new KeyEchoLocalizer())
            .Validate(Draft(title: "", excerpt: null, html: ""));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"type":"paragraph"}""")]
    [InlineData("""{"content":[]}""")]
    public void The_Editor_Document_Has_To_Be_A_Doc(string json)
    {
        var result = new SaveBlogDraftRequestValidator(new KeyEchoLocalizer()).Validate(Draft(json: json));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveBlogDraftRequest.ContentJson)
                                         && e.ErrorMessage == "VALIDATION_BLOG_CONTENT_JSON_INVALID");
    }

    [Fact]
    public void Refuses_An_Unknown_Language()
    {
        var result = new SaveBlogDraftRequestValidator(new KeyEchoLocalizer()).Validate(Draft(language: "de"));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveBlogDraftRequest.Language)
                                         && e.ErrorMessage == "VALIDATION_UNSUPPORTED_LANGUAGE");
    }

    [Theory]
    [InlineData("İşe-Alım")]
    [InlineData("media")]
    [InlineData("a--b")]
    public void Refuses_A_Malformed_Or_Reserved_Slug(string slug)
    {
        var result = new SaveBlogDraftRequestValidator(new KeyEchoLocalizer()).Validate(Draft(slug: slug));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(SaveBlogDraftRequest.Slug)
                                         && e.ErrorMessage == "BLOG_SLUG_INVALID");
    }

    [Fact]
    public void Enforces_The_Length_Caps_Field_By_Field()
    {
        var result = new SaveBlogDraftRequestValidator(new KeyEchoLocalizer())
            .Validate(Draft(title: new string('t', BlogPost.MaxTitleLength + 1), excerpt: new string('e', BlogPost.MaxExcerptLength + 1)));

        result.Errors.Select(e => e.PropertyName).ShouldBe(
            [nameof(SaveBlogDraftRequest.Title), nameof(SaveBlogDraftRequest.Excerpt)], ignoreOrder: true);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ise-alim")]
    public void A_Blank_Slug_Means_None(string? slug)
    {
        new SaveBlogDraftRequestValidator(new KeyEchoLocalizer()).Validate(Draft(slug: slug)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void A_Revision_Below_One_Is_Not_A_Revision()
    {
        new SaveBlogDraftRequestValidator(new KeyEchoLocalizer()).Validate(Draft(revision: 0)).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("tr", 1, true)]
    [InlineData("en", 1000, true)]
    [InlineData("de", 1, false)]
    [InlineData("tr", 0, false)]
    [InlineData("tr", 1001, false)]
    public void Public_List_Query_Needs_A_Known_Language_And_A_Sane_Page(string lang, int page, bool valid)
    {
        new PublicBlogListQueryValidator(new KeyEchoLocalizer()).Validate(new PublicBlogListQuery(lang, page))
            .IsValid.ShouldBe(valid);
    }

    [Fact]
    public void Admin_List_Query_Filters_Are_Optional()
    {
        var validator = new AdminBlogListQueryValidator(new KeyEchoLocalizer());

        validator.Validate(new AdminBlogListQuery()).IsValid.ShouldBeTrue();
        validator.Validate(new AdminBlogListQuery(BlogPostStatus.Published, "en", 2)).IsValid.ShouldBeTrue();
        validator.Validate(new AdminBlogListQuery(Lang: "fr")).IsValid.ShouldBeFalse();
        validator.Validate(new AdminBlogListQuery((BlogPostStatus)42)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Grouped_Admin_List_Query_Has_A_Status_And_A_Page_And_No_Language()
    {
        var validator = new AdminBlogGroupedListQueryValidator();

        validator.Validate(new AdminBlogGroupedListQuery()).IsValid.ShouldBeTrue();
        validator.Validate(new AdminBlogGroupedListQuery(BlogPostStatus.Draft, 3)).IsValid.ShouldBeTrue();
        validator.Validate(new AdminBlogGroupedListQuery((BlogPostStatus)42)).IsValid.ShouldBeFalse();
        validator.Validate(new AdminBlogGroupedListQuery(Page: 0)).IsValid.ShouldBeFalse();
        validator.Validate(new AdminBlogGroupedListQuery(Page: 1001)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_Comment_Is_Ten_To_Three_Thousand_Characters_Once_Trimmed()
    {
        var create = new CreateBlogCommentRequestValidator(new KeyEchoLocalizer());
        var edit = new EditBlogCommentRequestValidator(new KeyEchoLocalizer());

        create.Validate(new CreateBlogCommentRequest("Tam on kar.")).IsValid.ShouldBeTrue();
        create.Validate(new CreateBlogCommentRequest("")).IsValid.ShouldBeFalse();
        create.Validate(new CreateBlogCommentRequest("kısa")).IsValid.ShouldBeFalse();
        // Nine letters padded with spaces to ten are still nine letters.
        create.Validate(new CreateBlogCommentRequest("  dokuzhrf  ")).IsValid.ShouldBeFalse();
        create.Validate(new CreateBlogCommentRequest(new string('a', BlogComment.MaxContentLength))).IsValid.ShouldBeTrue();
        create.Validate(new CreateBlogCommentRequest(new string('a', BlogComment.MaxContentLength + 1))).IsValid.ShouldBeFalse();
        edit.Validate(new EditBlogCommentRequest("kısa")).Errors.ShouldHaveSingleItem().ErrorMessage.ShouldBe("VALIDATION_BLOG_COMMENT_TOO_SHORT");
    }

    [Fact]
    public void A_Comment_Report_Needs_A_Note_Only_For_Other()
    {
        var validator = new ReportBlogCommentRequestValidator(new KeyEchoLocalizer());

        validator.Validate(new ReportBlogCommentRequest(BlogCommentReportReason.Spam, null)).IsValid.ShouldBeTrue();
        validator.Validate(new ReportBlogCommentRequest(BlogCommentReportReason.Other, null)).IsValid.ShouldBeFalse();
        validator.Validate(new ReportBlogCommentRequest(BlogCommentReportReason.Other, "Yazıyla ilgisi yok.")).IsValid.ShouldBeTrue();
        validator.Validate(new ReportBlogCommentRequest(BlogCommentReportReason.Spam, new string('x', BlogCommentReport.MaxNoteLength + 1))).IsValid.ShouldBeFalse();
        validator.Validate(new ReportBlogCommentRequest((BlogCommentReportReason)42, null)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Comment_List_Queries_Bound_The_Page_And_Check_The_Status()
    {
        new PublicBlogCommentListQueryValidator().Validate(new PublicBlogCommentListQuery(0)).IsValid.ShouldBeFalse();
        new MyBlogCommentListQueryValidator().Validate(new MyBlogCommentListQuery(1001)).IsValid.ShouldBeFalse();
        var admin = new AdminBlogCommentListQueryValidator();
        admin.Validate(new AdminBlogCommentListQuery(BlogCommentStatus.Pending, true, 2)).IsValid.ShouldBeTrue();
        admin.Validate(new AdminBlogCommentListQuery((BlogCommentStatus)42)).IsValid.ShouldBeFalse();
    }

    private static CreateBlogPostRequest Create(string title = "Başlık", string? excerpt = "Özet", string json = Doc,
        string html = "<p>x</p>", string language = "tr", string? slug = null) =>
        new(title, excerpt, json, html, language, slug, null);

    [Fact]
    public void Create_Needs_A_Known_Language()
    {
        var validator = new CreateBlogPostRequestValidator(new KeyEchoLocalizer());

        validator.Validate(Create(language: "en")).IsValid.ShouldBeTrue();
        validator.Validate(Create(language: "")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Create_Shares_The_Forms_Rules_With_The_Autosave()
    {
        var validator = new CreateBlogPostRequestValidator(new KeyEchoLocalizer());

        validator.Validate(Create(json: "{}")).Errors
            .ShouldContain(e => e.PropertyName == nameof(CreateBlogPostRequest.ContentJson)
                                && e.ErrorMessage == "VALIDATION_BLOG_CONTENT_JSON_INVALID");
        validator.Validate(Create(slug: "Not A Slug")).Errors
            .ShouldContain(e => e.PropertyName == nameof(CreateBlogPostRequest.Slug) && e.ErrorMessage == "BLOG_SLUG_INVALID");
        validator.Validate(Create(title: new string('a', BlogPost.MaxTitleLength + 1))).Errors
            .ShouldContain(e => e.PropertyName == nameof(CreateBlogPostRequest.Title));
    }

    [Theory]
    [InlineData("", null, "")]
    [InlineData("   ", "", "<p></p>")]
    [InlineData("", "", "<p>&nbsp;</p><p> </p>")]
    public void Create_Refuses_A_Draft_With_Nothing_Written(string title, string? excerpt, string html)
    {
        var result = new CreateBlogPostRequestValidator(new KeyEchoLocalizer()).Validate(Create(title, excerpt, html: html));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBlogPostRequest.Title) && e.ErrorMessage == "BLOG_POST_EMPTY");
    }

    [Theory]
    [InlineData("a", null, "")]
    [InlineData("", "a", "<p></p>")]
    [InlineData("", "", "<p>a</p>")]
    public void Create_Accepts_A_Draft_With_One_Character_Anywhere(string title, string? excerpt, string html)
    {
        new CreateBlogPostRequestValidator(new KeyEchoLocalizer()).Validate(Create(title, excerpt, html: html)).IsValid.ShouldBeTrue();
    }
}
