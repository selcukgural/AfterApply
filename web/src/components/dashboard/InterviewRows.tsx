"use client";

import { useState, type ReactNode } from "react";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type {
  ApplicationStatus,
  InterviewOutcomeRequest,
  InterviewOutcomeResponse,
  ReminderResponse,
  UpcomingInterviewResponse,
} from "@/types/api";
import {
  formatInterviewDate,
  formatInterviewTime,
  interviewDayKey,
  nextStagesAfter,
  snoozeOptionsFor,
  snoozedUntil,
} from "@/lib/applications/interview";
import { formatPromiseDate, todayDateOnly } from "@/lib/applications/replyPromise";
import type { SnoozeDays } from "@/lib/api/reminders";
import { AddToCalendar } from "@/components/applications/AddToCalendar";
import { DropdownMenu, dropdownItemClassName } from "@/components/ui/DropdownMenu";
import { buttonClassName } from "@/components/ui/Button";

const smallButton = "flex items-center gap-1.5 px-3 py-1 text-xs";
const chip =
  "min-h-9 rounded-full border border-accent/40 bg-white px-3.5 text-sm text-accent-ink hover:bg-accent-wash disabled:opacity-50 dark:bg-gray-900 dark:hover:bg-accent-wash";

/** "yarın, 14:00" / "28 Eyl Pzt, 14:00" — the day word when there is one, the date otherwise. */
function useWhen() {
  const t = useTranslations("applications.interview");
  const locale = useLocale();
  return (iso: string) => {
    const key = interviewDayKey(iso);
    return {
      key,
      when: key === "onDate" ? formatInterviewDate(iso, locale) : t(`day.${key}`),
      time: formatInterviewTime(iso, locale),
    };
  };
}

/**
 * An interview still to come, at the top of the card (design canvas "Seçilen: A"). Not a reminder
 * row: nothing to answer, nothing to select — the calendar and a way to the application to change
 * the date.
 */
export function UpcomingInterviewRow({ interview }: { interview: UpcomingInterviewResponse }) {
  const t = useTranslations("dashboard.reminders");
  const tInterview = useTranslations("applications.interview");
  const tStatus = useTranslations("status");
  const whenOf = useWhen();
  const { key, when, time } = whenOf(interview.interviewAt);
  const badge = key === "today" || key === "tomorrow" ? t(`upcomingBadge.${key}`) : t("upcomingBadge.onDate", { date: when });

  return (
    <li className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2 py-2.5 first:pt-0">
      <div className="flex min-w-0 flex-col gap-0.5 pl-7">
        <Link
          href={`/applications/${interview.applicationId}`}
          className="truncate text-sm font-medium text-gray-900 hover:underline dark:text-gray-100"
        >
          {interview.companyName} — {interview.jobTitle}
        </Link>
        <p className="text-xs text-gray-500 dark:text-gray-400">
          <span className="font-medium text-accent-ink">{badge}</span>
          {" · "}
          {/* The badge already names the day; the line only adds the hour. */}
          {tStatus(interview.status)} · {time} ·{" "}
          {tInterview(`format.${interview.format}`)}
        </p>
      </div>
      <div className="flex items-center gap-2">
        <AddToCalendar
          applicationId={interview.applicationId}
          companyName={interview.companyName}
          jobTitle={interview.jobTitle}
          status={interview.status}
          interviewAt={interview.interviewAt}
          format={interview.format}
          triggerClassName={buttonClassName("primary", smallButton)}
        />
        <Link href={`/applications/${interview.applicationId}`} className={buttonClassName("secondary", "px-3 py-1 text-xs")}>
          {t("change")}
        </Link>
      </div>
    </li>
  );
}

