using AfterApply.Application.Blog;
using AfterApply.Domain.Blog;
using Shouldly;

namespace AfterApply.UnitTests.Blog;

public class BlogCommentTests
{
    private static readonly Guid Post = Guid.NewGuid();
    private static readonly Guid Reader = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly DateTimeOffset T0 = new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(5);

    [Fact]
    public void A_Readers_Comment_Waits_And_An_Admins_Is_On_The_Site_At_Once()
    {
        var readers = BlogComment.Create(Post, Reader, null, "  Bence en zor kısım sessizlik.\r\nCevap yok.  ", approved: false, T0);
        readers.Status.ShouldBe(BlogCommentStatus.Pending);
        readers.Content.ShouldBe("Bence en zor kısım sessizlik.\nCevap yok.");
        readers.EditedAt.ShouldBeNull();
        readers.ModeratedAt.ShouldBeNull();

        var admins = BlogComment.Create(Post, Admin, null, "Teşekkürler, çok haklısınız.", approved: true, T0);
        admins.Status.ShouldBe(BlogCommentStatus.Approved);
        admins.ModeratedByUserId.ShouldBe(Admin);
    }

    [Theory]
    [InlineData("kısa")]
    [InlineData("         ")]
    public void Create_Refuses_What_No_Validator_Saw(string content)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => BlogComment.Create(Post, Reader, null, content, false, T0));
        Should.Throw<ArgumentOutOfRangeException>(() => BlogComment.Create(Post, Reader, null, new string('a', BlogComment.MaxContentLength + 1), false, T0));
    }

    [Fact]
    public void The_Author_Edits_While_Pending_And_Never_After()
    {
        var comment = BlogComment.Create(Post, Reader, null, "İlk hâli, on karakterden uzun.", false, T0);

        comment.Edit("İlk hâli, on karakterden uzun.", T1);
        comment.EditedAt.ShouldBeNull("the same text is not an edit");

        comment.Edit("Düzeltilmiş hâli, yine uzun.", T1);
        comment.Content.ShouldBe("Düzeltilmiş hâli, yine uzun.");
        comment.EditedAt.ShouldBe(T1);
        comment.UpdatedAt.ShouldBe(T1);

        comment.Approve(Admin, T1);
        Should.Throw<BlogCommentLockedException>(() => comment.Edit("Bir daha düzenlemek istiyorum.", T1));

        var rejected = BlogComment.Create(Post, Reader, null, "Yayınlanmayacak bir yorum.", false, T0);
        rejected.Reject(Admin, T1);
        Should.Throw<BlogCommentLockedException>(() => rejected.Edit("Şimdi düzelttim, olur mu?", T1));
    }

    [Fact]
    public void Moderation_Records_Who_And_When_And_Is_Idempotent()
    {
        var comment = BlogComment.Create(Post, Reader, null, "Onaylanacak bir yorum.", false, T0);

        comment.Approve(Admin, T1);
        comment.Status.ShouldBe(BlogCommentStatus.Approved);
        comment.ModeratedAt.ShouldBe(T1);
        comment.ModeratedByUserId.ShouldBe(Admin);

        var later = T1.AddHours(1);
        comment.Approve(Guid.NewGuid(), later);
        comment.ModeratedAt.ShouldBe(T1, "approving an approved comment changes nothing");

        comment.Reject(Admin, later);
        comment.Status.ShouldBe(BlogCommentStatus.Rejected);
        comment.ModeratedAt.ShouldBe(later);
    }

    [Fact]
    public void A_Report_With_Reason_Other_Needs_A_Note_And_Closes_Once()
    {
        Should.Throw<BlogCommentReportNoteRequiredException>(() =>
            BlogCommentReport.Create(Guid.NewGuid(), Reader, BlogCommentReportReason.Other, "   ", T0));

        var report = BlogCommentReport.Create(Guid.NewGuid(), Reader, BlogCommentReportReason.Spam, null, T0);
        report.Status.ShouldBe(BlogCommentReportStatus.Open);

        Should.Throw<ArgumentOutOfRangeException>(() => report.Resolve(BlogCommentReportStatus.Open, Admin, T1));
        report.Resolve(BlogCommentReportStatus.ActionTaken, Admin, T1);
        report.Status.ShouldBe(BlogCommentReportStatus.ActionTaken);
        report.ResolvedByUserId.ShouldBe(Admin);

        report.Resolve(BlogCommentReportStatus.Dismissed, Guid.NewGuid(), T1.AddHours(1));
        report.Status.ShouldBe(BlogCommentReportStatus.ActionTaken, "a closed report stays as it was closed");
        report.ResolvedAt.ShouldBe(T1);
    }

    [Theory]
    [InlineData("Selin", "Yılmaz", "Selin Y.")]
    [InlineData("Selin", "yılmaz", "Selin Y.")]
    [InlineData("Selin", "ipek", "Selin İ.")]
    [InlineData("Selin", "Ipek", "Selin I.")]
    [InlineData(" Selin ", "", "Selin")]
    [InlineData("Selin", null, "Selin")]
    [InlineData("", "Yılmaz", null)]
    [InlineData(null, null, null)]
    public void The_Page_Name_Is_First_Name_And_Last_Initial_Or_Nothing(string? first, string? last, string? expected)
    {
        BlogCommentAuthorName.Format(first, last).ShouldBe(expected);
    }
}
