using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using AfterApply.Application.CvScan;
using AfterApply.Domain.Documents;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace AfterApply.Infrastructure.CvScan;

/// <summary>
/// The only code in the product that opens a stranger's CV. Two readers, both pure managed code:
/// PdfPig for PDF and the OpenXml SDK for .docx. Nothing is shelled out to a converter and nothing
/// is written to disk — the file exists as bytes in this process for the length of one request and
/// then does not exist at all.
///
/// Both readers run inside three bounds, because this sits behind an endpoint with no account in
/// front of it: the upload size cap (5 MB, enforced before this is called), a page cap, and a
/// deadline checked between pages. The .docx path adds one more — a zip may declare far more
/// content than it ships.
/// </summary>
internal sealed class CvTextExtractor(IOptions<CvScanOptions> options) : ICvTextExtractor
{
    public async Task<ExtractedCv> ExtractAsync(Stream content, CvFileFormat format,
        CancellationToken cancellationToken)
    {
        // Read the whole file first, asynchronously: both libraries want a seekable source, and
        // an upload is already capped at 5 MB, so this is bounded memory rather than a stream of
        // unknown length.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Seek(0, SeekOrigin.Begin);

        // Parsing itself is synchronous CPU work with no async API on either library, so it runs
        // on the request's own thread — wrapping it in Task.Run would move the block rather than
        // remove it. What keeps that honest is the deadline below: no single request can hold the
        // thread for longer than CvScan:ParseTimeoutSeconds.
        var deadline = Stopwatch.StartNew();

        return format switch
        {
            CvFileFormat.Pdf => ExtractPdf(buffer.ToArray(), deadline, cancellationToken),
            CvFileFormat.Docx => ExtractDocx(buffer, deadline, cancellationToken),
            _ => throw new CvExtractionException(CvExtractionFailure.UnsupportedLegacyFormat)
        };
    }

    private ExtractedCv ExtractPdf(byte[] bytes, Stopwatch deadline, CancellationToken cancellationToken)
    {
        PdfDocument document;
        try
        {
            // Lenient on purpose: a CV exported by a template site is routinely a slightly
            // malformed PDF, and refusing to read it would report a tooling problem as the
            // reader's problem. SkipMissingFonts keeps a page with an unembedded font readable —
            // and the letters that do come out wrong are exactly what the Turkish-character check
            // is there to notice.
            document = PdfDocument.Open(bytes, new ParsingOptions
            {
                UseLenientParsing = true,
                SkipMissingFonts = true
            });
        }
        catch (PdfDocumentEncryptedException)
        {
            throw new CvExtractionException(CvExtractionFailure.PasswordProtected);
        }
        // OperationCanceledException is excluded deliberately: a caller who hung up is not a
        // damaged file, and reporting one as the other would put a misleading message in front of
        // the next person whose upload actually is damaged.
        catch (Exception exception) when (exception is not (CvExtractionException or OperationCanceledException))
        {
            throw new CvExtractionException(CvExtractionFailure.Corrupt);
        }

        using (document)
        {
            if (document.IsEncrypted)
            {
                throw new CvExtractionException(CvExtractionFailure.PasswordProtected);
            }

            if (document.NumberOfPages > options.Value.MaxPages)
            {
                throw new CvExtractionException(CvExtractionFailure.TooManyPages);
            }

            var pages = new List<ExtractedCvPage>();
            var fonts = new Dictionary<(string Name, double Size), FontUsage>();
            var text = new StringBuilder();
            var truncated = false;
            var wordCount = 0;

            for (var number = 1; number <= document.NumberOfPages; number++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureWithinDeadline(deadline);

                var page = document.GetPage(number);

                var words = page.GetWords()
                    .Select(word => new ExtractedCvWord(word.Text,
                        word.BoundingBox.Left, word.BoundingBox.Right,
                        word.BoundingBox.Bottom, word.BoundingBox.Top))
                    .ToList();

                wordCount += words.Count(word => !string.IsNullOrWhiteSpace(word.Text));

                foreach (var letter in page.Letters)
                {
                    var key = (letter.FontName ?? string.Empty, Math.Round(letter.PointSize, 1));
                    if (!fonts.TryGetValue(key, out var usage))
                    {
                        usage = new FontUsage(number);
                        fonts[key] = usage;
                    }

                    usage.Add(letter.Value);
                }

                // Content order rather than the raw text stream: it is the reading order a naive
                // parser gets, which is the thing being reported on. A two-column page arrives
                // here already braided, and that braid is what the reader is shown.
                var pageText = ContentOrderTextExtractor.GetText(page) ?? string.Empty;
                var (kept, wasTruncated) = Append(text, pageText);
                truncated |= wasTruncated;

                pages.Add(new ExtractedCvPage(number, page.Width, page.Height, kept, words));
            }

            return new ExtractedCv(CvFileFormat.Pdf, document.NumberOfPages, wordCount, text.ToString(),
                truncated, pages, HeaderFooterText: string.Empty, TableCount: null,
                fonts.Select(entry => new ExtractedCvFont(entry.Key.Name, entry.Key.Size,
                    entry.Value.GlyphCount, entry.Value.Page, entry.Value.Sample)).ToList());
        }
    }

