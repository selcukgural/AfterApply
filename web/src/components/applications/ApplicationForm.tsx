"use client";

import { useState, type FormEvent } from "react";
import type { ApplicationDetailResponse, EmploymentType, Source } from "@/types/api";
import { createApplicationSchema, updateApplicationSchema } from "@/lib/validation/applicationSchema";
import { EMPLOYMENT_TYPES, EMPLOYMENT_TYPE_LABELS } from "@/lib/constants/employmentType";
import { SOURCES, SOURCE_LABELS } from "@/lib/constants/source";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Select } from "@/components/ui/Select";
import { Button } from "@/components/ui/Button";

export interface ApplicationFormValues {
  companyName: string;
  jobTitle: string;
  jobUrl: string;
  location: string;
  employmentType: EmploymentType;
  appliedAt: string;
  source: Source;
  notes: string;
}

interface ApplicationFormProps {
  mode: "create" | "edit";
  initial?: ApplicationDetailResponse;
  onSubmit: (values: ApplicationFormValues) => Promise<void>;
  submitLabel: string;
}

function toDateInputValue(iso: string): string {
  return iso.slice(0, 10);
}

export function ApplicationForm({ mode, initial, onSubmit, submitLabel }: ApplicationFormProps) {
  const [values, setValues] = useState<ApplicationFormValues>({
    companyName: initial?.companyName ?? "",
    jobTitle: initial?.jobTitle ?? "",
    jobUrl: initial?.jobUrl ?? "",
    location: initial?.location ?? "",
    employmentType: initial?.employmentType ?? "FullTime",
    appliedAt: initial ? toDateInputValue(initial.appliedAt) : toDateInputValue(new Date().toISOString()),
    source: initial?.source ?? "Manual",
    notes: initial?.notes ?? "",
  });
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const update = <K extends keyof ApplicationFormValues>(field: K) =>
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
      setValues((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const schema = mode === "create" ? createApplicationSchema : updateApplicationSchema;
    const result = schema.safeParse(values);
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors as Record<string, string[] | undefined>;
      setErrors(Object.fromEntries(Object.entries(fieldErrors).map(([k, v]) => [k, v?.[0] ?? ""])));
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      await onSubmit(values);
    } catch {
      setFormError("Kaydedilemedi. Lütfen tekrar deneyin.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4">
      {mode === "create" && (
        <FormField label="Şirket Adı" htmlFor="companyName" error={errors.companyName}>
          <Input id="companyName" value={values.companyName} onChange={update("companyName")} />
        </FormField>
      )}
      <FormField label="Pozisyon" htmlFor="jobTitle" error={errors.jobTitle}>
        <Input id="jobTitle" value={values.jobTitle} onChange={update("jobTitle")} />
      </FormField>
      <FormField label="İlan URL'si" htmlFor="jobUrl" error={errors.jobUrl}>
        <Input id="jobUrl" value={values.jobUrl} onChange={update("jobUrl")} />
      </FormField>
      <FormField label="Konum" htmlFor="location" error={errors.location}>
        <Input id="location" value={values.location} onChange={update("location")} />
      </FormField>
      <div className="grid grid-cols-2 gap-3">
        <FormField label="Çalışma Şekli" htmlFor="employmentType" error={errors.employmentType}>
          <Select id="employmentType" value={values.employmentType} onChange={update("employmentType")}>
            {EMPLOYMENT_TYPES.map((type) => (
              <option key={type} value={type}>
                {EMPLOYMENT_TYPE_LABELS[type]}
              </option>
            ))}
          </Select>
        </FormField>
        <FormField label="Başvuru Tarihi" htmlFor="appliedAt" error={errors.appliedAt}>
          <Input id="appliedAt" type="date" value={values.appliedAt} onChange={update("appliedAt")} />
        </FormField>
      </div>
      {mode === "create" && (
        <FormField label="Kaynak" htmlFor="source">
          <Select id="source" value={values.source} onChange={update("source")}>
            {SOURCES.map((source) => (
              <option key={source} value={source}>
                {SOURCE_LABELS[source]}
              </option>
            ))}
          </Select>
        </FormField>
      )}
      <FormField label="Notlar" htmlFor="notes" error={errors.notes}>
        <textarea
          id="notes"
          value={values.notes}
          onChange={update("notes")}
          rows={4}
          className="w-full rounded-md border border-gray-300 px-3 py-2 text-sm text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
        />
      </FormField>
      {formError && <p className="text-sm text-red-600">{formError}</p>}
      <Button type="submit" disabled={isSubmitting}>
        {isSubmitting ? "Kaydediliyor..." : submitLabel}
      </Button>
    </form>
  );
}
