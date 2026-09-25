using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AfterApply.Domain.Documents;

namespace AfterApply.Application.CvScan;

/// <summary>
/// The eight checks the score is made of. All of them are pure functions over
/// <see cref="ExtractedCv"/>: no I/O, no clock, no model, no configuration. That is what lets the
/// unit tests state a case as a handful of words with coordinates rather than as a fixture file,
/// and it is what makes the score reproducible — the same CV scores the same number tomorrow.
///
/// Every candidate carries evidence a reader can go and look at, because a scan that says
/// "your CV is hard to read" without pointing at anything is indistinguishable from a guess.
/// </summary>
public static partial class CvScanChecks
{
    /// <summary>Below this, there is no text layer worth the name — a scanned page, a photo, or
    /// text drawn as vector outlines. A real one-page CV runs to several hundred words.</summary>
    private const int MinimumWords = 25;

    /// <summary>A page under this in a document that otherwise has text: one scanned page in a
    /// digital file, or a page that is entirely an image.</summary>
    private const int MinimumWordsPerPage = 5;

    /// <summary>Shorter than this and the machine has almost nothing to work with, whatever the
    /// page count says. It was 150, which charged real, parseable one-page CVs of two roles and a
    /// summary; brevity is a content question, and this check is about there being enough for a
    /// parser to fill a profile with.</summary>
    private const int MinimumCvWords = 100;

    /// <summary>Below this the short CV costs the full amount: a few dozen words is a design with
    /// almost no text in it, not a concise writer.</summary>
    private const int VeryShortCvWords = 60;

    /// <summary>Words per page when the format will not say (a .docx has no pagination until
    /// something renders it). Conservative on purpose: it should not invent a page.</summary>
    private const int EstimatedWordsPerPage = 450;

    private const int QuoteLength = 120;

