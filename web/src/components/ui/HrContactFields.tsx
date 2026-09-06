"use client";

import { useTranslations } from "next-intl";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";

export interface HrContactValues {
  hrName: string;
  hrEmail: string;
  hrLinkedInUrl: string;
}

export const EMPTY_HR_CONTACT: HrContactValues = { hrName: "", hrEmail: "", hrLinkedInUrl: "" };

interface HrContactFieldsProps {
  /** Prefixes the input ids so two of these can sit on one page without colliding. */
  idPrefix: string;
  values: HrContactValues;
  errors: Record<string, string>;
  onChange: (field: keyof HrContactValues, value: string) => void;
}

/**
 * Shared by the application form and the tracked-job form — the same three optional fields in both
 * places, so the labels and the hint stay identical rather than drifting apart.
 */
export function HrContactFields({ idPrefix, values, errors, onChange }: HrContactFieldsProps) {
  const t = useTranslations("hrContact");

  return (
    <fieldset className="rounded-md border border-gray-200 p-4 dark:border-gray-800">
      <legend className="px-1 text-sm font-medium text-gray-900 dark:text-gray-100">{t("legend")}</legend>
      <p className="mb-3 text-xs text-gray-500 dark:text-gray-400">{t("hint")}</p>
      <div className="grid gap-3 sm:grid-cols-3">
        <FormField label={t("name")} htmlFor={`${idPrefix}-hrName`} error={errors.hrName}>
          <Input
            id={`${idPrefix}-hrName`}
            value={values.hrName}
            onChange={(e) => onChange("hrName", e.target.value)}
          />
        </FormField>
        <FormField label={t("email")} htmlFor={`${idPrefix}-hrEmail`} error={errors.hrEmail}>
          <Input
            id={`${idPrefix}-hrEmail`}
            type="email"
            inputMode="email"
            value={values.hrEmail}
            onChange={(e) => onChange("hrEmail", e.target.value)}
          />
        </FormField>
        <FormField label={t("linkedIn")} htmlFor={`${idPrefix}-hrLinkedInUrl`} error={errors.hrLinkedInUrl}>
          <Input
            id={`${idPrefix}-hrLinkedInUrl`}
            value={values.hrLinkedInUrl}
            placeholder="https://www.linkedin.com/in/..."
            onChange={(e) => onChange("hrLinkedInUrl", e.target.value)}
          />
        </FormField>
      </div>
    </fieldset>
  );
}
