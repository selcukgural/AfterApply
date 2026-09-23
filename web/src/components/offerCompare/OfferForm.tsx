"use client";

import { useId } from "react";
import { useLocale, useTranslations } from "next-intl";
import type { SalaryBasis } from "@/lib/offerCompare/compare";
import { formatLira, NAME_MAX_LENGTH, parseAmount, type DraftField, type OfferDraft } from "@/lib/offerCompare/input";
import { PAYROLL } from "@/lib/offerCompare/payroll";

interface OfferFormProps {
  draft: OfferDraft;
  onChange: (field: DraftField, value: string) => void;
  onBasisChange: (basis: SalaryBasis) => void;
}

const boxClass =
  "flex h-11 items-center gap-2 rounded-md border border-gray-300 bg-white px-3 focus-within:border-accent focus-within:ring-1 focus-within:ring-accent dark:border-gray-700 dark:bg-gray-950";
const inputClass =
  "w-full min-w-0 flex-1 border-0 bg-transparent p-0 text-[15px] text-gray-900 tabular-nums outline-none dark:text-gray-100";
const labelClass = "text-sm font-medium text-gray-600 dark:text-gray-400";
const unitClass = "shrink-0 text-xs text-gray-500 dark:text-gray-400";

/** One offer's inputs. Every field keeps the text as typed; `lib/offerCompare/input` parses it. */
export function OfferForm({ draft, onChange, onBasisChange }: OfferFormProps) {
  const t = useTranslations("offerCompare.fields");
  const tOffers = useTranslations("offerCompare.offers");
  const locale = useLocale();
  const id = useId();

  const salary = parseAmount(draft.salary);
  const belowMinimum = draft.basis === "gross" && salary > 0 && salary < PAYROLL.minimumWageGross;

  const field = (key: DraftField, label: string, unit: string, inputMode: "numeric" | "decimal" = "numeric") => (
    <div className="flex min-w-0 flex-col gap-1.5">
      <label htmlFor={`${id}-${key}`} className={labelClass}>
        {label}
      </label>
      <span className={boxClass}>
        <input
          id={`${id}-${key}`}
          type="text"
          inputMode={inputMode}
          autoComplete="off"
          value={draft[key]}
          onChange={(event) => onChange(key, event.target.value)}
          className={inputClass}
        />
        <span className={unitClass}>{unit}</span>
      </span>
    </div>
  );

  const basisButton = (basis: SalaryBasis, label: string) => (
    <button
      type="button"
      aria-pressed={draft.basis === basis}
      onClick={() => onBasisChange(basis)}
      className={`h-8 rounded px-3 text-sm font-semibold transition-colors ${
        draft.basis === basis
          ? "bg-accent text-white"
          : "text-gray-600 hover:text-gray-900 dark:text-gray-400 dark:hover:text-gray-100"
      }`}
    >
      {label}
    </button>
  );

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-col gap-1.5">
        <label htmlFor={`${id}-name`} className={labelClass}>
          {tOffers("nameLabel")}
        </label>
        <span className={boxClass}>
          <input
            id={`${id}-name`}
            type="text"
            autoComplete="off"
            maxLength={NAME_MAX_LENGTH}
            value={draft.name}
            onChange={(event) => onChange("name", event.target.value)}
            className={`${inputClass} font-semibold`}
          />
        </span>
      </div>

      <div className="flex flex-col gap-2">
        <label htmlFor={`${id}-salary`} className={labelClass}>
          {t("salary")}
        </label>
        <div
          role="group"
          aria-label={t("basisLabel")}
          className="inline-flex gap-0.5 self-start rounded-md border border-gray-200 bg-gray-100 p-0.5 dark:border-gray-800 dark:bg-gray-800/60"
        >
          {basisButton("gross", t("gross"))}
          {basisButton("net", t("net"))}
        </div>
        <span className={boxClass}>
          <input
            id={`${id}-salary`}
            type="text"
            inputMode="numeric"
            autoComplete="off"
            value={draft.salary}
            onChange={(event) => onChange("salary", event.target.value)}
            aria-describedby={belowMinimum ? `${id}-salary-note` : undefined}
            className={inputClass}
          />
          <span className={unitClass}>{draft.basis === "gross" ? t("grossUnit") : t("netUnit")}</span>
        </span>
        {belowMinimum && (
          <p id={`${id}-salary-note`} className="text-sm text-warn-ink">
            {t("belowMinimum", { amount: formatLira(PAYROLL.minimumWageGross, locale) })}
          </p>
        )}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        {field("bonusSalaries", t("bonus"), t("bonusUnit"), "decimal")}
        {field("mealPerDay", t("meal"), t("mealUnit"))}
        {field("healthPerMonth", t("health"), t("perMonthUnit"))}
        {field("besPerMonth", t("bes"), t("perMonthUnit"))}
        {field("remoteDaysPerWeek", t("remote"), t("remoteUnit"))}
        {field("officeDayCost", t("officeCost"), t("officeCostUnit"))}
      </div>
    </div>
  );
}
