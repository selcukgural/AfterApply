using System.IO.Compression;
using System.Text;

namespace AfterApply.Infrastructure.CvScan;

/// <summary>
/// Measures what a PDF's Flate-compressed streams inflate to, before the file reaches the parser
/// (2026-09-24). A 5 MB upload can hold a page whose content stream inflates to gigabytes; the
/// parser inflates it in a single call, where neither the page cap nor the between-pages deadline
/// can step in, and the process runs out of memory. This walks the raw bytes for
/// <c>stream … endstream</c> bodies whose dictionary names FlateDecode and inflates each into a
/// counter — never into memory — stopping the moment the running total passes the budget.
///
/// A stream that does not inflate cleanly (encrypted, damaged, a different filter spelled oddly)
/// counts what it produced before failing and is otherwise left to the parser, which treats a bad
/// file as bad on its own.
/// </summary>
public static class PdfInflationGuard
{
    private static readonly byte[] StreamKeyword = Encoding.ASCII.GetBytes("stream");
    private static readonly byte[] EndStreamKeyword = Encoding.ASCII.GetBytes("endstream");
    private const int DictionaryLookBehind = 1024;

    public static bool InflatesBeyond(byte[] pdf, long budget)
    {
        var total = 0L;
        var buffer = new byte[81920];
        var position = 0;

        while (true)
        {
            var keyword = IndexOf(pdf, StreamKeyword, position);
            if (keyword < 0)
            {
                return false;
            }

            position = keyword + StreamKeyword.Length;

            // "endstream" contains "stream"; only a real stream start is followed by an end of line.
            if (keyword >= 3 && pdf[keyword - 3] == 'e' && pdf[keyword - 2] == 'n' && pdf[keyword - 1] == 'd')
            {
                continue;
            }

            var dataStart = position;
            if (dataStart < pdf.Length && pdf[dataStart] == '\r')
            {
                dataStart++;
            }

            if (dataStart >= pdf.Length || pdf[dataStart] != '\n')
            {
                continue;
            }

            dataStart++;
            var dataEnd = IndexOf(pdf, EndStreamKeyword, dataStart);
            if (dataEnd < 0)
            {
                dataEnd = pdf.Length;
            }

            var dictionaryFrom = Math.Max(0, keyword - DictionaryLookBehind);
            var dictionary = Encoding.ASCII.GetString(pdf, dictionaryFrom, keyword - dictionaryFrom);
            var dictionaryStart = dictionary.LastIndexOf("<<", StringComparison.Ordinal);
            if (dictionaryStart >= 0 && dictionary.IndexOf("FlateDecode", dictionaryStart, StringComparison.Ordinal) >= 0)
            {
                total += Inflate(pdf, dataStart, dataEnd - dataStart, budget - total, buffer);
                if (total > budget)
                {
                    return true;
                }
            }

            position = dataEnd;
        }
    }

    // Bytes the stream inflates to, reading no further than one byte past what the budget allows.
    private static long Inflate(byte[] pdf, int offset, int length, long remaining, byte[] buffer)
    {
        var produced = 0L;
        try
        {
            using var zlib = new ZLibStream(new MemoryStream(pdf, offset, length, writable: false), CompressionMode.Decompress);
            int read;
            while ((read = zlib.Read(buffer, 0, buffer.Length)) > 0)
            {
                produced += read;
                if (produced > remaining)
                {
                    break;
                }
            }
        }
        catch (InvalidDataException)
        {
            // Not a clean zlib stream; what came out before the failure still counts.
        }

        return produced;
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int from) =>
        from >= haystack.Length ? -1 : haystack.AsSpan(from).IndexOf(needle) is var index and >= 0 ? from + index : -1;
}
