"use client";

import { useState } from "react";
import { useLocale, useTranslations } from "next-intl";
import type { ApplicationDetailResponse, InterviewFormat } from "@/types/api";
import {
  asksForInterview,
  formatInterviewDate,
  formatInterviewTime,
  INTERVIEW_WITH_MAX_LENGTH,
  splitInstant,
} from "@/lib/applications/interview";
import { InterviewZoneChoice, interviewInstant } from "@/components/applications/InterviewZoneChoice";
import { AddToCalendar } from "@/components/applications/AddToCalendar";
import { InterviewFormatPicker } from "@/components/applications/StatusChangeSelect";
import { Input } from "@/components/ui/Input";
import { Button, buttonClassName } from "@/components/ui/Button";

interface InterviewFieldProps {
  application: ApplicationDetailResponse;
  isSaving: boolean;
  error: string | null;
  onSave: (interviewAt: string | null, format: InterviewFormat | null, interviewWith: string | null) => Promise<void>;
}

/**
 * The "Görüşme" cell of the details grid: the interview of the current stage with its calendar
 * button, or "+ add" while the application is in a stage that has interviews. The status panel asks
 * the same question when a stage opens; this cell is for the date that arrives later, or moves.
 * Outside an interview stage with nothing recorded it shows nothing.
 */
export function InterviewField({ application, isSaving, error, onSave }: InterviewFieldProps) {
  const t = useTranslations("applications.detail.interview");
  const tInterview = useTranslations("applications.interview");
  const tChange = useTranslations("applications.statusChange");
  const locale = useLocale();
  const [isEditing, setIsEditing] = useState(false);
  const [date, setDate] = useState("");
  const [time, setTime] = useState("");
  const [format, setFormat] = useState<InterviewFormat>("Online");
  const [zone, setZone] = useState<string | null>(null);
  const [withWhom, setWithWhom] = useState("");
  // Read once per mount: whether the interview is behind us only decides between the calendar
  // button and "done", and a page left open across the interview can live with the old answer.
  const [now] = useState(() => Date.now());

  const interviewAt = application.interviewAt ?? null;
  const interviewFormat = application.interviewFormat ?? "Online";
  if (!interviewAt && !asksForInterview(application.status)) return null;

  const startEditing = () => {
    const parts = interviewAt ? splitInstant(interviewAt) : { date: "", time: "" };
    setDate(parts.date);
    setTime(parts.time);
    setFormat(interviewFormat);
    // The stored instant reads back on the reader's own clock; a zone is only for typing a new one.
    setZone(null);
    setWithWhom(application.interviewWith ?? "");
    setIsEditing(true);
  };

  const instant = interviewInstant(date, time, zone);
  const save = async () => {
    await onSave(instant, format, withWhom.trim() || null);
    setIsEditing(false);
  };

  const isPast = interviewAt !== null && new Date(interviewAt).getTime() < now;

  return (
    <div className="col-span-2 min-w-0">
      <dt className="text-gray-500 dark:text-gray-400">{t("label")}</dt>
      <dd className="flex flex-wrap items-center gap-2 text-gray-900 dark:text-gray-100">
        {isEditing ? (
          <div className="flex flex-wrap items-end gap-2">
            <div className="w-40">
              <Input type="date" aria-label={tChange("interviewDate")} value={date} onChange={(e) => setDate(e.target.value)} />
            </div>
            <div className="w-28">
              <Input type="time" aria-label={tChange("interviewTime")} value={time} onChange={(e) => setTime(e.target.value)} />
            </div>
            <InterviewFormatPicker label={tChange("interviewFormat")} value={format} onChange={setFormat} />
            <InterviewZoneChoice companyCountry={application.companyCountry} date={date} time={time} zone={zone} onZoneChange={setZone} />
            <div className="w-full max-w-sm">
              <Input
                aria-label={tChange("interviewWith")}
                placeholder={tChange("interviewWithPlaceholder")}
                maxLength={INTERVIEW_WITH_MAX_LENGTH}
                value={withWhom}
                onChange={(e) => setWithWhom(e.target.value)}
              />
            </div>
            <Button className="px-3 py-1 text-xs" onClick={save} disabled={isSaving || instant === null}>
              {isSaving ? t("saving") : t("save")}
            </Button>
            <Button variant="secondary" className="px-3 py-1 text-xs" onClick={() => setIsEditing(false)} disabled={isSaving}>
              {t("cancel")}
            </Button>
          </div>
        ) : interviewAt ? (
          <>
            <span>
              {formatInterviewDate(interviewAt, locale)}, {formatInterviewTime(interviewAt, locale)}
            </span>
            <span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-700 dark:bg-gray-800 dark:text-gray-300">
              {tInterview(`format.${interviewFormat}`)}
            </span>
            {application.interviewWith && (
              <span className="text-xs text-gray-600 dark:text-gray-400">{t("with", { names: application.interviewWith })}</span>
            )}
            {isPast ? (
              <span className="text-xs text-gray-500 dark:text-gray-400">{t("past")}</span>
            ) : (
              <AddToCalendar
                applicationId={application.id}
                companyName={application.companyName}
                jobTitle={application.jobTitle}
                status={application.status}
                interviewAt={interviewAt}
                format={interviewFormat}
                triggerClassName={buttonClassName("outline", "flex items-center gap-1.5 px-3 py-1 text-xs")}
              />
            )}
            <button type="button" onClick={startEditing} className="text-xs text-blue-600 hover:underline dark:text-blue-400">
              {t("change")}
            </button>
            <button
              type="button"
              onClick={() => onSave(null, null, null)}
              disabled={isSaving}
              className="text-xs text-gray-500 hover:underline disabled:opacity-50 dark:text-gray-400"
            >
              {t("remove")}
            </button>
          </>
        ) : (
          <button type="button" onClick={startEditing} className="text-blue-600 hover:underline dark:text-blue-400">
            {t("add")}
          </button>
        )}
      </dd>
      {error && <p className="mt-1 text-xs text-red-600 dark:text-red-400">{error}</p>}
    </div>
  );
}
