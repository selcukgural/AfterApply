namespace AfterApply.UnitTests.JobSources;

/// <summary>
/// Shapes mirror real fetches of LinkedIn's public job-search and job-posting fragments on
/// 2026-09-12 (plain HTTP GET, honest bot User-Agent, logged out), trimmed to what the parsers
/// read and with the companies replaced by made-up ones.
/// </summary>
public static class LinkedInJobSourceFixtures
{
    public static string Card(string id, string title, string company, string companySlug, string location, string date) => $"""
        <li>
          <div class="base-card relative w-full hover:no-underline focus:no-underline
            base-card--link
             base-search-card base-search-card--link job-search-card" data-entity-urn="urn:li:jobPosting:{id}" data-impression-id="jobs-search-result-0" data-reference-id="lQqt2AqM9T1wxfNKQzxcbQ==" data-tracking-id="A2jC1eHTXhSjEX4JrvRApA==" data-column="1" data-row="1">
            <a class="base-card__full-link absolute top-0 right-0 bottom-0 left-0 p-0 z-[2] outline-offset-[4px]" href="https://tr.linkedin.com/jobs/view/{companySlug}-{id}?position=1&amp;pageNum=0&amp;refId=lQqt2AqM9T1wxfNKQzxcbQ%3D%3D&amp;trackingId=A2jC1eHTXhSjEX4JrvRApA%3D%3D" data-tracking-control-name="public_jobs_jserp-result_search-card" data-tracking-client-ingraph data-tracking-will-navigate>
              <span class="sr-only">
            {title}
              </span>
            </a>
            <div class="search-entity-media">
              <img class="artdeco-entity-image artdeco-entity-image--square-4" data-delayed-url="https://media.licdn.com/dms/image/v2/x/company-logo_100_100/0/{id}?e=1" data-ghost-classes="artdeco-entity-image--ghost" alt>
            </div>
            <div class="base-search-card__info">
              <h3 class="base-search-card__title">
            {title}
              </h3>
                <h4 class="base-search-card__subtitle">
              <a class="hidden-nested-link" data-tracking-client-ingraph data-tracking-control-name="public_jobs_jserp-result_job-search-card-subtitle" data-tracking-will-navigate href="https://tr.linkedin.com/company/{companySlug}?trk=public_jobs_jserp-result_job-search-card-subtitle">
                {company}
              </a>
                </h4>
        <!---->
                <div class="base-search-card__metadata">
              <span class="job-search-card__location">
                {location}
              </span>
          <div class="job-posting-benefits text-sm">
            <icon class="job-posting-benefits__icon" data-delayed-url="https://static.licdn.com/aero-v1/sc/h/x" data-svg-class-name="job-posting-benefits__icon-svg"></icon>
            <span class="job-posting-benefits__text">
              Aktif Olarak İşe Alım Yapıyor
        <!---->        </span>
          </div>
              <time class="job-search-card__listdate" datetime="{date}">
                  2 gün önce
              </time>
        <!---->
                </div>
            </div>
        <!---->
          </div>
        </li>
        """;

    public static string SearchPage(params string[] cards) =>
        "<ul>\n" + string.Join("\n", cards) + "\n</ul>\n";

    public static readonly string ThreeCards = SearchPage(
        Card("4460989029", "Software Developer - .NET", "Acme Bankacılık Yazılımları", "acme-bankacilik", "Türkiye", "2026-09-10"),
        Card("4464222112", "Junior–Mid Level C#/.NET Geliştirici", "Kuzey Teknoloji", "kuzey-teknoloji", "İstanbul", "2026-09-11"),
        Card("4463992527", "Senior .NET Developer (Ecom/Marketplace)", "Path &amp; Co", "path-co", "Ataşehir, Istanbul, Türkiye", "2026-09-11"));

    public static string Posting(string descriptionHtml, string seniority = "Mid-Senior level", string employmentType = "Full-time",
        string jobFunction = "Engineering and Information Technology", string industries = "IT Services and IT Consulting") => $"""
        <section class="top-card-layout">
          <div class="topcard__flavor-row">
            <span class="topcard__flavor">
              <a class="topcard__org-name-link topcard__flavor--black-link" href="https://tr.linkedin.com/company/acme-bankacilik?trk=public_jobs_topcard-org-name" rel="noopener" target="_blank">
                Acme Bankacılık Yazılımları
              </a>
            </span>
          </div>
          <span class="num-applicants__caption topcard__flavor--metadata topcard__flavor--bullet">
            Over 200 applicants
          </span>
        </section>
        <div class="core-section-container__content break-words">
          <div class="description__text description__text--rich">
            <section class="show-more-less-html" data-max-lines="5">
              <div class="show-more-less-html__markup show-more-less-html__markup--clamp-after-5
                  relative overflow-hidden">
                {descriptionHtml}
              </div>
              <button class="show-more-less-html__button show-more-less-html__button--more" aria-expanded="false">
                Show more
              </button>
            </section>
          </div>
          <ul class="description__job-criteria-list">
            <li class="description__job-criteria-item">
              <h3 class="description__job-criteria-subheader">
                Seniority level
              </h3>
              <span class="description__job-criteria-text description__job-criteria-text--criteria">
                {seniority}
              </span>
            </li>
            <li class="description__job-criteria-item">
              <h3 class="description__job-criteria-subheader">
                Employment type
              </h3>
              <span class="description__job-criteria-text description__job-criteria-text--criteria">
                {employmentType}
              </span>
            </li>
            <li class="description__job-criteria-item">
              <h3 class="description__job-criteria-subheader">
                Job function
              </h3>
              <span class="description__job-criteria-text description__job-criteria-text--criteria">
                {jobFunction}
              </span>
            </li>
            <li class="description__job-criteria-item">
              <h3 class="description__job-criteria-subheader">
                Industries
              </h3>
              <span class="description__job-criteria-text description__job-criteria-text--criteria">
                {industries}
              </span>
            </li>
          </ul>
        </div>
        """;

    public const string RichDescription = """
        <p>We enable financial institutions to become digital leaders.</p><p><br></p><p>As a professional team of global scale, we work with <strong>best clients</strong> for great &amp; exciting projects.</p><ul><li>.NET 8, C#</li><li>PostgreSQL</li></ul><p>Apply now!</p>
        """;
}
