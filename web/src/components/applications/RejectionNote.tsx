"use client";

import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { ApplicationDetailResponse, ApplicationStatusHistoryResponse } from "@/types/api";
import { latestRejectionReason, REJECTION_TIPS } from "@/lib/applications/rejectionNote";

const bold = (chunks: React.ReactNode) => <strong className="font-semibold">{chunks}</strong>;

/**
 * The calm note on a rejected application (canvas "İnce dokunuşlar — Paket 2", 4A): the reason the
 * company gave, and — when it keeps coming up in the user's own rejections — that it does, with
 * one thing worth checking next time. Says what happened; no consolation, no celebration. Shown
 * only when there is a reason or a pattern to tell.
 */
export function RejectionNote({
  application,
  statusHistory,
}: {
  application: ApplicationDetailResponse;
  statusHistory: ApplicationStatusHistoryResponse[] | undefined;
}) {
  const t = useTranslations("applications.detail.rejectionNote");
  const tReason = useTranslations("emailSuggestions.rejectionReasonCategory");

  if (application.status !== "Rejected") return null;
  const reason = latestRejectionReason(statusHistory);
  const pattern =
    application.rejectionPatternCategory && application.rejectionPatternCount && application.rejectionPatternOutOf
      ? { category: application.rejectionPatternCategory, count: application.rejectionPatternCount, outOf: application.rejectionPatternOutOf }
      : null;
  if (!reason && !pattern) return null;

  const tip = pattern ? REJECTION_TIPS[pattern.category] : undefined;

  return (
    <section className="flex flex-col gap-2.5 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      <p className="text-sm text-gray-800 dark:text-gray-200">
        {reason ? t.rich("reason", { reason: tReason(reason), b: bold }) : t("closed")}
      </p>
      {pattern && (
        <div className="flex flex-col gap-1.5 rounded-md bg-gray-50 px-3.5 py-3 dark:bg-gray-800/60">
          <span className="text-sm text-gray-800 dark:text-gray-200">
            {t.rich("pattern", { count: pattern.count, outOf: pattern.outOf, reason: tReason(pattern.category), b: bold })}
          </span>
          {tip && <span className="text-xs text-gray-600 dark:text-gray-400">{t(`tips.${tip}`)}</span>}
          <Link href="/applications?status=Rejected" className="text-xs font-medium text-accent-ink hover:underline">
            {t("seeRejected")}
          </Link>
        </div>
      )}
    </section>
  );
}
