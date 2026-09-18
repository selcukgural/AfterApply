import { getLocale, getTranslations } from "next-intl/server";
import { formatCount } from "@/lib/dashboard/format";
import { fetchSiteStats } from "@/lib/siteStats/siteStats.server";
import { visibleFigures } from "@/lib/siteStats/figures";

/**
 * "So far: 412 CVs scanned · 131 benchmarks · 58 reviews" — one line under the tools strip
 * (growth audit 2026-09-14, finding 11; decided 2026-09-18, option A). A line and not tiles: a
 * tile makes a number look bigger than it is, and these start small. The API withholds any
 * figure under its threshold, and this renders nothing at all when none is left — the
 * "6 people answered" lesson (finding 10): a small number offered as social proof says the
 * opposite of what it means.
 *
 * Server component, fetched with the page's own revalidation: the figures are running totals,
 * an hour late is fine, and the landing page stays static.
 */
export async function SiteStatsStrip({ heading = false }: { heading?: boolean }) {
  const figures = visibleFigures(await fetchSiteStats());
  if (figures.length === 0) return null;

  const t = await getTranslations("siteStats");
  const locale = await getLocale();

  const line = (
    <p className="flex flex-wrap items-baseline gap-x-6 gap-y-1 text-sm text-gray-600 dark:text-gray-400">
      {!heading ? <span className="text-gray-500 dark:text-gray-500">{t("soFar")}</span> : null}
      {figures.map((figure) => (
        <span key={figure.key}>
          <span className="text-lg font-semibold tabular-nums text-gray-900 dark:text-gray-100">{formatCount(figure.count, locale)}</span>{" "}
          {t(figure.key, { count: figure.count })}
        </span>
      ))}
    </p>
  );

  if (heading) {
    return (
      <section className="flex flex-col gap-2">
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("soFar")}</h2>
        {line}
      </section>
    );
  }

  return (
    <section aria-label={t("soFar")} className="border-t border-gray-200 dark:border-gray-800">
      <div className="mx-auto max-w-6xl px-4 py-5">{line}</div>
    </section>
  );
}
