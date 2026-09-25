namespace CvScanCorpus;

/// <summary>
/// Synthetic people. Every name, employer and address here is invented, e-mails are on
/// example.com and phone numbers are in the 555 000 block — nothing in the corpus can be anyone.
/// </summary>
internal sealed record Persona(
    string Lang,
    string FullName,
    string Title,
    string Email,
    string Phone,
    string City,
    string Summary,
    IReadOnlyList<Job> Jobs,
    string School,
    string Degree,
    int SchoolStart,
    int SchoolEnd,
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Certificates);

internal sealed record Job(string Title, string Company, int StartYear, int StartMonth, int? EndYear, int? EndMonth,
    IReadOnlyList<string> Bullets);

internal sealed record Profession(string TitleTr, string TitleEn, string[] BulletsTr, string[] BulletsEn, string[] Skills);

internal static class Personas
{
    private static readonly string[] FirstTr = ["Deniz", "Ece", "Burak", "Selin", "Mert", "Zeynep", "Kaan", "Elif", "Onur", "Gizem", "Arda", "Ceren", "Emre", "Pınar", "Tolga", "Işıl"];
    private static readonly string[] LastTr = ["Aksoy", "Yıldırım", "Kılıç", "Şahin", "Öztürk", "Çelik", "Doğan", "Güneş", "Arslan", "Kaya", "Erdoğdu", "Uçar"];
    private static readonly string[] FirstEn = ["Alex", "Jordan", "Sam", "Taylor", "Morgan", "Casey", "Robin", "Jamie", "Avery", "Riley"];
    private static readonly string[] LastEn = ["Carter", "Bennett", "Hughes", "Porter", "Lawson", "Fletcher", "Reed", "Hayes", "Warren", "Ellis"];
    private static readonly string[] CitiesTr = ["İstanbul / Kadıköy", "Ankara / Çankaya", "İzmir / Karşıyaka", "Bursa / Nilüfer", "Eskişehir"];
    private static readonly string[] CitiesEn = ["Istanbul, Türkiye", "Ankara, Türkiye", "Izmir, Türkiye", "Amsterdam, Netherlands", "Berlin, Germany"];
    private static readonly string[] CompaniesTr = ["Örnek Yazılım A.Ş.", "Mavi Lojistik", "Deneme Teknoloji", "Kuzey Perakende", "Yıldız Finans", "Atlas Sağlık Grubu", "Ege Enerji", "Pusula Danışmanlık"];
    private static readonly string[] CompaniesEn = ["Acme Software", "Northwind Logistics", "Contoso Retail", "Fabrikam Finance", "Tailspin Health", "Blue Yonder Energy", "Litware Consulting", "Adatum Labs"];
    private static readonly string[] SchoolsTr = ["Örnek Üniversitesi", "Boğaziçi Deneme Üniversitesi", "Ege Örnek Üniversitesi", "Ankara Deneme Üniversitesi"];

