using AfterApply.Application.CvScan;
using AfterApply.Domain.Documents;
using Shouldly;

namespace AfterApply.UnitTests.CvScan;

/// <summary>
/// The checks, one case each. They are pure functions over <see cref="ExtractedCv"/>, so a case is
/// stated as the thing itself — a page with words in two columns, a line with a broken glyph in it —
/// rather than as a fixture file somebody has to open in a viewer to understand.
///
/// Every test starts from <see cref="ReadableCv"/>, a CV with nothing wrong with it, and breaks
/// exactly one thing. That is also the first assertion: the clean document scores 100, so any
/// finding below is caused by the change the test made and not by the baseline.
/// </summary>
public class CvScanChecksTests
{
    /// <summary>A CV a machine reads without complaint: one column, real text, Turkish letters
    /// intact, three sections, a date range, an e-mail and a phone number.</summary>
    private const string ReadableText = """
        Ahmet Yılmaz
        Kıdemli Yazılım Mühendisi
        ahmet.yilmaz@example.com | +90 532 123 45 67 | İstanbul

        DENEYİM
        Kıdemli Yazılım Mühendisi — Acme Yazılım A.Ş.
        01/2021 - halen
        Ödeme servisinin yeniden yazılmasını yürüttü.

        Yazılım Mühendisi — Beta Teknoloji
        06/2018 - 12/2020
        Sipariş takip servisini geliştirdi.

        EĞİTİM
        Boğaziçi Üniversitesi, Bilgisayar Mühendisliği, 2014 - 2018

        YETENEKLER
        C#, .NET, PostgreSQL, Docker
        """;

    private static ExtractedCv ReadableCv(
        string? text = null,
        CvFileFormat format = CvFileFormat.Pdf,
        int? pageCount = 1,
        int wordCount = 400,
        IReadOnlyList<ExtractedCvPage>? pages = null,
        string headerFooterText = "",
        int? tableCount = null,
        IReadOnlyList<ExtractedCvFont>? fonts = null)
    {
        var content = text ?? ReadableText;

        return new ExtractedCv(format, pageCount, wordCount, content, TextTruncated: false,
            pages ?? [new ExtractedCvPage(1, 595, 842, content, [])],
            headerFooterText, tableCount, fonts ?? []);
    }

    private static CvScanFindingCandidate? Find(ExtractedCv cv, CvScanFindingCode code) =>
        CvScanChecks.Run(cv).FirstOrDefault(finding => finding.Code == code);

    [Fact]
    public void A_Readable_Cv_Should_Produce_No_Findings()
    {
        CvScanChecks.Run(ReadableCv()).ShouldBeEmpty();
        CvScanScoring.Score(CvScanChecks.Run(ReadableCv())).Score.ShouldBe(100);
    }

    [Fact]
    public void A_Scanned_Page_Should_Cost_The_Whole_Machine_Readability_Category()
    {
        var cv = ReadableCv(text: "  ", wordCount: 0,
            pages: [new ExtractedCvPage(1, 595, 842, string.Empty, [])]);

        var finding = Find(cv, CvScanFindingCode.NoTextLayer).ShouldNotBeNull();

        finding.PotentialCost.ShouldBe(CvScanScoring.Weights[CvScanCategory.MachineReadability]);
        finding.Evidence.ShouldContain(evidence => evidence.Page == 1);
        CvScanScoring.Score(CvScanChecks.Run(cv)).Score.ShouldBeLessThanOrEqualTo(60);
    }

    /// <summary>The half-scanned CV: someone printed a page, signed it and put the photo back in.
    /// The rest is readable, so this costs a fraction rather than the category.</summary>
    [Fact]
    public void One_Empty_Page_In_A_Readable_Document_Should_Cost_Less_Than_A_Scan()
    {
        var cv = ReadableCv(pages:
        [
            new ExtractedCvPage(1, 595, 842, ReadableText, []),
            new ExtractedCvPage(2, 595, 842, string.Empty, [])
        ], pageCount: 2);

        var finding = Find(cv, CvScanFindingCode.NoTextLayer).ShouldNotBeNull();

        finding.PotentialCost.ShouldBe(15);
        finding.Evidence.Single().Page.ShouldBe(2);
    }

