"use client";

import { useState, type FormEvent } from "react";
import { useLocale, useTranslations } from "next-intl";
import type { CompanySalaryRequest, SalaryCurrency } from "@/types/api";
import { occupationsApi } from "@/lib/api/occupations";
import {
  SALARY_CURRENCIES,
  SALARY_EMPLOYMENT_STATUSES,
  SALARY_EMPLOYMENT_TYPES,
  buildSalaryRequest,
  occupationName,
  occupationOtherName,
  periodYearOptions,
  validateSalaryDraft,
  type SalaryDraft,
  type SalaryDraftField,
  type SalaryDraftProblem,
} from "@/lib/companySalaries/salaryDraft";
import { createCompanySalarySchema } from "@/lib/validation/companySalarySchema";
import { Button } from "@/components/ui/Button";
import { Combobox } from "@/components/ui/Combobox";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";

interface CompanySalaryFormProps {
  companyName: string;
  initialDraft: SalaryDraft;
  submitLabel: string;
  onSubmit: (request: CompanySalaryRequest) => Promise<void>;
  /** A message from the server (quota, duplicate title) — shown under the form. */
  serverError: string | null;
  /** "2 / 10" under the form; absent while the quota is still loading. */
  quota?: { used: number; limit: number };
}

const RADIO_CLASSES = "h-4 w-4 border-gray-300 text-accent focus:ring-accent dark:border-gray-600 dark:bg-gray-900";

/**
 * The salary form (design canvas 2B, 2026-09-16; period added 2026-09-18): an occupation picked
 * from the catalogue, the total years, the working arrangement, whether the author still draws
 * this salary, the years it was drawn in, the monthly net with its currency, and the bonus as an
 * explicit yes/no. The period's end year exists only for a former employee — a current one sees
 * "still drawing it" in its place, and switching back to current clears it. Nothing here is free
 * text a reader would see: the occupation must be a catalogue row (the typeahead searches both
 * languages), and typing again after a pick drops the pick.
 */
