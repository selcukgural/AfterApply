"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";
import { excerpt, spaceBlocks } from "@/lib/dashboard/interviewMorning";
import { cvVersionDate } from "@/lib/applications/cvVersion";
import { otherApplicationsAtCompany, QUICK_CARD_OTHERS } from "@/lib/quickFind/quickFind";
import { safeMailtoUrl } from "@/lib/url/externalLink";
import { StatusBadge } from "@/components/applications/StatusBadge";
import { CopyButton } from "@/components/ui/CopyButton";
import { DaysAgo } from "@/components/ui/DaysAgo";
import { buttonClassName } from "@/components/ui/Button";

/**
 * "Someone from this company is calling" (canvas "İnce dokunuşlar — Paket 5", 2A/2B): everything
 * the user needs in the first ten seconds of a call they did not expect — which role it was, when
 * they applied, the CV that went out, who the HR contact is, their own note, and what the posting
 * said. Shared by the Ctrl+K quick find and the applications search on a phone.
 */
export function QuickCard({ applicationId }: { applicationId: string }) {
  const t = useTranslations("quickFind.card");
  const tStatus = useTranslations("status");
  const locale = useLocale();

  // Same cache entry as the application page, so opening it from here is instant.
  const { data: application } = useQuery({
    queryKey: ["applications", "detail", applicationId],
    queryFn: () => applicationsApi.getById(applicationId),
  });

  const companyId = application?.companyId;
  const othersQuery = { companyId, page: 1, pageSize: QUICK_CARD_OTHERS + 1, sortBy: "AppliedAt", sortDirection: "Descending" } as const;
  const { data: companyApplications } = useQuery({
    queryKey: ["applications", "list", othersQuery],
    queryFn: () => applicationsApi.getAll({ ...othersQuery, companyId: companyId! }),
    enabled: companyId !== undefined,
  });

  // Plain text only, as on the interview-morning card: DOMParser runs no scripts and the text goes
  // out as a React text node.
  const postingText = useMemo(() => {
    const html = application?.jobDescriptionHtml;
    if (!html || typeof DOMParser === "undefined") return "";
    return excerpt(new DOMParser().parseFromString(spaceBlocks(html), "text/html").body.textContent ?? "", 160);
  }, [application?.jobDescriptionHtml]);

  if (!application) {
    return <div className="h-40 animate-pulse rounded-xl bg-gray-100 dark:bg-gray-800" aria-hidden="true" />;
  }

  const hrEmail = safeMailtoUrl(application.hrEmail) ? application.hrEmail!.trim() : null;
  const others = otherApplicationsAtCompany(companyApplications?.items ?? [], application.id);

  return (
    <section aria-label={t("label", { company: application.companyName })} className="flex flex-col gap-3.5 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <div className="flex items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-0.5">
          <span className="text-base font-semibold text-gray-900 dark:text-gray-100">{application.companyName}</span>
          <span className="text-sm text-gray-700 dark:text-gray-300">{application.jobTitle}</span>
        </div>
        <StatusBadge status={application.status} />
      </div>

      <dl className="grid grid-cols-1 gap-x-5 gap-y-2.5 text-sm sm:grid-cols-2">
        <div className="flex min-w-0 flex-col gap-0.5">
          <dt className="text-gray-500 dark:text-gray-400">{t("applied")}</dt>
          <dd className="text-gray-900 dark:text-gray-100">
            <DaysAgo iso={application.appliedAt} locale={locale} />
            {" · "}
            {new Date(application.appliedAt).toLocaleDateString(locale, { day: "numeric", month: "long" })}
          </dd>
        </div>
        {application.cvDocumentFileName && (
          <div className="flex min-w-0 flex-col gap-0.5">
            <dt className="text-gray-500 dark:text-gray-400">{t("cv")}</dt>
            <dd className="truncate text-gray-900 dark:text-gray-100">
              <Link href="/cv" className="text-accent-ink hover:underline">{application.cvDocumentFileName}</Link>
              {application.cvDocumentUploadedAt && (
                <span className="text-gray-500 dark:text-gray-400">
                  {" · "}
                  {t("cvVersion", { date: cvVersionDate(application.cvDocumentUploadedAt, locale) })}
                </span>
              )}
            </dd>
          </div>
        )}
        {(application.hrName || hrEmail) && (
          <div className="flex min-w-0 flex-col gap-0.5">
            <dt className="text-gray-500 dark:text-gray-400">{t("hr")}</dt>
            <dd className="flex min-w-0 items-center gap-1 text-gray-900 dark:text-gray-100">
              <span className="truncate">{[application.hrName, hrEmail].filter(Boolean).join(" · ")}</span>
              {hrEmail && <CopyButton value={hrEmail} label={t("copyHrEmail")} />}
            </dd>
          </div>
        )}
        {application.notes && (
          <div className="flex min-w-0 flex-col gap-0.5">
            <dt className="text-gray-500 dark:text-gray-400">{t("note")}</dt>
            <dd className="text-gray-700 dark:text-gray-300">“{excerpt(application.notes, 90)}”</dd>
          </div>
        )}
      </dl>

      {postingText && (
        <p className="rounded-lg bg-gray-50 px-3 py-2.5 text-sm leading-relaxed text-gray-700 dark:bg-gray-800/60 dark:text-gray-300">
          <span className="font-semibold text-gray-900 dark:text-gray-100">{t("fromPosting")}</span> {postingText}{" "}
          <Link href={`/applications/${application.id}?open=posting`} className="font-medium text-accent-ink hover:underline">
            {t("readPosting")}
          </Link>
        </p>
      )}

      <div className="grid grid-cols-2 gap-2 sm:flex sm:flex-wrap">
        <Link href={`/applications/${application.id}`} className={buttonClassName("primary", "flex min-h-11 items-center justify-center px-3.5 text-sm sm:min-h-9")}>
          {t("open")}
        </Link>
        <Link href={`/applications/${application.id}?open=note`} className={buttonClassName("secondary", "flex min-h-11 items-center justify-center px-3.5 text-sm sm:min-h-9")}>
          {t("addNote")}
        </Link>
      </div>

      {others.length > 0 && (
        <div className="flex flex-col gap-1.5 border-t border-gray-100 pt-3 dark:border-gray-800">
          <span className="text-xs font-semibold uppercase tracking-wide text-gray-500 dark:text-gray-400">{t("others")}</span>
          <ul className="flex flex-col gap-1">
            {others.map((other) => (
              <li key={other.id}>
                <Link href={`/applications/${other.id}`} className="flex justify-between gap-3 rounded-md px-1 py-1 text-sm hover:bg-gray-50 dark:hover:bg-gray-800">
                  <span className="min-w-0 truncate text-gray-900 dark:text-gray-100">
                    {other.jobTitle}
                    <span className="text-gray-500 dark:text-gray-400">
                      {" · "}
                      {new Date(other.appliedAt).toLocaleDateString(locale, { month: "long", year: "numeric" })}
                    </span>
                  </span>
                  <span className="flex-none text-gray-700 dark:text-gray-300">{tStatus(other.status)}</span>
                </Link>
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
