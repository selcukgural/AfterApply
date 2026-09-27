"use client";

import { useState } from "react";
import { useTranslations } from "next-intl";
import type { ApplicationStatus, InterviewFormat, RejectionNotice } from "@/types/api";
import { APPLICATION_STATUSES } from "@/lib/constants/applicationStatus";
import {
  asksForPromise,
  asksForRejectionNotice,
  promiseDateBounds,
  REJECTION_NOTICE_OPTIONS,
  todayDateOnly,
} from "@/lib/applications/replyPromise";
import { asksForInterview, INTERVIEW_FORMATS, toInterviewInstant } from "@/lib/applications/interview";
import { suggestedNextStatus } from "@/lib/applications/nextStatus";
import { Select } from "@/components/ui/Select";
import { Input } from "@/components/ui/Input";
import { Button } from "@/components/ui/Button";

/** The optional answers the panel collects beside the new status (design canvas 2026-09-22, "Son
 *  hâl"): a reply date for a status still in play, how a rejection was learned of for Rejected.
 *  Each is null when its question was not asked or left blank. */
export interface StatusChangeExtras {
  promisedReplyBy: string | null;
  rejectionNotice: RejectionNotice | null;
  /** ISO instant of the new stage's interview, when one was given (interview canvas, 2026-09-27). */
  interviewAt: string | null;
  interviewFormat: InterviewFormat | null;
}

interface StatusChangeSelectProps {
  currentStatus: ApplicationStatus;
  onChangeStatus: (newStatus: ApplicationStatus, note: string | null, extras: StatusChangeExtras) => Promise<void>;
  isSubmitting: boolean;
}

