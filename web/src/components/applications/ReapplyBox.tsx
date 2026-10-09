"use client";

import { useLocale, useTranslations } from "next-intl";
import type { ApplicationDetailResponse } from "@/types/api";
import { useSetReapplyReminder } from "@/hooks/useReminders";
import { REAPPLY_MONTHS } from "@/lib/applications/reapply";

/**
 * "Remind me to apply here again" under a rejected application's note (canvas "İnce dokunuşlar —
 * Paket 6", 4A). Asked once per rejection: a length sets it, "no need" answers it, and once set it
 * says when and offers to cancel. Shown only on a rejected application, and not again after a "no".
 */
export function ReapplyBox({ application }: { application: ApplicationDetailResponse }) {
  const t = useTranslations("applications.detail.reapply");
  const locale = useLocale();
  const reminder = useSetReapplyReminder();

  if (application.status !== "Rejected") return null;
  const remindAt = application.reapplyRemindAt ?? null;
  if (application.reapplyDecided && !remindAt) return null;

  const set = (months: 3 | 6 | 12 | null) => reminder.mutate({ applicationId: application.id, months });

  return (
    <section className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-white p-4 dark:border-gray-800 dark:bg-gray-900">
      {remindAt ? (
        <div role="status" className="flex flex-wrap items-center justify-between gap-x-4 gap-y-1 text-sm">
          <span className="text-gray-700 dark:text-gray-300">
            {t("set", { date: new Date(remindAt).toLocaleDateString(locale, { day: "numeric", month: "long", year: "numeric" }) })}
          </span>
          <button
            type="button"
            onClick={() => set(null)}
            disabled={reminder.isPending}
            className="font-medium text-accent-ink hover:underline disabled:opacity-50"
          >
            {t("cancel")}
          </button>
        </div>
      ) : (
        <>
          <div className="flex flex-col gap-1">
            <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
            <p className="text-sm leading-6 text-gray-600 dark:text-gray-400">{t("body")}</p>
          </div>
          <div className="flex flex-wrap gap-2">
            {REAPPLY_MONTHS.map((months) => (
              <button
                key={months}
                type="button"
                onClick={() => set(months)}
                disabled={reminder.isPending}
                className="min-h-10 rounded-full border border-gray-300 bg-white px-3.5 text-sm text-gray-900 hover:border-accent hover:text-accent-ink disabled:opacity-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
              >
                {t("months", { count: months })}
              </button>
            ))}
            <button
              type="button"
              onClick={() => set(null)}
              disabled={reminder.isPending}
              className="min-h-10 px-3 text-sm text-gray-600 hover:underline disabled:opacity-50 dark:text-gray-400"
            >
              {t("noNeed")}
            </button>
          </div>
        </>
      )}
      {reminder.isError && <p className="text-xs text-red-600 dark:text-red-400">{reminder.error.message}</p>}
    </section>
  );
}
