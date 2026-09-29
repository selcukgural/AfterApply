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
import { SalarySourceStrip } from "@/components/salaryMarket/SalarySections";
import { formatStat } from "@/lib/salaryMarket/chart";
import { safeExternalUrl } from "@/lib/url/externalLink";

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
  // The third headline figure: the highest median of the latest year, with whose it is.
  const top = data.occupations
    .filter((o) => o.latestYear === years.last)
    .reduce<(typeof data.occupations)[number] | null>((best, o) => (best === null || o.latest.p50 > best.latest.p50 ? o : best), null);
  const latestEdition = data.editions.find((e) => e.year === years.last);
  const latestMonth = latestEdition
    ? new Intl.DateTimeFormat(locale === "tr" ? "tr-TR" : "en-GB", { month: "long", year: "numeric", timeZone: "UTC" }).format(
        new Date(`${latestEdition.publishedMonth}-01T00:00:00Z`),
      )
    : null;
  // The survey's author, named and linked in the lead (2026-09-29). One source, so the first is it.
  const author = surveyCreators(data.editions)[0];
  const authorHref = author ? safeExternalUrl(author.url) : null;
  const stat = "flex flex-col gap-1 px-6 py-5 sm:px-8 sm:py-7";
  const statNumber = "text-5xl leading-none font-extrabold tracking-tight tabular-nums sm:text-6xl";

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

      {/* Canvas variant A (2026-09-29): the figures as a band of their own, the chart at full width
          and the source at the foot of the page — the old source box ran twice the chart's height. */}
      <header className="flex max-w-[70ch] flex-col gap-3">
        <p className="text-xs font-semibold tracking-wider text-accent uppercase">{t("eyebrow")}</p>
        <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">
          {t("title", { first: years.first, last: years.last })}
        </h1>
        <p className="text-lg text-gray-600 dark:text-gray-400">
          {t.rich("lead", {
            name: author?.name ?? "",
            link: (chunks) =>
              authorHref ? (
                <a href={authorHref} target="_blank" rel="noopener noreferrer" className="font-medium text-accent-ink underline-offset-2 hover:underline">
                  {chunks}
                </a>
              ) : (
                chunks
              ),
          })}
        </p>
      </header>

      <dl
        aria-label={t("statsLabel")}
        className="grid grid-cols-1 divide-y divide-gray-200 rounded-2xl border border-gray-200 bg-white sm:grid-cols-3 sm:divide-x sm:divide-y-0 dark:divide-gray-800 dark:border-gray-800 dark:bg-gray-900"
      >
        <div className={stat}>
          <dt className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("statResponses")}</dt>
          <dd className={`${statNumber} order-first text-accent`}>{format.format(total)}</dd>
          <dd className="text-sm text-gray-500 dark:text-gray-400">{t("statResponsesNote", { years: allYears.length })}</dd>
        </div>
        <div className={stat}>
          <dt className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("statOccupations")}</dt>
          <dd className={`${statNumber} order-first text-gray-900 dark:text-gray-100`}>{data.occupations.length}</dd>
          <dd className="text-sm text-gray-500 dark:text-gray-400">{t("statOccupationsNote", { minimum: data.minimumResponses })}</dd>
        </div>
        {top && (
          <div className={stat}>
            <dt className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("statTopMedian")}</dt>
            <dd className={`${statNumber} order-first whitespace-nowrap text-gray-900 dark:text-gray-100`}>{formatStat(top.latest, "p50", locale)}</dd>
            <dd className="text-sm text-gray-500 dark:text-gray-400">
              {t("statTopMedianNote", { year: years.last, occupation: locale === "tr" ? top.nameTr : top.nameEn })}
            </dd>
          </div>
        )}
      </dl>

      <section className="flex min-w-0 flex-col gap-4 rounded-2xl border border-gray-200 bg-white p-5 sm:p-7 dark:border-gray-800 dark:bg-gray-900">
        <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
          <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("participantsTitle")}</h2>
          {latestMonth && <p className="text-sm text-gray-500 dark:text-gray-400">{t("latestSurvey", { month: latestMonth })}</p>}
        </div>
        <ul className="flex items-end gap-1.5 sm:gap-4" aria-label={t("participantsTitle")}>
          {byYear.map(({ year, responses }) => (
            <li key={year} className="flex min-w-0 flex-1 flex-col items-center gap-2">
              <span className="text-[11px] font-semibold text-gray-700 tabular-nums sm:text-sm dark:text-gray-300">{responses > 0 ? format.format(responses) : "—"}</span>
              <span
                aria-hidden="true"
                className="w-full max-w-14 rounded-t-md rounded-b-sm bg-accent"
                style={{ height: `${Math.max(4, Math.round((responses / maxResponses) * 170))}px` }}
              />
              <span className="text-[11px] text-gray-500 sm:text-sm dark:text-gray-400">{year}</span>
            </li>
          ))}
        </ul>
      </section>

      <SalaryOccupationsTable occupations={data.occupations} years={allYears} />

      <p className="text-sm text-gray-500 dark:text-gray-400">{t("footnote", { minimum: data.minimumResponses })}</p>

      <SalarySourceStrip editions={data.editions} minimum={data.minimumResponses} />
    </div>
  );
}
