import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { Link } from "@/i18n/navigation";
import { pageMetadata } from "@/lib/seo/pageMetadata";
import { buttonClassName } from "@/components/ui/Button";
import { SectorResponseRatesTable } from "@/components/responseRates/SectorResponseRatesTable";

// The thresholds the copy quotes. The table itself prints the server's live values beside the
// rows; these are the shipped defaults (ResponseRateOptions / CompanyIntelligenceOptions), and
// the "Method" section states them the same way the benchmark page states its own.
const DEFAULTS = { months: 12, contributors: 5, applications: 30, maturityDays: 30, sharePercent: 50, companyFloor: 50 };

/**
 * "Which sectors actually reply?" — the public, company-less half of the response-rate data
 * (design canvas 2026-09-21, direction "tablo"). Same shared, untranslated segment as /benchmark
 * for the same reason; the search terms live in the title and heading. The shell is static and
 * indexable; the table is a client query so the period switch is not a navigation.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/response-rates">): Promise<Metadata> {
  const { locale } = await params;
  return pageMetadata(locale, "/response-rates", "responseRates");
}

export default async function ResponseRatesPage({ params }: PageProps<"/[locale]/response-rates">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const t = await getTranslations("responseRates");
  const method = ["source", "sector", "window", "maturity", "threshold", "definitions"] as const;

  return (
    <div className="mx-auto flex w-full max-w-5xl flex-col gap-10 px-4 py-12">
      <header className="flex max-w-[70ch] flex-col gap-3">
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">{t("title")}</h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">
          {t("lead", { months: DEFAULTS.months, contributors: DEFAULTS.contributors, applications: DEFAULTS.applications })}
        </p>
      </header>

      <SectorResponseRatesTable />

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-gray-50 p-5 dark:border-gray-800 dark:bg-gray-950">
          <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("benchmark.title")}</h2>
          <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {t("benchmark.body")}{" "}
            <Link href="/benchmark" className="text-accent-ink underline-offset-2 hover:underline">
              {t("benchmark.link")}
            </Link>
          </p>
        </div>
        <div className="flex flex-col gap-2 rounded-xl border border-gray-200 bg-gray-50 p-5 dark:border-gray-800 dark:bg-gray-950">
          <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("companies.title")}</h2>
          <p className="text-sm leading-relaxed text-gray-600 dark:text-gray-400">
            {t("companies.body", { floor: DEFAULTS.companyFloor })}{" "}
            <a href="#method" className="text-accent-ink underline-offset-2 hover:underline">
              {t("companies.link")}
            </a>
          </p>
        </div>
      </div>

      {/* The same shape as the benchmark's method section: what someone reads once they have seen
          a number and are deciding whether to believe it. Every item is a rule the server keeps. */}
      <section id="method" className="flex flex-col gap-3 border-t border-gray-200 pt-8 dark:border-gray-800">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("method.title")}</h2>
        <ul className="flex list-disc flex-col gap-2 pl-5 text-sm text-gray-600 dark:text-gray-400">
          {method.map((item) => (
            <li key={item}>
              {t(`method.items.${item}`, {
                days: DEFAULTS.maturityDays,
                contributors: DEFAULTS.contributors,
                applications: DEFAULTS.applications,
                share: DEFAULTS.sharePercent,
              })}
            </li>
          ))}
        </ul>
      </section>

      <section className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white p-6 sm:flex-row sm:items-center sm:justify-between dark:border-gray-800 dark:bg-gray-900">
        <div className="flex flex-col gap-1">
          <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("cta.title")}</h2>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("cta.body")}</p>
        </div>
        <Link href="/register" className={buttonClassName("primary", "inline-flex w-fit shrink-0")}>
          {t("cta.button")}
        </Link>
      </section>
    </div>
  );
}
