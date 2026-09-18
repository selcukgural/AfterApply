"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { BREAK_LENGTHS, type BreakLength } from "@/lib/api/reminders";
import { useEndBreak, useReminderBreak, useStartBreak } from "@/hooks/useReminderBreak";
import { ApiError } from "@/lib/api/httpClient";
import { Button } from "@/components/ui/Button";

/**
 * "Take a break": a week, a fortnight or a month with the reminders card and the stale question
 * off the dashboard (T5). Lives on the profile — the calm page — rather than in settings with the
 * extension key and the delete button, because it is about the person, not the account. The card
 * says what a break does and does not do in one sentence; it never argues for or against taking
 * one.
 */
export function BreakCard() {
  const t = useTranslations("profile.break");
  const locale = useLocale();
  const { data: pause } = useReminderBreak();
  const start = useStartBreak();
  const end = useEndBreak();
  const [error, setError] = useState<string | null>(null);

  const describeError = (e: unknown) => (e instanceof ApiError ? e.message : t("error"));
  const busy = start.isPending || end.isPending;

  let body;
  if (pause?.state === "Paused") {
    const until = new Intl.DateTimeFormat(locale, { dateStyle: "long" }).format(new Date(pause.pausedUntil!));
    body = (
      <>
        <p className="text-sm text-gray-700 dark:text-gray-300">{t("paused", { date: until })}</p>
        <Button
          variant="secondary"
          className="self-start px-3 py-1.5 text-sm"
          disabled={busy}
          onClick={() => end.mutate(undefined, { onSuccess: () => setError(null), onError: (e) => setError(describeError(e)) })}
        >
          {t("end")}
        </Button>
      </>
    );
  } else if (pause?.state === "Returned") {
    body = (
      <>
        <p className="text-sm text-gray-700 dark:text-gray-300">{t("returned")}</p>
        <Link href="/dashboard" className="self-start text-sm font-medium text-accent-ink underline-offset-2 hover:underline">
          {t("goToDashboard")}
        </Link>
      </>
    );
  } else {
    body = (
      <>
        <p className="text-sm text-gray-600 dark:text-gray-400">{t("body")}</p>
        <div className="flex flex-wrap gap-2">
          {BREAK_LENGTHS.map((days: BreakLength) => (
            <Button
              key={days}
              variant="secondary"
              className="px-3 py-1.5 text-sm"
              disabled={busy}
              onClick={() => start.mutate(days, { onSuccess: () => setError(null), onError: (e) => setError(describeError(e)) })}
            >
              {t(`length.${days}`)}
            </Button>
          ))}
        </div>
      </>
    );
  }

  return (
    <section
      aria-label={t("title")}
      className="flex flex-col gap-3 rounded-xl border border-gray-200 bg-white px-6 py-4 dark:border-gray-800 dark:bg-gray-900"
    >
      <h2 className="text-sm font-semibold text-gray-900 dark:text-gray-100">{t("title")}</h2>
      {body}
      {error ? <p className="text-sm text-red-600 dark:text-red-400">{error}</p> : null}
    </section>
  );
}
