"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";
import { trackedJobsApi } from "@/lib/api/trackedJobs";
import { cvDocumentsApi } from "@/lib/api/cvDocuments";

/**
 * Three counts that say how much of the product the account holds: applications, tracked jobs,
 * CVs. Each is a link to its list. Counts come from the lists' own endpoints — the applications
 * summary the dashboard already reads, the tracked-jobs and CV lists — so there is nothing new
 * to keep in step on the server.
 */
export function ActivityTiles() {
  const t = useTranslations("profile.activity");
  const applications = useQuery({ queryKey: ["applications", "summary"], queryFn: applicationsApi.getSummary });
  const trackedJobs = useQuery({ queryKey: ["trackedJobs"], queryFn: trackedJobsApi.getAll });
  const cvs = useQuery({ queryKey: ["cvDocuments"], queryFn: cvDocumentsApi.list });

  const tiles: { href: string; label: string; value: number | undefined }[] = [
    { href: "/applications", label: t("applications"), value: applications.data?.total },
    { href: "/tracked-jobs", label: t("trackedJobs"), value: trackedJobs.data?.length },
    { href: "/cv", label: t("cvs"), value: cvs.data?.items.length },
  ];

  return (
    <div className="grid grid-cols-3 gap-3" aria-label={t("title")}>
      {tiles.map((tile) => (
        <Link
          key={tile.href}
          href={tile.href}
          className="flex flex-col gap-0.5 rounded-xl border border-gray-200 bg-white px-4 py-3 hover:border-accent/50 dark:border-gray-800 dark:bg-gray-900"
        >
          <span className="text-xl font-semibold leading-tight text-gray-900 tabular-nums dark:text-gray-100">
            {tile.value ?? "–"}
          </span>
          <span className="text-xs text-gray-500 dark:text-gray-400">{tile.label}</span>
        </Link>
      ))}
    </div>
  );
}
