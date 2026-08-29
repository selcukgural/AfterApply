"use client";

import { useState, type FormEvent } from "react";
import { useTranslations } from "next-intl";
import { createTrackedJobSchema } from "@/lib/validation/trackedJobSchema";
import { FormField } from "@/components/ui/FormField";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

export interface TrackedJobFormValues {
  companyName: string;
  jobTitle: string;
  jobUrl: string;
  location: string;
  notes: string;
}

const EMPTY_VALUES: TrackedJobFormValues = {
  companyName: "",
  jobTitle: "",
  jobUrl: "",
  location: "",
  notes: "",
};

interface TrackedJobFormProps {
  onSubmit: (values: TrackedJobFormValues) => Promise<void>;
}

export function TrackedJobForm({ onSubmit }: TrackedJobFormProps) {
  const t = useTranslations("trackedJobs.form");
  const tValidation = useTranslations("validation");

  const [values, setValues] = useState<TrackedJobFormValues>(EMPTY_VALUES);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [formError, setFormError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const update = (field: keyof TrackedJobFormValues) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setValues((prev) => ({ ...prev, [field]: e.target.value }));

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setFormError(null);

    const result = createTrackedJobSchema(tValidation).safeParse(values);
    if (!result.success) {
      const fieldErrors = result.error.flatten().fieldErrors as Record<string, string[] | undefined>;
      setErrors(Object.fromEntries(Object.entries(fieldErrors).map(([k, v]) => [k, v?.[0] ?? ""])));
      return;
    }
    setErrors({});

    setIsSubmitting(true);
    try {
      await onSubmit(values);
      setValues(EMPTY_VALUES);
    } catch {
      setFormError(t("saveError"));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="grid gap-3 sm:grid-cols-2">
        <FormField label={t("companyName")} htmlFor="tj-companyName" error={errors.companyName}>
          <Input id="tj-companyName" value={values.companyName} onChange={update("companyName")} />
        </FormField>
        <FormField label={t("jobTitle")} htmlFor="tj-jobTitle" error={errors.jobTitle}>
          <Input id="tj-jobTitle" value={values.jobTitle} onChange={update("jobTitle")} />
        </FormField>
        <FormField label={t("jobUrl")} htmlFor="tj-jobUrl" error={errors.jobUrl}>
          <Input id="tj-jobUrl" value={values.jobUrl} onChange={update("jobUrl")} />
        </FormField>
        <FormField label={t("location")} htmlFor="tj-location" error={errors.location}>
          <Input id="tj-location" value={values.location} onChange={update("location")} />
        </FormField>
      </div>
      <FormField label={t("notes")} htmlFor="tj-notes" error={errors.notes}>
        <Input id="tj-notes" value={values.notes} onChange={update("notes")} />
      </FormField>
      {formError && <p className="text-sm text-red-600 dark:text-red-400">{formError}</p>}
      <Button type="submit" disabled={isSubmitting} className="self-start">
        {isSubmitting ? t("saving") : t("submit")}
      </Button>
    </form>
  );
}
