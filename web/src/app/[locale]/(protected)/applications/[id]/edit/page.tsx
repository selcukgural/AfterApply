"use client";

import { use } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";
import { ApplicationForm, type ApplicationFormValues } from "@/components/applications/ApplicationForm";

export default function EditApplicationPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const t = useTranslations("applications");
  const tCommon = useTranslations("common");
  const router = useRouter();
  const queryClient = useQueryClient();

  const { data: application, isLoading } = useQuery({
    queryKey: ["applications", "detail", id],
    queryFn: () => applicationsApi.getById(id),
  });

  const handleSubmit = async (values: ApplicationFormValues) => {
    await applicationsApi.update(id, {
      jobTitle: values.jobTitle,
      jobUrl: values.jobUrl || null,
      location: values.location || null,
      employmentType: values.employmentType,
      appliedAt: new Date(values.appliedAt).toISOString(),
      notes: values.notes || null,
    });

    await queryClient.invalidateQueries({ queryKey: ["applications"] });
    router.push(`/applications/${id}`);
  };

  if (isLoading || !application) {
    return <p className="text-sm text-gray-500">{tCommon("loading")}</p>;
  }

  return (
    <div className="mx-auto max-w-lg">
      <h1 className="mb-6 text-xl font-semibold text-gray-900">{t("edit.title")}</h1>
      <ApplicationForm mode="edit" initial={application} onSubmit={handleSubmit} submitLabel={t("form.editSubmit")} />
    </div>
  );
}
