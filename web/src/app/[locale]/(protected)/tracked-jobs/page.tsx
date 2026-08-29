"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import type { EmploymentType } from "@/types/api";
import { trackedJobsApi } from "@/lib/api/trackedJobs";
import { TrackedJobForm, type TrackedJobFormValues } from "@/components/trackedJobs/TrackedJobForm";
import { TrackedJobList } from "@/components/trackedJobs/TrackedJobList";

export default function TrackedJobsPage() {
  const t = useTranslations("trackedJobs");
  const tCommon = useTranslations("common");
  const queryClient = useQueryClient();
  const router = useRouter();

  const { data, isLoading } = useQuery({
    queryKey: ["trackedJobs"],
    queryFn: () => trackedJobsApi.getAll(),
  });

  const handleCreate = async (values: TrackedJobFormValues) => {
    await trackedJobsApi.create({
      companyName: values.companyName,
      jobTitle: values.jobTitle,
      jobUrl: values.jobUrl || null,
      location: values.location || null,
      notes: values.notes || null,
    });
    await queryClient.invalidateQueries({ queryKey: ["trackedJobs"] });
  };

  const handleDelete = async (id: string) => {
    await trackedJobsApi.remove(id);
    await queryClient.invalidateQueries({ queryKey: ["trackedJobs"] });
  };

  const handleConvert = async (id: string, values: { employmentType: EmploymentType; appliedAt: string; notes: string }) => {
    const application = await trackedJobsApi.convert(id, {
      employmentType: values.employmentType,
      appliedAt: new Date(values.appliedAt).toISOString(),
      notes: values.notes || null,
    });
    await queryClient.invalidateQueries({ queryKey: ["trackedJobs"] });
    await queryClient.invalidateQueries({ queryKey: ["applications"] });
    router.push(`/applications/${application.id}`);
  };

  return (
    <div className="flex flex-col gap-6">
      <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h1>
      <TrackedJobForm onSubmit={handleCreate} />
      {isLoading || !data ? (
        <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>
      ) : (
        <TrackedJobList items={data} onDelete={handleDelete} onConvert={handleConvert} />
      )}
    </div>
  );
}
