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
                "deneyim", "deneyimler", "is deneyimi", "is deneyimleri", "is tecrubesi", "tecrube",
                "tecrubeler", "calisma gecmisi", "calisma deneyimi", "staj deneyimi", "is staj deneyimi",
                "working experience", "experience summary", "employment details",
                "kariyer", "kariyer gecmisi",
                "profesyonel deneyim", "experience", "work experience", "professional experience",
                "employment", "employment history", "career history", "work history"
            ],
            [CvSection.Education] =
            [
                "egitim", "egitim bilgileri", "egitim gecmisi", "ogrenim", "ogrenim bilgileri", "akademik",
                "okul", "education", "education and training", "educational qualification",
                "educational qualifications", "education qualification", "educational background",
                "educational summary", "academic profile", "academics", "scholastic",
                "academic background", "academic", "qualifications"
            ],
            [CvSection.Skills] =
            [
                "yetenek", "yetenekler", "beceri", "beceriler", "temel beceriler", "yetkinlik",
                "yetkinlikler", "teknik beceriler", "uzmanlik alanlari", "skills", "technical skills",
                "core skills", "key skills", "competencies", "core competencies", "areas of expertise",
                "computer proficiency", "computer knowledge", "technical proficiency",
                "technologies", "tech stack"
            ]
        };

    /// <summary>
    /// Every heading a CV routinely carries, scored or not: the three above plus the ones no score
    /// depends on (contact, summary, languages, certificates...). Used only to recognise a line
    /// that <em>is</em> a heading and nothing else, which is what the reading-order check needs —
    /// a heading's neighbours say whether the text arrived in the order it was written.
    /// </summary>
    public static readonly IReadOnlySet<string> AllHeadings = SectionHeadings.Values
        .SelectMany(headings => headings)
        .Concat(
        [
            "iletisim", "iletisim bilgileri", "kisisel bilgiler", "hakkimda", "ozet", "profil",
            "ozgecmis", "hedef", "kariyer hedefi", "dil", "diller", "dil becerileri", "yabanci dil",
            "yabanci diller", "sertifikalar", "sertifika", "kurslar", "projeler", "referanslar",
            "hobiler", "ilgi alanlari", "oduller", "basarilar", "uyelikler", "gonullu aktiviteler",
            "gonulluluk", "yayinlar",
            "contact", "contact information", "personal information", "about me", "about", "summary",
            "profile", "professional summary", "objective", "languages", "certificates",
            "certifications", "courses", "projects", "references", "hobbies", "interests", "awards",
            "achievements", "volunteering", "volunteer experience", "memberships", "publications",
            "honors", "languages and skills", "career objective", "career objectives", "personal details",
            "personal detail", "personal data", "personal profile", "profile summary", "strengths",
            "strength", "declaration", "languages known", "area of interest", "areas of interest",
            "extra curricular activities", "certification", "training", "industrial training",
            "responsibilities", "job responsibilities", "roles and responsibilities", "project details",
            "academic project", "passport details"
        ])
        .ToHashSet(StringComparer.Ordinal);

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
    /// A span rather than a point: "2019 - 2021", "03/2020 – halen", "Jan 2018 — Present",
    /// "Aug 2007 to Current", "December 2014 to May 2015", "2019-03 / 2021-05". This is what a
    /// parser needs to build an employment timeline, and its absence is the difference between
    /// "we found dates" and "we found numbers". A month name before the closing year is allowed
    /// because the opening one is already anchored on a year; "to", "until" and "through" are
    /// separators as ordinary as a dash in English CVs.
    /// </summary>
    [GeneratedRegex(
        @"((19|20)\d{2}-\d{2}|\d{1,2}[./]\d{4}|(19|20)\d{2})" +
        // Every dash a font can carry: hyphen-minus, en and em dash, and the ones that look the same
        // on the page but are other code points — U+2010-2012 hyphens, U+2212 minus and the
        // full-width U+FF0D that some CV builders emit.
        @"\s*([-‐‑‒–—−－/]{1,2}|\b(to|until|till|through|thru)\b)\s*" +
        // A month named before the closing year. Only the plain form ("to May 2015"): "Mar-2016",
        // "DEC’ 2016" and two-digit closing years ("2015-16") are left out on purpose. Nobody has
        // shown that real parsers read them, and a scan that is more lenient than the systems it
        // stands in for gives a high score to a CV those systems misread — the complaint this
        // whole scan exists to avoid (DECISIONS.md 2026-09-25). When in doubt, the safe format is
        // the one we ask for.
        @"(\p{L}{3,9}\.?\s+)?" +
        @"((19|20)\d{2}-\d{2}|\d{1,2}[./]\d{4}|(19|20)\d{2}|halen|devam|günümüz|gunumuz|" +
        @"present|current|now|today|date)",
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
