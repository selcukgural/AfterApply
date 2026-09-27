"use client";

import { Suspense, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";
import { ApplicationForm, type ApplicationFormValues } from "@/components/applications/ApplicationForm";
import { FormField } from "@/components/ui/FormField";
import { Select } from "@/components/ui/Select";
import { parseStatus } from "@/lib/applications/listView";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";
import type { ApplicationStatus } from "@/types/api";

// useSearchParams() (the board's ?from=board&status=) needs a Suspense boundary above it.
export default function NewApplicationPage() {
  return (
    <Suspense fallback={null}>
      <NewApplicationForm />
    </Suspense>
  );
}

function NewApplicationForm() {
  const t = useTranslations("applications");
  const router = useRouter();
  const tStatus = useTranslations("status");
  const queryClient = useQueryClient();
  const searchParams = useSearchParams();
  // Opened from a board column's "+ add": the application is created in that column's stage and
  // the user goes back to the board (DECISIONS.md 2026-09-27). Without it, the form is as it was.
  const fromBoard = searchParams.get("from") === "board";
  const [stage, setStage] = useState<ApplicationStatus>(parseStatus(searchParams.get("status")) || "Applied");

  const handleSubmit = async (values: ApplicationFormValues) => {
    const created = await applicationsApi.create({
      companyName: values.companyName,
      jobTitle: values.jobTitle,
      jobUrl: values.jobUrl || null,
      location: values.location || null,
      employmentType: values.employmentType,
      appliedAt: new Date(values.appliedAt).toISOString(),
      source: values.source,
      notes: values.notes || null,
      hrName: values.hrName || null,
      hrEmail: values.hrEmail || null,
      hrLinkedInUrl: values.hrLinkedInUrl || null,
      cvDocumentId: values.cvDocumentId || null,
    });

    if (fromBoard && stage !== "Applied") {
      await applicationsApi.changeStatus(created.id, { newStatus: stage, note: null, changedAt: null });
    }

    await queryClient.invalidateQueries({ queryKey: ["applications"] });
    router.push(fromBoard ? "/applications?view=board" : `/applications/${created.id}`);
  };

  return (
    <div className="mx-auto max-w-lg">
      <h1 className="mb-6 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("new.title")}</h1>
      {fromBoard && (
        <FormField label={t("new.stage")} htmlFor="new-application-stage" className="mb-4">
          <Select
            id="new-application-stage"
            value={stage}
            onChange={(event) => setStage(event.target.value as ApplicationStatus)}
          >
            {APPLICATION_STATUSES.map((status) => (
              <option key={status} value={status}>
                {tStatus(status)}
              </option>
            ))}
          </Select>
        </FormField>
      )}
      <ApplicationForm mode="create" onSubmit={handleSubmit} submitLabel={t("form.createSubmit")} />
    </div>
  );
}