    /// <summary>Long climbs back up one page before its order counts as scrambled. Measured, not
    /// guessed (DECISIONS.md 2026-09-25): 2,484 real CVs and every one-column and sidebar layout in
    /// the calibration corpus stay at two or fewer; design-tool exports with boxes out of order
    /// start at three.</summary>
    private const int MinimumBacktracks = 3;

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
                ReadingOrder(cv),
                SectionsAndDates(cv),
                Contact(cv),
                Length(cv),
                Formatting(cv)
            }
            .OfType<CvScanFindingCandidate>()
            .ToList();
    }

    /// <summary>
    /// Is there text at all. This one check can cost the whole machine-readability category,
    /// and should. It cannot be the whole story, though: a picture of a CV reaches the recruiter's
    /// system with no sections and no contact details either, and scoring it 60 said "fair" about
    /// a file a parser gets nothing from. So the sections and contact checks charge their
    /// categories too, marked as consequences (metric <c>noText</c>) so the page explains them as
    /// one cause rather than three problems; the remaining checks stay quiet.
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
        // Only a page with a picture on it: a page with neither text nor image is blank — a stray
        // page break at the end of an export — and a parser loses nothing on it.
        var emptyPages = cv.Pages
            .Where(page => page.Words.Count < MinimumWordsPerPage && page.Text.Trim().Length < 40 &&
                           (page.ImageCount > 0 || cv.Format is not CvFileFormat.Pdf))
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
    /// Did the text arrive in the order it was written. The column check above infers this from
    /// where the words sit; this one reads the result itself, which is what a parser actually gets
    /// and what fills (or misfills) the candidate's form.
    ///
    /// The symptom it counts is a heading with no content of its own: the next line the machine
    /// reads is another heading. On a sidebar template that is what the braid looks like —
    /// "LANGUAGES", "CONTACT", "WORK EXPERIENCE" in a row, their contents somewhere else entirely,
    /// and a parser that indexes by heading files every one of those sections empty. One such
    /// heading happens in honest CVs (a "Skills" heading over a "Languages" subheading); two is a
    /// scramble.
    /// </summary>
    private static CvScanFindingCandidate? ReadingOrder(ExtractedCv cv)
    {
        if (cv.WordCount < MinimumWords)
        {
            return null;
        }

        var orphans = 0;
        (int Page, List<string> Headings)? evidence = null;

        foreach (var page in cv.Pages)
        {
            var run = new List<string>();

            // A trailing empty line closes the last run of the page, so a heading stack at the very
            // bottom is counted the same way as one in the middle.
            foreach (var line in SplitLines(page.Text).Where(line => line.Length > 0).Append(string.Empty))
            {
                if (line.Length > 0 && IsHeading(line))
                {
                    // The same heading twice in a row is one heading drawn twice — a text effect —
                    // and says nothing about order.
                    if (run.Count == 0 || CvScanVocabulary.Fold(run[^1]) != CvScanVocabulary.Fold(line))
                    {
                        run.Add(line);
                    }

                    continue;
                }

                // Every heading in a run but the last is followed by another heading. The last one
                // is followed by content — or by the end of the page, where the section may simply
                // continue overleaf, which is a typographic choice and not a parsing failure.
                if (run.Count > 1)
                {
                    orphans += run.Count - 1;
                    evidence ??= (page.Number, [.. run]);
                }

                run.Clear();
            }
        }

        // The second symptom, read from positions rather than from words: the file's own text order
        // climbing back up the page again and again. One climb is a second column; a shuffle of
        // boxes climbs on every other block, whatever its headings are called.
        var scrambledPage = cv.Pages.OrderByDescending(page => page.Backtracks).FirstOrDefault();
        var backtracks = scrambledPage?.Backtracks ?? 0;

        if (orphans < 2 && backtracks < MinimumBacktracks)
        {
            return null;
        }

        // -15, not the whole category: measured against outside tools (DECISIONS.md 2026-09-25).
        // Layout-aware parsers re-sort the words by position and recover most of these files — a
        // two-column Canva export that cost 25 here scored "fair" there — while systems that read
        // the file's own order misfill the form, as the HR complaint that started this showed. The
        // industry is a mix of both; so is this cost.
        return new CvScanFindingCandidate(CvScanFindingCode.ReadingOrderScrambled,
            CvScanCategory.MachineReadability, 15,
            evidence is { } stacked
                // The headings exactly as they arrived, side by side: the reader sees their own
                // section names stacked with nothing between them, which says more than any
                // explanation.
                ? [new CvScanEvidence(stacked.Page, Quote(string.Join(" / ", stacked.Headings)))]
                // Otherwise the page's opening as the machine reads it — on a shuffled page, rarely
                // the line the reader put at the top.
                : [new CvScanEvidence(scrambledPage!.Number, Quote(FirstNonEmptyLine(scrambledPage.Text)))],
            new Dictionary<string, double>
            {
                ["orphanHeadingCount"] = orphans,
                ["backtrackCount"] = backtracks
            });
    }

    /// <summary>
    /// Whether a line is a heading and nothing else: "Deneyim", "WORK EXPERIENCE:", or several
    /// known headings joined ("Education &amp; Certifications", "Skills / Languages") — as opposed to
    /// a sentence that merely mentions one. A line of several headings counts once. An earlier
    /// version counted "CONTACT WORK EXPERIENCE" as two headings braided from two columns; on five
    /// thousand real CVs that rule found only phrases like "EDUCATIONAL QUALIFICATION" and
    /// "EMPLOYMENT OBJECTIVE", and the position-based signal already catches the real braids.
    /// </summary>
    private static bool IsHeading(string line)
    {
        const int MaximumHeadingWords = 3;

        if (line.Length > 60)
        {
            return false;
        }

        var words = CvScanVocabulary.Fold(line).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            return false;
        }

        // splittable[i]: whether the first i words split into known headings of up to three words.
        var splittable = new bool[words.Length + 1];
        splittable[0] = true;

        for (var end = 1; end <= words.Length; end++)
        {
            for (var size = 1; size <= Math.Min(MaximumHeadingWords, end) && !splittable[end]; size++)
            {
                var start = end - size;
                splittable[end] = splittable[start] &&
                                  CvScanVocabulary.AllHeadings.Contains(string.Join(' ', words[start..end]));
            }
        }

        return splittable[^1];
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
            // Nothing was read, so nothing was found — and a parser fills this part of the profile
            // with nothing too. Scored in full, and marked, so the page explains it as a consequence
            // of the missing text rather than as a second problem with its own fix.
            return new CvScanFindingCandidate(CvScanFindingCode.SectionsOrDatesUnreadable,
                CvScanCategory.SectionsAndDates, CvScanScoring.Weights[CvScanCategory.SectionsAndDates],
                [new CvScanEvidence(1, null)], new Dictionary<string, double> { ["noText"] = 1 });
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
    /// contact details that exist only in a Word file's header or footer part — somewhere a parser
    /// routinely skips as furniture.
    ///
    /// Only Word's own parts, not the top and bottom of a PDF page. A PDF has no header; its top
    /// line is ordinary text a parser reads first, and it is where most CVs put the contact line
    /// — every one of ten LaTeX CVs in the calibration set was charged for doing exactly the right
    /// thing (DECISIONS.md 2026-09-25).
    /// </summary>
    private static CvScanFindingCandidate? Contact(ExtractedCv cv)
    {
        if (cv.WordCount < MinimumWords)
        {
            // Same reasoning as the sections check: an unreadable file reaches the recruiter's
            // system with no way to reply to it.
            return new CvScanFindingCandidate(CvScanFindingCode.ContactUnreadable,
                CvScanCategory.Contact, CvScanScoring.Weights[CvScanCategory.Contact],
                [new CvScanEvidence(1, null)], new Dictionary<string, double> { ["noText"] = 1 });
        }

        // A .docx keeps its header and footer in their own parts, so they are not in the document
        // text at all. Hence two strings: everything, and the body alone.
        var fullText = string.Join('\n', new[] { cv.Text, cv.HeaderFooterText }
            .Where(text => !string.IsNullOrWhiteSpace(text)));
        var bodyText = cv.Text;

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
            // Graded rather than a cliff: 99 words and 100 words are the same CV, and a file with
            // a few dozen words is a different problem from one that is merely spare.
            return new CvScanFindingCandidate(CvScanFindingCode.LengthOutOfRange,
                CvScanCategory.FormatAndLength, cv.WordCount < VeryShortCvWords ? 12 : 6,
                LastLineEvidence(cv), metrics);
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
    /// subset prefix ("ABCDEE+Calibri"), the style suffix ("Calibri-Bold", "Calibri,BoldItalic")
    /// and the PostScript "MT"/"PSMT" tail ("ArialMT", "TimesNewRomanPS-BoldMT"). Without this, one
    /// typeface in four weights reads as four typefaces.
    ///
    /// TeX's Computer Modern is the other case: its cuts are separate fonts named by abbreviation
    /// and design size (CMR10 roman, CMBX12 bold, CMTI10 italic, CMCSC10 small caps; SFRM1000 and
    /// friends in the EC encoding), and every LaTeX CV in the calibration set was charged for "four
    /// typefaces" that are one.
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

        value = value.Trim().ToLowerInvariant();

        if (TexComputerModern().IsMatch(value))
        {
            return "computer modern";
        }

        foreach (var tail in (string[])["psmt", "mt", "ps"])
        {
            if (value.Length > tail.Length + 2 && value.EndsWith(tail, StringComparison.Ordinal))
            {
                return value[..^tail.Length];
            }
        }

        return value;
    }

    [GeneratedRegex(@"^(cm|sf)(r|b|bx|bxti|ti|sl|csc|cc|ss|ssbx|ssi|mi|sy|ex|u|tt)\d*$",
        RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TexComputerModern();

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
/// What can only be known from where the words sit: the two-column pages. A PDF question — a .docx
/// answers it itself (see <see cref="ExtractedCv.TableCount"/>) — so this is empty for a document
/// with no geometry.
/// </summary>
internal sealed record CvPageGeometry(IReadOnlyList<CvColumnLayout> Columns)
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
    /// columns. About nine points on A4 — wider than any word space, and narrower than the gutter of
    /// the tightest sidebar template seen in the wild (a real CV whose columns sat 3.5% apart scored
    /// 98 when this was 6%). What keeps a narrow threshold honest is the rest of the test: the band
    /// has to run the height of every line on the page, with real text on both sides of it.</summary>
    private const double MinimumGutterRatio = 0.015;

    public static CvPageGeometry From(ExtractedCv cv)
    {
        if (cv.Format is not CvFileFormat.Pdf)
        {
            return new CvPageGeometry([]);
        }

        var columns = new List<CvColumnLayout>();

        foreach (var page in cv.Pages)
        {
            var words = page.Words.Where(word => !string.IsNullOrWhiteSpace(word.Text)).ToList();
            if (words.Count < MinimumWordsToJudge || page.Width <= 0 || page.Height <= 0)
            {
                continue;
            }

            if (FindGutter(page, words) is { } gutter)
            {
                columns.Add(gutter);
            }
        }

        return new CvPageGeometry(columns);
    }

    /// <summary>
    /// Looks for a vertical band down the middle of the page that no word crosses. Every word's
    /// horizontal extent is an interval; the intervals are merged, and the widest gap between them
    /// whose centre lies in the middle half of the page is the candidate gutter.
    ///
    /// Measured in points rather than in coarse slices of the page on purpose: template sites set
    /// the sidebar a few millimetres from the body, and rounding every word outwards to the next
    /// hundredth of the width used to close exactly that kind of gutter and report a two-column CV
    /// as one column.
    /// </summary>
    private static CvColumnLayout? FindGutter(ExtractedCvPage page, List<ExtractedCvWord> words)
    {
        var spans = words
            .Select(word => (From: Math.Max(word.Left, 0), To: Math.Min(word.Right, page.Width)))
            .Where(span => span.To > span.From)
            .OrderBy(span => span.From)
            .ToList();

        var gutterFrom = 0.0;
        var gutterTo = 0.0;
        var reach = double.NegativeInfinity;

        foreach (var (from, to) in spans)
        {
            if (reach > double.NegativeInfinity && from > reach)
            {
                // Only the middle half of the page: the margins are empty by definition and a gap
                // at 12% of the width is an indent.
                var center = (reach + from) / 2 / page.Width;
                if (from - reach > gutterTo - gutterFrom && center is > 0.25 and < 0.75)
                {
                    gutterFrom = reach;
                    gutterTo = from;
                }
            }

            reach = Math.Max(reach, to);
        }

        if ((gutterTo - gutterFrom) / page.Width < MinimumGutterRatio)
        {
            return null;
        }

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

        return new CvColumnLayout(page.Number, (gutterFrom + gutterTo) / 2 / page.Width);
    }
}

internal sealed record CvColumnLayout(int Page, double GutterCenterRatio);
