using System.Globalization;
using System.Text;
using AfterApply.Domain.Documents;

namespace AfterApply.Application.CvScan;

/// <summary>
/// The seven checks the score is made of. All of them are pure functions over
/// <see cref="ExtractedCv"/>: no I/O, no clock, no model, no configuration. That is what lets the
/// unit tests state a case as a handful of words with coordinates rather than as a fixture file,
/// and it is what makes the score reproducible — the same CV scores the same number tomorrow.
///
/// Every candidate carries evidence a reader can go and look at, because a scan that says
/// "your CV is hard to read" without pointing at anything is indistinguishable from a guess.
/// </summary>
public static class CvScanChecks
{
    /// <summary>Below this, there is no text layer worth the name — a scanned page, a photo, or
    /// text drawn as vector outlines. A real one-page CV runs to several hundred words.</summary>
    private const int MinimumWords = 25;

    /// <summary>A page under this in a document that otherwise has text: one scanned page in a
    /// digital file, or a page that is entirely an image.</summary>
    private const int MinimumWordsPerPage = 5;

    /// <summary>Shorter than this and the machine has almost nothing to work with, whatever the
    /// page count says.</summary>
    private const int MinimumCvWords = 150;

    /// <summary>Words per page when the format will not say (a .docx has no pagination until
    /// something renders it). Conservative on purpose: it should not invent a page.</summary>
    private const int EstimatedWordsPerPage = 450;

    /// <summary>How much of a page's height counts as its header and its footer. PDF has no header
    /// concept at all — only ink near an edge — and this is the band inside which a parser is
    /// likely to skip what it finds.</summary>
    private const double HeaderFooterBand = 0.08;

    private const int QuoteLength = 120;

    /// <summary>U+FFFD — what a decoder writes when it gave up on a byte sequence.</summary>
    private const char ReplacementCharacter = '\uFFFD';

    public static IReadOnlyList<CvScanFindingCandidate> Run(ExtractedCv cv)
    {
        var geometry = CvPageGeometry.From(cv);

        return new[]
            {
                TextLayer(cv),
                TurkishCharacters(cv),
                ColumnsAndTables(cv, geometry),
                SectionsAndDates(cv),
                Contact(cv, geometry),
                Length(cv),
                Formatting(cv)
            }
            .OfType<CvScanFindingCandidate>()
            .ToList();
    }

    /// <summary>
    /// Is there text at all. This one check can cost the whole machine-readability category,
    /// and should: when the answer is no, every other check downstream is reading an empty
    /// document, and reporting "no sections found" about a scanned page would be six ways of
    /// saying the same thing.
    /// </summary>
    private static CvScanFindingCandidate? TextLayer(ExtractedCv cv)
    {
        var metrics = new Dictionary<string, double>
        {
            ["wordCount"] = cv.WordCount,
            ["pageCount"] = cv.PageCount ?? cv.Pages.Count
        };

        if (cv.WordCount < MinimumWords)
        {
            var pages = cv.Pages.Count > 0
                ? cv.Pages.Select(page => new CvScanEvidence(page.Number, null)).ToList()
                : [new CvScanEvidence(1, null)];

            return new CvScanFindingCandidate(CvScanFindingCode.NoTextLayer,
                CvScanCategory.MachineReadability, CvScanScoring.Weights[CvScanCategory.MachineReadability],
                pages, metrics);
        }

        // A digital document with one scanned page in it — someone signed a page, printed it and
        // photographed it back in. The rest of the CV is readable, so this costs a fraction.
        var emptyPages = cv.Pages
            .Where(page => page.Words.Count < MinimumWordsPerPage && page.Text.Trim().Length < 40)
            .ToList();

        if (emptyPages.Count == 0 || emptyPages.Count == cv.Pages.Count)
        {
            return null;
        }

        metrics["emptyPageCount"] = emptyPages.Count;

        return new CvScanFindingCandidate(CvScanFindingCode.NoTextLayer,
            CvScanCategory.MachineReadability, 15,
            emptyPages.Select(page => new CvScanEvidence(page.Number, null)).ToList(), metrics);
    }

