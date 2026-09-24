using System.IO.Compression;
using System.Text;
using AfterApply.Infrastructure.CvScan;
using Shouldly;

namespace AfterApply.UnitTests.CvScan;

/// <summary>A PDF whose streams inflate past the budget is refused before the parser inflates them
/// in one call (2026-09-24).</summary>
public class PdfInflationGuardTests
{
    private const long Budget = 40L * 1024 * 1024;

    [Fact]
    public void A_Stream_That_Inflates_Past_The_Budget_Is_Caught()
    {
        // ~64 MB of zeros compresses to well under a megabyte — the shape of a decompression bomb.
        var pdf = Pdf(("/Filter /FlateDecode", Deflate(new byte[64 * 1024 * 1024])));

        pdf.Length.ShouldBeLessThan(1024 * 1024);
        PdfInflationGuard.InflatesBeyond(pdf, Budget).ShouldBeTrue();
    }

    [Fact]
    public void Many_Streams_Count_Together()
    {
        var chunk = Deflate(new byte[15 * 1024 * 1024]);
        var pdf = Pdf(("/Filter /FlateDecode", chunk), ("/Filter /FlateDecode", chunk), ("/Filter /FlateDecode", chunk));

        PdfInflationGuard.InflatesBeyond(pdf, Budget).ShouldBeTrue();
    }

    [Fact]
    public void An_Ordinary_Compressed_Page_Passes()
    {
        var content = Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("BT /F1 12 Tf 72 712 Td (Deneyim) Tj ET\n", 2000)));
        PdfInflationGuard.InflatesBeyond(Pdf(("/Filter /FlateDecode", Deflate(content))), Budget).ShouldBeFalse();
    }

    [Fact]
    public void A_Stream_With_Another_Filter_Is_Left_To_The_Parser() =>
        PdfInflationGuard.InflatesBeyond(Pdf(("/Filter /DCTDecode", Deflate(new byte[64 * 1024 * 1024]))), Budget).ShouldBeFalse();

    [Fact]
    public void Damaged_Stream_Data_Does_Not_Throw() =>
        PdfInflationGuard.InflatesBeyond(Pdf(("/Filter /FlateDecode", [1, 2, 3, 4, 5])), Budget).ShouldBeFalse();

    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }

        return output.ToArray();
    }

    private static byte[] Pdf(params (string Dictionary, byte[] Data)[] streams)
    {
        using var pdf = new MemoryStream();
        void Write(string text) => pdf.Write(Encoding.ASCII.GetBytes(text));

        Write("%PDF-1.7\n");
        for (var i = 0; i < streams.Length; i++)
        {
            Write($"{i + 1} 0 obj\n<< /Length {streams[i].Data.Length} {streams[i].Dictionary} >>\nstream\r\n");
            pdf.Write(streams[i].Data);
            Write("\r\nendstream\nendobj\n");
        }

        Write("%%EOF\n");
        return pdf.ToArray();
    }
}
