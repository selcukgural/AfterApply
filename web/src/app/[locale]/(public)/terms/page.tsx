import type { Metadata } from "next";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Link } from "@/i18n/navigation";

export async function generateMetadata({ params }: PageProps<"/[locale]/terms">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/terms", "terms");
}

const LISTS = {
  service: ["tracking", "tools", "reviews", "salaries", "experiences"],
  account: ["age", "accuracy", "security", "delete"],
  reviews: ["ownExperience", "structured", "onePerCompany", "publication", "noManipulation", "opinion", "companies"],
  salaries: ["ownData", "accurate", "signedInOnly", "quota", "opinion"],
  experiences: ["ownProcess", "structured", "onePerCompany", "publication", "noManipulation", "opinion"],
  content: ["ownership", "licence", "ourContent"],
  prohibited: ["scraping", "limits", "abuse", "security", "unlawful"],
  liability: ["noAdvice", "thirdParties", "availability", "reviewsDisclaimer", "salariesDisclaimer", "offerCompareDisclaimer", "experiencesDisclaimer"],
  termination: ["us", "you"],
} as const;

/**
 * The terms of use. Deliberately the same shape as the privacy policy: one plain page, section
 * ids for deep links, the date at the top. Every rule here is one the product already enforces —
 * the one-review-per-company limit, the quota, publication on save, the report path — so the
 * page describes the code rather than promising something it does not do.
 */
export default async function TermsPage({ params }: PageProps<"/[locale]/terms">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("terms");

  const heading = "mb-2 text-base font-semibold text-gray-900 dark:text-gray-100";
  const list = "mt-2 flex list-disc flex-col gap-1 pl-5";

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="mb-2 text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <p className="mb-8 text-sm text-gray-500 dark:text-gray-400">{t("lastUpdated")}</p>

      <div className="flex flex-col gap-8 text-sm leading-6 text-gray-700 dark:text-gray-300">
        <p>{t("intro")}</p>

        <section id="service">
          <h2 className={heading}>{t("service.title")}</h2>
          <p>{t("service.intro")}</p>
          <ul className={list}>
            {LISTS.service.map((item) => (
              <li key={item}>{t(`service.${item}`)}</li>
            ))}
          </ul>
          <p className="mt-2">{t("service.asIs")}</p>
        </section>

        <section id="account">
          <h2 className={heading}>{t("account.title")}</h2>
          <ul className={list}>
            {LISTS.account.map((item) => (
              <li key={item}>{t(`account.${item}`)}</li>
            ))}
          </ul>
        </section>

        <section id="company-reviews">
          <h2 className={heading}>{t("reviews.title")}</h2>
          <p>{t("reviews.intro")}</p>
          <ul className={list}>
            {LISTS.reviews.map((item) => (
              <li key={item}>{t(`reviews.${item}`)}</li>
            ))}
          </ul>
        </section>

        <section id="company-salaries">
          <h2 className={heading}>{t("salaries.title")}</h2>
          <p>{t("salaries.intro")}</p>
          <ul className={list}>
            {LISTS.salaries.map((item) => (
              <li key={item}>{t(`salaries.${item}`)}</li>
            ))}
          </ul>
        </section>

        <section id="candidate-experiences">
          <h2 className={heading}>{t("experiences.title")}</h2>
          <p>{t("experiences.intro")}</p>
          <ul className={list}>
            {LISTS.experiences.map((item) => (
              <li key={item}>{t(`experiences.${item}`)}</li>
            ))}
          </ul>
        </section>

        <section id="content">
          <h2 className={heading}>{t("content.title")}</h2>
          <ul className={list}>
            {LISTS.content.map((item) => (
              <li key={item}>{t(`content.${item}`)}</li>
            ))}
          </ul>
        </section>

        <section id="prohibited">
          <h2 className={heading}>{t("prohibited.title")}</h2>
          <p>{t("prohibited.intro")}</p>
          <ul className={list}>
            {LISTS.prohibited.map((item) => (
              <li key={item}>{t(`prohibited.${item}`)}</li>
            ))}
          </ul>
        </section>

        <section id="liability">
          <h2 className={heading}>{t("liability.title")}</h2>
          <ul className={list}>
            {LISTS.liability.map((item) => (
              <li key={item}>{t(`liability.${item}`)}</li>
            ))}
          </ul>
        </section>

        <section id="termination">
          <h2 className={heading}>{t("termination.title")}</h2>
          <ul className={list}>
            {LISTS.termination.map((item) => (
              <li key={item}>{t(`termination.${item}`)}</li>
            ))}
          </ul>
        </section>

        <section id="changes">
          <h2 className={heading}>{t("changes.title")}</h2>
          <p>{t("changes.body")}</p>
        </section>

        <section id="law">
          <h2 className={heading}>{t("law.title")}</h2>
          <p>{t("law.body")}</p>
        </section>

        <section id="contact">
          <h2 className={heading}>{t("contact.title")}</h2>
          <p>
            {t("contact.before")}{" "}
            <a href="mailto:privacy@ekariyerim.com" className="text-blue-600 hover:underline dark:text-blue-400">
              privacy@ekariyerim.com
            </a>
            {t("contact.after")}
          </p>
          <p className="mt-2">
            <Link href="/privacy" className="text-blue-600 hover:underline dark:text-blue-400">
              {t("privacyLink")}
            </Link>
          </p>
        </section>
      </div>
    </div>
  );
}
