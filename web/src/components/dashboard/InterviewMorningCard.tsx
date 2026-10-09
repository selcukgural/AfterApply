"use client";

import { useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import type { UpcomingInterviewResponse } from "@/types/api";
import { applicationsApi } from "@/lib/api/applications";
import { useUpcomingInterviews } from "@/hooks/useReminders";
import { useNow } from "@/hooks/useNow";
import { countdown, excerpt, isInterviewMorning, spaceBlocks } from "@/lib/dashboard/interviewMorning";
import { formatInterviewTime } from "@/lib/applications/interview";
import { daysAgo } from "@/lib/applications/daysAgo";
import { safeMailtoUrl } from "@/lib/url/externalLink";
import { AddToCalendar } from "@/components/applications/AddToCalendar";
import { CopyButton } from "@/components/ui/CopyButton";
import { buttonClassName } from "@/components/ui/Button";

/** Two at most: a third interview on the same day is rare, and the card is a morning brief. */
const MAX_CARDS = 2;

/** As many columns as there are tiles, so two tiles do not leave an empty third. */
const TILE_COLUMNS: Record<number, string> = { 1: "sm:grid-cols-1", 2: "sm:grid-cols-2", 3: "sm:grid-cols-3" };

/**
 * The interview-morning card (canvas "İnce dokunuşlar — Paket 2", 1B): on the day of an interview,
 * everything the user may have forgotten about this application in one place — the CV they sent,
 * who the HR contact is, the note they left themselves, and the posting as it read the day they
 * saved it, even if it has since come down. Gone two hours after the start, when the reminders
 * card's "how did it go?" takes over.
 */
export function InterviewMorningCard() {
  const { data: upcoming } = useUpcomingInterviews();
  const now = useNow();
  const today = (upcoming ?? []).filter((interview) => isInterviewMorning(interview.interviewAt, now)).slice(0, MAX_CARDS);

  if (today.length === 0) return null;
  return (
    <>
      {today.map((interview) => (
        <MorningCard key={interview.applicationId} interview={interview} now={now} />
      ))}
    </>
  );
}

function MorningCard({ interview, now }: { interview: UpcomingInterviewResponse; now: Date }) {
  const t = useTranslations("dashboard.interviewMorning");
  const tStatus = useTranslations("status");
  const tInterview = useTranslations("applications.interview");
  const locale = useLocale();

  // Shares the application page's cache entry, so opening the application afterwards is instant.
  const { data: application } = useQuery({
    queryKey: ["applications", "detail", interview.applicationId],
    queryFn: () => applicationsApi.getById(interview.applicationId),
  });

  // Plain text only: DOMParser does not run scripts, and the text goes out as a React text node,
  // so nothing in the stored HTML is ever interpreted.
  const postingText = useMemo(() => {
    const html = application?.jobDescriptionHtml;
    if (!html || typeof DOMParser === "undefined") return "";
    return excerpt(new DOMParser().parseFromString(spaceBlocks(html), "text/html").body.textContent ?? "");
  }, [application?.jobDescriptionHtml]);

  const left = countdown(interview.interviewAt, now);
  const hrEmail = application && safeMailtoUrl(application.hrEmail) ? application.hrEmail!.trim() : null;
  const tiles = application
    ? [
        application.cvDocumentFileName && { key: "cv", label: t("cv"), body: <Link href="/cv" className="font-medium text-accent-ink hover:underline">{application.cvDocumentFileName}</Link> },
        (application.hrName || hrEmail) && {
          key: "hr",
          label: t("hr"),
          body: (
            <span className="flex min-w-0 items-center gap-1 font-medium text-gray-900 dark:text-gray-100">
              <span className="truncate">{application.hrName || hrEmail}</span>
              {hrEmail && <CopyButton value={hrEmail} label={t("copyHrEmail")} />}
            </span>
          ),
        },
        application.notes && { key: "note", label: t("note"), body: <span className="font-medium text-gray-900 dark:text-gray-100">“{excerpt(application.notes, 90)}”</span> },
      ].filter((tile): tile is { key: string; label: string; body: React.ReactElement } => Boolean(tile))
    : [];

  return (
    <section className="flex flex-col gap-3.5 rounded-xl border border-accent/30 bg-white p-5 dark:border-accent/40 dark:bg-gray-900">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex min-w-0 flex-col gap-1">
          <span className="text-xs font-semibold uppercase tracking-wide text-accent-ink">
            {t("when", {
              time: formatInterviewTime(interview.interviewAt, locale),
              left: left.kind === "started" ? t("started") : t("left", { hours: left.hours, minutes: left.minutes }),
            })}
          </span>
          <Link href={`/applications/${interview.applicationId}`} className="text-base font-semibold text-gray-900 hover:underline dark:text-gray-100">
            {interview.companyName} — {interview.jobTitle}
          </Link>
          <span className="text-sm text-gray-500 dark:text-gray-400">
            {tStatus(interview.status)} · {tInterview(`format.${interview.format}`)}
            {interview.interviewWith && <> · {t("with", { names: interview.interviewWith })}</>}
            {application && <> · {t("applied", { ago: daysAgo(application.appliedAt, locale) })}</>}
          </span>
        </div>
        <AddToCalendar
          applicationId={interview.applicationId}
          companyName={interview.companyName}
          jobTitle={interview.jobTitle}
          status={interview.status}
          interviewAt={interview.interviewAt}
          format={interview.format}
          triggerClassName={buttonClassName("primary", "flex items-center gap-1.5 px-3 py-1.5 text-sm")}
        />
      </div>

      {tiles.length > 0 && (
        <div className={`grid grid-cols-1 gap-2.5 ${TILE_COLUMNS[tiles.length]}`}>
          {tiles.map((tile) => (
            <div key={tile.key} className="flex min-w-0 flex-col gap-1 rounded-lg bg-gray-50 px-3 py-2.5 text-sm dark:bg-gray-800/60">
              <span className="text-xs text-gray-500 dark:text-gray-400">{tile.label}</span>
              {tile.body}
            </div>
          ))}
        </div>
      )}

      {postingText && (
        <div className="flex flex-col gap-2 border-t border-gray-100 pt-3 dark:border-gray-800">
          <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
            <span className="text-sm font-semibold text-gray-700 dark:text-gray-300">{t("posting")}</span>
            <span className="text-xs text-gray-500 dark:text-gray-400">{t("saved")}</span>
          </div>
          <p className="text-sm leading-relaxed text-gray-700 dark:text-gray-300">{postingText}</p>
          <div className="flex gap-4 text-xs font-medium">
            <Link href={`/applications/${interview.applicationId}?open=posting`} className="text-accent-ink hover:underline">
              {t("readAll")}
            </Link>
            <Link href={`/applications/${interview.applicationId}`} className="text-gray-500 hover:underline dark:text-gray-400">
              {t("goTo")}
            </Link>
          </div>
        </div>
      )}
    </section>
  );
}
