using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace CvScanCorpus;

internal enum DocxVariant { Clean, TableLayout, ContactInHeader }

/// <summary>Word files built the way Word itself stores them: paragraphs in reading order, a real
/// table where the layout is a table, a real header part where the contact line is a header.</summary>
internal static class DocxCv
{
    public static void Write(string path, Persona p, DocxVariant variant)
    {
        var tr = p.Lang == "tr";
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        var body = new W.Body();
        main.Document = new W.Document(body);

        var contact = $"{p.Email} | {p.Phone} | {p.City}";

        if (variant == DocxVariant.ContactInHeader)
        {
            var header = main.AddNewPart<HeaderPart>();
            header.Header = new W.Header(Paragraph(contact, size: 18));
            var id = main.GetIdOfPart(header);
            body.Append(new W.SectionProperties(new W.HeaderReference { Type = W.HeaderFooterValues.Default, Id = id }));
        }

        var lines = new List<OpenXmlElement>
        {
            Paragraph(p.FullName, bold: true, size: 44),
            Paragraph(p.Title, size: 24)
        };

        if (variant != DocxVariant.ContactInHeader)
        {
            lines.Add(Paragraph(contact, size: 19));
        }

        var experience = new List<OpenXmlElement> { Paragraph(tr ? "İŞ DENEYİMİ" : "WORK EXPERIENCE", bold: true, size: 22) };
        foreach (var job in p.Jobs)
        {
            experience.Add(Paragraph(job.Title, bold: true, size: 19));
            experience.Add(Paragraph($"{job.Company} · {HtmlCv.Dates(job, DateStyle.Slash, tr)}", size: 18));
            experience.AddRange(job.Bullets.Select(bullet => Paragraph("• " + bullet, size: 19)));
        }

        var education = new List<OpenXmlElement>
        {
            Paragraph(tr ? "EĞİTİM" : "EDUCATION", bold: true, size: 22),
            Paragraph($"{p.School}, {p.Degree}, {p.SchoolStart} - {p.SchoolEnd}", size: 19)
        };

        var skills = new List<OpenXmlElement>
        {
            Paragraph(tr ? "YETENEKLER" : "SKILLS", bold: true, size: 22),
            Paragraph(string.Join(", ", p.Skills), size: 19),
            Paragraph(tr ? "DİLLER" : "LANGUAGES", bold: true, size: 22),
            Paragraph(string.Join(", ", p.Languages), size: 19)
        };

        var summary = new List<OpenXmlElement>
        {
            Paragraph(tr ? "HAKKIMDA" : "ABOUT ME", bold: true, size: 22),
            Paragraph(p.Summary, size: 19)
        };

        if (variant == DocxVariant.TableLayout)
        {
            // The two-column template: a borderless one-row table, sidebar on the left.
            var left = new W.TableCell(skills.Concat(education).Select(element => element.CloneNode(true)));
            var right = new W.TableCell(summary.Concat(experience).Select(element => element.CloneNode(true)));
            body.PrependChild(new W.Table(new W.TableProperties(new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct }),
                new W.TableRow(left, right)));
            foreach (var line in lines.AsEnumerable().Reverse())
            {
                body.PrependChild(line);
            }

            return;
        }

        var ordered = lines.Concat(summary).Concat(experience).Concat(education).Concat(skills).ToList();
        foreach (var element in ordered.AsEnumerable().Reverse())
        {
            // Prepended so a SectionProperties already in the body stays last, where Word expects it.
            body.PrependChild(element);
        }
    }

    private static W.Paragraph Paragraph(string text, bool bold = false, int size = 20)
    {
        var properties = new W.RunProperties(new W.RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" },
            new W.FontSize { Val = size.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        if (bold)
        {
            properties.PrependChild(new W.Bold());
        }

        return new W.Paragraph(new W.Run(properties, new W.Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }
}
