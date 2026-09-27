"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { ApplicationDetailResponse } from "@/types/api";
import { buttonClassName } from "@/components/ui/Button";
import { offerComparePath } from "@/lib/offerCompare/path";

/**
 * The offer moment (canvas "İnce dokunuşlar — Paket 2", 5A): a light word of congratulations and
 * the one tool worth opening before deciding. The comparison opens empty on purpose — a company
 * name or a salary in its address would end up in logs and history.
 */
export function OfferCard({ application }: { application: ApplicationDetailResponse }) {
  const t = useTranslations("applications.detail.offerCard");
  const locale = useLocale();

  if (application.status !== "Offer") return null;
  const others = application.otherInterviewingCount ?? 0;

  return (
    <section className="flex flex-col gap-3 rounded-xl border border-warn/30 bg-warn-wash p-5 dark:border-warn/40">
      <div className="flex items-center gap-2.5">
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"
          aria-hidden="true" className="size-5 shrink-0 text-warn-ink">
          <path d="M12 3l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.4 6.8 19.1l1-5.8L3.5 9.2l5.9-.9z" />
        </svg>
        <h2 className="text-base font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      </div>
      <p className="text-sm text-gray-700 dark:text-gray-300">{t("body")}</p>
      <Link href={offerComparePath(locale)} className={buttonClassName("primary", "w-fit")}>
        {t("compare")}
      </Link>
      {others > 0 && <p className="text-xs text-gray-600 dark:text-gray-400">{t("others", { count: others })}</p>}
    </section>
  );
}
