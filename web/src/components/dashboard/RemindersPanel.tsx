"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Card, CardHeader } from "@/components/dashboard/Card";
import { Button } from "@/components/ui/Button";
import { useDismissReminder, useFollowUpReminder, useMarkReminderGhosted, useReminders } from "@/hooks/useReminders";
import { useStaleSummary } from "@/hooks/useStaleApplications";
import { REMINDER_ANSWER_KEY, REMINDER_LABEL_KEY, sortReminders } from "@/lib/dashboard/reminders";
import { formatCount } from "@/lib/dashboard/format";
import type { ReminderResponse } from "@/types/api";

/**
 * The reminders the API has always generated (a follow-up is due, or an application has gone quiet
 * long enough to count as ghosted), finally shown somewhere. Renders nothing while loading, on
 * error and when there is nothing to nudge about: the board above is the point of the page, and an
 * empty "reminders" card would be a fourth tile saying nothing.
 *
 * Every row carries the answer its question asks for — "followed up" or "mark as ghosted" — with
 * "dismiss" second. The list is bounded by the scan's horizon (nothing older than
 * Notifications:StaleThresholdDays gets a row), so it never grows into an import; that batch is
 * StaleApplicationsBanner's question, and the footer here only points at the bulk tools once the
 * user has said "not now" to it.
 */
export function RemindersPanel() {
  const t = useTranslations("dashboard.reminders");
  const locale = useLocale();
  const { data } = useReminders();
  const { data: stale } = useStaleSummary();
  const dismiss = useDismissReminder();
  const followUp = useFollowUpReminder();
  const markGhosted = useMarkReminderGhosted();

  if (!data || data.length === 0) return null;

  const reminders = sortReminders(data);
  const isBusy = (reminder: ReminderResponse) =>
    (dismiss.isPending && dismiss.variables === reminder.id) ||
    (followUp.isPending && followUp.variables === reminder.id) ||
    (markGhosted.isPending && markGhosted.variables?.id === reminder.id);
  const answer = (reminder: ReminderResponse) =>
    reminder.type === "FollowUp" ? followUp.mutate(reminder.id) : markGhosted.mutate(reminder);
  const hasError = dismiss.isError || followUp.isError || markGhosted.isError;
  const showStaleNote = stale !== undefined && stale.count > 0 && !stale.suggest;

  return (
    <Card>
      <CardHeader title={t("title")} hint={t("count", { count: formatCount(reminders.length, locale) })} />
      <ul className="flex flex-col divide-y divide-gray-100 dark:divide-gray-800">
        {reminders.map((reminder) => (
          <li key={reminder.id} className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 py-2.5 first:pt-0 last:pb-0">
            <div className="flex min-w-0 flex-col gap-0.5">
              <Link
                href={`/applications/${reminder.applicationId}`}
                className="truncate text-sm font-medium text-gray-900 hover:underline dark:text-gray-100"
              >
                {reminder.companyName} — {reminder.jobTitle}
              </Link>
              <p className="text-xs text-gray-500 dark:text-gray-400">
                {t(REMINDER_LABEL_KEY[reminder.type])} · {t("days", { count: formatCount(reminder.daysElapsed, locale) })}
              </p>
            </div>
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                className="px-3 py-1 text-xs"
                disabled={isBusy(reminder)}
                onClick={() => answer(reminder)}
              >
                {t(REMINDER_ANSWER_KEY[reminder.type])}
              </Button>
              <Button
                type="button"
                variant="secondary"
                className="px-3 py-1 text-xs"
                disabled={isBusy(reminder)}
                onClick={() => dismiss.mutate(reminder.id)}
              >
                {t("dismiss")}
              </Button>
            </div>
          </li>
        ))}
      </ul>
      {hasError ? <p className="mt-2 text-xs text-red-600 dark:text-red-400">{t("error")}</p> : null}
      {showStaleNote ? (
        <div className="mt-3 flex flex-wrap items-center justify-between gap-x-3 gap-y-1 border-t border-gray-100 pt-3 dark:border-gray-800">
          <span className="text-xs text-gray-500 dark:text-gray-400">
            {t("staleNote", { count: stale.count, threshold: stale.thresholdDays })}
          </span>
          <Link href="/applications?status=Applied" className="text-xs font-medium text-accent-ink hover:underline">
            {t("staleBulkLink")}
          </Link>
        </div>
      ) : null}
    </Card>
  );
}