    [Fact]
    public void Glyphs_The_Font_Cannot_Name_Should_Be_Reported()
    {
        var cv = ReadableCv(text: ReadableText.Replace("Yazılım", "Yaz(cid:213)l(cid:213)m"));

        var finding = Find(cv, CvScanFindingCode.BrokenTurkishCharacters).ShouldNotBeNull();

        finding.Metrics["unreadableCharacterCount"].ShouldBeGreaterThan(0);
        finding.Evidence.ShouldAllBe(evidence => evidence.Quote != null);
    }

    /// <summary>
    /// The quieter failure: nothing looks broken, the Turkish letters were simply replaced with
    /// their ASCII shapes on the way out. A reader would never notice; a search for "Yazılım"
    /// never finds this CV again.
    /// </summary>
    [Fact]
    public void Turkish_Text_With_Every_Diacritic_Gone_Should_Be_Reported()
    {
        var stripped = string.Concat(Enumerable.Repeat(
            "Yazilim muhendisi olarak calisma deneyimi ve proje gelistirme egitimi. ", 12));

        var finding = Find(ReadableCv(text: stripped), CvScanFindingCode.BrokenTurkishCharacters)
            .ShouldNotBeNull();

        finding.Metrics["turkishLetterCount"].ShouldBe(0);
        finding.Metrics["turkishMarkerCount"].ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void An_English_Cv_Should_Not_Be_Accused_Of_Losing_Turkish_Letters()
    {
        var english = string.Concat(Enumerable.Repeat(
            "Senior software engineer with experience in payment systems and distributed services. ", 8));

        Find(ReadableCv(text: english), CvScanFindingCode.BrokenTurkishCharacters).ShouldBeNull();
    }

    [Fact]
    public void Two_Columns_Should_Be_Found_From_Where_The_Words_Sit()
    {
        var cv = ReadableCv(pages: [new ExtractedCvPage(1, 600, 800, ReadableText, TwoColumnWords())]);

        var finding = Find(cv, CvScanFindingCode.MultiColumnOrTableLayout).ShouldNotBeNull();

        finding.Metrics["columnCount"].ShouldBe(2);
        finding.Metrics["gutterPositionPercent"].ShouldBeInRange(30, 70);
        finding.Evidence.Single().Page.ShouldBe(1);
    }

    /// <summary>The check that keeps the one above honest: a single column has white space down
    /// both margins, and margins are not gutters.</summary>
    [Fact]
    public void A_Single_Column_Page_Should_Not_Be_Called_Two_Columns()
    {
        var cv = ReadableCv(pages: [new ExtractedCvPage(1, 600, 800, ReadableText, SingleColumnWords())]);

        Find(cv, CvScanFindingCode.MultiColumnOrTableLayout).ShouldBeNull();
    }

    [Fact]
    public void A_Docx_Built_Out_Of_Tables_Should_Be_Reported()
    {
        var cv = ReadableCv(format: CvFileFormat.Docx, pageCount: null, tableCount: 4);

        var finding = Find(cv, CvScanFindingCode.MultiColumnOrTableLayout).ShouldNotBeNull();
        finding.Metrics["tableCount"].ShouldBe(4);
    }

    [Fact]
    public void A_Missing_Section_Should_Cost_Points_And_Name_The_Ones_That_Were_Found()
    {
        var withoutEducation = ReadableText
            .Replace("EĞİTİM", "OKUDUKLARIM")
            .Replace("Boğaziçi Üniversitesi, Bilgisayar Mühendisliği, 2014 - 2018", "Boğaziçi, 2014");

        var finding = Find(ReadableCv(text: withoutEducation), CvScanFindingCode.SectionsOrDatesUnreadable)
            .ShouldNotBeNull();

        finding.Metrics["missingEducation"].ShouldBe(1);
        finding.Metrics["missingExperience"].ShouldBe(0);
        finding.Evidence.ShouldContain(evidence => evidence.Quote!.Contains("DENEYİM"));
    }

    [Fact]
    public void Years_Without_A_Range_Should_Not_Count_As_A_Timeline()
    {
        var noRanges = ReadableText
            .Replace("01/2021 - halen", "Halen sürüyor")
            .Replace("06/2018 - 12/2020", "İki yıl")
            .Replace("2014 - 2018", "dört yıl");

        var finding = Find(ReadableCv(text: noRanges), CvScanFindingCode.SectionsOrDatesUnreadable)
            .ShouldNotBeNull();

        finding.Metrics["dateRangeCount"].ShouldBe(0);
    }

    /// <summary>"kariyer" is a section heading and "kariyerim" is this product's own name; a CV
    /// that mentions us must not be credited with a section it does not have.</summary>
    [Fact]
    public void A_Heading_Should_Not_Match_Inside_A_Longer_Word()
    {
        CvScanVocabulary.ContainsPhrase("e kariyerim uzerinden takip", "kariyer").ShouldBeFalse();
        CvScanVocabulary.ContainsPhrase("kariyer ozeti", "kariyer").ShouldBeTrue();
    }

    [Fact]
    public void A_Cv_With_No_Readable_Email_Should_Be_Reported()
    {
        var cv = ReadableCv(text: ReadableText.Replace("ahmet.yilmaz@example.com", "posta ile ulasin"));

        var finding = Find(cv, CvScanFindingCode.ContactUnreadable).ShouldNotBeNull();

        finding.Metrics["emailFound"].ShouldBe(0);
        finding.Metrics["phoneFound"].ShouldBe(1);
        finding.PotentialCost.ShouldBe(10);
    }

    /// <summary>Contact details in a Word header are somewhere a parser routinely skips as
    /// furniture — the CV looks complete to a person and arrives without a way to reply.</summary>
    [Fact]
    public void Contact_Details_Only_In_A_Docx_Header_Should_Be_Reported()
    {
        var body = ReadableText.Replace("ahmet.yilmaz@example.com | +90 532 123 45 67 | İstanbul", "İstanbul");

        var cv = ReadableCv(text: body, format: CvFileFormat.Docx, pageCount: null,
            headerFooterText: "ahmet.yilmaz@example.com +90 532 123 45 67");

        var finding = Find(cv, CvScanFindingCode.ContactUnreadable).ShouldNotBeNull();

        finding.Metrics["emailFound"].ShouldBe(1);
        finding.Metrics["onlyInHeaderFooter"].ShouldBe(1);
        finding.PotentialCost.ShouldBe(7);
    }

    /// <summary>
    /// The PDF version of the same problem. A PDF has no header part to ask about, so the only
    /// thing that says "this is furniture" is where the ink sits: the top and bottom bands of the
    /// page. Contact details there, and nowhere else, are details a parser can miss.
    /// </summary>
    [Fact]
    public void Contact_Details_Only_In_A_Pdf_Page_Band_Should_Be_Reported()
    {
        var words = SingleColumnWords();
        words.Add(new ExtractedCvWord("ahmet.yilmaz@example.com", 50, 250, 790, 800));
        words.Add(new ExtractedCvWord("+90", 260, 290, 790, 800));
        words.Add(new ExtractedCvWord("532", 295, 325, 790, 800));
        words.Add(new ExtractedCvWord("123", 330, 360, 790, 800));
        words.Add(new ExtractedCvWord("45", 365, 385, 790, 800));
        words.Add(new ExtractedCvWord("67", 390, 410, 790, 800));

        var text = ReadableText.Replace("ahmet.yilmaz@example.com | +90 532 123 45 67 | İstanbul", "İstanbul")
                   + "\nahmet.yilmaz@example.com +90 532 123 45 67";

        var cv = ReadableCv(text: text, pages: [new ExtractedCvPage(1, 600, 800, text, words)]);

        var finding = Find(cv, CvScanFindingCode.ContactUnreadable).ShouldNotBeNull();

        finding.Metrics["onlyInHeaderFooter"].ShouldBe(1);
        finding.PotentialCost.ShouldBe(7);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 4)]
    [InlineData(5, 8)]
    [InlineData(9, 12)]
    public void Length_Should_Cost_More_As_The_Page_Count_Grows(int pageCount, int expectedCost)
    {
        var finding = Find(ReadableCv(pageCount: pageCount), CvScanFindingCode.LengthOutOfRange);

        (finding?.PotentialCost ?? 0).ShouldBe(expectedCost);
    }

