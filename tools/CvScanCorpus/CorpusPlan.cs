namespace CvScanCorpus;

/// <summary>
/// What a case is expected to produce. These labels are the corpus's claim about how a real
/// applicant tracking system treats the file — not a copy of what the checks happen to do today —
/// so a disagreement is a question about the checks first and about the label second.
/// </summary>
/// <param name="Must">Findings the scan has to report.</param>
/// <param name="May">Findings that are defensible either way; neither reported nor missing counts
/// against the scan.</param>
/// <param name="Bands">Acceptable score bands (good ≥ 80, fair ≥ 55, poor below — the page's own
/// thresholds).</param>
internal sealed record CaseLabel(string[] Must, string[] May, string[] Bands, string Why);

internal sealed record CorpusCase(
    int Id,
    string Group,
    string Format,
    string Lang,
    bool Holdout,
    CaseLabel Label,
    HtmlOptions? Html,
    DocxVariant? Docx,
    bool ImageOnly,
    bool Minimal,
    int Jobs,
    int Bullets,
    int PersonaSeed)
{
    public string FileName => $"{Id:000}-{Group}.{Format}";
}

internal static class CorpusPlan
{
    private const string NoText = "NoTextLayer";
    private const string Turkish = "BrokenTurkishCharacters";
    private const string Columns = "MultiColumnOrTableLayout";
    private const string Order = "ReadingOrderScrambled";
    private const string Sections = "SectionsOrDatesUnreadable";
    private const string Contact = "ContactUnreadable";
    private const string Length = "LengthOutOfRange";
    private const string Fonts = "InconsistentFormatting";

    private static readonly string[] Good = ["good"];
    private static readonly string[] GoodOrFair = ["good", "fair"];
    private static readonly string[] FairOrPoor = ["fair", "poor"];
    private static readonly string[] Poor = ["poor"];

    private static readonly string[] CleanFonts = ["Helvetica", "Arial", "Georgia", "Avenir Next", "Times New Roman", "Verdana"];

    private static readonly DateStyle[] DateStyles =
        [DateStyle.Slash, DateStyle.Years, DateStyle.MonthName, DateStyle.ShortMonth, DateStyle.Iso];

