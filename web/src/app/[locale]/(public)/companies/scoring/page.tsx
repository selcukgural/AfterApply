import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { fetchCompanyReviewsConfig } from "@/lib/companies/publicApi.server";

/**
 * How a company's score is calculated, and what a review may and may not contain. Public and
 * linked from every score: a number nobody can check is a number nobody should trust. The two
 * parameters are read from the live configuration so the text never quotes a stale copy.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/companies/scoring">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/companies/scoring", "companyScoring");
}

const DEFAULT_PRIOR_WEIGHT = 5;
const DEFAULT_MINIMUM = 3;

export default async function CompanyScoringPage() {
  const t = await getTranslations("companies.scoring");
  const tPage = await getTranslations("companies.page");
  const config = await fetchCompanyReviewsConfig();
  const m = config?.priorWeight ?? DEFAULT_PRIOR_WEIGHT;
  const minimum = config?.minimumReviewsForScore ?? DEFAULT_MINIMUM;

  return (
    <div className="mx-auto max-w-2xl px-4 py-12">
      <h1 className="mb-2 text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <p className="mb-8 text-sm text-gray-500 dark:text-gray-400">{t("intro")}</p>

      <div className="flex flex-col gap-8 text-sm leading-6 text-gray-700 dark:text-gray-300">
        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("formula.title")}</h2>
          <p>{t("formula.body", { m, minimum })}</p>
          <pre className="my-3 overflow-x-auto rounded-lg bg-gray-100 p-3 text-sm dark:bg-gray-800">
            {`score = (n × avg + ${m} × siteAvg) / (n + ${m})`}
          </pre>
          <ul className="list-disc flex flex-col gap-1 pl-5">
            <li>{t("formula.n")}</li>
            <li>{t("formula.avg")}</li>
            <li>{t("formula.siteAvg")}</li>
            <li>{t("formula.m", { m })}</li>
          </ul>
          <p className="mt-2">{t("formula.example", { m })}</p>
          <p className="mt-2">{t("formula.threshold", { minimum })}</p>
          <p className="mt-2">{t("formula.categories")}</p>
          {!config && <p className="mt-2 text-xs text-gray-500 dark:text-gray-400">{t("formula.defaultsNote")}</p>}
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("moderation.title")}</h2>
          <ul className="list-disc flex flex-col gap-1 pl-5">
            <li>{t("moderation.preModeration")}</li>
            <li>{t("moderation.onePerCompany")}</li>
            <li>{t("moderation.editResets")}</li>
            <li>{t("moderation.reports")}</li>
            <li>{t("moderation.anonymity")}</li>
          </ul>
        </section>

        <section>
          <h2 className="mb-2 text-base font-semibold text-gray-900 dark:text-gray-100">{t("guidelines.title")}</h2>
          <ul className="list-disc flex flex-col gap-1 pl-5">
            <li>{t("guidelines.ownExperience")}</li>
            <li>{t("guidelines.noNames")}</li>
            <li>{t("guidelines.noInsults")}</li>
            <li>{t("guidelines.noClaims")}</li>
            <li>{t("guidelines.noAds")}</li>
          </ul>
        </section>

        <p>
          <Link href="/companies" className="text-accent-ink underline-offset-2 hover:underline">
            {tPage("allCompanies")}
          </Link>
        </p>
      </div>
    </div>
  );
}