/** "Mülakat · dün, 10:30" under an InterviewHeld row's title. */
export function InterviewHeldMeta({ reminder }: { reminder: ReminderResponse }) {
  const t = useTranslations("dashboard.reminders");
  const tStatus = useTranslations("status");
  const whenOf = useWhen();
  if (!reminder.interviewAt) return <>{t("interviewHeld")}</>;
  const { when, time } = whenOf(reminder.interviewAt);
  return (
    <>
      <span className="font-medium text-accent-ink">{t("interviewHeld")}</span>
      {" · "}
      {t("interviewWas", {
        status: reminder.applicationStatus ? tStatus(reminder.applicationStatus) : "",
        when,
        time,
      })}
    </>
  );
}

/** The reply-date quick picks: days from today, and "they didn't say". */
const WAIT_OPTIONS = [3, 7, 14] as const;

function shiftedDateOnly(days: number): string {
  const now = new Date();
  return todayDateOnly(new Date(now.getFullYear(), now.getMonth(), now.getDate() + days));
}

/**
 * "How did it go?" under an InterviewHeld row: three answers, the first two of which ask one more
 * thing — which stage, and whether they gave a date. A second step rather than a guess: "moved on"
 * after a first interview is a technical round for one company and an offer for another, and the
 * stage timings are what the response-rate figures are built from.
 */
export function InterviewQuestion({
  reminder,
  busy,
  onAnswer,
}: {
  reminder: ReminderResponse;
  busy: boolean;
  onAnswer: (request: InterviewOutcomeRequest) => void;
}) {
  const t = useTranslations("dashboard.reminders");
  const tStatus = useTranslations("status");
  const [step, setStep] = useState<"choose" | "next" | "wait">("choose");

  const back = (
    <button type="button" onClick={() => setStep("choose")} className="text-xs text-accent-ink hover:underline">
      {t("back")}
    </button>
  );

  return (
    <div className="mt-2 flex flex-col gap-2 rounded-lg bg-accent-wash p-3 sm:ml-7">
      {step === "choose" ? (
        <>
          <span className="text-sm font-medium text-accent-ink">{t("howDidItGo")}</span>
          <div className="flex flex-wrap gap-2">
            <button type="button" className={chip} disabled={busy} onClick={() => setStep("next")}>
              {t("answerNext")}
            </button>
            <button type="button" className={chip} disabled={busy} onClick={() => setStep("wait")}>
              {t("answerWaiting")}
            </button>
            <button type="button" className={chip} disabled={busy} onClick={() => onAnswer({ outcome: "Rejected" })}>
              {t("answerRejected")}
            </button>
          </div>
        </>
      ) : step === "next" ? (
        <>
          <span className="flex items-baseline justify-between gap-3 text-sm font-medium text-accent-ink">
            {t("whichStage")}
            {back}
          </span>
          <div className="flex flex-wrap gap-2">
            {nextStagesAfter(reminder.applicationStatus).map((status: ApplicationStatus) => (
              <button
                key={status}
                type="button"
                className={chip}
                disabled={busy}
                onClick={() => onAnswer({ outcome: "NextStage", nextStatus: status })}
              >
                {tStatus(status)}
              </button>
            ))}
          </div>
        </>
      ) : (
        <>
          <span className="flex items-baseline justify-between gap-3 text-sm font-medium text-accent-ink">
            {t("whenReply")}
            {back}
          </span>
          <div className="flex flex-wrap gap-2">
            {WAIT_OPTIONS.map((days) => (
              <button
                key={days}
                type="button"
                className={chip}
                disabled={busy}
                onClick={() => onAnswer({ outcome: "Waiting", promisedReplyBy: shiftedDateOnly(days) })}
              >
                {days < 7 ? t("waitDays", { count: days }) : t("waitWeeks", { count: days / 7 })}
              </button>
            ))}
            <button type="button" className={chip} disabled={busy} onClick={() => onAnswer({ outcome: "Waiting" })}>
              {t("waitUnknown")}
            </button>
          </div>
        </>
      )}
    </div>
  );
}