    [Fact]
    public void A_Cv_With_Almost_No_Text_Should_Be_Reported_As_Too_Short()
    {
        var finding = Find(ReadableCv(wordCount: 90), CvScanFindingCode.LengthOutOfRange).ShouldNotBeNull();

        finding.PotentialCost.ShouldBe(12);
        finding.Metrics["wordCount"].ShouldBe(90);
    }

    /// <summary>A .docx has no pages until something renders it, so the estimate is marked as one
    /// rather than presented as a fact.</summary>
    [Fact]
    public void A_Docx_Page_Count_Should_Be_Reported_As_An_Estimate()
    {
        var finding = Find(ReadableCv(format: CvFileFormat.Docx, pageCount: null, wordCount: 3000),
            CvScanFindingCode.LengthOutOfRange).ShouldNotBeNull();

        finding.Metrics["pageCountEstimated"].ShouldBe(1);
        finding.Metrics["pageCount"].ShouldBe(7);
    }

    [Fact]
    public void Too_Many_Typefaces_Should_Be_Reported_With_A_Sample_To_Look_At()
    {
        var fonts = new List<ExtractedCvFont>
        {
            new("ABCDEE+Calibri", 11, 2000, 1, "Ödeme servisi"),
            new("Calibri-Bold", 11, 400, 1, "DENEYİM"),
            new("Georgia", 11, 300, 1, "Acme Yazılım"),
            new("Courier New", 11, 200, 1, "C#, .NET"),
            new("Comic Sans MS", 11, 100, 2, "Beta Teknoloji"),
            new("Impact", 11, 40, 2, "YETENEKLER")
        };

        var finding = Find(ReadableCv(fonts: fonts), CvScanFindingCode.InconsistentFormatting)
            .ShouldNotBeNull();

        // Calibri and Calibri-Bold are one typeface in two weights, not two typefaces.
        finding.Metrics["fontFamilyCount"].ShouldBe(5);
        finding.Evidence.Single().Quote.ShouldBe("YETENEKLER");
    }

