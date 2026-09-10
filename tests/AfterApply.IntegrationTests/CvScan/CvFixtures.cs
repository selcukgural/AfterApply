using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Word = DocumentFormat.OpenXml.Wordprocessing;

namespace AfterApply.IntegrationTests.CvScan;

/// <summary>
/// Real files, built in memory for each test rather than checked in as binaries.
///
/// Two reasons. A committed PDF is opaque — nobody reviewing a test can see whether it is one
/// column or two without opening it in a viewer, which is precisely the property the two-column
/// test is about. And a CV fixture that lives in the repository is a CV in the repository; these
/// are generated from obviously synthetic text instead.
///
/// The text is English and ASCII on purpose: the Standard 14 fonts a builder can embed without
/// shipping a font file cannot encode ı, ş or ğ, so a Turkish fixture here would fail to build for
/// a reason that has nothing to do with what is being tested. The Turkish-letter check is covered
/// where it belongs — in the unit tests, over an extracted document.
/// </summary>
internal static class CvFixtures
{
    /// <summary>Enough words that the length check stays quiet (it wants 150) without being so
    /// long that it trips the page-count rule.</summary>
    private static readonly string[] Body =
    [
        "Led the rewrite of the payment service and moved settlement onto a queue based design.",
        "Owned the order tracking service end to end including its on call rotation and alerts.",
        "Built the reporting pipeline that feeds the finance team their weekly reconciliation.",
        "Mentored two junior engineers through their first production incidents and reviews.",
        "Reduced the checkout error rate by rewriting the retry logic around the card gateway.",
        "Introduced contract tests between the storefront and the inventory service interfaces.",
        "Migrated the search index from a nightly rebuild to an incremental streaming update.",
        "Wrote the runbook the team still uses for database failover and connection draining.",
        "Replaced a hand rolled scheduler with a queue and cut duplicate job runs to zero.",
        "Ran the load tests that sized the cluster before the seasonal campaign traffic arrived.",
        "Refactored the notification service so a failed provider no longer blocked the others.",
        "Documented the payment reconciliation rules that had lived only in one person's head."
    ];

    /// <summary>A CV a parser has no trouble with: one column, real text, ordinary headings,
    /// date ranges, an e-mail and a phone number. It is the baseline every other fixture breaks
    /// exactly one thing about.</summary>
    public static byte[] ReadablePdf()
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        var lines = new List<string>
        {
            "Ahmet Yilmaz",
            "Senior Software Engineer",
            "ahmet.yilmaz@example.com | +90 532 123 45 67 | Istanbul",
            "EXPERIENCE",
            "Senior Software Engineer, Acme Software  01/2021 - present"
        };

        lines.AddRange(Body[..6]);
        lines.Add("Software Engineer, Beta Technology  06/2018 - 12/2020");
        lines.AddRange(Body[6..]);
        lines.Add("EDUCATION");
        lines.Add("Bogazici University, Computer Engineering, 2014 - 2018");
        lines.Add("SKILLS");
        lines.Add("C#, .NET, PostgreSQL, Docker, Kubernetes, RabbitMQ, React, TypeScript");

        var y = 800.0;
        foreach (var line in lines)
        {
            page.AddText(line, 9, new PdfPoint(40, y), font);
            y -= 18;
        }

        return builder.Build();
    }

    /// <summary>
    /// The same content in two columns. The gutter is real geometry — nothing on the page crosses
    /// x = 300 to x = 340 — because that is what the check reads, and a fixture that faked it
    /// would test nothing.
    /// </summary>
    public static byte[] TwoColumnPdf()
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);

        var y = 800.0;
        foreach (var line in Body)
        {
            page.AddText(Trim(line, 42), 8, new PdfPoint(40, y), font);
            page.AddText(Trim(line, 42), 8, new PdfPoint(340, y), font);
            y -= 16;
        }

        page.AddText("EXPERIENCE", 9, new PdfPoint(40, y - 20), font);
        page.AddText("EDUCATION 2014 - 2018", 9, new PdfPoint(340, y - 20), font);
        page.AddText("ahmet.yilmaz@example.com +90 532 123 45 67", 8, new PdfPoint(40, y - 40), font);

        return builder.Build();
    }

    /// <summary>A page with no text on it at all — what a scan, a photo or an exported image
    /// looks like to a reader that can only read text.</summary>
    public static byte[] TextlessPdf()
    {
        using var builder = new PdfDocumentBuilder();
        builder.AddPage(PageSize.A4);
        return builder.Build();
    }

    /// <summary>
    /// A .docx whose contact details live only in the document's header part — a real header, not
    /// a band of ink near the top of a page, which is the distinction the check turns on.
    /// </summary>
    public static byte[] DocxWithContactInHeader()
    {
        using var stream = new MemoryStream();

        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            var body = new Word.Body();

            void Paragraph(string text) =>
                body.AppendChild(new Word.Paragraph(new Word.Run(new Word.Text(text) { Space = SpaceProcessingModeValues.Preserve })));

            Paragraph("Ahmet Yilmaz");
            Paragraph("Senior Software Engineer, Istanbul");
            Paragraph("EXPERIENCE");
            Paragraph("Senior Software Engineer, Acme Software  01/2021 - present");
            foreach (var line in Body)
            {
                Paragraph(line);
            }

            Paragraph("EDUCATION");
            Paragraph("Bogazici University, Computer Engineering, 2014 - 2018");
            Paragraph("SKILLS");
            Paragraph("C#, .NET, PostgreSQL, Docker");

            mainPart.Document = new Word.Document(body);

            var headerPart = mainPart.AddNewPart<HeaderPart>();
            headerPart.Header = new Word.Header(
                new Word.Paragraph(new Word.Run(new Word.Text("ahmet.yilmaz@example.com +90 532 123 45 67"))));
            headerPart.Header.Save();
            mainPart.Document.Save();
        }

        return stream.ToArray();
    }

    private static string Trim(string line, int length) => line.Length <= length ? line : line[..length];
}