    /// <summary>
    /// Do Turkish letters survive extraction. Two separate failures wear the same face to a
    /// reader: a font with no usable ToUnicode map (the machine sees ink it cannot name, and
    /// PdfPig writes "(cid:213)"), and a font that quietly substitutes ASCII, which turns
    /// "Yazılım Mühendisi" into "Yazilim Muhendisi" — searchable for nothing anyone typed.
    /// </summary>
    private static CvScanFindingCandidate? TurkishCharacters(ExtractedCv cv)
    {
        if (cv.WordCount < MinimumWords)
        {
            return null;
        }

        var unmapped = CvScanVocabulary.UnmappedGlyph.Matches(cv.Text).Count;
        // U+FFFD by code point rather than as a literal: the character is a black diamond that
        // renders differently in every editor, and CvFileRules already sets the precedent of
        // spelling invisible or ambiguous characters out.
        var replacement = cv.Text.Count(character => character == ReplacementCharacter);
        var broken = unmapped + replacement;

        if (broken > 0)
        {
            var ratio = (double)broken / Math.Max(cv.Text.Length, 1);
            var evidence = FindEvidence(cv, line =>
                CvScanVocabulary.UnmappedGlyph.IsMatch(line) || line.Contains(ReplacementCharacter));

            return new CvScanFindingCandidate(CvScanFindingCode.BrokenTurkishCharacters,
                CvScanCategory.MachineReadability, ratio > 0.01 ? 12 : 6, evidence,
                new Dictionary<string, double>
                {
                    ["unreadableCharacterCount"] = broken,
                    ["unreadableShare"] = Math.Round(ratio * 100, 2)
                });
        }

        // No broken glyphs, so the second question: is this a Turkish CV whose Turkish letters are
        // simply not there? Asked only of documents long enough for the answer to mean something.
        if (cv.Text.Length < 400 || cv.Text.Any(IsTurkishSpecificLetter))
        {
            return null;
        }

        var folded = CvScanVocabulary.Fold(cv.Text);
        var markers = CvScanVocabulary.TurkishMarkers.Count(marker => folded.Contains(marker, StringComparison.Ordinal));

        if (markers < 3)
        {
            return null;
        }

        return new CvScanFindingCandidate(CvScanFindingCode.BrokenTurkishCharacters,
            CvScanCategory.MachineReadability, 8,
            FindEvidence(cv, line => CvScanVocabulary.TurkishMarkers
                .Any(marker => CvScanVocabulary.Fold(line).Contains(marker.Trim(), StringComparison.Ordinal))),
            new Dictionary<string, double> { ["turkishMarkerCount"] = markers, ["turkishLetterCount"] = 0 });
    }

    private static bool IsTurkishSpecificLetter(char character) =>
        character is 'ı' or 'İ' or 'ş' or 'Ş' or 'ğ' or 'Ğ' or 'ç' or 'Ç' or 'ö' or 'Ö' or 'ü' or 'Ü';

    /// <summary>
    /// Two columns, or a table doing a column's job. Both are a layout decision that reads
    /// beautifully to a person and arrives at a parser as interleaved fragments — the sidebar's
    /// first line, then the body's first line, then the sidebar's second.
    ///
    /// For .docx the format says so itself (a table is an element). For PDF there is nothing to
    /// ask, so the answer comes from where the words sit: a vertical band down the middle of the
    /// page that no word crosses, with enough text on both sides of it to be two columns rather
    /// than a centred heading.
    /// </summary>
    private static CvScanFindingCandidate? ColumnsAndTables(ExtractedCv cv, CvPageGeometry geometry)
    {
        if (cv.WordCount < MinimumWords)
        {
            return null;
        }

        if (cv.TableCount is > 0)
        {
            return new CvScanFindingCandidate(CvScanFindingCode.MultiColumnOrTableLayout,
                CvScanCategory.MachineReadability, 10,
                FirstLineEvidence(cv),
                new Dictionary<string, double> { ["tableCount"] = cv.TableCount.Value });
        }

        var column = geometry.Columns.FirstOrDefault();
        if (column is null)
        {
            return null;
        }

        var page = cv.Pages.First(candidate => candidate.Number == column.Page);

        return new CvScanFindingCandidate(CvScanFindingCode.MultiColumnOrTableLayout,
            CvScanCategory.MachineReadability, 14,
            // The quote is the page's first line exactly as the machine reads it — which, on a
            // two-column page, is usually the two columns already braided together. Nothing
            // explains the finding as well as the reader's own scrambled sentence.
            [new CvScanEvidence(page.Number, Quote(FirstNonEmptyLine(page.Text)))],
            new Dictionary<string, double>
            {
                ["columnCount"] = 2,
                ["gutterPositionPercent"] = Math.Round(column.GutterCenterRatio * 100, 1),
                ["affectedPageCount"] = geometry.Columns.Count
            });
    }