    private static readonly Profession[] Professions =
    [
        new("Yazılım Mühendisi", "Software Engineer",
        [
            "Ödeme servisini kuyruk tabanlı bir mimariye taşıyarak hata oranını yüzde kırk azalttı.",
            "Sipariş takip sisteminin uçtan uca geliştirilmesini ve nöbet sürecini yönetti.",
            "Finans ekibinin haftalık mutabakat raporunu besleyen veri hattını kurdu.",
            "İki yeni mühendise kod incelemesi ve üretim olayları konusunda mentorluk yaptı.",
            "Arama indeksini gece yeniden oluşturmadan artımlı güncellemeye geçirdi.",
            "Mağaza ile stok servisi arasına sözleşme testleri ekledi.",
            "Kampanya öncesi yük testlerini yürütüp küme kapasitesini belirledi.",
            "Bildirim servisini, bir sağlayıcı çöktüğünde diğerlerini durdurmayacak şekilde yeniden düzenledi."
        ],
        [
            "Moved the payment service onto a queue based design and cut the error rate by forty percent.",
            "Owned the order tracking system end to end, including its on call rotation.",
            "Built the data pipeline that feeds the finance team their weekly reconciliation report.",
            "Mentored two new engineers through code review and their first production incidents.",
            "Migrated the search index from a nightly rebuild to incremental updates.",
            "Introduced contract tests between the storefront and the inventory service.",
            "Ran the load tests that sized the cluster before the seasonal campaign.",
            "Reworked the notification service so one failing provider no longer blocked the others."
        ],
        ["C#", ".NET", "PostgreSQL", "Docker", "Kubernetes", "RabbitMQ", "React", "TypeScript", "Git"]),

        new("Dijital Pazarlama Uzmanı", "Digital Marketing Specialist",
        [
            "Arama ağı kampanyalarında tıklama başına maliyeti altı ayda yüzde yirmi beş düşürdü.",
            "Aylık bülten listesini on iki binden otuz bin aboneye çıkardı.",
            "Ürün lansmanları için içerik takvimini planladı ve ajansla koordinasyonu yürüttü.",
            "Pazarlama panosunu kurarak kanal bazında dönüşüm raporlamasını otomatikleştirdi.",
            "A/B testleriyle açılış sayfası dönüşümünü yüzde on sekiz artırdı.",
            "Sosyal medya hesaplarında topluluk yönetimi ve kriz iletişimini üstlendi.",
            "Etkinlik sponsorluklarının bütçesini ve getiri ölçümünü yönetti.",
            "Satış ekibiyle birlikte potansiyel müşteri puanlama modelini tasarladı."
        ],
        [
            "Reduced cost per click on search campaigns by twenty five percent in six months.",
            "Grew the monthly newsletter list from twelve thousand to thirty thousand subscribers.",
            "Planned the content calendar for product launches and coordinated with the agency.",
            "Built the marketing dashboard and automated conversion reporting per channel.",
            "Raised landing page conversion by eighteen percent through structured A/B tests.",
            "Handled community management and crisis communication on social channels.",
            "Managed the budget and return measurement of event sponsorships.",
            "Designed the lead scoring model together with the sales team."
        ],
        ["Google Ads", "Google Analytics", "SEO", "HubSpot", "Figma", "Excel", "Looker Studio", "Meta Ads"]),

        new("Muhasebe Uzmanı", "Accounting Specialist",
        [
            "Aylık kapanış sürecini on iki iş gününden yedi iş gününe indirdi.",
            "Yüz elliden fazla tedarikçinin cari hesap mutabakatını yürüttü.",
            "KDV ve muhtasar beyannamelerini zamanında ve eksiksiz hazırladı.",
            "Bağımsız denetim sürecinde denetçilere belge ve analiz desteği verdi.",
            "Masraf merkezleri bazında bütçe gerçekleşme raporlarını hazırladı.",
            "Yeni ERP sistemine geçişte hesap planının aktarımını üstlendi.",
            "Sabit kıymet kayıtlarını ve amortisman hesaplarını düzenledi.",
            "Nakit akış tahminlerini haftalık olarak finans müdürüne raporladı."
        ],
        [
            "Shortened the monthly close from twelve working days to seven.",
            "Ran the account reconciliation for more than one hundred and fifty suppliers.",
            "Prepared VAT and withholding returns on time and in full.",
            "Supported the external auditors with documents and analysis during the audit.",
            "Prepared budget versus actual reports per cost centre.",
            "Owned the chart of accounts migration during the move to the new ERP.",
            "Maintained fixed asset records and depreciation schedules.",
            "Reported weekly cash flow forecasts to the finance manager."
        ],
        ["SAP FI", "Logo", "Excel", "IFRS", "Power BI", "Netsis", "Budgeting", "Reconciliation"]),

        new("İnsan Kaynakları Uzmanı", "Human Resources Specialist",
        [
            "Yılda iki yüzden fazla pozisyon için işe alım sürecini baştan sona yönetti.",
            "Oryantasyon programını yeniden tasarlayarak ilk yıl ayrılma oranını düşürdü.",
            "Performans değerlendirme döneminin planlamasını ve raporlamasını yürüttü.",
            "Ücret yan hak karşılaştırma çalışmasını hazırlayıp yönetime sundu.",
            "Çalışan bağlılığı anketinin sonuçlarına göre aksiyon planları oluşturdu.",
            "Bordro sürecinde puantaj kontrolü ve mevzuat uyumunu sağladı.",
            "Üniversite iş birlikleriyle staj programını kurdu.",
            "İK süreçlerini yeni bir insan kaynakları yazılımına taşıdı."
        ],
        [
            "Ran recruitment end to end for more than two hundred positions a year.",
            "Redesigned the onboarding programme and lowered first year attrition.",
            "Planned and reported the performance review cycle.",
            "Prepared a compensation and benefits benchmark and presented it to management.",
            "Built action plans from the results of the employee engagement survey.",
            "Checked timesheets and legal compliance during payroll.",
            "Set up the internship programme through university partnerships.",
            "Moved HR processes onto a new human resources platform."
        ],
        ["Recruitment", "Onboarding", "Payroll", "Labour Law", "Excel", "Workday", "Interviewing", "Employer Branding"]),

        new("Hemşire", "Registered Nurse",
        [
            "Yirmi yataklı dahiliye servisinde vardiya sorumlusu olarak görev yaptı.",
            "Yeni başlayan hemşirelerin klinik uyum eğitimlerini verdi.",
            "İlaç uygulama hatalarını azaltan çift kontrol protokolünü başlattı.",
            "Hasta düşme riskini değerlendirme formunu servise uyarladı.",
            "Enfeksiyon kontrol komitesinde servis temsilcisi olarak çalıştı.",
            "Taburculuk eğitim broşürlerini hasta geri bildirimlerine göre yeniledi.",
            "Yoğun dönemlerde acil servise destek vardiyaları üstlendi.",
            "Elektronik hasta kayıt sistemine geçişte servis eğitimlerini yürüttü."
        ],
        [
            "Worked as shift lead on a twenty bed internal medicine ward.",
            "Delivered clinical onboarding training to newly hired nurses.",
            "Introduced a double check protocol that reduced medication errors.",
            "Adapted the patient fall risk assessment form for the ward.",
            "Represented the ward on the infection control committee.",
            "Rewrote discharge education leaflets based on patient feedback.",
            "Covered support shifts in the emergency department during peak periods.",
            "Led ward training during the move to electronic patient records."
        ],
        ["Patient Care", "Medication Administration", "Triage", "Infection Control", "EHR", "Wound Care", "BLS", "Team Leadership"])
    ];

