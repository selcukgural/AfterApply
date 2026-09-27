namespace AfterApply.Application.SalaryMarket;

/// <summary>
/// One occupation on the public salary pages: the survey answers of every year are folded into
/// one of these, so "Back-end Developer | .Net" (2018) and "Back-end Developer" (2026) are the
/// same line. <see cref="Slug"/> is the page address in both locales and never changes once
/// published. <see cref="OccupationCodes"/> are the catalogue rows (occupations.v1.csv) whose
/// company-salary tab links here.
/// </summary>
public sealed record SalaryMarketGroup(string Slug, string NameTr, string NameEn, IReadOnlyList<string> OccupationCodes);

public static class SalaryMarketGroups
{
    public static readonly IReadOnlyList<SalaryMarketGroup> All =
    [
        new("full-stack-developer", "Full Stack Developer", "Full Stack Developer", ["EK-0003"]),
        new("back-end-developer", "Back-end Developer", "Back-end Developer", ["EK-0001"]),
        new("front-end-developer", "Front-end Developer", "Front-end Developer", ["EK-0002"]),
        new("mobile-developer", "Mobil Uygulama Geliştirici", "Mobile Developer", ["EK-0004", "EK-0005", "EK-0006"]),
        new("embedded-software-developer", "Gömülü Yazılım Geliştirici", "Embedded Software Developer", ["EK-0011"]),
        new("devops-engineer", "DevOps / SRE", "DevOps / SRE", ["EK-0016", "EK-0017", "EK-0018", "EK-0019"]),
        new("qa-engineer", "QA / Test Mühendisi", "QA / Test Engineer", ["EK-0013", "EK-0014", "EK-0015"]),
        new("data-scientist", "Veri Bilimci / ML Mühendisi", "Data Scientist / ML Engineer", ["EK-0026", "EK-0027"]),
        new("data-analyst", "Veri Analisti", "Data Analyst", ["EK-0029"]),
        new("data-engineer", "Veri Mühendisi", "Data Engineer", ["EK-0024"]),
        new("game-developer", "Oyun Geliştirici", "Game Developer", ["EK-0012"]),
        new("software-architect", "Yazılım Mimarı", "Software Architect", ["EK-0010"]),
        new("team-lead", "Team / Tech Lead", "Team / Tech Lead", ["EK-0041"]),
        new("engineering-manager", "Yazılım Geliştirme Yöneticisi", "Engineering Manager", ["EK-0040"]),
        new("software-director", "Yazılım Direktörü", "Director of Software Development", []),
        new("cto", "CTO", "CTO", ["EK-0042"]),
        new("product-manager", "Ürün Yöneticisi / Product Owner", "Product Manager / Product Owner", ["EK-0035", "EK-0036", "EK-0037"]),
        new("project-manager", "Proje Yöneticisi", "Project Manager", ["EK-0038"]),
        new("business-analyst", "İş Analisti", "Business Analyst", ["EK-0030"]),
        new("sap-erp-developer", "SAP / ERP Geliştirici", "SAP / ERP Developer", ["EK-0033", "EK-0034"]),
        new("database-administrator", "Veritabanı Yöneticisi (DBA)", "Database Administrator (DBA)", ["EK-0025"]),
        new("cyber-security", "Siber Güvenlik Uzmanı", "Cybersecurity Specialist", ["EK-0021", "EK-0022", "EK-0023"]),
        new("system-engineer", "Sistem / Destek Mühendisi", "Systems / Support Engineer", ["EK-0020", "EK-0049"]),
        new("ai-engineer", "AI Engineer", "AI Engineer", ["EK-0028"]),
    ];

    private static readonly IReadOnlyDictionary<string, SalaryMarketGroup> BySlugMap =
        All.ToDictionary(g => g.Slug, StringComparer.Ordinal);

    public static SalaryMarketGroup? BySlug(string slug) => BySlugMap.GetValueOrDefault(slug);
}
