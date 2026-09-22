"use client";

import { useLocale, useTranslations } from "next-intl";
import { formatCount } from "@/lib/dashboard/format";
import type { SurveyProgress, SurveyStep } from "@/lib/benchmark/survey";
import type { BenchmarkSector } from "@/types/api";

/**
 * The survey framing of a result that cannot yet be compared within its own sector (growth
 * research 2026-09-21, item 0.1; canvas variant B, 2026-09-22). Below the threshold the page used
 * to answer with a single amber "too early" — accurate, and read by a stranger as "nothing here".
 * The same numbers are now shown as a participation: your place in the survey, and what opens next.
 *
 * Presentation only. Every figure comes from `surveyProgress`, which only rearranges what the API
 * returned; no threshold is lowered and no median is shown that the API did not send.
 */

export function BenchmarkParticipantCard({ participant }: { participant: number }) {
  const t = useTranslations("benchmark.survey");
  const locale = useLocale();

  return (
    <div className="rounded-xl border border-accent/30 bg-accent-wash p-6">
      <p className="text-sm text-accent-ink">{t("participantLabel")}</p>
      <p className="mt-1 text-5xl font-semibold tracking-tight text-accent-ink tabular-nums">
        {t("participantValue", { number: formatCount(participant, locale) })}
      </p>
      <p className="mt-2 text-sm text-accent-ink">{t("participantNote")}</p>
    </div>
  );
}

interface StepsProps {
  progress: SurveyProgress;
  sector: BenchmarkSector;
}

export function BenchmarkSurveySteps({ progress, sector }: StepsProps) {
  const t = useTranslations("benchmark");
  const locale = useLocale();
  const { overall } = progress;
  const minimum = formatCount(overall.minimum, locale);
  const sectorName = t(`sectors.${sector}`);

  return (
    <div className="flex flex-col gap-5 rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-900">
      {overall.reached ? (
        // The overall median is already on the page in its own card; this panel is only what is left.
        <p className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("survey.nextTitle")}</p>
      ) : (
        <div className="flex flex-col gap-1">
          <p className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("survey.earlyTitle")}</p>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("survey.earlyBody", { minimum })}</p>
        </div>
      )}

      <ol className="flex flex-col gap-5">
        {overall.reached ? (
          <li className="flex items-start gap-3.5">
            <span className="flex size-7 shrink-0 items-center justify-center rounded-full bg-accent text-white" aria-hidden="true">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round">
                <polyline points="20 6 9 17 4 12" />
              </svg>
            </span>
            <div className="flex flex-1 flex-wrap justify-between gap-x-3 pt-1 text-sm">
              <span className="font-medium text-gray-900 dark:text-gray-100">{t("survey.overallStepDone")}</span>
              <span className="text-gray-600 tabular-nums dark:text-gray-400">
                {t("survey.answers", { count: formatCount(overall.count, locale), n: overall.count })}
              </span>
            </div>
          </li>
        ) : (
          <StepRow
            number={1}
            active
            title={t("survey.overallStep")}
            hint={t("survey.overallStepHint", { minimum })}
            step={overall}
          />
        )}

        <StepRow
          number={2}
          active={overall.reached}
          title={t("survey.sectorStep", { sector: sectorName })}
          hint={t("survey.sectorStepHint", { minimum })}
          step={progress.sector}
        />

        <li className="flex items-start gap-3.5">
          <StepNumber number={3} active={false} />
          <div className="flex flex-1 flex-col gap-1">
            <span className="text-sm font-medium text-gray-900 dark:text-gray-100">{t("survey.reportStep")}</span>
            <span className="text-sm text-gray-500 dark:text-gray-400">{t("survey.reportStepHint")}</span>
          </div>
        </li>
      </ol>
    </div>
  );
}

function StepNumber({ number, active }: { number: number; active: boolean }) {
  return (
    <span
      className={`flex size-7 shrink-0 items-center justify-center rounded-full border-2 text-xs font-semibold ${
        active ? "border-accent text-accent" : "border-gray-300 text-gray-500 dark:border-gray-700 dark:text-gray-400"
      }`}
      aria-hidden="true"
    >
      {number}
    </span>
  );
}

interface StepRowProps {
  number: number;
  /** The step currently being filled — drawn in the accent; the ones after it stay grey. */
  active: boolean;
  title: string;
  hint: string;
  step: SurveyStep;
}

function StepRow({ number, active, title, hint, step }: StepRowProps) {
  const t = useTranslations("benchmark.survey");
  const locale = useLocale();
  const progress = t("progress", {
    count: formatCount(step.count, locale),
    minimum: formatCount(step.minimum, locale),
  });

  return (
    <li className="flex items-start gap-3.5">
      <StepNumber number={number} active={active} />
      <div className="flex flex-1 flex-col gap-1.5">
        <div className="flex flex-wrap justify-between gap-x-3 text-sm">
          <span className="font-medium text-gray-900 dark:text-gray-100">{title}</span>
          <span className="text-gray-600 tabular-nums dark:text-gray-400">{progress}</span>
        </div>
        <div
          role="progressbar"
          aria-label={title}
          aria-valuemin={0}
          aria-valuemax={step.minimum}
          aria-valuenow={Math.min(step.count, step.minimum)}
          aria-valuetext={progress}
          className={`h-1.5 overflow-hidden rounded-full ${active ? "bg-accent-wash" : "bg-gray-100 dark:bg-gray-800"}`}
        >
          <div
            className={`h-full rounded-full ${active ? "bg-accent" : "bg-accent/40"}`}
            style={{ width: `${step.percent}%` }}
          />
        </div>
        <span className="text-sm text-gray-500 dark:text-gray-400">{hint}</span>
      </div>
    </li>
  );
}