    public static Persona Create(Random random, string lang, int jobCount, int bulletsPerJob)
    {
        var tr = lang == "tr";
        var profession = Professions[random.Next(Professions.Length)];
        var first = tr ? Pick(random, FirstTr) : Pick(random, FirstEn);
        var last = tr ? Pick(random, LastTr) : Pick(random, LastEn);
        var slug = Ascii($"{first}.{last}").ToLowerInvariant();

        var jobs = new List<Job>();
        var endYear = 2026;
        var endMonth = 9;
        var bullets = tr ? profession.BulletsTr : profession.BulletsEn;
        var companies = (tr ? CompaniesTr : CompaniesEn).OrderBy(_ => random.Next()).ToList();

        for (var index = 0; index < jobCount; index++)
        {
            var years = 1 + random.Next(3);
            var startYear = endYear - years;
            var startMonth = 1 + random.Next(12);
            var title = index == 0
                ? (tr ? "Kıdemli " : "Senior ") + (tr ? profession.TitleTr : profession.TitleEn)
                : tr ? profession.TitleTr : profession.TitleEn;

            jobs.Add(new Job(title, companies[index % companies.Count], startYear, startMonth,
                index == 0 ? null : endYear, index == 0 ? null : endMonth,
                Enumerable.Range(0, bulletsPerJob)
                    .Select(offset => bullets[(index * 3 + offset) % bullets.Length])
                    .ToList()));

            endYear = startYear;
            endMonth = startMonth == 1 ? 12 : startMonth - 1;
        }

        var schoolEnd = endYear - random.Next(2);
        var summary = tr
            ? $"{profession.TitleTr} olarak {2026 - schoolEnd} yıllık deneyime sahibim. Ölçülebilir sonuçlara odaklanan, ekip çalışmasına yatkın ve sürekli öğrenmeye açık biriyim. Son rolümde süreçleri sadeleştirerek ekibin verimliliğini artırdım."
            : $"{profession.TitleEn} with {2026 - schoolEnd} years of experience. Focused on measurable results, comfortable in cross-functional teams and always learning. In my latest role I simplified processes and raised the team's throughput.";

        return new Persona(lang, $"{first} {last}", tr ? profession.TitleTr : profession.TitleEn,
            $"{slug}@example.com",
            $"+90 555 000 {random.Next(10, 99)} {random.Next(10, 99)}",
            tr ? Pick(random, CitiesTr) : Pick(random, CitiesEn),
            summary, jobs,
            tr ? Pick(random, SchoolsTr) : Ascii(Pick(random, SchoolsTr)).Replace("Universitesi", "University"),
            tr ? "Lisans" : "Bachelor's degree",
            schoolEnd - 4, schoolEnd,
            profession.Skills.OrderBy(_ => random.Next()).Take(6).ToList(),
            tr ? ["Türkçe (Ana dil)", "İngilizce (B2)"] : ["Turkish (Native)", "English (C1)"],
            tr ? ["Proje Yönetimi Sertifikası, 2023"] : ["Project Management Certificate, 2023"]);
    }

    private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];

    /// <summary>Turkish letters folded to ASCII — for e-mail slugs, and for the corpus cases where a
    /// CV's Turkish letters were flattened on export.</summary>
    public static string Ascii(string value) => value
        .Replace('ı', 'i').Replace('İ', 'I').Replace('ş', 's').Replace('Ş', 'S').Replace('ğ', 'g')
        .Replace('Ğ', 'G').Replace('ç', 'c').Replace('Ç', 'C').Replace('ö', 'o').Replace('Ö', 'O')
        .Replace('ü', 'u').Replace('Ü', 'U');
}
