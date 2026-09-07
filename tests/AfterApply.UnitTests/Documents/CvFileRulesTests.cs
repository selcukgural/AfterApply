using System.Text;
using AfterApply.Application.Documents;
using AfterApply.Domain.Documents;
using Shouldly;

namespace AfterApply.UnitTests.Documents;

public class CvFileRulesTests
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    [Theory]
    [InlineData("cv.pdf", CvFileFormat.Pdf)]
    [InlineData("CV.PDF", CvFileFormat.Pdf)]
    [InlineData("ozgecmis.docx", CvFileFormat.Docx)]
    [InlineData("ozgecmis.doc", CvFileFormat.Doc)]
    [InlineData("Selcuk.Gural.CV.2026.pdf", CvFileFormat.Pdf)]
    public void FormatFromFileName_Reads_The_Claimed_Format(string fileName, CvFileFormat expected)
    {
        CvFileRules.FormatFromFileName(fileName).ShouldBe(expected);
    }

    [Theory]
    [InlineData("cv.exe")]
    [InlineData("cv.pdf.exe")]
    [InlineData("cv")]
    [InlineData("cv.html")]
    [InlineData("cv.pdf ")] // trailing space — a real Windows trick for hiding an extension
    public void FormatFromFileName_Refuses_Anything_Else(string fileName)
    {
        CvFileRules.FormatFromFileName(fileName).ShouldBeNull();
    }

    [Fact]
    public void InspectClaim_Accepts_A_Plausible_Upload()
    {
        CvFileRules.InspectClaim("cv.pdf", 284_000, MaxFileSizeBytes).ShouldBeNull();
    }

    [Theory]
    [InlineData("cv.exe", 100, CvFileProblem.UnsupportedExtension)]
    [InlineData("cv.pdf", 0, CvFileProblem.Empty)]
    [InlineData("cv.pdf", -1, CvFileProblem.Empty)]
    [InlineData("cv.pdf", 10 * 1024 * 1024 + 1, CvFileProblem.TooLarge)]
    public void InspectClaim_Names_The_Reason_It_Refused(string fileName, long length, CvFileProblem expected)
    {
        CvFileRules.InspectClaim(fileName, length, MaxFileSizeBytes).ShouldBe(expected);
    }

    [Fact]
    public void InspectClaim_Checks_The_Extension_Before_The_Size()
    {
        // An .exe of a plausible size must be refused for being an .exe, not pass because it fits.
        CvFileRules.InspectClaim("payload.exe", 1024, MaxFileSizeBytes)
            .ShouldBe(CvFileProblem.UnsupportedExtension);
    }

    [Theory]
    [InlineData(CvFileFormat.Pdf, new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x37 })] // %PDF-1.7
    [InlineData(CvFileFormat.Docx, new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00 })]
    [InlineData(CvFileFormat.Doc, new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 })]
    public void HeaderMatchesFormat_Accepts_The_Real_Container(CvFileFormat format, byte[] header)
    {
        CvFileRules.HeaderMatchesFormat(header, format).ShouldBeTrue();
    }

    [Fact]
    public void HeaderMatchesFormat_Refuses_An_Executable_Renamed_To_Pdf()
    {
        // "MZ..." — a Windows PE. The whole point of the header check.
        byte[] header = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00];

        CvFileRules.HeaderMatchesFormat(header, CvFileFormat.Pdf).ShouldBeFalse();
    }

    [Fact]
    public void HeaderMatchesFormat_Refuses_Html_Renamed_To_Pdf()
    {
        var header = Encoding.ASCII.GetBytes("<!DOCTYPE");

        CvFileRules.HeaderMatchesFormat(header, CvFileFormat.Pdf).ShouldBeFalse();
    }

    [Fact]
    public void HeaderMatchesFormat_Refuses_A_Zip_That_Is_Not_A_Local_File_Entry()
    {
        // PK\x05\x06 is an empty archive: a valid ZIP, never a .docx.
        byte[] emptyArchive = [0x50, 0x4B, 0x05, 0x06, 0x00, 0x00, 0x00, 0x00];

        CvFileRules.HeaderMatchesFormat(emptyArchive, CvFileFormat.Docx).ShouldBeFalse();
    }

    [Fact]
    public void HeaderMatchesFormat_Refuses_A_File_Too_Short_To_Carry_A_Signature()
    {
        CvFileRules.HeaderMatchesFormat([0x25, 0x50], CvFileFormat.Pdf).ShouldBeFalse();
        CvFileRules.HeaderMatchesFormat([], CvFileFormat.Pdf).ShouldBeFalse();
    }

    [Fact]
    public void HeaderMatchesFormat_Does_Not_Confuse_Docx_With_Doc()
    {
        byte[] zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00];

        CvFileRules.HeaderMatchesFormat(zip, CvFileFormat.Doc).ShouldBeFalse();
    }

    [Theory]
    [InlineData("../../etc/passwd.pdf", "passwd.pdf")]
    [InlineData("..\\..\\Windows\\cv.pdf", "cv.pdf")]
    [InlineData("/absolute/path/cv.pdf", "cv.pdf")]
    public void SanitizeFileName_Keeps_Only_The_Base_Name(string given, string expected)
    {
        CvFileRules.SanitizeFileName(given, CvFileFormat.Pdf).ShouldBe(expected);
    }

    [Fact]
    public void SanitizeFileName_Strips_Control_Characters()
    {
        CvFileRules.SanitizeFileName("cv\r\n\tInjected.pdf", CvFileFormat.Pdf).ShouldBe("cvInjected.pdf");
    }

    [Fact]
    public void SanitizeFileName_Strips_The_Bidi_Override_That_Disguises_An_Extension()
    {
        // With U+202E in the middle, the name renders right-to-left as "cv exe.pdf" — it looks
        // like a PDF in every UI that shows a file name. Removing the override leaves the name
        // reading as what it actually is.
        var disguised = "cv" + (char)0x202E + "fdp.exe";

        var sanitized = CvFileRules.SanitizeFileName(disguised, CvFileFormat.Pdf);

        sanitized.ShouldBe("cvfdp.exe");
        sanitized.ShouldNotContain(((char)0x202E).ToString());
    }

    [Fact]
    public void SanitizeFileName_Truncates_But_Keeps_The_Extension()
    {
        var given = new string('a', 500) + ".pdf";

        var sanitized = CvFileRules.SanitizeFileName(given, CvFileFormat.Pdf);

        sanitized.Length.ShouldBe(200);
        sanitized.ShouldEndWith(".pdf");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    public void SanitizeFileName_Falls_Back_To_A_Readable_Name(string given)
    {
        CvFileRules.SanitizeFileName(given, CvFileFormat.Pdf).ShouldBe("cv.pdf");
    }

    [Fact]
    public void SanitizeFileName_Leaves_A_Turkish_File_Name_Alone()
    {
        CvFileRules.SanitizeFileName("Özgeçmiş Güncel.pdf", CvFileFormat.Pdf).ShouldBe("Özgeçmiş Güncel.pdf");
    }
}
