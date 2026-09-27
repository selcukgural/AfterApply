"use client";

import { useTranslations } from "next-intl";
import type { ApplicationStatus, InterviewFormat } from "@/types/api";
import { buildIcs, googleCalendarUrl, toCalendarStamp, type CalendarEvent } from "@/lib/applications/interview";
import { DropdownMenu, dropdownItemClassName } from "@/components/ui/DropdownMenu";
import { CalendarIcon } from "@/components/ui/CalendarIcon";

interface AddToCalendarProps {
  applicationId: string;
  companyName: string;
  jobTitle: string;
  status: ApplicationStatus;
  interviewAt: string;
  format: InterviewFormat;
  triggerClassName: string;
}

/**
 * "Add to calendar": Google Calendar's add-event form, or a one-event .ics file for everything
 * else. Both are built in the browser from what the page already has — no server route, no
 * calendar permission, nothing written anywhere the user did not choose to save it. The file is a
 * download of a Blob, not a link to the API, so it needs no token in a URL.
 */
export function AddToCalendar({ applicationId, companyName, jobTitle, status, interviewAt, format, triggerClassName }: AddToCalendarProps) {
  const t = useTranslations("applications.interview");
  const tStatus = useTranslations("status");

  const event: CalendarEvent = {
    uid: `${applicationId}-${toCalendarStamp(interviewAt)}`,
    start: interviewAt,
    title: t("calendarTitle", { status: tStatus(status), company: companyName }),
    description: t("calendarDescription", { jobTitle, format: t(`format.${format}`) }),
  };

  const downloadIcs = () => {
    const blob = new Blob([buildIcs(event)], { type: "text/calendar;charset=utf-8" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = "e-kariyerim-interview.ics";
    link.click();
    URL.revokeObjectURL(url);
  };

  return (
    <DropdownMenu
      title={t("calendarNote")}
      triggerClassName={triggerClassName}
      label={
        <>
          <CalendarIcon />
          {t("addToCalendar")}
        </>
      }
    >
      {(close) => (
        <>
          <a
            href={googleCalendarUrl(event)}
            target="_blank"
            rel="noopener noreferrer"
            onClick={close}
            className={dropdownItemClassName}
          >
            {t("googleCalendar")}
          </a>
          <button
            type="button"
            className={dropdownItemClassName}
            onClick={() => {
              downloadIcs();
              close();
            }}
          >
            {t("icsDownload")}
          </button>
        </>
      )}
    </DropdownMenu>
  );
}