/**
 * What an answer did, in the row's place, with its undo (canvas: the green strip). Kept by the card
 * after the row itself has left the server's list, until the user undoes it or leaves the page.
 */
export function InterviewAnsweredRow({
  reminder,
  outcome,
  busy,
  onUndo,
}: {
  reminder: ReminderResponse;
  outcome: InterviewOutcomeResponse;
  busy: boolean;
  onUndo: () => void;
}) {
  const t = useTranslations("dashboard.reminders");
  const tStatus = useTranslations("status");
  const locale = useLocale();

  let title: string;
  let hint: ReactNode;
  if (outcome.toStatus === "Rejected") {
    title = t("doneRejected");
    hint = (
      <>
        {t("doneRejectedHint")}{" "}
        <Link href="/contribute?tab=experience" className="font-medium underline underline-offset-2">
          {t("shareAfterGhostLink")}
        </Link>
      </>
    );
  } else if (outcome.toStatus) {
    title = t("doneStatus", { status: tStatus(outcome.toStatus) });
    hint = (
      <>
        {t("doneStatusHint")}{" "}
        <Link href={`/applications/${reminder.applicationId}`} className="font-medium underline underline-offset-2">
          {t("doneStatusLink")}
        </Link>
      </>
    );
  } else if (outcome.promisedReplyBy) {
    title = t("donePromise", { date: formatPromiseDate(outcome.promisedReplyBy, locale) });
    hint = t("donePromiseHint");
  } else {
    title = t("doneWaiting");
    hint = t("doneWaitingHint");
  }

  return (
    <li className="py-2.5 first:pt-0">
      <p className="truncate pl-7 text-sm font-medium text-gray-900 dark:text-gray-100">
        {reminder.companyName} — {reminder.jobTitle}
      </p>
      <div className="mt-2 flex items-center justify-between gap-3 rounded-lg bg-good-wash px-3 py-2.5 sm:ml-7">
        <div className="flex flex-col gap-0.5">
          <span className="text-sm font-medium text-good-ink">{title}</span>
          <span className="text-xs text-gray-600 dark:text-gray-300">{hint}</span>
        </div>
        <button
          type="button"
          onClick={onUndo}
          disabled={busy}
          className="shrink-0 px-1.5 py-1 text-sm font-medium text-good-ink hover:underline disabled:opacity-50"
        >
          {t("undo")}
        </button>
      </div>
    </li>
  );
}

/** "Snooze ▾" on a row: a few fixed lengths, shorter ones on an interview question. */
export function SnoozeMenu({
  reminder,
  disabled,
  onSnooze,
}: {
  reminder: ReminderResponse;
  disabled: boolean;
  onSnooze: (days: SnoozeDays) => void;
}) {
  const t = useTranslations("dashboard.reminders");
  const locale = useLocale();
  const interview = reminder.type === "InterviewHeld";
  const [now] = useState(() => new Date());

  return (
    <DropdownMenu
      title={t("snoozeTitle")}
      disabled={disabled}
      triggerClassName={buttonClassName("secondary", smallButton)}
      label={
        <>
          {t("snooze")}
          <svg className="h-2.5 w-2.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <path d="m6 9 6 6 6-6" />
          </svg>
        </>
      }
    >
      {(close) =>
        snoozeOptionsFor(reminder.type).map((days) => (
          <button
            key={days}
            type="button"
            className={dropdownItemClassName}
            onClick={() => {
              close();
              onSnooze(days);
            }}
          >
            <span>
              {interview
                ? days === 1
                  ? t("snoozeAskTomorrow")
                  : t("snoozeAskIn", { count: days })
                : t("snoozeFor", { count: days })}
            </span>
            <span className="text-xs text-gray-500 dark:text-gray-400">
              {snoozedUntil(days, now).toLocaleDateString(locale, { day: "numeric", month: "short" })}
            </span>
          </button>
        ))
      }
    </DropdownMenu>
  );
}
