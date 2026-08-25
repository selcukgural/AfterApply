"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";
import { ApplicationForm, type ApplicationFormValues } from "@/components/applications/ApplicationForm";

export default function NewApplicationPage() {
  const t = useTranslations("applications");
  const router = useRouter();
  const queryClient = useQueryClient();

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
    });

    await queryClient.invalidateQueries({ queryKey: ["applications"] });
    router.push(`/applications/${created.id}`);
  };

  return (
    <div className="mx-auto max-w-lg">
      <h1 className="mb-6 text-xl font-semibold text-gray-900 dark:text-gray-100">{t("new.title")}</h1>
      <ApplicationForm mode="create" onSubmit={handleSubmit} submitLabel={t("form.createSubmit")} />
    </div>
  );
}