    public static IReadOnlyList<CorpusCase> Build()
    {
        var cases = new List<CorpusCase>();

        void Add(string group, CaseLabel label, HtmlOptions? html = null, DocxVariant? docx = null,
            bool imageOnly = false, bool minimal = false, int jobs = 2, int bullets = 5, string? lang = null)
        {
            var id = cases.Count + 1;
            cases.Add(new CorpusCase(id, group, docx is null ? "pdf" : "docx", lang ?? (id % 2 == 0 ? "tr" : "en"),
                Holdout: id % 5 == 0, label, html is null ? null : html with { Seed = id * 7919 }, docx, imageOnly,
                minimal, jobs, bullets, PersonaSeed: id * 104729));
        }

        for (var i = 0; i < 22; i++)
        {
            Add("clean", new([], [], Good, "One column, standard headings, plain text."),
                new HtmlOptions { Font = CleanFonts[i % CleanFonts.Length], Dates = DateStyles[i % DateStyles.Length] },
                jobs: i % 4 == 3 ? 4 : 2 + i % 2);
        }

        for (var i = 0; i < 4; i++)
        {
            Add("banner", new([], [Contact], Good,
                    "Contact line inside a coloured band at the top of a PDF. A PDF has no header part; parsers read it."),
                new HtmlOptions { Layout = Layout.Banner, Dates = DateStyles[i % DateStyles.Length] });
        }

        double[] mainFirstGutters = [4, 9, 16, 30];
        foreach (var gutter in mainFirstGutters)
        {
            foreach (var left in new[] { false, true })
            {
                Add("sidebar-mainfirst", new([Columns], [], GoodOrFair,
                        "Two columns, body first in the file. Readable in order, but a column layout all the same."),
                    new HtmlOptions { Layout = Layout.Sidebar, GutterPt = gutter, SidebarLeft = left });
            }
        }

        double[] sideFirstGutters = [6, 12, 24];
        foreach (var gutter in sideFirstGutters)
        {
            foreach (var left in new[] { false, true })
            {
                Add("sidebar-sidefirst", new([Columns], [], GoodOrFair,
                        "Two columns, sidebar first in the file. Each heading still arrives with its own content."),
                    new HtmlOptions { Layout = Layout.Sidebar, GutterPt = gutter, SidebarLeft = left, SidebarFirstInDom = true });
            }
        }

        double[] canvaGutters = [6, 12, 20, 30];
        foreach (var gutter in canvaGutters)
        {
            foreach (var left in new[] { false, true })
            {
                Add("canva-headings", new([Columns, Order], [], FairOrPoor,
                        "Design-tool export: every label is its own layer, so the headings arrive together and apart from their content."),
                    new HtmlOptions { Layout = Layout.Sidebar, GutterPt = gutter, SidebarLeft = left, Scramble = Scramble.HeadingsFirst });
            }
        }

        for (var i = 0; i < 5; i++)
        {
            Add("canva-shuffle", new([Columns, Order], [], FairOrPoor,
                    "Design-tool export with boxes added in no particular order: the content stream is a shuffle."),
                new HtmlOptions { Layout = Layout.Sidebar, GutterPt = 12 + i * 4, SidebarLeft = i % 2 == 0, Scramble = Scramble.Shuffle });
        }

        for (var i = 0; i < 4; i++)
        {
            // Good or fair since the outside-tool calibration: a layout-aware parser re-sorts one
            // column by position and recovers it (OwlApply scored this exact file 85).
            Add("canva-single", new([Order], [], GoodOrFair,
                    "One column on the page, but headings drawn as separate layers before the text."),
                new HtmlOptions { Scramble = Scramble.HeadingsFirst, Font = CleanFonts[i] });
        }

        for (var i = 0; i < 4; i++)
        {
            Add("image-only", new([NoText], [Sections, Contact], Poor,
                    "The whole CV is a picture. Nothing reaches the parser — no sections, no contact details either."),
                new HtmlOptions { Dates = DateStyles[i] }, imageOnly: true);
        }

        for (var i = 0; i < 4; i++)
        {
            Add("contact-image", new([Contact], [], GoodOrFair, "E-mail and phone drawn as an image."),
                new HtmlOptions { ContactAsImage = true });
        }

        for (var i = 0; i < 4; i++)
        {
            Add("flat-turkish", new([Turkish], [], GoodOrFair, "Turkish CV whose letters were flattened to ASCII on export."),
                new HtmlOptions { FlattenTurkish = true, Dates = DateStyles[i] }, lang: "tr");
        }

        for (var i = 0; i < 5; i++)
        {
            Add("creative-headings", new([Sections], [], GoodOrFair, "Section names no parser indexes by."),
                new HtmlOptions { CreativeHeadings = true, Dates = DateStyles[i] });
        }

        for (var i = 0; i < 3; i++)
        {
            Add("no-dates", new([Sections], [], GoodOrFair, "Durations instead of date ranges: no timeline to build."),
                new HtmlOptions { Dates = DateStyle.Duration });
        }

        for (var i = 0; i < 3; i++)
        {
            Add("long", new([Length], [], GoodOrFair, "Four pages or more."), new HtmlOptions(), jobs: 10, bullets: 10);
        }

        for (var i = 0; i < 2; i++)
        {
            Add("long-3", new([], [Length], GoodOrFair, "About three pages: a preference, not a failure."),
                new HtmlOptions(), jobs: 7, bullets: 8);
        }

        for (var i = 0; i < 3; i++)
        {
            Add("short", new([Length], [Sections], GoodOrFair,
                    "Far too little text to parse into a profile. It has no skills section either, so that finding is fair."),
                new HtmlOptions(), minimal: true, jobs: 1, bullets: 1);
        }

        for (var i = 0; i < 3; i++)
        {
            Add("many-fonts", new([Fonts], [], GoodOrFair, "A different typeface in every section."),
                new HtmlOptions { ManyFonts = true });
        }

        for (var i = 0; i < 6; i++)
        {
            Add("docx-clean", new([], [], Good, "Word file, plain paragraphs."), docx: DocxVariant.Clean, jobs: 2 + i % 2);
        }

        for (var i = 0; i < 3; i++)
        {
            Add("docx-table", new([Columns], [], GoodOrFair, "Word file laid out as a two-column table."),
                docx: DocxVariant.TableLayout);
        }

        for (var i = 0; i < 3; i++)
        {
            Add("docx-header", new([Contact], [], GoodOrFair, "Contact details only in the Word header part."),
                docx: DocxVariant.ContactInHeader);
        }

        return cases;
    }
}
