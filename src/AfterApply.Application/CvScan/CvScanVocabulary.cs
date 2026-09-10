using System.Text;
using System.Text.RegularExpressions;

namespace AfterApply.Application.CvScan;

/// <summary>
/// The words and shapes the deterministic checks look for. Turkish and English side by side,
/// because the CV of someone applying in Turkey is routinely one or the other and often both — a
/// scan that only knew "Experience" would report a perfectly readable Turkish CV as sectionless.
/// </summary>
public static partial class CvScanVocabulary
{
    /// <summary>
    /// Folds a heading down to something comparable: lower case, Turkish letters mapped to their
    /// ASCII shapes, punctuation dropped. The mapping is deliberate rather than incidental — it
    /// makes "DENEYİM", "Deneyim" and the diacritic-stripped "DENEYIM" one string, and a CV whose
    /// font ate its Turkish letters is exactly the case this scan exists to catch, so its headings
    /// must still be recognisable.
    /// </summary>
    public static string Fold(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            var folded = character switch
            {
                'ı' or 'I' or 'İ' or 'i' => 'i',
                'ş' or 'Ş' => 's',
                'ğ' or 'Ğ' => 'g',
                'ç' or 'Ç' => 'c',
                'ö' or 'Ö' => 'o',
                'ü' or 'Ü' => 'u',
                _ => char.ToLowerInvariant(character)
            };

            if (char.IsLetterOrDigit(folded) || folded == ' ')
            {
                builder.Append(folded);
            }
            else if (builder.Length > 0 && builder[^1] != ' ')
            {
                // Punctuation becomes a space rather than nothing: "eğitim/education" is two
                // headings on one line, not one word.
                builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Whether a folded line contains a heading as a whole phrase rather than as a fragment —
    /// "kariyer" must not match inside "kariyerim", which is the product's own name and appears on
    /// plenty of CVs by now.
    /// </summary>
    public static bool ContainsPhrase(string foldedLine, string phrase)
    {
        var index = foldedLine.IndexOf(phrase, StringComparison.Ordinal);
        while (index >= 0)
        {
            var startsCleanly = index == 0 || !char.IsLetterOrDigit(foldedLine[index - 1]);
            var end = index + phrase.Length;
            var endsCleanly = end == foldedLine.Length || !char.IsLetterOrDigit(foldedLine[end]);

            if (startsCleanly && endsCleanly)
            {
                return true;
            }

            index = foldedLine.IndexOf(phrase, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>The sections an ATS looks for by name. Work history and education are the two it
    /// cannot do without; skills is worth less because a parser that missed the heading can still
    /// find the words.</summary>
    public static readonly IReadOnlyDictionary<CvSection, string[]> SectionHeadings =
        new Dictionary<CvSection, string[]>
        {
            [CvSection.Experience] =
            [
                "deneyim", "is deneyimi", "is tecrubesi", "tecrube", "calisma gecmisi", "kariyer",
                "profesyonel deneyim", "experience", "work experience", "professional experience",
                "employment", "employment history", "career history", "work history"
            ],
            [CvSection.Education] =
            [
                "egitim", "egitim bilgileri", "ogrenim", "akademik", "okul", "education",
                "academic background", "academic", "qualifications"
            ],
            [CvSection.Skills] =
            [
                "yetenek", "yetenekler", "beceri", "beceriler", "yetkinlik", "yetkinlikler",
                "teknik beceriler", "skills", "technical skills", "core skills", "competencies",
                "technologies", "tech stack"
            ]
        };

    /// <summary>A month named in either language — the strongest single signal that a line is a
    /// date, because a bare year could be anything.</summary>
    [GeneratedRegex(
        @"\b(ocak|subat|mart|nisan|mayis|haziran|temmuz|agustos|eylul|ekim|kasim|aralik|" +
        @"january|february|march|april|may|june|july|august|september|october|november|december|" +
        @"jan|feb|mar|apr|jun|jul|aug|sep|sept|oct|nov|dec)\b",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    public static partial Regex MonthName { get; }

    /// <summary>A four-digit year in the range a CV plausibly mentions.</summary>
    [GeneratedRegex(@"\b(19|20)\d{2}\b", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Year { get; }

    /// <summary>
    /// A span rather than a point: "2019 - 2021", "03/2020 – halen", "Jan 2018 — Present". This is
    /// what a parser needs to build an employment timeline, and its absence is the difference
    /// between "we found dates" and "we found numbers".
    /// </summary>
    [GeneratedRegex(
        @"((19|20)\d{2}|\d{1,2}[./]\d{4})\s*[-–—/]{1,2}\s*((19|20)\d{2}|\d{1,2}[./]\d{4}|halen|devam|" +
        @"present|current|now|today)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    public static partial Regex DateRange { get; }

    /// <summary>Deliberately loose. This is not address validation — it decides whether a machine
    /// can find an e-mail on the page at all, and a false positive there costs nothing.</summary>
    [GeneratedRegex(@"[\w.+-]+@[\w-]+\.[\w.-]{2,}", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Email { get; }

    /// <summary>
    /// Turkish shapes first (+90 5xx, 0(5xx), 05xx with spaces or dots), then a generic
    /// international run of digits. Same reasoning as the e-mail pattern: recall over precision.
    /// </summary>
    [GeneratedRegex(@"(\+\d{1,3}[\s.\-()]*)?\(?0?\d{3}\)?[\s.\-]*\d{3}[\s.\-]*\d{2}[\s.\-]*\d{2}",
        RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex Phone { get; }

    /// <summary>PdfPig's marker for a glyph whose font carries no usable ToUnicode map. It is the
    /// literal text "(cid:123)" appearing in extracted output — the machine's way of saying it saw
    /// ink it could not name.</summary>
    [GeneratedRegex(@"\(cid:\d+\)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    public static partial Regex UnmappedGlyph { get; }

    /// <summary>Common Turkish words with no Turkish-specific letter in them. Their presence says
    /// the document is Turkish; whether ı, ş, ğ, ç, ö and ü also survived is then a fair question
    /// to ask of the font.</summary>
    public static readonly string[] TurkishMarkers =
    [
        " ve ", " ile ", " bir ", " icin ", " olarak ", "deneyim", "sirket", "universite", "proje",
        "gelistirme", "muhendis", "calisma", "egitim", "yetenek", "sorumlu"
    ];
}

public enum CvSection
{
    Experience,
    Education,
    Skills
}