    /// <summary>
    /// Can a parser find the shape of a career: the section headings it indexes by, and dates it
    /// can turn into a timeline. Years alone are not dates — "2019" beside a certificate name is
    /// not a span anyone can order — so a CV with no readable range is missing the thing every
    /// tracker sorts by.
    /// </summary>
    private static CvScanFindingCandidate? SectionsAndDates(ExtractedCv cv)
    {
        if (cv.WordCount < MinimumWords)
        {
            return null;
        }

        var found = new List<(CvSection Section, int Page, string Line)>();

        foreach (var page in cv.Pages)
        {
            foreach (var line in SplitLines(page.Text))
            {
                var folded = CvScanVocabulary.Fold(line);
                foreach (var (section, headings) in CvScanVocabulary.SectionHeadings)
                {
                    if (found.Any(entry => entry.Section == section))
                    {
                        continue;
                    }

                    if (headings.Any(heading => CvScanVocabulary.ContainsPhrase(folded, heading)))
                    {
                        found.Add((section, page.Number, line));
                    }
                }
            }
        }

        var missing = new Dictionary<CvSection, int>
        {
            [CvSection.Experience] = 10,
            [CvSection.Education] = 8,
            [CvSection.Skills] = 4
        };

        var cost = missing
            .Where(entry => found.All(candidate => candidate.Section != entry.Key))
            .Sum(entry => entry.Value);

        var ranges = CvScanVocabulary.DateRange.Matches(cv.Text).Count;
        var years = CvScanVocabulary.Year.Matches(cv.Text).Count;
        var months = CvScanVocabulary.MonthName.Matches(CvScanVocabulary.Fold(cv.Text)).Count;

        var dateCost = ranges switch
        {
            0 when years < 2 && months < 2 => 10,
            0 => 6,
            _ => 0
        };

        cost += dateCost;

        if (cost == 0)
        {
            return null;
        }

        var evidence = found.Count > 0
            // What was found, not what was missed: it shows the reader which headings the machine
            // did recognise, which is the only checkable half of "we could not find the others".
            ? found.Select(entry => new CvScanEvidence(entry.Page, Quote(entry.Line))).ToList()
            : FirstLineEvidence(cv);

        return new CvScanFindingCandidate(CvScanFindingCode.SectionsOrDatesUnreadable,
            CvScanCategory.SectionsAndDates, Math.Min(cost, CvScanScoring.Weights[CvScanCategory.SectionsAndDates]),
            evidence,
            new Dictionary<string, double>
            {
                ["foundSectionCount"] = found.Count,
                ["missingExperience"] = found.Any(entry => entry.Section == CvSection.Experience) ? 0 : 1,
                ["missingEducation"] = found.Any(entry => entry.Section == CvSection.Education) ? 0 : 1,
                ["missingSkills"] = found.Any(entry => entry.Section == CvSection.Skills) ? 0 : 1,
                ["dateRangeCount"] = ranges,
                ["yearCount"] = years
            });
    }

