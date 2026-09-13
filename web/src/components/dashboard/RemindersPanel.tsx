"use client";

import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { Card, CardHeader } from "@/components/dashboard/Card";
import { Button } from "@/components/ui/Button";
import { useDismissReminder, useReminders } from "@/hooks/useReminders";
import { REMINDER_LABEL_KEY, sortReminders } from "@/lib/dashboard/reminders";
import { formatCount } from "@/lib/dashboard/format";

/**
 * The reminders the API has always generated (a follow-up is due, or an application has gone quiet
 * long enough to count as ghosted), finally shown somewhere. Renders nothing while loading, on
 * error and when there is nothing to nudge about: the board above is the point of the page, and an
 * empty "reminders" card would be a fourth tile saying nothing.
 */
export function RemindersPanel() {
  const t = useTranslations("dashboard.reminders");
  const locale = useLocale();
  const { data } = useReminders();
  const dismiss = useDismissReminder();

  if (!data || data.length === 0) return null;

  const reminders = sortReminders(data);

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
            <Button
              type="button"
              variant="secondary"
              className="px-3 py-1 text-xs"
              disabled={dismiss.isPending && dismiss.variables === reminder.id}
              onClick={() => dismiss.mutate(reminder.id)}
            >
              {t("dismiss")}
            </Button>
          </li>
        ))}
      </ul>
      {dismiss.isError ? <p className="mt-2 text-xs text-red-600 dark:text-red-400">{t("error")}</p> : null}
    </Card>
  );
}
