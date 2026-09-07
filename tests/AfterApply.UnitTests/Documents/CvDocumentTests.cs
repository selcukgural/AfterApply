using AfterApply.Domain.Documents;
using Shouldly;

namespace AfterApply.UnitTests.Documents;

public class CvDocumentTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_Builds_A_Storage_Key_From_Ids_Only()
    {
        var document = CvDocument.Create(UserId, "Özgeçmiş 2026.pdf", CvFileFormat.Pdf, 284_000,
            isDefault: true, Now);

        // Nothing the uploader chose appears in the key — that is what makes the key safe to use
        // as a path, and it is why the display name never has to be.
        document.StorageObjectName.ShouldBe($"cvs/{UserId:D}/{document.Id:D}.pdf");
        document.StorageObjectName.ShouldNotContain("Özgeçmiş");
    }

    [Theory]
    [InlineData(CvFileFormat.Pdf, ".pdf")]
    [InlineData(CvFileFormat.Doc, ".doc")]
    [InlineData(CvFileFormat.Docx, ".docx")]
    public void Storage_Key_Carries_The_Format_Extension(CvFileFormat format, string extension)
    {
        var document = CvDocument.Create(UserId, "cv" + extension, format, 1024, isDefault: false, Now);

        document.StorageObjectName.ShouldEndWith(extension);
    }

    [Fact]
    public void Two_Users_Never_Share_A_Prefix()
    {
        var otherUserId = Guid.CreateVersion7();

        var mine = CvDocument.Create(UserId, "cv.pdf", CvFileFormat.Pdf, 1024, isDefault: false, Now);
        var theirs = CvDocument.Create(otherUserId, "cv.pdf", CvFileFormat.Pdf, 1024, isDefault: false, Now);

        mine.StorageObjectName.ShouldStartWith($"cvs/{UserId:D}/");
        theirs.StorageObjectName.ShouldStartWith($"cvs/{otherUserId:D}/");
        mine.StorageObjectName.ShouldNotBe(theirs.StorageObjectName);
    }

    [Fact]
    public void Create_Stamps_The_Audit_Fields()
    {
        var document = CvDocument.Create(UserId, "cv.pdf", CvFileFormat.Pdf, 1024, isDefault: false, Now);

        document.UserId.ShouldBe(UserId);
        document.FileName.ShouldBe("cv.pdf");
        document.SizeBytes.ShouldBe(1024);
        document.IsDefault.ShouldBeFalse();
        document.UploadedAt.ShouldBe(Now);
        document.CreatedAt.ShouldBe(Now);
        document.UpdatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_Refuses_A_Document_With_No_Bytes(long sizeBytes)
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            CvDocument.Create(UserId, "cv.pdf", CvFileFormat.Pdf, sizeBytes, isDefault: false, Now));
    }

    [Fact]
    public void MarkAsDefault_Sets_The_Flag_And_Touches_The_Row()
    {
        var document = CvDocument.Create(UserId, "cv.pdf", CvFileFormat.Pdf, 1024, isDefault: false, Now);
        var later = Now.AddHours(1);

        document.MarkAsDefault(later);

        document.IsDefault.ShouldBeTrue();
        document.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void MarkAsDefault_Is_A_No_Op_When_It_Already_Is()
    {
        var document = CvDocument.Create(UserId, "cv.pdf", CvFileFormat.Pdf, 1024, isDefault: true, Now);

        document.MarkAsDefault(Now.AddHours(1));

        document.IsDefault.ShouldBeTrue();
        // Not touched: nothing about the row changed, so its UpdatedAt should not move either.
        document.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void ClearDefault_Unsets_The_Flag()
    {
        var document = CvDocument.Create(UserId, "cv.pdf", CvFileFormat.Pdf, 1024, isDefault: true, Now);
        var later = Now.AddHours(1);

        document.ClearDefault(later);

        document.IsDefault.ShouldBeFalse();
        document.UpdatedAt.ShouldBe(later);
    }

    [Fact]
    public void ClearDefault_Is_A_No_Op_When_It_Already_Is_Not()
    {
        var document = CvDocument.Create(UserId, "cv.pdf", CvFileFormat.Pdf, 1024, isDefault: false, Now);

        document.ClearDefault(Now.AddHours(1));

        document.IsDefault.ShouldBeFalse();
        document.UpdatedAt.ShouldBe(Now);
    }

    [Fact]
    public void The_Per_User_Cap_Is_Ten()
    {
        // Quoted in the UI, the help centre and the privacy text. If this ever changes, all four
        // have to change together — this test is the reminder.
        CvDocument.MaxPerUser.ShouldBe(10);
    }
}