    /// <summary>
    /// Can the machine reach you. Two failures: contact details it cannot read at all, and
    /// contact details that exist only in a header or footer — a real place in a Word file and a
    /// band of ink near the paper's edge in a PDF, and in both cases somewhere a parser routinely
    /// skips as furniture.
    /// </summary>
    private static CvScanFindingCandidate? Contact(ExtractedCv cv, CvPageGeometry geometry)
    {
        if (cv.WordCount < MinimumWords)
        {
            return null;
        }

        // A .docx keeps its header and footer in their own parts, so they are not in the document
        // text at all; a PDF has them inline, and the band they sit in is the only thing that says
        // so. Hence two strings rather than one: everything, and everything outside the bands.
        var bandText = string.Join('\n', new[] { cv.HeaderFooterText, geometry.HeaderFooterText }
            .Where(text => !string.IsNullOrWhiteSpace(text)));
        var fullText = string.Join('\n', new[] { cv.Text, bandText }
            .Where(text => !string.IsNullOrWhiteSpace(text)));
        var bodyText = geometry.BodyText ?? cv.Text;

        var emailFound = CvScanVocabulary.Email.IsMatch(fullText);
        var phoneFound = CvScanVocabulary.Phone.IsMatch(fullText);

        var emailInBand = emailFound && !CvScanVocabulary.Email.IsMatch(bodyText);
        var phoneInBand = phoneFound && !CvScanVocabulary.Phone.IsMatch(bodyText);

        var cost = 0;
        if (!emailFound)
        {
            cost += 10;
        }

        if (!phoneFound)
        {
            cost += 5;
        }

        if (emailInBand || phoneInBand)
        {
            cost += 7;
        }

        if (cost == 0)
        {
            return null;
        }

        var evidence = emailFound || phoneFound
            ? FindEvidence(cv, line => CvScanVocabulary.Email.IsMatch(line) || CvScanVocabulary.Phone.IsMatch(line))
            : FirstLineEvidence(cv);

        return new CvScanFindingCandidate(CvScanFindingCode.ContactUnreadable,
            CvScanCategory.Contact, Math.Min(cost, CvScanScoring.Weights[CvScanCategory.Contact]), evidence,
            new Dictionary<string, double>
            {
                ["emailFound"] = emailFound ? 1 : 0,
                ["phoneFound"] = phoneFound ? 1 : 0,
                ["onlyInHeaderFooter"] = emailInBand || phoneInBand ? 1 : 0
            });
    }

    /// <summary>
    /// Length. Two pages is the shape a recruiter reads and a parser handles; the cost rises with
    /// the page count rather than jumping at a threshold, because a three-page CV is a preference
    /// and a nine-page one is a different document. A CV far too short is scored as well —
    /// it usually means the file carries a design and not much text.
    /// </summary>
    private static CvScanFindingCandidate? Length(ExtractedCv cv)
    {
        var pages = cv.PageCount ?? (int)Math.Ceiling((double)cv.WordCount / EstimatedWordsPerPage);
        var metrics = new Dictionary<string, double>
        {
            ["pageCount"] = pages,
            ["wordCount"] = cv.WordCount,
            ["pageCountEstimated"] = cv.PageCount is null ? 1 : 0
        };

        if (cv.WordCount is >= MinimumWords and < MinimumCvWords)
        {
            return new CvScanFindingCandidate(CvScanFindingCode.LengthOutOfRange,
                CvScanCategory.FormatAndLength, 12, LastLineEvidence(cv), metrics);
        }

        var cost = pages switch
        {
            <= 2 => 0,
            3 => 4,
            4 or 5 => 8,
            _ => 12
        };

        return cost == 0
            ? null
            : new CvScanFindingCandidate(CvScanFindingCode.LengthOutOfRange, CvScanCategory.FormatAndLength,
                cost, LastLineEvidence(cv), metrics);
    }

    /// <summary>
    /// Typographic consistency. Not a taste judgement — every extra typeface is another font
    /// program that has to carry a usable character map, and a document set in seven of them is
    /// one bad embed away from the broken-letters finding above.
    /// </summary>
    private static CvScanFindingCandidate? Formatting(ExtractedCv cv)
    {
        // A face used for a handful of glyphs is a bullet, a ligature or an icon, not a decision.
        const int MeaningfulGlyphCount = 30;

        var used = cv.Fonts.Where(font => font.GlyphCount >= MeaningfulGlyphCount).ToList();
        if (used.Count == 0)
        {
            return null;
        }

        var families = used
            .GroupBy(font => NormalizeFontFamily(font.Name), StringComparer.Ordinal)
            .ToList();

        var sizes = used
            .Select(font => Math.Round(font.Size, 1))
            .Distinct()
            .Count();

        var familyCost = families.Count > 3 ? Math.Min((families.Count - 3) * 3, 6) : 0;
        var sizeCost = sizes > 5 ? 2 : 0;
        var cost = Math.Min(familyCost + sizeCost, 8);

        if (cost == 0)
        {
            return null;
        }

        // Point at the rarest family: it is the one the reader is least likely to know is in
        // their file, which usually means it arrived with something they pasted in.
        var rarest = families.OrderBy(family => family.Sum(font => font.GlyphCount)).First().First();

        return new CvScanFindingCandidate(CvScanFindingCode.InconsistentFormatting,
            CvScanCategory.FormatAndLength, cost,
            [new CvScanEvidence(rarest.Page, Quote(rarest.SampleText))],
            new Dictionary<string, double>
            {
                ["fontFamilyCount"] = families.Count,
                ["fontSizeCount"] = sizes
            });
    }