export function CompanySalaryForm({ companyName, initialDraft, submitLabel, onSubmit, serverError, quota }: CompanySalaryFormProps) {
  const t = useTranslations("companySalaries.form");
  const locale = useLocale();
  const tType = useTranslations("employmentType");
  const tStatus = useTranslations("salaryEmploymentStatus");
  const tCurrency = useTranslations("salaryCurrency");
  const tValidation = useTranslations("validation");

  const [draft, setDraft] = useState<SalaryDraft>(initialDraft);
  const [problems, setProblems] = useState<Partial<Record<SalaryDraftField, SalaryDraftProblem>>>({});
  const [submitting, setSubmitting] = useState(false);
  const [yearOptions] = useState(periodYearOptions);
  const isFormer = draft.employmentStatus === "FormerEmployee";

  const problemText = (field: SalaryDraftField) => {
    const problem = problems[field];
    return problem ? t(`problems.${problem}`) : undefined;
  };

  const set = <K extends keyof SalaryDraft>(field: K, value: SalaryDraft[K]) => setDraft((prev) => ({ ...prev, [field]: value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const found = validateSalaryDraft(draft);
    setProblems(found);
    if (Object.keys(found).length > 0) {
      return;
    }

    const request = buildSalaryRequest(draft);
    // Belt to the braces above: the same rules as the server's, on the request that leaves.
    if (!createCompanySalarySchema(tValidation).safeParse(request).success) {
      return;
    }

    setSubmitting(true);
    try {
      await onSubmit(request);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <form
      onSubmit={handleSubmit}
      noValidate
      className="flex flex-col gap-5 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-800 dark:bg-gray-900"
    >
      <div>
        <h2 className="text-lg font-semibold text-gray-900 dark:text-gray-100">{t("heading", { company: companyName })}</h2>
        <p className="mt-1 text-sm text-gray-600 dark:text-gray-400">{t("anonymityNote")}</p>
      </div>

      <FormField label={t("occupation")} htmlFor="salary-occupation" error={problemText("occupation")}>
        <Combobox
          id="salary-occupation"
          value={draft.occupationLabel}
          // Typing again is not the pick any more: the id goes until a row is chosen.
          onChange={(value) => setDraft((prev) => ({ ...prev, occupationLabel: value, occupationId: null }))}
          onSelect={(option) => setDraft((prev) => ({ ...prev, occupationLabel: option.label, occupationId: option.id }))}
          onSearch={async (q) =>
            (await occupationsApi.search(q)).map((o) => ({
              id: o.id,
              label: occupationName(o, locale),
              hint: occupationOtherName(o, locale),
            }))
          }
          placeholder={t("occupationPlaceholder")}
          loadingText={t("occupationSearching")}
          emptyText={t("occupationNoMatch")}
        />
        <p className="text-xs text-gray-500 dark:text-gray-400">{t("occupationHint")}</p>
      </FormField>

      <div className="grid gap-4 sm:grid-cols-2">
        <FormField label={t("years")} htmlFor="salary-years" error={problemText("yearsOfExperience")}>
          <Input
            id="salary-years"
            type="number"
            inputMode="numeric"
            min={0}
            max={50}
            step={1}
            value={draft.yearsOfExperience}
            onChange={(e) => set("yearsOfExperience", e.target.value)}
          />
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("yearsHint")}</p>
        </FormField>

        <FormField label={t("employmentType")} htmlFor="salary-employment-type" error={problemText("employmentType")}>
          <Select
            id="salary-employment-type"
            value={draft.employmentType}
            onChange={(e) => set("employmentType", e.target.value as SalaryDraft["employmentType"])}
          >
            <option value="">{t("employmentTypePlaceholder")}</option>
            {SALARY_EMPLOYMENT_TYPES.map((type) => (
              <option key={type} value={type}>
                {tType(type)}
              </option>
            ))}
          </Select>
        </FormField>
      </div>

      <fieldset className="flex flex-col gap-1">
        <legend className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("employmentStatus")}</legend>
        <div className="flex flex-wrap gap-x-6 gap-y-1">
          {SALARY_EMPLOYMENT_STATUSES.map((status) => (
            <label key={status} className="flex min-h-11 items-center gap-2 text-sm text-gray-900 dark:text-gray-100">
              <input
                type="radio"
                name="salary-employment-status"
                value={status}
                checked={draft.employmentStatus === status}
                // Back to "current" means "still drawing it": the end year has nothing to say.
                onChange={() =>
                  setDraft((prev) => ({
                    ...prev,
                    employmentStatus: status,
                    periodEndYear: status === "FormerEmployee" ? prev.periodEndYear : "",
                  }))
                }
                className={RADIO_CLASSES}
              />
              {tStatus(status)}
            </label>
          ))}
        </div>
        {problemText("employmentStatus") && <p className="text-sm text-red-600 dark:text-red-400">{problemText("employmentStatus")}</p>}
      </fieldset>

      <fieldset className="flex flex-col gap-1">
        <legend className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("period")}</legend>
        <div className="grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-2">
          <Select
            id="salary-period-start"
            aria-label={t("periodStart")}
            value={draft.periodStartYear}
            onChange={(e) => set("periodStartYear", e.target.value)}
          >
            <option value="">{t("periodStart")}</option>
            {yearOptions.map((year) => (
              <option key={year} value={String(year)}>
                {year}
              </option>
            ))}
          </Select>
          <span className="text-gray-400 dark:text-gray-500" aria-hidden="true">
            –
          </span>
          {isFormer ? (
            <Select
              id="salary-period-end"
              aria-label={t("periodEnd")}
              value={draft.periodEndYear}
              onChange={(e) => set("periodEndYear", e.target.value)}
            >
              <option value="">{t("periodEnd")}</option>
              {yearOptions.map((year) => (
                <option key={year} value={String(year)}>
                  {year}
                </option>
              ))}
            </Select>
          ) : (
            <div
              id="salary-period-end-static"
              className="flex min-h-[2.5rem] items-center rounded-md border border-gray-300 bg-gray-50 px-3 py-2 text-sm text-gray-500 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-400"
            >
              {draft.employmentStatus === "CurrentEmployee" ? t("periodOngoing") : t("periodEnd")}
            </div>
          )}
        </div>
        {problemText("periodStartYear") || problemText("periodEndYear") ? (
          <p className="text-sm text-red-600 dark:text-red-400">{problemText("periodStartYear") ?? problemText("periodEndYear")}</p>
        ) : (
          <p className="text-xs text-gray-500 dark:text-gray-400">{isFormer ? t("periodHintFormer") : t("periodHintCurrent")}</p>
        )}
      </fieldset>

      <div className="flex flex-col gap-1">
        <label htmlFor="salary-amount" className="text-sm font-medium text-gray-700 dark:text-gray-300">
          {t("monthlyNet")}
        </label>
        <div className="grid grid-cols-[minmax(0,1fr)_8rem] gap-2">
          <Input
            id="salary-amount"
            inputMode="decimal"
            value={draft.monthlyNetAmount}
            onChange={(e) => set("monthlyNetAmount", e.target.value)}
            placeholder={t("monthlyNetPlaceholder")}
          />
          <Select
            id="salary-currency"
            aria-label={t("currency")}
            value={draft.currency}
            onChange={(e) => set("currency", e.target.value as SalaryCurrency)}
          >
            {SALARY_CURRENCIES.map((currency) => (
              <option key={currency} value={currency}>
                {tCurrency(currency)}
              </option>
            ))}
          </Select>
        </div>
        {problemText("monthlyNetAmount") ? (
          <p className="text-sm text-red-600 dark:text-red-400">{problemText("monthlyNetAmount")}</p>
        ) : (
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("monthlyNetHint")}</p>
        )}
      </div>

      <fieldset className="flex flex-col gap-2">
        <legend className="text-sm font-medium text-gray-700 dark:text-gray-300">{t("bonus")}</legend>
        <div className="flex flex-wrap gap-x-6 gap-y-1">
          {([false, true] as const).map((answer) => (
            <label key={String(answer)} className="flex min-h-11 items-center gap-2 text-sm text-gray-900 dark:text-gray-100">
              <input
                type="radio"
                name="salary-has-bonus"
                value={String(answer)}
                checked={draft.hasBonus === answer}
                onChange={() => set("hasBonus", answer)}
                className={RADIO_CLASSES}
              />
              {answer ? t("bonusYes") : t("bonusNo")}
            </label>
          ))}
        </div>
        {problemText("hasBonus") && <p className="text-sm text-red-600 dark:text-red-400">{problemText("hasBonus")}</p>}
        {draft.hasBonus && (
          <div className="flex flex-col gap-1">
            <label htmlFor="salary-bonus-amount" className="sr-only">
              {t("bonusAmount")}
            </label>
            <div className="grid grid-cols-[minmax(0,1fr)_8rem] gap-2">
              <Input
                id="salary-bonus-amount"
                inputMode="decimal"
                value={draft.annualBonusAmount}
                onChange={(e) => set("annualBonusAmount", e.target.value)}
                placeholder={t("bonusAmountPlaceholder")}
              />
              <div className="flex items-center rounded-md border border-gray-300 bg-gray-50 px-3 py-2 text-sm text-gray-500 dark:border-gray-700 dark:bg-gray-800 dark:text-gray-400">
                {t("bonusUnit", { currency: tCurrency(draft.currency) })}
              </div>
            </div>
            {problemText("annualBonusAmount") ? (
              <p className="text-sm text-red-600 dark:text-red-400">{problemText("annualBonusAmount")}</p>
            ) : (
              <p className="text-xs text-gray-500 dark:text-gray-400">{t("bonusHint")}</p>
            )}
          </div>
        )}
      </fieldset>

      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-gray-100 pt-4 dark:border-gray-800">
        <div className="flex flex-col gap-1">
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {quota ? t("quotaNote", { used: quota.used, limit: quota.limit }) : t("publishNote")}
          </p>
          {serverError && (
            <p role="alert" className="text-sm text-red-600 dark:text-red-400">
              {serverError}
            </p>
          )}
        </div>
        <Button type="submit" disabled={submitting}>
          {submitting ? t("submitting") : submitLabel}
        </Button>
      </div>
    </form>
  );
}
