using AfterApply.Domain.Blog;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

/// <summary>The generated cover's line and icon (2026-09-27): the value, and its trip from the draft
/// to the published slot.</summary>
public class BlogCoverCardTests
{
    private static readonly Guid Author = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 27, 10, 0, 0, TimeSpan.Zero);

    private static BlogDraftContent Content(BlogCoverCard? card = null) =>
        new("İşe alım", "Özet", """{"type":"doc","content":[]}""", "<p>Merhaba</p>", BlogSeo.Empty, CoverCard: card);

    [Fact]
    public void Normalize_Trims_Folds_Whitespace_Lowercases_The_Icon_And_Blanks_To_Null()
    {
        var card = BlogCoverCard.Normalize("  İletildi\n ≠   okundu ", " Mail ");

        card.Hook.ShouldBe("İletildi ≠ okundu");
        card.Icon.ShouldBe("mail");
        BlogCoverCard.Normalize("   ", "").ShouldBe(BlogCoverCard.Empty);
        BlogCoverCard.Normalize(null, null).IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [InlineData("mail", true)]
    [InlineData("chart-column", true)]
    [InlineData("building-2", true)]
    [InlineData("Mail", false)]
    [InlineData("-mail", false)]
    [InlineData("mail-", false)]
    [InlineData("chart--column", false)]
    [InlineData("../mail", false)]
    [InlineData("<svg>", false)]
    [InlineData("mail icon", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Icon_Names_Are_Dash_Separated_Lowercase_Words(string? value, bool expected)
    {
        BlogCoverCard.IsIconName(value).ShouldBe(expected);
    }

    [Fact]
    public void Validity_Is_The_Store_Caps_And_The_Icon_Shape()
    {
        new BlogCoverCard(new string('a', BlogCoverCard.MaxHookLength), "mail").IsValid.ShouldBeTrue();
        new BlogCoverCard(new string('a', BlogCoverCard.MaxHookLength + 1), null).IsValid.ShouldBeFalse();
        new BlogCoverCard(null, "a" + new string('b', BlogCoverCard.MaxIconLength)).IsValid.ShouldBeFalse();
        new BlogCoverCard(null, "javascript:alert(1)").IsValid.ShouldBeFalse();
        BlogCoverCard.Empty.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void The_Card_Rides_The_Draft_And_Reaches_The_Published_Slot_Only_On_Publish()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);
        var card = new BlogCoverCard("İletildi ≠ okundu", "mail");

        post.SaveDraft(Content(card), expectedRevision: 1, T0);
        post.DraftCoverCard.ShouldBe(card);
        post.CoverCard.ShouldBe(BlogCoverCard.Empty);

        post.SetSlug("ise-alim", T0);
        post.Publish(T0.AddMinutes(1));
        post.CoverCard.ShouldBe(card);

        // Clearing it in the draft clears the published copy on the next publish, nothing sooner.
        post.SaveDraft(Content(), expectedRevision: 2, T0.AddMinutes(2));
        post.DraftCoverCard.ShouldBe(BlogCoverCard.Empty);
        post.CoverCard.ShouldBe(card);
        post.Publish(T0.AddMinutes(3));
        post.CoverCard.ShouldBe(BlogCoverCard.Empty);
    }

    [Fact]
    public void An_Invalid_Card_Is_Refused_Before_Anything_Changes()
    {
        var post = BlogPost.Create(Author, BlogLanguage.Tr, T0);

        Should.Throw<BlogPostContentInvalidException>(() =>
            post.SaveDraft(Content(new BlogCoverCard(null, "Not An Icon")), expectedRevision: 1, T0));
        post.Revision.ShouldBe(1);
        post.DraftCoverCard.ShouldBe(BlogCoverCard.Empty);
    }
}
