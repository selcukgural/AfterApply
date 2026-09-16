namespace AfterApply.UnitTests.JobSources;

/// <summary>
/// kariyer.net markup as the site served it on 2026-09-14, trimmed to what the parsers read and
/// with synthetic companies. The <c>data-test</c> attributes and the Vue scoped-style
/// <c>data-v-*</c> noise are kept as they came, because that is what the regexes have to live with.
/// </summary>
public static class KariyerNetJobSourceFixtures
{
    public static string Card(string id, string slug, string title, string company, string location, string workModel, string date) => $"""
        <div class="job-list-card-item" data-v-bc2b9780 data-v-e8af4478><div data-v-bc2b9780><a href="/is-ilani/{slug}-{id}" target="_blank" data-test="ad-card-item" class="k-ad-card radius" data-v-bc2b9780><!----> <div data-test="ad-card-top" class="card-top" data-v-bc2b9780><div data-test="company-logo" data-v-134bbc72 data-v-bc2b9780><img data-test="company-image" src="https://img-kariyer.mncdn.com/mnresize/150/150/x.png" alt="{company}" class="default-img" data-v-134bbc72 data-v-134bbc72></div> <div data-test="title-wrapper" class="title-wrapper" data-v-bc2b9780><div class="title-left" data-v-bc2b9780><span data-test="ad-card-title" class="k-ad-card-title multiline" data-v-bc2b9780>{title}</span> <div data-test="title-icon" class="title-icon" data-v-bc2b9780><!----> <!----></div></div> <div data-test="subtitle-section" class="subtitle" data-v-bc2b9780><span data-test="subtitle" data-v-bc2b9780>{company}</span></div> <div data-test="job-detail" class="job-detail" data-v-bc2b9780><span data-test="location" class="location" data-v-bc2b9780>{location}</span> <span class="dot" data-v-bc2b9780></span> <span data-test="work-model" class="work-model" data-v-bc2b9780>{workModel}</span></div></div> <!----></div> <div data-test="card-bottom-wrapper" class="card-bottom-wrapper" data-v-bc2b9780><div data-test="card-bottom" class="card-bottom" data-v-bc2b9780><div data-test="badges-section" class="badges-wrapper" data-v-bc2b9780><!----> </div></div></div> <div class="card-footer-wrapper" data-v-bc2b9780><div class="footer-badges" data-v-bc2b9780><div data-type="default" data-test="mapped-badges" class="badge-item badge-item--default" data-v-bc2b9780><!----> <span data-test="text" class="text" data-v-bc2b9780>Tam zamanlı</span></div></div> <div data-test="ad-date" class="ad-date" data-v-bc2b9780><span data-test="ad-date-item-date-other" class="date date-other" data-v-bc2b9780><i class="kariyer-icons update-icon" data-v-bc2b9780>update</i> {date}</span></div></div></a> <!----></div></div>
        """;

    /// <summary>Three cards and a pager that offers page 2 — a first page of a longer list.</summary>
    public static readonly string ThreeCardsWithNextPage =
        "<div class=\"list\">" +
        Card("4460000001", "acme-yazilim-a-s-software-developer-net", "Software Developer (.Net)", "Acme Yazılım A.Ş.", "İstanbul", "İş Yerinde", "3 gün") +
        Card("4460000002", "beta-bilisim-backend-developer", "Backend Developer &amp; Team Lead", "Beta Bilişim", "İstanbul(Asya)", "Uzaktan", "12 saat") +
        Card("4460000003", "gamma-teknoloji-net-developer", ".NET Developer", "Gamma Teknoloji", "İstanbul(Asya) +2 il daha", "Hibrit", "Dün") +
        "</div><div data-test=\"pagination\" class=\"py-3\"><a href=\"/is-ilanlari/istanbul?ct=34%2C82&amp;kw=.net+developer&amp;cp=1\">1</a>" +
        "<a href=\"/is-ilanlari/istanbul-2?ct=34%2C82&amp;kw=.net+developer&amp;cp=2\">2</a></div>";

    /// <summary>One card, and a pager with no page after this one.</summary>
    public static readonly string LastPage =
        Card("4460000009", "delta-a-s-yazilim-uzmani", "Yazılım Uzmanı", "Delta A.Ş.", "Ankara", "İş Yerinde", "20 gün") +
        "<div data-test=\"pagination\"><a href=\"/is-ilanlari/istanbul?ct=34%2C82&amp;kw=x&amp;cp=1\">1</a><a href=\"/is-ilanlari/istanbul-2?ct=34%2C82&amp;kw=x&amp;cp=2\">2</a></div>";

    public static string Posting(string descriptionMarkup, string experience = "En az 7 yıl tecrübeli", string publishDate = "11.09.2026") => $"""
        <html><head><title>Acme Yazılım A.Ş. Software Developer (.Net) İş İlanı - {publishDate}</title></head><body>
        <div class="card-footer-wrapper"><div class="footer-badges"><div data-test="mapped-badges" class="badge-item"><span data-test="text" class="text">Tam zamanlı</span></div></div></div>
        <div isActive="true" versionId="1" closingDate="29.09.2026" lastPublishDate="{publishDate}" jobRecommendationSkill="" class="job-container mt-3 mt-md-4 mb-3" data-v-ff90018a data-v-82c024c2><h2 data-test="qualifications-and-job-description-headline" class="job-headline-title" data-v-ff90018a> İş İlanı Hakkında </h2> <div class="job-detail-container-description" data-v-ff90018a><div class="job-sub-detail" data-v-ff90018a><!----> <!----> <div class="job-detail-content fixed-height-container" data-v-ff90018a><div data-test="qualifications-and-job-description" class="job-detail-qualifications" data-v-ff90018a>{descriptionMarkup}</div> <!----> <div class="aligment-container my-3" data-v-0bde6fce data-v-ff90018a><header data-v-0bde6fce><h2 data-test="alignment-list-header" data-v-0bde6fce>Aday Kriterleri</h2></header> <section data-v-0bde6fce><div class="alignment-list" data-v-0bde6fce><div data-test="alignment-list-title" class="alignment-title" data-v-0bde6fce><span class="bullet" data-v-0bde6fce>•</span> Tecrübe </div> <div data-test="alignment-list-value" class="alignment-value" data-v-0bde6fce> {experience} </div><div data-test="alignment-list-title" class="alignment-title" data-v-0bde6fce><span class="bullet" data-v-0bde6fce>•</span> Eğitim Seviyesi </div> <div data-test="alignment-list-value" class="alignment-value" data-v-0bde6fce> Üniversite(Mezun) </div></div></section></div></div></div></div></div>
        </body></html>
        """;

    public const string RichDescription =
        "<p>Kıdemli Yazılım Mühendisi (.NET)</p><br><p>Ekibimizde değerlendirmek üzere <strong>7+ yıl deneyimli</strong> arıyoruz.</p><p><br></p>" +
        "<p><strong>Aradığımız Nitelikler:</strong></p><ul><li>C# ve .NET / .NET Core konusunda güçlü deneyim, </li><li>REST API &amp; web servisleri, </li>" +
        "<li><strong>İstanbul’da ikamet eden.</strong></li></ul><p></p>";
}
