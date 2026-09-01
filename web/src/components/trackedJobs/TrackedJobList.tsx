"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import type { EmploymentType, TrackedJobResponse } from "@/types/api";
import { EMPLOYMENT_TYPES } from "@/lib/constants/employmentType";
import { Button } from "@/components/ui/Button";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { FormField } from "@/components/ui/FormField";

interface ConvertValues {
  employmentType: EmploymentType;
  appliedAt: string;
  notes: string;
}

function todayInputValue(): string {
  return new Date().toISOString().slice(0, 10);
}

interface TrackedJobListProps {
  items: TrackedJobResponse[];
  onDelete: (id: string) => Promise<void>;
  onConvert: (id: string, values: ConvertValues) => Promise<void>;
}

export function TrackedJobList({ items, onDelete, onConvert }: TrackedJobListProps) {
  const t = useTranslations("trackedJobs.list");
  const tEmploymentType = useTranslations("employmentType");
  const locale = useLocale();

  const [convertingId, setConvertingId] = useState<string | null>(null);
  const [convertValues, setConvertValues] = useState<ConvertValues>({
    employmentType: "FullTime",
    appliedAt: todayInputValue(),
    notes: "",
  });
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (items.length === 0) {
    return <p className="py-8 text-center text-sm text-gray-500 dark:text-gray-400">{t("empty")}</p>;
  }

  const startConvert = (id: string) => {
    setConvertingId(id);
    setConvertValues({ employmentType: "FullTime", appliedAt: todayInputValue(), notes: "" });
  };

  const handleConfirmConvert = async (id: string) => {
    setIsSubmitting(true);
    try {
      await onConvert(id, convertValues);
      setConvertingId(null);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <ul className="flex flex-col gap-3">
      {items.map((item) => (
        <li key={item.id} className="rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
          <div className="flex items-start justify-between gap-4">
            <div className="min-w-0">
              <p className="font-medium text-gray-900 dark:text-gray-100">{item.companyName}</p>
              <p className="text-sm text-gray-700 dark:text-gray-300">{item.jobTitle}</p>
              {item.location && <p className="text-sm text-gray-500 dark:text-gray-400">{item.location}</p>}
              {item.jobUrl && (
                <a
                  href={item.jobUrl}
                  target="_blank"
                  rel="noreferrer"
                  className="block truncate text-sm text-blue-600 hover:underline dark:text-blue-400"
                >
                  {item.jobUrl}
                </a>
              )}
              {item.notes && <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{item.notes}</p>}
              <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">
                {t("addedAt", { date: new Date(item.addedAt).toLocaleDateString(locale) })}
              </p>
            </div>
            <div className="flex shrink-0 gap-2">
              <Button variant="secondary" onClick={() => startConvert(item.id)}>
                {t("markApplied")}
              </Button>
              <Button variant="danger" onClick={() => onDelete(item.id)}>
                {t("remove")}
              </Button>
            </div>
          </div>

          {convertingId === item.id && (
            <div className="mt-4 flex flex-col gap-3 border-t border-gray-100 pt-4 dark:border-gray-800">
              <div className="grid gap-3 sm:grid-cols-2">
                <FormField label={t("employmentType")} htmlFor={`convert-employmentType-${item.id}`}>
                  <Select
                    id={`convert-employmentType-${item.id}`}
                    value={convertValues.employmentType}
                    onChange={(e) => setConvertValues((prev) => ({ ...prev, employmentType: e.target.value as EmploymentType }))}
                  >
                    {EMPLOYMENT_TYPES.map((type) => (
                      <option key={type} value={type}>
                        {tEmploymentType(type)}
                      </option>
                    ))}
                  </Select>
                </FormField>
                <FormField label={t("appliedAt")} htmlFor={`convert-appliedAt-${item.id}`}>
                  <Input
                    id={`convert-appliedAt-${item.id}`}
                    type="date"
                    value={convertValues.appliedAt}
                    onChange={(e) => setConvertValues((prev) => ({ ...prev, appliedAt: e.target.value }))}
                  />
                </FormField>
              </div>
              <FormField label={t("notes")} htmlFor={`convert-notes-${item.id}`}>
                <Input
                  id={`convert-notes-${item.id}`}
                  value={convertValues.notes}
                  onChange={(e) => setConvertValues((prev) => ({ ...prev, notes: e.target.value }))}
                />
              </FormField>
              <div className="flex gap-2">
                <Button disabled={isSubmitting} onClick={() => handleConfirmConvert(item.id)}>
                  {isSubmitting ? t("saving") : t("confirmConvert")}
                </Button>
                <Button variant="secondary" onClick={() => setConvertingId(null)}>
                  {t("cancel")}
                </Button>
              </div>
            </div>
          )}
        </li>
      ))}
    </ul>
  );
}
