import type { Metadata } from "next";
import { getTranslations, setRequestLocale } from "next-intl/server";
import { notFound } from "next/navigation";
import { Link } from "@/i18n/navigation";
import { buildMetadata } from "@/lib/seo/pageMetadata";
import { JsonLd } from "@/components/seo/JsonLd";
import { breadcrumbJsonLd, jsonLdGraph } from "@/lib/seo/jsonLd";
import { salaryMarketPath, salaryOccupationPath, salaryOccupationPaths } from "@/lib/salaryMarket/path";
import { fetchSalaryOccupation } from "@/lib/salaryMarket/publicApi.server";
import { formatChange, formatStat, percentChange } from "@/lib/salaryMarket/chart";
import { offerComparePath } from "@/lib/offerCompare/path";
import { SalaryTrendChart } from "@/components/salaryMarket/SalaryTrendChart";
import { ExperienceBars, LevelCards, SalarySources, SalaryYearsTable } from "@/components/salaryMarket/SalarySections";

/**
 * One occupation (design canvas 2026-09-27, variant "Detay A"): every survey year's median and
 * middle half on one chart, the latest year by experience and by level, and every figure again as
 * a table. One page per occupation, not per year: 24 × 9 near-identical pages would be the
 * templated mass Google's spam policy names (DECISIONS.md 2026-09-27). An unknown slug, one with
 * no year at or above the threshold, or the flag being off are all a 404 from the API.
 */
export async function generateMetadata({ params }: PageProps<"/[locale]/maaslar/[slug]">): Promise<Metadata> {
  const { locale, slug } = await params;
  setRequestLocale(locale);
  const occupation = await fetchSalaryOccupation(slug);
  if (!occupation) {
    return {};
  }
  const t = await getTranslations("salaryMarket.detail");
  const latest = occupation.years[occupation.years.length - 1];
  const name = locale === "tr" ? occupation.nameTr : occupation.nameEn;
  return buildMetadata({
    locale,
    path: salaryOccupationPaths(slug),
    title: t("metaTitle", { occupation: name, year: latest.year }),
    description: t("metaDescription", {
      occupation: name,
      year: latest.year,
      median: formatStat(latest.overall, "p50", locale),
      p25: formatStat(latest.overall, "p25", locale),
      p75: formatStat(latest.overall, "p75", locale),
      count: latest.overall.count,
    }),
    kicker: t("kicker"),
  });
}

export default async function SalaryOccupationPage({ params }: PageProps<"/[locale]/maaslar/[slug]">) {
  const { locale, slug } = await params;
  setRequestLocale(locale);
  const occupation = await fetchSalaryOccupation(slug);
  if (!occupation) {
    notFound();
  }

  const t = await getTranslations("salaryMarket.detail");
  const tList = await getTranslations("salaryMarket.list");
  const name = locale === "tr" ? occupation.nameTr : occupation.nameEn;
  const latest = occupation.years[occupation.years.length - 1];
  const previous = occupation.years.find((y) => y.year === latest.year - 1);
  const change = percentChange(latest.overall.p50, previous?.overall.p50);
  const totalResponses = occupation.years.reduce((sum, y) => sum + y.overall.count, 0);
  const format = new Intl.NumberFormat(locale === "tr" ? "tr-TR" : "en-GB");
  const tile = "rounded-xl border border-gray-200 bg-white px-4 py-3 dark:border-gray-800 dark:bg-gray-900";

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-8 px-4 py-12">
      <JsonLd
        data={jsonLdGraph(
          breadcrumbJsonLd(locale, [
            { name: tList("breadcrumb"), path: salaryMarketPath(locale) },
            { name, path: salaryOccupationPath(locale, slug) },
          ]),
        )}
      />

      <nav aria-label={t("breadcrumbLabel")} className="text-sm text-gray-500 dark:text-gray-400">
        <Link href={salaryMarketPath(locale)} className="underline-offset-2 hover:underline">
          {tList("breadcrumb")}
        </Link>
        <span aria-hidden="true"> › </span>
        <span className="text-gray-700 dark:text-gray-300">{name}</span>
      </nav>

      <header className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex max-w-[68ch] flex-col gap-3">
          <h1 className="text-3xl font-semibold tracking-tight text-gray-900 sm:text-4xl dark:text-gray-100">{t("title", { occupation: name })}</h1>
          <p className="text-lg text-gray-600 dark:text-gray-400">
            {t.rich("lead", {
              total: format.format(totalResponses),
              first: occupation.years[0].year,
              year: latest.year,
              median: formatStat(latest.overall, "p50", locale),
              p25: formatStat(latest.overall, "p25", locale),
              p75: formatStat(latest.overall, "p75", locale),
              strong: (chunks) => <strong className="font-semibold text-gray-900 dark:text-gray-100">{chunks}</strong>,
            })}
          </p>
        </div>
        <dl className="flex flex-wrap gap-3 lg:shrink-0 lg:flex-nowrap">
          <div className={tile}>
            <dt className="text-sm text-gray-500 dark:text-gray-400">{t("statMedian", { year: latest.year })}</dt>
            <dd className="text-2xl font-bold text-gray-900 tabular-nums dark:text-gray-100">{formatStat(latest.overall, "p50", locale)}</dd>
          </div>
          {change !== null && (
            <div className={tile}>
              <dt className="text-sm text-gray-500 dark:text-gray-400">{t("statChange")}</dt>
              <dd className="text-2xl font-bold text-good-ink tabular-nums">{formatChange(change, locale)}</dd>
            </div>
          )}
          <div className={tile}>
            <dt className="text-sm text-gray-500 dark:text-gray-400">{t("statResponses", { year: latest.year })}</dt>
            <dd className="text-2xl font-bold text-gray-900 tabular-nums dark:text-gray-100">{format.format(latest.overall.count)}</dd>
          </div>
        </dl>
      </header>

      <SalaryTrendChart years={occupation.years} />

      <div className="grid items-start gap-5 lg:grid-cols-2">
        <ExperienceBars year={latest} />
        <div className="flex flex-col gap-5">
          <LevelCards year={latest} />
          <p className="rounded-xl border border-gray-200 bg-white p-4 text-sm leading-relaxed text-gray-700 dark:border-gray-800 dark:bg-gray-900 dark:text-gray-300">
            {t("offerCompare")}{" "}
            <Link href={offerComparePath(locale)} className="font-medium text-accent-ink underline-offset-2 hover:underline">
              {t("offerCompareLink")}
            </Link>
          </p>
        </div>
      </div>

      <section className="flex flex-col gap-3">
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("tableTitle")}</h2>
        <SalaryYearsTable years={occupation.years} />
      </section>

      <SalarySources editions={occupation.editions} minimum={occupation.minimumResponses} />

      <Link href={salaryMarketPath(locale)} className="w-fit text-sm font-medium text-accent-ink underline-offset-2 hover:underline">
        {t("allOccupations")}
      </Link>
    </div>
  );
}