    /// <summary>
    /// Strips what a PDF producer adds to a font name and a word processor does not: the six-letter
    /// subset prefix ("ABCDEE+Calibri") and the style suffix ("Calibri-Bold", "Calibri,BoldItalic").
    /// Without this, one typeface in four weights reads as four typefaces.
    /// </summary>
    private static string NormalizeFontFamily(string name)
    {
        var value = name;
        var plus = value.IndexOf('+');
        if (plus == 6)
        {
            value = value[(plus + 1)..];
        }

        var cut = value.IndexOfAny([',', '-']);
        if (cut > 0)
        {
            value = value[..cut];
        }

        return value.Trim().ToLowerInvariant();
    }

    private static IReadOnlyList<CvScanEvidence> FindEvidence(ExtractedCv cv, Func<string, bool> predicate)
    {
        foreach (var page in cv.Pages)
        {
            foreach (var line in SplitLines(page.Text))
            {
                if (predicate(line))
                {
                    return [new CvScanEvidence(page.Number, Quote(line))];
                }
            }
        }

        return FirstLineEvidence(cv);
    }

    private static IReadOnlyList<CvScanEvidence> FirstLineEvidence(ExtractedCv cv)
    {
        var page = cv.Pages.FirstOrDefault();
        return page is null
            ? [new CvScanEvidence(1, null)]
            : [new CvScanEvidence(page.Number, Quote(FirstNonEmptyLine(page.Text)))];
    }

    private static IReadOnlyList<CvScanEvidence> LastLineEvidence(ExtractedCv cv)
    {
        var page = cv.Pages.LastOrDefault();
        if (page is null)
        {
            return [new CvScanEvidence(1, null)];
        }

        var line = SplitLines(page.Text).LastOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
        return [new CvScanEvidence(page.Number, Quote(line ?? string.Empty))];
    }

    private static string FirstNonEmptyLine(string text) =>
        SplitLines(text).FirstOrDefault(line => !string.IsNullOrWhiteSpace(line)) ?? string.Empty;

    internal static IEnumerable<string> SplitLines(string text) =>
        text.Split('\n', StringSplitOptions.TrimEntries);

    /// <summary>
    /// One short, single-line excerpt. Collapsed to spaces and cut to length because this string is
    /// rendered in a browser and read in a terminal, and because a CV's own text is untrusted
    /// input: it is data on its way back to the person who just sent it, nothing more.
    /// </summary>
    internal static string Quote(string line)
    {
        var builder = new StringBuilder(Math.Min(line.Length, QuoteLength) + 1);
        foreach (var character in line)
        {
            var next = char.IsControl(character) || char.IsWhiteSpace(character) ? ' ' : character;
            if (next == ' ' && (builder.Length == 0 || builder[^1] == ' '))
            {
                continue;
            }

            builder.Append(next);
            if (builder.Length >= QuoteLength)
            {
                builder.Append('…');
                break;
            }
        }

        return builder.ToString().Trim();
    }
}

