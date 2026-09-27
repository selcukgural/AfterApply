namespace SalarySurveyImport;

/// <summary>How a published file lays its answers out.</summary>
internal enum FileLayout
{
    /// <summary>2018: one sheet, no header row, six columns in a fixed order.</summary>
    Xlsx2018,

    /// <summary>2019: one sheet with an English header row.</summary>
    XlsxWithHeader,

    /// <summary>2020–2026: a JSON array of objects; the key names change over the years (below).</summary>
    Json
}

/// <summary>Where a survey year's file is, and which of its keys hold what we read.</summary>
internal sealed record SurveyEdition(
    int Year,
    string SourceCode,
    string SourceName,
    string SourceUrl,
    string PublishedMonth,
    string FileName,
    string DownloadUrl,
    FileLayout Layout,
    string PositionKey = "position",
    string LevelKey = "level",
    string ExperienceKey = "experience",
    string CityKey = "city",
    string? CurrencyKey = "currency",
    string SalaryKey = "salary");

internal static class SurveyEditions
{
    private const string Onceki = "onceki-yazilimci";
    private const string OncekiName = "Önceki Yazılımcı";
    private const string Raw = "https://raw.githubusercontent.com/oncekiyazilimci";

    // The month each year's results went up on GitHub (the repository's first push), which is
    // what a reader can check; the surveys ran in the weeks before it.
    public static readonly IReadOnlyList<SurveyEdition> All =
    [
        new(2018, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2018-yazilimci-maaslari", "2018-07",
            "onceki-2018.xlsx", $"{Raw}/2018-yazilimci-maaslari/master/yazilimci-maaslari.xlsx", FileLayout.Xlsx2018, CurrencyKey: null),
        new(2019, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2019-yazilimci-maaslari", "2019-02",
            "onceki-2019.xlsx", $"{Raw}/2019-yazilimci-maaslari/master/yazilimci-maaslari-anketi-2019.xlsx", FileLayout.XlsxWithHeader, CurrencyKey: null),
        new(2020, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2020-yazilimci-maaslari", "2020-04",
            "onceki-2020.json", $"{Raw}/2020-yazilimci-maaslari/master/yazilimci-maaslari-2020.json", FileLayout.Json, CurrencyKey: null),
        new(2021, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2021-yazilimci-maaslari", "2021-06",
            "onceki-2021.json", $"{Raw}/2021-yazilimci-maaslari/main/maas-anketi-2021.json", FileLayout.Json, SalaryKey: "salary_for_tl_currency"),
        new(2022, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2022-yazilimci-maaslari", "2022-04",
            "onceki-2022.json", $"{Raw}/2022-yazilimci-maaslari/main/maas-anketi.json", FileLayout.Json, SalaryKey: "salary_for_tl_currency"),
        new(2023, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2023-yazilim-sektoru-maaslari", "2023-03",
            "onceki-2023.json", $"{Raw}/2023-yazilim-sektoru-maaslari/main/2023-yazilim-sektoru-maaslari-oncekiyazilimci.json", FileLayout.Json),
        new(2024, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2024-yazilim-sektoru-maaslari", "2024-03",
            "onceki-2024.json", $"{Raw}/2024-yazilim-sektoru-maaslari/main/2024-yazilim-sektoru-maaslari-onceki-yazilimci.json", FileLayout.Json),
        new(2025, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2025-yazilim-sektoru-maaslari", "2025-03",
            "onceki-2025.json", $"{Raw}/2025-yazilim-sektoru-maaslari/main/2025-yazilim-sektoru-maaslari-onceki-yazilimci.json", FileLayout.Json),
        new(2026, Onceki, OncekiName, "https://github.com/oncekiyazilimci/2026-yazilim-sektoru-maaslari", "2026-03",
            "onceki-2026.json", $"{Raw}/2026-yazilim-sektoru-maaslari/main/2026-yazilim-sektoru-maaslari-onceki-yazilimci.json", FileLayout.Json,
            PositionKey: "Hangi pozisyonda çalışıyorsunuz?",
            LevelKey: "Uzmanlığınız nedir?",
            ExperienceKey: "Kaç yıldır sektörde çalışıyorsunuz?",
            CityKey: "Hangi ülkede/şehirde çalışıyorsunuz/yaşıyorsunuz?",
            CurrencyKey: "Hangi para birimi ile maaş alıyorsunuz?",
            // The 2026 salary question is a long sentence; the reader matches it by this prefix.
            SalaryKey: "Aylık [NET] geliriniz nedir?"),
        // yazilimcimaaslari.org (Şubat 2026, 1.223 kişi) joins the 2026 pool once its author has
        // agreed (DECISIONS.md 2026-09-27); its file and keys go here then.
    ];
}
