"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Card } from "@/components/dashboard/Card";
import { useDismissReminder, useReapplyReminders, useSetReapplyReminder } from "@/hooks/useReminders";
import { daysAgo } from "@/lib/applications/daysAgo";
import { reapplyOpeningsLink } from "@/lib/applications/reapply";
import { buttonClassName } from "@/components/ui/Button";
import type { ReapplyReminderResponse } from "@/types/api";

/**
 * "You asked me to remind you to apply here again" (canvas "İnce dokunuşlar — Paket 6", 4C): the
 * reminders the user set on rejected applications, once their day has come. Its own card rather
 * than rows on the follow-up list: the application is closed, so the list's bulk answers ("mark as
 * ghosted", "followed up") mean nothing here. Renders nothing until one is due.
 */
export function ReapplyRemindersCard() {
  const t = useTranslations("dashboard.reapply");
  const { data } = useReapplyReminders();
  const dismiss = useDismissReminder();
  const again = useSetReapplyReminder();

  if (!data || data.length === 0) return null;
  const busy = dismiss.isPending || again.isPending;

  return (
    <Card>
      <h3 className="mb-3 text-sm font-medium text-gray-700 dark:text-gray-300">{t("title")}</h3>
      <ul className="flex flex-col divide-y divide-gray-100 dark:divide-gray-800">
        {data.map((reminder) => (
          <ReapplyRow
            key={reminder.id}
            reminder={reminder}
            busy={busy}
            onLater={() => again.mutate({ applicationId: reminder.applicationId, months: 3 })}
            onClose={() => dismiss.mutate(reminder.id)}
          />
        ))}
      </ul>
      {(dismiss.isError || again.isError) && <p className="mt-2 text-xs text-red-600 dark:text-red-400">{t("error")}</p>}
    </Card>
  );
}

function ReapplyRow({ reminder, busy, onLater, onClose }: {
  reminder: ReapplyReminderResponse;
  busy: boolean;
  onLater: () => void;
  onClose: () => void;
}) {
  const t = useTranslations("dashboard.reapply");
  const tReason = useTranslations("emailSuggestions.rejectionReasonCategory");
  const locale = useLocale();
  const openings = reapplyOpeningsLink(reminder);
  const openingsClass = buttonClassName("primary", "flex min-h-9 items-center px-3 text-xs");

  return (
    <li className="flex flex-col gap-2 py-2.5 first:pt-0 last:pb-0">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3">
        <Link href={`/applications/${reminder.applicationId}`} className="min-w-0 truncate text-sm font-medium text-gray-900 hover:underline dark:text-gray-100">
          {reminder.companyName} — {reminder.jobTitle}
        </Link>
        <span className="text-xs text-gray-500 dark:text-gray-400">{t("rejected", { ago: daysAgo(reminder.rejectedAt, locale) })}</span>
      </div>
      <p className="text-xs leading-5 text-gray-600 dark:text-gray-400">
        {t("body")}
        {reminder.rejectionReason && reminder.rejectionReason !== "NotStated" && reminder.rejectionReason !== "Other" && (
          <> {t("reason", { reason: tReason(reminder.rejectionReason) })}</>
        )}
      </p>
      <div className="flex flex-wrap items-center gap-2">
        {openings?.kind === "external" && (
          <a href={openings.href} target="_blank" rel="noopener noreferrer" className={openingsClass}>
            {t("openings")}
          </a>
        )}
        {openings?.kind === "internal" && (
          <Link href={openings.href} className={openingsClass}>
            {t("openings")}
          </Link>
        )}
        <button type="button" onClick={onLater} disabled={busy} className={buttonClassName("outline", "min-h-9 px-3 text-xs")}>
          {t("later")}
        </button>
        <button type="button" onClick={onClose} disabled={busy} className="min-h-9 px-2 text-xs text-gray-600 hover:underline disabled:opacity-50 dark:text-gray-400">
          {t("close")}
        </button>
      </div>
    </li>
  );
}
