"use client";

import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { cvDocumentsApi } from "@/lib/api/cvDocuments";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";

interface CvSelectFieldProps {
  value: string;
  onChange: (cvDocumentId: string) => void;
  /** Pre-selects the user's default CV once the list arrives, but only while nothing is selected —
   *  so it fills a new form and never overwrites what an edit form loaded. */
  autoSelectDefault?: boolean;
}

/**
 * Picks which stored CV an application was sent with. Optional by design: plenty of applications
 * predate the feature, and plenty go out through a form that never took a file.
 */
export function CvSelectField({ value, onChange, autoSelectDefault = false }: CvSelectFieldProps) {
  const t = useTranslations("applications.form");
  // Same query key as the CV page, so uploading a CV there and coming back here shows it without
  // a reload.
  const { data } = useQuery({ queryKey: ["cvDocuments"], queryFn: () => cvDocumentsApi.list() });

  const items = data?.items ?? [];
  const defaultId = items.find((item) => item.isDefault)?.id;

  useEffect(() => {
    if (autoSelectDefault && value === "" && defaultId) {
      onChange(defaultId);
    }
    // onChange is a new closure on every render of the parent form; depending on it would re-run
    // this on every keystroke elsewhere in the form.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [autoSelectDefault, value, defaultId]);

  if (data && items.length === 0) {
    return (
      <FormField label={t("cvDocument")} htmlFor="cvDocument">
        <p className="text-sm leading-6 text-gray-500 dark:text-gray-400">
          {t("cvDocumentEmpty")}{" "}
          <Link href="/cv" className="text-accent-ink underline">
            {t("cvDocumentEmptyLink")}
          </Link>
        </p>
      </FormField>
    );
  }

  return (
    <FormField label={t("cvDocument")} htmlFor="cvDocument">
      <Select id="cvDocument" value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">{t("cvDocumentNone")}</option>
        {items.map((item) => (
          <option key={item.id} value={item.id}>
            {item.fileName}
          </option>
        ))}
      </Select>
    </FormField>
  );
}
