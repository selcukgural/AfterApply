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

    /// <summary>A picture of a CV reaches the recruiter's system with no sections and no contact
    /// details either. The score says so — it used to call this 60, "fair" — and the two
    /// consequences are marked as such, so the page does not present them as separate problems.</summary>
    [Fact]
    public void A_Cv_With_No_Text_At_All_Should_Score_Poor_With_Its_Consequences_Marked()
    {
        var cv = ReadableCv(text: "  ", wordCount: 0,
            pages: [new ExtractedCvPage(1, 595, 842, string.Empty, [], ImageCount: 1)]);

        var findings = CvScanChecks.Run(cv);

        findings.Single(f => f.Code == CvScanFindingCode.SectionsOrDatesUnreadable).Metrics["noText"].ShouldBe(1);
        findings.Single(f => f.Code == CvScanFindingCode.ContactUnreadable).Metrics["noText"].ShouldBe(1);
        CvScanScoring.Score(findings).Score.ShouldBeLessThan(55);
    }

    /// <summary>The half-scanned CV: someone printed a page, signed it and put the photo back in.
    /// The rest is readable, so this costs a fraction rather than the category.</summary>
    [Fact]
    public void One_Empty_Page_In_A_Readable_Document_Should_Cost_Less_Than_A_Scan()
    {
        var cv = ReadableCv(pages:
        [
            new ExtractedCvPage(1, 595, 842, ReadableText, []),
            new ExtractedCvPage(2, 595, 842, string.Empty, [], ImageCount: 1)
        ], pageCount: 2);

        var finding = Find(cv, CvScanFindingCode.NoTextLayer).ShouldNotBeNull();

        finding.PotentialCost.ShouldBe(15);
        finding.Evidence.Single().Page.ShouldBe(2);
    }

    /// <summary>A page with neither text nor a picture is a stray page break at the end of an
    /// export, not a scan. A parser loses nothing on it, so neither does the score.</summary>
    [Fact]
    public void A_Blank_Trailing_Page_Should_Not_Be_Called_A_Scan()
    {
        var cv = ReadableCv(pages:
        [
            new ExtractedCvPage(1, 595, 842, ReadableText, []),
            new ExtractedCvPage(2, 595, 842, string.Empty, [])
        ], pageCount: 2);

        Find(cv, CvScanFindingCode.NoTextLayer).ShouldBeNull();
    }

    /// <summary>Two roles and a summary in 120 words is brief, not unparseable. The short rule is
    /// for files with too little text to fill a profile, not for concise writers.</summary>
    [Fact]
    public void A_Concise_Cv_Should_Not_Be_Called_Too_Short()
    {
        Find(ReadableCv(wordCount: 120), CvScanFindingCode.LengthOutOfRange).ShouldBeNull();
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

    /// <summary>
    /// The sidebar template that used to slip through: the body runs almost to the sidebar, and
    /// the gutter between them is a few millimetres — 3.5% of the width on the real CV that exposed
    /// it. Narrow, but it still runs the height of the page with text on both sides.
    /// </summary>
    [Fact]
    public void A_Tight_Sidebar_Gutter_Should_Still_Be_Two_Columns()
    {
        var cv = ReadableCv(pages: [new ExtractedCvPage(1, 600, 800, ReadableText, TightSidebarWords())]);

        var finding = Find(cv, CvScanFindingCode.MultiColumnOrTableLayout).ShouldNotBeNull();

        finding.Metrics["gutterPositionPercent"].ShouldBeInRange(60, 70);
    }

    /// <summary>
    /// Headings stacked with nothing between them: what a sidebar template's content stream looks
    /// like once a machine reads it. The evidence is the stack itself, in the order it arrived.
    /// </summary>
    [Fact]
    public void Headings_Stacked_Without_Their_Content_Should_Be_Reported_As_Scrambled()
    {
        var scrambled = ReadableText
            .Replace("DENEYİM\n", "DİLLER\nİLETİŞİM\nDENEYİM\n")
            .Replace("EĞİTİM\n", "EĞİTİM\nSERTİFİKALAR\n");

        var finding = Find(ReadableCv(text: scrambled), CvScanFindingCode.ReadingOrderScrambled).ShouldNotBeNull();

        finding.Category.ShouldBe(CvScanCategory.MachineReadability);
        // Calibrated against outside tools, not the whole category: layout-aware parsers recover
        // much of a scrambled file, stream-order ones do not (DECISIONS.md 2026-09-25).
        finding.PotentialCost.ShouldBe(15);
        finding.Metrics["orphanHeadingCount"].ShouldBe(3);
        finding.Evidence.Single().Quote.ShouldBe("DİLLER / İLETİŞİM / DENEYİM");
    }

    /// <summary>A line made of known headings is one heading, not two columns meeting: on five
    /// thousand real CVs, "EDUCATIONAL QUALIFICATION" and "CONTACT ADDRESS" were the only lines that
    /// ever split that way.</summary>
    [Fact]
    public void A_Line_Of_Several_Headings_Should_Count_Once()
    {
        var text = ReadableText
            .Replace("DENEYİM\n", "İLETİŞİM İŞ DENEYİMİ\n")
            .Replace("EĞİTİM\n", "EMPLOYMENT OBJECTIVE\nEĞİTİM\n");

        Find(ReadableCv(text: text), CvScanFindingCode.ReadingOrderScrambled).ShouldBeNull();
    }

    /// <summary>The same heading twice in a row is a text effect — one heading drawn twice — not
    /// a scramble.</summary>
    [Fact]
    public void A_Heading_Drawn_Twice_Should_Not_Count_As_Stacked()
    {
        var text = ReadableText
            .Replace("DENEYİM\n", "DENEYİM\nDENEYİM\n")
            .Replace("EĞİTİM\n", "EĞİTİM\nEĞİTİM\n");

        Find(ReadableCv(text: text), CvScanFindingCode.ReadingOrderScrambled).ShouldBeNull();
    }

    /// <summary>"Education &amp; Certifications" is one heading someone wrote on purpose, not two
    /// columns that met on a line. Two of them in one CV must not add up to a scramble.</summary>
    [Fact]
    public void Headings_Joined_On_Purpose_Should_Not_Count_As_Braided()
    {
        var joined = ReadableText
            .Replace("EĞİTİM\n", "EĞİTİM & SERTİFİKALAR\n")
            .Replace("YETENEKLER\n", "YETENEKLER / DİLLER\n");

        Find(ReadableCv(text: joined), CvScanFindingCode.ReadingOrderScrambled).ShouldBeNull();
    }

    /// <summary>A shuffle of boxes has no stacked headings to find, but its text order climbs back up
    /// the page again and again. Three long climbs on a page is the line: real CVs, including
    /// sidebar layouts, stay at two or fewer.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, true)]
    [InlineData(7, true)]
    public void A_Page_Whose_Text_Order_Keeps_Climbing_Should_Be_Scrambled(int backtracks, bool scrambled)
    {
        var cv = ReadableCv(pages: [new ExtractedCvPage(1, 595, 842, ReadableText, [], Backtracks: backtracks)]);

        var finding = Find(cv, CvScanFindingCode.ReadingOrderScrambled);

        if (!scrambled)
        {
            finding.ShouldBeNull();
            return;
        }

        finding.ShouldNotBeNull().Metrics["backtrackCount"].ShouldBe(backtracks);
        finding.Evidence.Single().Quote.ShouldBe("Ahmet Yılmaz");
    }

    /// <summary>One heading directly over another happens in honest CVs — a skills section that
    /// opens with a "Languages" subheading — and is not a scramble on its own.</summary>
    [Fact]
    public void A_Single_Heading_Over_A_Subheading_Should_Not_Be_Called_Scrambled()
    {
        var subheading = ReadableText.Replace("YETENEKLER\n", "YETENEKLER\nDiller\nİngilizce (C1)\n");

        Find(ReadableCv(text: subheading), CvScanFindingCode.ReadingOrderScrambled).ShouldBeNull();
    }

    /// <summary>A heading is a line that is only a heading. A sentence that mentions one —
    /// "Deneyim" in a summary — is content, and content between two headings is exactly what an
    /// intact section looks like.</summary>
    [Fact]
    public void A_Sentence_Mentioning_A_Heading_Should_Not_Count_As_One()
    {
        var text = ReadableText.Replace("DENEYİM\n",
            "ÖZET\nDeneyim ve eğitim alanında on yıl.\nDENEYİM\n");

        Find(ReadableCv(text: text), CvScanFindingCode.ReadingOrderScrambled).ShouldBeNull();
    }

    /// <summary>A section can continue over a page break. A heading that closes one page and the
    /// heading that opens the next are two pages apart, not two headings in a row.</summary>
    [Fact]
    public void Headings_On_Either_Side_Of_A_Page_Break_Should_Not_Be_Stacked()
    {
        var cv = ReadableCv(pageCount: 2, pages:
        [
            new ExtractedCvPage(1, 595, 842, ReadableText + "\nPROJELER", []),
            new ExtractedCvPage(2, 595, 842, "REFERANSLAR\nİstek üzerine paylaşılır.", [])
        ]);

        Find(cv, CvScanFindingCode.ReadingOrderScrambled).ShouldBeNull();
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

    /// <summary>The date shapes real CVs use, every one of them a range a parser can order. The
    /// "to" forms are how most English CV builders write it; a month-named end is how most people
    /// do.</summary>
    [Theory]
    [InlineData("Aug 2007 to Current", "Mar 2005 to Jul 2007")]
    [InlineData("December 2014 to May 2015", "January 2010 until March 2014")]
    [InlineData("Mart 2019 – Mayıs 2021", "Ocak 2017 - Şubat 2019")]
    [InlineData("2019-03 / 2021-05", "2016-09 / 2019-02")]
    [InlineData("06/2012 to Date", "2008 through 2011")]
    [InlineData("08/2012 \uFF0D Current", "01/2009 \u2212 08/2012")]
    public void Ordinary_Date_Ranges_Should_Count_As_A_Timeline(string first, string second)
    {
        var text = ReadableText
            .Replace("01/2021 - halen", first)
            .Replace("06/2018 - 12/2020", second)
            .Replace("2014 - 2018", "2018");

        Find(ReadableCv(text: text), CvScanFindingCode.SectionsOrDatesUnreadable).ShouldBeNull();
    }

    /// <summary>The headings real Turkish templates use, plurals included. A Canva template titled
    /// "Deneyimler" and "Eğitim Geçmişi" was being told it had no experience section.</summary>
    [Fact]
    public void Plural_And_Template_Turkish_Headings_Should_Be_Recognised()
    {
        var text = ReadableText
            .Replace("DENEYİM", "Deneyimler:")
            .Replace("EĞİTİM", "Eğitim Geçmişi")
            .Replace("YETENEKLER", "Uzmanlık Alanları");

        Find(ReadableCv(text: text), CvScanFindingCode.SectionsOrDatesUnreadable).ShouldBeNull();
    }

    /// <summary>Formats nobody has shown real parsers read — two-digit closing years, a month glued
    /// to its year with a hyphen — are not credited as ranges. Being more lenient than the systems
    /// this scan stands in for is the failure it exists to avoid.</summary>
    [Theory]
    [InlineData("2015-16", "2012-14")]
    [InlineData("Jul-2015 - Mar-2016", "Dec-2014 - Feb-2015")]
    public void Unproven_Date_Shapes_Should_Not_Count_As_A_Timeline(string first, string second)
    {
        var text = ReadableText
            .Replace("01/2021 - halen", first)
            .Replace("06/2018 - 12/2020", second)
            .Replace("2014 - 2018", "2018");

        Find(ReadableCv(text: text), CvScanFindingCode.SectionsOrDatesUnreadable)
            .ShouldNotBeNull().Metrics["dateRangeCount"].ShouldBe(0);
    }

    /// <summary>The headings of CVs written for the Indian and Gulf markets, the most common ones
    /// in a real-world set of five thousand files. "Educational" is not the word "education".</summary>
    [Fact]
    public void Educational_Qualification_And_Computer_Proficiency_Should_Be_Recognised()
    {
        var text = ReadableText
            .Replace("EĞİTİM", "EDUCATIONAL QUALIFICATION")
            .Replace("YETENEKLER", "COMPUTER PROFICIENCY");

        Find(ReadableCv(text: text), CvScanFindingCode.SectionsOrDatesUnreadable).ShouldBeNull();
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
    /// A PDF has no header part. The contact line at the very top of the page is ordinary text a
    /// parser reads first — and where most CVs put it — so it must not be charged as furniture the
    /// way a Word header is.
    /// </summary>
    [Fact]
    public void Contact_Details_At_The_Top_Of_A_Pdf_Page_Should_Not_Be_Reported()
    {
        var words = SingleColumnWords();
        words.Add(new ExtractedCvWord("ahmet.yilmaz@example.com", 50, 250, 790, 800));
        words.Add(new ExtractedCvWord("+90", 260, 290, 790, 800));

        var text = ReadableText.Replace("ahmet.yilmaz@example.com | +90 532 123 45 67 | İstanbul", "İstanbul")
                   + "\nahmet.yilmaz@example.com +90 532 123 45 67";

        var cv = ReadableCv(text: text, pages: [new ExtractedCvPage(1, 600, 800, text, words)]);

        Find(cv, CvScanFindingCode.ContactUnreadable).ShouldBeNull();
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

    /// <summary>Graded, not a cliff: a spare CV costs a little, a file with a few dozen words costs
    /// the full amount, and 100 words costs nothing.</summary>
    [Theory]
    [InlineData(40, 12)]
    [InlineData(59, 12)]
    [InlineData(60, 6)]
    [InlineData(90, 6)]
    [InlineData(100, 0)]
    public void A_Cv_With_Too_Little_Text_Should_Cost_By_How_Little(int wordCount, int expectedCost)
    {
        var finding = Find(ReadableCv(wordCount: wordCount), CvScanFindingCode.LengthOutOfRange);

        (finding?.PotentialCost ?? 0).ShouldBe(expectedCost);
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

    /// <summary>A LaTeX CV: TeX names every cut of Computer Modern separately (roman, bold, italic,
    /// small caps, each with its design size). One typeface, and the PostScript "MT" tails on
    /// Arial are the same story.</summary>
    [Fact]
    public void Tex_Cuts_And_PostScript_Tails_Should_Count_As_One_Typeface_Each()
    {
        var fonts = new List<ExtractedCvFont>
        {
            new("BBCNPR+CMR10", 10, 2216, 1, "Education"),
            new("ZYKCFI+CMBX10", 10, 199, 1, "Experience"),
            new("GTQUPU+CMTI10", 10, 240, 1, "Stanford"),
            new("ZTRATB+CMCSC10", 12, 44, 1, "Projects"),
            new("ArialMT", 10, 300, 1, "Contact"),
            new("Arial-BoldMT", 10, 100, 1, "Skills")
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

    /// <summary>A body column running to x = 380 and a sidebar starting at x = 401 on a 600-wide
    /// page: a 21-point gutter, the kind a design tool leaves.</summary>
    private static List<ExtractedCvWord> TightSidebarWords()
    {
        var words = new List<ExtractedCvWord>();

        for (var line = 0; line < 10; line++)
        {
            var baseline = 700 - line * 20;

            for (var index = 0; index < 5; index++)
            {
                var left = 50 + index * 66;
                words.Add(new ExtractedCvWord($"govde{line}{index}", left, left + 62, baseline, baseline + 10));
            }

            words.Add(new ExtractedCvWord($"govde{line}5", 360, 380, baseline, baseline + 10));

            for (var index = 0; index < 3; index++)
            {
                var left = 401 + index * 60;
                words.Add(new ExtractedCvWord($"yan{line}{index}", left, left + 55, baseline, baseline + 10));
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
