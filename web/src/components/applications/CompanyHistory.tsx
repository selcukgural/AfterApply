"use client";

import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { applicationsApi } from "@/lib/api/applications";

const SHOWN = 3;

/**
 * "Your history with this company" under the company field of a new application (canvas
 * "İnce dokunuşlar — Paket 2", 3B): the user's own earlier applications there, newest first, so a
 * second application is made knowing how the first went. Information, not a warning — applying
 * again is fine. Renders nothing for a company they never applied to.
 */
export function CompanyHistory({ companyId }: { companyId: string }) {
  const t = useTranslations("applications.form.companyHistory");
  const tStatus = useTranslations("status");
  const locale = useLocale();

  const { data } = useQuery({
    queryKey: ["applications", "list", { companyId, page: 1, pageSize: SHOWN, sortBy: "AppliedAt", sortDirection: "Descending" }],
    queryFn: () => applicationsApi.getAll({ companyId, page: 1, pageSize: SHOWN, sortBy: "AppliedAt", sortDirection: "Descending" }),
  });

  if (!data || data.totalCount === 0) return null;

  return (
    <div className="flex flex-col gap-2 rounded-lg border border-warn/30 bg-warn-wash/60 px-3.5 py-3 text-sm" role="note">
      <span className="text-xs font-semibold text-warn-ink">{t("title")}</span>
      <ul className="flex flex-col gap-1.5">
        {data.items.map((item) => (
          <li key={item.id} className="flex flex-wrap justify-between gap-x-3">
            <span className="text-gray-900 dark:text-gray-100">
              {item.jobTitle}
              <span className="text-gray-500 dark:text-gray-400">
                {" · "}
                {new Date(item.appliedAt).toLocaleDateString(locale, { month: "long", year: "numeric" })}
              </span>
            </span>
            <span className="text-gray-700 dark:text-gray-300">{tStatus(item.status)}</span>
          </li>
        ))}
      </ul>
      <div className="flex flex-wrap items-center justify-between gap-2 border-t border-warn/20 pt-2 text-xs text-gray-600 dark:text-gray-400">
        <span>{t("reassure")}</span>
        <Link href={`/applications?companyId=${companyId}`} className="font-medium text-accent-ink hover:underline">
          {t("seeAll", { count: data.totalCount })}
        </Link>
      </div>
    </div>
  );
}
