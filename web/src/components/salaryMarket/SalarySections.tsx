import { getLocale, getTranslations } from "next-intl/server";
import type { SalaryExperience, SalaryOccupationYear, SalarySurveyEdition } from "@/types/api";
import { formatChange, formatStat, percentChange, rangeBar } from "@/lib/salaryMarket/chart";
import { LEVEL_STYLES } from "@/lib/salaryMarket/levelStyles";
import { safeExternalUrl } from "@/lib/url/externalLink";

const EXPERIENCE_ORDER: readonly SalaryExperience[] = ["ZeroToTwo", "ThreeToFive", "SixToTen", "TenPlus"];

const card = "flex flex-col gap-4 rounded-xl border border-gray-200 bg-white p-5 sm:p-6 dark:border-gray-800 dark:bg-gray-900";

/** The latest year by experience range: the middle half as a bar, the median as a tick and a number. */
export async function ExperienceBars({ year }: { year: SalaryOccupationYear }) {
  const t = await getTranslations("salaryMarket.detail");
  const tExperience = await getTranslations("salaryMarket.experience");
  const locale = await getLocale();
  const rows = EXPERIENCE_ORDER.map((key) => year.experience.find((e) => e.experience === key)).filter((row) => row !== undefined);
  const max = Math.max(...rows.map((row) => row.stats.p75), 1) * 1.1;

  return (
    <section className={card}>
      <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("byExperience", { year: year.year })}</h2>
      {rows.length === 0 ? (
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("notEnoughSlices")}</p>
      ) : (
        <ul className="flex flex-col gap-3">
          {rows.map((row) => {
            const bar = rangeBar(row.stats, max);
            const range = t("rangeTitle", { p25: formatStat(row.stats, "p25", locale), p75: formatStat(row.stats, "p75", locale), count: row.stats.count });
            return (
              <li key={row.experience} className="grid grid-cols-[5.5rem_1fr_6.5rem] items-center gap-3">
                <span className="text-sm text-gray-700 dark:text-gray-300">{tExperience(row.experience)}</span>
                <span className="relative h-5" title={range}>
                  <span aria-hidden="true" className="absolute inset-x-0 top-[9px] h-0.5 bg-gray-200 dark:bg-gray-800" />
                  <span aria-hidden="true" className="absolute top-1 h-3 rounded bg-accent/40" style={{ left: `${bar.left}%`, width: `${bar.width}%` }} />
                  <span aria-hidden="true" className="absolute top-0 h-5 w-[3px] rounded-sm bg-accent-strong" style={{ left: `${bar.median}%` }} />
                  <span className="sr-only">{range}</span>
                </span>
                <span className="text-right text-sm font-semibold text-gray-900 tabular-nums dark:text-gray-100">{formatStat(row.stats, "p50", locale)}</span>
              </li>
            );
          })}
        </ul>
      )}
      <p className="text-xs text-gray-500 dark:text-gray-400">{t("barLegend")}</p>
    </section>
  );
}

/** The latest year by the respondents' own seniority. */
export async function LevelCards({ year }: { year: SalaryOccupationYear }) {
  const t = await getTranslations("salaryMarket.detail");
  const tLevel = await getTranslations("salaryMarket.levels");
  const locale = await getLocale();

  return (
    <section className={card}>
      <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("byLevel", { year: year.year })}</h2>
      {year.levels.length === 0 ? (
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("notEnoughSlices")}</p>
      ) : (
        <ul className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          {year.levels.map(({ level, stats }) => (
            <li key={level} className="flex flex-col gap-1.5 rounded-xl border border-gray-200 p-4 dark:border-gray-800">
              <span className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
                <span aria-hidden="true" className={`h-2.5 w-2.5 rounded-full ${LEVEL_STYLES[level].swatch}`} />
                {tLevel(level)}
              </span>
              <span className="text-xl font-bold text-gray-900 tabular-nums dark:text-gray-100">{formatStat(stats, "p50", locale)}</span>
              <span className="text-xs whitespace-nowrap text-gray-500 tabular-nums dark:text-gray-400">
                {formatStat(stats, "p25", locale)} – {formatStat(stats, "p75", locale)}
              </span>
              <span className="text-sm text-gray-500 dark:text-gray-400">{t("people", { count: stats.count })}</span>
            </li>
          ))}
        </ul>
      )}
      <p className="text-xs text-gray-500 dark:text-gray-400">{t("levelNote")}</p>
    </section>
  );
}