    [Fact]
    public void One_Typeface_In_Several_Weights_Should_Not_Be_Called_Inconsistent()
    {
        var fonts = new List<ExtractedCvFont>
        {
            new("ABCDEE+Calibri", 11, 2000, 1, "Ödeme servisi"),
            new("ABCDEE+Calibri-Bold", 14, 400, 1, "DENEYİM"),
            new("ABCDEE+Calibri-Italic", 11, 200, 1, "Acme")
        };

        Find(ReadableCv(fonts: fonts), CvScanFindingCode.InconsistentFormatting).ShouldBeNull();
    }

    /// <summary>Two columns, 600pt wide page: words from 50 to 250, a gutter, then 320 to 550.</summary>
    private static List<ExtractedCvWord> TwoColumnWords()
    {
        var words = new List<ExtractedCvWord>();

        for (var line = 0; line < 10; line++)
        {
            var baseline = 700 - line * 20;

            for (var index = 0; index < 3; index++)
            {
                var left = 50 + index * 70;
                words.Add(new ExtractedCvWord($"sol{line}{index}", left, left + 60, baseline, baseline + 10));
            }

            for (var index = 0; index < 3; index++)
            {
                var left = 320 + index * 78;
                words.Add(new ExtractedCvWord($"sag{line}{index}", left, left + 70, baseline, baseline + 10));
            }
        }

        return words;
    }

    /// <summary>The same page as one column: every line runs the full width of the text area.</summary>
    private static List<ExtractedCvWord> SingleColumnWords()
    {
        var words = new List<ExtractedCvWord>();

        for (var line = 0; line < 10; line++)
        {
            var baseline = 700 - line * 20;

            for (var index = 0; index < 7; index++)
            {
                var left = 50 + index * 72;
                words.Add(new ExtractedCvWord($"kelime{line}{index}", left, left + 68, baseline, baseline + 10));
            }
        }

        return words;
    }
}