export function StatusChangeSelect({ currentStatus, onChangeStatus, isSubmitting }: StatusChangeSelectProps) {
  const t = useTranslations("applications.statusChange");
  const tStatus = useTranslations("status");
  const otherStatuses = APPLICATION_STATUSES.filter((s) => s !== currentStatus);
  const [selected, setSelected] = useState<ApplicationStatus>(() => suggestedNextStatus(currentStatus));
  const [note, setNote] = useState("");
  const [promisedReplyBy, setPromisedReplyBy] = useState("");
  const [rejectionNotice, setRejectionNotice] = useState<RejectionNotice | null>(null);
  const [interviewDate, setInterviewDate] = useState("");
  const [interviewTime, setInterviewTime] = useState("");
  const [interviewFormat, setInterviewFormat] = useState<InterviewFormat>("Online");
  const [isOpen, setIsOpen] = useState(false);

  if (!isOpen) {
    return (
      <Button
        variant="secondary"
        onClick={() => {
          // Re-derived on every open: after a change the status moved on, and the old pick would
          // now be the current status itself.
          setSelected(suggestedNextStatus(currentStatus));
          setIsOpen(true);
        }}
      >
        {t("changeStatus")}
      </Button>
    );
  }

  const showPromise = asksForPromise(selected);
  const showRejectionNotice = asksForRejectionNotice(selected);
  const showInterview = asksForInterview(selected);
  const interviewAt = showInterview ? toInterviewInstant(interviewDate, interviewTime) : null;
  const bounds = promiseDateBounds(todayDateOnly());

  const reset = () => {
    setIsOpen(false);
    setNote("");
    setPromisedReplyBy("");
    setRejectionNotice(null);
    setInterviewDate("");
    setInterviewTime("");
    setInterviewFormat("Online");
  };

  const handleConfirm = async () => {
    // Only what the current status choice actually asked for travels: a date typed while
    // "Interview" was selected must not ride along once the choice became "Rejected".
    await onChangeStatus(selected, note.trim() || null, {
      promisedReplyBy: showPromise && promisedReplyBy ? promisedReplyBy : null,
      rejectionNotice: showRejectionNotice ? rejectionNotice : null,
      interviewAt,
      interviewFormat: interviewAt ? interviewFormat : null,
    });
    reset();
  };

  return (
    <div className="flex flex-col gap-2 rounded-md border border-gray-200 dark:border-gray-800 bg-gray-50 dark:bg-gray-800 p-3">
      <Select aria-label={t("newStatus")} value={selected} onChange={(e) => setSelected(e.target.value as ApplicationStatus)}>
        {otherStatuses.map((status) => (
          <option key={status} value={status}>
            {tStatus(status)}
          </option>
        ))}
      </Select>
      {showRejectionNotice && (
        <fieldset className="flex flex-col gap-2">
          <legend className="pb-1.5 text-sm text-gray-700 dark:text-gray-300">{t("rejectionNoticeLegend")}</legend>
          {REJECTION_NOTICE_OPTIONS.map((option) => {
            const checked = rejectionNotice === option;
            return (
              <label
                key={option}
                className={
                  checked
                    ? "flex min-h-10 cursor-pointer items-center gap-2 rounded-md border-2 border-blue-600 bg-blue-50 px-3 text-sm text-gray-900 dark:border-blue-400 dark:bg-blue-950 dark:text-gray-100"
                    : "flex min-h-10 cursor-pointer items-center gap-2 rounded-md border border-gray-300 bg-white px-3 text-sm text-gray-900 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-100"
                }
              >
                <input
                  type="radio"
                  name="rejection-notice"
                  value={option}
                  checked={checked}
                  onChange={() => setRejectionNotice(option)}
                />
                {t(`rejectionNotice.${option}`)}
              </label>
            );
          })}
        </fieldset>
      )}
      {showInterview && (
        <fieldset className="flex flex-col gap-2 rounded-md border border-accent/30 bg-white p-3 dark:bg-gray-900">
          <legend className="px-1 text-sm text-gray-700 dark:text-gray-300">{t("interviewLegend")}</legend>
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("interviewHint")}</p>
          <div className="flex flex-wrap items-end gap-3">
            <div className="flex flex-col gap-1">
              <label htmlFor="status-change-interview-date" className="text-xs font-medium text-gray-700 dark:text-gray-300">
                {t("interviewDate")}
              </label>
              <div className="w-44">
                <Input
                  id="status-change-interview-date"
                  type="date"
                  value={interviewDate}
                  onChange={(e) => setInterviewDate(e.target.value)}
                />
              </div>
            </div>
            <div className="flex flex-col gap-1">
              <label htmlFor="status-change-interview-time" className="text-xs font-medium text-gray-700 dark:text-gray-300">
                {t("interviewTime")}
              </label>
              <div className="w-32">
                <Input
                  id="status-change-interview-time"
                  type="time"
                  value={interviewTime}
                  onChange={(e) => setInterviewTime(e.target.value)}
                />
              </div>
            </div>
            <InterviewFormatPicker label={t("interviewFormat")} value={interviewFormat} onChange={setInterviewFormat} />
          </div>
        </fieldset>
      )}
      <Input placeholder={t("notePlaceholder")} value={note} onChange={(e) => setNote(e.target.value)} />
      {showPromise && (
        <div className="flex flex-col gap-1">
          <label htmlFor="status-change-promise" className="text-sm text-gray-700 dark:text-gray-300">
            {t("promiseLabel")}
          </label>
          <div className="w-52">
            <Input
              id="status-change-promise"
              type="date"
              min={bounds.min}
              max={bounds.max}
              value={promisedReplyBy}
              onChange={(e) => setPromisedReplyBy(e.target.value)}
            />
          </div>
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("promiseHint")}</p>
        </div>
      )}
      <div className="flex gap-2">
        <Button onClick={handleConfirm} disabled={isSubmitting}>
          {isSubmitting ? t("saving") : t("confirm")}
        </Button>
        <Button variant="secondary" onClick={reset} disabled={isSubmitting}>
          {t("cancel")}
        </Button>
      </div>
    </div>
  );
}

/** Online / In person / Phone as three pressable buttons — the same control on the status panel and
 *  the application page's interview cell. */
export function InterviewFormatPicker({
  label,
  value,
  onChange,
}: {
  label: string;
  value: InterviewFormat;
  onChange: (format: InterviewFormat) => void;
}) {
  const t = useTranslations("applications.interview");
  return (
    <div className="flex flex-col gap-1" role="group" aria-label={label}>
      <span className="text-xs font-medium text-gray-700 dark:text-gray-300">{label}</span>
      <div className="flex gap-1.5">
        {INTERVIEW_FORMATS.map((format) => {
          const pressed = value === format;
          return (
            <button
              key={format}
              type="button"
              aria-pressed={pressed}
              onClick={() => onChange(format)}
              className={
                pressed
                  ? "min-h-10 rounded-md border border-accent bg-accent-wash px-3 text-sm font-medium text-accent-ink"
                  : "min-h-10 rounded-md border border-gray-300 bg-white px-3 text-sm text-gray-700 hover:bg-gray-50 dark:border-gray-700 dark:bg-gray-900 dark:text-gray-300 dark:hover:bg-gray-800"
              }
            >
              {t(`format.${format}`)}
            </button>
          );
        })}
      </div>
    </div>
  );
}
