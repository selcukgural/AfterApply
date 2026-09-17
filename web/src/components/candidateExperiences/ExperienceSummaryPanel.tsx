"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { CandidateExperienceSummary, ExperienceStatementCount, ReviewStatementKind } from "@/types/api";
import { distributionFractions, formatScore } from "@/lib/companyReviews/score";
import { EXPERIENCE_CATALOGUE } from "@/lib/candidateExperiences/statementCatalogue";
import { categoryMessageKey } from "@/lib/statements/catalogue";
import { StarRating } from "@/components/companyReviews/StarRating";
import { StatementTag } from "@/components/companyReviews/StatementChip";

/**
 * The aggregate of a company's candidate experiences (design canvas "Sekme 1", 2026-09-17): the
 * review summary panel's twin on top — score or the gap to it, the rated areas, the
 * distribution, the two "most picked" lists — and a process strip under it: how the processes
 * ended, how long they typically took, how many rounds, how many asked for an assignment. Every
 * figure but the count and the distribution waits for the threshold, and the sample size sits
 * next to every number.
 */
export function ExperienceSummaryPanel({ summary }: { summary: CandidateExperienceSummary }) {
  const t = useTranslations("candidateExperiences.summary");
  const tScoring = useTranslations("companies.scoring");
  const tCategories = useTranslations("candidateExperiences.categories");
  const tStatements = useTranslations("candidateExperiences.statements");
  const tDuration = useTranslations("processDuration");
  const tStages = useTranslations("stageCount");
  const tTypes = useTranslations("interviewType");
  const locale = useLocale();
  const fractions = distributionFractions(summary.distribution);
  const remaining = Math.max(0, summary.minimumForStats - summary.count);
  const categories = summary.categories.filter((c) => c.count > 0);
  const atThreshold = summary.count > 0 && summary.count >= summary.minimumForStats;
  const outcomeTotal = summary.outcomes.reduce((sum, o) => sum + o.count, 0);

  const topList = (items: ExperienceStatementCount[], kind: ReviewStatementKind, heading: string, tone: string) =>
    items.length === 0 ? null : (
      <div className="flex flex-col gap-1.5">
        <span className={`text-[11px] font-semibold uppercase tracking-wide ${tone}`}>{heading}</span>
        <ul className="flex flex-wrap gap-1.5">
          {items.flatMap(({ key, count }) => {
            if (!EXPERIENCE_CATALOGUE.find(key)) return [];
            return [
              <li key={key}>
                <StatementTag
                  label={tStatements(`${key}.label`)}
                  sentence={tStatements(`${key}.sentence`)}
                  kind={kind}
                  suffix={`· ${count}`}
                />
              </li>,
            ];
          })}
        </ul>
      </div>
    );

  return (
    <div className="flex flex-col gap-4">
      <section className="grid gap-6 rounded-xl border border-gray-200 bg-white p-5 sm:grid-cols-[auto_1fr] dark:border-gray-800 dark:bg-gray-900">
        <div className="flex flex-col items-start gap-1">
          {summary.score !== null ? (
            <>
              <span className="text-5xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
                {formatScore(summary.score, locale)}
              </span>
              <StarRating value={summary.score} label={t("scoreLabel", { score: formatScore(summary.score, locale) })} size="lg" />
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("basedOn", { count: summary.count })}</span>
            </>
          ) : (
            <>
              <span className="text-2xl font-semibold text-gray-900 dark:text-gray-100">{t("noScoreYet")}</span>
              {summary.count > 0 ? (
                <span className="max-w-[28ch] text-xs text-gray-500 dark:text-gray-400">
                  {t("untilStats", { count: summary.count, remaining })}
                </span>
              ) : null}
            </>
          )}
          <Link href="/companies/scoring" className="mt-1 text-xs text-accent-ink underline-offset-2 hover:underline">
            {tScoring("linkLabel")}
          </Link>
        </div>

        <div className="flex flex-col gap-4">
          {categories.length > 0 ? (
            <dl className="grid gap-x-6 gap-y-1.5 text-sm sm:grid-cols-2" aria-label={t("categoriesLabel")}>
              {categories.map(({ category, count, average }) => {
                const label = tCategories(categoryMessageKey(category));
                return (
                  <div key={category} className="flex items-center justify-between gap-3">
                    <dt className="text-gray-600 dark:text-gray-400">{label}</dt>
                    <dd className="flex items-center gap-2 whitespace-nowrap">
                      {average === null ? (
                        <>
                          <span className="text-gray-400">—</span>
                          <span className="text-xs text-gray-400">{t("votes", { count })}</span>
                        </>
                      ) : (
                        <>
                          <StarRating value={average} label={`${label}: ${formatScore(average, locale)}`} />
                          <span className="w-7 text-right font-medium text-gray-900 dark:text-gray-100">{formatScore(average, locale)}</span>
                        </>
                      )}
                    </dd>
                  </div>
                );
              })}
            </dl>
          ) : null}

          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5" aria-label={t("distributionLabel")}>
              {[5, 4, 3, 2, 1].map((star) => (
                <div key={star} className="flex items-center gap-2 text-xs text-gray-600 dark:text-gray-400">
                  <span className="w-3 text-right">{star}</span>
                  <span className="h-2 flex-1 overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
                    <span className="block h-full rounded-full bg-amber-500" style={{ width: `${fractions[star - 1] * 100}%` }} />
                  </span>
                  <span className="w-6 text-right tabular-nums">{summary.distribution[star - 1] ?? 0}</span>
                </div>
              ))}
            </div>
            {summary.topLiked.length + summary.topImprovable.length > 0 ? (
              <div className="flex flex-col gap-3">
                {topList(summary.topLiked, "Liked", t("topLiked"), "text-good-ink")}
                {topList(summary.topImprovable, "Improve", t("topImprovable"), "text-warn-ink")}
              </div>
            ) : null}
          </div>
        </div>
      </section>

      {atThreshold ? (
        <section
          aria-label={t("processHeading")}
          className="grid gap-5 rounded-xl border border-gray-200 bg-white p-5 sm:grid-cols-2 lg:grid-cols-[1.6fr_1fr_1fr_1.2fr] dark:border-gray-800 dark:bg-gray-900"
        >
          <div className="flex flex-col gap-2">
            <span className="text-xs text-gray-500 dark:text-gray-400">{t("outcomes")}</span>
            {summary.outcomes.length === 0 ? (
              <span className="text-sm text-gray-400">{t("notEnough")}</span>
            ) : (
              <ul className="flex flex-col gap-1">
                {summary.outcomes.map(({ outcome, count }) => (
                  <li key={outcome} className="flex items-center gap-2 text-xs text-gray-700 dark:text-gray-300">
                    <span className="w-24 shrink-0 truncate">{t(`outcomeShort.${outcome}`)}</span>
                    <span className="h-2 flex-1 overflow-hidden rounded-full bg-gray-100 dark:bg-gray-800">
                      <span
                        className={`block h-full rounded-full ${outcome === "NoResponse" ? "bg-crit" : outcome === "Offer" ? "bg-good" : "bg-muted"}`}
                        style={{ width: `${outcomeTotal === 0 ? 0 : (count / outcomeTotal) * 100}%` }}
                      />
                    </span>
                    <span className="w-5 text-right tabular-nums">{count}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <Stat label={t("typicalDuration")} value={summary.typicalDuration ? tDuration(summary.typicalDuration) : t("notEnough")} hint={t("median")} />
          <Stat label={t("typicalStages")} value={summary.typicalStages ? tStages(summary.typicalStages) : t("notEnough")} hint={t("median")} />
          <div className="flex flex-col gap-2">
            <Stat label={t("takeHome")} value={t("takeHomeValue", { count: summary.takeHomeAssignmentCount, total: summary.count })} />
            {summary.interviewTypes.length > 0 ? (
              <div className="flex flex-col gap-1">
                <span className="text-xs text-gray-500 dark:text-gray-400">{t("interviewTypes")}</span>
                <ul className="flex flex-wrap gap-1">
                  {summary.interviewTypes.map(({ type, count }) => (
                    <li key={type} className="rounded-full border border-gray-300 px-2 py-0.5 text-[11px] text-gray-700 dark:border-gray-700 dark:text-gray-300">
                      {tTypes(type)} · {count}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </div>
        </section>
      ) : null}
    </div>
  );
}

function Stat({ label, value, hint }: { label: string; value: string; hint?: string }) {
  return (
    <div className="flex flex-col gap-1">
      <span className="text-xs text-gray-500 dark:text-gray-400">{label}</span>
      <span className="text-xl font-semibold text-gray-900 dark:text-gray-100">{value}</span>
      {hint ? <span className="text-[11px] text-gray-400">{hint}</span> : null}
    </div>
  );
}