    private ExtractedCv ExtractDocx(MemoryStream buffer, Stopwatch deadline, CancellationToken cancellationToken)
    {
        GuardAgainstOversizedPackage(buffer);
        buffer.Seek(0, SeekOrigin.Begin);

        WordprocessingDocument document;
        try
        {
            document = WordprocessingDocument.Open(buffer, isEditable: false);
        }
        catch (Exception exception) when (exception is not (CvExtractionException or OperationCanceledException))
        {
            throw new CvExtractionException(CvExtractionFailure.Corrupt);
        }

        using (document)
        {
            var mainPart = document.MainDocumentPart;
            var body = mainPart?.Document?.Body
                ?? throw new CvExtractionException(CvExtractionFailure.Corrupt);

            var text = new StringBuilder();
            var truncated = false;
            var wordCount = 0;
            var fonts = new Dictionary<(string Name, double Size), FontUsage>();

            foreach (var paragraph in body.Descendants<Word.Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureWithinDeadline(deadline);

                var line = paragraph.InnerText;
                wordCount += line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

                var (_, wasTruncated) = Append(text, line + "\n");
                truncated |= wasTruncated;

                if (truncated)
                {
                    break;
                }

                CollectFonts(paragraph, fonts);
            }

            // A .docx says what a header and a footer are, so the contact check does not have to
            // guess from coordinates the way it does for a PDF.
            var headerFooter = new StringBuilder();
            foreach (var part in mainPart.HeaderParts)
            {
                headerFooter.AppendLine(part.Header?.InnerText ?? string.Empty);
            }

            foreach (var part in mainPart.FooterParts)
            {
                headerFooter.AppendLine(part.Footer?.InnerText ?? string.Empty);
            }

            var content = text.ToString();

            return new ExtractedCv(CvFileFormat.Docx,
                // No page count: a Word file has no pagination until something renders it, and
                // guessing here would put an invented number next to an evidenced finding.
                PageCount: null,
                wordCount, content, truncated,
                [new ExtractedCvPage(1, Width: 0, Height: 0, content, Words: [])],
                headerFooter.ToString(),
                body.Descendants<Word.Table>().Count(),
                fonts.Select(entry => new ExtractedCvFont(entry.Key.Name, entry.Key.Size,
                    entry.Value.GlyphCount, entry.Value.Page, entry.Value.Sample)).ToList());
        }
    }

    private static void CollectFonts(Word.Paragraph paragraph,
        Dictionary<(string Name, double Size), FontUsage> fonts)
    {
        foreach (var run in paragraph.Descendants<Word.Run>())
        {
            var properties = run.RunProperties;
            var name = properties?.RunFonts?.Ascii?.Value;
            var halfPoints = properties?.FontSize?.Val?.Value;

            // Runs that state neither are set by the document's default style — one face, so they
            // say nothing about consistency and are left out rather than counted as a fourth
            // typeface.
            if (name is null && halfPoints is null)
            {
                continue;
            }

            var size = halfPoints is not null && double.TryParse(halfPoints, out var parsed) ? parsed / 2 : 0;
            var key = (name ?? "default", Math.Round(size, 1));

            if (!fonts.TryGetValue(key, out var usage))
            {
                usage = new FontUsage(1);
                fonts[key] = usage;
            }

            foreach (var character in run.InnerText)
            {
                usage.Add(character);
            }
        }
    }

    /// <summary>
    /// Refuses a package that expands to far more than it should before anything is decompressed.
    /// The sizes come from the archive's own central directory, so a zip bomb is answered by
    /// reading a few hundred bytes of headers.
    /// </summary>
    private void GuardAgainstOversizedPackage(MemoryStream buffer)
    {
        try
        {
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
            var declared = archive.Entries.Sum(entry => entry.Length);

            if (declared > options.Value.MaxUncompressedBytes)
            {
                throw new CvExtractionException(CvExtractionFailure.TooExpensive);
            }
        }
        catch (InvalidDataException)
        {
            throw new CvExtractionException(CvExtractionFailure.Corrupt);
        }
    }

    private void EnsureWithinDeadline(Stopwatch deadline)
    {
        if (deadline.Elapsed > TimeSpan.FromSeconds(options.Value.ParseTimeoutSeconds))
        {
            throw new CvExtractionException(CvExtractionFailure.TooExpensive);
        }
    }

    /// <summary>Appends up to the extraction cap and reports what was kept, so a page's own text
    /// and the document's text never disagree about where the cut fell.</summary>
    private (string Kept, bool Truncated) Append(StringBuilder text, string addition)
    {
        var room = options.Value.MaxExtractedCharacters - text.Length;
        if (room <= 0)
        {
            return (string.Empty, true);
        }

        // The separator counts against the budget too, so the cap is a cap rather than "the cap
        // plus one newline per page".
        if (addition.Length < room)
        {
            text.Append(addition);
            if (!addition.EndsWith('\n'))
            {
                text.Append('\n');
            }

            return (addition, false);
        }

        var kept = addition[..room];
        text.Append(kept);
        return (kept, true);
    }

    /// <summary>Mutable while extracting, flattened into <see cref="ExtractedCvFont"/> after.</summary>
    private sealed class FontUsage(int page)
    {
        private readonly StringBuilder _sample = new();

        public int Page { get; } = page;

        public int GlyphCount { get; private set; }

        public string Sample => _sample.ToString().Trim();

        public void Add(char value)
        {
            GlyphCount++;

            // A few words is enough to point at, and this string ends up in a response body.
            if (_sample.Length < 60)
            {
                _sample.Append(char.IsControl(value) ? ' ' : value);
            }
        }

        public void Add(string value)
        {
            foreach (var character in value)
            {
                Add(character);
            }
        }
    }
}