/// <summary>
/// What can only be known from where the words sit: the two-column pages, and the text that lives
/// in the top and bottom bands of a page. Both are PDF questions — a .docx answers them itself
/// (see <see cref="ExtractedCv.TableCount"/> and <see cref="ExtractedCv.HeaderFooterText"/>) — so
/// this is empty for a document with no geometry, and the checks fall back accordingly.
/// </summary>
/// <param name="BodyText">Everything outside those bands, or null for a format that answers the
/// header question itself. It is what "the e-mail is only in the footer" is decided against.</param>
internal sealed record CvPageGeometry(IReadOnlyList<CvColumnLayout> Columns, string HeaderFooterText,
    string? BodyText)
{
    /// <summary>A page with too few words tells you nothing: a cover page has white space down the
    /// middle and is not a two-column layout.</summary>
    private const int MinimumWordsToJudge = 40;

    /// <summary>Words needed on each side before a white band counts as a gutter.</summary>
    private const int MinimumWordsPerColumn = 15;

    /// <summary>And on that many separate lines — otherwise a centred title with a date on the
    /// right reads as two columns.</summary>
    private const int MinimumLinesPerColumn = 4;

    /// <summary>Narrower than this and it is the space between two words, not between two
    /// columns.</summary>
    private const double MinimumGutterRatio = 0.06;

    private const double BandRatio = 0.08;

    public static CvPageGeometry From(ExtractedCv cv)
    {
        if (cv.Format is not CvFileFormat.Pdf)
        {
            return new CvPageGeometry([], string.Empty, BodyText: null);
        }

        var columns = new List<CvColumnLayout>();
        var band = new StringBuilder();
        var body = new StringBuilder();

        foreach (var page in cv.Pages)
        {
            var words = page.Words.Where(word => !string.IsNullOrWhiteSpace(word.Text)).ToList();
            if (words.Count == 0 || page.Width <= 0 || page.Height <= 0)
            {
                continue;
            }

            AppendBands(band, body, page, words);

            if (words.Count < MinimumWordsToJudge)
            {
                continue;
            }

            if (FindGutter(page, words) is { } gutter)
            {
                columns.Add(gutter);
            }
        }

        // Null rather than empty when no page carried words: "the e-mail is not in the body" is
        // only worth asserting when a body was actually read, and an empty string here would
        // otherwise report every contact detail as header furniture.
        return new CvPageGeometry(columns, band.ToString(),
            body.Length > 0 ? body.ToString() : null);
    }

    private static void AppendBands(StringBuilder band, StringBuilder body, ExtractedCvPage page,
        List<ExtractedCvWord> words)
    {
        var top = page.Height * (1 - BandRatio);
        var bottom = page.Height * BandRatio;

        foreach (var group in words.GroupBy(word => word.Bottom >= top || word.Top <= bottom))
        {
            var line = string.Join(' ', group
                .OrderByDescending(word => word.Bottom)
                .ThenBy(word => word.Left)
                .Select(word => word.Text));

            (group.Key ? band : body).AppendLine(line);
        }
    }

    /// <summary>
    /// Looks for a vertical band down the middle of the page that no word crosses. The page is cut
    /// into hundredths and each word marks the hundredths it covers; the longest unmarked run whose
    /// centre lies in the middle half of the page is the candidate gutter.
    /// </summary>
    private static CvColumnLayout? FindGutter(ExtractedCvPage page, List<ExtractedCvWord> words)
    {
        const int Bins = 100;
        var occupied = new bool[Bins];

        foreach (var word in words)
        {
            var from = (int)Math.Floor(word.Left / page.Width * Bins);
            var to = (int)Math.Ceiling(word.Right / page.Width * Bins);

            for (var bin = Math.Max(from, 0); bin < Math.Min(to, Bins); bin++)
            {
                occupied[bin] = true;
            }
        }

        var bestStart = -1;
        var bestLength = 0;
        var start = -1;

        for (var bin = 0; bin <= Bins; bin++)
        {
            var empty = bin < Bins && !occupied[bin];
            if (empty && start < 0)
            {
                start = bin;
            }
            else if (!empty && start >= 0)
            {
                var length = bin - start;
                var center = (start + bin) / 2.0 / Bins;

                // Only the middle half of the page: the margins are empty by definition and a gap
                // at 12% of the width is an indent.
                if (length > bestLength && center is > 0.25 and < 0.75)
                {
                    bestStart = start;
                    bestLength = length;
                }

                start = -1;
            }
        }

        if (bestStart < 0 || (double)bestLength / Bins < MinimumGutterRatio)
        {
            return null;
        }

        var gutterFrom = (double)bestStart / Bins * page.Width;
        var gutterTo = (double)(bestStart + bestLength) / Bins * page.Width;

        var left = words.Where(word => word.Right <= gutterFrom).ToList();
        var right = words.Where(word => word.Left >= gutterTo).ToList();

        if (left.Count < MinimumWordsPerColumn || right.Count < MinimumWordsPerColumn)
        {
            return null;
        }

        // Distinct baselines, rounded to the nearest point: two columns run down the page, a
        // heading-plus-date pair sits on one line.
        var leftLines = left.Select(word => Math.Round(word.Bottom)).Distinct().Count();
        var rightLines = right.Select(word => Math.Round(word.Bottom)).Distinct().Count();

        if (leftLines < MinimumLinesPerColumn || rightLines < MinimumLinesPerColumn)
        {
            return null;
        }

        return new CvColumnLayout(page.Number, (bestStart + bestLength / 2.0) / Bins);
    }
}

internal sealed record CvColumnLayout(int Page, double GutterCenterRatio);
