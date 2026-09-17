"use client";

import { useId } from "react";

const PILL = "cursor-pointer rounded-lg border px-3 py-1.5 text-sm transition-colors focus-within:ring-2 focus-within:ring-accent";
const IDLE = "border-gray-300 bg-white text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800";
const SELECTED = "border-accent bg-accent-wash font-medium text-accent-ink";

/**
 * One closed-list fact of the experience form as a row of pills: a radio group with a "not
 * saying" pill first, so leaving the question unanswered is a visible choice rather than a
 * missing one. Real radio inputs, visually hidden, so it reads and tabs as a radio group.
 */
export function FactPills<T extends string>({
  label,
  hint,
  options,
  value,
  optionLabel,
  notSaidLabel,
  optionalLabel,
  onChange,
}: {
  label: string;
  hint?: string;
  options: readonly T[];
  value: T | "";
  optionLabel: (option: T) => string;
  notSaidLabel: string;
  optionalLabel: string;
  onChange: (value: T | "") => void;
}) {
  const name = useId();
  const choices: (T | "")[] = ["", ...options];

  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="flex flex-wrap items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300">
        {label}
        <span className="rounded-full bg-muted-wash px-2 py-0.5 text-[11px] font-medium text-muted-ink">{optionalLabel}</span>
        {hint ? <span className="text-xs font-normal text-gray-500 dark:text-gray-400">{hint}</span> : null}
      </legend>
      <div className="flex flex-wrap gap-1.5">
        {choices.map((choice) => {
          const selected = value === choice;
          return (
            <label key={choice || "__none"} className={`${PILL} ${selected ? SELECTED : IDLE}`}>
              <input
                type="radio"
                name={name}
                value={choice}
                checked={selected}
                onChange={() => onChange(choice)}
                className="sr-only"
              />
              {choice === "" ? notSaidLabel : optionLabel(choice)}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}

/** The multi-select twin, for the interview types: checkboxes in the same pill clothes. */
export function FactCheckPills<T extends string>({
  label,
  hint,
  options,
  values,
  optionLabel,
  optionalLabel,
  onToggle,
}: {
  label: string;
  hint?: string;
  options: readonly T[];
  values: readonly T[];
  optionLabel: (option: T) => string;
  optionalLabel: string;
  onToggle: (value: T) => void;
}) {
  return (
    <fieldset className="flex flex-col gap-2">
      <legend className="flex flex-wrap items-center gap-2 text-sm font-medium text-gray-700 dark:text-gray-300">
        {label}
        <span className="rounded-full bg-muted-wash px-2 py-0.5 text-[11px] font-medium text-muted-ink">{optionalLabel}</span>
        {hint ? <span className="text-xs font-normal text-gray-500 dark:text-gray-400">{hint}</span> : null}
      </legend>
      <div className="flex flex-wrap gap-1.5">
        {options.map((option) => {
          const selected = values.includes(option);
          return (
            <label key={option} className={`${PILL} ${selected ? SELECTED : IDLE}`}>
              <input type="checkbox" checked={selected} onChange={() => onToggle(option)} className="sr-only" />
              {optionLabel(option)}
            </label>
          );
        })}
      </div>
    </fieldset>
  );
}