/** Every year's figures as a table: the chart's numbers for anyone who does not read the chart. */
export async function SalaryYearsTable({ years }: { years: readonly SalaryOccupationYear[] }) {
  const t = await getTranslations("salaryMarket.detail.table");
  const locale = await getLocale();
  const rows = [...years].reverse();
  const cell = "px-4 py-2.5 text-right whitespace-nowrap tabular-nums";

  return (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-gray-900">
      <table className="w-full min-w-[36rem] text-sm">
        <caption className="sr-only">{t("caption")}</caption>
        <thead className="bg-gray-50 text-xs font-semibold tracking-wide text-gray-600 uppercase dark:bg-gray-950 dark:text-gray-400">
          <tr>
            <th scope="col" className="px-4 py-3 text-left">{t("year")}</th>
            <th scope="col" className="px-4 py-3 text-right whitespace-nowrap">{t("responses")}</th>
            <th scope="col" className="px-4 py-3 text-right whitespace-nowrap">{t("p25")}</th>
            <th scope="col" className="px-4 py-3 text-right whitespace-nowrap">{t("median")}</th>
            <th scope="col" className="px-4 py-3 text-right whitespace-nowrap">{t("p75")}</th>
            <th scope="col" className="px-4 py-3 text-right whitespace-nowrap">{t("change")}</th>
          </tr>
        </thead>
        <tbody className="text-gray-900 dark:text-gray-100">
          {rows.map((year) => {
            const previous = years.find((y) => y.year === year.year - 1);
            const change = percentChange(year.overall.p50, previous?.overall.p50);
            return (
              <tr key={year.year} className="border-t border-gray-100 dark:border-gray-800">
                <th scope="row" className="px-4 py-2.5 text-left font-semibold">{year.year}</th>
                <td className={`${cell} text-gray-600 dark:text-gray-400`}>{new Intl.NumberFormat(locale === "tr" ? "tr-TR" : "en-GB").format(year.overall.count)}</td>
                <td className={cell}>{formatStat(year.overall, "p25", locale)}</td>
                <td className={`${cell} font-semibold`}>{formatStat(year.overall, "p50", locale)}</td>
                <td className={cell}>{formatStat(year.overall, "p75", locale)}</td>
                <td className={`${cell} text-good-ink`}>{change === null ? "—" : formatChange(change, locale)}</td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

/** Where the numbers come from, named with each survey's size and month, and how they were made. */
export async function SalarySources({ editions, minimum }: { editions: readonly SalarySurveyEdition[]; minimum: number }) {
  const t = await getTranslations("salaryMarket.sources");
  const locale = await getLocale();
  const bySource = new Map<string, SalarySurveyEdition[]>();
  for (const edition of editions) {
    bySource.set(edition.sourceCode, [...(bySource.get(edition.sourceCode) ?? []), edition]);
  }
  const format = new Intl.NumberFormat(locale === "tr" ? "tr-TR" : "en-GB");
  const methods = ["band", "currency", "nominal"] as const;

  return (
    <section id="method" className="flex scroll-mt-24 flex-col gap-3 rounded-xl bg-accent-wash p-5 text-sm leading-relaxed text-gray-800 sm:p-6 dark:text-gray-200">
      <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      <ul className="flex flex-col gap-2">
        {[...bySource.values()].map((list) => {
          const first = list[0];
          const total = list.reduce((sum, e) => sum + e.responses, 0);
          const years = list.map((e) => e.year);
          // The survey author's profile (the repository's parent), through the site's http(s)-only guard.
          const href = safeExternalUrl(first.sourceUrl.replace(/\/[^/]+$/, ""));
          return (
            <li key={first.sourceCode}>
              {t.rich("source", {
                name: first.sourceName,
                first: Math.min(...years),
                last: Math.max(...years),
                count: format.format(total),
                link: (chunks) =>
                  href ? (
                    <a href={href} target="_blank" rel="noopener noreferrer" className="font-medium text-accent-ink underline-offset-2 hover:underline">
                      {chunks}
                    </a>
                  ) : (
                    chunks
                  ),
              })}
            </li>
          );
        })}
      </ul>
      <p>{t("permission")}</p>
      <ul className="flex list-disc flex-col gap-1.5 pl-5 text-gray-700 dark:text-gray-300">
        <li>{t("threshold", { minimum })}</li>
        {methods.map((key) => (
          <li key={key}>{t(`method.${key}`)}</li>
        ))}
      </ul>
    </section>
  );
}

/**
 * The list page's short version of `SalarySources` (canvas variant A, 2026-09-29), at the foot of
 * the page under the table: the threshold and the three method points as a four-column grid, then
 * one line naming each survey with its link and the permission. The long box sat beside the
 * participants chart and ran twice its height; the detail page keeps it.
 */
export async function SalarySourceStrip({ editions, minimum }: { editions: readonly SalarySurveyEdition[]; minimum: number }) {
  const t = await getTranslations("salaryMarket.sources.strip");
  const bySource = new Map<string, SalarySurveyEdition[]>();
  for (const edition of editions) {
    bySource.set(edition.sourceCode, [...(bySource.get(edition.sourceCode) ?? []), edition]);
  }
  const facts = ["threshold", "band", "currency", "nominal"] as const;

  return (
    <section id="method" aria-labelledby="method-title" className="flex scroll-mt-24 flex-col gap-4 border-t border-gray-200 pt-6 dark:border-gray-800">
      <h2 id="method-title" className="text-sm font-semibold text-gray-900 dark:text-gray-100">
        {t("title")}
      </h2>
      <dl className="grid grid-cols-1 gap-x-8 gap-y-4 sm:grid-cols-2 lg:grid-cols-4">
        {facts.map((key) => (
          <div key={key} className="flex flex-col gap-1">
            <dt className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t(`facts.${key}.title`, { minimum })}</dt>
            <dd className="text-sm leading-snug text-gray-600 dark:text-gray-400">{t(`facts.${key}.text`)}</dd>
          </div>
        ))}
      </dl>
      {[...bySource.values()].map((list) => {
        const first = list[0];
        const years = list.map((e) => e.year);
        // The survey author's profile (the repository's parent), through the site's http(s)-only guard.
        const href = safeExternalUrl(first.sourceUrl.replace(/\/[^/]+$/, ""));
        return (
          <p key={first.sourceCode} className="text-sm text-gray-500 dark:text-gray-400">
            {t.rich("source", {
              name: first.sourceName,
              first: Math.min(...years),
              last: Math.max(...years),
              link: (chunks) =>
                href ? (
                  <a href={href} target="_blank" rel="noopener noreferrer" className="font-medium text-accent-ink underline-offset-2 hover:underline">
                    {chunks}
                  </a>
                ) : (
                  chunks
                ),
            })}
          </p>
        );
      })}
    </section>
  );
}
