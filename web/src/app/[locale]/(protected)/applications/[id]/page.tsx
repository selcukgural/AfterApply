"use client";

import { use } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link, useRouter } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";
import type { ApplicationStatus } from "@/types/api";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { StatusChangeSelect } from "@/components/applications/StatusChangeSelect";
import { Timeline } from "@/components/applications/Timeline";
import { JobMatchPanel } from "@/components/applications/JobMatchPanel";
import { Button } from "@/components/ui/Button";

export default function ApplicationDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const t = useTranslations("applications.detail");
  const tCommon = useTranslations("common");
  const tEmploymentType = useTranslations("employmentType");
  const locale = useLocale();
  const router = useRouter();
  const queryClient = useQueryClient();

  const { data: application, isLoading } = useQuery({
    queryKey: ["applications", "detail", id],
    queryFn: () => applicationsApi.getById(id),
  });

  const { data: timeline } = useQuery({
    queryKey: ["applications", "timeline", id],
    queryFn: () => applicationsApi.getTimeline(id),
  });

  const changeStatusMutation = useMutation({
    mutationFn: (variables: { newStatus: ApplicationStatus; note: string | null }) =>
      applicationsApi.changeStatus(id, { ...variables, changedAt: null }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["applications"] });
    },
  });

  const deleteMutation = useMutation({
    mutationFn: () => applicationsApi.remove(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["applications"] });
      router.push("/applications");
    },
  });

  if (isLoading || !application) {
    return <p className="text-sm text-gray-500 dark:text-gray-400">{tCommon("loading")}</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-xl font-semibold text-gray-900 dark:text-gray-100">{application.jobTitle}</h1>
          <p className="text-sm text-gray-600 dark:text-gray-400">{application.companyName}</p>
        </div>
        <div className="flex gap-2">
          <Link href={`/applications/${id}/edit`}>
            <Button variant="secondary">{t("edit")}</Button>
          </Link>
          <Button
            variant="danger"
            onClick={() => {
              if (confirm(t("deleteConfirm"))) {
                deleteMutation.mutate();
              }
            }}
          >
            {t("delete")}
          </Button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-6 md:grid-cols-3">
        <div className="md:col-span-2 flex flex-col gap-4 rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4">
          <div className="flex items-center gap-2">
            <StatusBadge status={application.status} />
          </div>
          <dl className="grid grid-cols-2 gap-3 text-sm">
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("location")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">{application.location ?? t("emptyValue")}</dd>
            </div>
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("employmentType")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">{tEmploymentType(application.employmentType)}</dd>
            </div>
            <div>
              <dt className="text-gray-500 dark:text-gray-400">{t("appliedAt")}</dt>
              <dd className="text-gray-900 dark:text-gray-100">{new Date(application.appliedAt).toLocaleDateString(locale)}</dd>
            </div>
            {application.jobUrl && (
              <div>
                <dt className="text-gray-500 dark:text-gray-400">{t("jobUrl")}</dt>
                <dd>
                  <a href={application.jobUrl} target="_blank" rel="noreferrer" className="text-blue-600 dark:text-blue-400 hover:underline">
                    {t("openLink")}
                  </a>
                </dd>
              </div>
            )}
          </dl>
          {application.notes && (
            <div>
              <p className="text-gray-500 dark:text-gray-400 text-sm">{t("notes")}</p>
              <p className="whitespace-pre-wrap text-sm text-gray-900 dark:text-gray-100">{application.notes}</p>
            </div>
          )}
          <StatusChangeSelect
            currentStatus={application.status}
            isSubmitting={changeStatusMutation.isPending}
            onChangeStatus={async (newStatus, note) => {
              await changeStatusMutation.mutateAsync({ newStatus, note });
            }}
          />
        </div>

        <div className="flex flex-col gap-6">
          <div className="rounded-lg border border-gray-200 dark:border-gray-800 bg-white dark:bg-gray-900 p-4">
            <h2 className="mb-3 text-sm font-semibold text-gray-900 dark:text-gray-100">{t("timeline")}</h2>
            <Timeline events={timeline ?? []} />
          </div>
          <JobMatchPanel applicationId={id} />
        </div>
      </div>
    </div>
  );
}
