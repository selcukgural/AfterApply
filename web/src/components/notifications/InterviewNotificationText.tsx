"use client";

import { useLocale, useTranslations } from "next-intl";
import type { InterviewNotificationResponse } from "@/types/api";
import { formatInterviewDate, formatInterviewTime, interviewDayKey } from "@/lib/applications/interview";

/** The sentence of an interview row in the bell and on the notifications page. The day word is the
 *  reader's own calendar, read when the row is drawn, so a row written at dawn still says "today"
 *  in the afternoon. */
export function InterviewNotificationText({ interview: i }: { interview: InterviewNotificationResponse }) {
  const t = useTranslations("notifications.interview");
  const tDay = useTranslations("notifications.interview.day");
  const tStatus = useTranslations("status");
  const locale = useLocale();
  const key = interviewDayKey(i.interviewAt);
  const when = key === "onDate" ? formatInterviewDate(i.interviewAt, locale) : tDay(key);
  const company = <strong className="font-semibold">{i.companyName}</strong>;

  return i.kind === "Upcoming"
    ? t.rich("upcoming", {
        when,
        time: formatInterviewTime(i.interviewAt, locale),
        status: tStatus(i.status),
        company: () => company,
      })
    : t.rich("held", { company: () => company });
}
