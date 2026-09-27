"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { ApplicationDetailResponse } from "@/types/api";
import { applicationsApi } from "@/lib/api/applications";
import { buildReminderBox } from "@/lib/applications/reminderBox";
import { daysAgo } from "@/lib/applications/daysAgo";

const bold = (chunks: React.ReactNode) => <strong className="font-semibold">{chunks}</strong>;

function Icon({ path, tone }: { path: React.ReactNode; tone: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
      aria-hidden="true" className={`mt-0.5 size-4 shrink-0 ${tone}`}>
      {path}
    </svg>
  );
}

/**
 * "Hatırlatayım" (canvas "İnce dokunuşlar — Paket 2", 2B): the things about this application the
 * user may have forgotten and the app did not — how long companies usually take to answer them,
 * how old the posting already was, that it has since come down, and that they applied to this
 * company before. Renders nothing when none of it applies.
 */
export function ReminderBox({ application }: { application: ApplicationDetailResponse }) {
  const t = useTranslations("applications.detail.reminderBox");
  const tStatus = useTranslations("status");
  const locale = useLocale();

  // The user's own applications at this company, through the list's company filter — the same
  // query the company view's "show all" link opens, so it is already scoped to the caller.
  const { data: sameCompany } = useQuery({
    queryKey: ["applications", "list", { companyId: application.companyId, page: 1, pageSize: 10, sortBy: "AppliedAt", sortDirection: "Descending" }],
    queryFn: () =>
      applicationsApi.getAll({ companyId: application.companyId, page: 1, pageSize: 10, sortBy: "AppliedAt", sortDirection: "Descending" }),
  });

  const box = buildReminderBox(application, sameCompany?.items ?? []);
  if (!box) return null;

  return (
    <section className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <h2 className="text-sm font-semibold text-gray-700 dark:text-gray-300">{t("title")}</h2>
      <ul className="flex flex-col gap-2.5 text-sm text-gray-800 dark:text-gray-200">
        {box.closed && (
          <li className="flex gap-2.5">
            <Icon tone="text-crit-ink" path={<><rect x="4" y="11" width="16" height="10" rx="2" /><path d="M8 11V7a4 4 0 0 1 8 0v4" /></>} />
            <span>
              {t.rich(box.closed.noReply ? "closedNoReply" : "closed", {
                when: daysAgo(application.jobClosedAt!, locale),
                b: bold,
              })}
            </span>
          </li>
        )}
        {box.patience && (
          <li className="flex gap-2.5">
            <Icon tone={box.patience.early ? "text-good-ink" : "text-muted-ink"} path={<><circle cx="12" cy="12" r="9" /><path d="M12 7v5l3 2" /></>} />
            <div className="flex flex-1 flex-col gap-1.5">
              <span>{t.rich(box.patience.early ? "patienceEarly" : "patienceLate", { day: box.patience.day, median: box.patience.median, b: bold })}</span>
              <div
                role="progressbar"
                aria-valuemin={0}
                aria-valuemax={box.patience.median}
                aria-valuenow={Math.min(box.patience.day, box.patience.median)}
                className="h-1.5 w-full max-w-xs rounded-full bg-track"
              >
                <div
                  className={`h-1.5 rounded-full ${box.patience.early ? "bg-good" : "bg-muted"}`}
                  style={{ width: `${Math.round(box.patience.progress * 100)}%` }}
                />
              </div>
            </div>
          </li>
        )}
        {box.postingAgeDays !== null && (
          <li className="flex gap-2.5">
            <Icon tone="text-muted-ink" path={<><rect x="4" y="5" width="16" height="15" rx="2" /><path d="M4 10h16M9 3v4M15 3v4" /></>} />
            <span>{t.rich("postingAge", { days: box.postingAgeDays, b: bold })}</span>
          </li>
        )}
        {box.earlier && (
          <li className="flex gap-2.5">
            <Icon tone="text-warn-ink" path={<><path d="M3 12a9 9 0 1 0 3-6.7" /><path d="M3 4v5h5" /></>} />
            <span>
              {t.rich("earlier", {
                title: box.earlier.jobTitle,
                month: new Date(box.earlier.appliedAt).toLocaleDateString(locale, { month: "long", year: "numeric" }),
                status: tStatus(box.earlier.status),
                b: bold,
              })}{" "}
              <Link href={`/applications/${box.earlier.id}`} className="font-medium text-accent-ink hover:underline">
                {t("earlierLink")}
              </Link>
            </span>
          </li>
        )}
      </ul>
    </section>
  );
}
