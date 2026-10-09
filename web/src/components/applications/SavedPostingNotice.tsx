"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";
import { savedPostingSearch } from "@/lib/applications/pastedLink";
import { useDebouncedValue } from "@/hooks/useDebouncedValue";
import { DaysAgo } from "@/components/ui/DaysAgo";

/**
 * "You already saved this posting" under the new-application form's posting field (canvas "İnce
 * dokunuşlar — Paket 5", 1A). The extension never creates a second application for the same link;
 * the hand-typed form could, and a second row for one posting splits its history in two. A notice,
 * not a block: a reposted job can be a genuinely new application.
 *
 * The match is the list search's own posting-link match (JobUrlSearchKey), so tracking parameters
 * and LinkedIn's two address shapes find the same row, and it never leaves the user's own list.
 */
export function SavedPostingNotice({ jobUrl }: { jobUrl: string }) {
  const t = useTranslations("applications.form.savedPosting");
  const tStatus = useTranslations("status");
  const locale = useLocale();
  const search = savedPostingSearch(useDebouncedValue(jobUrl, 400));

  const { data } = useQuery({
    queryKey: ["applications", "list", { search, page: 1, pageSize: 1, sortBy: "AppliedAt", sortDirection: "Descending" }],
    queryFn: () => applicationsApi.getAll({ search: search!, page: 1, pageSize: 1, sortBy: "AppliedAt", sortDirection: "Descending" }),
    enabled: search !== null,
  });

  const saved = search !== null ? data?.items[0] : undefined;
  if (!saved) return null;

  return (
    <div role="status" className="flex items-start gap-2.5 rounded-md border border-warn/35 bg-warn-wash px-3 py-2.5 text-sm leading-6">
      <svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" className="mt-1 flex-none text-warn-ink">
        <circle cx="12" cy="12" r="10" />
        <path d="M12 8v4" />
        <path d="M12 16h.01" />
      </svg>
      <div className="flex min-w-0 flex-col gap-0.5">
        <span className="text-gray-900 dark:text-gray-100">
          {t.rich("text", {
            title: saved.jobTitle,
            strong: (chunks) => <strong className="font-semibold">{chunks}</strong>,
          })}
          {" "}
          <span className="text-gray-600 dark:text-gray-400">
            <DaysAgo iso={saved.appliedAt} locale={locale} /> · {tStatus(saved.status)}
          </span>
        </span>
        <span className="flex flex-wrap gap-x-3.5">
          <Link href={`/applications/${saved.id}`} className="font-medium text-accent-ink hover:underline">
            {t("open")}
          </Link>
          <span className="text-gray-600 dark:text-gray-400">{t("reassure")}</span>
        </span>
      </div>
    </div>
  );
}
