"use client";

import { useId, useState, type FormEvent } from "react";
import { useMutation } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { useClientConfig } from "@/hooks/useClientConfig";
import { silenceReportsApi } from "@/lib/api/silenceReports";
import { ApiError } from "@/lib/api/httpClient";
import {
  EMPTY_SILENCE_REPORT_DRAFT,
  SILENCE_STAGES,
  SILENCE_WAITS,
  buildSilenceReportRequest,
  silenceReportProblems,
  type PromiseAnswer,
  type SilenceReportDraft,
} from "@/lib/companies/silenceReport";
import { Button } from "@/components/ui/Button";
import { FactPills } from "@/components/candidateExperiences/FactPills";

const PILL = "cursor-pointer rounded-lg border px-3 py-1.5 text-sm transition-colors focus-within:ring-2 focus-within:ring-accent";
const IDLE = "border-gray-300 bg-white text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800";
const SELECTED = "border-accent bg-accent-wash font-medium text-accent-ink";

type Step = "closed" | "editing" | "sent";

/**
 * "Did you interview here and then hear nothing?" — the anonymous report under a company page's
 * tabs (growth research 2026-09-21 item 1.6, canvas "Son hâl — C", 2026-09-23). Closed by default,
 * one line and a button; opening it asks the stage, how long the silence has lasted, and whether a
 * reply date had been promised. No free text, no name, no account.
 *
 * Nothing about a single report is ever shown back: the count appears on the company's figures
 * only above its own floor, and only where company figures are enabled. The card says that where
 * the report is given, and points a signed-in tracker user at marking the application instead,
 * which is the better datum (it enters the response rates too).
 */
export function SilenceReportCard({ companySlug }: { companySlug: string }) {
  const t = useTranslations("companies.silenceReport");
  const locale = useLocale();
  const { config } = useClientConfig();
  const [step, setStep] = useState<Step>("closed");
  const [draft, setDraft] = useState<SilenceReportDraft>(EMPTY_SILENCE_REPORT_DRAFT);
  const [problems, setProblems] = useState<string[]>([]);
  const [error, setError] = useState<string | null>(null);

  const submit = useMutation({
    mutationFn: () => silenceReportsApi.submit(companySlug, buildSilenceReportRequest(draft, locale, window.location.search)),
    onSuccess: () => setStep("sent"),
    onError: (err) => setError(err instanceof ApiError ? err.message : t("error")),
  });

  if (config.silenceReports?.enabled !== true) return null;

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    const found = silenceReportProblems(draft);
    setProblems(found);
    if (found.length > 0) return;
    try {
      await submit.mutateAsync();
    } catch {
      // Already on screen through onError.
    }
  };

  return (
    <section
      aria-labelledby="silence-report-title"
      className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900"
    >
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex flex-col gap-0.5">
          <h2 id="silence-report-title" className="text-base font-semibold text-gray-900 dark:text-gray-100">
            {t("title")}
          </h2>
          <p className="text-sm text-gray-600 dark:text-gray-400">{t("subtitle")}</p>
        </div>
        {step === "closed" && (
          <Button type="button" variant="secondary" className="shrink-0 self-start sm:self-auto" onClick={() => setStep("editing")}>
            {t("open")}
          </Button>
        )}
      </div>

      {step === "editing" && (
        <form onSubmit={handleSubmit} noValidate className="flex flex-col gap-4 border-t border-gray-100 pt-4 dark:border-gray-800">
          <ChoicePills
            label={t("stageLabel")}
            options={SILENCE_STAGES}
            value={draft.stage}
            optionLabel={(o) => t(`stages.${o}`)}
            onChange={(stage) => {
              setDraft((prev) => ({ ...prev, stage }));
              setProblems((prev) => prev.filter((p) => p !== "stageRequired"));
            }}
            error={problems.includes("stageRequired") ? t("problems.stageRequired") : undefined}
          />
          <ChoicePills
            label={t("waitLabel")}
            hint={t("waitHint")}
            options={SILENCE_WAITS}
            value={draft.wait}
            optionLabel={(o) => t(`waits.${o}`)}
            onChange={(wait) => {
              setDraft((prev) => ({ ...prev, wait }));
              setProblems((prev) => prev.filter((p) => p !== "waitRequired"));
            }}
            error={problems.includes("waitRequired") ? t("problems.waitRequired") : undefined}
          />
          <FactPills
            label={t("promiseLabel")}
            options={["yes", "no"] as const}
            value={draft.promise}
            optionLabel={(o: PromiseAnswer) => t(`promise.${o}`)}
            notSaidLabel={t("promise.notSaid")}
            optionalLabel={t("optional")}
            onChange={(promise) => setDraft((prev) => ({ ...prev, promise }))}
          />
          <p className="rounded-md bg-gray-50 p-3 text-xs leading-relaxed text-gray-600 dark:bg-gray-950 dark:text-gray-400">
            {t("privacy")}{" "}
            <Link href="/applications" className="font-medium text-gray-900 hover:underline dark:text-gray-100">
              {t("trackerLink")}
            </Link>
          </p>
          <div className="flex flex-wrap items-center gap-3">
            <Button type="submit" disabled={submit.isPending}>
              {submit.isPending ? t("sending") : t("send")}
            </Button>
            <button
              type="button"
              className="min-h-11 px-1 text-sm text-gray-600 hover:underline dark:text-gray-400"
              onClick={() => {
                setStep("closed");
                setProblems([]);
                setError(null);
              }}
            >
              {t("cancel")}
            </button>
            <span className="text-xs text-gray-500 dark:text-gray-400">{t("repeatNote")}</span>
          </div>
          {error && (
            <p role="alert" className="text-sm text-red-600 dark:text-red-400">
              {error}
            </p>
          )}
        </form>
      )}

      {step === "sent" && (
        <p role="status" className="border-t border-gray-100 pt-4 text-sm leading-relaxed text-gray-600 dark:border-gray-800 dark:text-gray-400">
          {t("sent")}{" "}
          <Link href="/applications" className="font-medium text-gray-900 hover:underline dark:text-gray-100">
            {t("sentLink")}
          </Link>
        </p>
      )}
    </section>
  );
}

/** A required closed-list question as a row of pills: real radio inputs, visually hidden. */
function ChoicePills<T extends string>({
  label,
  hint,
  options,
  value,
  optionLabel,
  onChange,
  error,
}: {
  label: string;
  hint?: string;
  options: readonly T[];
  value: T | "";
  optionLabel: (option: T) => string;
  onChange: (value: T) => void;
  error?: string;
}) {
  const name = useId();
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="mb-2 text-sm font-medium text-gray-700 dark:text-gray-300">{label}</legend>
      <div className="flex flex-wrap gap-1.5">
        {options.map((option) => {
          const selected = value === option;
          return (
            <label key={option} className={`${PILL} ${selected ? SELECTED : IDLE}`}>
              <input type="radio" name={name} value={option} checked={selected} onChange={() => onChange(option)} className="sr-only" />
              {optionLabel(option)}
            </label>
          );
        })}
      </div>
      {error ? (
        <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
      ) : (
        hint && <p className="text-xs text-gray-500 dark:text-gray-400">{hint}</p>
      )}
    </fieldset>
  );
}
