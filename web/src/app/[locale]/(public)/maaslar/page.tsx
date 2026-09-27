import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { JsonLd } from "@/components/seo/JsonLd";
import { breadcrumbJsonLd, datasetJsonLd, jsonLdGraph, organizationJsonLd } from "@/lib/seo/jsonLd";
import { SALARY_MARKET_PATHS, salaryMarketPath } from "@/lib/salaryMarket/path";
import { fetchSalaryOccupations } from "@/lib/salaryMarket/publicApi.server";
import { surveyCreators, surveyYears } from "@/lib/salaryMarket/surveys";
import { SalaryOccupationsTable } from "@/components/salaryMarket/SalaryOccupationsTable";
import { SalarySources } from "@/components/salaryMarket/SalarySections";

/**
 * The occupations list (design canvas 2026-09-27, variant "Liste A"): every occupation with a
 * published year, its latest median and middle half, and the median's history. Rendered on the
 * server with the data in the HTML, like the company pages; a 404 while the SalaryMarket flag is
 * off, which is also why it is listed in the sitemap from the API rather than in PUBLIC_PATHS.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/maaslar">): Promise<Metadata> {
  const { locale } = await params;
  setRequestLocale(locale);
  const data = await fetchSalaryOccupations();
  if (!data) {
    return {};
  }
  const t = await getTranslations("salaryMarket.list");
  const years = surveyYears(data.editions);
  return buildMetadata({
    locale,
    path: SALARY_MARKET_PATHS,
    title: t("metaTitle", { last: years.last }),
    description: t("metaDescription", { first: years.first, last: years.last, count: data.occupations.length }),
  });
}

export default async function SalaryMarketPage({ params }: PageProps<"/[locale]/maaslar">) {
  const { locale } = await params;
  setRequestLocale(locale);
  const data = await fetchSalaryOccupations();
  if (!data || data.occupations.length === 0) {
    notFound();
  }

  const t = await getTranslations("salaryMarket.list");
  const years = surveyYears(data.editions);
  const allYears = Array.from({ length: years.last - years.first + 1 }, (_, i) => years.first + i);
  const byYear = allYears.map((year) => ({
    year,
    responses: data.editions.filter((e) => e.year === year).reduce((sum, e) => sum + e.responses, 0),
  }));
  const maxResponses = Math.max(...byYear.map((y) => y.responses), 1);
  const format = new Intl.NumberFormat(locale === "tr" ? "tr-TR" : "en-GB");
  const total = data.editions.reduce((sum, e) => sum + e.responses, 0);

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-8 px-4 py-12">
      <JsonLd
        data={jsonLdGraph(
          organizationJsonLd(),
          breadcrumbJsonLd(locale, [{ name: t("breadcrumb"), path: salaryMarketPath(locale) }]),
          datasetJsonLd({
            locale,
            path: salaryMarketPath(locale),
            name: t("title", { first: years.first, last: years.last }),
            description: t("metaDescription", { first: years.first, last: years.last, count: data.occupations.length }),
            creators: surveyCreators(data.editions),
            temporalCoverage: `${years.first}/${years.last}`,
            keywords: t("keywords").split(",").map((k) => k.trim()),
          }),
        )}
      />

      <header className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex max-w-[70ch] flex-col gap-3">
          <p className="text-xs font-semibold tracking-wider text-accent uppercase">{t("eyebrow")}</p>
          <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">
            {t("title", { first: years.first, last: years.last })}
          </h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">{t("lead")}</p>
        </div>
        <dl className="flex gap-3">
          <div className="rounded-xl border border-gray-200 bg-white px-5 py-4 dark:border-gray-800 dark:bg-gray-900">
            <dt className="text-sm text-gray-500 dark:text-gray-400">{t("statResponses", { years: allYears.length })}</dt>
            <dd className="mt-1 text-2xl font-bold text-gray-900 tabular-nums dark:text-gray-100">{format.format(total)}</dd>
          </div>
          <div className="rounded-xl border border-gray-200 bg-white px-5 py-4 dark:border-gray-800 dark:bg-gray-900">
            <dt className="text-sm text-gray-500 dark:text-gray-400">{t("statOccupations")}</dt>
            <dd className="mt-1 text-2xl font-bold text-gray-900 tabular-nums dark:text-gray-100">{data.occupations.length}</dd>
          </div>
        </dl>
      </header>

      <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <section className="flex min-w-0 flex-col gap-3 rounded-xl border border-gray-200 bg-white p-5 sm:p-6 dark:border-gray-800 dark:bg-gray-900">
          <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("participantsTitle")}</h2>
          <ul className="flex h-36 items-end gap-1 sm:gap-4" aria-label={t("participantsTitle")}>
            {byYear.map(({ year, responses }) => (
              <li key={year} className="flex min-w-0 flex-1 flex-col items-center gap-1.5">
                <span className="text-[10px] text-gray-600 tabular-nums sm:text-xs dark:text-gray-400">{responses > 0 ? format.format(responses) : "—"}</span>
                <span aria-hidden="true" className="w-full max-w-6 rounded-t bg-accent" style={{ height: `${Math.max(2, Math.round((responses / maxResponses) * 88))}px` }} />
                <span className="text-[10px] text-gray-500 sm:text-xs dark:text-gray-400">{year}</span>
              </li>
            ))}
          </ul>
        </section>
        <SalarySources editions={data.editions} minimum={data.minimumResponses} />
      </div>

      <SalaryOccupationsTable occupations={data.occupations} years={allYears} />

      <p className="text-sm text-gray-500 dark:text-gray-400">{t("footnote", { minimum: data.minimumResponses })}</p>
    </div>
  );
}
